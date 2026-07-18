using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

public class MockInsightGenerationService : IInsightGenerationService
{
    private readonly ILogger<MockInsightGenerationService> _logger;

    public MockInsightGenerationService(ILogger<MockInsightGenerationService> logger)
    {
        _logger = logger;
    }

    public Task<SprintInsights> GenerateInsightsAsync(SprintMetrics metrics, CancellationToken ct = default)
    {
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

        return $"{metrics.SprintName} achieved {performance} performance with {metrics.CompletionRatePercent}% task completion and {metrics.CompletedStoryPoints:F1} of {metrics.TotalStoryPoints:F1} story points delivered.{blockerNote}";
    }

    private static List<string> GenerateKeyHighlights(SprintMetrics metrics)
    {
        var highlights = new List<string>();

        highlights.Add($"Completed {metrics.CompletedTasks} of {metrics.TotalTasks} planned tasks");

        if (metrics.TotalStoryPoints > 0)
        {
            var pointsRate = (metrics.CompletedStoryPoints / metrics.TotalStoryPoints) * 100;
            highlights.Add($"Delivered {metrics.CompletedStoryPoints:F1} story points ({pointsRate:F0}% of planned capacity)");
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

        if (metrics.TotalStoryPoints > 0)
        {
            var pointsRate = (metrics.CompletedStoryPoints / metrics.TotalStoryPoints) * 100;
            if (pointsRate < 60)
            {
                risks.Add("Story point delivery significantly behind planned capacity");
            }
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

        return recommendations.Take(3).ToList();
    }

    private static string GenerateTeamPerformanceNarrative(SprintMetrics metrics)
    {
        if (!metrics.WorkloadByAssignee.Any())
        {
            return "Team performance data not available for this sprint.";
        }

        var teamSize = metrics.WorkloadByAssignee.Count;
        var avgTasksPerPerson = metrics.TotalTasks / (double)teamSize;
        var avgPointsPerPerson = metrics.TotalStoryPoints / teamSize;

        var performance = metrics.CompletionRatePercent >= 80 ? "delivered strong results" : "faced some challenges";
        
        return $"Team of {teamSize} members {performance} with an average of {avgTasksPerPerson:F1} tasks and {avgPointsPerPerson:F1} story points per person. " +
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
}