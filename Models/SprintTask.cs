namespace WorkingSprintAgent.Models;

public class SprintTask
{
    public string TaskId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Assignee { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public double StoryPoints { get; set; }
    public string SprintName { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public bool IsDone =>
        Status.Trim().Equals("Done", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("Completed", StringComparison.OrdinalIgnoreCase);

    public bool IsBlocked =>
        Status.Trim().Equals("Blocked", StringComparison.OrdinalIgnoreCase) ||
        Priority.Trim().Equals("Critical", StringComparison.OrdinalIgnoreCase) && !IsDone;
}