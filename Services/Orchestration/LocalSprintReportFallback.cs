using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services.Agents;

namespace WorkingSprintAgent.Services.Orchestration;

/// <summary>
/// Local, non-AI fallback used after an agent workflow fails or cannot pass quality review.
/// </summary>
public sealed class LocalSprintReportFallback : ISprintReportOrchestrator
{
    private readonly IFileUploadAgent _fileUploadAgent;
    private readonly IPresentationAgent _presentationAgent;
    private readonly IAnalysisAgent _analysisAgent;
    private readonly ILogger<LocalSprintReportFallback> _logger;

    public LocalSprintReportFallback(
        IFileUploadAgent fileUploadAgent,
        IPresentationAgent presentationAgent,
        MockInsightGenerationService insightService,
        ILogger<AnalysisAgent> analysisLogger,
        ILogger<LocalSprintReportFallback> logger)
    {
        _fileUploadAgent = fileUploadAgent;
        _presentationAgent = presentationAgent;
        _analysisAgent = new AnalysisAgent(insightService, analysisLogger);
        _logger = logger;
    }

    public async Task<SprintAnalysisResult> AnalyzeAsync(
        Stream csvStream,
        string? sprintName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Local fallback started analysis workflow");
        var data = await _fileUploadAgent.ProcessAsync(csvStream, sprintName, cancellationToken);
        var response = await _analysisAgent.AnalyzeAsync(data.Metrics, cancellationToken);
        return new SprintAnalysisResult(data, response);
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
