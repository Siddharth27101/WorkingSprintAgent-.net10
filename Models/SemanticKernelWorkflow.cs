namespace WorkingSprintAgent.Models;

/// <summary>
/// Request-scoped state exchanged through narrow Semantic Kernel plugins.
/// </summary>
public sealed class SprintWorkflowState
{
    public Guid WorkflowId { get; init; } = Guid.NewGuid();
    public byte[] CsvContent { get; init; } = [];
    public SprintReportGenerationOptions GenerationOptions { get; init; } =
        new(null, "professional", null, WorkingSprintAgent.Services.PresentationFormat.PowerPoint);
    public SprintDataSet? Data { get; set; }
    public SprintInsights? AnalystInsights { get; set; }
    public SprintInsights? CandidateInsights { get; set; }
    public AgentReviewResult? Review { get; set; }
    public int RevisionCount { get; set; }
    public bool ManagerInvoked { get; set; }
    public AgentManagerDecision? ManagerDecision { get; set; }
    public bool QualityApproved { get; set; }
    public PresentationArtifact? Presentation { get; set; }
    public List<AgentConversationEntry> Conversation { get; } = [];
}

/// <summary>
/// Strict reviewer response used to control revision and approval.
/// </summary>
public sealed class AgentReviewResult
{
    public bool Approved { get; set; }
    public double Score { get; set; }
    public bool EscalateToManager { get; set; }
    public List<string> Issues { get; set; } = [];
    public string RevisionInstructions { get; set; } = string.Empty;
}

/// <summary>
/// Manager decision used only after the normal reviewer loop cannot finish safely.
/// </summary>
public sealed class AgentManagerDecision
{
    public string NextAction { get; set; } = "fallback";
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Minimal trace of agent collaboration retained for the current request only.
/// </summary>
public sealed record AgentConversationEntry(
    string Agent,
    string Stage,
    DateTime Timestamp,
    string Summary);
