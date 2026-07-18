using WorkingSprintAgent.Models;
using System.Text;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Enhanced presentation builder service supporting multiple output formats
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

    public byte[] BuildPowerPointPresentation(SprintMetrics metrics, SprintInsights insights, PresentationOptions? options = null)
    {
        options ??= new PresentationOptions();
        
        _logger.LogInformation("Building PowerPoint presentation for sprint '{SprintName}' using template '{Template}'", 
            metrics.SprintName, options.Template);

        try
        {
            return _powerPointService.CreatePresentationFromTemplate(metrics, insights, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PowerPoint presentation for sprint '{SprintName}'. Falling back to HTML.", metrics.SprintName);
            
            // Fallback to HTML if PowerPoint generation fails
            return BuildPresentation(metrics, insights);
        }
    }

    public byte[] BuildPresentation(SprintMetrics metrics, SprintInsights insights)
    {
        _logger.LogInformation("Building HTML presentation for sprint '{SprintName}'", metrics.SprintName);

        var html = BuildHTMLPresentation(metrics, insights);
        var htmlBytes = Encoding.UTF8.GetBytes(html);
        
        _logger.LogInformation("Generated HTML presentation with {Size} bytes", htmlBytes.Length);
        
        return htmlBytes;
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

        var chartTypes = new List<string>
        {
            "Completion Rate Chart",
            "Task Status Distribution", 
            "Team Performance Metrics"
        };

        if (metrics.TasksByPriority.Any())
            chartTypes.Add("Priority Breakdown");

        if (metrics.TotalStoryPoints > 0)
            chartTypes.Add("Story Points Progress");

        return new PresentationSummary
        {
            Title = $"{metrics.SprintName} - Sprint Report",
            SlideCount = slideTopics.Count,
            SlideTopics = slideTopics,
            ChartTypes = chartTypes,
            EstimatedViewingTimeMinutes = Math.Max(5, slideTopics.Count * 2), // 2 minutes per slide minimum
            GeneratedAt = DateTime.UtcNow,
            Template = "Professional",
            EstimatedFileSizeBytes = EstimateFileSizeBytes(metrics, insights)
        };
    }

    public List<PresentationTemplate> GetAvailableTemplates()
    {
        return new List<PresentationTemplate>
        {
            new PresentationTemplate
            {
                Id = "professional",
                Name = "Professional",
                Description = "Clean, corporate design suitable for stakeholder presentations",
                Features = new List<string> { "Professional color scheme", "Clear charts", "Executive summary focus" },
                RequiresCompanyBranding = false
            },
            new PresentationTemplate
            {
                Id = "modern",
                Name = "Modern",
                Description = "Contemporary design with vibrant colors and modern typography",
                Features = new List<string> { "Modern typography", "Gradient backgrounds", "Interactive elements" },
                RequiresCompanyBranding = false
            },
            new PresentationTemplate
            {
                Id = "corporate",
                Name = "Corporate",
                Description = "Formal template with company branding integration",
                Features = new List<string> { "Company logo integration", "Brand color compliance", "Formal layout" },
                RequiresCompanyBranding = true
            },
            new PresentationTemplate
            {
                Id = "minimal",
                Name = "Minimal",
                Description = "Clean, distraction-free design focusing on content",
                Features = new List<string> { "Minimal design", "Focus on content", "High readability" },
                RequiresCompanyBranding = false
            }
        };
    }

    #region Private Helper Methods
    private string BuildHTMLPresentation(SprintMetrics metrics, SprintInsights insights)
    {
        var html = new StringBuilder();
        
        html.Append("<!DOCTYPE html>");
        html.Append("<html lang=\"en\">");
        html.Append("<head>");
        html.Append("<meta charset=\"UTF-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.Append("<title>Sprint Report - ");
        html.Append(EscapeHtml(metrics.SprintName));
        html.Append("</title>");
        html.Append(GetPresentationStyles());
        html.Append("</head>");
        html.Append("<body>");
        
        // Title slide
        html.Append("<div class=\"slide title-slide\">");
        html.Append("<h1>Sprint Report</h1>");
        html.Append("<h2>");
        html.Append(EscapeHtml(metrics.SprintName));
        html.Append("</h2>");
        html.Append("<p class=\"subtitle\">Generated on ");
        html.Append(DateTime.Now.ToString("MMMM dd, yyyy"));
        html.Append("</p>");
        html.Append("</div>");

        // Executive Summary slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Executive Summary</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<p class=\"summary\">");
        html.Append(EscapeHtml(insights.ExecutiveSummary));
        html.Append("</p>");
        
        html.Append("<div class=\"metrics-grid\">");
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.CompletionRatePercent.ToString("F1"));
        html.Append("%</div>");
        html.Append("<div class=\"metric-label\">Completion Rate</div>");
        html.Append("</div>");
        
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.CompletedTasks);
        html.Append("/");
        html.Append(metrics.TotalTasks);
        html.Append("</div>");
        html.Append("<div class=\"metric-label\">Tasks Completed</div>");
        html.Append("</div>");
        
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.CompletedStoryPoints.ToString("F1"));
        html.Append("</div>");
        html.Append("<div class=\"metric-label\">Story Points</div>");
        html.Append("</div>");
        
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.BlockedTasks);
        html.Append("</div>");
        html.Append("<div class=\"metric-label\">Blocked Tasks</div>");
        html.Append("</div>");
        html.Append("</div>");
        html.Append("</div>");
        html.Append("</div>");

        // Key Highlights slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Key Highlights</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<ul class=\"highlights\">");
        foreach (var highlight in insights.KeyHighlights)
        {
            html.Append("<li>");
            html.Append(EscapeHtml(highlight));
            html.Append("</li>");
        }
        html.Append("</ul>");
        html.Append("</div>");
        html.Append("</div>");

        // Team Performance slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Team Performance</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<p class=\"performance-narrative\">");
        html.Append(EscapeHtml(insights.TeamPerformanceNarrative));
        html.Append("</p>");
        
        if (metrics.WorkloadByAssignee.Any())
        {
            html.Append("<div class=\"team-table\">");
            html.Append("<table>");
            html.Append("<thead>");
            html.Append("<tr>");
            html.Append("<th>Team Member</th>");
            html.Append("<th>Total Tasks</th>");
            html.Append("<th>Completed</th>");
            html.Append("<th>Story Points</th>");
            html.Append("<th>Completion %</th>");
            html.Append("</tr>");
            html.Append("</thead>");
            html.Append("<tbody>");
            
            foreach (var assignee in metrics.WorkloadByAssignee)
            {
                var completionRate = assignee.TotalTasks > 0 
                    ? (assignee.CompletedTasks * 100.0 / assignee.TotalTasks) 
                    : 0;
                
                html.Append("<tr>");
                html.Append("<td>");
                html.Append(EscapeHtml(assignee.Assignee));
                html.Append("</td>");
                html.Append("<td>");
                html.Append(assignee.TotalTasks);
                html.Append("</td>");
                html.Append("<td>");
                html.Append(assignee.CompletedTasks);
                html.Append("</td>");
                html.Append("<td>");
                html.Append(assignee.StoryPoints.ToString("F1"));
                html.Append("</td>");
                html.Append("<td>");
                html.Append(completionRate.ToString("F0"));
                html.Append("%</td>");
                html.Append("</tr>");
            }
            
            html.Append("</tbody>");
            html.Append("</table>");
            html.Append("</div>");
        }
        html.Append("</div>");
        html.Append("</div>");

        // Risks and Blockers slide
        if (insights.RisksAndBlockers.Any())
        {
            html.Append("<div class=\"slide\">");
            html.Append("<h2>Risks & Blockers</h2>");
            html.Append("<div class=\"content\">");
            html.Append("<ul class=\"risks\">");
            foreach (var risk in insights.RisksAndBlockers)
            {
                html.Append("<li>");
                html.Append(EscapeHtml(risk));
                html.Append("</li>");
            }
            html.Append("</ul>");
            
            if (metrics.BlockedTaskTitles.Any())
            {
                html.Append("<h3>Blocked Items</h3>");
                html.Append("<ul class=\"blocked-tasks\">");
                foreach (var blockedTask in metrics.BlockedTaskTitles)
                {
                    html.Append("<li>");
                    html.Append(EscapeHtml(blockedTask));
                    html.Append("</li>");
                }
                html.Append("</ul>");
            }
            html.Append("</div>");
            html.Append("</div>");
        }

        // Recommendations slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Recommendations</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<ul class=\"recommendations\">");
        foreach (var recommendation in insights.Recommendations)
        {
            html.Append("<li>");
            html.Append(EscapeHtml(recommendation));
            html.Append("</li>");
        }
        html.Append("</ul>");
        html.Append("</div>");
        html.Append("</div>");

        // Next Sprint Focus slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Next Sprint Focus</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<p class=\"next-focus\">");
        html.Append(EscapeHtml(insights.NextSprintFocus));
        html.Append("</p>");
        html.Append("</div>");
        html.Append("</div>");

        html.Append("</body>");
        html.Append("</html>");

        return html.ToString();
    }

    private static long EstimateFileSizeBytes(SprintMetrics metrics, SprintInsights insights)
    {
        // Rough estimation based on content
        var baseSize = 50000; // Base PowerPoint size
        var contentSize = (insights.ExecutiveSummary.Length + 
                          insights.KeyHighlights.Sum(h => h.Length) +
                          insights.RisksAndBlockers.Sum(r => r.Length) +
                          insights.Recommendations.Sum(r => r.Length)) * 10; // Estimate formatting overhead
        
        var teamSize = metrics.WorkloadByAssignee.Count * 500; // Team data
        var chartsSize = 20000; // Estimated chart data
        
        return baseSize + contentSize + teamSize + chartsSize;
    }
        html.Append("<html lang=\"en\">");
        html.Append("<head>");
        html.Append("<meta charset=\"UTF-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.Append("<title>Sprint Report - ");
        html.Append(EscapeHtml(metrics.SprintName));
        html.Append("</title>");
        html.Append(GetPresentationStyles());
        html.Append("</head>");
        html.Append("<body>");
        
        // Title slide
        html.Append("<div class=\"slide title-slide\">");
        html.Append("<h1>Sprint Report</h1>");
        html.Append("<h2>");
        html.Append(EscapeHtml(metrics.SprintName));
        html.Append("</h2>");
        html.Append("<p class=\"subtitle\">Generated on ");
        html.Append(DateTime.Now.ToString("MMMM dd, yyyy"));
        html.Append("</p>");
        html.Append("</div>");

        // Executive Summary slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Executive Summary</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<p class=\"summary\">");
        html.Append(EscapeHtml(insights.ExecutiveSummary));
        html.Append("</p>");
        
        html.Append("<div class=\"metrics-grid\">");
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.CompletionRatePercent.ToString("F1"));
        html.Append("%</div>");
        html.Append("<div class=\"metric-label\">Completion Rate</div>");
        html.Append("</div>");
        
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.CompletedTasks);
        html.Append("/");
        html.Append(metrics.TotalTasks);
        html.Append("</div>");
        html.Append("<div class=\"metric-label\">Tasks Completed</div>");
        html.Append("</div>");
        
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.CompletedStoryPoints.ToString("F1"));
        html.Append("</div>");
        html.Append("<div class=\"metric-label\">Story Points</div>");
        html.Append("</div>");
        
        html.Append("<div class=\"metric-card\">");
        html.Append("<div class=\"metric-value\">");
        html.Append(metrics.BlockedTasks);
        html.Append("</div>");
        html.Append("<div class=\"metric-label\">Blocked Tasks</div>");
        html.Append("</div>");
        html.Append("</div>");
        html.Append("</div>");
        html.Append("</div>");

        // Key Highlights slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Key Highlights</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<ul class=\"highlights\">");
        foreach (var highlight in insights.KeyHighlights)
        {
            html.Append("<li>");
            html.Append(EscapeHtml(highlight));
            html.Append("</li>");
        }
        html.Append("</ul>");
        html.Append("</div>");
        html.Append("</div>");

        // Team Performance slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Team Performance</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<p class=\"performance-narrative\">");
        html.Append(EscapeHtml(insights.TeamPerformanceNarrative));
        html.Append("</p>");
        
        if (metrics.WorkloadByAssignee.Any())
        {
            html.Append("<div class=\"team-table\">");
            html.Append("<table>");
            html.Append("<thead>");
            html.Append("<tr>");
            html.Append("<th>Team Member</th>");
            html.Append("<th>Total Tasks</th>");
            html.Append("<th>Completed</th>");
            html.Append("<th>Story Points</th>");
            html.Append("<th>Completion %</th>");
            html.Append("</tr>");
            html.Append("</thead>");
            html.Append("<tbody>");
            
            foreach (var assignee in metrics.WorkloadByAssignee)
            {
                var completionRate = assignee.TotalTasks > 0 
                    ? (assignee.CompletedTasks * 100.0 / assignee.TotalTasks) 
                    : 0;
                
                html.Append("<tr>");
                html.Append("<td>");
                html.Append(EscapeHtml(assignee.Assignee));
                html.Append("</td>");
                html.Append("<td>");
                html.Append(assignee.TotalTasks);
                html.Append("</td>");
                html.Append("<td>");
                html.Append(assignee.CompletedTasks);
                html.Append("</td>");
                html.Append("<td>");
                html.Append(assignee.StoryPoints.ToString("F1"));
                html.Append("</td>");
                html.Append("<td>");
                html.Append(completionRate.ToString("F0"));
                html.Append("%</td>");
                html.Append("</tr>");
            }
            
            html.Append("</tbody>");
            html.Append("</table>");
            html.Append("</div>");
        }
        html.Append("</div>");
        html.Append("</div>");

        // Risks and Blockers slide
        if (insights.RisksAndBlockers.Any())
        {
            html.Append("<div class=\"slide\">");
            html.Append("<h2>Risks & Blockers</h2>");
            html.Append("<div class=\"content\">");
            html.Append("<ul class=\"risks\">");
            foreach (var risk in insights.RisksAndBlockers)
            {
                html.Append("<li>");
                html.Append(EscapeHtml(risk));
                html.Append("</li>");
            }
            html.Append("</ul>");
            
            if (metrics.BlockedTaskTitles.Any())
            {
                html.Append("<h3>Blocked Items</h3>");
                html.Append("<ul class=\"blocked-tasks\">");
                foreach (var blockedTask in metrics.BlockedTaskTitles)
                {
                    html.Append("<li>");
                    html.Append(EscapeHtml(blockedTask));
                    html.Append("</li>");
                }
                html.Append("</ul>");
            }
            html.Append("</div>");
            html.Append("</div>");
        }

        // Recommendations slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Recommendations</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<ul class=\"recommendations\">");
        foreach (var recommendation in insights.Recommendations)
        {
            html.Append("<li>");
            html.Append(EscapeHtml(recommendation));
            html.Append("</li>");
        }
        html.Append("</ul>");
        html.Append("</div>");
        html.Append("</div>");

        // Next Sprint Focus slide
        html.Append("<div class=\"slide\">");
        html.Append("<h2>Next Sprint Focus</h2>");
        html.Append("<div class=\"content\">");
        html.Append("<p class=\"next-focus\">");
        html.Append(EscapeHtml(insights.NextSprintFocus));
        html.Append("</p>");
        html.Append("</div>");
        html.Append("</div>");

        html.Append("</body>");
        html.Append("</html>");

        var htmlBytes = Encoding.UTF8.GetBytes(html.ToString());
        
        _logger.LogInformation("Generated HTML presentation with {Size} bytes", htmlBytes.Length);
        
        return htmlBytes;
    }

    private static string EscapeHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    private static string GetPresentationStyles()
    {
        return @"<style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        
        body { 
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }
        
        .slide {
            width: 100vw;
            height: 100vh;
            padding: 60px;
            display: flex;
            flex-direction: column;
            page-break-after: always;
            background: white;
            margin-bottom: 20px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }
        
        .title-slide {
            justify-content: center;
            align-items: center;
            text-align: center;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
        }
        
        .title-slide h1 {
            font-size: 4rem;
            margin-bottom: 20px;
            font-weight: 300;
        }
        
        .title-slide h2 {
            font-size: 2.5rem;
            margin-bottom: 20px;
            opacity: 0.9;
        }
        
        .subtitle {
            font-size: 1.2rem;
            opacity: 0.8;
        }
        
        h2 {
            font-size: 2.5rem;
            color: #2c3e50;
            margin-bottom: 40px;
            border-bottom: 3px solid #3498db;
            padding-bottom: 10px;
        }
        
        .content {
            flex: 1;
            overflow: hidden;
        }
        
        .summary {
            font-size: 1.3rem;
            color: #2c3e50;
            margin-bottom: 40px;
            padding: 20px;
            background: #ecf0f1;
            border-radius: 8px;
            border-left: 5px solid #3498db;
        }
        
        .metrics-grid {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 30px;
            margin-top: 30px;
        }
        
        .metric-card {
            background: linear-gradient(135deg, #3498db, #2980b9);
            color: white;
            padding: 30px;
            border-radius: 12px;
            text-align: center;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
        }
        
        .metric-value {
            font-size: 2.5rem;
            font-weight: bold;
            margin-bottom: 10px;
        }
        
        .metric-label {
            font-size: 1rem;
            opacity: 0.9;
        }
        
        .highlights, .risks, .recommendations {
            list-style: none;
            padding: 0;
        }
        
        .highlights li, .risks li, .recommendations li {
            background: #f8f9fa;
            margin: 15px 0;
            padding: 20px;
            border-radius: 8px;
            border-left: 5px solid #28a745;
            font-size: 1.1rem;
        }
        
        .risks li {
            border-left-color: #dc3545;
            background: #fff5f5;
        }
        
        .recommendations li {
            border-left-color: #ffc107;
            background: #fffdf0;
        }
        
        .performance-narrative, .next-focus {
            font-size: 1.2rem;
            color: #2c3e50;
            margin-bottom: 30px;
            padding: 20px;
            background: #f8f9fa;
            border-radius: 8px;
        }
        
        .team-table {
            margin-top: 30px;
        }
        
        table {
            width: 100%;
            border-collapse: collapse;
            background: white;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        
        th, td {
            padding: 15px;
            text-align: left;
            border-bottom: 1px solid #ddd;
        }
        
        th {
            background: #34495e;
            color: white;
            font-weight: 600;
        }
        
        tr:hover {
            background: #f5f5f5;
        }
        
        .blocked-tasks {
            background: #fff5f5;
            padding: 20px;
            border-radius: 8px;
            margin-top: 20px;
        }
        
        .blocked-tasks li {
            color: #dc3545;
            margin: 10px 0;
            font-weight: 500;
        }
        
        @media print {
            body { background: white; }
            .slide { box-shadow: none; margin-bottom: 0; }
        }
        </style>";
    }
}