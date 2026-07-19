using System.Collections.Concurrent;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Orchestration;

/// <summary>
/// Request-scoped workflow storage. Binary CSV and presentation data never enter agent chat history.
/// </summary>
public sealed class ScopedSprintWorkflowStateStore : ISprintWorkflowStateStore
{
    private readonly ConcurrentDictionary<Guid, SprintWorkflowState> _states = new();

    public SprintWorkflowState Begin(
        byte[] csvContent,
        SprintReportGenerationOptions generationOptions)
    {
        ArgumentNullException.ThrowIfNull(csvContent);
        ArgumentNullException.ThrowIfNull(generationOptions);

        var state = new SprintWorkflowState
        {
            CsvContent = csvContent.ToArray(),
            GenerationOptions = generationOptions
        };

        if (!_states.TryAdd(state.WorkflowId, state))
        {
            throw new InvalidOperationException($"Workflow state '{state.WorkflowId}' already exists.");
        }

        return state;
    }

    public SprintWorkflowState GetRequired(Guid workflowId)
    {
        return _states.TryGetValue(workflowId, out var state)
            ? state
            : throw new InvalidOperationException(
                $"Workflow state '{workflowId}' is not available in the current request scope.");
    }
}
