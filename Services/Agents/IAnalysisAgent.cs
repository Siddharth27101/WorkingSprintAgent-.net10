using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Agents;

/// <summary>
/// Produces AI-enhanced sprint insights and associated usage metadata.
/// </summary>
public interface IAnalysisAgent
{
    Task<AIInsightsResponse> AnalyzeAsync(
        SprintMetrics metrics,
        CancellationToken cancellationToken = default);
}
