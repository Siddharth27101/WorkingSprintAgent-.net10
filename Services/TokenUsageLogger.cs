using System.Text.Json;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Comprehensive token usage logger with structured logging and analytics
/// </summary>
public class TokenUsageLogger : ITokenUsageLogger
{
    private readonly ILogger<TokenUsageLogger> _logger;
    private readonly List<StructuredLogEntry> _logEntries;
    private readonly object _lockObject = new();

    public TokenUsageLogger(ILogger<TokenUsageLogger> logger)
    {
        _logger = logger;
        _logEntries = new List<StructuredLogEntry>();
    }

    public Task LogTokenUsageAsync(TokenUsageStats usage, TokenUsageContext? context = null)
    {
        var requestId = context?.RequestId ?? Guid.NewGuid().ToString();
        
        // Structured logging with detailed properties
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId,
            ["TokenUsage"] = new
            {
                usage.TotalTokens,
                usage.InputTokens,
                usage.OutputTokens,
                usage.EstimatedCost,
                usage.Model,
                usage.CacheHit
            },
            ["Context"] = context != null ? new
            {
                context.SprintName,
                context.TaskCount,
                context.DataSource,
                context.UserId
            } : null
        });

        _logger.LogInformation("Token usage recorded: {Model} - {TotalTokens} tokens (Input: {InputTokens}, Output: {OutputTokens}), " +
                              "Cost: ${Cost:F6}, Cache Hit: {CacheHit}, Response Time: {ResponseTime}ms, Sprint: {SprintName}",
            usage.Model, usage.TotalTokens, usage.InputTokens, usage.OutputTokens, 
            usage.EstimatedCost, usage.CacheHit, usage.ResponseTime.TotalMilliseconds,
            context?.SprintName ?? "N/A");

        // Store structured entry
        var logEntry = new StructuredLogEntry
        {
            Timestamp = usage.Timestamp,
            LogType = LogType.TokenUsage,
            Level = "Information",
            Message = $"Token usage: {usage.TotalTokens} tokens, ${usage.EstimatedCost:F6}",
            RequestId = requestId,
            Category = "AI.TokenUsage",
            Properties = new Dictionary<string, object>
            {
                ["TokenStats"] = usage,
                ["Context"] = context ?? new TokenUsageContext(),
                ["CostPerToken"] = usage.TotalTokens > 0 ? usage.EstimatedCost / usage.TotalTokens : 0,
                ["EfficiencyScore"] = CalculateEfficiencyScore(usage)
            }
        };

        lock (_lockObject)
        {
            _logEntries.Add(logEntry);
            // Keep only last 10000 entries to prevent memory issues
            if (_logEntries.Count > 10000)
            {
                _logEntries.RemoveRange(0, _logEntries.Count - 10000);
            }
        }

        return Task.CompletedTask;
    }

    public Task LogCostAlertAsync(CostAlert alert, string? context = null)
    {
        var logLevel = alert.Severity switch
        {
            CostAlertSeverity.Critical => LogLevel.Critical,
            CostAlertSeverity.Warning => LogLevel.Warning,
            CostAlertSeverity.Emergency => LogLevel.Critical,
            _ => LogLevel.Information
        };

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["AlertId"] = alert.Id,
            ["AlertType"] = alert.Type.ToString(),
            ["Severity"] = alert.Severity.ToString(),
            ["ThresholdValue"] = alert.ThresholdValue,
            ["ActualValue"] = alert.ActualValue
        });

        _logger.Log(logLevel, "Cost alert triggered: {AlertType} - {Title}. Threshold: {Threshold}, Actual: {Actual}, " +
                             "Recommended Action: {Action}",
            alert.Type, alert.Title, alert.ThresholdValue, alert.ActualValue, alert.RecommendedAction);

        var logEntry = new StructuredLogEntry
        {
            Timestamp = alert.Timestamp,
            LogType = LogType.CostAlert,
            Level = alert.Severity.ToString(),
            Message = $"Cost alert: {alert.Title}",
            RequestId = alert.Id,
            Category = "AI.CostMonitoring",
            Properties = new Dictionary<string, object>
            {
                ["Alert"] = alert,
                ["Context"] = context ?? string.Empty,
                ["Severity"] = alert.Severity.ToString(),
                ["ExceedsThresholdBy"] = alert.ActualValue - alert.ThresholdValue
            }
        };

        lock (_lockObject)
        {
            _logEntries.Add(logEntry);
        }

        return Task.CompletedTask;
    }

    public Task LogOptimizationEventAsync(OptimizationEvent optimizationEvent)
    {
        var tokensSaved = optimizationEvent.Metrics.TokensBefore - optimizationEvent.Metrics.TokensAfter;
        var costSaved = optimizationEvent.Metrics.CostBefore - optimizationEvent.Metrics.CostAfter;

        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OptimizationId"] = optimizationEvent.EventId,
            ["Strategy"] = optimizationEvent.Strategy.ToString(),
            ["EventType"] = optimizationEvent.EventType.ToString(),
            ["TokensSaved"] = tokensSaved,
            ["CostSaved"] = costSaved
        });

        _logger.LogInformation("Optimization event: {EventType} using {Strategy} - Tokens saved: {TokensSaved}, " +
                              "Cost saved: ${CostSaved:F6}, Compression ratio: {CompressionRatio:P2}, " +
                              "Processing time: {ProcessingTime}ms, Success: {Success}",
            optimizationEvent.EventType, optimizationEvent.Strategy, tokensSaved, costSaved,
            optimizationEvent.Metrics.CompressionRatio, optimizationEvent.Duration.TotalMilliseconds,
            optimizationEvent.Success);

        if (!optimizationEvent.Success && !string.IsNullOrEmpty(optimizationEvent.ErrorMessage))
        {
            _logger.LogWarning("Optimization event failed: {EventType} - {ErrorMessage}",
                optimizationEvent.EventType, optimizationEvent.ErrorMessage);
        }

        var logEntry = new StructuredLogEntry
        {
            Timestamp = optimizationEvent.Timestamp,
            LogType = LogType.OptimizationEvent,
            Level = optimizationEvent.Success ? "Information" : "Warning",
            Message = $"Optimization: {optimizationEvent.EventType} - {(optimizationEvent.Success ? "Success" : "Failed")}",
            RequestId = optimizationEvent.RequestId,
            Category = "AI.Optimization",
            Properties = new Dictionary<string, object>
            {
                ["OptimizationEvent"] = optimizationEvent,
                ["TokensSaved"] = tokensSaved,
                ["CostSaved"] = costSaved,
                ["EfficiencyGain"] = optimizationEvent.Metrics.TokensBefore > 0 
                    ? (double)tokensSaved / optimizationEvent.Metrics.TokensBefore : 0
            }
        };

        lock (_lockObject)
        {
            _logEntries.Add(logEntry);
        }

        return Task.CompletedTask;
    }

    public Task LogPerformanceMetricsAsync(PerformanceMetrics metrics)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["PerformanceSnapshot"] = DateTime.UtcNow,
            ["AverageResponseTime"] = metrics.AverageResponseTime.TotalMilliseconds,
            ["RequestsPerHour"] = metrics.RequestsPerHour,
            ["SuccessRate"] = metrics.SuccessRate
        });

        _logger.LogInformation("Performance metrics: Avg response time: {AvgResponseTime}ms, " +
                              "Requests/hour: {RequestsPerHour}, Success rate: {SuccessRate:P2}, " +
                              "Cost efficiency: ${CostEfficiency:F4}, Optimization score: {OptimizationScore}",
            metrics.AverageResponseTime.TotalMilliseconds, metrics.RequestsPerHour,
            metrics.SuccessRate, metrics.CostEfficiency, metrics.OptimizationScore);

        var logEntry = new StructuredLogEntry
        {
            Timestamp = DateTime.UtcNow,
            LogType = LogType.PerformanceMetrics,
            Level = "Information",
            Message = $"Performance snapshot: {metrics.OptimizationScore}",
            RequestId = Guid.NewGuid().ToString(),
            Category = "AI.Performance",
            Properties = new Dictionary<string, object>
            {
                ["PerformanceMetrics"] = metrics,
                ["Benchmark"] = GetPerformanceBenchmark(metrics)
            }
        };

        lock (_lockObject)
        {
            _logEntries.Add(logEntry);
        }

        return Task.CompletedTask;
    }

    public Task<List<StructuredLogEntry>> GetStructuredLogsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        LogType[]? logTypes = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-7);
        var to = toDate ?? DateTime.UtcNow;
        var types = logTypes ?? Enum.GetValues<LogType>();

        List<StructuredLogEntry> filteredLogs;
        lock (_lockObject)
        {
            filteredLogs = _logEntries
                .Where(entry => entry.Timestamp >= from && 
                               entry.Timestamp <= to && 
                               types.Contains(entry.LogType))
                .OrderBy(entry => entry.Timestamp)
                .ToList();
        }

        return Task.FromResult(filteredLogs);
    }

    public async Task<UsageAnalyticsSummary> GenerateAnalyticsSummaryAsync(TimeRange timeRange)
    {
        var logs = await GetStructuredLogsAsync(timeRange.StartDate, timeRange.EndDate);
        
        var tokenLogs = logs.Where(l => l.LogType == LogType.TokenUsage).ToList();
        var optimizationLogs = logs.Where(l => l.LogType == LogType.OptimizationEvent).ToList();
        var performanceLogs = logs.Where(l => l.LogType == LogType.PerformanceMetrics).ToList();
        var alertLogs = logs.Where(l => l.LogType == LogType.CostAlert).ToList();

        var summary = new UsageAnalyticsSummary
        {
            AnalyzedPeriod = timeRange,
            TokenAnalytics = GenerateTokenAnalytics(tokenLogs),
            CostAnalytics = GenerateCostAnalytics(tokenLogs),
            OptimizationAnalytics = GenerateOptimizationAnalytics(optimizationLogs),
            PerformanceAnalytics = GeneratePerformanceAnalytics(performanceLogs),
            KeyInsights = GenerateKeyInsights(logs),
            Recommendations = GenerateRecommendations(logs, alertLogs.Count)
        };

        _logger.LogInformation("Generated usage analytics summary for period {StartDate} to {EndDate}: " +
                              "{TotalTokens} tokens, ${TotalCost:F4}, {OptimizationEvents} optimization events",
            timeRange.StartDate, timeRange.EndDate, summary.TokenAnalytics.TotalTokens,
            summary.CostAnalytics.TotalCost, summary.OptimizationAnalytics.TotalOptimizationEvents);

        return summary;
    }

    #region Private Helper Methods

    private static string CalculateEfficiencyScore(TokenUsageStats usage)
    {
        var costPerToken = usage.TotalTokens > 0 ? (double)(usage.EstimatedCost / usage.TotalTokens) : 0;
        var responseTimeMs = usage.ResponseTime.TotalMilliseconds;
        
        // Efficiency score based on cost per token and response time
        if (costPerToken < 0.00001 && responseTimeMs < 3000) return "Excellent";
        if (costPerToken < 0.00002 && responseTimeMs < 5000) return "Good";
        if (costPerToken < 0.00005 && responseTimeMs < 10000) return "Average";
        if (costPerToken < 0.0001) return "Below Average";
        return "Poor";
    }

    private static string GetPerformanceBenchmark(PerformanceMetrics metrics)
    {
        var score = 0;
        if (metrics.AverageResponseTime.TotalSeconds < 3) score += 25;
        else if (metrics.AverageResponseTime.TotalSeconds < 5) score += 15;
        else if (metrics.AverageResponseTime.TotalSeconds < 10) score += 5;

        if (metrics.SuccessRate > 0.98) score += 25;
        else if (metrics.SuccessRate > 0.95) score += 20;
        else if (metrics.SuccessRate > 0.90) score += 15;
        else if (metrics.SuccessRate > 0.80) score += 10;

        if (metrics.CostEfficiency < 0.01m) score += 25;
        else if (metrics.CostEfficiency < 0.05m) score += 20;
        else if (metrics.CostEfficiency < 0.10m) score += 15;

        if (metrics.OptimizationScore == "Excellent") score += 25;
        else if (metrics.OptimizationScore == "Good") score += 20;
        else if (metrics.OptimizationScore == "Average") score += 15;

        return score switch
        {
            >= 90 => "Excellent",
            >= 70 => "Good",
            >= 50 => "Average",
            >= 30 => "Below Average",
            _ => "Needs Improvement"
        };
    }

    private static TokenUsageAnalytics GenerateTokenAnalytics(List<StructuredLogEntry> tokenLogs)
    {
        if (!tokenLogs.Any())
            return new TokenUsageAnalytics();

        var tokenStats = tokenLogs
            .Select(l => JsonSerializer.Deserialize<TokenUsageStats>(
                JsonSerializer.Serialize(((JsonElement)l.Properties["TokenStats"]).GetRawText())))
            .Where(stats => stats != null)
            .Cast<TokenUsageStats>()
            .ToList();

        return new TokenUsageAnalytics
        {
            TotalTokens = tokenStats.Sum(s => s.TotalTokens),
            AverageTokensPerRequest = tokenStats.Any() ? (int)tokenStats.Average(s => s.TotalTokens) : 0,
            PeakTokensPerRequest = tokenStats.Any() ? tokenStats.Max(s => s.TotalTokens) : 0,
            TokensByModel = tokenStats.GroupBy(s => s.Model).ToDictionary(g => g.Key, g => g.Sum(s => s.TotalTokens)),
            TokensByRequestType = tokenStats.GroupBy(s => s.RequestType).ToDictionary(g => g.Key, g => g.Sum(s => s.TotalTokens)),
            DailyTrends = tokenStats
                .GroupBy(s => s.Timestamp.Date)
                .Select(g => new TokenUsageTrend
                {
                    Date = g.Key,
                    TotalTokens = g.Sum(s => s.TotalTokens),
                    RequestCount = g.Count(),
                    AverageTokensPerRequest = g.Any() ? (int)g.Average(s => s.TotalTokens) : 0
                })
                .OrderBy(t => t.Date)
                .ToList()
        };
    }

    private static CostAnalytics GenerateCostAnalytics(List<StructuredLogEntry> tokenLogs)
    {
        if (!tokenLogs.Any())
            return new CostAnalytics();

        var tokenStats = tokenLogs
            .Select(l => JsonSerializer.Deserialize<TokenUsageStats>(
                JsonSerializer.Serialize(((JsonElement)l.Properties["TokenStats"]).GetRawText())))
            .Where(stats => stats != null)
            .Cast<TokenUsageStats>()
            .ToList();

        return new CostAnalytics
        {
            TotalCost = tokenStats.Sum(s => s.EstimatedCost),
            AverageCostPerRequest = tokenStats.Any() ? tokenStats.Average(s => s.EstimatedCost) : 0,
            PeakDailyCost = tokenStats
                .GroupBy(s => s.Timestamp.Date)
                .Select(g => g.Sum(s => s.EstimatedCost))
                .DefaultIfEmpty(0)
                .Max(),
            CostsByModel = tokenStats.GroupBy(s => s.Model).ToDictionary(g => g.Key, g => g.Sum(s => s.EstimatedCost)),
            DailyTrends = tokenStats
                .GroupBy(s => s.Timestamp.Date)
                .Select(g => new CostTrend
                {
                    Time = g.Key,
                    Cost = g.Sum(s => s.EstimatedCost),
                    Requests = g.Count(),
                    Tokens = g.Sum(s => s.TotalTokens)
                })
                .OrderBy(t => t.Time)
                .ToList()
        };
    }

    private static OptimizationAnalytics GenerateOptimizationAnalytics(List<StructuredLogEntry> optimizationLogs)
    {
        if (!optimizationLogs.Any())
            return new OptimizationAnalytics();

        var cacheHits = optimizationLogs.Count(l => l.Message.Contains("Cache Hit"));
        var cacheMisses = optimizationLogs.Count(l => l.Message.Contains("Cache Miss"));

        return new OptimizationAnalytics
        {
            TotalOptimizationEvents = optimizationLogs.Count,
            TotalCostSavings = optimizationLogs
                .Where(l => l.Properties.ContainsKey("CostSaved"))
                .Sum(l => Convert.ToDecimal(l.Properties["CostSaved"])),
            CacheHits = cacheHits,
            CacheMisses = cacheMisses,
            // Additional analytics would be populated from actual optimization event data
            AverageCompressionRatio = 0.75 // Placeholder - would be calculated from actual events
        };
    }

    private static PerformanceAnalytics GeneratePerformanceAnalytics(List<StructuredLogEntry> performanceLogs)
    {
        if (!performanceLogs.Any())
            return new PerformanceAnalytics { TotalRequests = 0, SuccessfulRequests = 0 };

        return new PerformanceAnalytics
        {
            TotalRequests = performanceLogs.Count,
            SuccessfulRequests = performanceLogs.Count, // Simplified - would track actual success/failure
            FailedRequests = 0,
            AverageResponseTime = TimeSpan.FromMilliseconds(2500), // Placeholder
            PeakResponseTime = TimeSpan.FromMilliseconds(8000) // Placeholder
        };
    }

    private static List<string> GenerateKeyInsights(List<StructuredLogEntry> allLogs)
    {
        var insights = new List<string>();

        var tokenLogs = allLogs.Where(l => l.LogType == LogType.TokenUsage).ToList();
        var alertLogs = allLogs.Where(l => l.LogType == LogType.CostAlert).ToList();

        if (tokenLogs.Any())
        {
            var avgTokens = tokenLogs.Average(l => 
            {
                if (l.Properties.TryGetValue("TokenStats", out var tokenStatsObj))
                {
                    var tokenStats = JsonSerializer.Deserialize<TokenUsageStats>(
                        JsonSerializer.Serialize(tokenStatsObj));
                    return tokenStats?.TotalTokens ?? 0;
                }
                return 0;
            });

            insights.Add($"Average tokens per request: {avgTokens:F0}");
        }

        if (alertLogs.Any())
        {
            insights.Add($"Generated {alertLogs.Count} cost alerts during analysis period");
        }

        var optimizationLogs = allLogs.Where(l => l.LogType == LogType.OptimizationEvent).ToList();
        if (optimizationLogs.Any())
        {
            insights.Add($"Executed {optimizationLogs.Count} optimization events");
        }

        return insights;
    }

    private static List<string> GenerateRecommendations(List<StructuredLogEntry> allLogs, int alertCount)
    {
        var recommendations = new List<string>();

        if (alertCount > 5)
        {
            recommendations.Add("High number of cost alerts detected - consider implementing more aggressive optimization");
        }

        var tokenLogs = allLogs.Where(l => l.LogType == LogType.TokenUsage).ToList();
        if (tokenLogs.Count > 100)
        {
            recommendations.Add("High API usage volume - consider implementing request batching");
        }

        recommendations.Add("Enable comprehensive caching to reduce redundant AI API calls");
        recommendations.Add("Monitor daily cost trends and set up automated alerts");

        return recommendations;
    }

    #endregion
}