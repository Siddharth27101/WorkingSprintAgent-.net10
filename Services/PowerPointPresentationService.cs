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
        var inProgress = metrics.InProgressTasks;
        var remaining = metrics.NotStartedTasks;
        var healthCards = BuildHealthCards(metrics);
        var healthExplanation = BuildHealthExplanation(metrics);
        var velocityPoints = metrics.VelocityTrend.TakeLast(8).ToList();
        var teamPoints = metrics.WorkloadByAssignee.Take(7)
            .Select(member => new MetricPoint
            {
                Label = member.Assignee,
                Value = member.CompletedTasks,
                ComparisonValue = member.TotalTasks
            }).ToList();

        var teamMembers = metrics.WorkloadByAssignee.Take(9).ToList();
        var pointsHeader = metrics.UsesWorkItemProxy ? "Items (Done/Plan)" : "Story pts (Done/Plan)";
        var teamTableRows = new List<IReadOnlyList<string>>();
        foreach (var member in teamMembers)
        {
            teamTableRows.Add(new[]
            {
                member.Assignee,
                member.TotalTasks.ToString(),
                member.CompletedTasks.ToString(),
                member.InProgressTasks.ToString(),
                $"{member.CompletedStoryPoints:0.#}/{member.StoryPoints:0.#}",
                $"{member.CompletionRatePercent:F0}%"
            });
        }

        if (teamMembers.Count > 0)
        {
            var totalAssigned = teamMembers.Sum(member => member.TotalTasks);
            var totalCompleted = teamMembers.Sum(member => member.CompletedTasks);
            var totalInProgress = teamMembers.Sum(member => member.InProgressTasks);
            var totalDonePoints = teamMembers.Sum(member => member.CompletedStoryPoints);
            var totalPlanPoints = teamMembers.Sum(member => member.StoryPoints);
            var totalRate = totalAssigned == 0 ? 0 : totalCompleted * 100.0 / totalAssigned;
            teamTableRows.Add(new[]
            {
                "TEAM TOTAL",
                totalAssigned.ToString(),
                totalCompleted.ToString(),
                totalInProgress.ToString(),
                $"{totalDonePoints:0.#}/{totalPlanPoints:0.#}",
                $"{totalRate:F0}%"
            });
        }

        var topLoad = metrics.WorkloadByAssignee
            .Where(member => !member.Assignee.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(member => member.TotalTasks)
            .FirstOrDefault();
        var teamProductivityNarrative =
            "Each member shows completed work (solid bar) against the total assigned to them (light bar); the label reads done/assigned. " +
            (topLoad is not null
                ? $"{topLoad.Assignee} carries the heaviest load at {topLoad.TotalTasks} assigned ({topLoad.CompletedTasks} done, {topLoad.CompletionRatePercent:F0}%). "
                : string.Empty) +
            "A short solid bar next to a long light bar signals an overloaded or blocked owner. " +
            insights.TeamPerformanceNarrative;
        var riskPoints = new List<MetricPoint>
        {
            new() { Label = "Blocked", Value = metrics.BlockedTasks },
            new() { Label = "High risks", Value = metrics.HighRiskCount },
            new() { Label = "Critical bugs", Value = metrics.CriticalBugs },
            new() { Label = "Open risks", Value = metrics.OpenRiskCount }
        };

        List<SlideContent> slides =
        [
            new SlideContent(
                "Cover",
                $"{metrics.SprintName} - Sprint Report\nGenerated on {DateTime.Now:MMMM dd, yyyy}{company}",
                SlideKind.Cover),
            new SlideContent(
                "Executive Summary",
                $"{insights.ExecutiveSummary}\n\nSprint health: {metrics.SprintHealthScore:F0}/100 | Completion: {metrics.CompletionRatePercent:F1}%\n\nKey highlights:\n{FormatItems(insights.KeyHighlights, 5)}"),
            new SlideContent(
                "Sprint Metrics Dashboard",
                $"Data as of {metrics.ReportSnapshotDate?.ToString("MMM dd, yyyy") ?? "latest uploaded data"} \u00b7 Generated {DateTime.Now:MMM dd, yyyy}\n{proxyNote}",
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
                "Sprint Health Breakdown",
                healthExplanation,
                SlideKind.Dashboard,
                Explanation: "Each card shows its point contribution to the sprint health score; higher is better.",
                Cards: healthCards),
            new SlideContent(
                "Velocity Trend",
                string.Empty,
                SlideKind.BarChart,
                new ChartContent(velocityPoints, "Completed", "Planned", "3279B7", "B8C7D9"),
                $"Completed versus planned {metrics.WorkUnitLabel.ToLowerInvariant()} by sprint. " +
                $"The latest completion is {(velocityPoints.LastOrDefault()?.Value ?? 0):F0}; compare the blue bar with the grey plan to spot delivery variance. " +
                (metrics.DistinctSprintCount < 3
                    ? $"Only {Math.Max(1, metrics.DistinctSprintCount)} sprint(s) of history are present, so the trend is indicative only — supply 5-8 sprints of SprintSummary data to reveal whether velocity is improving or declining. "
                    : string.Empty) +
                proxyNote),
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
                teamProductivityNarrative),
            new SlideContent(
                "Team Workload & Delivery",
                string.Empty,
                SlideKind.Table,
                Explanation:
                    "Assigned = issues owned by the member; Completed = issues finished; In Progress = actively being worked. " +
                    $"The {pointsHeader.ToLowerInvariant()} column contrasts delivered effort with the effort planned for that person, and Completion is completed \u00f7 assigned. " +
                    "Read across a row to see whether an owner delivered what they took on, and compare rows to balance load in the next sprint. " +
                    (metrics.UsesWorkItemProxy
                        ? "Story points were not supplied, so issue counts are used as a labelled proxy for effort."
                        : "Effort figures use the story points supplied in the source data."),
                Table: new TableContent(
                    new[] { "Team member", "Assigned", "Completed", "In progress", pointsHeader, "Completion" },
                    teamTableRows,
                    new[] { 30, 12, 12, 13, 19, 14 },
                    HasTotalRow: teamMembers.Count > 0)),
            new SlideContent(
                "Quality Metrics",
                $"Defect density: {metrics.DefectDensityPercent:F1}% of issues are bugs ({metrics.BugCount} of {metrics.TotalTasks}); ~{metrics.BugsPerContributor:F1} bugs per contributor.\n" +
                $"Build stability: {(metrics.HasCiCdData ? $"{metrics.BuildSuccessRatePercent:F1}% success" : "not supplied")}  |  " +
                $"Deployments: {(metrics.HasCiCdData ? metrics.DeploymentCount.ToString() : "not supplied")}",
                SlideKind.Dashboard,
                Explanation: "Quality signals combine bug severity, defect density, code coverage, technical debt, Sonar findings, and CI/CD stability. Bug leakage, reopened bugs, and defect trend-over-time need issue history that a single-sprint export does not contain; missing measures are shown as N/A rather than invented.",
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
                "Challenges",
                $"Delivery challenges:\n{FormatItems(insights.RisksAndBlockers, 6)}\n\n" +
                $"Operational signals: {metrics.BlockedTasks} blocked, {metrics.HighRiskCount} high risk, " +
                $"{metrics.CriticalBugs} critical bugs, {metrics.CarryOverTasks} carried over " +
                $"({metrics.NotStartedTasks} never started, {metrics.InProgressTasks} in progress)" +
                (metrics.HasCycleTimeData ? $", average cycle time {metrics.AverageCycleTimeDays:F1} day(s)" : string.Empty) + "."),
            new SlideContent(
                "AI Recommendations",
                FormatItems(insights.Recommendations, 7)),
            new SlideContent(
                "Next Sprint Action Items",
                $"Primary focus: {insights.NextSprintFocus}\n\n" +
                $"• Resolve the {metrics.BlockedTasks} blocked item(s) before accepting new work.\n" +
                $"• Prioritize {metrics.HighRiskCount} high-risk item(s) and {metrics.CriticalBugs} critical bug(s).\n" +
                $"• Right-size the commitment to ~{Math.Max(1, (int)Math.Round(metrics.CompletedTasks * 1.1))} issues based on demonstrated completion of {metrics.CompletedWork:F0} {metrics.WorkUnitLabel.ToLowerInvariant()}.\n" +
                $"• Triage the {metrics.CarryOverTasks} carried-over item(s), including {metrics.NotStartedTasks} that never started.\n" +
                "• Reconfirm capacity, owners, due dates, and mitigation status at sprint kickoff.")
        ];

        slides[0] = slides[0] with
        {
            Body = $"{metrics.SprintName} - Sprint Report\nGenerated on {DateTime.Now:MMMM dd, yyyy}{company}\n{slides.Count}-slide sprint intelligence briefing"
        };

        return slides;
    }

    private static List<MetricCard> BuildHealthCards(SprintMetrics metrics)
    {
        var cards = new List<MetricCard>();
        var components = metrics.HealthBreakdown;
        if (components.Count == 0)
        {
            cards.Add(new MetricCard("Overall health", $"{metrics.SprintHealthScore:F0}/100"));
            return cards;
        }

        foreach (var component in components.Take(8))
        {
            var value = component.IsTotal
                ? $"{component.Points:F0}/100"
                : component.Points >= 0
                    ? $"+{component.Points:F0}"
                    : $"{component.Points:F0}";
            cards.Add(new MetricCard(component.Label, value));
        }

        return cards;
    }

    private static string BuildHealthExplanation(SprintMetrics metrics)
    {
        var terms = metrics.HealthBreakdown
            .Where(component => !component.IsTotal)
            .Select(component =>
            {
                var sign = component.Points >= 0 ? $"+{component.Points:F0}" : $"{component.Points:F0}";
                return $"{component.Label} {sign}";
            })
            .ToList();

        return terms.Count > 0
            ? $"How the score is calculated: {string.Join("  ", terms)}  =  {metrics.SprintHealthScore:F0}/100"
            : $"Overall health: {metrics.SprintHealthScore:F0}/100";
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
            SlideKind.Table => BuildTable(content, theme),
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
        return BuildGradientShape(2, "Cover panel", 0, 0, SlideWidth, SlideHeight, theme.TitleColor, theme.CardColor, 60)
            + BuildFilledShape(6, "Cover accent", 3_579_200, 2_050_000, 2_900_000, 60_000, theme.AccentColor, "roundRect")
            + BuildTextShape(4, "Cover heading", heading, 850_000, 2_300_000, 8_350_000, 1_700_000, 3400, true, "FFFFFF", "ctr")
            + BuildTextShape(5, "Cover subtitle", subtitle, 1_300_000, 4_150_000, 7_450_000, 1_400_000, 1700, false, "FFFFFF", "ctr");
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
            xml.Append(BuildRoundedRect(10u + (uint)index, $"Metric {index + 1}", x, y, 1_980_000, 1_380_000, fill, shadow: true));
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
        xml.Append(BuildRoundedRect(6, "Chart panel", 550_000, 1_400_000, 6_050_000, 5_300_000, theme.PanelColor, shadow: true));
        xml.Append(BuildRoundedRect(7, "Explanation panel", 6_820_000, 1_400_000, 2_650_000, 5_300_000, theme.ExplanationColor, shadow: true));
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
                var comparisonWidth = Math.Max(30_000, (long)(3_700_000 * point.ComparisonValue.Value / max));
                xml.Append(BuildFilledShape(40u + (uint)index, $"Comparison bar {index + 1}", 2_200_000, y + 60_000, comparisonWidth, 175_000, chart.ComparisonColor, "roundRect"));
            }

            var valueWidth = Math.Max(30_000, (long)(3_700_000 * point.Value / max));
            xml.Append(BuildFilledShape(60u + (uint)index, $"Value bar {index + 1}", 2_200_000, y + 245_000, valueWidth, 210_000, chart.PrimaryColor, "roundRect", shadow: true));
            var valueText = point.ComparisonValue.HasValue
                ? $"{point.Value:0.#}/{point.ComparisonValue.Value:0.#}"
                : point.Value.ToString("0.#");
            xml.Append(BuildTextShape(80u + (uint)index, $"Value label {index + 1}", valueText, 5_760_000, y + 150_000, 780_000, 300_000, 900, true, theme.TextColor, "r"));
        }

        return xml.ToString();
    }

    private static string BuildLineChart(SlideContent content, PresentationTheme theme)
    {
        var chart = content.Chart ?? new ChartContent([], "Value", null, theme.CardColor, theme.SecondaryCardColor);
        var points = chart.Points.Take(20).ToList();
        var xml = new StringBuilder();
        xml.Append(BuildChartHeader(content.Title, chart, theme));
        xml.Append(BuildRoundedRect(6, "Chart panel", 550_000, 1_400_000, 6_050_000, 5_300_000, theme.PanelColor, shadow: true));
        xml.Append(BuildRoundedRect(7, "Explanation panel", 6_820_000, 1_400_000, 2_650_000, 5_300_000, theme.ExplanationColor, shadow: true));
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

    private static string BuildTable(SlideContent content, PresentationTheme theme)
    {
        var xml = new StringBuilder();
        xml.Append(BuildTextShape(2, "Title", content.Title, 685_800, 300_000, 8_686_800, 800_000, 2900, true, theme.TitleColor));
        xml.Append(BuildFilledShape(3, "Title accent", 685_800, 1_150_000, 1_600_000, 55_000, theme.AccentColor));

        var table = content.Table;
        if (table is null || table.Rows.Count == 0)
        {
            xml.Append(BuildTextShape(4, "No data", "No team data was supplied.", 850_000, 1_600_000, 8_300_000, 800_000, 1800, false, theme.TextColor));
            return xml.ToString();
        }

        const long tableX = 685_800;
        const long tableY = 1_460_000;
        const long tableWidth = 8_686_800;
        var totalWeight = Math.Max(1, table.ColumnWeights.Sum());
        var gridCols = string.Concat(table.ColumnWeights
            .Select(weight => $"<a:gridCol w=\"{tableWidth * weight / totalWeight}\"/>"));

        const long headerHeight = 480_000;
        const long rowHeight = 415_000;

        var rowsXml = new StringBuilder();
        rowsXml.Append(BuildTableRow(table.Headers, headerHeight, theme.TitleColor, "FFFFFF", bold: true, fontSize: 1150, align: "ctr"));
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var isTotal = table.HasTotalRow && r == table.Rows.Count - 1;
            var fill = isTotal
                ? theme.CardColor
                : (r % 2 == 0 ? theme.PanelColor : theme.BackgroundColor);
            var textColor = isTotal ? "FFFFFF" : theme.TextColor;
            rowsXml.Append(BuildTableRow(table.Rows[r], rowHeight, fill, textColor, bold: isTotal, fontSize: 1100, align: "ctr", firstColAlign: "l"));
        }

        var tableHeight = headerHeight + rowHeight * table.Rows.Count;
        xml.Append($"""
            <p:graphicFrame>
              <p:nvGraphicFramePr><p:cNvPr id="20" name="Team table"/><p:cNvGraphicFramePr><a:graphicFrameLocks noGrp="1"/></p:cNvGraphicFramePr><p:nvPr/></p:nvGraphicFramePr>
              <p:xfrm><a:off x="{tableX}" y="{tableY}"/><a:ext cx="{tableWidth}" cy="{tableHeight}"/></p:xfrm>
              <a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/table">
                <a:tbl><a:tblPr firstRow="1" bandRow="0"/><a:tblGrid>{gridCols}</a:tblGrid>{rowsXml}</a:tbl>
              </a:graphicData></a:graphic>
            </p:graphicFrame>
            """);

        if (!string.IsNullOrWhiteSpace(content.Explanation))
        {
            var noteY = tableY + tableHeight + 200_000;
            var noteHeight = Math.Max(400_000, SlideHeight - noteY - 250_000);
            xml.Append(BuildTextShape(60, "Table note", content.Explanation, 685_800, noteY, 8_686_800, noteHeight, 1150, false, theme.TextColor));
        }

        return xml.ToString();
    }

    private static string BuildTableRow(
        IReadOnlyList<string> cells,
        long height,
        string fillColor,
        string fontColor,
        bool bold,
        int fontSize,
        string align,
        string? firstColAlign = null)
    {
        var sb = new StringBuilder();
        sb.Append($"<a:tr h=\"{height}\">");
        for (var c = 0; c < cells.Count; c++)
        {
            var cellAlign = c == 0 && firstColAlign is not null ? firstColAlign : align;
            sb.Append(BuildTableCell(cells[c], fillColor, fontColor, bold, fontSize, cellAlign));
        }

        sb.Append("</a:tr>");
        return sb.ToString();
    }

    private static string BuildTableCell(string text, string fillColor, string fontColor, bool bold, int fontSize, string align)
    {
        return $"<a:tc><a:txBody><a:bodyPr/><a:lstStyle/><a:p><a:pPr algn=\"{align}\"/><a:r><a:rPr lang=\"en-US\" sz=\"{fontSize}\" b=\"{(bold ? 1 : 0)}\"><a:solidFill><a:srgbClr val=\"{fontColor}\"/></a:solidFill></a:rPr><a:t>{EscapeXml(text)}</a:t></a:r></a:p></a:txBody>"
            + $"<a:tcPr marL=\"109728\" marR=\"91440\" marT=\"27432\" marB=\"27432\" anchor=\"ctr\"><a:solidFill><a:srgbClr val=\"{fillColor}\"/></a:solidFill></a:tcPr></a:tc>";
    }

    private static string BuildFilledShape(
        uint id,
        string name,
        long x,
        long y,
        long width,
        long height,
        string fillColor,
        string geometry = "rect",
        bool shadow = false)
    {
        var effects = shadow ? SoftShadow : string.Empty;
        return $"""
            <p:sp>
              <p:nvSpPr><p:cNvPr id="{id}" name="{EscapeXml(name)}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr><a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{width}" cy="{height}"/></a:xfrm><a:prstGeom prst="{geometry}"><a:avLst/></a:prstGeom><a:solidFill><a:srgbClr val="{fillColor}"/></a:solidFill><a:ln><a:noFill/></a:ln>{effects}</p:spPr>
            </p:sp>
            """;
    }

    private static string BuildGradientShape(
        uint id,
        string name,
        long x,
        long y,
        long width,
        long height,
        string startColor,
        string endColor,
        int angleDegrees = 45)
    {
        var angle = angleDegrees * 60_000;
        return $"""
            <p:sp>
              <p:nvSpPr><p:cNvPr id="{id}" name="{EscapeXml(name)}"/><p:cNvSpPr/><p:nvPr/></p:nvSpPr>
              <p:spPr><a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{width}" cy="{height}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:gradFill><a:gsLst><a:gs pos="0"><a:srgbClr val="{startColor}"/></a:gs><a:gs pos="100000"><a:srgbClr val="{endColor}"/></a:gs></a:gsLst><a:lin ang="{angle}" scaled="1"/></a:gradFill><a:ln><a:noFill/></a:ln></p:spPr>
            </p:sp>
            """;
    }

    private static string BuildRoundedRect(uint id, string name, long x, long y, long width, long height, string fillColor, bool shadow = false) =>
        BuildFilledShape(id, name, x, y, width, height, fillColor, "roundRect", shadow);

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
        LineChart,
        Table
    }

    private sealed record SlideContent(
        string Title,
        string Body,
        SlideKind Kind = SlideKind.Text,
        ChartContent? Chart = null,
        string Explanation = "",
        List<MetricCard>? Cards = null,
        TableContent? Table = null);

    private sealed record TableContent(
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyList<string>> Rows,
        IReadOnlyList<int> ColumnWeights,
        bool HasTotalRow = false);

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

    private const string SoftShadow =
        "<a:effectLst><a:outerShdw blurRad=\"90000\" dist=\"38100\" dir=\"5400000\" rotWithShape=\"0\"><a:srgbClr val=\"1B2733\"><a:alpha val=\"26000\"/></a:srgbClr></a:outerShdw></a:effectLst>";

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
