using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Specialized logging service for token usage and AI operations
/// </summary>
public interface ITokenUsageLogger
{
    /// <summary>
    /// Log token usage with detailed context
    /// </summary>
    /// <param name="usage">Token usage statistics</param>
    /// <param name="context">Additional context information</param>
    Task LogTokenUsageAsync(TokenUsageStats usage, TokenUsageContext? context = null);

    /// <summary>
    /// Log cost threshold violations
    /// </summary>
    /// <param name="alert">Cost alert information</param>
    /// <param name="context">Additional context</param>
    Task LogCostAlertAsync(CostAlert alert, string? context = null);

    /// <summary>
    /// Log optimization events
    /// </summary>
    /// <param name="optimizationEvent">Optimization event details</param>
    Task LogOptimizationEventAsync(OptimizationEvent optimizationEvent);

    /// <summary>
    /// Log performance metrics
    /// </summary>
    /// <param name="metrics">Performance metrics</param>
    Task LogPerformanceMetricsAsync(PerformanceMetrics metrics);

    /// <summary>
    /// Get structured logs for analysis
    /// </summary>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <param name="logTypes">Types of logs to retrieve</param>
    /// <returns>Structured log entries</returns>
    Task<List<StructuredLogEntry>> GetStructuredLogsAsync(
        DateTime? fromDate = null, 
        DateTime? toDate = null, 
        LogType[]? logTypes = null);

    /// <summary>
    /// Generate usage analytics summary
    /// </summary>
    /// <param name="timeRange">Time range for analysis</param>
    /// <returns>Analytics summary</returns>
    Task<UsageAnalyticsSummary> GenerateAnalyticsSummaryAsync(TimeRange timeRange);
}

/// <summary>
/// Token usage context for enhanced logging
/// </summary>
public class TokenUsageContext
{
    public string RequestId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SprintName { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public string DataSource { get; set; } = string.Empty;
    public Dictionary<string, object> AdditionalProperties { get; set; } = new();
}

/// <summary>
/// Optimization event for logging optimization activities
/// </summary>
public class OptimizationEvent
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public OptimizationEventType EventType { get; set; }
    public string Description { get; set; } = string.Empty;
    public OptimizationStrategy Strategy { get; set; }
    public OptimizationMetrics Metrics { get; set; } = new();
    public string RequestId { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Optimization event types
/// </summary>
public enum OptimizationEventType
{
    DataCompression,
    PromptOptimization,
    CacheHit,
    CacheMiss,
    ModelSelection,
    BatchProcessing,
    TokenReduction,
    CostThresholdCheck,
    FallbackActivation
}

/// <summary>
/// Optimization metrics for events
/// </summary>
public class OptimizationMetrics
{
    public int TokensBefore { get; set; }
    public int TokensAfter { get; set; }
    public decimal CostBefore { get; set; }
    public decimal CostAfter { get; set; }
    public double CompressionRatio { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}

/// <summary>
/// Structured log entry for analysis
/// </summary>
public class StructuredLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; }
    public LogType LogType { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public string? Exception { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Log types for filtering
/// </summary>
public enum LogType
{
    TokenUsage,
    CostAlert,
    OptimizationEvent,
    PerformanceMetrics,
    Error,
    Warning,
    Information
}

/// <summary>
/// Usage analytics summary
/// </summary>
public class UsageAnalyticsSummary
{
    public TimeRange AnalyzedPeriod { get; set; } = new();
    public TokenUsageAnalytics TokenAnalytics { get; set; } = new();
    public CostAnalytics CostAnalytics { get; set; } = new();
    public OptimizationAnalytics OptimizationAnalytics { get; set; } = new();
    public PerformanceAnalytics PerformanceAnalytics { get; set; } = new();
    public List<string> KeyInsights { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Time range for analysis
/// </summary>
public class TimeRange
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DurationDays => (EndDate - StartDate).Days;
}

/// <summary>
/// Token usage analytics
/// </summary>
public class TokenUsageAnalytics
{
    public int TotalTokens { get; set; }
    public int AverageTokensPerRequest { get; set; }
    public int PeakTokensPerRequest { get; set; }
    public Dictionary<string, int> TokensByModel { get; set; } = new();
    public Dictionary<string, int> TokensByRequestType { get; set; } = new();
    public List<TokenUsageTrend> DailyTrends { get; set; } = new();
}

/// <summary>
/// Cost analytics
/// </summary>
public class CostAnalytics
{
    public decimal TotalCost { get; set; }
    public decimal AverageCostPerRequest { get; set; }
    public decimal PeakDailyCost { get; set; }
    public decimal CostSavingsFromOptimization { get; set; }
    public Dictionary<string, decimal> CostsByModel { get; set; } = new();
    public List<CostTrend> DailyTrends { get; set; } = new();
}

/// <summary>
/// Optimization analytics
/// </summary>
public class OptimizationAnalytics
{
    public int TotalOptimizationEvents { get; set; }
    public decimal TotalCostSavings { get; set; }
    public Dictionary<OptimizationStrategy, int> EventsByStrategy { get; set; } = new();
    public Dictionary<OptimizationStrategy, decimal> SavingsByStrategy { get; set; } = new();
    public double AverageCompressionRatio { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double CacheHitRate => CacheHits + CacheMisses > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0;
}

/// <summary>
/// Performance analytics
/// </summary>
public class PerformanceAnalytics
{
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan PeakResponseTime { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
    public Dictionary<string, TimeSpan> ResponseTimesByEndpoint { get; set; } = new();
}

/// <summary>
/// Token usage trend data point
/// </summary>
public class TokenUsageTrend
{
    public DateTime Date { get; set; }
    public int TotalTokens { get; set; }
    public int RequestCount { get; set; }
    public int AverageTokensPerRequest { get; set; }
}