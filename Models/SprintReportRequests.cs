using System.ComponentModel.DataAnnotations;

namespace WorkingSprintAgent.Models;

/// <summary>
/// Multipart form used to generate a sprint presentation from CSV or Excel data.
/// </summary>
public sealed class GenerateSprintReportRequest
{
    /// <summary>
    /// Sprint CSV or Excel workbook containing an Issues table/sheet.
    /// </summary>
    [Required]
    public IFormFile CsvFile { get; set; } = null!;

    /// <summary>
    /// Optional name shown in the generated presentation.
    /// </summary>
    [StringLength(150)]
    public string? SprintName { get; set; }

    /// <summary>
    /// Presentation style. Rendered as a dropdown in Swagger.
    /// </summary>
    public PresentationStyle Template { get; set; } = PresentationStyle.Professional;

    /// <summary>
    /// Optional company name displayed on the title slide.
    /// </summary>
    [StringLength(150)]
    public string? CompanyName { get; set; }
}

/// <summary>
/// Available presentation styles for the generated PowerPoint deck.
/// </summary>
public enum PresentationStyle
{
    Professional,
    Modern,
    Corporate,
    Minimal
}

/// <summary>
/// Multipart form used to preview sprint metrics and AI insights without creating a file.
/// </summary>
public sealed class PreviewSprintDataRequest
{
    /// <summary>
    /// Sprint CSV or Excel workbook containing an Issues table/sheet.
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
