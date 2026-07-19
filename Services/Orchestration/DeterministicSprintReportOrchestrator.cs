using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services.Agents;

namespace WorkingSprintAgent.Services.Orchestration;

/// <summary>
/// Reliable parse-analyze-present pipeline retained as the default and fallback workflow.
/// </summary>
public sealed class DeterministicSprintReportOrchestrator : ISprintReportOrchestrator
{
    private readonly IFileUploadAgent _fileUploadAgent;
    private readonly IAnalysisAgent _analysisAgent;
    private readonly IPresentationAgent _presentationAgent;
    private readonly ILogger<DeterministicSprintReportOrchestrator> _logger;

    public DeterministicSprintReportOrchestrator(
        IFileUploadAgent fileUploadAgent,
        IAnalysisAgent analysisAgent,
        IPresentationAgent presentationAgent,
        ILogger<DeterministicSprintReportOrchestrator> logger)
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
        _logger.LogInformation("Deterministic orchestrator started analysis workflow");

        var data = await _fileUploadAgent.ProcessAsync(csvStream, sprintName, cancellationToken);
        var aiResponse = await _analysisAgent.AnalyzeAsync(data.Metrics, cancellationToken);

        _logger.LogInformation(
            "Deterministic orchestrator completed analysis for '{SprintName}'",
            data.Metrics.SprintName);

        return new SprintAnalysisResult(data, aiResponse);
    }

    public async Task<SprintReportWorkflowResult> GenerateAsync(
        Stream csvStream,
        SprintReportGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var analysis = await AnalyzeAsync(csvStream, options.SprintName, cancellationToken);
        var presentation = await _presentationAgent.CreateAsync(
            analysis.Data.Metrics,
            analysis.AIResponse.Insights,
            options,
            cancellationToken);

        return new SprintReportWorkflowResult(analysis, presentation);
    }
}
