using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using System.Drawing;
using WorkingSprintAgent.Models;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Professional PowerPoint presentation generation service using OpenXML
/// </summary>
public class PowerPointPresentationService
{
    private readonly ILogger<PowerPointPresentationService> _logger;

    public PowerPointPresentationService(ILogger<PowerPointPresentationService> logger)
    {
        _logger = logger;
    }

    public byte[] CreatePresentationFromTemplate(SprintMetrics metrics, SprintInsights insights, PresentationOptions options)
    {
        _logger.LogInformation("Creating PowerPoint presentation for sprint: {SprintName}", metrics.SprintName);

        using var stream = new MemoryStream();
        
        // Create presentation document
        using (var presentationDocument = PresentationDocument.Create(stream, PresentationDocumentType.Presentation))
        {
            CreatePresentationPart(presentationDocument);
            
            var presentationPart = presentationDocument.PresentationPart!;
            var slideIdList = presentationPart.Presentation.SlideIdList!;

            // Create slides
            CreateTitleSlide(presentationPart, slideIdList, metrics, options);
            CreateExecutiveSummarySlide(presentationPart, slideIdList, metrics, insights);
            CreateMetricsOverviewSlide(presentationPart, slideIdList, metrics);
            CreateCompletionChartSlide(presentationPart, slideIdList, metrics);
            CreateTeamPerformanceSlide(presentationPart, slideIdList, metrics, insights);
            CreateRisksAndBlockersSlide(presentationPart, slideIdList, insights);
            CreateRecommendationsSlide(presentationPart, slideIdList, insights);
            CreateNextStepsSlide(presentationPart, slideIdList, insights);

            presentationDocument.Save();
        }

        var result = stream.ToArray();
        _logger.LogInformation("PowerPoint presentation created successfully. Size: {Size} KB", result.Length / 1024);
        
        return result;
    }

    #region Slide Creation Methods

    private void CreatePresentationPart(PresentationDocument presentationDocument)
    {
        var presentationPart = presentationDocument.AddPresentationPart();
        presentationPart.Presentation = new P.Presentation();

        CreateSlideMasterPart(presentationPart);
        
        presentationPart.Presentation.SlideIdList = new P.SlideIdList();
        presentationPart.Presentation.SlideSize = new P.SlideSize
        {
            Cx = 10058400,  // Standard slide width
            Cy = 7534800    // Standard slide height
        };

        presentationPart.Presentation.NotesSize = new P.NotesSize
        {
            Cx = 10058400,
            Cy = 7534800
        };
    }

    private void CreateSlideMasterPart(PresentationPart presentationPart)
    {
        var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
        var slideMaster = new P.SlideMaster();
        slideMaster.Append(new P.CommonSlideData(new P.ShapeTree()));
        slideMaster.Append(new P.ColorMap());
        slideMasterPart.SlideMaster = slideMaster;

        var slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
        var slideLayout = new P.SlideLayout(new P.CommonSlideData(new P.ShapeTree()));
        slideLayoutPart.SlideLayout = slideLayout;
    }

    private SlidePart CreateSlide(PresentationPart presentationPart, P.SlideIdList slideIdList)
    {
        var slidePart = presentationPart.AddNewPart<SlidePart>();
        var slide = new P.Slide(new P.CommonSlideData(new P.ShapeTree()));
        slidePart.Slide = slide;

        var slideId = new P.SlideId
        {
            Id = (uint)(slideIdList.ChildElements.Count + 256),
            RelationshipId = presentationPart.GetIdOfPart(slidePart)
        };
        slideIdList.Append(slideId);

        return slidePart;
    }

    private void CreateTitleSlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintMetrics metrics, PresentationOptions options)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title shape
        var titleShape = CreateTextBox(
            shapeId: 2,
            x: 1016000, y: 2032000,
            width: 8128000, height: 1143000,
            text: $"{metrics.SprintName} - Sprint Report",
            fontSize: 4400,
            isBold: true
        );
        shapeTree.Append(titleShape);

        // Subtitle shape
        var subtitleShape = CreateTextBox(
            shapeId: 3,
            x: 1016000, y: 3175200,
            width: 8128000, height: 1143000,
            text: $"Generated on {DateTime.Now:MMMM dd, yyyy} | {options.CompanyName}",
            fontSize: 2000,
            isBold: false
        );
        shapeTree.Append(subtitleShape);

        // Key metrics preview
        var metricsPreview = CreateTextBox(
            shapeId: 4,
            x: 1016000, y: 4572000,
            width: 8128000, height: 1905000,
            text: $"Sprint Completion: {metrics.CompletionRatePercent:F0}%\n" +
                  $"Tasks Completed: {metrics.CompletedTasks} of {metrics.TotalTasks}\n" +
                  (metrics.TotalStoryPoints > 0 ? $"Story Points: {metrics.CompletedStoryPoints:F0} of {metrics.TotalStoryPoints:F0}" : ""),
            fontSize: 1800,
            isBold: false
        );
        shapeTree.Append(metricsPreview);
    }

    private void CreateExecutiveSummarySlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintMetrics metrics, SprintInsights insights)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title
        var titleShape = CreateTextBox(2, 686400, 457200, 8636800, 1371600,
            "Executive Summary", 3200, true);
        shapeTree.Append(titleShape);

        // Main summary
        var summaryShape = CreateTextBox(3, 686400, 1828800, 8636800, 2286000,
            insights.ExecutiveSummary, 2400, false);
        shapeTree.Append(summaryShape);

        // Key highlights section
        var highlightsTitle = CreateTextBox(4, 686400, 4114800, 8636800, 685800,
            "Key Highlights:", 2000, true);
        shapeTree.Append(highlightsTitle);

        var highlights = string.Join("\n• ", new[] { "" }.Concat(insights.KeyHighlights.Take(4)));
        var highlightsShape = CreateTextBox(5, 1371600, 4800600, 7962000, 2286000,
            highlights, 1800, false);
        shapeTree.Append(highlightsShape);
    }

    private void CreateMetricsOverviewSlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintMetrics metrics)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title
        var titleShape = CreateTextBox(2, 686400, 457200, 8636800, 1371600,
            "Sprint Metrics Overview", 3200, true);
        shapeTree.Append(titleShape);

        // Metrics cards layout
        CreateMetricCard(shapeTree, 3, 1016000, 2286000, "Total Tasks", metrics.TotalTasks.ToString());
        CreateMetricCard(shapeTree, 4, 3048000, 2286000, "Completed", metrics.CompletedTasks.ToString());
        CreateMetricCard(shapeTree, 5, 5080000, 2286000, "Completion Rate", $"{metrics.CompletionRatePercent:F0}%");
        CreateMetricCard(shapeTree, 6, 7112000, 2286000, "Blocked", metrics.BlockedTasks.ToString());

        if (metrics.TotalStoryPoints > 0)
        {
            CreateMetricCard(shapeTree, 7, 1016000, 4572000, "Story Points", $"{metrics.CompletedStoryPoints:F0}/{metrics.TotalStoryPoints:F0}");
            CreateMetricCard(shapeTree, 8, 3048000, 4572000, "Points Rate", $"{(metrics.CompletedStoryPoints / metrics.TotalStoryPoints * 100):F0}%");
        }

        // Team size
        CreateMetricCard(shapeTree, 9, 5080000, 4572000, "Team Size", metrics.WorkloadByAssignee.Count.ToString());
    }

    private void CreateCompletionChartSlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintMetrics metrics)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title
        var titleShape = CreateTextBox(2, 686400, 457200, 8636800, 1371600,
            "Task Status Distribution", 3200, true);
        shapeTree.Append(titleShape);

        // Create a simple chart representation (text-based for simplicity)
        var chartText = "Task Status Breakdown:\n\n";
        foreach (var status in metrics.TasksByStatus.OrderByDescending(x => x.Value))
        {
            var percentage = (status.Value / (double)metrics.TotalTasks * 100);
            var bar = new string('█', Math.Max(1, (int)(percentage / 5))); // Visual bar
            chartText += $"{status.Key}: {status.Value} ({percentage:F0}%)\n{bar}\n\n";
        }

        var chartShape = CreateTextBox(3, 1371600, 2286000, 7315200, 4572000,
            chartText, 1600, false);
        shapeTree.Append(chartShape);
    }

    private void CreateTeamPerformanceSlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintMetrics metrics, SprintInsights insights)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title
        var titleShape = CreateTextBox(2, 686400, 457200, 8636800, 1371600,
            "Team Performance", 3200, true);
        shapeTree.Append(titleShape);

        // Team narrative
        var narrativeShape = CreateTextBox(3, 686400, 1828800, 8636800, 1143000,
            insights.TeamPerformanceNarrative, 2000, false);
        shapeTree.Append(narrativeShape);

        // Team breakdown
        if (metrics.WorkloadByAssignee.Any())
        {
            var teamTitle = CreateTextBox(4, 686400, 3200400, 8636800, 685800,
                "Individual Performance:", 1800, true);
            shapeTree.Append(teamTitle);

            var teamData = string.Join("\n", metrics.WorkloadByAssignee
                .OrderByDescending(a => a.CompletedTasks)
                .Take(8)
                .Select(a => $"{a.Assignee}: {a.CompletedTasks}/{a.TotalTasks} tasks completed ({(a.CompletedTasks / (double)Math.Max(1, a.TotalTasks) * 100):F0}%)"));

            var teamShape = CreateTextBox(5, 1371600, 4000200, 7962000, 3086000,
                teamData, 1600, false);
            shapeTree.Append(teamShape);
        }
    }

    private void CreateRisksAndBlockersSlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintInsights insights)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title
        var titleShape = CreateTextBox(2, 686400, 457200, 8636800, 1371600,
            "Risks & Blockers", 3200, true);
        shapeTree.Append(titleShape);

        // Risks content
        var risksText = "⚠️ Key Risks and Blockers:\n\n";
        risksText += string.Join("\n\n• ", new[] { "" }.Concat(insights.RisksAndBlockers));

        var risksShape = CreateTextBox(3, 1016000, 2286000, 8128000, 4572000,
            risksText, 2000, false);
        shapeTree.Append(risksShape);
    }

    private void CreateRecommendationsSlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintInsights insights)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title
        var titleShape = CreateTextBox(2, 686400, 457200, 8636800, 1371600,
            "Recommendations", 3200, true);
        shapeTree.Append(titleShape);

        // Recommendations content
        var recommendationsText = "💡 Action Items & Recommendations:\n\n";
        recommendationsText += string.Join("\n\n• ", new[] { "" }.Concat(insights.Recommendations));

        var recommendationsShape = CreateTextBox(3, 1016000, 2286000, 8128000, 4572000,
            recommendationsText, 2000, false);
        shapeTree.Append(recommendationsShape);
    }

    private void CreateNextStepsSlide(PresentationPart presentationPart, P.SlideIdList slideIdList, SprintInsights insights)
    {
        var slidePart = CreateSlide(presentationPart, slideIdList);
        var shapeTree = slidePart.Slide.CommonSlideData!.ShapeTree!;

        // Title
        var titleShape = CreateTextBox(2, 686400, 457200, 8636800, 1371600,
            "Next Sprint Focus", 3200, true);
        shapeTree.Append(titleShape);

        // Next sprint focus
        var focusShape = CreateTextBox(3, 1016000, 2286000, 8128000, 2286000,
            $"🎯 {insights.NextSprintFocus}", 2400, false);
        shapeTree.Append(focusShape);

        // Thank you section
        var thanksShape = CreateTextBox(4, 1016000, 5486400, 8128000, 1371600,
            "Thank you for your attention!\nQuestions & Discussion", 2000, true);
        shapeTree.Append(thanksShape);
    }

    #endregion

    #region Helper Methods

    private P.Shape CreateTextBox(uint shapeId, long x, long y, long width, long height, string text, int fontSize, bool isBold)
    {
        var shape = new P.Shape();

        var nvSpPr = new P.NonVisualShapeProperties();
        nvSpPr.Append(new P.NonVisualDrawingProperties { Id = shapeId, Name = $"TextBox {shapeId}" });
        nvSpPr.Append(new P.NonVisualShapeDrawingProperties());
        nvSpPr.Append(new P.ApplicationNonVisualDrawingProperties());

        var spPr = new P.ShapeProperties();
        spPr.Append(new A.Transform2D
        {
            Offset = new A.Offset { X = x, Y = y },
            Extents = new A.Extents { Cx = width, Cy = height }
        });
        spPr.Append(new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });

        var txBody = new P.TextBody();
        txBody.Append(new A.BodyProperties());
        txBody.Append(new A.ListStyle());

        var paragraph = new A.Paragraph();
        if (isBold)
        {
            var run = new A.Run();
            run.Append(new A.RunProperties { FontSize = fontSize, Bold = true });
            run.Append(new A.Text(text));
            paragraph.Append(run);
        }
        else
        {
            var run = new A.Run();
            run.Append(new A.RunProperties { FontSize = fontSize });
            run.Append(new A.Text(text));
            paragraph.Append(run);
        }

        txBody.Append(paragraph);

        shape.Append(nvSpPr);
        shape.Append(spPr);
        shape.Append(txBody);

        return shape;
    }

    private void CreateMetricCard(P.ShapeTree shapeTree, uint shapeId, long x, long y, string label, string value)
    {
        // Card background
        var cardShape = new P.Shape();
        var nvSpPr = new P.NonVisualShapeProperties();
        nvSpPr.Append(new P.NonVisualDrawingProperties { Id = shapeId, Name = $"Card {shapeId}" });
        nvSpPr.Append(new P.NonVisualShapeDrawingProperties());
        nvSpPr.Append(new P.ApplicationNonVisualDrawingProperties());

        var spPr = new P.ShapeProperties();
        spPr.Append(new A.Transform2D
        {
            Offset = new A.Offset { X = x, Y = y },
            Extents = new A.Extents { Cx = 1524000, Cy = 1905000 }
        });
        spPr.Append(new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle });
        spPr.Append(new A.SolidFill(new A.RgbColorModelHex { Val = "F8F9FA" }));

        var txBody = new P.TextBody();
        txBody.Append(new A.BodyProperties { Anchor = A.TextAnchoringTypeValues.Center });
        txBody.Append(new A.ListStyle());

        // Value paragraph
        var valueParagraph = new A.Paragraph();
        valueParagraph.Append(new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Center });
        var valueRun = new A.Run();
        valueRun.Append(new A.RunProperties { FontSize = 3600, Bold = true });
        valueRun.Append(new A.Text(value));
        valueParagraph.Append(valueRun);
        txBody.Append(valueParagraph);

        // Label paragraph
        var labelParagraph = new A.Paragraph();
        labelParagraph.Append(new A.ParagraphProperties { Alignment = A.TextAlignmentTypeValues.Center });
        var labelRun = new A.Run();
        labelRun.Append(new A.RunProperties { FontSize = 1400 });
        labelRun.Append(new A.Text(label));
        labelParagraph.Append(labelRun);
        txBody.Append(labelParagraph);

        cardShape.Append(nvSpPr);
        cardShape.Append(spPr);
        cardShape.Append(txBody);

        shapeTree.Append(cardShape);
    }

    #endregion
}