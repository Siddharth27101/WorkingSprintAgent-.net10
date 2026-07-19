using System.ComponentModel.DataAnnotations;

namespace WorkingSprintAgent.Models;

/// <summary>
/// Controls the optional Semantic Kernel multi-agent workflow.
/// </summary>
public sealed class SemanticKernelOptions
{
    public const string ConfigSection = "SemanticKernel";

    /// <summary>
    /// Enables Semantic Kernel orchestration. The deterministic workflow remains the default.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Optional model override. When empty, the OpenAI model setting is used.
    /// </summary>
    [StringLength(100)]
    public string Model { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// Maximum output tokens requested from each agent invocation.
    /// </summary>
    [Range(200, 4_000)]
    public int MaxTokensPerAgent { get; set; } = 1_200;

    /// <summary>
    /// Sampling temperature used by analyst and coach agents.
    /// </summary>
    [Range(0, 2)]
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Maximum duration for the Semantic Kernel attempt before a bounded fallback is used.
    /// </summary>
    [Range(10, 300)]
    public int TimeoutSeconds { get; set; } = 90;

    /// <summary>
    /// Maximum coach/reviewer revision cycles before escalation or fallback.
    /// </summary>
    [Range(0, 5)]
    public int MaxReviewerRevisions { get; set; } = 2;

    /// <summary>
    /// Minimum reviewer score required for approval.
    /// </summary>
    [Range(0.5, 1)]
    public double ReviewerApprovalThreshold { get; set; } = 0.8;

    /// <summary>
    /// Allows a manager agent to choose the final action only for unresolved reviews.
    /// </summary>
    public bool EnableManagerSelection { get; set; } = true;

    /// <summary>
    /// Review scores at or below this value are eligible for manager escalation.
    /// </summary>
    [Range(0, 1)]
    public double ManagerEscalationThreshold { get; set; } = 0.65;
}
