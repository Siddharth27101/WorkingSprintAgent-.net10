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
        var statusLines = metrics.TasksByStatus.Any()
            ? string.Join('\n', metrics.TasksByStatus
                .OrderByDescending(item => item.Value)
                .Select(item => $"{item.Key}: {item.Value}"))
            : "No status data available";
        var teamLines = metrics.WorkloadByAssignee.Any()
            ? string.Join('\n', metrics.WorkloadByAssignee
                .OrderByDescending(member => member.CompletedTasks)
                .Take(10)
                .Select(member => $"{member.Assignee}: {member.CompletedTasks}/{member.TotalTasks} tasks completed"))
            : "No team allocation data available";

        return new List<SlideContent>
        {
            new(
                $"{metrics.SprintName} - Sprint Report",
                $"Generated on {DateTime.Now:MMMM dd, yyyy}{company}\n\n" +
                $"Completion: {metrics.CompletionRatePercent:F0}%\n" +
                $"Tasks: {metrics.CompletedTasks} of {metrics.TotalTasks} completed"),
            new(
                "Executive Summary",
                $"{insights.ExecutiveSummary}\n\nKey highlights:\n{FormatItems(insights.KeyHighlights)}"),
            new(
                "Sprint Metrics Overview",
                $"Total tasks: {metrics.TotalTasks}\n" +
                $"Completed tasks: {metrics.CompletedTasks}\n" +
                $"Blocked tasks: {metrics.BlockedTasks}\n" +
                $"Completion rate: {metrics.CompletionRatePercent:F1}%\n" +
                $"Story points: {metrics.CompletedStoryPoints:F1} of {metrics.TotalStoryPoints:F1}"),
            new("Task Status Distribution", statusLines),
            new("Team Performance", $"{insights.TeamPerformanceNarrative}\n\n{teamLines}"),
            new("Risks & Blockers", FormatItems(insights.RisksAndBlockers)),
            new("Recommendations", FormatItems(insights.Recommendations)),
            new("Next Sprint Focus", insights.NextSprintFocus)
        };
    }

    private static string FormatItems(IEnumerable<string> items)
    {
        var materialized = items.Where(item => !string.IsNullOrWhiteSpace(item)).ToList();
        return materialized.Count == 0
            ? "None identified"
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
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <p:sld xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld>
                <p:bg><p:bgPr><a:solidFill><a:srgbClr val="{theme.BackgroundColor}"/></a:solidFill><a:effectLst/></p:bgPr></p:bg>
                <p:spTree>
                  {GroupShapeProperties}
                  {BuildTextShape(2, "Title", content.Title, 685800, 457200, 8686800, 1143000, 3200, true, theme.TitleColor)}
                  {BuildTextShape(3, "Content", content.Body, 914400, 1828800, 8229600, 4800600, 1800, false, theme.TextColor)}
                </p:spTree>
              </p:cSld>
              <p:clrMapOvr><a:masterClrMapping/></p:clrMapOvr>
            </p:sld>
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
        string fontColor)
    {
        var paragraphs = string.Concat(
            text.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Split('\n')
                .Select(line =>
                    $"<a:p><a:r><a:rPr lang=\"en-US\" sz=\"{fontSize}\" b=\"{(bold ? 1 : 0)}\"><a:solidFill><a:srgbClr val=\"{fontColor}\"/></a:solidFill></a:rPr><a:t>{EscapeXml(line)}</a:t></a:r><a:endParaRPr lang=\"en-US\" sz=\"{fontSize}\"/></a:p>"));

        return $"""
            <p:sp>
              <p:nvSpPr><p:cNvPr id="{id}" name="{EscapeXml(name)}"/><p:cNvSpPr txBox="1"/><p:nvPr/></p:nvSpPr>
              <p:spPr>
                <a:xfrm><a:off x="{x}" y="{y}"/><a:ext cx="{width}" cy="{height}"/></a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom><a:noFill/><a:ln><a:noFill/></a:ln>
              </p:spPr>
              <p:txBody><a:bodyPr wrap="square"/><a:lstStyle/>{paragraphs}</p:txBody>
            </p:sp>
            """;
    }

    private static PresentationTheme GetTheme(string template)
    {
        return template.ToLowerInvariant() switch
        {
            "modern" => new PresentationTheme("F7F3FF", "603C8F", "302842"),
            "corporate" => new PresentationTheme("F4F7FA", "17365D", "243447"),
            "minimal" => new PresentationTheme("FFFFFF", "111111", "333333"),
            _ => new PresentationTheme("FFFFFF", "243447", "243447")
        };
    }

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private sealed record SlideContent(string Title, string Body);
    private sealed record PresentationTheme(string BackgroundColor, string TitleColor, string TextColor);

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
          <p:sldLayoutIdLst><p:sldLayoutId id="1" r:id="rId1"/></p:sldLayoutIdLst>
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
