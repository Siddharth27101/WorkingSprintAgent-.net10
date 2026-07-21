using System.Globalization;
using System.Text;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Parses either Jira CSV exports or multi-sheet .xlsx sprint workbooks and computes presentation metrics.
/// The historical service name is retained to avoid breaking existing integrations.
/// </summary>
public class CsvSprintDataService : ICsvSprintDataService
{
    private const int MaximumTaskCount = 20_000;
    private readonly ILogger<CsvSprintDataService> _logger;

    public CsvSprintDataService(ILogger<CsvSprintDataService> logger)
    {
        _logger = logger;
    }

    public async Task<SprintDataSet> ParseDataSetAsync(
        Stream source,
        string? sprintNameOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0)
        {
            throw new InvalidDataException("The uploaded data file is empty.");
        }

        buffer.Position = 0;
        List<SprintTask> tasks;
        IReadOnlyList<WorkbookSheet> workbookSheets = [];
        if (IsZipPackage(buffer))
        {
            buffer.Position = 0;
            try
            {
                workbookSheets = XlsxWorkbookReader.Read(buffer);
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or System.Xml.XmlException or NotSupportedException or ArgumentException)
            {
                throw new InvalidDataException("The Excel workbook is malformed or uses an unsupported structure.", ex);
            }

            var issueSheet = FindIssueSheet(workbookSheets)
                ?? throw new InvalidDataException(
                    "The workbook must contain an Issues sheet with issue key, summary, status, and assignee columns.");
            if (issueSheet.Rows.Count > MaximumTaskCount)
            {
                throw new InvalidDataException($"The Issues sheet contains more than the supported maximum of {MaximumTaskCount:N0} issues.");
            }
            tasks = ParseTaskRows(issueSheet.Rows);
            _logger.LogInformation(
                "Parsed {Count} issues from Excel workbook with {SheetCount} readable sheets",
                tasks.Count,
                workbookSheets.Count);
        }
        else
        {
            buffer.Position = 0;
            var rows = await ReadCsvRowsAsync(buffer, cancellationToken);
            tasks = ParseTaskRows(rows);
            _logger.LogInformation("Parsed {Count} sprint issue rows from CSV", tasks.Count);
        }

        ValidateTaskCount(tasks);
        var metrics = ComputeMetrics(tasks, sprintNameOverride);
        if (workbookSheets.Count > 0)
        {
            ApplyWorkbookMetrics(metrics, workbookSheets);
            metrics.WorkCompletionRatePercent = Percentage(metrics.CompletedWork, metrics.PlannedWork);
            FinalizeHealthScore(metrics);
        }

        return new SprintDataSet(tasks, metrics);
    }

    public async Task<List<SprintTask>> ParseAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default)
    {
        var dataSet = await ParseDataSetAsync(csvStream, cancellationToken: cancellationToken);
        return dataSet.Tasks.ToList();
    }

    public SprintMetrics ComputeMetrics(List<SprintTask> tasks, string? sprintNameOverride = null)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var distinctSprints = tasks
            .Select(task => task.SprintName?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sprintName = sprintNameOverride
            ?? (distinctSprints.Count switch
            {
                0 => "Current Sprint",
                1 => distinctSprints[0]!,
                _ => $"Sprint Portfolio ({distinctSprints.Count} sprints)"
            });
        var hasStoryPoints = tasks.Any(task => task.StoryPoints > 0);
        var snapshotDate = tasks
            .SelectMany(task => new[] { task.UpdatedDate, task.EndDate, task.StartDate })
            .Where(date => date.HasValue)
            .Select(date => date!.Value)
            .DefaultIfEmpty()
            .Max();

        var metrics = new SprintMetrics
        {
            SprintName = sprintName,
            TotalTasks = tasks.Count,
            CompletedTasks = tasks.Count(task => task.IsDone),
            BlockedTasks = tasks.Count(task => task.IsBlocked),
            TotalStoryPoints = tasks.Sum(task => task.StoryPoints),
            CompletedStoryPoints = tasks.Where(task => task.IsDone).Sum(task => task.StoryPoints),
            UsesWorkItemProxy = !hasStoryPoints,
            WorkUnitLabel = hasStoryPoints ? "Story points" : "Work items (story-point proxy)",
            PlannedWork = hasStoryPoints ? tasks.Sum(task => task.StoryPoints) : tasks.Count,
            CompletedWork = hasStoryPoints
                ? tasks.Where(task => task.IsDone).Sum(task => task.StoryPoints)
                : tasks.Count(task => task.IsDone),
            ReportSnapshotDate = snapshotDate == default ? null : snapshotDate,
            HasQualityData = tasks.Any(task => !string.IsNullOrWhiteSpace(task.Type) || !string.IsNullOrWhiteSpace(task.Priority) || !string.IsNullOrWhiteSpace(task.Labels)),
            BugCount = tasks.Count(IsBug),
            CriticalBugs = tasks.Count(task => IsBug(task) && IsPriority(task, "critical", "highest", "blocker")),
            MajorBugs = tasks.Count(task => IsBug(task) && IsPriority(task, "high", "major")),
            MinorBugs = tasks.Count(task => IsBug(task) && !IsPriority(task, "critical", "highest", "blocker", "high", "major")),
            TechnicalDebtItems = tasks.Count(task => ContainsToken(task.Labels, "tech-debt", "technical debt")),
            SonarIssues = tasks.Count(task => ContainsToken(task.Labels, "sonar"))
        };

        metrics.CompletionRatePercent = Percentage(metrics.CompletedTasks, metrics.TotalTasks);
        metrics.WorkCompletionRatePercent = Percentage(metrics.CompletedWork, metrics.PlannedWork);
        metrics.TasksByStatus = GroupCounts(tasks, task => task.Status);
        metrics.TasksByType = GroupCounts(tasks, task => task.Type);
        metrics.TasksByPriority = GroupCounts(tasks, task => task.Priority);
        metrics.WorkloadByAssignee = tasks
            .GroupBy(task => NormalizeCategory(task.Assignee, "Unassigned"), StringComparer.OrdinalIgnoreCase)
            .Select(group => new AssigneeLoad
            {
                Assignee = group.Key,
                TotalTasks = group.Count(),
                CompletedTasks = group.Count(task => task.IsDone),
                StoryPoints = hasStoryPoints ? group.Sum(task => task.StoryPoints) : group.Count()
            })
            .OrderByDescending(member => member.CompletedTasks)
            .ThenByDescending(member => member.TotalTasks)
            .ToList();
        metrics.BlockedTaskTitles = tasks
            .Where(task => task.IsBlocked)
            .Select(task => string.IsNullOrWhiteSpace(task.TaskId)
                ? task.Title.Trim()
                : $"{task.TaskId}: {task.Title}".Trim(':', ' '))
            .Take(50)
            .ToList();

        metrics.VelocityTrend = BuildVelocityTrend(tasks, hasStoryPoints);
        metrics.BurndownTrend = BuildBurndownTrend(tasks, hasStoryPoints);
        metrics.ScopeTrend =
        [
            new MetricPoint { Label = "Committed", Value = metrics.TotalTasks },
            new MetricPoint { Label = "Current", Value = metrics.TotalTasks }
        ];
        metrics.ScopeAddedItems = 0;
        metrics.ScopeChangePercent = 0;
        metrics.CapacityUtilizationPercent = CalculateCapacityUtilization(tasks);
        metrics.HasCapacityData = tasks.Any(task => task.OriginalEstimateHours > 0 || task.TimeSpentHours > 0);
        metrics.Risks = BuildDerivedRisks(tasks, metrics.ReportSnapshotDate);
        metrics.HasRiskData = metrics.Risks.Count > 0;
        metrics.OpenRiskCount = metrics.Risks.Count;
        metrics.HighRiskCount = metrics.Risks.Count(risk => risk.Score >= 6);
        metrics.DistinctSprintCount = distinctSprints.Count;
        ComputeFlowAndConcentration(metrics, tasks);
        FinalizeHealthScore(metrics);
        return metrics;
    }

    private static void ComputeFlowAndConcentration(SprintMetrics metrics, List<SprintTask> tasks)
    {
        // Workflow distribution: separate active work from work that never started.
        metrics.InProgressTasks = tasks.Count(task => !task.IsDone
            && (task.Status.Contains("progress", StringComparison.OrdinalIgnoreCase)
                || task.Status.Contains("review", StringComparison.OrdinalIgnoreCase)));
        metrics.CarryOverTasks = Math.Max(0, metrics.TotalTasks - metrics.CompletedTasks);
        metrics.NotStartedTasks = Math.Max(0, metrics.TotalTasks - metrics.CompletedTasks - metrics.InProgressTasks);

        // Contributor concentration: how much of delivered work sits with one person.
        var namedContributors = metrics.WorkloadByAssignee
            .Where(member => !member.Assignee.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var top = namedContributors
            .OrderByDescending(member => member.CompletedTasks)
            .FirstOrDefault();
        if (top is not null && metrics.CompletedTasks > 0 && top.CompletedTasks > 0)
        {
            metrics.TopContributor = top.Assignee;
            metrics.TopContributorCompleted = top.CompletedTasks;
            metrics.TopContributorSharePercent = Math.Round(top.CompletedTasks * 100.0 / metrics.CompletedTasks, 1);
        }

        // Quality depth.
        metrics.DefectDensityPercent = Percentage(metrics.BugCount, metrics.TotalTasks);
        var teamSize = Math.Max(1, metrics.WorkloadByAssignee.Count);
        metrics.BugsPerContributor = Math.Round(metrics.BugCount / (double)teamSize, 1);

        // Cycle time from start-to-completion when both dates are present.
        var cycleTimes = tasks
            .Where(task => task.IsDone && task.StartDate.HasValue && task.EndDate.HasValue)
            .Select(task => (task.EndDate!.Value.Date - task.StartDate!.Value.Date).TotalDays)
            .Where(days => days >= 0)
            .ToList();
        if (cycleTimes.Count > 0)
        {
            metrics.AverageCycleTimeDays = Math.Round(cycleTimes.Average(), 1);
            metrics.HasCycleTimeData = true;
        }
    }

    private static async Task<List<IReadOnlyDictionary<string, string>>> ReadCsvRowsAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string[]? headers = null;
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var record = new StringBuilder();
        string? line;

        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record.Length > 0)
            {
                record.Append('\n');
            }
            record.Append(line);

            if (record.Length > 1_000_000)
            {
                throw new InvalidDataException("A CSV record exceeds the supported 1 MB limit.");
            }

            if (!IsCompleteCsvRecord(record))
            {
                continue;
            }

            var values = ParseCsvLine(record.ToString());
            record.Clear();
            if (headers is null)
            {
                headers = values.Select(value => value.Trim().Trim('"')).ToArray();
                if (headers.Length == 0 || headers.All(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidDataException("CSV file appears to be empty or missing a header row.");
                }
                continue;
            }

            if (values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < headers.Length; index++)
            {
                row[headers[index]] = index < values.Length ? values[index].Trim() : string.Empty;
            }
            rows.Add(row);
            if (rows.Count > MaximumTaskCount)
            {
                throw new InvalidDataException($"The data contains more than the supported maximum of {MaximumTaskCount:N0} issues.");
            }
        }

        if (record.Length > 0)
        {
            throw new InvalidDataException("The CSV contains an unterminated quoted field.");
        }
        if (headers is null)
        {
            throw new InvalidDataException("CSV file appears to be empty or missing a header row.");
        }

        return rows;
    }

    private static bool IsCompleteCsvRecord(StringBuilder record)
    {
        var inQuotes = false;
        for (var index = 0; index < record.Length; index++)
        {
            if (record[index] != '"')
            {
                continue;
            }

            if (inQuotes && index + 1 < record.Length && record[index + 1] == '"')
            {
                index++;
                continue;
            }

            inQuotes = !inQuotes;
        }

        return !inQuotes;
    }

    private static List<SprintTask> ParseTaskRows(
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        if (rows.Count == 0)
        {
            throw new InvalidDataException("No issue rows were found in the uploaded data.");
        }

        var normalizedHeaders = rows[0].Keys.Select(NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var required = new Dictionary<string, string[]>
        {
            ["TaskId"] = ["taskid", "id", "key", "issuekey", "issueid"],
            ["Title"] = ["title", "summary", "taskname", "name"],
            ["Status"] = ["status", "state"],
            ["Assignee"] = ["assignee", "owner", "assignedto"]
        };
        var missing = required
            .Where(requirement => !requirement.Value.Any(normalizedHeaders.Contains))
            .Select(requirement => requirement.Key)
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidDataException($"Issue data is missing required columns: {string.Join(", ", missing)}.");
        }

        return rows
            .Select(ParseSprintTask)
            .Where(task => !string.IsNullOrWhiteSpace(task.TaskId) || !string.IsNullOrWhiteSpace(task.Title))
            .ToList();
    }

    private static SprintTask ParseSprintTask(IReadOnlyDictionary<string, string> row)
    {
        return new SprintTask
        {
            TaskId = Get(row, "taskid", "id", "key", "issuekey", "issueid"),
            Title = Get(row, "title", "summary", "taskname", "name"),
            Assignee = Get(row, "assignee", "owner", "assignedto"),
            Status = Get(row, "status", "state"),
            Type = Get(row, "type", "issuetype", "workitemtype"),
            Priority = Get(row, "priority", "severity"),
            StoryPoints = ParseNumber(Get(row, "storypoints", "storypointestimate", "customfieldstorypoints", "customfieldstorypointestimate", "points", "estimate", "plannedstorypoints")),
            SprintName = Get(row, "sprintname", "sprint"),
            Resolution = Get(row, "resolution"),
            Team = Get(row, "team", "teamname"),
            Component = Get(row, "component", "components", "project"),
            Labels = Get(row, "labels", "label", "tags"),
            StartDate = ParseDate(Get(row, "startdate", "created", "createddate")),
            UpdatedDate = ParseDate(Get(row, "updated", "updateddate", "lastupdated")),
            EndDate = ParseDate(Get(row, "enddate", "resolved", "completeddate", "resolutiondate")),
            DueDate = ParseDate(Get(row, "duedate", "due")),
            BlockedFlag = ParseBlockedFlag(Get(row, "blocked", "blockedflag", "isblocked", "flagged", "impediment")),
            OriginalEstimateHours = ParseDurationHours(Get(row, "originalestimate", "estimatehours", "estimatedhours")),
            TimeSpentHours = ParseDurationHours(Get(row, "timespent", "workedhours", "actualhours"))
        };
    }

    private static void ApplyWorkbookMetrics(
        SprintMetrics metrics,
        IReadOnlyList<WorkbookSheet> sheets)
    {
        var summary = FindSheet(sheets, "sprintsummary", "summary");
        if (summary is not null)
        {
            var velocity = new List<MetricPoint>();
            foreach (var row in summary.Rows)
            {
                var label = Get(row, "sprint", "sprintname", "name");
                var hasPlanned = TryGetNumber(row, out var planned, "plannedstorypoints", "plannedpoints", "planned");
                var hasCompleted = TryGetNumber(row, out var completed, "completedstorypoints", "completedpoints", "velocity", "completed");
                if (!string.IsNullOrWhiteSpace(label) && (hasPlanned || hasCompleted))
                {
                    velocity.Add(new MetricPoint
                    {
                        Label = label,
                        Value = completed,
                        ComparisonValue = hasPlanned ? planned : null
                    });
                }
            }

            var selectedRow = summary.Rows.LastOrDefault(row =>
                    Get(row, "sprint", "sprintname", "name").Equals(metrics.SprintName, StringComparison.OrdinalIgnoreCase))
                ?? summary.Rows.Last();
            if (TryGetNumber(selectedRow, out var selectedPlanned, "plannedstorypoints", "plannedpoints", "planned"))
            {
                metrics.PlannedWork = selectedPlanned;
            }
            if (TryGetNumber(selectedRow, out var selectedCompleted, "completedstorypoints", "completedpoints", "velocity", "completed"))
            {
                metrics.CompletedWork = selectedCompleted;
            }
            if (TryGetPercentage(selectedRow, out var parsedHealth, "sprinthealth", "healthscore", "sprinthealthscore"))
            {
                metrics.SprintHealthScore = parsedHealth;
                metrics.HasSprintHealthScore = true;
            }
            if (TryGetPercentage(selectedRow, out var parsedCapacity, "capacityutilization", "capacity", "utilization"))
            {
                metrics.CapacityUtilizationPercent = parsedCapacity;
                metrics.HasCapacityData = true;
            }
            if (TryGetPercentage(selectedRow, out var parsedScopeChange, "scopechange", "scopecreep", "scopechangepercent"))
            {
                metrics.ScopeChangePercent = parsedScopeChange;
                metrics.HasScopeData = true;
            }
            if (TryGetNumber(selectedRow, out var added, "scopeadded", "addeditems", "addedissues"))
            {
                metrics.ScopeAddedItems = (int)Math.Round(added);
                metrics.HasScopeData = true;
            }

            if (metrics.HasScopeData)
            {
                var finalScope = Math.Max(0, 100 + metrics.ScopeChangePercent);
                metrics.ScopeTrend =
                [
                    new MetricPoint { Label = "Committed", Value = 100 },
                    new MetricPoint { Label = "Final", Value = finalScope }
                ];
            }

            if (velocity.Count > 0)
            {
                metrics.VelocityTrend = velocity.TakeLast(12).ToList();
                metrics.UsesWorkItemProxy = false;
                metrics.WorkUnitLabel = "Story points";
            }
        }

        var burndown = FindSheet(sheets, "burndown", "burndownchart");
        if (burndown is not null)
        {
            var trend = burndown.Rows.Select(row => new MetricPoint
            {
                Label = FormatDateLabel(Get(row, "date", "day", "sprintday")),
                Value = ParseNumber(Get(row, "remainingstorypoints", "actualremaining", "remaining", "actual")),
                ComparisonValue = ParseNullableNumber(Get(row, "idealremaining", "idealburndown", "ideal"))
            }).Where(point => !string.IsNullOrWhiteSpace(point.Label)).ToList();
            if (trend.Count > 0)
            {
                metrics.BurndownTrend = trend.TakeLast(20).ToList();
                metrics.HasBurndownData = true;
            }
        }

        var capacity = FindSheet(sheets, "capacity", "teamcapacity");
        if (capacity is not null)
        {
            var availability = capacity.Rows.Sum(row => ParseNumber(Get(row, "developeravailability", "availablehours", "availability", "capacityhours")));
            var leave = capacity.Rows.Sum(row => ParseNumber(Get(row, "leavehours", "leave")));
            var worked = capacity.Rows.Sum(row => ParseNumber(Get(row, "workedhours", "actualhours", "timespent")));
            var net = Math.Max(0, availability - leave);
            if (net > 0)
            {
                metrics.CapacityUtilizationPercent = Math.Round(Math.Clamp(worked * 100 / net, 0, 200), 1);
                metrics.HasCapacityData = true;
            }
        }

        var quality = FindSheet(sheets, "quality", "qualitymetrics");
        if (quality is not null)
        {
            metrics.HasQualityData = true;
            if (HasColumn(quality, "severity", "bugseverity", "priority"))
            {
                metrics.CriticalBugs = quality.Rows
                    .Where(row => IsCategory(Get(row, "severity", "bugseverity", "priority"), "critical", "blocker", "highest"))
                    .Sum(GetRowCount);
                metrics.MajorBugs = quality.Rows
                    .Where(row => IsCategory(Get(row, "severity", "bugseverity", "priority"), "major", "high"))
                    .Sum(GetRowCount);
                metrics.MinorBugs = quality.Rows
                    .Where(row => IsCategory(Get(row, "severity", "bugseverity", "priority"), "minor", "medium", "low", "lowest"))
                    .Sum(GetRowCount);
            }
            else
            {
                metrics.CriticalBugs = SumInt(quality, "criticalbugs", "critical");
                metrics.MajorBugs = SumInt(quality, "majorbugs", "major");
                metrics.MinorBugs = SumInt(quality, "minorbugs", "minor");
            }
            metrics.BugCount = metrics.CriticalBugs + metrics.MajorBugs + metrics.MinorBugs;
            metrics.TechnicalDebtItems = SumInt(quality, "technicaldebt", "technicaldebtitems", "debtitems");
            metrics.SonarIssues = SumInt(quality, "sonarissues", "sonar");
            var coverageValues = quality.Rows
                .Where(row => !string.IsNullOrWhiteSpace(Get(row, "codecoverage", "coverage", "codecoveragepercent")))
                .Select(row => ParsePercentage(Get(row, "codecoverage", "coverage", "codecoveragepercent")))
                .ToList();
            if (coverageValues.Count > 0) metrics.CodeCoveragePercent = Math.Round(coverageValues.Average(), 1);
        }

        var ciCd = FindSheet(sheets, "cicd", "builds", "deployments", "cicdmetrics");
        if (ciCd is not null)
        {
            metrics.HasCiCdData = true;
            if (HasColumn(ciCd, "buildstatus", "status", "result"))
            {
                metrics.BuildSuccessCount = ciCd.Rows
                    .Where(row => IsCategory(Get(row, "buildstatus", "status", "result"), "success", "succeeded", "passed"))
                    .Sum(GetRowCount);
                metrics.BuildFailureCount = ciCd.Rows
                    .Where(row => IsCategory(Get(row, "buildstatus", "status", "result"), "failure", "failed", "error"))
                    .Sum(GetRowCount);
            }
            else
            {
                metrics.BuildSuccessCount = SumInt(ciCd, "successcount", "successfulbuilds", "buildsuccesses", "success");
                metrics.BuildFailureCount = SumInt(ciCd, "failurecount", "failedbuilds", "buildfailures", "failure");
            }
            metrics.DeploymentCount = SumInt(ciCd, "deployments", "deploymentcount");
            if (metrics.DeploymentCount == 0 && HasColumn(ciCd, "deploymentstatus", "environment", "deploymentid"))
            {
                metrics.DeploymentCount = ciCd.Rows.Count;
            }
            var durations = ciCd.Rows
                .Select(row => ParseNumber(Get(row, "deploymentduration", "durationminutes", "deploymentdurationminutes")))
                .Where(value => value > 0)
                .ToList();
            if (durations.Count > 0) metrics.AverageDeploymentDurationMinutes = Math.Round(durations.Average(), 1);
            metrics.BuildSuccessRatePercent = Percentage(
                metrics.BuildSuccessCount,
                metrics.BuildSuccessCount + metrics.BuildFailureCount);
        }

        var risks = FindSheet(sheets, "risks", "sprintrisks", "riskregister");
        if (risks is not null)
        {
            metrics.HasRiskData = true;
            metrics.Risks = risks.Rows.Select(row =>
            {
                var probability = Get(row, "probability", "likelihood");
                var impact = Get(row, "impact", "severity");
                return new RiskMetric
                {
                    Name = Get(row, "risk", "riskname", "description", "summary"),
                    Probability = probability,
                    Impact = impact,
                    MitigationStatus = Get(row, "mitigationstatus", "status", "mitigation"),
                    Score = RiskValue(probability) * RiskValue(impact)
                };
            }).Where(risk => !string.IsNullOrWhiteSpace(risk.Name)).ToList();
            metrics.OpenRiskCount = metrics.Risks.Count(risk => !IsClosedRisk(risk.MitigationStatus));
            metrics.HighRiskCount = metrics.Risks.Count(risk => !IsClosedRisk(risk.MitigationStatus) && risk.Score >= 6);
        }
    }

    private static List<MetricPoint> BuildVelocityTrend(List<SprintTask> tasks, bool hasStoryPoints)
    {
        var grouped = tasks
            .GroupBy(task => NormalizeCategory(task.SprintName, "Current Sprint"), StringComparer.OrdinalIgnoreCase)
            .Select(group => new MetricPoint
            {
                Label = group.Key,
                Value = hasStoryPoints ? group.Where(task => task.IsDone).Sum(task => task.StoryPoints) : group.Count(task => task.IsDone),
                ComparisonValue = hasStoryPoints ? group.Sum(task => task.StoryPoints) : group.Count()
            })
            .TakeLast(12)
            .ToList();
        return grouped.Count > 0 ? grouped : [new MetricPoint { Label = "Current Sprint", Value = 0, ComparisonValue = tasks.Count }];
    }

    private static List<MetricPoint> BuildBurndownTrend(List<SprintTask> tasks, bool hasStoryPoints)
    {
        var dated = tasks.Where(task => task.StartDate.HasValue).ToList();
        if (dated.Count == 0)
        {
            return [
                new MetricPoint { Label = "Start", Value = hasStoryPoints ? tasks.Sum(task => task.StoryPoints) : tasks.Count, ComparisonValue = hasStoryPoints ? tasks.Sum(task => task.StoryPoints) : tasks.Count },
                new MetricPoint { Label = "Current", Value = hasStoryPoints ? tasks.Where(task => !task.IsDone).Sum(task => task.StoryPoints) : tasks.Count(task => !task.IsDone), ComparisonValue = 0 }
            ];
        }

        var start = dated.Min(task => task.StartDate!.Value.Date);
        var end = tasks.SelectMany(task => new[] { task.DueDate, task.UpdatedDate, task.EndDate })
            .Where(date => date.HasValue).Select(date => date!.Value.Date).DefaultIfEmpty(start.AddDays(13)).Max();
        if (end <= start) end = start.AddDays(1);
        var total = hasStoryPoints ? tasks.Sum(task => task.StoryPoints) : tasks.Count;
        var points = new List<MetricPoint>();
        const int intervals = 6;
        for (var index = 0; index < intervals; index++)
        {
            var date = start.AddDays((end - start).TotalDays * index / (intervals - 1));
            var completed = tasks.Where(task => task.IsDone && (task.EndDate ?? task.UpdatedDate ?? end) <= date)
                .Sum(task => hasStoryPoints ? task.StoryPoints : 1);
            points.Add(new MetricPoint
            {
                Label = date.ToString("MMM d", CultureInfo.InvariantCulture),
                Value = Math.Max(0, total - completed),
                ComparisonValue = total * (intervals - 1 - index) / (intervals - 1d)
            });
        }

        return points;
    }

    private static double CalculateCapacityUtilization(List<SprintTask> tasks)
    {
        var estimate = tasks.Sum(task => task.OriginalEstimateHours);
        var spent = tasks.Sum(task => task.TimeSpentHours);
        return estimate > 0 ? Math.Round(Math.Clamp(spent * 100 / estimate, 0, 200), 1) : 0;
    }

    private static List<RiskMetric> BuildDerivedRisks(List<SprintTask> tasks, DateTime? snapshot)
    {
        var result = new List<RiskMetric>();
        var blocked = tasks.Count(task => task.IsBlocked && !task.IsDone);
        if (blocked > 0)
        {
            result.Add(new RiskMetric { Name = $"{blocked} blocked work item(s)", Probability = "High", Impact = "High", MitigationStatus = "Open", Score = 9 });
        }

        if (snapshot.HasValue)
        {
            var overdue = tasks.Count(task => !task.IsDone && task.DueDate.HasValue && task.DueDate.Value.Date < snapshot.Value.Date);
            if (overdue > 0)
            {
                result.Add(new RiskMetric { Name = $"{overdue} overdue unresolved item(s)", Probability = "High", Impact = "Medium", MitigationStatus = "Open", Score = 6 });
            }
        }

        var highestOpen = tasks.Count(task => !task.IsDone && IsPriority(task, "highest", "critical", "blocker"));
        if (highestOpen > 0)
        {
            result.Add(new RiskMetric { Name = $"{highestOpen} highest-priority item(s) remain open", Probability = "Medium", Impact = "High", MitigationStatus = "Open", Score = 6 });
        }

        return result;
    }

    private static void FinalizeHealthScore(SprintMetrics metrics)
    {
        if (metrics.HasSprintHealthScore)
        {
            // The score was supplied by the source workbook. Record an honest, minimal
            // breakdown so the slide still explains where the number came from.
            metrics.HealthBreakdown =
            [
                new HealthComponent
                {
                    Label = "Source workbook score",
                    Points = metrics.SprintHealthScore,
                    Detail = "Taken directly from the SprintSummary sheet"
                },
                new HealthComponent
                {
                    Label = "Overall health",
                    Points = metrics.SprintHealthScore,
                    IsTotal = true,
                    Detail = "0-100"
                }
            ];
            return;
        }

        const double baseline = 20;
        var completion = Math.Round(metrics.CompletionRatePercent, 1);
        var blockedPenalty = Math.Min(25, metrics.BlockedTasks * 5);
        var riskPenalty = Math.Min(20, metrics.HighRiskCount * 5);
        var qualityPenalty = Math.Min(15, metrics.CriticalBugs * 3 + metrics.MajorBugs);
        var workloadPenalty = metrics.TopContributorSharePercent switch
        {
            >= 50 => 12,
            >= 35 => 6,
            _ => 0
        };

        var raw = baseline + completion - blockedPenalty - riskPenalty - qualityPenalty - workloadPenalty;
        metrics.SprintHealthScore = Math.Round(Math.Clamp(raw, 0, 100), 1);

        // Transparent, defensible breakdown so stakeholders can see how the score was derived.
        metrics.HealthBreakdown =
        [
            new HealthComponent { Label = "Baseline", Points = baseline, Detail = "Starting allowance" },
            new HealthComponent { Label = "Completion rate", Points = completion, Detail = $"{metrics.CompletedTasks}/{metrics.TotalTasks} issues done" },
            new HealthComponent { Label = "Blocked tasks", Points = -blockedPenalty, Detail = $"{metrics.BlockedTasks} blocked (-5 each, max -25)" },
            new HealthComponent { Label = "High risks", Points = -riskPenalty, Detail = $"{metrics.HighRiskCount} high-scoring risk(s) (-5 each, max -20)" },
            new HealthComponent { Label = "Bug severity", Points = -qualityPenalty, Detail = $"{metrics.CriticalBugs} critical, {metrics.MajorBugs} major (max -15)" },
            new HealthComponent { Label = "Workload balance", Points = -workloadPenalty, Detail = metrics.TopContributorSharePercent > 0 ? $"top contributor delivered {metrics.TopContributorSharePercent:F0}%" : "even distribution" },
            new HealthComponent { Label = "Overall health", Points = metrics.SprintHealthScore, IsTotal = true, Detail = "clamped to 0-100" }
        ];
    }

    private static WorkbookSheet? FindIssueSheet(IReadOnlyList<WorkbookSheet> sheets)
    {
        return FindSheet(sheets, "issues", "jiraissues", "workitems")
            ?? sheets.FirstOrDefault(sheet =>
            {
                var headers = sheet.Rows.FirstOrDefault()?.Keys.Select(NormalizeHeader).ToHashSet() ?? [];
                return headers.Contains("status")
                    && (headers.Contains("issuekey") || headers.Contains("taskid") || headers.Contains("key"))
                    && (headers.Contains("summary") || headers.Contains("title"));
            });
    }

    private static WorkbookSheet? FindSheet(IReadOnlyList<WorkbookSheet> sheets, params string[] aliases)
    {
        return sheets.FirstOrDefault(sheet => aliases.Contains(NormalizeHeader(sheet.Name), StringComparer.OrdinalIgnoreCase));
    }

    private static Dictionary<string, int> GroupCounts(List<SprintTask> tasks, Func<SprintTask, string> selector)
    {
        return tasks.GroupBy(task => NormalizeCategory(selector(task), "Unspecified"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString().Trim());
        return values.ToArray();
    }

    private static bool TryGetNumber(
        IReadOnlyDictionary<string, string> row,
        out double value,
        params string[] aliases)
    {
        var raw = Get(row, aliases);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = 0;
            return false;
        }

        var normalized = raw.Replace("%", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetPercentage(
        IReadOnlyDictionary<string, string> row,
        out double value,
        params string[] aliases)
    {
        var raw = Get(row, aliases);
        if (!TryGetNumber(row, out value, aliases))
        {
            return false;
        }

        if (!raw.Contains('%') && value is >= 0 and <= 1)
        {
            value *= 100;
        }
        value = Math.Round(value, 1);
        return true;
    }

    private static string Get(IReadOnlyDictionary<string, string> row, params string[] aliases)
    {
        foreach (var pair in row)
        {
            var normalized = NormalizeHeader(pair.Key);
            if (aliases.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                return pair.Value?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string NormalizeHeader(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string NormalizeCategory(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static double ParseNumber(string value)
    {
        var normalized = value.Replace("%", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal).Trim();
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static double? ParseNullableNumber(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ParseNumber(value);
    }

    private static double ParsePercentage(string value)
    {
        var parsed = ParseNumber(value);
        return !value.Contains('%') && parsed is > 0 and <= 1 ? parsed * 100 : parsed;
    }

    private static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date)) return date;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 1 and < 2_958_466)
        {
            try { return DateTime.FromOADate(serial); } catch (ArgumentException) { return null; }
        }

        return null;
    }

    private static string FormatDateLabel(string value)
    {
        return ParseDate(value)?.ToString("MMM d", CultureInfo.InvariantCulture) ?? value.Trim();
    }

    private static bool ParseBoolean(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
        || value.Equals("y", StringComparison.OrdinalIgnoreCase)
        || value == "1";

    private static bool ParseBlockedFlag(string value) =>
        ParseBoolean(value)
        || value.Contains("impediment", StringComparison.OrdinalIgnoreCase)
        || value.Contains("blocked", StringComparison.OrdinalIgnoreCase);

    private static double ParseDurationHours(string value)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hours)) return hours;
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration)) return duration.TotalHours;
        return 0;
    }

    private static bool IsZipPackage(Stream stream)
    {
        var original = stream.Position;
        Span<byte> header = stackalloc byte[4];
        var read = stream.Read(header);
        stream.Position = original;
        return read == 4 && header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
    }

    private static void ValidateTaskCount(List<SprintTask> tasks)
    {
        if (tasks.Count == 0) throw new InvalidDataException("No valid issue rows could be parsed from the uploaded data.");
        if (tasks.Count > MaximumTaskCount)
        {
            throw new InvalidDataException($"The data contains more than the supported maximum of {MaximumTaskCount:N0} issues.");
        }
    }

    private static bool HasColumn(WorkbookSheet sheet, params string[] aliases)
    {
        var normalizedAliases = aliases.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return sheet.Rows.FirstOrDefault()?.Keys.Any(key => normalizedAliases.Contains(NormalizeHeader(key))) == true;
    }

    private static int GetRowCount(IReadOnlyDictionary<string, string> row)
    {
        var count = ParseNumber(Get(row, "count", "bugcount", "buildcount", "total"));
        return count > 0 ? (int)Math.Round(count) : 1;
    }

    private static bool IsCategory(string value, params string[] categories) =>
        categories.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    private static bool IsBug(SprintTask task) => task.Type.Equals("Bug", StringComparison.OrdinalIgnoreCase) || task.Type.Contains("defect", StringComparison.OrdinalIgnoreCase);
    private static bool IsPriority(SprintTask task, params string[] values) => values.Contains(task.Priority.Trim(), StringComparer.OrdinalIgnoreCase);
    private static bool ContainsToken(string value, params string[] tokens) => tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    private static double Percentage(double numerator, double denominator) => denominator <= 0 ? 0 : Math.Round(numerator * 100 / denominator, 1);
    private static double RiskValue(string value) => value.Trim().ToLowerInvariant() switch { "high" or "critical" => 3, "medium" or "moderate" => 2, "low" => 1, _ => 1 };
    private static bool IsClosedRisk(string value) => value.Contains("closed", StringComparison.OrdinalIgnoreCase) || value.Contains("mitigated", StringComparison.OrdinalIgnoreCase) || value.Contains("resolved", StringComparison.OrdinalIgnoreCase);
    private static int SumInt(WorkbookSheet sheet, params string[] aliases) => (int)Math.Round(sheet.Rows.Sum(row => ParseNumber(Get(row, aliases))));
}
