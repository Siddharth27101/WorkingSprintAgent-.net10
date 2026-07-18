namespace WorkingSprintAgent.Models;

public class SprintInsights
{
    public string ExecutiveSummary { get; set; } = string.Empty;
    public List<string> KeyHighlights { get; set; } = new();
    public List<string> RisksAndBlockers { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public string TeamPerformanceNarrative { get; set; } = string.Empty;
    public string NextSprintFocus { get; set; } = string.Empty;
}