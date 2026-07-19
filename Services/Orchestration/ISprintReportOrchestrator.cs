using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Orchestration;

/// <summary>
/// Coordinates the file upload, analysis, and presentation agents.
/// </summary>
public interface ISprintReportOrchestrator
{
    Task<SprintAnalysisResult> AnalyzeAsync(
        Stream csvStream,
        string? sprintName,
        CancellationToken cancellationToken = default);

    Task<SprintReportWorkflowResult> GenerateAsync(
        Stream csvStream,
        SprintReportGenerationOptions options,
        CancellationToken cancellationToken = default);
}
