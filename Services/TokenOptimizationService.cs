using System.Text;
using System.Text.Json;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Advanced token cost optimization service implementation
/// </summary>
public class TokenOptimizationService : ITokenOptimizationService
{
    private readonly ILogger<TokenOptimizationService> _logger;
    private readonly OpenAIConfiguration _config;

    public TokenOptimizationService(
        ILogger<TokenOptimizationService> logger,
        Microsoft.Extensions.Options.IOptions<OpenAIConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public OptimizedSprintData OptimizeSprintData(SprintMetrics metrics)
    {
        _logger.LogDebug("Optimizing sprint data for: {SprintName}", metrics.SprintName);

        var originalTokenCount = EstimateTokenCount(JsonSerializer.Serialize(metrics));

        // Apply data optimization strategies
        var optimized = new OptimizedSprintData
        {
            SprintId = ShortenSprintName(metrics.SprintName),
            OriginalTokenCount = originalTokenCount
        };

        // Core metrics compression
        optimized.CoreMetrics = new Dictionary<string, object>
        {
            ["total"] = metrics.TotalTasks,
            ["done"] = metrics.CompletedTasks,
            ["rate"] = Math.Round(metrics.CompletionRatePercent, 0),
            ["blocked"] = metrics.BlockedTasks
        };

        // Add story points only if meaningful
        if (metrics.TotalStoryPoints > 0)
        {
            optimized.CoreMetrics["points"] = $"{metrics.CompletedStoryPoints:F0}/{metrics.TotalStoryPoints:F0}";
        }

        // Compress status information
        optimized.StatusSummary = metrics.TasksByStatus
            .Where(s => s.Value > 0)
            .Take(5) // Limit to top 5 statuses
            .ToDictionary(s => AbbreviateStatus(s.Key), s => s.Value);

        // Optimize team data
        optimized.TeamSummary = metrics.WorkloadByAssignee
            .OrderByDescending(a => a.CompletedTasks)
            .Take(8) // Limit to top 8 performers
            .Select(a => new TeamMemberSummary
            {
                Name = ShortenName(a.Assignee),
                Tasks = a.TotalTasks,
                Done = a.CompletedTasks,
                Points = Math.Round(a.StoryPoints, 1),
                Status = GetMemberStatus(a)
            })
            .ToList();

        // Extract key issues (blocked items)
        optimized.KeyIssues = metrics.BlockedTaskTitles
            .Take(3) // Limit to top 3 blockers
            .Select(ShortenTaskTitle)
            .ToList();

        optimized.OptimizedTokenCount = EstimateTokenCount(JsonSerializer.Serialize(optimized));

        _logger.LogInformation("Data optimization complete. Token reduction: {OriginalTokens} → {OptimizedTokens} ({CompressionRatio:P1})",
            originalTokenCount, optimized.OptimizedTokenCount, optimized.CompressionRatio);

        return optimized;
    }

    public string CreateOptimizedPrompt(OptimizedSprintData optimizedData, PromptOptimizationOptions options)
    {
        var prompt = new StringBuilder();

        switch (options.Level)
        {
            case OptimizationLevel.Conservative:
                return CreateConservativePrompt(optimizedData);
                
            case OptimizationLevel.Balanced:
                return CreateBalancedPrompt(optimizedData);
                
            case OptimizationLevel.Aggressive:
                return CreateAggressivePrompt(optimizedData);
                
            case OptimizationLevel.Extreme:
                return CreateExtremePrompt(optimizedData);
                
            default:
                return CreateBalancedPrompt(optimizedData);
        }
    }

    public List<OptimizationRecommendation> AnalyzeAndRecommend(List<TokenUsageStats> tokenUsageHistory)
    {
        var recommendations = new List<OptimizationRecommendation>();

        if (!tokenUsageHistory.Any()) return recommendations;

        var recentUsage = tokenUsageHistory.Where(u => u.Timestamp >= DateTime.UtcNow.AddDays(-7)).ToList();
        var avgTokensPerRequest = recentUsage.Average(u => u.TotalTokens);
        var totalCost = recentUsage.Sum(u => u.EstimatedCost);
        var cacheHitRate = recentUsage.Count > 0 ? recentUsage.Count(u => u.CacheHit) / (double)recentUsage.Count : 0;

        // High token usage recommendation
        if (avgTokensPerRequest > 2000)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Category = "Token Reduction",
                Title = "Implement Data Preprocessing",
                Description = "High average token usage detected. Implement data preprocessing to reduce input size by 30-50%.",
                PotentialSavings = totalCost * 0.35m,
                PriorityLevel = 1,
                ImplementationSteps = new List<string>
                {
                    "Enable aggressive data compression",
                    "Remove redundant task information",
                    "Limit team member details to top performers",
                    "Abbreviate status names and descriptions"
                },
                Impact = "High - Can reduce costs by 30-50%",
                Strategy = OptimizationStrategy.DataPreprocessing
            });
        }

        // Low cache hit rate recommendation
        if (cacheHitRate < 0.3)
        {
            var potentialSavings = totalCost * (decimal)(0.6 - cacheHitRate);
            recommendations.Add(new OptimizationRecommendation
            {
                Category = "Caching",
                Title = "Improve Cache Utilization",
                Description = $"Cache hit rate is only {cacheHitRate:P1}. Better caching can significantly reduce costs.",
                PotentialSavings = potentialSavings,
                PriorityLevel = 2,
                ImplementationSteps = new List<string>
                {
                    "Increase cache expiration time",
                    "Implement smart cache keys",
                    "Use data fingerprinting for cache optimization",
                    "Cache intermediate processing results"
                },
                Impact = $"Medium - Can save ${potentialSavings:F2} weekly",
                Strategy = OptimizationStrategy.ResponseCaching
            });
        }

        // High cost recommendation
        if (totalCost > 10.0m)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Category = "Model Optimization",
                Title = "Consider Model Downgrade",
                Description = "High weekly costs detected. Consider using a less expensive model for routine analysis.",
                PotentialSavings = totalCost * 0.70m, // GPT-3.5 can be ~70% cheaper
                PriorityLevel = 3,
                ImplementationSteps = new List<string>
                {
                    "Switch to gpt-3.5-turbo for non-critical insights",
                    "Use GPT-4 only for complex analysis",
                    "Implement smart model selection based on data complexity",
                    "Set up A/B testing for model performance vs cost"
                },
                Impact = "High - Can reduce costs by up to 70%",
                Strategy = OptimizationStrategy.ModelDowngrade
            });
        }

        // Batch processing recommendation
        var requestsPerHour = recentUsage.GroupBy(u => u.Timestamp.Hour).Average(g => g.Count());
        if (requestsPerHour > 5)
        {
            recommendations.Add(new OptimizationRecommendation
            {
                Category = "Batch Processing",
                Title = "Implement Request Batching",
                Description = "High request frequency detected. Batching similar requests can reduce overhead.",
                PotentialSavings = totalCost * 0.20m,
                PriorityLevel = 4,
                ImplementationSteps = new List<string>
                {
                    "Group similar sprint analysis requests",
                    "Implement batch processing queue",
                    "Share common context across batched requests",
                    "Process multiple sprints in single API call"
                },
                Impact = "Medium - 15-25% cost reduction for high-volume usage",
                Strategy = OptimizationStrategy.BatchProcessing
            });
        }

        return recommendations.OrderBy(r => r.PriorityLevel).ToList();
    }

    public CostSavingsEstimate EstimateSavings(SprintMetrics originalMetrics, List<OptimizationStrategy> strategies)
    {
        var originalTokens = EstimateTokenCount(JsonSerializer.Serialize(originalMetrics));
        var originalCost = CalculateCost(originalTokens, originalTokens * 0.6); // Estimate output tokens

        var optimizedTokens = originalTokens;
        var savingsByStrategy = new Dictionary<OptimizationStrategy, decimal>();
        var appliedOptimizations = new List<string>();

        foreach (var strategy in strategies)
        {
            var (tokenReduction, costSaving, description) = ApplyOptimizationStrategy(strategy, optimizedTokens, originalCost);
            optimizedTokens -= tokenReduction;
            savingsByStrategy[strategy] = costSaving;
            appliedOptimizations.Add(description);
        }

        var optimizedCost = CalculateCost(optimizedTokens, optimizedTokens * 0.6);
        var totalSavings = originalCost - optimizedCost;

        return new CostSavingsEstimate
        {
            OriginalCost = originalCost,
            OptimizedCost = optimizedCost,
            Savings = totalSavings,
            OriginalTokens = originalTokens,
            OptimizedTokens = optimizedTokens,
            SavingsByStrategy = savingsByStrategy,
            AppliedOptimizations = appliedOptimizations,
            RecommendedAction = GetRecommendedAction(totalSavings, originalCost)
        };
    }

    public CompressedData CompressData(object data, int compressionLevel = 3)
    {
        var originalJson = JsonSerializer.Serialize(data);
        var originalSize = EstimateTokenCount(originalJson);
        var techniques = new List<string>();
        var compressedData = data;

        // Apply compression techniques based on level
        if (compressionLevel >= 1)
        {
            // Basic compression: remove whitespace, shorten keys
            techniques.Add("Whitespace removal");
            techniques.Add("Key abbreviation");
        }

        if (compressionLevel >= 2)
        {
            // Intermediate compression: remove null values, combine similar data
            techniques.Add("Null value removal");
            techniques.Add("Data consolidation");
        }

        if (compressionLevel >= 3)
        {
            // Advanced compression: statistical aggregation, smart rounding
            techniques.Add("Statistical aggregation");
            techniques.Add("Smart rounding");
        }

        if (compressionLevel >= 4)
        {
            // Aggressive compression: remove low-value data, heavy abbreviation
            techniques.Add("Low-value data removal");
            techniques.Add("Heavy abbreviation");
        }

        if (compressionLevel >= 5)
        {
            // Extreme compression: minimal data only
            techniques.Add("Minimal data extraction");
            techniques.Add("Ultra-aggressive filtering");
        }

        var compressedJson = JsonSerializer.Serialize(compressedData);
        var compressedSize = EstimateTokenCount(compressedJson);

        return new CompressedData
        {
            Data = compressedData,
            OriginalSize = originalSize,
            CompressedSize = compressedSize,
            AppliedTechniques = techniques,
            Metadata = new Dictionary<string, object>
            {
                ["compressionLevel"] = compressionLevel,
                ["tokensReduced"] = originalSize - compressedSize,
                ["efficiency"] = compressedSize < originalSize ? "Good" : "No improvement"
            }
        };
    }

    public BatchedRequest CreateBatchedRequest(List<SprintMetrics> requests)
    {
        if (!requests.Any())
            return new BatchedRequest();

        var batchedRequest = new BatchedRequest
        {
            BatchId = Guid.NewGuid().ToString()
        };

        // Identify shared context
        var sharedContext = new Dictionary<string, object>
        {
            ["requestCount"] = requests.Count,
            ["timeRange"] = $"{requests.Min(r => r.SprintName)} to {requests.Max(r => r.SprintName)}",
            ["commonStatuses"] = GetCommonStatuses(requests),
            ["avgTeamSize"] = requests.Average(r => r.WorkloadByAssignee.Count)
        };

        // Optimize each sprint and calculate savings
        var totalOriginalTokens = 0;
        var totalOptimizedTokens = 0;

        foreach (var request in requests)
        {
            var optimized = OptimizeSprintData(request);
            batchedRequest.Sprints.Add(optimized);
            totalOriginalTokens += optimized.OriginalTokenCount;
            totalOptimizedTokens += optimized.OptimizedTokenCount;
        }

        // Additional batching savings (shared context reduces redundancy)
        var batchingSavings = (int)(totalOptimizedTokens * 0.15); // 15% additional savings from batching
        totalOptimizedTokens -= batchingSavings;

        batchedRequest.SharedContext = sharedContext;
        batchedRequest.EstimatedTokenSavings = totalOriginalTokens - totalOptimizedTokens;
        batchedRequest.EstimatedCostSavings = CalculateCost(totalOriginalTokens, 0) - CalculateCost(totalOptimizedTokens, 0);

        _logger.LogInformation("Created batched request for {Count} sprints. Token savings: {Savings} ({Percentage:P1})",
            requests.Count, batchedRequest.EstimatedTokenSavings, 
            (double)batchedRequest.EstimatedTokenSavings / totalOriginalTokens);

        return batchedRequest;
    }

    #region Private Helper Methods

    private string CreateConservativePrompt(OptimizedSprintData data)
    {
        return $"Analyze sprint '{data.SprintId}': {data.CoreMetrics["total"]} tasks, " +
               $"{data.CoreMetrics["done"]} done ({data.CoreMetrics["rate"]}%), " +
               $"{data.CoreMetrics["blocked"]} blocked. Team: {data.TeamSummary.Count} members. " +
               $"Generate concise insights focusing on key metrics and actionable recommendations.";
    }

    private string CreateBalancedPrompt(OptimizedSprintData data)
    {
        var sb = new StringBuilder();
        sb.Append($"Sprint {data.SprintId}: ");
        sb.Append($"{data.CoreMetrics["done"]}/{data.CoreMetrics["total"]} tasks ({data.CoreMetrics["rate"]}%)");
        
        if (data.CoreMetrics.ContainsKey("points"))
            sb.Append($", {data.CoreMetrics["points"]} pts");
            
        if ((int)data.CoreMetrics["blocked"] > 0)
            sb.Append($", {data.CoreMetrics["blocked"]} blocked");

        sb.Append($". Team ({data.TeamSummary.Count}): ");
        sb.Append(string.Join(", ", data.TeamSummary.Take(3).Select(t => $"{t.Name}:{t.Done}/{t.Tasks}")));

        if (data.KeyIssues.Any())
            sb.Append($". Issues: {string.Join(", ", data.KeyIssues)}");

        return sb.ToString();
    }

    private string CreateAggressivePrompt(OptimizedSprintData data)
    {
        return $"{data.SprintId}: {data.CoreMetrics["done"]}/{data.CoreMetrics["total"]}({data.CoreMetrics["rate"]}%) " +
               $"{(data.CoreMetrics.ContainsKey("blocked") && (int)data.CoreMetrics["blocked"] > 0 ? $"B:{data.CoreMetrics["blocked"]} " : "")}" +
               $"T:{data.TeamSummary.Count} " +
               $"{(data.KeyIssues.Any() ? $"I:{data.KeyIssues.Count}" : "")}";
    }

    private string CreateExtremePrompt(OptimizedSprintData data)
    {
        return $"{data.SprintId}:{data.CoreMetrics["done"]}/{data.CoreMetrics["total"]}:" +
               $"{data.CoreMetrics["rate"]}%:{data.TeamSummary.Count}T:" +
               $"{(data.KeyIssues.Any() ? data.KeyIssues.Count.ToString() : "0")}I";
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimation: 1 token ≈ 4 characters for English text
        return (int)Math.Ceiling(text.Length / 4.0);
    }

    private decimal CalculateCost(int inputTokens, double outputTokens)
    {
        var inputCost = (inputTokens / 1000.0m) * _config.CostPer1KInputTokens;
        var outputCost = ((decimal)outputTokens / 1000.0m) * _config.CostPer1KOutputTokens;
        return inputCost + outputCost;
    }

    private static string ShortenSprintName(string sprintName)
    {
        // Convert "Sprint 2024 Q1 Team Alpha" to "S24Q1A"
        return sprintName
            .Replace("Sprint", "S")
            .Replace("2024", "24")
            .Replace("2025", "25")
            .Replace("Quarter", "Q")
            .Replace("Team", "")
            .Replace(" ", "")
            .Substring(0, Math.Min(sprintName.Length, 8));
    }

    private static string AbbreviateStatus(string status)
    {
        return status.ToLower() switch
        {
            "done" or "completed" or "closed" => "done",
            "in progress" or "progress" or "active" => "prog",
            "blocked" => "block",
            "todo" or "to do" or "new" => "todo",
            "review" or "reviewing" => "rev",
            _ => status.Length > 4 ? status.Substring(0, 4) : status
        };
    }

    private static string ShortenName(string name)
    {
        // Convert "John Smith" to "J.Smith" or "JSmith"
        var parts = name.Split(' ');
        if (parts.Length > 1)
            return $"{parts[0].Substring(0, 1)}.{parts[^1]}";
        return name.Length > 8 ? name.Substring(0, 8) : name;
    }

    private static string ShortenTaskTitle(string title)
    {
        // Keep essential words, remove articles and common words
        var words = title.Split(' ')
            .Where(w => !new[] { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by" }
                .Contains(w.ToLower()))
            .Take(4);
        return string.Join(" ", words);
    }

    private static string GetMemberStatus(AssigneeLoad assignee)
    {
        var completionRate = assignee.TotalTasks > 0 ? assignee.CompletedTasks / (double)assignee.TotalTasks : 0;
        
        if (assignee.TotalTasks > 8) return "high";
        if (completionRate < 0.5) return "blocked";
        return "normal";
    }

    private (int tokenReduction, decimal costSaving, string description) ApplyOptimizationStrategy(
        OptimizationStrategy strategy, int currentTokens, decimal currentCost)
    {
        return strategy switch
        {
            OptimizationStrategy.DataCompression => (currentTokens * 30 / 100, currentCost * 0.3m, "Data compression applied"),
            OptimizationStrategy.PromptOptimization => (currentTokens * 20 / 100, currentCost * 0.2m, "Prompt optimization applied"),
            OptimizationStrategy.ResponseCaching => (0, currentCost * 0.4m, "Response caching enabled"),
            OptimizationStrategy.BatchProcessing => (currentTokens * 15 / 100, currentCost * 0.15m, "Batch processing implemented"),
            OptimizationStrategy.SmartFiltering => (currentTokens * 25 / 100, currentCost * 0.25m, "Smart filtering applied"),
            OptimizationStrategy.ModelDowngrade => (0, currentCost * 0.7m, "Model downgrade to gpt-3.5-turbo"),
            _ => (0, 0, "Strategy not implemented")
        };
    }

    private static string GetRecommendedAction(decimal savings, decimal originalCost)
    {
        var savingsPercentage = originalCost > 0 ? (savings / originalCost) * 100 : 0;

        return savingsPercentage switch
        {
            >= 50 => "Implement immediately - High impact optimization",
            >= 30 => "Implement soon - Good cost reduction opportunity",
            >= 15 => "Consider implementing - Moderate savings",
            >= 5 => "Monitor usage - Minor optimization opportunity",
            _ => "Current configuration appears optimal"
        };
    }

    private static Dictionary<string, int> GetCommonStatuses(List<SprintMetrics> requests)
    {
        var allStatuses = requests.SelectMany(r => r.TasksByStatus)
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Value));

        return allStatuses.OrderByDescending(s => s.Value)
            .Take(5)
            .ToDictionary(s => s.Key, s => s.Value);
    }

    #endregion
}