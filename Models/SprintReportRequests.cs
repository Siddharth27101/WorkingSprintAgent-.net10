using System.ComponentModel.DataAnnotations;

namespace WorkingSprintAgent.Models;

/// <summary>
/// Multipart form used to generate a sprint presentation from CSV data.
/// </summary>
public sealed class GenerateSprintReportRequest
{
    /// <summary>
    /// CSV file containing TaskId, Title, Status, and Assignee columns.
    /// </summary>
    [Required]
    public IFormFile CsvFile { get; set; } = null!;

    /// <summary>
    /// Optional name shown in the generated presentation.
    /// </summary>
    [StringLength(150)]
    public string? SprintName { get; set; }

    /// <summary>
    /// Output type. Use powerpoint to download a .pptx file or html for an HTML presentation.
    /// </summary>
    [RegularExpression("(?i)^(powerpoint|html)$", ErrorMessage = "OutputFormat must be 'powerpoint' or 'html'.")]
    public string OutputFormat { get; set; } = "powerpoint";

    /// <summary>
    /// Presentation style: professional, modern, corporate, or minimal.
    /// </summary>
    [RegularExpression("(?i)^(professional|modern|corporate|minimal)$", ErrorMessage = "Template must be professional, modern, corporate, or minimal.")]
    public string Template { get; set; } = "professional";

    /// <summary>
    /// Optional company name displayed on the title slide.
    /// </summary>
    [StringLength(150)]
    public string? CompanyName { get; set; }
}

/// <summary>
/// Multipart form used to preview sprint metrics and AI insights without creating a file.
/// </summary>
public sealed class PreviewSprintDataRequest
{
    /// <summary>
    /// CSV file containing TaskId, Title, Status, and Assignee columns.
    /// </summary>
    [Required]
    public IFormFile CsvFile { get; set; } = null!;

    /// <summary>
    /// Optional sprint name used in the preview.
    /// </summary>
    [StringLength(150)]
    public string? SprintName { get; set; }

    /// <summary>
    /// Include token and cost optimization analysis in the JSON response.
    /// </summary>
    public bool IncludeOptimization { get; set; }
}
