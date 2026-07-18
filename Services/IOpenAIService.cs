using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Service interface for OpenAI integration with token cost optimization
/// </summary>
public interface IOpenAIService
{
    /// <summary>
    /// Generate AI-powered sprint insights with cost optimization
    /// </summary>
    /// <param name="metrics">Sprint metrics data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-generated insights with token usage statistics</returns>
    Task<AIInsightsResponse> GenerateInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current token usage statistics for cost monitoring
    /// </summary>
    /// <param name="fromDate">Start date for statistics</param>
    /// <param name="toDate">End date for statistics</param>
    /// <returns>Token usage summary</returns>
    Task<TokenUsageSummary> GetTokenUsageAsync(DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Estimate token cost before making API call
    /// </summary>
    /// <param name="inputText">Text to be sent to OpenAI</param>
    /// <returns>Estimated token count and cost</returns>
    TokenCostEstimate EstimateTokenCost(string inputText);

    /// <summary>
    /// Check if daily token budget is exceeded
    /// </summary>
    /// <returns>True if budget exceeded</returns>
    Task<bool> IsDailyBudgetExceededAsync();

    /// <summary>
    /// Get optimization recommendations for token usage
    /// </summary>
    /// <returns>List of cost optimization suggestions</returns>
    List<string> GetOptimizationRecommendations();
}

/// <summary>
/// Token usage summary for cost monitoring
/// </summary>
public class TokenUsageSummary
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TotalRequests { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AverageCostPerRequest { get; set; }
    public int CacheHits { get; set; }
    public decimal CacheSavings { get; set; }
    public List<TokenUsageStats> DailyBreakdown { get; set; } = new();
}

/// <summary>
/// Token cost estimation
/// </summary>
public class TokenCostEstimate
{
    public int EstimatedInputTokens { get; set; }
    public int EstimatedOutputTokens { get; set; }
    public int EstimatedTotalTokens { get; set; }
    public decimal EstimatedInputCost { get; set; }
    public decimal EstimatedOutputCost { get; set; }
    public decimal EstimatedTotalCost { get; set; }
    public List<string> CostOptimizationTips { get; set; } = new();
}