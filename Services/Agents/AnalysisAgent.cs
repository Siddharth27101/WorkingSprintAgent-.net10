using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Agents;

/// <summary>
/// Analysis workflow stage using GPT-4o mini when configured and deterministic fallback otherwise.
/// </summary>
public sealed class AnalysisAgent : IAnalysisAgent
{
    private readonly IInsightGenerationService _insightService;
    private readonly ILogger<AnalysisAgent> _logger;

    public AnalysisAgent(
        IInsightGenerationService insightService,
        ILogger<AnalysisAgent> logger)
    {
        _insightService = insightService;
        _logger = logger;
    }

    public async Task<AIInsightsResponse> AnalyzeAsync(
        SprintMetrics metrics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Analysis Agent started for sprint '{SprintName}' using {Mode}",
            metrics.SprintName,
            _insightService.IsAIEnabled ? "OpenAI" : "fallback analysis");

        var response = await _insightService.GenerateEnhancedInsightsAsync(metrics, cancellationToken);

        _logger.LogInformation(
            "Analysis Agent completed for sprint '{SprintName}' with model {Model}",
            metrics.SprintName,
            response.TokenUsage.Model);

        return response;
    }
}
