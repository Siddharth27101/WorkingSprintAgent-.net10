using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// OpenAI service implementation with comprehensive token cost optimization
/// </summary>
public class OpenAIService : IOpenAIService
{
    private readonly HttpClient? _openAIClient;
    private readonly OpenAIConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly ILogger<OpenAIService> _logger;
    private readonly ICostMonitoringService _costMonitoring;
    private readonly ITokenOptimizationService _tokenOptimization;
    private readonly ITokenUsageLogger _tokenUsageLogger;
    private readonly List<TokenUsageStats> _tokenUsageHistory;
    private readonly object _lockObject = new();

    public OpenAIService(
        IOptions<OpenAIConfiguration> config,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<OpenAIService> logger,
        ICostMonitoringService costMonitoring,
        ITokenOptimizationService tokenOptimization,
        ITokenUsageLogger tokenUsageLogger)
    {
        _config = config.Value;
        _cache = cache;
        _logger = logger;
        _costMonitoring = costMonitoring;
        _tokenOptimization = tokenOptimization;
        _tokenUsageLogger = tokenUsageLogger;
        _tokenUsageHistory = new List<TokenUsageStats>();

        // The application works in fallback mode when no API key is configured.
        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            _openAIClient = httpClientFactory.CreateClient(nameof(OpenAIService));
            _openAIClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _openAIClient.Timeout = TimeSpan.FromSeconds(Math.Max(1, _config.TimeoutSeconds));
            _openAIClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        }
    }

    public async Task<AIInsightsResponse> GenerateInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (_openAIClient is null)
            {
                _logger.LogInformation("OpenAI is not configured. Using fallback insights for sprint: {SprintName}", metrics.SprintName);
                return GenerateFallbackInsights(metrics);
            }

            // Check daily budget before making API call
            if (await IsDailyBudgetExceededAsync())
            {
                _logger.LogWarning("Daily token budget exceeded. Falling back to optimized generation.");
                return GenerateFallbackInsights(metrics);
            }

            // Use token optimization service for better cost efficiency
            var optimizedData = _tokenOptimization.OptimizeSprintData(metrics);
            var optimizedPrompt = _tokenOptimization.CreateOptimizedPrompt(optimizedData, new PromptOptimizationOptions
            {
                Level = OptimizationLevel.Balanced,
                MaxPromptLength = _config.MaxTokens / 2
            });

            // Generate cache key from optimized data for better cache hits
            var cacheKey = GenerateCacheKey(optimizedData);
            
            // Check cache first to save costs
            if (_config.EnableCaching && _cache.TryGetValue(cacheKey, out AIInsightsResponse? cachedResponse))
            {
                _logger.LogInformation("Returning cached insights for sprint: {SprintName}", metrics.SprintName);
                cachedResponse!.FromCache = true;
                return cachedResponse;
            }

            // Estimate costs before making the call
            var costEstimate = EstimateTokenCost(optimizedPrompt);
            _logger.LogInformation("Estimated cost for request: ${Cost:F4} ({Tokens} tokens)", 
                costEstimate.EstimatedTotalCost, costEstimate.EstimatedTotalTokens);

            // Make the OpenAI REST call without requiring an external SDK package.
            var chatCompletion = await CompleteChatAsync(optimizedPrompt, cancellationToken);

            stopwatch.Stop();

            // Parse the AI response
            var aiResponse = ParseAIResponse(chatCompletion.Content);
            
            // Track token usage for cost monitoring
            var tokenStats = new TokenUsageStats
            {
                Timestamp = startTime,
                RequestType = "InsightGeneration",
                InputTokens = chatCompletion.InputTokens,
                OutputTokens = chatCompletion.OutputTokens,
                TotalTokens = chatCompletion.TotalTokens,
                EstimatedCost = CalculateActualCost(chatCompletion.InputTokens, chatCompletion.OutputTokens),
                Model = _config.Model,
                ResponseTime = stopwatch.Elapsed,
                CacheHit = false
            };

            lock (_lockObject)
            {
                _tokenUsageHistory.Add(tokenStats);
            }

            // Record usage in cost monitoring service
            await _costMonitoring.RecordUsageAsync(tokenStats);

            // Record detailed usage in token usage logger
            var context = new TokenUsageContext
            {
                RequestId = Guid.NewGuid().ToString(),
                SprintName = metrics.SprintName,
                TaskCount = metrics.TotalTasks,
                DataSource = "CSV Upload",
                AdditionalProperties = new Dictionary<string, object>
                {
                    ["CompletionRate"] = metrics.CompletionRatePercent,
                    ["TeamSize"] = metrics.WorkloadByAssignee.Count,
                    ["HasBlockers"] = metrics.BlockedTasks > 0,
                    ["OptimizationLevel"] = "Balanced"
                }
            };
            await _tokenUsageLogger.LogTokenUsageAsync(tokenStats, context);

            // Log optimization event
            var optimizationEvent = new OptimizationEvent
            {
                RequestId = context.RequestId,
                EventType = OptimizationEventType.DataCompression,
                Strategy = OptimizationStrategy.DataCompression,
                Description = "Applied data compression and prompt optimization",
                Success = true,
                Duration = stopwatch.Elapsed,
                Metrics = new OptimizationMetrics
                {
                    TokensBefore = optimizedData.OriginalTokenCount,
                    TokensAfter = optimizedData.OptimizedTokenCount,
                    CostBefore = CalculateActualCost(optimizedData.OriginalTokenCount, (int)(optimizedData.OriginalTokenCount * 0.6)),
                    CostAfter = tokenStats.EstimatedCost,
                    CompressionRatio = optimizedData.CompressionRatio,
                    ProcessingTime = stopwatch.Elapsed
                }
            };
            await _tokenUsageLogger.LogOptimizationEventAsync(optimizationEvent);

            // Get optimization recommendations
            var optimizationRecommendations = await Task.Run(() => 
                _tokenOptimization.AnalyzeAndRecommend(_tokenUsageHistory.TakeLast(50).ToList()));

            var response = new AIInsightsResponse
            {
                Insights = aiResponse,
                TokenUsage = tokenStats,
                OptimizationSuggestions = optimizationRecommendations.Take(3).Select(r => r.Description).ToList(),
                FromCache = false
            };

            // Cache successful response
            if (_config.EnableCaching)
            {
                _cache.Set(cacheKey, response, TimeSpan.FromMinutes(_config.CacheExpirationMinutes));
                _logger.LogInformation("Cached insights for sprint: {SprintName}", metrics.SprintName);
            }

            _logger.LogInformation("Generated AI insights for sprint '{SprintName}' - Tokens: {Tokens}, Cost: ${Cost:F4}, Time: {Time}ms",
                metrics.SprintName, tokenStats.TotalTokens, tokenStats.EstimatedCost, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI insights for sprint: {SprintName}", metrics.SprintName);
            
            // Fallback to basic insights on error
            return GenerateFallbackInsights(metrics);
        }
    }

    public async Task<TokenUsageSummary> GetTokenUsageAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        await Task.CompletedTask; // For async interface consistency

        var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow.Date.AddDays(1);

        List<TokenUsageStats> filteredUsage;
        lock (_lockObject)
        {
            filteredUsage = _tokenUsageHistory
                .Where(u => u.Timestamp >= from && u.Timestamp < to)
                .OrderBy(u => u.Timestamp)
                .ToList();
        }

        var totalCost = filteredUsage.Sum(u => u.EstimatedCost);
        var totalRequests = filteredUsage.Count;
        var cacheHits = filteredUsage.Count(u => u.CacheHit);
        
        // Estimate cache savings (assuming cached requests would have cost the average)
        var avgCostPerRequest = totalRequests > 0 ? totalCost / totalRequests : 0;
        var cacheSavings = cacheHits * avgCostPerRequest;

        return new TokenUsageSummary
        {
            FromDate = from,
            ToDate = to,
            TotalRequests = totalRequests,
            TotalInputTokens = filteredUsage.Sum(u => u.InputTokens),
            TotalOutputTokens = filteredUsage.Sum(u => u.OutputTokens),
            TotalTokens = filteredUsage.Sum(u => u.TotalTokens),
            TotalCost = totalCost,
            AverageCostPerRequest = avgCostPerRequest,
            CacheHits = cacheHits,
            CacheSavings = cacheSavings,
            DailyBreakdown = filteredUsage
                .GroupBy(u => u.Timestamp.Date)
                .Select(g => new TokenUsageStats
                {
                    Timestamp = g.Key,
                    TotalTokens = g.Sum(u => u.TotalTokens),
                    EstimatedCost = g.Sum(u => u.EstimatedCost),
                    RequestType = $"{g.Count()} requests"
                })
                .ToList()
        };
    }

    public TokenCostEstimate EstimateTokenCost(string inputText)
    {
        // Rough token estimation (1 token ≈ 4 characters for English text)
        var estimatedInputTokens = (int)Math.Ceiling(inputText.Length / 4.0);
        var estimatedOutputTokens = Math.Min(_config.MaxTokens, 800); // Conservative estimate
        var estimatedTotalTokens = estimatedInputTokens + estimatedOutputTokens;

        var inputCost = (estimatedInputTokens / 1000.0m) * _config.CostPer1KInputTokens;
        var outputCost = (estimatedOutputTokens / 1000.0m) * _config.CostPer1KOutputTokens;
        var totalCost = inputCost + outputCost;

        var tips = new List<string>();
        if (estimatedInputTokens > 2000)
            tips.Add("Consider preprocessing data to reduce input size");
        if (estimatedOutputTokens > 1000)
            tips.Add("Reduce MaxTokens setting to limit response length");
        if (totalCost > 0.10m)
            tips.Add("Enable caching to avoid repeated costs for similar requests");

        return new TokenCostEstimate
        {
            EstimatedInputTokens = estimatedInputTokens,
            EstimatedOutputTokens = estimatedOutputTokens,
            EstimatedTotalTokens = estimatedTotalTokens,
            EstimatedInputCost = inputCost,
            EstimatedOutputCost = outputCost,
            EstimatedTotalCost = totalCost,
            CostOptimizationTips = tips
        };
    }

    public async Task<bool> IsDailyBudgetExceededAsync()
    {
        var todayUsage = await GetTokenUsageAsync(DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1));
        return todayUsage.TotalTokens >= _config.MaxDailyTokens;
    }

    public List<string> GetOptimizationRecommendations()
    {
        var recommendations = new List<string>();

        lock (_lockObject)
        {
            var recentUsage = _tokenUsageHistory.Where(u => u.Timestamp >= DateTime.UtcNow.AddHours(-24)).ToList();
            
            if (recentUsage.Count == 0) return recommendations;

            var avgTokensPerRequest = recentUsage.Average(u => u.TotalTokens);
            var totalCostLast24h = recentUsage.Sum(u => u.EstimatedCost);
            var cacheHitRate = recentUsage.Count > 0 ? recentUsage.Count(u => u.CacheHit) / (double)recentUsage.Count : 0;

            if (avgTokensPerRequest > 2000)
                recommendations.Add("High token usage detected. Consider data preprocessing to reduce input size.");

            if (totalCostLast24h > 5.0m)
                recommendations.Add("High daily costs detected. Consider using gpt-3.5-turbo for less critical insights.");

            if (cacheHitRate < 0.3)
                recommendations.Add("Low cache hit rate. Enable caching for repeated similar requests.");

            if (recentUsage.Any(u => u.ResponseTime.TotalSeconds > 10))
                recommendations.Add("Slow response times detected. Consider reducing MaxTokens or using a faster model.");

            if (recentUsage.Count > 100)
                recommendations.Add("High API usage volume. Consider batching similar requests or implementing rate limiting.");
        }

        return recommendations;
    }

    #region Private Helper Methods

    private string CreateOptimizedPrompt(SprintMetrics metrics)
    {
        // Preprocess and minimize data to reduce token usage
        var optimizedData = new
        {
            sprint = metrics.SprintName,
            totals = new { tasks = metrics.TotalTasks, completed = metrics.CompletedTasks, rate = $"{metrics.CompletionRatePercent:F0}%" },
            points = metrics.TotalStoryPoints > 0 ? new { total = metrics.TotalStoryPoints, completed = metrics.CompletedStoryPoints } : null,
            blockers = metrics.BlockedTasks > 0 ? new { count = metrics.BlockedTasks, items = metrics.BlockedTaskTitles.Take(3) } : null,
            status = metrics.TasksByStatus.Take(5).ToDictionary(k => k.Key, v => v.Value),
            team = metrics.WorkloadByAssignee.Take(5).Select(a => new { name = a.Assignee, tasks = a.TotalTasks, done = a.CompletedTasks }),
            priority = metrics.TasksByPriority.Any() ? metrics.TasksByPriority.Take(3).ToDictionary(k => k.Key, v => v.Value) : null
        };

        return $"Analyze this sprint data and generate actionable insights:\n{JsonSerializer.Serialize(optimizedData, new JsonSerializerOptions { WriteIndented = false })}";
    }

    private string GetSystemPrompt()
    {
        return @"You are an expert Agile coach and data analyst. Generate concise, actionable sprint insights in JSON format.

Return ONLY valid JSON with this exact structure:
{
  ""ExecutiveSummary"": ""2-sentence summary of sprint performance and key outcome"",
  ""KeyHighlights"": [""3-4 specific achievements or notable metrics""],
  ""RisksAndBlockers"": [""2-3 current risks or areas of concern""],
  ""Recommendations"": [""3-4 specific, actionable next steps""],
  ""TeamPerformanceNarrative"": ""1-2 sentences on team dynamics and workload distribution"",
  ""NextSprintFocus"": ""1 sentence priority for next sprint""
}

Guidelines:
- Be specific with numbers and percentages
- Focus on actionable insights, not generic observations  
- Highlight both successes and improvement areas
- Keep each field concise to minimize token usage
- Use data-driven language, avoid fluff";
    }

    private async Task<ChatCompletionResult> CompleteChatAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        if (_openAIClient is null)
        {
            throw new InvalidOperationException("OpenAI is not configured.");
        }

        var request = new
        {
            model = _config.Model,
            messages = new object[]
            {
                new { role = "system", content = GetSystemPrompt() },
                new { role = "user", content = prompt }
            },
            max_tokens = _config.MaxTokens,
            temperature = _config.Temperature,
            response_format = new { type = "json_object" }
        };

        using var response = await _openAIClient.PostAsJsonAsync(
            "chat/completions",
            request,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"OpenAI request failed with status {(int)response.StatusCode}: {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var content = root.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? throw new JsonException("OpenAI returned an empty message.");

        var usage = root.TryGetProperty("usage", out var usageElement)
            ? usageElement
            : default;
        var inputTokens = GetInt32(usage, "prompt_tokens");
        var outputTokens = GetInt32(usage, "completion_tokens");
        var totalTokens = GetInt32(usage, "total_tokens");

        return new ChatCompletionResult(
            content,
            inputTokens,
            outputTokens,
            totalTokens > 0 ? totalTokens : inputTokens + outputTokens);
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out var value)
                ? value
                : 0;
    }

    private sealed record ChatCompletionResult(
        string Content,
        int InputTokens,
        int OutputTokens,
        int TotalTokens);

    private SprintInsights ParseAIResponse(string jsonResponse)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var parsed = JsonSerializer.Deserialize<SprintInsights>(jsonResponse, options);
            return parsed ?? new SprintInsights();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse AI response as JSON: {Error}. Response: {Response}", ex.Message, jsonResponse);
            
            // Fallback: try to extract insights from raw text
            return new SprintInsights
            {
                ExecutiveSummary = "AI response parsing failed - manual review required",
                KeyHighlights = new List<string> { "Raw AI response needs manual processing" },
                RisksAndBlockers = new List<string> { "Unable to extract structured insights" },
                Recommendations = new List<string> { "Review raw AI output for manual insights extraction" },
                TeamPerformanceNarrative = jsonResponse.Length > 200 ? jsonResponse.Substring(0, 200) + "..." : jsonResponse,
                NextSprintFocus = "Manual insight extraction required"
            };
        }
    }

    private decimal CalculateActualCost(int inputTokens, int outputTokens)
    {
        var inputCost = (inputTokens / 1000.0m) * _config.CostPer1KInputTokens;
        var outputCost = (outputTokens / 1000.0m) * _config.CostPer1KOutputTokens;
        return inputCost + outputCost;
    }

    private string GenerateCacheKey(OptimizedSprintData optimizedData)
    {
        var keyData = JsonSerializer.Serialize(optimizedData.CoreMetrics) + 
                     JsonSerializer.Serialize(optimizedData.StatusSummary) +
                     optimizedData.TeamSummary.Count;
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(keyData));
        return Convert.ToHexString(hash);
    }

    private AIInsightsResponse GenerateFallbackInsights(SprintMetrics metrics)
    {
        _logger.LogInformation("Generating fallback insights for sprint: {SprintName}", metrics.SprintName);

        // Use the existing mock service logic as fallback
        var mockService = new MockInsightGenerationService(new NullLogger<MockInsightGenerationService>());
        var insights = mockService.GenerateInsightsAsync(metrics).Result;

        return new AIInsightsResponse
        {
            Insights = insights,
            TokenUsage = new TokenUsageStats
            {
                Timestamp = DateTime.UtcNow,
                RequestType = "Fallback",
                EstimatedCost = 0,
                Model = "Fallback"
            },
            OptimizationSuggestions = new List<string> { "AI service unavailable - using fallback insights generation" },
            FromCache = false
        };
    }

    #endregion
}