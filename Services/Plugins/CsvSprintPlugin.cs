using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services.Orchestration;

namespace WorkingSprintAgent.Services.Plugins;

/// <summary>
/// Exposes deterministic CSV parsing and metrics as safe Semantic Kernel functions.
/// </summary>
public sealed class CsvSprintPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ICsvSprintDataService _csvService;
    private readonly ISprintWorkflowStateStore _stateStore;

    public CsvSprintPlugin(
        ICsvSprintDataService csvService,
        ISprintWorkflowStateStore stateStore)
    {
        _csvService = csvService;
        _stateStore = stateStore;
    }

    [KernelFunction("load_sprint_data")]
    [Description("Parses the uploaded CSV for a workflow and stores verified sprint tasks and metrics. Call once before analysis.")]
    public async Task<string> LoadSprintDataAsync(
        [Description("The current workflow identifier.")] string workflowId,
        CancellationToken cancellationToken = default)
    {
        var state = GetState(workflowId);
        if (state.Data is null)
        {
            await using var stream = new MemoryStream(state.CsvContent, writable: false);
            var tasks = await _csvService.ParseAsync(stream, cancellationToken);
            var metrics = _csvService.ComputeMetrics(tasks, state.GenerationOptions.SprintName);
            state.Data = new SprintDataSet(tasks, metrics);
        }

        return SerializeMetrics(state.Data.Metrics);
    }

    [KernelFunction("get_verified_sprint_metrics")]
    [Description("Returns verified sprint metrics previously loaded for this workflow. Never invent or recalculate values.")]
    public string GetVerifiedSprintMetrics(
        [Description("The current workflow identifier.")] string workflowId)
    {
        var state = GetState(workflowId);
        var data = state.Data
            ?? throw new InvalidOperationException("Sprint data must be loaded before metrics are requested.");

        return SerializeMetrics(data.Metrics);
    }

    private SprintWorkflowState GetState(string workflowId)
    {
        return Guid.TryParse(workflowId, out var id)
            ? _stateStore.GetRequired(id)
            : throw new ArgumentException("A valid workflow identifier is required.", nameof(workflowId));
    }

    private static string SerializeMetrics(SprintMetrics metrics)
    {
        var payload = new
        {
            SprintName = Truncate(metrics.SprintName),
            metrics.TotalTasks,
            metrics.CompletedTasks,
            metrics.BlockedTasks,
            metrics.TotalStoryPoints,
            metrics.CompletedStoryPoints,
            metrics.CompletionRatePercent,
            TasksByStatus = SummarizeCategories(metrics.TasksByStatus),
            TasksByType = SummarizeCategories(metrics.TasksByType),
            TasksByPriority = SummarizeCategories(metrics.TasksByPriority),
            TeamMemberCount = metrics.WorkloadByAssignee.Count,
            TeamMembersShown = metrics.WorkloadByAssignee
                .OrderByDescending(member => member.StoryPoints)
                .ThenBy(member => member.Assignee, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .Select(member => new
                {
                    Assignee = Truncate(member.Assignee),
                    member.TotalTasks,
                    member.CompletedTasks,
                    member.StoryPoints
                }),
            TeamMembersOmitted = Math.Max(0, metrics.WorkloadByAssignee.Count - 20),
            BlockedTaskCount = metrics.BlockedTaskTitles.Count,
            BlockedTaskTitlesShown = metrics.BlockedTaskTitles.Take(20).Select(Truncate),
            BlockedTaskTitlesOmitted = Math.Max(0, metrics.BlockedTaskTitles.Count - 20)
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        if (json.Length > 24_000)
        {
            throw new InvalidDataException(
                "Verified sprint metrics exceed the safe Semantic Kernel context limit.");
        }

        return json;
    }

    private static object SummarizeCategories(IReadOnlyDictionary<string, int> categories)
    {
        return new
        {
            Values = categories
                .OrderByDescending(category => category.Value)
                .ThenBy(category => category.Key, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .Select(category => new
                {
                    Label = Truncate(category.Key),
                    Count = category.Value
                }),
            OmittedCategories = Math.Max(0, categories.Count - 20)
        };
    }

    private static string Truncate(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
        return normalized.Length <= 200 ? normalized : normalized[..200];
    }
}
