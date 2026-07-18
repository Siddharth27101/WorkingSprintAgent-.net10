using Microsoft.Extensions.Options;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// AI-powered insight generation service with fallback to mock service
/// </summary>
public class OpenAIInsightGenerationService : IInsightGenerationService
{
    private readonly IOpenAIService _openAIService;
    private readonly MockInsightGenerationService _fallbackService;
    private readonly OpenAIConfiguration _config;
    private readonly ILogger<OpenAIInsightGenerationService> _logger;

    public OpenAIInsightGenerationService(
        IOpenAIService openAIService,
        ILogger<OpenAIInsightGenerationService> logger,
        IOptions<OpenAIConfiguration> config)
    {
        _openAIService = openAIService;
        _config = config.Value;
        _logger = logger;
        
        // Create fallback service for when AI is not available
        _fallbackService = new MockInsightGenerationService(new NullLogger<MockInsightGenerationService>());
    }

    public bool IsAIEnabled => !string.IsNullOrWhiteSpace(_config.ApiKey);

    public async Task<SprintInsights> GenerateInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsAIEnabled)
            {
                _logger.LogInformation("AI service not configured, using fallback insights generation for sprint: {SprintName}", metrics.SprintName);
                return await _fallbackService.GenerateInsightsAsync(metrics, cancellationToken);
            }

            // Check if we're over budget before making expensive AI call
            if (await _openAIService.IsDailyBudgetExceededAsync())
            {
                _logger.LogWarning("Daily AI budget exceeded, falling back to mock service for sprint: {SprintName}", metrics.SprintName);
                return await _fallbackService.GenerateInsightsAsync(metrics, cancellationToken);
            }

            _logger.LogInformation("Generating AI-powered insights for sprint: {SprintName}", metrics.SprintName);
            
            var aiResponse = await _openAIService.GenerateInsightsAsync(metrics, cancellationToken);
            
            // Log cost information for transparency
            _logger.LogInformation("AI insights generated - Cost: ${Cost:F4}, Tokens: {Tokens}, From Cache: {FromCache}", 
                aiResponse.TokenUsage.EstimatedCost, aiResponse.TokenUsage.TotalTokens, aiResponse.FromCache);

            return aiResponse.Insights;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI insights for sprint '{SprintName}', falling back to mock service", metrics.SprintName);
            return await _fallbackService.GenerateInsightsAsync(metrics, cancellationToken);
        }
    }

    public async Task<AIInsightsResponse> GenerateEnhancedInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!IsAIEnabled)
            {
                _logger.LogInformation("AI service not configured, generating enhanced fallback insights for sprint: {SprintName}", metrics.SprintName);
                return await GenerateFallbackEnhancedInsights(metrics, cancellationToken);
            }

            _logger.LogInformation("Generating enhanced AI insights with cost tracking for sprint: {SprintName}", metrics.SprintName);
            
            var response = await _openAIService.GenerateInsightsAsync(metrics, cancellationToken);
            
            // Add additional optimization suggestions based on the request
            response.OptimizationSuggestions.AddRange(GetContextualOptimizations(metrics, response));
            
            _logger.LogInformation("Enhanced AI insights generated with {SuggestionCount} optimization suggestions", 
                response.OptimizationSuggestions.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating enhanced AI insights for sprint '{SprintName}', falling back", metrics.SprintName);
            return await GenerateFallbackEnhancedInsights(metrics, cancellationToken);
        }
    }

    public InsightServiceStatus GetServiceStatus()
    {
        var status = new InsightServiceStatus
        {
            IsAIEnabled = IsAIEnabled,
            ServiceType = IsAIEnabled ? "AI-Powered (OpenAI)" : "Fallback (Mock)",
            Model = _config.Model,
            IsCachingEnabled = _config.EnableCaching,
            IsTokenTrackingEnabled = _config.EnableTokenTracking,
            MaxDailyTokens = _config.MaxDailyTokens,
            EstimatedCostPerRequest = CalculateEstimatedCostPerRequest()
        };

        if (IsAIEnabled)
        {
            status.Capabilities.AddRange(new[]
            {
                "AI-powered insight generation",
                "Natural language processing",
                "Contextual recommendations",
                "Cost optimization strategies",
                "Token usage tracking",
                "Response caching"
            });

            status.Limitations.AddRange(new[]
            {
                $"Daily token budget: {_config.MaxDailyTokens:N0} tokens",
                $"Max tokens per request: {_config.MaxTokens:N0}",
                $"Cache expiration: {_config.CacheExpirationMinutes} minutes"
            });
        }
        else
        {
            status.Capabilities.AddRange(new[]
            {
                "Rule-based insight generation",
                "Statistical analysis",
                "Pattern recognition",
                "Deterministic recommendations"
            });

            status.Limitations.AddRange(new[]
            {
                "No AI-powered analysis",
                "Limited contextual understanding",
                "Static recommendation patterns",
                "No natural language generation"
            });
        }

        return status;
    }

    #region Private Helper Methods

    private async Task<AIInsightsResponse> GenerateFallbackEnhancedInsights(SprintMetrics metrics, CancellationToken cancellationToken)
    {
        var insights = await _fallbackService.GenerateInsightsAsync(metrics, cancellationToken);
        
        return new AIInsightsResponse
        {
            Insights = insights,
            TokenUsage = new TokenUsageStats
            {
                Timestamp = DateTime.UtcNow,
                RequestType = "Fallback",
                InputTokens = 0,
                OutputTokens = 0,
                TotalTokens = 0,
                EstimatedCost = 0,
                Model = "Mock Service",
                ResponseTime = TimeSpan.FromMilliseconds(50),
                CacheHit = false
            },
            OptimizationSuggestions = GetFallbackOptimizations(),
            FromCache = false
        };
    }

    private List<string> GetContextualOptimizations(SprintMetrics metrics, AIInsightsResponse response)
    {
        var suggestions = new List<string>();

        // Add suggestions based on the sprint performance
        if (metrics.CompletionRatePercent < 70 && response.TokenUsage.EstimatedCost > 0.05m)
        {
            suggestions.Add("For low-performing sprints, consider using cached insights or simplified analysis to reduce AI costs");
        }

        if (metrics.TotalTasks > 50 && response.TokenUsage.InputTokens > 2000)
        {
            suggestions.Add("Large datasets detected - implement data sampling or preprocessing to reduce token usage");
        }

        if (response.TokenUsage.ResponseTime.TotalSeconds > 10)
        {
            suggestions.Add("Slow AI response time - consider reducing MaxTokens or switching to a faster model");
        }

        // Team-specific optimizations
        if (metrics.WorkloadByAssignee.Count > 10)
        {
            suggestions.Add("Large team detected - consider aggregating team metrics before AI analysis to optimize costs");
        }

        return suggestions;
    }

    private List<string> GetFallbackOptimizations()
    {
        return new List<string>
        {
            "AI service not configured - set OpenAI API key to enable AI-powered insights",
            "Mock service provides deterministic insights - enable AI for more contextual analysis",
            "Consider setting up OpenAI integration for enhanced natural language insights",
            "Current mock service has zero operational costs but limited analytical capabilities"
        };
    }

    private decimal CalculateEstimatedCostPerRequest()
    {
        if (!IsAIEnabled) return 0;

        // Estimate based on average sprint data size
        var avgInputTokens = 1000; // Typical sprint data
        var avgOutputTokens = Math.Min(_config.MaxTokens, 800);
        
        var inputCost = (avgInputTokens / 1000.0m) * _config.CostPer1KInputTokens;
        var outputCost = (avgOutputTokens / 1000.0m) * _config.CostPer1KOutputTokens;
        
        return inputCost + outputCost;
    }

    #endregion
}