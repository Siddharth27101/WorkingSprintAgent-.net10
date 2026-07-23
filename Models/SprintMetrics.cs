namespace WorkingSprintAgent.Models;

public class SprintMetrics
{
    public string SprintName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int BlockedTasks { get; set; }
    public double TotalStoryPoints { get; set; }
    public double CompletedStoryPoints { get; set; }
    public double PlannedWork { get; set; }
    public double CompletedWork { get; set; }
    public string WorkUnitLabel { get; set; } = "Story points";
    public bool UsesWorkItemProxy { get; set; }
    public double CompletionRatePercent { get; set; }
    public double WorkCompletionRatePercent { get; set; }
    public double SprintHealthScore { get; set; }
    public bool HasSprintHealthScore { get; set; }
    public double CapacityUtilizationPercent { get; set; }
    public bool HasCapacityData { get; set; }
    public double ScopeChangePercent { get; set; }
    public bool HasScopeData { get; set; }
    public bool HasBurndownData { get; set; }
    public bool HasQualityData { get; set; }
    public bool HasCiCdData { get; set; }
    public bool HasRiskData { get; set; }
    public int ScopeAddedItems { get; set; }
    public int BugCount { get; set; }
    public int CriticalBugs { get; set; }
    public int MajorBugs { get; set; }
    public int MinorBugs { get; set; }
    public double CodeCoveragePercent { get; set; }
    public int TechnicalDebtItems { get; set; }
    public int SonarIssues { get; set; }
    public int BuildSuccessCount { get; set; }
    public int BuildFailureCount { get; set; }
    public double BuildSuccessRatePercent { get; set; }
    public int DeploymentCount { get; set; }
    public double AverageDeploymentDurationMinutes { get; set; }
    public int HighRiskCount { get; set; }
    public int OpenRiskCount { get; set; }
    public DateTime? ReportSnapshotDate { get; set; }

    // Workflow / carry-over signals
    public int InProgressTasks { get; set; }
    public int NotStartedTasks { get; set; }
    public int CarryOverTasks { get; set; }

    // Contributor concentration
    public string TopContributor { get; set; } = string.Empty;
    public int TopContributorCompleted { get; set; }
    public double TopContributorSharePercent { get; set; }

    // Quality depth
    public double DefectDensityPercent { get; set; }
    public double BugsPerContributor { get; set; }

    // Delivery cadence / flow
    public int DistinctSprintCount { get; set; }
    public double AverageCycleTimeDays { get; set; }
    public bool HasCycleTimeData { get; set; }

    // Transparent health score components (signed point contributions)
    public List<HealthComponent> HealthBreakdown { get; set; } = new();
    public Dictionary<string, int> TasksByStatus { get; set; } = new();
    public Dictionary<string, int> TasksByType { get; set; } = new();
    public Dictionary<string, int> TasksByPriority { get; set; } = new();
    public List<AssigneeLoad> WorkloadByAssignee { get; set; } = new();
    public List<string> BlockedTaskTitles { get; set; } = new();
    public List<MetricPoint> VelocityTrend { get; set; } = new();
    public List<MetricPoint> BurndownTrend { get; set; } = new();
    public List<MetricPoint> ScopeTrend { get; set; } = new();
    public List<RiskMetric> Risks { get; set; } = new();
}

public class AssigneeLoad
{
    public string Assignee { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public double StoryPoints { get; set; }
    public double CompletedStoryPoints { get; set; }
    public double CompletionRatePercent => TotalTasks == 0 ? 0 : CompletedTasks * 100.0 / TotalTasks;
    public double PointsCompletionRatePercent => StoryPoints <= 0 ? 0 : CompletedStoryPoints * 100.0 / StoryPoints;
    public int RemainingTasks => Math.Max(0, TotalTasks - CompletedTasks);
}

public class MetricPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public double? ComparisonValue { get; set; }
}

public class HealthComponent
{
    public string Label { get; set; } = string.Empty;
    public double Points { get; set; }
    public string Detail { get; set; } = string.Empty;
    public bool IsTotal { get; set; }
}

public class RiskMetric
{
    public string Name { get; set; } = string.Empty;
    public string Probability { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string MitigationStatus { get; set; } = string.Empty;
    public double Score { get; set; }
}
