using System.Diagnostics;
using System.Text;
using WorkingSprintAgent.Services;

namespace WorkingSprintAgent.Middleware;

/// <summary>
/// Middleware for automatic token usage logging and request monitoring
/// </summary>
public class TokenUsageLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenUsageLoggingMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;

    public TokenUsageLoggingMiddleware(
        RequestDelegate next, 
        ILogger<TokenUsageLoggingMiddleware> logger,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        // Add request ID to context for correlation
        context.Items["RequestId"] = requestId;

        // Log request start
        await LogRequestStartAsync(context, requestId);

        try
        {
            // Continue to next middleware
            await _next(context);
            
            stopwatch.Stop();
            
            // Log successful request completion
            await LogRequestCompletionAsync(context, requestId, stopwatch.Elapsed, isSuccess: true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            // Log failed request
            await LogRequestCompletionAsync(context, requestId, stopwatch.Elapsed, isSuccess: false, exception: ex);
            
            throw;
        }
    }

    private async Task LogRequestStartAsync(HttpContext context, string requestId)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["Method"] = context.Request.Method,
            ["Path"] = context.Request.Path,
            ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
            ["RemoteIpAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
        });

        var isApiRequest = context.Request.Path.StartsWithSegments("/api");
        var isSprintReportRequest = context.Request.Path.StartsWithSegments("/api/sprintreport");

        if (isApiRequest)
        {
            _logger.LogInformation("API request started: {Method} {Path} - Request ID: {RequestId}", 
                context.Request.Method, context.Request.Path, requestId);

            if (isSprintReportRequest)
            {
                // Log additional context for sprint report requests
                var queryParams = context.Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());
                
                _logger.LogInformation("Sprint report request details - Endpoint: {Path}, " +
                                     "Query params: {QueryParams}, Content length: {ContentLength}",
                    context.Request.Path, queryParams, context.Request.ContentLength ?? 0);
            }
        }

        await Task.CompletedTask;
    }

    private async Task LogRequestCompletionAsync(
        HttpContext context, 
        string requestId, 
        TimeSpan duration,
        bool isSuccess,
        Exception? exception = null)
    {
        var statusCode = context.Response.StatusCode;
        var isApiRequest = context.Request.Path.StartsWithSegments("/api");
        var isSprintReportRequest = context.Request.Path.StartsWithSegments("/api/sprintreport");

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["StatusCode"] = statusCode,
            ["Duration"] = duration.TotalMilliseconds,
            ["Success"] = isSuccess
        });

        if (isApiRequest)
        {
            var logLevel = isSuccess && statusCode < 400 ? LogLevel.Information : LogLevel.Warning;
            
            _logger.Log(logLevel, "API request completed: {Method} {Path} - Status: {StatusCode}, " +
                                 "Duration: {Duration}ms, Success: {Success}, Request ID: {RequestId}",
                context.Request.Method, context.Request.Path, statusCode, 
                duration.TotalMilliseconds, isSuccess, requestId);

            if (isSprintReportRequest && isSuccess)
            {
                await LogSprintReportRequestMetricsAsync(context, requestId, duration);
            }

            if (!isSuccess && exception != null)
            {
                _logger.LogError(exception, "API request failed: {Method} {Path} - Request ID: {RequestId}, " +
                                          "Error: {ErrorMessage}",
                    context.Request.Method, context.Request.Path, requestId, exception.Message);
            }
        }
    }

    private async Task LogSprintReportRequestMetricsAsync(HttpContext context, string requestId, TimeSpan duration)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var tokenLogger = scope.ServiceProvider.GetService<ITokenUsageLogger>();
            
            if (tokenLogger != null)
            {
                // Log performance metrics for sprint report requests
                var performanceMetrics = new Models.PerformanceMetrics
                {
                    AverageResponseTime = duration,
                    RequestsPerHour = 1, // Would be calculated based on actual throughput
                    CostEfficiency = 0.01m, // Would be calculated from actual costs
                    SuccessRate = context.Response.StatusCode < 400 ? 1.0 : 0.0,
                    OptimizationScore = "Good" // Would be calculated from actual optimization data
                };

                await tokenLogger.LogPerformanceMetricsAsync(performanceMetrics);
            }

            // Log specific metrics for different sprint report endpoints
            var endpoint = GetEndpointName(context.Request.Path);
            _logger.LogInformation("Sprint report endpoint metrics - Endpoint: {Endpoint}, " +
                                 "Response time: {ResponseTime}ms, Content length: {ContentLength}, " +
                                 "Request ID: {RequestId}",
                endpoint, duration.TotalMilliseconds, context.Response.ContentLength ?? 0, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log sprint report request metrics for Request ID: {RequestId}", requestId);
        }
    }

    private static string GetEndpointName(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
        
        if (segments.Length >= 3 && segments[0] == "api" && segments[1] == "sprintreport")
        {
            return segments[2] switch
            {
                "generate" => "Generate Report",
                "preview" => "Preview Data",
                "csv-format" => "CSV Format Info",
                "health" => "Health Check",
                "ai-status" => "AI Status",
                "token-usage" => "Token Usage",
                "cost-dashboard" => "Cost Dashboard",
                "optimization-recommendations" => "Optimization Recommendations",
                "templates" => "Templates",
                "export-cost-report" => "Export Cost Report",
                _ => "Unknown Endpoint"
            };
        }

        return "Non-Sprint-Report API";
    }
}

/// <summary>
/// Extension methods for registering the token usage logging middleware
/// </summary>
public static class TokenUsageLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseTokenUsageLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TokenUsageLoggingMiddleware>();
    }
}