using System.Net;
using System.Text;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Builds PowerPoint and standalone HTML sprint presentations.
/// </summary>
public class PresentationBuilderService : IPresentationBuilderService
{
    private readonly ILogger<PresentationBuilderService> _logger;
    private readonly PowerPointPresentationService _powerPointService;

    public PresentationBuilderService(
        ILogger<PresentationBuilderService> logger,
        PowerPointPresentationService powerPointService)
    {
        _logger = logger;
        _powerPointService = powerPointService;
    }

    public byte[] BuildPowerPointPresentation(
        SprintMetrics metrics,
        SprintInsights insights,
        PresentationOptions? options = null)
    {
        options ??= new PresentationOptions();

        _logger.LogInformation(
            "Building PowerPoint presentation for sprint '{SprintName}' using template '{Template}'",
            metrics.SprintName,
            options.Template);

        // Do not return HTML from this method: callers label these bytes as a .pptx file.
        return _powerPointService.CreatePresentationFromTemplate(metrics, insights, options);
    }

    public byte[] BuildPresentation(SprintMetrics metrics, SprintInsights insights)
    {
        _logger.LogInformation("Building HTML presentation for sprint '{SprintName}'", metrics.SprintName);

        var bytes = Encoding.UTF8.GetBytes(BuildHtmlPresentation(metrics, insights));
        _logger.LogInformation("Generated HTML presentation with {Size} bytes", bytes.Length);
        return bytes;
    }

    public PresentationSummary GetPresentationSummary(SprintMetrics metrics, SprintInsights insights)
    {
        var slideTopics = new List<string>
        {
            "Title Slide",
            "Executive Summary",
            "Sprint Metrics Overview",
            "Task Completion Analysis",
            "Team Performance",
            "Risks & Blockers",
            "Recommendations",
            "Next Sprint Focus"
        };

        var chartTypes = new List<string>();

        return new PresentationSummary
        {
            Title = $"{metrics.SprintName} - Sprint Report",
            SlideCount = slideTopics.Count,
            SlideTopics = slideTopics,
            ChartTypes = chartTypes,
            EstimatedViewingTimeMinutes = Math.Max(5, slideTopics.Count * 2),
            GeneratedAt = DateTime.UtcNow,
            Template = "Professional",
            EstimatedFileSizeBytes = EstimateFileSizeBytes(metrics, insights)
        };
    }

    public List<PresentationTemplate> GetAvailableTemplates()
    {
        return new List<PresentationTemplate>
        {
            new()
            {
                Id = "professional",
                Name = "Professional",
                Description = "Clean, corporate design suitable for stakeholder presentations",
                Features = new List<string> { "Blue accent palette", "Executive summary", "Readable metrics" }
            },
            new()
            {
                Id = "modern",
                Name = "Modern",
                Description = "Contemporary design with vibrant colors and modern typography",
                Features = new List<string> { "Purple accent palette", "Contemporary typography", "Readable metrics" }
            },
            new()
            {
                Id = "corporate",
                Name = "Corporate",
                Description = "Formal template with company branding integration",
                Features = new List<string> { "Navy accent palette", "Company name on title slide", "Formal layout" },
                RequiresCompanyBranding = true
            },
            new()
            {
                Id = "minimal",
                Name = "Minimal",
                Description = "Clean, distraction-free design focusing on content",
                Features = new List<string> { "Monochrome palette", "Content-focused layout", "High readability" }
            }
        };
    }

    private static string BuildHtmlPresentation(SprintMetrics metrics, SprintInsights insights)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>")
            .AppendLine("<html lang=\"en\">")
            .AppendLine("<head>")
            .AppendLine("<meta charset=\"UTF-8\">")
            .AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">")
            .Append("<title>Sprint Report - ").Append(EscapeHtml(metrics.SprintName)).AppendLine("</title>")
            .AppendLine(GetPresentationStyles())
            .AppendLine("</head>")
            .AppendLine("<body>");

        AppendTitleSlide(html, metrics);
        AppendExecutiveSummarySlide(html, metrics, insights);
        AppendListSlide(html, "Key Highlights", "highlights", insights.KeyHighlights);
        AppendTeamPerformanceSlide(html, metrics, insights);

        if (insights.RisksAndBlockers.Any() || metrics.BlockedTaskTitles.Any())
        {
            AppendRisksSlide(html, metrics, insights);
        }

        AppendListSlide(html, "Recommendations", "recommendations", insights.Recommendations);
        AppendTextSlide(html, "Next Sprint Focus", "next-focus", insights.NextSprintFocus);

        html.AppendLine("</body>").AppendLine("</html>");
        return html.ToString();
    }

    private static void AppendTitleSlide(StringBuilder html, SprintMetrics metrics)
    {
        html.AppendLine("<section class=\"slide title-slide\">")
            .AppendLine("<h1>Sprint Report</h1>")
            .Append("<h2>").Append(EscapeHtml(metrics.SprintName)).AppendLine("</h2>")
            .Append("<p class=\"subtitle\">Generated on ")
            .Append(DateTime.Now.ToString("MMMM dd, yyyy"))
            .AppendLine("</p></section>");
    }

    private static void AppendExecutiveSummarySlide(
        StringBuilder html,
        SprintMetrics metrics,
        SprintInsights insights)
    {
        html.AppendLine("<section class=\"slide\"><h2>Executive Summary</h2><div class=\"content\">")
            .Append("<p class=\"summary\">").Append(EscapeHtml(insights.ExecutiveSummary)).AppendLine("</p>")
            .AppendLine("<div class=\"metrics-grid\">");

        AppendMetricCard(html, $"{metrics.CompletionRatePercent:F1}%", "Completion Rate");
        AppendMetricCard(html, $"{metrics.CompletedTasks}/{metrics.TotalTasks}", "Tasks Completed");
        AppendMetricCard(html, $"{metrics.CompletedStoryPoints:F1}", "Story Points");
        AppendMetricCard(html, metrics.BlockedTasks.ToString(), "Blocked Tasks");

        html.AppendLine("</div></div></section>");
    }

    private static void AppendMetricCard(StringBuilder html, string value, string label)
    {
        html.AppendLine("<div class=\"metric-card\">")
            .Append("<div class=\"metric-value\">").Append(EscapeHtml(value)).AppendLine("</div>")
            .Append("<div class=\"metric-label\">").Append(EscapeHtml(label)).AppendLine("</div></div>");
    }

    private static void AppendListSlide(
        StringBuilder html,
        string title,
        string cssClass,
        IEnumerable<string> items)
    {
        html.AppendLine("<section class=\"slide\">")
            .Append("<h2>").Append(EscapeHtml(title)).AppendLine("</h2><div class=\"content\">")
            .Append("<ul class=\"").Append(cssClass).AppendLine("\">");

        foreach (var item in items)
        {
            html.Append("<li>").Append(EscapeHtml(item)).AppendLine("</li>");
        }

        html.AppendLine("</ul></div></section>");
    }

    private static void AppendTextSlide(StringBuilder html, string title, string cssClass, string text)
    {
        html.AppendLine("<section class=\"slide\">")
            .Append("<h2>").Append(EscapeHtml(title)).AppendLine("</h2><div class=\"content\">")
            .Append("<p class=\"").Append(cssClass).Append("\">")
            .Append(EscapeHtml(text)).AppendLine("</p></div></section>");
    }

    private static void AppendTeamPerformanceSlide(
        StringBuilder html,
        SprintMetrics metrics,
        SprintInsights insights)
    {
        html.AppendLine("<section class=\"slide\"><h2>Team Performance</h2><div class=\"content\">")
            .Append("<p class=\"performance-narrative\">")
            .Append(EscapeHtml(insights.TeamPerformanceNarrative))
            .AppendLine("</p>");

        if (metrics.WorkloadByAssignee.Any())
        {
            html.AppendLine("<div class=\"team-table\"><table><thead><tr>")
                .AppendLine("<th>Team Member</th><th>Total Tasks</th><th>Completed</th><th>Story Points</th><th>Completion %</th>")
                .AppendLine("</tr></thead><tbody>");

            foreach (var assignee in metrics.WorkloadByAssignee)
            {
                var completionRate = assignee.TotalTasks > 0
                    ? assignee.CompletedTasks * 100.0 / assignee.TotalTasks
                    : 0;

                html.AppendLine("<tr>")
                    .Append("<td>").Append(EscapeHtml(assignee.Assignee)).AppendLine("</td>")
                    .Append("<td>").Append(assignee.TotalTasks).AppendLine("</td>")
                    .Append("<td>").Append(assignee.CompletedTasks).AppendLine("</td>")
                    .Append("<td>").Append(assignee.StoryPoints.ToString("F1")).AppendLine("</td>")
                    .Append("<td>").Append(completionRate.ToString("F0")).AppendLine("%</td></tr>");
            }

            html.AppendLine("</tbody></table></div>");
        }

        html.AppendLine("</div></section>");
    }

    private static void AppendRisksSlide(
        StringBuilder html,
        SprintMetrics metrics,
        SprintInsights insights)
    {
        html.AppendLine("<section class=\"slide\"><h2>Risks &amp; Blockers</h2><div class=\"content\">")
            .AppendLine("<ul class=\"risks\">");

        foreach (var risk in insights.RisksAndBlockers)
        {
            html.Append("<li>").Append(EscapeHtml(risk)).AppendLine("</li>");
        }

        html.AppendLine("</ul>");

        if (metrics.BlockedTaskTitles.Any())
        {
            html.AppendLine("<h3>Blocked Items</h3><ul class=\"blocked-tasks\">");
            foreach (var blockedTask in metrics.BlockedTaskTitles)
            {
                html.Append("<li>").Append(EscapeHtml(blockedTask)).AppendLine("</li>");
            }

            html.AppendLine("</ul>");
        }

        html.AppendLine("</div></section>");
    }

    private static string EscapeHtml(string? input) => WebUtility.HtmlEncode(input ?? string.Empty);

    private static long EstimateFileSizeBytes(SprintMetrics metrics, SprintInsights insights)
    {
        var contentLength = insights.ExecutiveSummary.Length
            + insights.KeyHighlights.Sum(item => item.Length)
            + insights.RisksAndBlockers.Sum(item => item.Length)
            + insights.Recommendations.Sum(item => item.Length);

        return 50_000L + (contentLength * 10L) + (metrics.WorkloadByAssignee.Count * 500L) + 20_000L;
    }

    private static string GetPresentationStyles()
    {
        return """
            <style>
            * { box-sizing: border-box; }
            html, body { margin: 0; padding: 0; }
            body { font-family: "Segoe UI", Arial, sans-serif; color: #243447; background: #e9eef5; }
            .slide { width: 100%; min-height: 100vh; padding: 60px; background: #fff; page-break-after: always; }
            .title-slide { display: flex; flex-direction: column; justify-content: center; align-items: center; text-align: center; color: #fff; background: linear-gradient(135deg, #3264a8, #603c8f); }
            .title-slide h1 { margin: 0 0 20px; font-size: 4rem; font-weight: 300; }
            .title-slide h2 { border: 0; color: #fff; font-size: 2.5rem; }
            .subtitle { font-size: 1.2rem; opacity: .85; }
            h2 { margin: 0 0 40px; padding-bottom: 10px; border-bottom: 3px solid #3279b7; color: #243447; font-size: 2.5rem; }
            .summary, .performance-narrative, .next-focus { padding: 20px; border-radius: 8px; background: #eef3f7; font-size: 1.2rem; }
            .metrics-grid { display: grid; grid-template-columns: repeat(4, minmax(150px, 1fr)); gap: 24px; margin-top: 30px; }
            .metric-card { padding: 30px 15px; border-radius: 12px; color: #fff; text-align: center; background: linear-gradient(135deg, #3279b7, #225685); }
            .metric-value { font-size: 2.5rem; font-weight: 700; }
            .highlights, .risks, .recommendations { padding: 0; list-style: none; }
            .highlights li, .risks li, .recommendations li { margin: 15px 0; padding: 20px; border-left: 5px solid #27864b; border-radius: 8px; background: #f5f8fa; font-size: 1.1rem; }
            .risks li { border-left-color: #c73535; background: #fff3f3; }
            .recommendations li { border-left-color: #d49b00; background: #fff9e8; }
            table { width: 100%; border-collapse: collapse; }
            th, td { padding: 14px; border-bottom: 1px solid #d6dde5; text-align: left; }
            th { color: #fff; background: #34495e; }
            .blocked-tasks { padding: 20px 40px; border-radius: 8px; color: #a32424; background: #fff3f3; }
            @media (max-width: 800px) { .slide { padding: 30px; } .metrics-grid { grid-template-columns: repeat(2, 1fr); } }
            @media print { .slide { min-height: 100vh; } }
            </style>
            """;
    }
}
