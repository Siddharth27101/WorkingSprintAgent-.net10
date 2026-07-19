using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Agents;

/// <summary>
/// Produces the final downloadable sprint presentation artifact.
/// </summary>
public interface IPresentationAgent
{
    Task<PresentationArtifact> CreateAsync(
        SprintMetrics metrics,
        SprintInsights insights,
        SprintReportGenerationOptions options,
        CancellationToken cancellationToken = default);
}
