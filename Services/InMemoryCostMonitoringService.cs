using System.Globalization;
using System.Text;
using System.Text.Json;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// In-memory implementation of cost monitoring service for development and testing
/// </summary>
public class InMemoryCostMonitoringService : ICostMonitoringService
{
    private readonly List<TokenUsageStats> _usageHistory;
    private readonly List<CostAlert> _alerts;
    private readonly ILogger<InMemoryCostMonitoringService> _logger;
    private readonly object _lockObject = new();

    public InMemoryCostMonitoringService(ILogger<InMemoryCostMonitoringService> logger)
    {
        _logger = logger;
        _usageHistory = new List<TokenUsageStats>();
        _alerts = new List<CostAlert>();
    }

    public Task RecordUsageAsync(TokenUsageStats usage)
    {
        lock (_lockObject)
        {
            _usageHistory.Add(usage);
            
            // Keep only last 1000 records to prevent memory issues
            if (_usageHistory.Count > 1000)
            {
                _usageHistory.RemoveRange(0, _usageHistory.Count - 1000);
            }
        }

        _logger.LogDebug("Recorded usage: {RequestType} - {Tokens} tokens, ${Cost:F4}",
            usage.RequestType, usage.TotalTokens, usage.EstimatedCost);

        // Check for alerts asynchronously
        _ = Task.Run(CheckCostAlertsAsync);

        return Task.CompletedTask;
    }

    public Task<CostAnalysisReport> GetCostAnalysisAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        List<TokenUsageStats> filteredUsage;
        lock (_lockObject)
        {
            filteredUsage = _usageHistory
                .Where(u => u.Timestamp >= from && u.Timestamp <= to)
                .OrderBy(u => u.Timestamp)
                .ToList();
        }

        var report = new CostAnalysisReport
        {
            FromDate = from,
            ToDate = to,
            TotalCost = filteredUsage.Sum(u => u.EstimatedCost),
            TotalRequests = filteredUsage.Count,
            TotalTokens = filteredUsage.Sum(u => u.TotalTokens)
        };

        if (report.TotalRequests > 0)
        {
            report.AverageCostPerRequest = report.TotalCost / report.TotalRequests;
        }

        if (report.TotalTokens > 0)
        {
            report.AverageCostPerToken = report.TotalCost / report.TotalTokens;
        }

        // Costs by model
        report.CostsByModel = filteredUsage
            .GroupBy(u => u.Model)
            .ToDictionary(g => g.Key, g => g.Sum(u => u.EstimatedCost));

        // Costs by request type
        report.CostsByRequestType = filteredUsage
            .GroupBy(u => u.RequestType)
            .ToDictionary(g => g.Key, g => g.Sum(u => u.EstimatedCost));

        // Daily costs
        report.DailyCosts = filteredUsage
            .GroupBy(u => u.Timestamp.Date)
            .ToDictionary(g => g.Key, g => g.Sum(u => u.EstimatedCost));

        // Top expensive requests
        report.TopExpensiveRequests = filteredUsage
            .OrderByDescending(u => u.EstimatedCost)
            .Take(10)
            .ToList();

        // Cache efficiency
        var cacheHits = filteredUsage.Count(u => u.CacheHit);
        report.CacheEfficiency = new CacheEfficiencyStats
        {
            HitRate = filteredUsage.Count > 0 ? (double)cacheHits / filteredUsage.Count : 0,
            TotalHits = cacheHits,
            TotalMisses = filteredUsage.Count - cacheHits,
            SavingsFromCache = filteredUsage.Where(u => u.CacheHit).Sum(u => u.EstimatedCost * 0.8m), // Estimate savings
            AverageResponseTime = TimeSpan.FromMilliseconds(filteredUsage.Average(u => u.ResponseTime.TotalMilliseconds)),
            AverageCacheResponseTime = cacheHits > 0 
                ? TimeSpan.FromMilliseconds(filteredUsage.Where(u => u.CacheHit).Average(u => u.ResponseTime.TotalMilliseconds))
                : TimeSpan.Zero
        };

        // Cost trends
        report.CostTrends = GenerateCostTrends(filteredUsage);

        // Projected monthly cost
        var dailyAverage = report.DailyCosts.Values.Any() ? report.DailyCosts.Values.Average() : 0;
        report.ProjectedMonthlyCost = dailyAverage * 30;

        return Task.FromResult(report);
    }

    public Task<CostDashboard> GetDashboardDataAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(today.Year, today.Month, 1);

        List<TokenUsageStats> allUsage;
        lock (_lockObject)
        {
            allUsage = _usageHistory.ToList();
        }

        var todayUsage = allUsage.Where(u => u.Timestamp.Date == today).ToList();
        var weekUsage = allUsage.Where(u => u.Timestamp.Date >= weekStart).ToList();
        var monthUsage = allUsage.Where(u => u.Timestamp.Date >= monthStart).ToList();

        var dashboard = new CostDashboard
        {
            LastUpdated = now,
            TodayCost = todayUsage.Sum(u => u.EstimatedCost),
            WeekCost = weekUsage.Sum(u => u.EstimatedCost),
            MonthCost = monthUsage.Sum(u => u.EstimatedCost),
            TodayRequests = todayUsage.Count,
            TodayTokens = todayUsage.Sum(u => u.TotalTokens),
            BudgetUtilization = CalculateBudgetUtilization(todayUsage),
            
            RecentActivity = allUsage
                .OrderByDescending(u => u.Timestamp)
                .Take(10)
                .Select(u => new RecentActivity
                {
                    Timestamp = u.Timestamp,
                    RequestType = u.RequestType,
                    SprintName = "Sprint Analysis", // Would be extracted from context in real implementation
                    Tokens = u.TotalTokens,
                    Cost = u.EstimatedCost,
                    FromCache = u.CacheHit,
                    ResponseTime = u.ResponseTime
                })
                .ToList(),

            HourlyCosts = GenerateHourlyCosts(todayUsage),
            
            RequestsByType = allUsage
                .Where(u => u.Timestamp.Date == today)
                .GroupBy(u => u.RequestType)
                .ToDictionary(g => g.Key, g => g.Count()),

            Performance = new PerformanceMetrics
            {
                AverageResponseTime = todayUsage.Any() 
                    ? TimeSpan.FromMilliseconds(todayUsage.Average(u => u.ResponseTime.TotalMilliseconds))
                    : TimeSpan.Zero,
                RequestsPerHour = todayUsage.Count > 0 ? todayUsage.Count / Math.Max(1, (int)(now - today).TotalHours) : 0,
                CostEfficiency = todayUsage.Count > 0 ? todayUsage.Sum(u => u.EstimatedCost) / todayUsage.Count : 0,
                SuccessRate = 0.95, // Would be calculated from actual success/failure data
                OptimizationScore = GetOptimizationScore(todayUsage)
            }
        };

        lock (_lockObject)
        {
            dashboard.ActiveAlerts = _alerts.Where(a => !a.IsAcknowledged).ToList();
        }

        return Task.FromResult(dashboard);
    }

    public Task<List<CostAlert>> CheckCostAlertsAsync()
    {
        var alerts = new List<CostAlert>();
        var now = DateTime.UtcNow;
        var today = now.Date;

        List<TokenUsageStats> todayUsage;
        lock (_lockObject)
        {
            todayUsage = _usageHistory.Where(u => u.Timestamp.Date == today).ToList();
        }

        var todayCost = todayUsage.Sum(u => u.EstimatedCost);
        var dailyBudget = 10.0m; // Example daily budget

        // Daily budget check
        if (todayCost > dailyBudget)
        {
            alerts.Add(new CostAlert
            {
                Timestamp = now,
                Type = CostAlertType.DailyBudgetExceeded,
                Title = "Daily Budget Exceeded",
                Description = $"Today's costs (${todayCost:F2}) have exceeded the daily budget of ${dailyBudget:F2}",
                Severity = CostAlertSeverity.Warning,
                ThresholdValue = dailyBudget,
                ActualValue = todayCost,
                RecommendedAction = "Consider enabling more aggressive cost optimization or review usage patterns"
            });
        }

        // Cache hit rate check
        if (todayUsage.Any())
        {
            var cacheHitRate = todayUsage.Count(u => u.CacheHit) / (double)todayUsage.Count;
            if (cacheHitRate < 0.3)
            {
                alerts.Add(new CostAlert
                {
                    Timestamp = now,
                    Type = CostAlertType.LowCacheHitRate,
                    Title = "Low Cache Hit Rate",
                    Description = $"Cache hit rate is {cacheHitRate:P1}, which may lead to higher costs",
                    Severity = CostAlertSeverity.Info,
                    ThresholdValue = 0.3m,
                    ActualValue = (decimal)cacheHitRate,
                    RecommendedAction = "Review caching strategy and increase cache expiration time if appropriate"
                });
            }
        }

        // High token usage check
        var averageTokens = todayUsage.Any() ? todayUsage.Average(u => u.TotalTokens) : 0;
        if (averageTokens > 2000)
        {
            alerts.Add(new CostAlert
            {
                Timestamp = now,
                Type = CostAlertType.HighTokenUsage,
                Title = "High Token Usage Detected",
                Description = $"Average token usage per request ({averageTokens:F0}) is higher than optimal",
                Severity = CostAlertSeverity.Warning,
                ThresholdValue = 2000m,
                ActualValue = (decimal)averageTokens,
                RecommendedAction = "Enable data compression and prompt optimization features"
            });
        }

        lock (_lockObject)
        {
            _alerts.AddRange(alerts);
            // Keep only recent alerts (last 7 days)
            var cutoffDate = now.AddDays(-7);
            _alerts.RemoveAll(a => a.Timestamp < cutoffDate);
        }

        return Task.FromResult(alerts);
    }

    public Task<CostPrediction> PredictCostsAsync(int days = 30)
    {
        List<TokenUsageStats> recentUsage;
        lock (_lockObject)
        {
            recentUsage = _usageHistory
                .Where(u => u.Timestamp >= DateTime.UtcNow.AddDays(-14))
                .ToList();
        }

        var dailyAverageCost = 0m;
        var confidenceLevel = 0.5m;

        if (recentUsage.Any())
        {
            var dailyCosts = recentUsage
                .GroupBy(u => u.Timestamp.Date)
                .Select(g => g.Sum(u => u.EstimatedCost))
                .ToList();

            dailyAverageCost = dailyCosts.Average();
            confidenceLevel = dailyCosts.Count >= 7 ? 0.8m : 0.5m; // Higher confidence with more data
        }

        var predictedCost = dailyAverageCost * days;
        var variance = dailyAverageCost * 0.2m; // 20% variance

        var prediction = new CostPrediction
        {
            PredictionDate = DateTime.UtcNow,
            DaysForward = days,
            PredictedCost = predictedCost,
            ConfidenceLevel = confidenceLevel,
            Methodology = "Linear extrapolation based on recent usage patterns",
            MinEstimate = predictedCost - variance,
            MaxEstimate = predictedCost + variance,
            AssumptionsAndFactors = new List<string>
            {
                "Based on last 14 days of usage data",
                "Assumes consistent usage patterns",
                "Does not account for seasonal variations",
                "Includes current optimization settings"
            },
            RiskFactors = new List<string>
            {
                "Increased request volume",
                "Changes in data complexity",
                "Model pricing changes",
                "Optimization setting modifications"
            }
        };

        // Generate forecast points
        var currentDate = DateTime.UtcNow.Date;
        for (int i = 1; i <= days; i++)
        {
            var date = currentDate.AddDays(i);
            var variation = 1m + (((decimal)Random.Shared.NextDouble() - 0.5m) * 0.1m);
            var dailyCost = dailyAverageCost * variation;

            prediction.ForecastPoints.Add(new CostForecastPoint
            {
                Date = date,
                PredictedCost = dailyCost,
                LowerBound = dailyCost * 0.8m,
                UpperBound = dailyCost * 1.2m,
                Confidence = confidenceLevel > 0.7m ? "High" : confidenceLevel > 0.5m ? "Medium" : "Low"
            });
        }

        return Task.FromResult(prediction);
    }

    public Task<List<CostOptimizationOpportunity>> GetOptimizationOpportunitiesAsync()
    {
        var opportunities = new List<CostOptimizationOpportunity>();

        List<TokenUsageStats> recentUsage;
        lock (_lockObject)
        {
            recentUsage = _usageHistory
                .Where(u => u.Timestamp >= DateTime.UtcNow.AddDays(-7))
                .ToList();
        }

        if (!recentUsage.Any()) return Task.FromResult(opportunities);

        var averageTokens = recentUsage.Average(u => u.TotalTokens);
        var cacheHitRate = recentUsage.Count(u => u.CacheHit) / (double)recentUsage.Count;
        var totalWeeklyCost = recentUsage.Sum(u => u.EstimatedCost);

        // Data compression opportunity
        if (averageTokens > 1500)
        {
            opportunities.Add(new CostOptimizationOpportunity
            {
                Category = "Data Optimization",
                Title = "Implement Advanced Data Compression",
                Description = "High token usage indicates opportunities for data compression and preprocessing",
                PotentialSavings = totalWeeklyCost * 0.3m,
                ImplementationEffort = 3,
                Priority = 1,
                Benefits = new List<string>
                {
                    "Reduce token usage by 30-40%",
                    "Faster API responses",
                    "Lower costs per request"
                },
                Risks = new List<string>
                {
                    "Potential loss of data detail",
                    "Need for testing to ensure quality"
                },
                ImplementationSteps = new List<string>
                {
                    "Enable aggressive data compression",
                    "Implement smart data filtering",
                    "Test with sample sprint data",
                    "Monitor insight quality"
                },
                ROIEstimate = "3-6 months payback period",
                Strategy = OptimizationStrategy.DataCompression
            });
        }

        // Caching opportunity
        if (cacheHitRate < 0.4)
        {
            opportunities.Add(new CostOptimizationOpportunity
            {
                Category = "Caching Strategy",
                Title = "Improve Response Caching",
                Description = "Low cache hit rate indicates missed cost-saving opportunities",
                PotentialSavings = totalWeeklyCost * 0.25m,
                ImplementationEffort = 2,
                Priority = 2,
                Benefits = new List<string>
                {
                    "Reduce API calls by 25-40%",
                    "Faster response times",
                    "Better user experience"
                },
                ImplementationSteps = new List<string>
                {
                    "Increase cache expiration time",
                    "Implement smarter cache keys",
                    "Add cache warming strategies"
                },
                ROIEstimate = "1-2 months payback period",
                Strategy = OptimizationStrategy.ResponseCaching
            });
        }

        // Model optimization
        if (totalWeeklyCost > 20m)
        {
            opportunities.Add(new CostOptimizationOpportunity
            {
                Category = "Model Selection",
                Title = "Hybrid Model Strategy",
                Description = "High costs suggest opportunity for smart model selection based on request complexity",
                PotentialSavings = totalWeeklyCost * 0.5m,
                ImplementationEffort = 4,
                Priority = 3,
                Benefits = new List<string>
                {
                    "Use cheaper models for routine analysis",
                    "Reserve premium models for complex insights",
                    "Significant cost reduction potential"
                },
                Risks = new List<string>
                {
                    "Potential quality differences",
                    "Need for complexity assessment logic"
                },
                ImplementationSteps = new List<string>
                {
                    "Implement request complexity scoring",
                    "Set up model selection logic",
                    "A/B test model performance",
                    "Monitor quality metrics"
                },
                ROIEstimate = "2-4 months payback period",
                Strategy = OptimizationStrategy.ModelDowngrade
            });
        }

        return Task.FromResult(opportunities.OrderBy(o => o.Priority).ToList());
    }

    public Task<byte[]> ExportCostReportAsync(string format = "CSV", DateTime? fromDate = null, DateTime? toDate = null)
    {
        var from = fromDate ?? DateTime.UtcNow.AddDays(-30);
        var to = toDate ?? DateTime.UtcNow;

        List<TokenUsageStats> filteredUsage;
        lock (_lockObject)
        {
            filteredUsage = _usageHistory
                .Where(u => u.Timestamp >= from && u.Timestamp <= to)
                .OrderBy(u => u.Timestamp)
                .ToList();
        }

        byte[] data;

        switch (format.ToUpper())
        {
            case "JSON":
                var json = JsonSerializer.Serialize(filteredUsage, new JsonSerializerOptions { WriteIndented = true });
                data = Encoding.UTF8.GetBytes(json);
                break;

            case "CSV":
            default:
                data = GenerateCsvReport(filteredUsage);
                break;
        }

        return Task.FromResult(data);
    }

    #region Private Helper Methods

    private static List<string> GenerateCostTrends(List<TokenUsageStats> usage)
    {
        var trends = new List<string>();

        if (usage.Count < 2) return trends;

        var orderedUsage = usage.OrderBy(u => u.Timestamp).ToList();
        var firstHalf = orderedUsage.Take(orderedUsage.Count / 2);
        var secondHalf = orderedUsage.Skip(orderedUsage.Count / 2);

        var firstHalfAvg = firstHalf.Average(u => u.EstimatedCost);
        var secondHalfAvg = secondHalf.Average(u => u.EstimatedCost);

        if (secondHalfAvg > firstHalfAvg * 1.1m)
            trends.Add("Costs are trending upward - consider optimization");
        else if (secondHalfAvg < firstHalfAvg * 0.9m)
            trends.Add("Costs are trending downward - optimizations are working");
        else
            trends.Add("Costs are stable");

        return trends;
    }

    private static decimal CalculateBudgetUtilization(List<TokenUsageStats> todayUsage)
    {
        var dailyBudget = 10.0m; // Example budget
        var todayCost = todayUsage.Sum(u => u.EstimatedCost);
        return dailyBudget > 0 ? Math.Min(1.0m, todayCost / dailyBudget) : 0;
    }

    private static List<CostTrend> GenerateHourlyCosts(List<TokenUsageStats> todayUsage)
    {
        return todayUsage
            .GroupBy(u => u.Timestamp.Hour)
            .Select(g => new CostTrend
            {
                Time = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, g.Key, 0, 0),
                Cost = g.Sum(u => u.EstimatedCost),
                Requests = g.Count(),
                Tokens = g.Sum(u => u.TotalTokens)
            })
            .OrderBy(ct => ct.Time)
            .ToList();
    }

    private static string GetOptimizationScore(List<TokenUsageStats> todayUsage)
    {
        if (!todayUsage.Any()) return "No data";

        var avgTokens = todayUsage.Average(u => u.TotalTokens);
        var cacheHitRate = todayUsage.Count(u => u.CacheHit) / (double)todayUsage.Count;

        var score = 100;
        if (avgTokens > 2000) score -= 20;
        if (avgTokens > 3000) score -= 20;
        if (cacheHitRate < 0.3) score -= 15;
        if (cacheHitRate < 0.2) score -= 15;

        return score switch
        {
            >= 90 => "Excellent",
            >= 75 => "Good",
            >= 60 => "Average",
            >= 40 => "Poor",
            _ => "Needs Improvement"
        };
    }

    private static byte[] GenerateCsvReport(List<TokenUsageStats> usage)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,RequestType,Model,InputTokens,OutputTokens,TotalTokens,EstimatedCost,ResponseTime,CacheHit");

        foreach (var u in usage)
        {
            sb.AppendLine($"{u.Timestamp:yyyy-MM-dd HH:mm:ss},{u.RequestType},{u.Model}," +
                         $"{u.InputTokens},{u.OutputTokens},{u.TotalTokens},{u.EstimatedCost:F6}," +
                         $"{u.ResponseTime.TotalSeconds:F2},{u.CacheHit}");
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    #endregion
}