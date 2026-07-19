using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Agents;

/// <summary>
/// File-upload workflow stage backed by the CSV parsing service.
/// </summary>
public sealed class FileUploadAgent : IFileUploadAgent
{
    private readonly ICsvSprintDataService _csvService;
    private readonly ILogger<FileUploadAgent> _logger;

    public FileUploadAgent(
        ICsvSprintDataService csvService,
        ILogger<FileUploadAgent> logger)
    {
        _csvService = csvService;
        _logger = logger;
    }

    public async Task<SprintDataSet> ProcessAsync(
        Stream csvStream,
        string? sprintName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation("File Upload Agent started CSV processing");
        var tasks = await _csvService.ParseAsync(csvStream, cancellationToken);
        var metrics = _csvService.ComputeMetrics(tasks, sprintName);

        _logger.LogInformation(
            "File Upload Agent completed with {TaskCount} tasks for sprint '{SprintName}'",
            tasks.Count,
            metrics.SprintName);

        return new SprintDataSet(tasks, metrics);
    }
}
