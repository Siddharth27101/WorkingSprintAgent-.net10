using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Mock insight generation service - used as fallback when AI is not available
/// </summary>
public class MockInsightGenerationService : IInsightGenerationService
{
    private readonly ILogger<MockInsightGenerationService> _logger;

    public MockInsightGenerationService(ILogger<MockInsightGenerationService> logger)
    {
        _logger = logger;
    }

    public bool IsAIEnabled => false;

    public Task<SprintInsights> GenerateInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger.LogInformation("Generating mock insights for sprint '{SprintName}'", metrics.SprintName);

        var insights = new SprintInsights
        {
            ExecutiveSummary = GenerateExecutiveSummary(metrics),
            KeyHighlights = GenerateKeyHighlights(metrics),
            RisksAndBlockers = GenerateRisksAndBlockers(metrics),
            Recommendations = GenerateRecommendations(metrics),
            TeamPerformanceNarrative = GenerateTeamPerformanceNarrative(metrics),
            NextSprintFocus = GenerateNextSprintFocus(metrics)
        };

        return Task.FromResult(insights);
    }

    public async Task<AIInsightsResponse> GenerateEnhancedInsightsAsync(SprintMetrics metrics, CancellationToken cancellationToken = default)
    {
        var insights = await GenerateInsightsAsync(metrics, cancellationToken);
        
        return new AIInsightsResponse
        {
            Insights = insights,
            TokenUsage = new TokenUsageStats
            {
                Timestamp = DateTime.UtcNow,
                RequestType = "Mock",
                InputTokens = 0,
                OutputTokens = 0,
                TotalTokens = 0,
                EstimatedCost = 0,
                Model = "Mock Service",
                ResponseTime = TimeSpan.FromMilliseconds(10),
                CacheHit = false
            },
            OptimizationSuggestions = new List<string>
            {
                "Mock service active - configure OpenAI API key for AI-powered insights",
                "Zero cost operation but limited analytical capabilities",
                "Enable AI service for enhanced contextual understanding"
            },
            FromCache = false
        };
    }

    public InsightServiceStatus GetServiceStatus()
    {
        return new InsightServiceStatus
        {
            IsAIEnabled = false,
            ServiceType = "Mock Service",
            Model = "Rule-based",
            IsCachingEnabled = false,
            IsTokenTrackingEnabled = false,
            MaxDailyTokens = 0,
            EstimatedCostPerRequest = 0,
            Capabilities = new List<string>
            {
                "Rule-based insight generation",
                "Statistical analysis", 
                "Pattern recognition",
                "Deterministic recommendations"
            },
            Limitations = new List<string>
            {
                "No AI-powered analysis",
                "Limited contextual understanding",
                "Static recommendation patterns",
                "No natural language generation"
            }
        };
    }

    #region Private Helper Methods - Keep existing implementation

    private static string GenerateExecutiveSummary(SprintMetrics metrics)
    {
        var performance = metrics.CompletionRatePercent switch
        {
            >= 90 => "exceptional",
            >= 80 => "strong", 
            >= 70 => "solid",
            >= 60 => "moderate",
            _ => "challenging"
        };

        var blockerNote = metrics.BlockedTasks > 0 
            ? $" {metrics.BlockedTasks} critical blocker{(metrics.BlockedTasks > 1 ? "s require" : " requires")} immediate attention."
            : " No critical blockers identified.";

        return $"{metrics.SprintName} achieved {performance} performance with {metrics.CompletionRatePercent:F1}% issue completion, " +
               $"a sprint health score of {metrics.SprintHealthScore:F0}/100, and {metrics.CompletedWork:F1} of {metrics.PlannedWork:F1} {metrics.WorkUnitLabel.ToLowerInvariant()} delivered.{blockerNote}";
    }

    private static List<string> GenerateKeyHighlights(SprintMetrics metrics)
    {
        var highlights = new List<string>();

        highlights.Add($"Completed {metrics.CompletedTasks} of {metrics.TotalTasks} planned tasks");

        if (metrics.PlannedWork > 0)
        {
            var deliveryRate = (metrics.CompletedWork / metrics.PlannedWork) * 100;
            highlights.Add($"Delivered {metrics.CompletedWork:F1} of {metrics.PlannedWork:F1} {metrics.WorkUnitLabel.ToLowerInvariant()} ({deliveryRate:F0}%)");
        }

        if (metrics.WorkloadByAssignee.Any())
        {
            var topPerformer = metrics.WorkloadByAssignee.OrderByDescending(a => a.CompletedTasks).First();
            highlights.Add($"{topPerformer.Assignee} led team performance with {topPerformer.CompletedTasks} completed tasks");
        }

        if (metrics.TasksByStatus.TryGetValue("Done", out var doneCount) && doneCount > 0)
        {
            highlights.Add($"Successfully closed {doneCount} items with quality deliverables");
        }

        return highlights.Take(4).ToList();
    }

    private static List<string> GenerateRisksAndBlockers(SprintMetrics metrics)
    {
        var risks = new List<string>();

        if (metrics.BlockedTasks > 0)
        {
            risks.Add($"{metrics.BlockedTasks} task{(metrics.BlockedTasks > 1 ? "s are" : " is")} currently blocked and impacting delivery");
        }

        if (metrics.CompletionRatePercent < 70)
        {
            risks.Add("Sprint completion rate below target threshold of 70%");
        }

        if (metrics.WorkloadByAssignee.Any())
        {
            var overloadedMembers = metrics.WorkloadByAssignee.Count(a => a.TotalTasks > 5);
            if (overloadedMembers > 0)
            {
                risks.Add($"{overloadedMembers} team member{(overloadedMembers > 1 ? "s have" : " has")} high task allocation");
            }
        }

        if (metrics.PlannedWork > 0)
        {
            var deliveryRate = (metrics.CompletedWork / metrics.PlannedWork) * 100;
            if (deliveryRate < 60)
            {
                risks.Add($"{metrics.WorkUnitLabel} delivery is significantly behind planned capacity");
            }
        }

        if (metrics.HighRiskCount > 0)
        {
            risks.Add($"{metrics.HighRiskCount} high-impact sprint risk{(metrics.HighRiskCount > 1 ? "s remain" : " remains")} open");
        }

        if (metrics.CriticalBugs > 0)
        {
            risks.Add($"{metrics.CriticalBugs} critical bug{(metrics.CriticalBugs > 1 ? "s require" : " requires")} immediate quality attention");
        }

        return risks.Any() ? risks : new List<string> { "No significant risks identified for this sprint" };
    }

    private static List<string> GenerateRecommendations(SprintMetrics metrics)
    {
        var recommendations = new List<string>();

        if (metrics.BlockedTasks > 0)
        {
            recommendations.Add("Prioritize resolving blocked items in next standup meeting");
        }

        if (metrics.CompletionRatePercent < 80)
        {
            recommendations.Add("Review sprint planning process to improve task estimation accuracy");
        }

        if (metrics.WorkloadByAssignee.Any())
        {
            var workloadVariance = metrics.WorkloadByAssignee.Max(a => a.TotalTasks) - 
                                 metrics.WorkloadByAssignee.Min(a => a.TotalTasks);
            if (workloadVariance > 3)
            {
                recommendations.Add("Rebalance task distribution across team members for next sprint");
            }
        }

        if (metrics.CompletionRatePercent >= 80)
        {
            recommendations.Add("Maintain current sprint velocity and team collaboration practices");
        }

        if (metrics.ScopeChangePercent > 10)
        {
            recommendations.Add("Add a sprint scope-change checkpoint and require explicit trade-offs for new work");
        }

        if (metrics.CriticalBugs > 0 || metrics.HighRiskCount > 0)
        {
            recommendations.Add("Assign named owners and due dates to critical defects and high-impact risks");
        }

        if (metrics.BuildFailureCount > 0)
        {
            recommendations.Add("Review failed builds and strengthen CI quality gates before the next deployment");
        }

        return recommendations.Take(5).ToList();
    }

    private static string GenerateTeamPerformanceNarrative(SprintMetrics metrics)
    {
        if (!metrics.WorkloadByAssignee.Any())
        {
            return "Team performance data not available for this sprint.";
        }

        var teamSize = metrics.WorkloadByAssignee.Count;
        var avgTasksPerPerson = metrics.TotalTasks / (double)teamSize;
        var avgWorkPerPerson = teamSize == 0 ? 0 : metrics.PlannedWork / teamSize;

        var performance = metrics.CompletionRatePercent >= 80 ? "delivered strong results" : "faced some challenges";
        
        return $"Team of {teamSize} members {performance} with an average of {avgTasksPerPerson:F1} issues and {avgWorkPerPerson:F1} {metrics.WorkUnitLabel.ToLowerInvariant()} per person. " +
               $"Collaboration and task distribution patterns show {(avgTasksPerPerson > 4 ? "high engagement" : "balanced workload")} across the sprint.";
    }

    private static string GenerateNextSprintFocus(SprintMetrics metrics)
    {
        if (metrics.BlockedTasks > 0)
        {
            return "Focus on resolving current blockers and maintaining delivery momentum.";
        }

        if (metrics.CompletionRatePercent >= 90)
        {
            return "Excellent sprint performance - consider taking on additional stretch goals next sprint.";
        }

        if (metrics.CompletionRatePercent < 70)
        {
            return "Prioritize sprint planning improvements and capacity management for better predictability.";
        }

        return "Continue current practices while identifying opportunities for process optimization.";
    }

    #endregion
}