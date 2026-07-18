using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Service for monitoring and analyzing AI costs and usage patterns
/// </summary>
public interface ICostMonitoringService
{
    /// <summary>
    /// Record a token usage event
    /// </summary>
    /// <param name="usage">Token usage statistics</param>
    Task RecordUsageAsync(TokenUsageStats usage);

    /// <summary>
    /// Get comprehensive cost analysis for a time period
    /// </summary>
    /// <param name="fromDate">Start date (optional)</param>
    /// <param name="toDate">End date (optional)</param>
    /// <returns>Detailed cost analysis</returns>
    Task<CostAnalysisReport> GetCostAnalysisAsync(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Get real-time cost monitoring dashboard data
    /// </summary>
    /// <returns>Dashboard data</returns>
    Task<CostDashboard> GetDashboardDataAsync();

    /// <summary>
    /// Check if cost thresholds are being exceeded
    /// </summary>
    /// <returns>List of threshold violations</returns>
    Task<List<CostAlert>> CheckCostAlertsAsync();

    /// <summary>
    /// Predict future costs based on usage patterns
    /// </summary>
    /// <param name="days">Number of days to predict</param>
    /// <returns>Cost prediction</returns>
    Task<CostPrediction> PredictCostsAsync(int days = 30);

    /// <summary>
    /// Get optimization opportunities based on usage patterns
    /// </summary>
    /// <returns>Optimization opportunities</returns>
    Task<List<CostOptimizationOpportunity>> GetOptimizationOpportunitiesAsync();

    /// <summary>
    /// Export detailed cost report
    /// </summary>
    /// <param name="format">Export format (CSV, JSON, Excel)</param>
    /// <param name="fromDate">Start date</param>
    /// <param name="toDate">End date</param>
    /// <returns>Exported report data</returns>
    Task<byte[]> ExportCostReportAsync(string format = "CSV", DateTime? fromDate = null, DateTime? toDate = null);
}

/// <summary>
/// Comprehensive cost analysis report
/// </summary>
public class CostAnalysisReport
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalCost { get; set; }
    public int TotalRequests { get; set; }
    public int TotalTokens { get; set; }
    public decimal AverageCostPerRequest { get; set; }
    public decimal AverageCostPerToken { get; set; }
    public Dictionary<string, decimal> CostsByModel { get; set; } = new();
    public Dictionary<string, decimal> CostsByRequestType { get; set; } = new();
    public Dictionary<DateTime, decimal> DailyCosts { get; set; } = new();
    public List<TokenUsageStats> TopExpensiveRequests { get; set; } = new();
    public CacheEfficiencyStats CacheEfficiency { get; set; } = new();
    public List<string> CostTrends { get; set; } = new();
    public decimal ProjectedMonthlyCost { get; set; }
}

/// <summary>
/// Real-time cost monitoring dashboard data
/// </summary>
public class CostDashboard
{
    public DateTime LastUpdated { get; set; }
    public decimal TodayCost { get; set; }
    public decimal WeekCost { get; set; }
    public decimal MonthCost { get; set; }
    public int TodayRequests { get; set; }
    public int TodayTokens { get; set; }
    public decimal BudgetUtilization { get; set; }
    public List<RecentActivity> RecentActivity { get; set; } = new();
    public List<CostTrend> HourlyCosts { get; set; } = new();
    public Dictionary<string, int> RequestsByType { get; set; } = new();
    public PerformanceMetrics Performance { get; set; } = new();
    public List<CostAlert> ActiveAlerts { get; set; } = new();
}

/// <summary>
/// Cache efficiency statistics
/// </summary>
public class CacheEfficiencyStats
{
    public double HitRate { get; set; }
    public int TotalHits { get; set; }
    public int TotalMisses { get; set; }
    public decimal SavingsFromCache { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan AverageCacheResponseTime { get; set; }
}

/// <summary>
/// Recent activity item
/// </summary>
public class RecentActivity
{
    public DateTime Timestamp { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string SprintName { get; set; } = string.Empty;
    public int Tokens { get; set; }
    public decimal Cost { get; set; }
    public bool FromCache { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

/// <summary>
/// Cost trend data point
/// </summary>
public class CostTrend
{
    public DateTime Time { get; set; }
    public decimal Cost { get; set; }
    public int Requests { get; set; }
    public int Tokens { get; set; }
}

/// <summary>
/// Performance metrics
/// </summary>
public class PerformanceMetrics
{
    public TimeSpan AverageResponseTime { get; set; }
    public int RequestsPerHour { get; set; }
    public decimal CostEfficiency { get; set; } // Cost per successful insight
    public double SuccessRate { get; set; }
    public string OptimizationScore { get; set; } = string.Empty;
}

/// <summary>
/// Cost alert
/// </summary>
public class CostAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; }
    public CostAlertType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CostAlertSeverity Severity { get; set; }
    public decimal ThresholdValue { get; set; }
    public decimal ActualValue { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
    public bool IsAcknowledged { get; set; }
}

/// <summary>
/// Cost alert types
/// </summary>
public enum CostAlertType
{
    DailyBudgetExceeded,
    WeeklyBudgetExceeded,
    MonthlyBudgetExceeded,
    UnusualSpike,
    LowCacheHitRate,
    HighTokenUsage,
    ModelCostIncrease,
    RequestVolumeHigh
}

/// <summary>
/// Cost alert severity levels
/// </summary>
public enum CostAlertSeverity
{
    Info,
    Warning,
    Critical,
    Emergency
}

/// <summary>
/// Cost prediction
/// </summary>
public class CostPrediction
{
    public DateTime PredictionDate { get; set; }
    public int DaysForward { get; set; }
    public decimal PredictedCost { get; set; }
    public decimal ConfidenceLevel { get; set; }
    public List<CostForecastPoint> ForecastPoints { get; set; } = new();
    public string Methodology { get; set; } = string.Empty;
    public List<string> AssumptionsAndFactors { get; set; } = new();
    public decimal MinEstimate { get; set; }
    public decimal MaxEstimate { get; set; }
    public List<string> RiskFactors { get; set; } = new();
}

/// <summary>
/// Cost forecast data point
/// </summary>
public class CostForecastPoint
{
    public DateTime Date { get; set; }
    public decimal PredictedCost { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
    public string Confidence { get; set; } = string.Empty;
}

/// <summary>
/// Cost optimization opportunity
/// </summary>
public class CostOptimizationOpportunity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PotentialSavings { get; set; }
    public decimal ImplementationEffort { get; set; } // 1-5 scale
    public int Priority { get; set; } // 1-5, 1 = highest
    public List<string> Benefits { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public List<string> ImplementationSteps { get; set; } = new();
    public string ROIEstimate { get; set; } = string.Empty;
    public OptimizationStrategy Strategy { get; set; }
    public bool IsImplemented { get; set; }
}