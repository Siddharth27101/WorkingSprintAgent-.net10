using System.IO.Compression;
using System.Security;
using System.Text;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Creates a standards-based PowerPoint package using only the .NET runtime.
/// </summary>
public class PowerPointPresentationService
{
    private const long SlideWidth = 10_058_400;
    private const long SlideHeight = 7_534_800;
    private readonly ILogger<PowerPointPresentationService> _logger;

    public PowerPointPresentationService(ILogger<PowerPointPresentationService> logger)
    {
        _logger = logger;
    }

    public byte[] CreatePresentationFromTemplate(
        SprintMetrics metrics,
        SprintInsights insights,
        PresentationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(insights);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        var slides = CreateSlides(metrics, insights, options);
        var theme = GetTheme(options.Template);
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteEntry(archive, "[Content_Types].xml", BuildContentTypes(slides.Count));
            WriteEntry(archive, "_rels/.rels", PackageRelationships);
            WriteEntry(archive, "ppt/presentation.xml", BuildPresentation(slides.Count));
            WriteEntry(archive, "ppt/_rels/presentation.xml.rels", BuildPresentationRelationships(slides.Count));
            WriteEntry(archive, "ppt/slideMasters/slideMaster1.xml", SlideMaster);
            WriteEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", SlideMasterRelationships);
            WriteEntry(archive, "ppt/slideLayouts/slideLayout1.xml", SlideLayout);
            WriteEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", SlideLayoutRelationships);
            WriteEntry(archive, "ppt/theme/theme1.xml", Theme);

            for (var index = 0; index < slides.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var slideNumber = index + 1;
                WriteEntry(archive, $"ppt/slides/slide{slideNumber}.xml", BuildSlide(slides[index], theme));
                WriteEntry(archive, $"ppt/slides/_rels/slide{slideNumber}.xml.rels", SlideRelationships);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var bytes = stream.ToArray();
        _logger.LogInformation(
            "Created {SlideCount}-slide PowerPoint presentation for sprint '{SprintName}' ({Size} bytes)",
            slides.Count,
            metrics.SprintName,
            bytes.Length);
        return bytes;
    }

    private static List<SlideContent> CreateSlides(
        SprintMetrics metrics,
        SprintInsights insights,
        PresentationOptions options)
    {
        var company = string.IsNullOrWhiteSpace(options.CompanyName)
            ? string.Empty
            : $" | {options.CompanyName.Trim()}";
        var proxyNote = metrics.UsesWorkItemProxy
            ? "Story points were not present, so work-item counts are used as a clearly labelled proxy."
            : "Values use story points supplied in the source data.";
        var inProgress = metrics.TasksByStatus
            .Where(item => item.Key.Contains("progress", StringComparison.OrdinalIgnoreCase)
                || item.Key.Contains("review", StringComparison.OrdinalIgnoreCase))
            .Sum(item => item.Value);
        var remaining = Math.Max(0, metrics.TotalTasks - metrics.CompletedTasks - inProgress);
        var velocityPoints = metrics.VelocityTrend.TakeLast(8).ToList();
        var teamPoints = metrics.WorkloadByAssignee.Take(7)
            .Select(member => new MetricPoint
            {
                Label = member.Assignee,
                Value = member.CompletedTasks,
                ComparisonValue = member.TotalTasks
            }).ToList();
        var riskPoints = new List<MetricPoint>
        {
            new() { Label = "Blocked", Value = metrics.BlockedTasks },
            new() { Label = "High risks", Value = metrics.HighRiskCount },
            new() { Label = "Critical bugs", Value = metrics.CriticalBugs },
            new() { Label = "Open risks", Value = metrics.OpenRiskCount }
        };

        return
        [
            new SlideContent(
                "Cover",
                $"{metrics.SprintName} - Sprint Report\nGenerated on {DateTime.Now:MMMM dd, yyyy}{company}\n14-slide sprint intelligence briefing",
                SlideKind.Cover),
            new SlideContent(
                "Executive Summary",
                $"{insights.ExecutiveSummary}\n\nSprint health: {metrics.SprintHealthScore:F0}/100 | Completion: {metrics.CompletionRatePercent:F1}%\n\nKey highlights:\n{FormatItems(insights.KeyHighlights, 5)}"),
            new SlideContent(
                "Sprint Metrics Dashboard",
                $"Snapshot: {metrics.ReportSnapshotDate?.ToString("MMM dd, yyyy") ?? "latest uploaded data"}\n{proxyNote}",
                SlideKind.Dashboard,
                Cards:
                [
                    new("Work completion", $"{metrics.WorkCompletionRatePercent:F1}%"),
                    new("Sprint health", $"{metrics.SprintHealthScore:F0}/100"),
                    new("Delivered", $"{metrics.CompletedWork:F0}/{metrics.PlannedWork:F0}"),
                    new("Blocked", metrics.BlockedTasks.ToString()),
                    new("Capacity", metrics.HasCapacityData ? $"{metrics.CapacityUtilizationPercent:F1}%" : "N/A"),
                    new("Scope change", metrics.HasScopeData ? $"{metrics.ScopeChangePercent:F1}%" : "N/A"),
                    new("Bugs", metrics.BugCount.ToString()),
                    new("Open risks", metrics.OpenRiskCount.ToString())
                ]),
            new SlideContent(
                "Velocity Trend",
                string.Empty,
                SlideKind.BarChart,
                new ChartContent(velocityPoints, "Completed", "Planned", "3279B7", "B8C7D9"),
                $"Completed versus planned {metrics.WorkUnitLabel.ToLowerInvariant()} by sprint. " +
                $"The latest completion is {(velocityPoints.LastOrDefault()?.Value ?? 0):F0}; compare the blue bar with the grey plan to spot delivery variance. {proxyNote}"),
            new SlideContent(
                "Burndown Chart",
                string.Empty,
                SlideKind.LineChart,
                new ChartContent(metrics.BurndownTrend, "Actual remaining", "Ideal remaining", "C73535", "7C8795"),
                metrics.HasBurndownData
                    ? "The red line is actual remaining work and the grey line is the ideal path to zero. An actual line above ideal indicates the sprint is burning work more slowly than planned."
                    : "A dedicated Burndown sheet was not supplied. This red proxy line estimates remaining work from issue creation and update/completion dates; the grey line is an ideal path and should be treated as directional, not as an official daily burndown."),
            new SlideContent(
                "Story Completion",
                string.Empty,
                SlideKind.BarChart,
                new ChartContent(
                [
                    new MetricPoint { Label = "Completed", Value = metrics.CompletedTasks },
                    new MetricPoint { Label = "In progress/review", Value = inProgress },
                    new MetricPoint { Label = "Not started", Value = remaining }
                ], "Issues", null, "27864B", "B8C7D9"),
                $"{metrics.CompletedTasks} of {metrics.TotalTasks} issues are complete ({metrics.CompletionRatePercent:F1}%). " +
                "The chart separates finished work from active and not-started work so carry-over exposure is visible."),
            new SlideContent(
                "Team Productivity",
                string.Empty,
                SlideKind.BarChart,
                new ChartContent(teamPoints, "Completed", "Assigned", "603C8F", "D8CBE8"),
                $"Bars rank up to seven assignees by completed issues against assigned issues. {insights.TeamPerformanceNarrative}"),
            new SlideContent(
                "Quality Metrics",
                $"Build stability: {(metrics.HasCiCdData ? $"{metrics.BuildSuccessRatePercent:F1}% success" : "not supplied")}\n" +
                $"Deployments: {(metrics.HasCiCdData ? metrics.DeploymentCount.ToString() : "not supplied")}",
                SlideKind.Dashboard,
                Explanation: "Quality signals combine bug severity, code coverage, technical debt, Sonar findings, and CI/CD stability. Missing workbook measures are shown as N/A rather than invented.",
                Cards:
                [
                    new("Critical bugs", metrics.HasQualityData ? metrics.CriticalBugs.ToString() : "N/A"),
                    new("Major bugs", metrics.HasQualityData ? metrics.MajorBugs.ToString() : "N/A"),
                    new("Minor bugs", metrics.HasQualityData ? metrics.MinorBugs.ToString() : "N/A"),
                    new("Coverage", metrics.CodeCoveragePercent > 0 ? $"{metrics.CodeCoveragePercent:F1}%" : "N/A"),
                    new("Tech debt", metrics.HasQualityData ? metrics.TechnicalDebtItems.ToString() : "N/A"),
                    new("Sonar issues", metrics.SonarIssues > 0 ? metrics.SonarIssues.ToString() : "N/A"),
                    new("Build success", metrics.HasCiCdData ? $"{metrics.BuildSuccessRatePercent:F1}%" : "N/A"),
                    new("Deployments", metrics.HasCiCdData ? metrics.DeploymentCount.ToString() : "N/A")
                ]),
            new SlideContent(
                "Risk & Blockers",
                FormatItems(insights.RisksAndBlockers, 4),
                SlideKind.BarChart,
                new ChartContent(riskPoints, "Count", null, "C73535", "F2C7C7"),
                "Taller red bars indicate greater delivery exposure. Blockers, high-scoring risks, critical bugs, and open risks should be reviewed first; workbook probability and impact are used when available."),
            new SlideContent(
                "Scope Changes",
                string.Empty,
                SlideKind.LineChart,
                new ChartContent(metrics.ScopeTrend, "Cumulative scope", null, "D49B00", "D49B00"),
                metrics.HasScopeData
                    ? $"The line compares committed scope (indexed to 100) with final scope. {metrics.ScopeAddedItems} item(s) were added and the supplied scope change is {metrics.ScopeChangePercent:F1}%."
                    : "No dedicated scope-change history was supplied. The flat line shows that scope creep cannot be calculated reliably from issue creation dates alone; add Scope Change or Added Items to SprintSummary for an evidence-based comparison."),
            new SlideContent(
                "Key Achievements",
                FormatItems(insights.KeyHighlights, 7)),
            new SlideContent(
                "Challenges",
                $"Delivery challenges:\n{FormatItems(insights.RisksAndBlockers, 6)}\n\n" +
                $"Operational signals: {metrics.BlockedTasks} blocked, {metrics.HighRiskCount} high risk, " +
                $"{metrics.CriticalBugs} critical bugs, and {Math.Max(0, metrics.TotalTasks - metrics.CompletedTasks)} incomplete issues."),
            new SlideContent(
                "AI Recommendations",
                FormatItems(insights.Recommendations, 7)),
            new SlideContent(
                "Next Sprint Action Items",
                $"Primary focus: {insights.NextSprintFocus}\n\n" +
                $"• Resolve the {metrics.BlockedTasks} blocked item(s) before accepting new work.\n" +
                $"• Prioritize {metrics.HighRiskCount} high-risk item(s) and {metrics.CriticalBugs} critical bug(s).\n" +
                $"• Plan from demonstrated completion of {metrics.CompletedWork:F0} {metrics.WorkUnitLabel.ToLowerInvariant()}.\n" +
                "• Reconfirm capacity, owners, due dates, and mitigation status at sprint kickoff.")
        ];
    }

    private static string FormatItems(IEnumerable<string> items, int maximum)
    {
        var materialized = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Length > 220 ? item[..217] + "..." : item)
            .Take(maximum)
            .ToList();
        return materialized.Count == 0
            ? "• None identified"
            : string.Join('\n', materialized.Select(item => $"• {item}"));
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string BuildContentTypes(int slideCount)
    {
        var slideOverrides = string.Concat(Enumerable.Range(1, slideCount).Select(index =>
            $"<Override PartName=\"/ppt/slides/slide{index}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.presentationml.slide+xml\"/>"));

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/ppt/presentation.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"/>
              <Override PartName="/ppt/slideMasters/slideMaster1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideMaster+xml"/>
              <Override PartName="/ppt/slideLayouts/slideLayout1.xml" ContentType="application/vnd.openxmlformats-officedocument.presentationml.slideLayout+xml"/>
              <Override PartName="/ppt/theme/theme1.xml" ContentType="application/vnd.openxmlformats-officedocument.theme+xml"/>
              {slideOverrides}
            </Types>
            """;
    }

    private static string BuildPresentation(int slideCount)
    {
        var slideIds = string.Concat(Enumerable.Range(1, slideCount).Select(index =>
            $"<p:sldId id=\"{255 + index}\" r:id=\"rId{index + 1}\"/>"));

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:presentation xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:sldMasterIdLst><p:sldMasterId id="2147483648" r:id="rId1"/></p:sldMasterIdLst>
              <p:sldIdLst>{slideIds}</p:sldIdLst>
              <p:sldSz cx="{SlideWidth}" cy="{SlideHeight}" type="screen4x3"/>
              <p:notesSz cx="{SlideHeight}" cy="{SlideWidth}"/>
              <p:defaultTextStyle/>
            </p:presentation>
            """;
    }

    private static string BuildPresentationRelationships(int slideCount)
    {
        var slideRelationships = string.Concat(Enumerable.Range(1, slideCount).Select(index =>
            $"<Relationship Id=\"rId{index + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide\" Target=\"slides/slide{index}.xml\"/>"));

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="slideMasters/slideMaster1.xml"/>
              {slideRelationships}
            </Relationships>
            """;
    }

    private static string BuildSlide(SlideContent content, PresentationTheme theme)
    {
        var body = content.Kind switch
        {
            SlideKind.Cover => BuildCover(content, theme),
            SlideKind.Dashboard => BuildDashboard(content, theme),
            SlideKind.BarChart => BuildBarChart(content, theme),
            SlideKind.LineChart => BuildLineChart(content, theme),
            _ => BuildTextBody(content, theme)
        };

        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld>
                <p:bg><p:bgPr><a:solidFill><a:srgbClr val="{theme.BackgroundColor}"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>
                <p:spTree>
                  {GroupShapeProperties}
                  {body}
                </p:spTree>
              </p:cSld>
              <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
            </p:sld>
            """;
    }

    private static string BuildCover(SlideContent content, PresentationTheme theme)
    {
        var lines = content.Body.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        var heading = lines.ElementAtOrDefault(0) ?? "Sprint Report";
        var subtitle = string.Join('\n', lines.Skip(1));
        return BuildFilledShape(2, "Cover panel", 0, 0, SlideWidth, SlideHeight, theme.TitleColor)
            + BuildTextShape(3, "Slide title", content.Title.ToUpperInvariant(), 850_000, 850_000, 2_000_000, 450_000, 1050, true, "FFFFFF")
            + BuildTextShape(4, "Cover heading", heading, 850_000, 2_150_000, 8_350_000, 1_700_000, 3400, true, "FFFFFF", "ctr")
            + BuildTextShape(5, "Cover subtitle", subtitle, 1_300_000, 4_100_000, 7_450_000, 1_200_000, 1800, false, "FFFFFF", "ctr");
    }

    private static string BuildTextBody(SlideContent content, PresentationTheme theme)
    {
        return BuildTextShape(2, "Title", content.Title, 685_800, 350_000, 8_686_800, 900_000, 3000, true, theme.TitleColor)
            + BuildFilledShape(3, "Title accent", 685_800, 1_270_000, 1_600_000, 55_000, theme.AccentColor)
            + BuildTextShape(4, "Content", content.Body, 850_000, 1_600_000, 8_300_000, 5_200_000, 1750, false, theme.TextColor);
    }

    private static string BuildDashboard(SlideContent content, PresentationTheme theme)
    {
        var xml = new StringBuilder();
        xml.Append(BuildTextShape(2, "Title", content.Title, 685_800, 300_000, 8_686_800, 800_000, 2900, true, theme.TitleColor));
        xml.Append(BuildFilledShape(3, "Title accent", 685_800, 1_150_000, 1_600_000, 55_000, theme.AccentColor));
        var cards = content.Cards ?? [];
        for (var index = 0; index < Math.Min(8, cards.Count); index++)
        {
            var row = index / 4;
            var column = index % 4;
            var x = 685_800L + column * 2_250_000L;
            var y = 1_480_000L + row * 1_720_000L;
            var fill = (index % 3) switch
            {
                0 => theme.CardColor,
                1 => theme.SecondaryCardColor,
                _ => theme.AccentColor
            };
            xml.Append(BuildRoundedRect(10u + (uint)index, $"Metric {index + 1}", x, y, 1_980_000, 1_380_000, fill));
            xml.Append(BuildTextShape(30u + (uint)index, $"Metric value {index + 1}", cards[index].Value, x + 90_000, y + 210_000, 1_800_000, 520_000, 2350, true, "FFFFFF", "ctr"));
            xml.Append(BuildTextShape(50u + (uint)index, $"Metric label {index + 1}", cards[index].Label, x + 90_000, y + 800_000, 1_800_000, 350_000, 1150, false, "FFFFFF", "ctr"));
        }

        var footer = string.IsNullOrWhiteSpace(content.Explanation)
            ? content.Body
            : $"{content.Explanation}\n{content.Body}";
        xml.Append(BuildTextShape(80, "Dashboard note", footer, 850_000, 5_050_000, 8_250_000, 1_550_000, 1350, false, theme.TextColor));
        return xml.ToString();
    }

    private static string BuildBarChart(SlideContent content, PresentationTheme theme)
    {
        var chart = content.Chart ?? new ChartContent([], "Value", null, theme.CardColor, theme.SecondaryCardColor);
        var points = chart.Points.Take(8).ToList();
        var xml = new StringBuilder();
        xml.Append(BuildChartHeader(content.Title, chart, theme));
        xml.Append(BuildFilledShape(6, "Chart panel", 550_000, 1_400_000, 6_050_000, 5_300_000, theme.PanelColor));
        xml.Append(BuildFilledShape(7, "Explanation panel", 6_820_000, 1_400_000, 2_650_000, 5_300_000, theme.ExplanationColor));
        xml.Append(BuildTextShape(8, "Explanation heading", "GRAPH EXPLANATION", 7_050_000, 1_680_000, 2_200_000, 400_000, 1050, true, theme.TitleColor));
        xml.Append(BuildTextShape(9, "Explanation", content.Explanation, 7_050_000, 2_180_000, 2_150_000, 3_850_000, 1150, false, theme.TextColor));
        if (!string.IsNullOrWhiteSpace(content.Body))
        {
            xml.Append(BuildTextShape(10, "Chart notes", content.Body, 7_050_000, 5_750_000, 2_150_000, 650_000, 950, false, theme.TextColor));
        }

        if (points.Count == 0)
        {
            xml.Append(BuildTextShape(11, "No chart data", "No chart data was supplied.", 1_200_000, 3_200_000, 4_500_000, 800_000, 1800, false, theme.TextColor, "ctr"));
            return xml.ToString();
        }

        var max = Math.Max(1, points.Max(point => Math.Max(point.Value, point.ComparisonValue ?? 0)));
        var rowHeight = 4_350_000L / points.Count;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var y = 1_800_000L + index * rowHeight;
            var label = point.Label.Length > 18 ? point.Label[..17] + "…" : point.Label;
            xml.Append(BuildTextShape(20u + (uint)index, $"Category {index + 1}", label, 720_000, y, 1_500_000, 340_000, 950, false, theme.TextColor));
            if (point.ComparisonValue.HasValue)
            {
                var comparisonWidth = Math.Max(15_000, (long)(3_700_000 * point.ComparisonValue.Value / max));
                xml.Append(BuildFilledShape(40u + (uint)index, $"Comparison bar {index + 1}", 2_200_000, y + 65_000, comparisonWidth, 170_000, chart.ComparisonColor));
            }

            var valueWidth = Math.Max(15_000, (long)(3_700_000 * point.Value / max));
            xml.Append(BuildFilledShape(60u + (uint)index, $"Value bar {index + 1}", 2_200_000, y + 245_000, valueWidth, 210_000, chart.PrimaryColor));
            xml.Append(BuildTextShape(80u + (uint)index, $"Value label {index + 1}", point.Value.ToString("0.#"), 5_980_000, y + 150_000, 420_000, 300_000, 900, true, theme.TextColor, "r"));
        }

        return xml.ToString();
    }

    private static string BuildLineChart(SlideContent content, PresentationTheme theme)
    {
        var chart = content.Chart ?? new ChartContent([], "Value", null, theme.CardColor, theme.SecondaryCardColor);
        var points = chart.Points.Take(20).ToList();
        var xml = new StringBuilder();
        xml.Append(BuildChartHeader(content.Title, chart, theme));
        xml.Append(BuildFilledShape(6, "Chart panel", 550_000, 1_400_000, 6_050_000, 5_300_000, theme.PanelColor));
        xml.Append(BuildFilledShape(7, "Explanation panel", 6_820_000, 1_400_000, 2_650_000, 5_300_000, theme.ExplanationColor));
        xml.Append(BuildTextShape(8, "Explanation heading", "GRAPH EXPLANATION", 7_050_000, 1_680_000, 2_200_000, 400_000, 1050, true, theme.TitleColor));
        xml.Append(BuildTextShape(9, "Explanation", content.Explanation, 7_050_000, 2_180_000, 2_150_000, 4_000_000, 1150, false, theme.TextColor));
        if (points.Count == 0)
        {
            xml.Append(BuildTextShape(10, "No chart data", "No trend data was supplied.", 1_200_000, 3_200_000, 4_500_000, 800_000, 1800, false, theme.TextColor, "ctr"));
            return xml.ToString();
        }

        const long left = 1_050_000;
        const long top = 1_900_000;
        const long width = 5_050_000;
        const long height = 3_950_000;
        xml.Append(BuildLineShape(11, "Y axis", left, top, left, top + height, "8A98A8", 12_000));
        xml.Append(BuildLineShape(12, "X axis", left, top + height, left + width, top + height, "8A98A8", 12_000));
        var max = Math.Max(1, points.Max(point => Math.Max(point.Value, point.ComparisonValue ?? 0)));
        var coordinates = points.Select((point, index) => new
        {
            Point = point,
            X = left + (points.Count == 1 ? width / 2 : width * index / (points.Count - 1)),
            Y = top + height - (long)(height * point.Value / max),
            ComparisonY = point.ComparisonValue.HasValue ? top + height - (long)(height * point.ComparisonValue.Value / max) : (long?)null
        }).ToList();

        for (var index = 0; index < coordinates.Count - 1; index++)
        {
            xml.Append(BuildLineShape(20u + (uint)index, $"Primary segment {index + 1}", coordinates[index].X, coordinates[index].Y, coordinates[index + 1].X, coordinates[index + 1].Y, chart.PrimaryColor, 28_000));
            if (coordinates[index].ComparisonY.HasValue && coordinates[index + 1].ComparisonY.HasValue)
            {
                xml.Append(BuildLineShape(50u + (uint)index, $"Comparison segment {index + 1}", coordinates[index].X, coordinates[index].ComparisonY!.Value, coordinates[index + 1].X, coordinates[index + 1].ComparisonY!.Value, chart.ComparisonColor, 20_000));
            }
        }

        for (var index = 0; index < coordinates.Count; index++)
        {
            var coordinate = coordinates[index];
            xml.Append(BuildFilledShape(80u + (uint)index, $"Point {index + 1}", coordinate.X - 45_000, coordinate.Y - 45_000, 90_000, 90_000, chart.PrimaryColor, "ellipse"));
            if (index == 0 || index == coordinates.Count - 1 || coordinates.Count <= 8 || index % 2 == 0)
            {
                var label = coordinate.Point.Label.Length > 10 ? coordinate.Point.Label[..9] + "…" : coordinate.Point.Label;
                xml.Append(BuildTextShape(110u + (uint)index, $"Axis label {index + 1}", label, coordinate.X - 350_000, top + height + 100_000, 700_000, 350_000, 800, false, theme.TextColor, "ctr"));
            }
        }

        return xml.ToString();
    }

    private static string BuildChartHeader(string title, ChartContent chart, PresentationTheme theme)
    {
        var legend = chart.ComparisonLabel is null
            ? $"■ {chart.PrimaryLabel}"
            : $"■ {chart.PrimaryLabel}    ■ {chart.ComparisonLabel}";
        return BuildTextShape(2, "Title", title, 685_800, 280_000, 6_000_000, 750_000, 2850, true, theme.TitleColor)
            + BuildTextShape(3, "Legend", legend, 6_300_000, 500_000, 3_100_000, 400_000, 950, false, theme.TextColor, "r")
            + BuildFilledShape(4, "Title accent", 685_800, 1_150_000, 1_600_000, 55_000, theme.AccentColor);
    }

    private static string BuildFilledShape(
        uint id,
        string name,
        long x,
        long y,
        long width,
        long height,
        string fillColor,
        string geometry = "rect")
    {
        return $"""
            <p:sp>
              <p:nvSpPr><p:cNvPr id="{id}" name="{EscapeXml(name)}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr><a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{width}" cy="{height}"/></a:xfrm><a:prstGeom prst="{geometry}"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="{fillColor}"/></a:solidFill><a:ln><a:noFill/></a:ln></p:spPr>
            </p:sp>
            """;
    }

    private static string BuildRoundedRect(uint id, string name, long x, long y, long width, long height, string fillColor) =>
        BuildFilledShape(id, name, x, y, width, height, fillColor, "roundRect");

    private static string BuildLineShape(
        uint id,
        string name,
        long startX,
        long startY,
        long endX,
        long endY,
        string color,
        long width)
    {
        var x = Math.Min(startX, endX);
        var y = Math.Min(startY, endY);
        var flipH = startX > endX ? " flipH=\"1\"" : string.Empty;
        var flipV = startY > endY ? " flipV=\"1\"" : string.Empty;
        var extentWidth = Math.Max(1, Math.Abs(endX - startX));
        var extentHeight = Math.Max(1, Math.Abs(endY - startY));
        return $"""
            <p:sp>
              <p:nvSpPr><p:cNvPr id="{id}" name="{EscapeXml(name)}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr><a:xfrm{flipH}{flipV}><a:off x="{x}" y="{y}"/><a:ext cx="{extentWidth}" cy="{extentHeight}"/></a:xfrm><a:prstGeom prst="line"><a:avLst/></a:prstGeom><a:ln w="{width}"><a:solidFill><a:srgbClr val="{color}"/></a:solidFill></a:ln></p:spPr>
            </p:sp>
            """;
    }

    private static string BuildTextShape(
        uint id,
        string name,
        string text,
        long x,
        long y,
        long width,
        long height,
        int fontSize,
        bool bold,
        string fontColor,
        string alignment = "l")
    {
        var paragraphs = string.Concat(
            text.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n')
                .Select(line =>
                    $"<a:p><a:pPr algn=\"{alignment}\"/><a:r><a:rPr lang=\"en-US\" sz=\"{fontSize}\" b=\"{(bold ? 1 : 0)}\"><a:solidFill><a:srgbClr val=\"{fontColor}\"/></a:solidFill></a:rPr><a:t>{EscapeXml(line)}</a:t></a:r><a:endParaRPr lang=\"en-US\" sz=\"{fontSize}\"/></a:p>"));

        return $"""
            <p:sp>
              <p:nvSpPr><p:cNvPr id="{id}" name="{EscapeXml(name)}"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{width}" cy="{height}"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln>
              </p:spPr>
              <p:txBody><a:bodyPr wrap="square" anchor="t"/><a:lstStyle/>{paragraphs}</p:txBody>
            </p:sp>
            """;
    }

    private static PresentationTheme GetTheme(string template)
    {
        return template.ToLowerInvariant() switch
        {
            "modern" => new PresentationTheme("F7F3FF", "603C8F", "302842", "603C8F", "27864B", "D49B00", "F0E9F7", "EEE7F5"),
            "corporate" => new PresentationTheme("F4F7FA", "17365D", "243447", "3279B7", "27864B", "D49B00", "E8EEF5", "E5EDF6"),
            "minimal" => new PresentationTheme("FFFFFF", "111111", "333333", "444444", "6B7280", "8A6500", "F3F4F6", "F5F5F5"),
            _ => new PresentationTheme("FFFFFF", "243447", "243447", "3279B7", "27864B", "D49B00", "EEF3F7", "E8F0F7")
        };
    }

    private static string EscapeXml(string value)
    {
        var sanitized = new StringBuilder(value.Length);
        foreach (var rune in value.EnumerateRunes())
        {
            // Escaping XML metacharacters does not remove control characters that
            // XML 1.0 forbids. Ignore those characters while preserving Unicode text.
            if (IsXml10Character(rune.Value))
            {
                sanitized.Append(rune.ToString());
            }
        }

        return SecurityElement.Escape(sanitized.ToString()) ?? string.Empty;
    }

    private static bool IsXml10Character(int value)
    {
        return value is 0x9 or 0xA or 0xD
            || value is >= 0x20 and <= 0xD7FF
            || value is >= 0xE000 and <= 0xFFFD
            || value is >= 0x10000 and <= 0x10FFFF;
    }

    private enum SlideKind
    {
        Text,
        Cover,
        Dashboard,
        BarChart,
        LineChart
    }

    private sealed record SlideContent(
        string Title,
        string Body,
        SlideKind Kind = SlideKind.Text,
        ChartContent? Chart = null,
        string Explanation = "",
        List<MetricCard>? Cards = null);

    private sealed record ChartContent(
        IReadOnlyList<MetricPoint> Points,
        string PrimaryLabel,
        string? ComparisonLabel,
        string PrimaryColor,
        string ComparisonColor);

    private sealed record MetricCard(string Label, string Value);

    private sealed record PresentationTheme(
        string BackgroundColor,
        string TitleColor,
        string TextColor,
        string CardColor,
        string SecondaryCardColor,
        string AccentColor,
        string PanelColor,
        string ExplanationColor);

    private const string PackageRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml"/>
        </Relationships>
        """;

    private const string SlideRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
        </Relationships>
        """;

    private const string SlideMasterRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml"/>
          <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml"/>
        </Relationships>
        """;

    private const string SlideLayoutRelationships = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml"/>
        </Relationships>
        """;

    private const string GroupShapeProperties = """
        <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
        <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
        """;

    private const string SlideMaster = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <p:sldMaster xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
          <p:cSld name="Default"><p:spTree>
            <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
            <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
          </p:spTree></p:cSld>
          <p:clrMap accent1="accent1" accent2="accent2" accent3="accent3" accent4="accent4" accent5="accent5" accent6="accent6" bg1="lt1" bg2="lt2" folHlink="folHlink" hlink="hlink" tx1="dk1" tx2="dk2"/>
          <p:sldLayoutIdLst><p:sldLayoutId id="2147483649" r:id="rId1"/></p:sldLayoutIdLst>
          <p:txStyles><p:titleStyle/><p:bodyStyle/><p:otherStyle/></p:txStyles>
        </p:sldMaster>
        """;

    private const string SlideLayout = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <p:sldLayout xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" type="blank" preserve="1">
          <p:cSld name="Blank"><p:spTree>
            <p:nvGrpSpPr><p:cNvPr id="1" name=""/><p:cNvGrpSpPr/><p:nvPr/></p:nvGrpSpPr>
            <p:grpSpPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="0" cy="0"/><a:chOff x="0" y="0"/><a:chExt cx="0" cy="0"/></a:xfrm></p:grpSpPr>
          </p:spTree></p:cSld>
          <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
        </p:sldLayout>
        """;

    private const string Theme = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="Sprint Report Theme">
          <a:themeElements>
            <a:clrScheme name="Sprint Report">
              <a:dk1><a:sysClr val="windowText" lastClr="000000"/></a:dk1><a:lt1><a:sysClr val="window" lastClr="FFFFFF"/></a:lt1>
              <a:dk2><a:srgbClr val="243447"/></a:dk2><a:lt2><a:srgbClr val="E9EEF5"/></a:lt2>
              <a:accent1><a:srgbClr val="3279B7"/></a:accent1><a:accent2><a:srgbClr val="603C8F"/></a:accent2>
              <a:accent3><a:srgbClr val="27864B"/></a:accent3><a:accent4><a:srgbClr val="D49B00"/></a:accent4>
              <a:accent5><a:srgbClr val="C73535"/></a:accent5><a:accent6><a:srgbClr val="5A6573"/></a:accent6>
              <a:hlink><a:srgbClr val="0563C1"/></a:hlink><a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
            </a:clrScheme>
            <a:fontScheme name="Sprint Report"><a:majorFont><a:latin typeface="Aptos Display"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont><a:minorFont><a:latin typeface="Aptos"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont></a:fontScheme>
            <a:fmtScheme name="Sprint Report">
              <a:fillStyleLst><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:solidFill><a:schemeClr val="accent1"/></a:solidFill><a:solidFill><a:schemeClr val="accent2"/></a:solidFill></a:fillStyleLst>
              <a:lnStyleLst><a:ln w="9525"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln><a:ln w="25400"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln><a:ln w="38100"><a:solidFill><a:schemeClr val="phClr"/></a:solidFill><a:prstDash val="solid"/></a:ln></a:lnStyleLst>
              <a:effectStyleLst><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle><a:effectStyle><a:effectLst/></a:effectStyle></a:effectStyleLst>
              <a:bgFillStyleLst><a:solidFill><a:schemeClr val="lt1"/></a:solidFill><a:solidFill><a:schemeClr val="lt2"/></a:solidFill><a:solidFill><a:schemeClr val="dk1"/></a:solidFill></a:bgFillStyleLst>
            </a:fmtScheme>
          </a:themeElements>
        </a:theme>
        """;
}
