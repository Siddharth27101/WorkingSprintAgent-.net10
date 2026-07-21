using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Parses Jira CSV exports and multi-sheet Excel sprint workbooks.
/// </summary>
public interface ICsvSprintDataService
{
    Task<SprintDataSet> ParseDataSetAsync(
        Stream source,
        string? sprintNameOverride = null,
        CancellationToken cancellationToken = default);

    Task<List<SprintTask>> ParseAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default);

    SprintMetrics ComputeMetrics(List<SprintTask> tasks, string? sprintNameOverride = null);
}
