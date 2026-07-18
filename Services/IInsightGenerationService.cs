using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Service interface for generating sprint insights with AI capabilities
/// </summary>
public interface IInsightGenerationService
{
    /// <summary>
    /// Generate AI-powered sprint insights with cost optimization
    /// </summary>
    /// <param name="metrics">Sprint metrics data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Sprint insights (enhanced with AI when available)</returns>
    Task<SprintInsights> GenerateInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate enhanced AI insights with metadata and cost information
    /// </summary>
    /// <param name="metrics">Sprint metrics data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AI-enhanced insights with token usage and optimization data</returns>
    Task<AIInsightsResponse> GenerateEnhancedInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if AI-powered insights are available
    /// </summary>
    /// <returns>True if AI service is configured and available</returns>
    bool IsAIEnabled { get; }

    /// <summary>
    /// Get current service status and capabilities
    /// </summary>
    /// <returns>Service status information</returns>
    InsightServiceStatus GetServiceStatus();
}

/// <summary>
/// Service status information
/// </summary>
public class InsightServiceStatus
{
    public bool IsAIEnabled { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public bool IsCachingEnabled { get; set; }
    public bool IsTokenTrackingEnabled { get; set; }
    public int MaxDailyTokens { get; set; }
    public decimal EstimatedCostPerRequest { get; set; }
    public List<string> Capabilities { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
}