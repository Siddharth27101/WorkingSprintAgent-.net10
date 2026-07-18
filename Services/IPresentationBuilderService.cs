using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

public interface IPresentationBuilderService
{
    byte[] BuildPresentation(SprintMetrics metrics, SprintInsights insights);
}