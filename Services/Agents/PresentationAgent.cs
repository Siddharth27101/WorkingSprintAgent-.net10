using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services.Agents;

/// <summary>
/// PPT Agent that turns analyzed sprint data into a downloadable presentation.
/// </summary>
public sealed class PresentationAgent : IPresentationAgent
{
    private const string PowerPointContentType =
        "application/vnd.openxmlformats-officedocument.presentationml.presentation";

    private readonly IPresentationBuilderService _presentationService;
    private readonly ILogger<PresentationAgent> _logger;

    public PresentationAgent(
        IPresentationBuilderService presentationService,
        ILogger<PresentationAgent> logger)
    {
        _presentationService = presentationService;
        _logger = logger;
    }

    public Task<PresentationArtifact> CreateAsync(
        SprintMetrics metrics,
        SprintInsights insights,
        SprintReportGenerationOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(insights);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "PPT Agent started for sprint '{SprintName}' with template '{Template}'",
            metrics.SprintName,
            options.Template);

        byte[] content;
        string contentType;
        string extension;

        if (options.OutputFormat == PresentationFormat.PowerPoint)
        {
            content = _presentationService.BuildPowerPointPresentation(
                metrics,
                insights,
                new PresentationOptions
                {
                    Template = options.Template,
                    CompanyName = options.CompanyName ?? string.Empty,
                    OutputFormat = PresentationFormat.PowerPoint,
                    IncludeCharts = true,
                    IncludeDetailedMetrics = true,
                    IncludeTeamBreakdown = metrics.TotalTasks <= 100,
                    IncludeRecommendations = true
                },
                cancellationToken);
            contentType = PowerPointContentType;
            extension = "pptx";
        }
        else
        {
            content = _presentationService.BuildPresentation(metrics, insights, cancellationToken);
            contentType = "text/html; charset=utf-8";
            extension = "html";
        }

        var fileName = $"Sprint_Report_{SanitizeFileName(metrics.SprintName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{extension}";
        var artifact = new PresentationArtifact(content, contentType, fileName);

        _logger.LogInformation(
            "PPT Agent completed '{FileName}' ({Size} bytes)",
            artifact.FileName,
            artifact.Content.Length);

        return Task.FromResult(artifact);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(character => !invalidChars.Contains(character))
            .ToArray())
            .Trim();

        return string.IsNullOrWhiteSpace(sanitized) ? "Sprint" : sanitized;
    }
}
