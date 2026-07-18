using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Advanced token cost optimization service interface
/// </summary>
public interface ITokenOptimizationService
{
    /// <summary>
    /// Preprocess and optimize sprint data before sending to AI
    /// </summary>
    /// <param name="metrics">Original sprint metrics</param>
    /// <returns>Optimized metrics with reduced token footprint</returns>
    OptimizedSprintData OptimizeSprintData(SprintMetrics metrics);

    /// <summary>
    /// Generate cost-optimized prompt for AI processing
    /// </summary>
    /// <param name="optimizedData">Preprocessed sprint data</param>
    /// <param name="options">Optimization options</param>
    /// <returns>Optimized prompt with minimal token usage</returns>
    string CreateOptimizedPrompt(OptimizedSprintData optimizedData, PromptOptimizationOptions options);

    /// <summary>
    /// Analyze and recommend optimizations for future requests
    /// </summary>
    /// <param name="tokenUsageHistory">Historical token usage data</param>
    /// <returns>Optimization recommendations</returns>
    List<OptimizationRecommendation> AnalyzeAndRecommend(List<TokenUsageStats> tokenUsageHistory);

    /// <summary>
    /// Estimate potential cost savings for optimization strategies
    /// </summary>
    /// <param name="originalMetrics">Original sprint metrics</param>
    /// <param name="strategies">Optimization strategies to apply</param>
    /// <returns>Cost savings estimation</returns>
    CostSavingsEstimate EstimateSavings(SprintMetrics originalMetrics, List<OptimizationStrategy> strategies);

    /// <summary>
    /// Apply data compression techniques to reduce token usage
    /// </summary>
    /// <param name="data">Raw data to compress</param>
    /// <param name="compressionLevel">Compression level (1-5, higher = more aggressive)</param>
    /// <returns>Compressed data with metadata</returns>
    CompressedData CompressData(object data, int compressionLevel = 3);

    /// <summary>
    /// Smart batching of similar requests to reduce API calls
    /// </summary>
    /// <param name="requests">Multiple sprint analysis requests</param>
    /// <returns>Batched request with shared context</returns>
    BatchedRequest CreateBatchedRequest(List<SprintMetrics> requests);
}

/// <summary>
/// Optimized sprint data with reduced token footprint
/// </summary>
public class OptimizedSprintData
{
    public string SprintId { get; set; } = string.Empty;
    public Dictionary<string, object> CoreMetrics { get; set; } = new();
    public List<string> KeyIssues { get; set; } = new();
    public Dictionary<string, int> StatusSummary { get; set; } = new();
    public List<TeamMemberSummary> TeamSummary { get; set; } = new();
    public int OriginalTokenCount { get; set; }
    public int OptimizedTokenCount { get; set; }
    public double CompressionRatio => OriginalTokenCount > 0 ? (double)OptimizedTokenCount / OriginalTokenCount : 1.0;
}

/// <summary>
/// Team member summary for optimized processing
/// </summary>
public class TeamMemberSummary
{
    public string Name { get; set; } = string.Empty;
    public int Tasks { get; set; }
    public int Done { get; set; }
    public double Points { get; set; }
    public string Status { get; set; } = string.Empty; // "high", "normal", "blocked"
}

/// <summary>
/// Prompt optimization options
/// </summary>
public class PromptOptimizationOptions
{
    public bool UseAbbreviations { get; set; } = true;
    public bool RemoveRedundantData { get; set; } = true;
    public bool PrioritizeKeyMetrics { get; set; } = true;
    public bool UseStructuredFormat { get; set; } = true;
    public int MaxPromptLength { get; set; } = 2000;
    public OptimizationLevel Level { get; set; } = OptimizationLevel.Balanced;
}

/// <summary>
/// Optimization level
/// </summary>
public enum OptimizationLevel
{
    Conservative,  // Minimal optimization, preserve all data
    Balanced,      // Good balance between optimization and completeness
    Aggressive,    // Maximum optimization, may lose some detail
    Extreme        // Ultra-aggressive optimization for cost-critical scenarios
}

/// <summary>
/// Optimization recommendation
/// </summary>
public class OptimizationRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PotentialSavings { get; set; }
    public int PriorityLevel { get; set; }
    public List<string> ImplementationSteps { get; set; } = new();
    public string Impact { get; set; } = string.Empty;
    public OptimizationStrategy Strategy { get; set; }
}

/// <summary>
/// Optimization strategy enumeration
/// </summary>
public enum OptimizationStrategy
{
    DataCompression,
    PromptOptimization,
    ResponseCaching,
    BatchProcessing,
    SmartFiltering,
    ContextReduction,
    OutputLimiting,
    ModelDowngrade,
    RequestBatching,
    DataPreprocessing
}

/// <summary>
/// Cost savings estimation
/// </summary>
public class CostSavingsEstimate
{
    public decimal OriginalCost { get; set; }
    public decimal OptimizedCost { get; set; }
    public decimal Savings { get; set; }
    public decimal SavingsPercentage => OriginalCost > 0 ? (Savings / OriginalCost) * 100 : 0;
    public int OriginalTokens { get; set; }
    public int OptimizedTokens { get; set; }
    public Dictionary<OptimizationStrategy, decimal> SavingsByStrategy { get; set; } = new();
    public List<string> AppliedOptimizations { get; set; } = new();
    public string RecommendedAction { get; set; } = string.Empty;
}

/// <summary>
/// Compressed data with metadata
/// </summary>
public class CompressedData
{
    public object Data { get; set; } = new();
    public int OriginalSize { get; set; }
    public int CompressedSize { get; set; }
    public double CompressionRatio => OriginalSize > 0 ? (double)CompressedSize / OriginalSize : 1.0;
    public List<string> AppliedTechniques { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Batched request for processing multiple sprints
/// </summary>
public class BatchedRequest
{
    public List<OptimizedSprintData> Sprints { get; set; } = new();
    public Dictionary<string, object> SharedContext { get; set; } = new();
    public int EstimatedTokenSavings { get; set; }
    public decimal EstimatedCostSavings { get; set; }
    public string BatchId { get; set; } = Guid.NewGuid().ToString();
}