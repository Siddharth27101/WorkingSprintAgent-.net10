using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

public interface IInsightGenerationService
{
    Task<SprintInsights> GenerateInsightsAsync(SprintMetrics metrics, CancellationToken ct = default);
}