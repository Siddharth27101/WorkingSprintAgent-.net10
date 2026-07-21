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
    public string Resolution { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string Labels { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? DueDate { get; set; }
    public bool BlockedFlag { get; set; }
    public double OriginalEstimateHours { get; set; }
    public double TimeSpentHours { get; set; }

    public bool IsDone =>
        Status.Trim().Equals("Done", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("Closed", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("Resolved", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("Finished", StringComparison.OrdinalIgnoreCase);

    public bool IsBlocked =>
        BlockedFlag ||
        Status.Trim().Equals("Blocked", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("Impediment", StringComparison.OrdinalIgnoreCase) ||
        Status.Trim().Equals("On Hold", StringComparison.OrdinalIgnoreCase) ||
        Priority.Trim().Equals("Critical", StringComparison.OrdinalIgnoreCase) && !IsDone;
}
