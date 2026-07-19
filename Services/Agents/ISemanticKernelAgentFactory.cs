using Microsoft.SemanticKernel.Agents;

namespace WorkingSprintAgent.Services.Agents;

/// <summary>
/// Creates role-specific Semantic Kernel agents with least-privilege plugin access.
/// </summary>
public interface ISemanticKernelAgentFactory
{
    ChatCompletionAgent CreateAnalystAgent();
    ChatCompletionAgent CreateCoachAgent();
    ChatCompletionAgent CreateReviewerAgent();
    ChatCompletionAgent CreateManagerAgent();
}
