using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Orchestration;

/// <summary>
/// Stores workflow data by operation identifier for the lifetime of the current dependency-injection scope.
/// </summary>
public interface ISprintWorkflowStateStore
{
    SprintWorkflowState Begin(
        byte[] csvContent,
        SprintReportGenerationOptions generationOptions);

    SprintWorkflowState GetRequired(Guid workflowId);
}
