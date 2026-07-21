using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Agents;

/// <summary>
/// Validates and transforms an uploaded CSV or Excel stream into sprint tasks and metrics.
/// </summary>
public interface IFileUploadAgent
{
    Task<SprintDataSet> ProcessAsync(
        Stream csvStream,
        string? sprintName,
        CancellationToken cancellationToken = default);
}
