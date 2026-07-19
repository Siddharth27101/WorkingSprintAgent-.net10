using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

public interface ICsvSprintDataService
{
    Task<List<SprintTask>> ParseAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default);

    SprintMetrics ComputeMetrics(List<SprintTask> tasks, string? sprintNameOverride = null);
}