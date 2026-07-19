using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services.Agents;
using WorkingSprintAgent.Services.Orchestration;

namespace WorkingSprintAgent.Services.Plugins;

/// <summary>
/// Exposes deterministic presentation generation as a bounded Semantic Kernel function.
/// </summary>
public sealed class PresentationPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IPresentationAgent _presentationAgent;
    private readonly ISprintWorkflowStateStore _stateStore;

    public PresentationPlugin(
        IPresentationAgent presentationAgent,
        ISprintWorkflowStateStore stateStore)
    {
        _presentationAgent = presentationAgent;
        _stateStore = stateStore;
    }

    [KernelFunction("create_sprint_presentation")]
    [Description("Creates the final presentation only from verified metrics and approved insights in workflow state.")]
    public async Task<string> CreateSprintPresentationAsync(
        [Description("The current workflow identifier.")] string workflowId,
        CancellationToken cancellationToken = default)
    {
        var state = GetState(workflowId);
        var data = state.Data
            ?? throw new InvalidOperationException("Verified sprint data is required before presentation generation.");
        if (!state.QualityApproved)
        {
            throw new InvalidOperationException(
                "Quality review approval is required before presentation generation.");
        }

        var insights = state.CandidateInsights
            ?? throw new InvalidOperationException("Approved sprint insights are required before presentation generation.");

        state.Presentation = await _presentationAgent.CreateAsync(
            data.Metrics,
            insights,
            state.GenerationOptions,
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            state.Presentation.FileName,
            state.Presentation.ContentType,
            SizeBytes = state.Presentation.Content.Length
        }, JsonOptions);
    }

    private SprintWorkflowState GetState(string workflowId)
    {
        return Guid.TryParse(workflowId, out var id)
            ? _stateStore.GetRequired(id)
            : throw new ArgumentException("A valid workflow identifier is required.", nameof(workflowId));
    }
}
