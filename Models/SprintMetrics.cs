namespace WorkingSprintAgent.Models;

public class SprintMetrics
{
    public string SprintName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int BlockedTasks { get; set; }
    public double TotalStoryPoints { get; set; }
    public double CompletedStoryPoints { get; set; }
    public double CompletionRatePercent { get; set; }
    public Dictionary<string, int> TasksByStatus { get; set; } = new();
    public Dictionary<string, int> TasksByType { get; set; } = new();
    public Dictionary<string, int> TasksByPriority { get; set; } = new();
    public List<AssigneeLoad> WorkloadByAssignee { get; set; } = new();
    public List<string> BlockedTaskTitles { get; set; } = new();
}

public class AssigneeLoad
{
    public string Assignee { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public double StoryPoints { get; set; }
}