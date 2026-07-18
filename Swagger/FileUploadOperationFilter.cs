using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace WorkingSprintAgent.Swagger;

/// <summary>
/// Swagger operation filter to properly document file upload endpoints
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var hasFileParameter = context.MethodInfo.GetParameters()
            .Any(p => p.ParameterType == typeof(IFormFile) || p.ParameterType == typeof(IFormFileCollection));

        if (!hasFileParameter) return;

        // Clear existing parameters for file upload endpoints
        operation.Parameters?.Clear();

        // Set up proper request body for file uploads
        operation.RequestBody = new OpenApiRequestBody
        {
            Description = "Upload CSV file with sprint data",
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["csvFile"] = new OpenApiSchema
                            {
                                Type = "string",
                                Format = "binary",
                                Description = "CSV file containing sprint data. Required columns: TaskId, Title, Status, Assignee"
                            },
                            ["sprintName"] = new OpenApiSchema
                            {
                                Type = "string",
                                Description = "Optional custom sprint name. If not provided, will be extracted from data or use timestamp",
                                Nullable = true,
                                Example = new Microsoft.OpenApi.Any.OpenApiString("Sprint 2024-Q1")
                            },
                            ["outputFormat"] = new OpenApiSchema
                            {
                                Type = "string",
                                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                                {
                                    new Microsoft.OpenApi.Any.OpenApiString("powerpoint"),
                                    new Microsoft.OpenApi.Any.OpenApiString("html")
                                },
                                Description = "Output format for the presentation",
                                Default = new Microsoft.OpenApi.Any.OpenApiString("powerpoint")
                            },
                            ["template"] = new OpenApiSchema
                            {
                                Type = "string",
                                Enum = new List<Microsoft.OpenApi.Any.IOpenApiAny>
                                {
                                    new Microsoft.OpenApi.Any.OpenApiString("professional"),
                                    new Microsoft.OpenApi.Any.OpenApiString("modern"),
                                    new Microsoft.OpenApi.Any.OpenApiString("corporate"),
                                    new Microsoft.OpenApi.Any.OpenApiString("minimal")
                                },
                                Description = "Presentation template to use",
                                Default = new Microsoft.OpenApi.Any.OpenApiString("professional")
                            },
                            ["companyName"] = new OpenApiSchema
                            {
                                Type = "string",
                                Description = "Company name for branding (optional)",
                                Nullable = true,
                                Example = new Microsoft.OpenApi.Any.OpenApiString("Your Company")
                            }
                        },
                        Required = new HashSet<string> { "csvFile" }
                    }
                }
            }
        };

        // Add comprehensive response documentation
        operation.Responses["200"] = new OpenApiResponse
        {
            Description = "Successfully generated presentation",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary",
                        Description = "PowerPoint presentation file (.pptx)"
                    }
                },
                ["text/html"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "string",
                        Description = "HTML presentation (fallback format)"
                    }
                }
            }
        };

        operation.Responses["400"] = new OpenApiResponse
        {
            Description = "Bad request - invalid file or parameters",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["error"] = new OpenApiSchema { Type = "string", Description = "Error description" }
                        }
                    },
                    Examples = new Dictionary<string, OpenApiExample>
                    {
                        ["file-required"] = new OpenApiExample
                        {
                            Summary = "Missing file",
                            Value = new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["error"] = new Microsoft.OpenApi.Any.OpenApiString("Please upload a valid CSV file.")
                            }
                        },
                        ["invalid-format"] = new OpenApiExample
                        {
                            Summary = "Invalid file format",
                            Value = new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["error"] = new Microsoft.OpenApi.Any.OpenApiString("File must be a CSV file.")
                            }
                        },
                        ["no-data"] = new OpenApiExample
                        {
                            Summary = "No valid data",
                            Value = new Microsoft.OpenApi.Any.OpenApiObject
                            {
                                ["error"] = new Microsoft.OpenApi.Any.OpenApiString("No valid tasks found in CSV file. Please check the format.")
                            }
                        }
                    }
                }
            }
        };

        operation.Responses["500"] = new OpenApiResponse
        {
            Description = "Internal server error",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>
                        {
                            ["error"] = new OpenApiSchema { Type = "string", Description = "Error description" }
                        }
                    }
                }
            }
        };
    }
}