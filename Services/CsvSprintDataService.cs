using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

public class CsvSprintDataService : ICsvSprintDataService
{
    private readonly ILogger<CsvSprintDataService> _logger;

    public CsvSprintDataService(ILogger<CsvSprintDataService> logger)
    {
        _logger = logger;
    }

    public async Task<List<SprintTask>> ParseAsync(Stream csvStream)
    {
        var records = new List<SprintTask>();
        using var reader = new StreamReader(csvStream);
        
        var headerLine = await reader.ReadLineAsync();
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException("CSV file appears to be empty or missing header row.");
        }

        var headers = ParseCsvLine(headerLine);
        var headerMap = CreateHeaderMap(headers);

        string? line;
        int lineNumber = 2;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var values = ParseCsvLine(line);
                var task = ParseSprintTask(values, headerMap);
                if (task is not null) records.Add(task);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse line {LineNumber}: {Line}", lineNumber, line);
            }
            lineNumber++;
        }

        _logger.LogInformation("Parsed {Count} sprint task rows from CSV", records.Count);

        if (records.Count == 0)
        {
            throw new InvalidOperationException("No valid rows could be parsed from the CSV.");
        }

        return records;
    }

    public SprintMetrics ComputeMetrics(List<SprintTask> tasks, string? sprintNameOverride = null)
    {
        var sprintName = sprintNameOverride
            ?? tasks.Select(t => t.SprintName).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s))
            ?? "Current Sprint";

        var metrics = new SprintMetrics
        {
            SprintName = sprintName,
            TotalTasks = tasks.Count,
            CompletedTasks = tasks.Count(t => t.IsDone),
            BlockedTasks = tasks.Count(t => t.IsBlocked),
            TotalStoryPoints = tasks.Sum(t => t.StoryPoints),
            CompletedStoryPoints = tasks.Where(t => t.IsDone).Sum(t => t.StoryPoints),
        };

        metrics.CompletionRatePercent = metrics.TotalTasks == 0
            ? 0
            : Math.Round(metrics.CompletedTasks * 100.0 / metrics.TotalTasks, 1);

        metrics.TasksByStatus = tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Status) ? "Unspecified" : t.Status.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        metrics.TasksByType = tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Type) ? "Unspecified" : t.Type.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        metrics.TasksByPriority = tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Priority) ? "Unspecified" : t.Priority.Trim())
            .ToDictionary(g => g.Key, g => g.Count());

        metrics.WorkloadByAssignee = tasks
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Assignee) ? "Unassigned" : t.Assignee.Trim())
            .Select(g => new AssigneeLoad
            {
                Assignee = g.Key,
                TotalTasks = g.Count(),
                CompletedTasks = g.Count(t => t.IsDone),
                StoryPoints = g.Sum(t => t.StoryPoints)
            })
            .OrderByDescending(a => a.TotalTasks)
            .ToList();

        metrics.BlockedTaskTitles = tasks
            .Where(t => t.IsBlocked)
            .Select(t => string.IsNullOrWhiteSpace(t.TaskId) 
                ? t.Title.Trim() 
                : $"{t.TaskId}: {t.Title}".Trim(':', ' '))
            .ToList();

        return metrics;
    }

    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new List<char>();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Add('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(new string(current.ToArray()).Trim());
                current.Clear();
            }
            else
            {
                current.Add(c);
            }
        }

        values.Add(new string(current.ToArray()).Trim());
        return values.ToArray();
    }

    private static Dictionary<string, int> CreateHeaderMap(string[] headers)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        
        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i].Trim().Trim('"');
            var normalized = header.ToLowerInvariant().Replace(" ", "").Replace("_", "");
            
            if (IsHeaderMatch(normalized, "taskid", "id", "key", "issuekey"))
                map["TaskId"] = i;
            else if (IsHeaderMatch(normalized, "title", "summary", "taskname", "name"))
                map["Title"] = i;
            else if (IsHeaderMatch(normalized, "assignee", "owner", "assignedto"))
                map["Assignee"] = i;
            else if (IsHeaderMatch(normalized, "status", "state"))
                map["Status"] = i;
            else if (IsHeaderMatch(normalized, "type", "issuetype", "workitemtype"))
                map["Type"] = i;
            else if (IsHeaderMatch(normalized, "priority"))
                map["Priority"] = i;
            else if (IsHeaderMatch(normalized, "storypoints", "points", "estimate"))
                map["StoryPoints"] = i;
            else if (IsHeaderMatch(normalized, "sprintname", "sprint"))
                map["SprintName"] = i;
            else if (IsHeaderMatch(normalized, "startdate", "created"))
                map["StartDate"] = i;
            else if (IsHeaderMatch(normalized, "enddate", "resolved", "completeddate"))
                map["EndDate"] = i;
        }

        return map;
    }

    private static bool IsHeaderMatch(string normalized, params string[] matches)
    {
        return matches.Any(m => normalized.Contains(m));
    }

    private static SprintTask? ParseSprintTask(string[] values, Dictionary<string, int> headerMap)
    {
        var task = new SprintTask();

        if (headerMap.TryGetValue("TaskId", out var idx) && idx < values.Length)
            task.TaskId = values[idx].Trim('"');
        
        if (headerMap.TryGetValue("Title", out idx) && idx < values.Length)
            task.Title = values[idx].Trim('"');
        
        if (headerMap.TryGetValue("Assignee", out idx) && idx < values.Length)
            task.Assignee = values[idx].Trim('"');
        
        if (headerMap.TryGetValue("Status", out idx) && idx < values.Length)
            task.Status = values[idx].Trim('"');
        
        if (headerMap.TryGetValue("Type", out idx) && idx < values.Length)
            task.Type = values[idx].Trim('"');
        
        if (headerMap.TryGetValue("Priority", out idx) && idx < values.Length)
            task.Priority = values[idx].Trim('"');
        
        if (headerMap.TryGetValue("StoryPoints", out idx) && idx < values.Length)
            if (double.TryParse(values[idx].Trim('"'), out var points))
                task.StoryPoints = points;
        
        if (headerMap.TryGetValue("SprintName", out idx) && idx < values.Length)
            task.SprintName = values[idx].Trim('"');
        
        if (headerMap.TryGetValue("StartDate", out idx) && idx < values.Length)
            if (DateTime.TryParse(values[idx].Trim('"'), out var startDate))
                task.StartDate = startDate;
        
        if (headerMap.TryGetValue("EndDate", out idx) && idx < values.Length)
            if (DateTime.TryParse(values[idx].Trim('"'), out var endDate))
                task.EndDate = endDate;

        return !string.IsNullOrWhiteSpace(task.TaskId) || !string.IsNullOrWhiteSpace(task.Title) 
            ? task : null;
    }
}