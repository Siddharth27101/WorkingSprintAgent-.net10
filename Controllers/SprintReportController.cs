using Microsoft.AspNetCore.Mvc;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services;

namespace WorkingSprintAgent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SprintReportController : ControllerBase
{
    private readonly ICsvSprintDataService _csvService;
    private readonly IInsightGenerationService _insightService;
    private readonly IPresentationBuilderService _presentationService;
    private readonly ILogger<SprintReportController> _logger;

    public SprintReportController(
        ICsvSprintDataService csvService,
        IInsightGenerationService insightService,
        IPresentationBuilderService presentationService,
        ILogger<SprintReportController> logger)
    {
        _csvService = csvService;
        _insightService = insightService;
        _presentationService = presentationService;
        _logger = logger;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GenerateSprintReport(
        IFormFile csvFile,
        [FromForm] string? sprintName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest(new { error = "Please upload a valid CSV file." });
            }

            if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "File must be a CSV file." });
            }

            if (csvFile.Length > 10 * 1024 * 1024)
            {
                return BadRequest(new { error = "File size must be less than 10MB." });
            }

            _logger.LogInformation("Processing sprint report generation for file: {FileName} ({Size} bytes)", 
                csvFile.FileName, csvFile.Length);

            List<SprintTask> tasks;
            using (var stream = csvFile.OpenReadStream())
            {
                tasks = await _csvService.ParseAsync(stream);
            }

            if (tasks.Count == 0)
            {
                return BadRequest(new { error = "No valid tasks found in CSV file. Please check the format." });
            }

            var metrics = _csvService.ComputeMetrics(tasks, sprintName);
            var insights = await _insightService.GenerateInsightsAsync(metrics, cancellationToken);
            var presentationBytes = _presentationService.BuildPresentation(metrics, insights);

            _logger.LogInformation("Successfully generated sprint report for {TaskCount} tasks", tasks.Count);

            var fileName = $"Sprint_Report_{metrics.SprintName}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            return File(presentationBytes, "text/html", fileName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sprint report generation was cancelled");
            return StatusCode(499, new { error = "Request was cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sprint report");
            return StatusCode(500, new { error = "An error occurred while generating the report. Please try again." });
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewSprintData(
        IFormFile csvFile,
        [FromForm] string? sprintName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (csvFile == null || csvFile.Length == 0)
            {
                return BadRequest(new { error = "Please upload a valid CSV file." });
            }

            _logger.LogInformation("Processing sprint data preview for file: {FileName}", csvFile.FileName);

            List<SprintTask> tasks;
            using (var stream = csvFile.OpenReadStream())
            {
                tasks = await _csvService.ParseAsync(stream);
            }

            if (tasks.Count == 0)
            {
                return BadRequest(new { error = "No valid tasks found in CSV file." });
            }

            var metrics = _csvService.ComputeMetrics(tasks, sprintName);
            var insights = await _insightService.GenerateInsightsAsync(metrics, cancellationToken);

            var preview = new
            {
                SprintName = metrics.SprintName,
                TaskCount = tasks.Count,
                Metrics = new
                {
                    metrics.TotalTasks,
                    metrics.CompletedTasks,
                    metrics.BlockedTasks,
                    metrics.TotalStoryPoints,
                    metrics.CompletedStoryPoints,
                    metrics.CompletionRatePercent,
                    metrics.TasksByStatus,
                    metrics.TasksByType,
                    metrics.TasksByPriority,
                    TeamMembers = metrics.WorkloadByAssignee.Count,
                    BlockedItems = metrics.BlockedTaskTitles.Count
                },
                Insights = new
                {
                    insights.ExecutiveSummary,
                    HighlightCount = insights.KeyHighlights.Count,
                    RiskCount = insights.RisksAndBlockers.Count,
                    RecommendationCount = insights.Recommendations.Count,
                    insights.NextSprintFocus
                },
                SampleTasks = tasks.Take(5).Select(t => new
                {
                    t.TaskId,
                    t.Title,
                    t.Status,
                    t.Assignee,
                    t.StoryPoints
                })
            };

            return Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing sprint data");
            return StatusCode(500, new { error = "An error occurred while processing the data." });
        }
    }

    [HttpGet("csv-format")]
    public IActionResult GetCsvFormatInfo()
    {
        var formatInfo = new
        {
            Description = "Sprint data CSV format specification",
            RequiredColumns = new[]
            {
                "TaskId (or ID, Key, IssueKey)",
                "Title (or Summary, TaskName, Name)",
                "Status (or State)",
                "Assignee (or Owner, AssignedTo)"
            },
            OptionalColumns = new[]
            {
                "Type (or IssueType, WorkItemType)",
                "Priority",
                "StoryPoints (or Points, Estimate)",
                "SprintName (or Sprint)",
                "StartDate (or Created)",
                "EndDate (or Resolved, CompletedDate)"
            },
            Notes = new[]
            {
                "Column names are case-insensitive and can contain spaces or underscores",
                "At minimum, provide TaskId/Title, Status, and Assignee columns",
                "StoryPoints should be numeric values",
                "Dates should be in a recognizable format (YYYY-MM-DD, MM/DD/YYYY, etc.)",
                "Status values like 'Done', 'Completed', 'Closed' are treated as completed",
                "Status 'Blocked' or Priority 'Critical' (if not done) are treated as blocked"
            },
            SampleRow = "TASK-123,Implement user login,John Doe,Done,Story,High,5,Sprint 1,2024-01-01,2024-01-15"
        };

        return Ok(formatInfo);
    }

    [HttpGet("health")]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Services = new
            {
                CsvParser = "Ready",
                InsightGeneration = "Ready (Mock Mode)",
                PresentationBuilder = "Ready"
            }
        });
    }
}