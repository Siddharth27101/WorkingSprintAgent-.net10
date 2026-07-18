namespace WorkingSprintAgent.Models;

/// <summary>
/// Configuration settings for OpenAI integration
/// </summary>
public class OpenAIConfiguration
{
    public const string ConfigSection = "OpenAI";

    /// <summary>
    /// OpenAI API Key - should be set via environment variable or user secrets
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Organization ID (optional)
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Model to use for insights generation (default: gpt-4o-mini for cost optimization)
    /// </summary>
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Maximum tokens per request (helps control costs)
    /// </summary>
    public int MaxTokens { get; set; } = 1500;

    /// <summary>
    /// Temperature for creativity vs determinism (0.0-2.0)
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable response caching to reduce duplicate API calls
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration in minutes
    /// </summary>
    public int CacheExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Enable token usage tracking for cost monitoring
    /// </summary>
    public bool EnableTokenTracking { get; set; } = true;

    /// <summary>
    /// Maximum daily token budget (for cost control)
    /// </summary>
    public int MaxDailyTokens { get; set; } = 50000;

    /// <summary>
    /// Cost per 1K tokens (input) - updated based on current OpenAI pricing
    /// </summary>
    public decimal CostPer1KInputTokens { get; set; } = 0.000150m; // GPT-4o-mini pricing

    /// <summary>
    /// Cost per 1K tokens (output) - updated based on current OpenAI pricing
    /// </summary>
    public decimal CostPer1KOutputTokens { get; set; } = 0.000600m; // GPT-4o-mini pricing
}

/// <summary>
/// Token usage statistics for cost monitoring
/// </summary>
public class TokenUsageStats
{
    public DateTime Timestamp { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCost { get; set; }
    public string Model { get; set; } = string.Empty;
    public TimeSpan ResponseTime { get; set; }
    public bool CacheHit { get; set; }
}

/// <summary>
/// AI-generated insights with metadata
/// </summary>
public class AIInsightsResponse
{
    public SprintInsights Insights { get; set; } = new();
    public TokenUsageStats TokenUsage { get; set; } = new();
    public List<string> OptimizationSuggestions { get; set; } = new();
    public bool FromCache { get; set; }
}