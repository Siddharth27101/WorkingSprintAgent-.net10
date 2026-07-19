using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Services;

/// <summary>
/// Service for building professional PowerPoint presentations from sprint data and AI insights
/// </summary>
public interface IPresentationBuilderService
{
    /// <summary>
    /// Build a professional PowerPoint presentation with charts and insights
    /// </summary>
    /// <param name="metrics">Sprint metrics data</param>
    /// <param name="insights">AI-generated insights</param>
    /// <param name="options">Presentation customization options</param>
    /// <param name="cancellationToken">Stops presentation generation when the request is cancelled.</param>
    /// <returns>PowerPoint file as byte array</returns>
    byte[] BuildPowerPointPresentation(
        SprintMetrics metrics,
        SprintInsights insights,
        PresentationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Build HTML presentation (fallback option)
    /// </summary>
    /// <param name="metrics">Sprint metrics data</param>
    /// <param name="insights">AI-generated insights</param>
    /// <param name="cancellationToken">Stops presentation generation when the request is cancelled.</param>
    /// <returns>HTML content as byte array</returns>
    byte[] BuildPresentation(
        SprintMetrics metrics,
        SprintInsights insights,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate presentation preview/summary
    /// </summary>
    /// <param name="metrics">Sprint metrics data</param>
    /// <param name="insights">AI-generated insights</param>
    /// <returns>Presentation summary information</returns>
    PresentationSummary GetPresentationSummary(SprintMetrics metrics, SprintInsights insights);

    /// <summary>
    /// Get available presentation templates
    /// </summary>
    /// <returns>List of available presentation templates</returns>
    List<PresentationTemplate> GetAvailableTemplates();
}

/// <summary>
/// Presentation customization options
/// </summary>
public class PresentationOptions
{
    public string Template { get; set; } = "Professional";
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyLogo { get; set; } = string.Empty;
    public bool IncludeCharts { get; set; } = true;
    public bool IncludeDetailedMetrics { get; set; } = true;
    public bool IncludeTeamBreakdown { get; set; } = true;
    public bool IncludeRecommendations { get; set; } = true;
    public string PrimaryColor { get; set; } = "#0078D4"; // Microsoft Blue
    public string SecondaryColor { get; set; } = "#106EBE";
    public string AccentColor { get; set; } = "#FFB900";
    public PresentationFormat OutputFormat { get; set; } = PresentationFormat.PowerPoint;
}

/// <summary>
/// Presentation output format
/// </summary>
public enum PresentationFormat
{
    PowerPoint,
    HTML,
    PDF
}

/// <summary>
/// Presentation summary information
/// </summary>
public class PresentationSummary
{
    public string Title { get; set; } = string.Empty;
    public int SlideCount { get; set; }
    public List<string> SlideTopics { get; set; } = new();
    public List<string> ChartTypes { get; set; } = new();
    public int EstimatedViewingTimeMinutes { get; set; }
    public DateTime GeneratedAt { get; set; }
    public string Template { get; set; } = string.Empty;
    public long EstimatedFileSizeBytes { get; set; }
}

/// <summary>
/// Presentation template information
/// </summary>
public class PresentationTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PreviewImage { get; set; } = string.Empty;
    public List<string> Features { get; set; } = new();
    public bool RequiresCompanyBranding { get; set; }
}