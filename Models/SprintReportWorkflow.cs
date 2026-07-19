using WorkingSprintAgent.Services;

namespace WorkingSprintAgent.Models;

/// <summary>
/// Parsed sprint data produced by the file-upload agent.
/// </summary>
public sealed record SprintDataSet(
    IReadOnlyList<SprintTask> Tasks,
    SprintMetrics Metrics);

/// <summary>
/// Combined output from the file-upload and analysis agents.
/// </summary>
public sealed record SprintAnalysisResult(
    SprintDataSet Data,
    AIInsightsResponse AIResponse);

/// <summary>
/// Options passed through the orchestrator to the presentation agent.
/// </summary>
public sealed record SprintReportGenerationOptions(
    string? SprintName,
    string Template,
    string? CompanyName,
    PresentationFormat OutputFormat);

/// <summary>
/// Downloadable file produced by the presentation agent.
/// </summary>
public sealed record PresentationArtifact(
    byte[] Content,
    string ContentType,
    string FileName);

/// <summary>
/// Completed output of the agent-orchestrated sprint report workflow.
/// </summary>
public sealed record SprintReportWorkflowResult(
    SprintAnalysisResult Analysis,
    PresentationArtifact Presentation);
