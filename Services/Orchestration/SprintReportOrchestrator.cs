using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services.Agents;

namespace WorkingSprintAgent.Services.Orchestration;

/// <summary>
/// Runs the sprint report pipeline in order and keeps HTTP concerns out of the agents.
/// </summary>
public sealed class SprintReportOrchestrator : ISprintReportOrchestrator
{
    private readonly IFileUploadAgent _fileUploadAgent;
    private readonly IAnalysisAgent _analysisAgent;
    private readonly IPresentationAgent _presentationAgent;
    private readonly ILogger<SprintReportOrchestrator> _logger;

    public SprintReportOrchestrator(
        IFileUploadAgent fileUploadAgent,
        IAnalysisAgent analysisAgent,
        IPresentationAgent presentationAgent,
        ILogger<SprintReportOrchestrator> logger)
    {
        _fileUploadAgent = fileUploadAgent;
        _analysisAgent = analysisAgent;
        _presentationAgent = presentationAgent;
        _logger = logger;
    }

    public async Task<SprintAnalysisResult> AnalyzeAsync(
        Stream csvStream,
        string? sprintName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Agent Orchestrator started analysis workflow");

        var data = await _fileUploadAgent.ProcessAsync(csvStream, sprintName, cancellationToken);
        var aiResponse = await _analysisAgent.AnalyzeAsync(data.Metrics, cancellationToken);

        _logger.LogInformation(
            "Agent Orchestrator completed analysis workflow for '{SprintName}'",
            data.Metrics.SprintName);

        return new SprintAnalysisResult(data, aiResponse);
    }

    public async Task<SprintReportWorkflowResult> GenerateAsync(
        Stream csvStream,
        SprintReportGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger.LogInformation("Agent Orchestrator started complete report workflow");

        var analysis = await AnalyzeAsync(csvStream, options.SprintName, cancellationToken);
        var presentation = await _presentationAgent.CreateAsync(
            analysis.Data.Metrics,
            analysis.AIResponse.Insights,
            options,
            cancellationToken);

        _logger.LogInformation(
            "Agent Orchestrator completed report workflow with '{FileName}'",
            presentation.FileName);

        return new SprintReportWorkflowResult(analysis, presentation);
    }
}
