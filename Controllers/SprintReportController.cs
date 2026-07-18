using Microsoft.AspNetCore.Mvc;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services;
using System.ComponentModel.DataAnnotations;

namespace WorkingSprintAgent.Controllers;

/// <summary>
/// AI-powered sprint report generation controller with comprehensive analytics and cost optimization
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Sprint Reports")]
public class SprintReportController : ControllerBase
{
    private readonly ICsvSprintDataService _csvService;
    private readonly IInsightGenerationService _insightService;
    private readonly IPresentationBuilderService _presentationService;
    private readonly IOpenAIService _openAIService;
    private readonly ICostMonitoringService _costMonitoring;
    private readonly ITokenOptimizationService _tokenOptimization;
    private readonly ILogger<SprintReportController> _logger;

    public SprintReportController(
        ICsvSprintDataService csvService,
        IInsightGenerationService insightService,
        IPresentationBuilderService presentationService,
        IOpenAIService openAIService,
        ICostMonitoringService costMonitoring,
        ITokenOptimizationService tokenOptimization,
        ILogger<SprintReportController> logger)
    {
        _csvService = csvService;
        _insightService = insightService;
        _presentationService = presentationService;
        _openAIService = openAIService;
        _costMonitoring = costMonitoring;
        _tokenOptimization = tokenOptimization;
        _logger = logger;
    }

    /// <summary>
    /// Generate comprehensive sprint presentation with AI-powered insights
    /// </summary>
    /// <param name="csvFile">CSV file containing sprint data with required columns: TaskId, Title, Status, Assignee</param>
    /// <param name="sprintName">Optional custom sprint name</param>
    /// <param name="outputFormat">Output format: powerpoint (default) or html</param>
    /// <param name="template">Presentation template: professional, modern, corporate, minimal</param>
    /// <param name="companyName">Company name for branding (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Professional presentation file ready for stakeholders</returns>
    /// <response code="200">Successfully generated presentation</response>
    /// <response code="400">Invalid file or parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> GenerateSprintReport(
        [Required] IFormFile csvFile,
        [FromForm] string? sprintName = null,
        [FromForm] string outputFormat = "powerpoint",
        [FromForm] string template = "professional", 
        [FromForm] string? companyName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate input
            var validationResult = ValidateGenerateRequest(csvFile, outputFormat, template);
            if (validationResult != null) return validationResult;

            _logger.LogInformation("Processing sprint report generation for file: {FileName} ({Size} bytes), Format: {Format}, Template: {Template}", 
                csvFile.FileName, csvFile.Length, outputFormat, template);

            // Parse CSV data
            List<SprintTask> tasks;
            using (var stream = csvFile.OpenReadStream())
            {
                tasks = await _csvService.ParseAsync(stream);
            }

            if (tasks.Count == 0)
            {
                return BadRequest(new { error = "No valid tasks found in CSV file. Please check the format.", 
                                       helpUrl = "/api/sprintreport/csv-format" });
            }

            // Generate metrics and insights
            var metrics = _csvService.ComputeMetrics(tasks, sprintName);
            var aiResponse = await _insightService.GenerateEnhancedInsightsAsync(metrics, cancellationToken);
            var insights = aiResponse.Insights;

            // Generate presentation based on format
            byte[] presentationBytes;
            string contentType;
            string fileExtension;

            if (outputFormat.ToLower() == "powerpoint")
            {
                var presentationOptions = new PresentationOptions
                {
                    Template = template,
                    CompanyName = companyName ?? string.Empty,
                    OutputFormat = PresentationFormat.PowerPoint,
                    IncludeCharts = true,
                    IncludeDetailedMetrics = true,
                    IncludeTeamBreakdown = tasks.Count <= 100, // Skip for very large datasets
                    IncludeRecommendations = true
                };

                presentationBytes = _presentationService.BuildPowerPointPresentation(metrics, insights, presentationOptions);
                contentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                fileExtension = "pptx";
            }
            else
            {
                presentationBytes = _presentationService.BuildPresentation(metrics, insights);
                contentType = "text/html";
                fileExtension = "html";
            }

            var fileName = $"Sprint_Report_{SanitizeFileName(metrics.SprintName)}_{DateTime.Now:yyyyMMdd_HHmmss}.{fileExtension}";
            
            // Log success with AI metadata
            _logger.LogInformation("Successfully generated {Format} sprint report for {TaskCount} tasks. " +
                                 "AI Cost: ${Cost:F4}, Tokens: {Tokens}, From Cache: {FromCache}, Optimizations: {OptCount}",
                outputFormat, tasks.Count, aiResponse.TokenUsage.EstimatedCost, aiResponse.TokenUsage.TotalTokens, 
                aiResponse.FromCache, aiResponse.OptimizationSuggestions.Count);

            return File(presentationBytes, contentType, fileName);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Sprint report generation was cancelled");
            return StatusCode(499, new { error = "Request was cancelled" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sprint report");
            return StatusCode(500, new { 
                error = "An error occurred while generating the report. Please try again.",
                supportInfo = "Check the CSV format requirements at /api/sprintreport/csv-format"
            });
        }
    }

    /// <summary>
    /// Preview sprint data and get AI-powered insights without generating full presentation
    /// </summary>
    /// <param name="csvFile">CSV file containing sprint data</param>
    /// <param name="sprintName">Optional custom sprint name</param>
    /// <param name="includeOptimization">Include cost optimization analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Preview data with metrics, insights, and optimization recommendations</returns>
    /// <response code="200">Successfully analyzed sprint data</response>
    /// <response code="400">Invalid file or data</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    [ProducesResponseType(typeof(object), 500)]
    public async Task<IActionResult> PreviewSprintData(
        [Required] IFormFile csvFile,
        [FromForm] string? sprintName = null,
        [FromForm] bool includeOptimization = false,
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
                return BadRequest(new { error = "No valid tasks found in CSV file.", 
                                       helpUrl = "/api/sprintreport/csv-format" });
            }

            var metrics = _csvService.ComputeMetrics(tasks, sprintName);
            var aiResponse = await _insightService.GenerateEnhancedInsightsAsync(metrics, cancellationToken);
            var insights = aiResponse.Insights;

            // Build comprehensive preview response
            var preview = new
            {
                SprintName = metrics.SprintName,
                TaskCount = tasks.Count,
                ProcessedAt = DateTime.UtcNow,
                
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
                    KeyHighlights = insights.KeyHighlights.Take(5),
                    RisksAndBlockers = insights.RisksAndBlockers.Take(3),
                    Recommendations = insights.Recommendations.Take(4),
                    insights.TeamPerformanceNarrative,
                    insights.NextSprintFocus
                },
                
                AIMetadata = new
                {
                    ServiceEnabled = _insightService.IsAIEnabled,
                    FromCache = aiResponse.FromCache,
                    TokenUsage = new
                    {
                        aiResponse.TokenUsage.TotalTokens,
                        aiResponse.TokenUsage.EstimatedCost,
                        aiResponse.TokenUsage.Model,
                        ResponseTime = aiResponse.TokenUsage.ResponseTime.TotalSeconds
                    },
                    OptimizationSuggestions = aiResponse.OptimizationSuggestions.Take(3)
                },
                
                PresentationOptions = new
                {
                    AvailableTemplates = _presentationService.GetAvailableTemplates().Select(t => new { t.Id, t.Name, t.Description }),
                    AvailableFormats = new[] { "powerpoint", "html" },
                    EstimatedSlides = 8,
                    EstimatedViewingTime = "12-16 minutes"
                },
                
                SampleTasks = tasks.Take(5).Select(t => new
                {
                    t.TaskId,
                    t.Title,
                    t.Status,
                    t.Assignee,
                    t.StoryPoints,
                    t.Type,
                    t.Priority
                })
            };

            // Add optimization analysis if requested
            if (includeOptimization)
            {
                var optimizedData = _tokenOptimization.OptimizeSprintData(metrics);
                var costSavings = await _tokenOptimization.EstimateSavings(metrics, new List<OptimizationStrategy> 
                { 
                    OptimizationStrategy.DataCompression, 
                    OptimizationStrategy.PromptOptimization,
                    OptimizationStrategy.ResponseCaching
                });

                preview = new
                {
                    preview.SprintName,
                    preview.TaskCount,
                    preview.ProcessedAt,
                    preview.Metrics,
                    preview.Insights,
                    preview.AIMetadata,
                    preview.PresentationOptions,
                    preview.SampleTasks,
                    
                    OptimizationAnalysis = new
                    {
                        DataCompression = new
                        {
                            OriginalTokens = optimizedData.OriginalTokenCount,
                            OptimizedTokens = optimizedData.OptimizedTokenCount,
                            CompressionRatio = optimizedData.CompressionRatio,
                            TokensSaved = optimizedData.OriginalTokenCount - optimizedData.OptimizedTokenCount
                        },
                        CostSavings = new
                        {
                            costSavings.OriginalCost,
                            costSavings.OptimizedCost,
                            costSavings.Savings,
                            costSavings.SavingsPercentage,
                            costSavings.RecommendedAction
                        }
                    }
                };
            }

            _logger.LogInformation("Generated preview for sprint '{SprintName}' with {TaskCount} tasks. " +
                                 "AI Cost: ${Cost:F4}, Optimization: {OptimizationIncluded}",
                metrics.SprintName, tasks.Count, aiResponse.TokenUsage.EstimatedCost, includeOptimization);

            return Ok(preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing sprint data");
            return StatusCode(500, new { error = "An error occurred while processing the data." });
        }
    }

    /// <summary>
    /// Get CSV format requirements and examples for sprint data upload
    /// </summary>
    /// <returns>Comprehensive CSV format guide with examples</returns>
    /// <response code="200">Format information retrieved successfully</response>
    [HttpGet("csv-format")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetCsvFormatInfo()
    {
        var formatInfo = new
        {
            Description = "Sprint data CSV format specification for AI-powered analysis",
            RequiredColumns = new[]
            {
                new { Name = "TaskId", Aliases = new[] { "ID", "Key", "IssueKey" }, Description = "Unique identifier for the task", Example = "PROJ-123" },
                new { Name = "Title", Aliases = new[] { "Summary", "TaskName", "Name" }, Description = "Task title or summary", Example = "Implement user authentication" },
                new { Name = "Status", Aliases = new[] { "State" }, Description = "Current task status", Example = "Done, In Progress, Blocked" },
                new { Name = "Assignee", Aliases = new[] { "Owner", "AssignedTo" }, Description = "Person assigned to the task", Example = "John Doe" }
            },
            OptionalColumns = new[]
            {
                new { Name = "Type", Aliases = new[] { "IssueType", "WorkItemType" }, Description = "Type of work item", Example = "Story, Bug, Task" },
                new { Name = "Priority", Aliases = new string[0], Description = "Task priority level", Example = "High, Medium, Low" },
                new { Name = "StoryPoints", Aliases = new[] { "Points", "Estimate" }, Description = "Effort estimation (numeric)", Example = "3, 5, 8" },
                new { Name = "SprintName", Aliases = new[] { "Sprint" }, Description = "Sprint identifier", Example = "Sprint 2024-Q1" },
                new { Name = "StartDate", Aliases = new[] { "Created" }, Description = "Task creation date", Example = "2024-01-01" },
                new { Name = "EndDate", Aliases = new[] { "Resolved", "CompletedDate" }, Description = "Task completion date", Example = "2024-01-15" }
            },
            StatusValues = new
            {
                Completed = new[] { "Done", "Completed", "Closed", "Resolved", "Finished" },
                InProgress = new[] { "In Progress", "Active", "Working", "Development" },
                Blocked = new[] { "Blocked", "Impediment", "On Hold" },
                Todo = new[] { "To Do", "New", "Open", "Backlog", "Ready" }
            },
            DataQualityTips = new[]
            {
                "Column names are case-insensitive and spaces/underscores are ignored",
                "Ensure consistent status naming across all tasks",
                "Story points should be numeric values (decimals allowed)",
                "Dates can be in various formats (YYYY-MM-DD, MM/DD/YYYY, etc.)",
                "Empty cells are acceptable for optional columns",
                "Remove any header rows beyond the first column definition row"
            },
            SampleCsvContent = @"TaskId,Title,Status,Assignee,Type,Priority,StoryPoints,SprintName
PROJ-123,Implement user login,Done,John Doe,Story,High,5,Sprint 2024-Q1
PROJ-124,Fix login bug,In Progress,Jane Smith,Bug,Critical,2,Sprint 2024-Q1
PROJ-125,Update user profile,To Do,Bob Johnson,Story,Medium,3,Sprint 2024-Q1
PROJ-126,Database migration,Blocked,Alice Brown,Task,High,8,Sprint 2024-Q1",
            AdvancedFeatures = new
            {
                AIOptimization = "The system automatically optimizes data processing to minimize AI costs",
                SmartCaching = "Identical data sets are cached to avoid redundant AI processing",
                DataCompression = "Large datasets are intelligently compressed before AI analysis",
                QualityValidation = "Automatic data quality checks and suggestions for improvement"
            },
            Limits = new
            {
                MaxFileSize = "10 MB",
                RecommendedTaskCount = "Up to 500 tasks for optimal performance",
                MaxTaskCount = "2000 tasks (larger files may experience slower processing)"
            }
        };

        return Ok(formatInfo);
    }

    /// <summary>
    /// Get comprehensive system health check with AI service status
    /// </summary>
    /// <returns>System health and service status information</returns>
    /// <response code="200">Health check completed successfully</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> HealthCheck()
    {
        var serviceStatus = _insightService.GetServiceStatus();
        var costDashboard = await _costMonitoring.GetDashboardDataAsync();
        var activeAlerts = await _costMonitoring.CheckCostAlertsAsync();

        var healthStatus = new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            
            Services = new
            {
                CsvParser = "Ready",
                InsightGeneration = serviceStatus.IsAIEnabled ? "AI-Powered (Ready)" : "Fallback (Ready)",
                PresentationBuilder = "Ready",
                TokenOptimization = "Ready", 
                CostMonitoring = "Ready"
            },
            
            AIServiceDetails = new
            {
                serviceStatus.IsAIEnabled,
                serviceStatus.ServiceType,
                serviceStatus.Model,
                serviceStatus.EstimatedCostPerRequest,
                serviceStatus.IsCachingEnabled,
                serviceStatus.IsTokenTrackingEnabled,
                Capabilities = serviceStatus.Capabilities.Take(3),
                CurrentLimitations = serviceStatus.Limitations.Take(2)
            },
            
            Performance = new
            {
                TodayRequests = costDashboard.TodayRequests,
                TodayTokens = costDashboard.TodayTokens,
                TodayCost = costDashboard.TodayCost,
                BudgetUtilization = costDashboard.BudgetUtilization,
                AverageResponseTime = costDashboard.Performance.AverageResponseTime.TotalSeconds,
                OptimizationScore = costDashboard.Performance.OptimizationScore
            },
            
            Alerts = new
            {
                ActiveAlertCount = activeAlerts.Count,
                CriticalAlerts = activeAlerts.Count(a => a.Severity >= CostAlertSeverity.Critical),
                RecentAlerts = activeAlerts.OrderByDescending(a => a.Timestamp).Take(3).Select(a => new
                {
                    a.Type,
                    a.Title,
                    a.Severity,
                    a.Timestamp
                })
            }
        };

        return Ok(healthStatus);
    }

    /// <summary>
    /// Get detailed AI service status and configuration
    /// </summary>
    /// <returns>Comprehensive AI service information</returns>
    /// <response code="200">AI status retrieved successfully</response>
    [HttpGet("ai-status")]
    [Tags("AI Services")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetAIStatus()
    {
        var serviceStatus = _insightService.GetServiceStatus();
        
        return Ok(new
        {
            LastChecked = DateTime.UtcNow,
            serviceStatus.IsAIEnabled,
            serviceStatus.ServiceType,
            serviceStatus.Model,
            serviceStatus.IsCachingEnabled,
            serviceStatus.IsTokenTrackingEnabled,
            serviceStatus.MaxDailyTokens,
            serviceStatus.EstimatedCostPerRequest,
            serviceStatus.Capabilities,
            serviceStatus.Limitations,
            
            Configuration = new
            {
                CostOptimizationEnabled = true,
                SmartCachingEnabled = serviceStatus.IsCachingEnabled,
                DataCompressionEnabled = true,
                BatchProcessingAvailable = true,
                FallbackModeAvailable = true
            },
            
            Recommendations = serviceStatus.IsAIEnabled ? 
                new[] { "AI service is fully operational", "Consider enabling data compression for cost optimization" } :
                new[] { "Configure OpenAI API key to enable AI features", "Currently using rule-based fallback insights" }
        });
    }

    /// <summary>
    /// Get token usage statistics and cost analysis
    /// </summary>
    /// <param name="days">Number of days to analyze (default: 7)</param>
    /// <returns>Detailed token usage and cost analysis</returns>
    /// <response code="200">Usage statistics retrieved successfully</response>
    [HttpGet("token-usage")]
    [Tags("AI Services")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetTokenUsage([FromQuery] int days = 7)
    {
        try
        {
            var fromDate = DateTime.UtcNow.AddDays(-Math.Abs(days));
            var usage = await _openAIService.GetTokenUsageAsync(fromDate);
            var dashboard = await _costMonitoring.GetDashboardDataAsync();
            var prediction = await _costMonitoring.PredictCostsAsync(30);

            return Ok(new
            {
                AnalysisPeriod = new
                {
                    FromDate = usage.FromDate,
                    ToDate = usage.ToDate,
                    Days = days
                },
                
                Summary = new
                {
                    usage.TotalRequests,
                    usage.TotalTokens,
                    usage.TotalInputTokens,
                    usage.TotalOutputTokens,
                    usage.TotalCost,
                    usage.AverageCostPerRequest
                },
                
                CacheEfficiency = new
                {
                    usage.CacheHits,
                    CacheMisses = usage.TotalRequests - usage.CacheHits,
                    HitRate = usage.TotalRequests > 0 ? (double)usage.CacheHits / usage.TotalRequests : 0,
                    usage.CacheSavings
                },
                
                DailyBreakdown = usage.DailyBreakdown.Select(d => new
                {
                    Date = d.Timestamp.ToString("yyyy-MM-dd"),
                    d.TotalTokens,
                    d.EstimatedCost,
                    RequestCount = int.Parse(d.RequestType.Split(' ')[0])
                }),
                
                CurrentPeriod = new
                {
                    dashboard.TodayCost,
                    dashboard.WeekCost,
                    dashboard.MonthCost,
                    dashboard.BudgetUtilization
                },
                
                Predictions = new
                {
                    prediction.PredictedCost,
                    prediction.ConfidenceLevel,
                    prediction.MinEstimate,
                    prediction.MaxEstimate,
                    TimeFrame = $"Next {prediction.DaysForward} days"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving token usage statistics");
            return StatusCode(500, new { error = "Unable to retrieve usage statistics" });
        }
    }

    /// <summary>
    /// Get cost monitoring dashboard data
    /// </summary>
    /// <returns>Real-time cost monitoring dashboard</returns>
    /// <response code="200">Dashboard data retrieved successfully</response>
    [HttpGet("cost-dashboard")]
    [Tags("AI Services")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetCostDashboard()
    {
        try
        {
            var dashboard = await _costMonitoring.GetDashboardDataAsync();
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cost dashboard data");
            return StatusCode(500, new { error = "Unable to retrieve dashboard data" });
        }
    }

    /// <summary>
    /// Get cost optimization recommendations
    /// </summary>
    /// <returns>Personalized cost optimization opportunities</returns>
    /// <response code="200">Optimization recommendations retrieved successfully</response>
    [HttpGet("optimization-recommendations")]
    [Tags("AI Services")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetOptimizationRecommendations()
    {
        try
        {
            var opportunities = await _costMonitoring.GetOptimizationOpportunitiesAsync();
            var usage = await _openAIService.GetTokenUsageAsync(DateTime.UtcNow.AddDays(-7));
            
            return Ok(new
            {
                LastAnalyzed = DateTime.UtcNow,
                TotalOpportunities = opportunities.Count,
                PotentialSavings = opportunities.Sum(o => o.PotentialSavings),
                
                HighPriorityRecommendations = opportunities
                    .Where(o => o.Priority <= 2)
                    .Select(o => new
                    {
                        o.Category,
                        o.Title,
                        o.Description,
                        o.PotentialSavings,
                        o.Priority,
                        o.ROIEstimate,
                        o.Benefits,
                        ImplementationSteps = o.ImplementationSteps.Take(3)
                    }),
                
                QuickWins = opportunities
                    .Where(o => o.ImplementationEffort <= 2 && o.PotentialSavings > 0)
                    .OrderByDescending(o => o.PotentialSavings / o.ImplementationEffort)
                    .Take(3)
                    .Select(o => new
                    {
                        o.Title,
                        o.Description,
                        o.PotentialSavings,
                        o.ImplementationEffort,
                        ROI = o.PotentialSavings / o.ImplementationEffort
                    }),
                
                CurrentEfficiency = new
                {
                    WeeklyCost = usage.TotalCost,
                    AverageTokensPerRequest = usage.TotalRequests > 0 ? usage.TotalTokens / usage.TotalRequests : 0,
                    CacheHitRate = usage.TotalRequests > 0 ? (double)usage.CacheHits / usage.TotalRequests : 0,
                    OptimizationScore = CalculateOptimizationScore(usage)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving optimization recommendations");
            return StatusCode(500, new { error = "Unable to retrieve optimization recommendations" });
        }
    }

    /// <summary>
    /// Get available presentation templates
    /// </summary>
    /// <returns>List of available presentation templates with details</returns>
    /// <response code="200">Templates retrieved successfully</response>
    [HttpGet("templates")]
    [Tags("Presentation Templates")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetAvailableTemplates()
    {
        var templates = _presentationService.GetAvailableTemplates();
        
        return Ok(new
        {
            TotalTemplates = templates.Count,
            Templates = templates.Select(t => new
            {
                t.Id,
                t.Name,
                t.Description,
                t.Features,
                t.RequiresCompanyBranding,
                SamplePreview = $"/api/templates/{t.Id}/preview" // Future endpoint
            }),
            
            Recommendations = new
            {
                ForStakeholders = "professional",
                ForTeamMeetings = "modern", 
                ForExecutives = "corporate",
                ForInternalUse = "minimal"
            }
        });
    }

    /// <summary>
    /// Export detailed cost analysis report
    /// </summary>
    /// <param name="format">Export format: CSV, JSON</param>
    /// <param name="days">Number of days to include (default: 30)</param>
    /// <returns>Cost analysis report in requested format</returns>
    /// <response code="200">Report exported successfully</response>
    /// <response code="400">Invalid format specified</response>
    [HttpGet("export-cost-report")]
    [Tags("AI Services")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> ExportCostReport([FromQuery] string format = "CSV", [FromQuery] int days = 30)
    {
        try
        {
            var validFormats = new[] { "CSV", "JSON" };
            if (!validFormats.Contains(format.ToUpper()))
            {
                return BadRequest(new { 
                    error = "Invalid format. Supported formats: " + string.Join(", ", validFormats),
                    supportedFormats = validFormats
                });
            }

            var fromDate = DateTime.UtcNow.AddDays(-Math.Abs(days));
            var reportData = await _costMonitoring.ExportCostReportAsync(format, fromDate);
            
            var contentType = format.ToUpper() switch
            {
                "JSON" => "application/json",
                _ => "text/csv"
            };
            
            var fileName = $"cost_analysis_report_{DateTime.Now:yyyyMMdd}_{days}days.{format.ToLower()}";
            
            return File(reportData, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting cost report");
            return StatusCode(500, new { error = "Unable to export cost report" });
        }
    }

    /// <summary>
    /// Get comprehensive usage analytics and insights
    /// </summary>
    /// <param name="days">Number of days to analyze (default: 7)</param>
    /// <returns>Detailed usage analytics with insights and recommendations</returns>
    /// <response code="200">Analytics retrieved successfully</response>
    [HttpGet("usage-analytics")]
    [Tags("AI Services")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetUsageAnalytics([FromQuery] int days = 7)
    {
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var tokenLogger = scope.ServiceProvider.GetService<ITokenUsageLogger>();
            
            if (tokenLogger == null)
            {
                return StatusCode(500, new { error = "Token usage logger not available" });
            }

            var timeRange = new TimeRange
            {
                StartDate = DateTime.UtcNow.AddDays(-Math.Abs(days)),
                EndDate = DateTime.UtcNow,
                Description = $"Last {days} days"
            };

            var analytics = await tokenLogger.GenerateAnalyticsSummaryAsync(timeRange);
            
            return Ok(new
            {
                GeneratedAt = DateTime.UtcNow,
                AnalysisPeriod = new
                {
                    timeRange.StartDate,
                    timeRange.EndDate,
                    timeRange.DurationDays,
                    timeRange.Description
                },
                
                Summary = new
                {
                    analytics.TokenAnalytics.TotalTokens,
                    analytics.CostAnalytics.TotalCost,
                    analytics.OptimizationAnalytics.TotalOptimizationEvents,
                    analytics.PerformanceAnalytics.TotalRequests,
                    analytics.PerformanceAnalytics.SuccessRate
                },
                
                TokenAnalytics = new
                {
                    analytics.TokenAnalytics.TotalTokens,
                    analytics.TokenAnalytics.AverageTokensPerRequest,
                    analytics.TokenAnalytics.PeakTokensPerRequest,
                    TopModels = analytics.TokenAnalytics.TokensByModel
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(3)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    DailyTrends = analytics.TokenAnalytics.DailyTrends.Take(7)
                },
                
                CostAnalytics = new
                {
                    analytics.CostAnalytics.TotalCost,
                    analytics.CostAnalytics.AverageCostPerRequest,
                    analytics.CostAnalytics.PeakDailyCost,
                    ProjectedMonthlyCost = analytics.CostAnalytics.TotalCost * (30.0m / days),
                    DailyTrends = analytics.CostAnalytics.DailyTrends.Take(7)
                },
                
                OptimizationAnalytics = new
                {
                    analytics.OptimizationAnalytics.TotalOptimizationEvents,
                    analytics.OptimizationAnalytics.TotalCostSavings,
                    analytics.OptimizationAnalytics.CacheHitRate,
                    CacheEfficiency = new
                    {
                        analytics.OptimizationAnalytics.CacheHits,
                        analytics.OptimizationAnalytics.CacheMisses,
                        HitRate = analytics.OptimizationAnalytics.CacheHitRate
                    }
                },
                
                PerformanceAnalytics = new
                {
                    analytics.PerformanceAnalytics.AverageResponseTime.TotalSeconds,
                    analytics.PerformanceAnalytics.TotalRequests,
                    analytics.PerformanceAnalytics.SuccessRate,
                    Throughput = analytics.PerformanceAnalytics.TotalRequests > 0 
                        ? analytics.PerformanceAnalytics.TotalRequests / (double)days : 0
                },
                
                KeyInsights = analytics.KeyInsights,
                Recommendations = analytics.Recommendations,
                
                ComplianceInfo = new
                {
                    DataRetention = "Logs retained for analysis and optimization purposes",
                    Privacy = "No personal data is logged, only technical metrics",
                    Monitoring = "Automated cost monitoring and optimization active"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating usage analytics");
            return StatusCode(500, new { error = "Unable to generate usage analytics" });
        }
    }
    }
}


    #region Helper Methods

    private IActionResult? ValidateGenerateRequest(IFormFile csvFile, string outputFormat, string template)
    {
        if (csvFile == null || csvFile.Length == 0)
        {
            return BadRequest(new { error = "Please upload a valid CSV file.", helpUrl = "/api/sprintreport/csv-format" });
        }

        if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "File must be a CSV file." });
        }

        if (csvFile.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { error = "File size must be less than 10MB." });
        }

        var validFormats = new[] { "powerpoint", "html" };
        if (!validFormats.Contains(outputFormat.ToLower()))
        {
            return BadRequest(new { 
                error = "Invalid output format. Supported formats: " + string.Join(", ", validFormats),
                supportedFormats = validFormats 
            });
        }

        var validTemplates = new[] { "professional", "modern", "corporate", "minimal" };
        if (!validTemplates.Contains(template.ToLower()))
        {
            return BadRequest(new { 
                error = "Invalid template. Available templates: " + string.Join(", ", validTemplates),
                availableTemplates = validTemplates,
                templatesUrl = "/api/sprintreport/templates"
            });
        }

        return null;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Sprint_Report" : sanitized;
    }

    private static string CalculateOptimizationScore(TokenUsageSummary usage)
    {
        if (usage.TotalRequests == 0) return "No data";

        var avgTokensPerRequest = usage.TotalTokens / usage.TotalRequests;
        var cacheHitRate = usage.TotalRequests > 0 ? (double)usage.CacheHits / usage.TotalRequests : 0;

        var score = 100;
        if (avgTokensPerRequest > 2000) score -= 20;
        if (avgTokensPerRequest > 3000) score -= 20;
        if (cacheHitRate < 0.3) score -= 15;
        if (cacheHitRate < 0.2) score -= 15;

        return score switch
        {
            >= 90 => "Excellent",
            >= 75 => "Good", 
            >= 60 => "Average",
            >= 40 => "Poor",
            _ => "Needs Improvement"
        };
    }

    #endregion
}