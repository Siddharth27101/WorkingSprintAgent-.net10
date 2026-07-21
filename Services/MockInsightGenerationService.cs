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
        var target = metrics.CompletionRatePercent switch
        {
            >= 90 => "at or above expectations",
            >= 80 => "close to the expected target",
            >= 70 => "slightly below the expected target",
            >= 60 => "below the expected target",
            _ => "significantly below the expected target"
        };

        var sentences = new List<string>
        {
            $"{metrics.SprintName} completion reached {metrics.CompletionRatePercent:F0}% " +
            $"({metrics.CompletedTasks} of {metrics.TotalTasks} issues), {target}, for a health score of {metrics.SprintHealthScore:F0}/100."
        };

        if (metrics.CarryOverTasks > 0)
        {
            var blockerClause = metrics.BlockedTasks > 0
                ? $"{metrics.BlockedTasks} item(s) remain blocked and"
                : "no blockers remain, yet";
            sentences.Add($"With {blockerClause} {metrics.CarryOverTasks} issue(s) carried over " +
                          $"({metrics.NotStartedTasks} never started), the data points to overcommitment or execution bottlenecks.");
        }

        if (!string.IsNullOrEmpty(metrics.TopContributor) && metrics.TopContributorSharePercent >= 20)
        {
            sentences.Add($"{metrics.TopContributor} led delivery with {metrics.TopContributorCompleted} completed issue(s) " +
                          $"({metrics.TopContributorSharePercent:F0}% of all delivered work), suggesting an uneven workload.");
        }

        if (metrics.CriticalBugs > 0 || metrics.HighRiskCount > 0)
        {
            sentences.Add($"{metrics.CriticalBugs} critical bug(s) and {metrics.HighRiskCount} high-risk item(s) should be prioritised before sprint scope is increased.");
        }

        return string.Join(' ', sentences);
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

        if (!string.IsNullOrEmpty(metrics.TopContributor))
        {
            highlights.Add($"{metrics.TopContributor} led delivery with {metrics.TopContributorCompleted} completed issues ({metrics.TopContributorSharePercent:F0}% of delivered work)");
        }
        else if (metrics.WorkloadByAssignee.Any())
        {
            var topPerformer = metrics.WorkloadByAssignee.OrderByDescending(a => a.CompletedTasks).First();
            highlights.Add($"{topPerformer.Assignee} led team performance with {topPerformer.CompletedTasks} completed tasks");
        }

        if (metrics.HasCycleTimeData)
        {
            highlights.Add($"Average cycle time was {metrics.AverageCycleTimeDays:F1} day(s) from start to completion");
        }

        if (metrics.TasksByStatus.TryGetValue("Done", out var doneCount) && doneCount > 0)
        {
            highlights.Add($"Successfully closed {doneCount} items with quality deliverables");
        }

        return highlights.Take(5).ToList();
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

        // Contributor concentration — call out the specific person and share.
        if (!string.IsNullOrEmpty(metrics.TopContributor) && metrics.TopContributorSharePercent >= 30)
        {
            recommendations.Add(
                $"{metrics.TopContributor} completed {metrics.TopContributorSharePercent:F0}% of all delivered work " +
                $"({metrics.TopContributorCompleted} issues) — redistribute ownership to reduce concentration risk.");
        }

        // Right-size the next commitment based on demonstrated throughput.
        if (metrics.CompletionRatePercent < 70 && metrics.TotalTasks > 0)
        {
            var suggestedCommitment = Math.Max(1, (int)Math.Round(metrics.CompletedTasks * 1.1));
            var reduction = Math.Max(0, (int)Math.Round(100 - metrics.CompletionRatePercent - 10));
            recommendations.Add(
                $"Only {metrics.CompletionRatePercent:F0}% of issues were completed — reduce the next sprint commitment by ~{reduction}% " +
                $"(to roughly {suggestedCommitment} issues) to match demonstrated throughput.");
        }

        // Quality first when critical defects exist.
        if (metrics.CriticalBugs > 0)
        {
            recommendations.Add(
                $"Prioritise the {metrics.CriticalBugs} critical bug(s) before accepting new feature work.");
        }

        // Stalled work that never started.
        if (metrics.NotStartedTasks > 0)
        {
            recommendations.Add(
                $"Analyse why {metrics.NotStartedTasks} issue(s) never moved beyond \"Not Started\" — refine intake, dependencies, or WIP limits.");
        }

        if (metrics.BlockedTasks > 0)
        {
            recommendations.Add(
                $"Clear the {metrics.BlockedTasks} blocked item(s) in the next standup before pulling in new scope.");
        }

        if (metrics.HighRiskCount > 0)
        {
            recommendations.Add(
                $"Assign named owners and due dates to the {metrics.HighRiskCount} high-impact risk(s).");
        }

        if (metrics.ScopeChangePercent > 10)
        {
            recommendations.Add(
                $"Scope grew by {metrics.ScopeChangePercent:F0}% mid-sprint — add a scope-change checkpoint requiring explicit trade-offs.");
        }

        if (metrics.BuildFailureCount > 0)
        {
            recommendations.Add("Review failed builds and strengthen CI quality gates before the next deployment.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Maintain current velocity and collaboration practices; consider a modest stretch goal next sprint.");
        }

        return recommendations.Take(6).ToList();
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
        var distribution = metrics.TopContributorSharePercent >= 35
            ? $"delivery was concentrated, with {metrics.TopContributor} accounting for {metrics.TopContributorSharePercent:F0}% of completed work"
            : "work was reasonably distributed across the team";

        return $"A team of {teamSize} members {performance} with an average of {avgTasksPerPerson:F1} issues and {avgWorkPerPerson:F1} {metrics.WorkUnitLabel.ToLowerInvariant()} per person. " +
               $"On distribution, {distribution}.";
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
            var suggestedCommitment = Math.Max(1, (int)Math.Round(metrics.CompletedTasks * 1.1));
            return $"Right-size the next commitment to ~{suggestedCommitment} issues, clear {metrics.CarryOverTasks} carried-over item(s), and improve planning predictability.";
        }

        return "Continue current practices while identifying opportunities for process optimization.";
    }

    #endregion
}