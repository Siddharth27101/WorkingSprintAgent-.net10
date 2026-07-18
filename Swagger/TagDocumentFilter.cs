using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WorkingSprintAgent.Swagger;

/// <summary>
/// Document filter to organize API endpoints with proper tags and descriptions
/// </summary>
public class TagDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // Define comprehensive API tags with descriptions
        swaggerDoc.Tags = new List<OpenApiTag>
        {
            new OpenApiTag
            {
                Name = "Sprint Reports",
                Description = "Generate AI-powered sprint reports and presentations from CSV data. " +
                             "Upload sprint data to get automated insights, stakeholder-ready PowerPoint presentations, and detailed analytics.",
                ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "Learn more about CSV format requirements",
                    Url = new Uri("https://docs.sprintagent.com/csv-format", UriKind.Absolute)
                }
            },
            new OpenApiTag
            {
                Name = "AI Services",
                Description = "AI-powered analytics and insight generation services. " +
                             "Monitor token usage, cost optimization, and service capabilities.",
                ExternalDocs = new OpenApiExternalDocs
                {
                    Description = "AI Service Configuration Guide",
                    Url = new Uri("https://docs.sprintagent.com/ai-config", UriKind.Absolute)
                }
            },
            new OpenApiTag
            {
                Name = "Presentation Templates",
                Description = "Manage presentation templates and customization options. " +
                             "Configure branding, layouts, and output formats for stakeholder presentations."
            },
            new OpenApiTag
            {
                Name = "System",
                Description = "System health, status monitoring, and service information endpoints."
            }
        };

        // Add comprehensive API description
        swaggerDoc.Info.Description += "\n\n## Quick Start\n\n" +
            "1. **Upload CSV Data**: Use `/api/sprintreport/generate` to upload your sprint CSV file\n" +
            "2. **Get AI Insights**: The system will automatically generate insights using OpenAI\n" +
            "3. **Download Presentation**: Receive a professional PowerPoint presentation ready for stakeholders\n\n" +
            "## Key Features\n\n" +
            "- 🤖 **AI-Powered Analysis**: Advanced insights using OpenAI GPT models\n" +
            "- 📊 **Professional Presentations**: Stakeholder-ready PowerPoint slides\n" +
            "- 💰 **Cost Optimization**: Token usage tracking and cost minimization strategies\n" +
            "- 🎨 **Multiple Templates**: Professional, Modern, Corporate, and Minimal designs\n" +
            "- 📈 **Comprehensive Analytics**: Team performance, blockers, and recommendations\n" +
            "- 🔄 **Smart Caching**: Reduce costs with intelligent response caching\n\n" +
            "## CSV Format Requirements\n\n" +
            "Your CSV file should include these columns (case-insensitive):\n" +
            "- **Required**: TaskId, Title, Status, Assignee\n" +
            "- **Optional**: Type, Priority, StoryPoints, SprintName, StartDate, EndDate\n\n" +
            "## AI Cost Optimization\n\n" +
            "The system includes several cost optimization strategies:\n" +
            "- Response caching for identical requests\n" +
            "- Data preprocessing to reduce token usage\n" +
            "- Daily budget limits and monitoring\n" +
            "- Fallback to rule-based insights when budget exceeded\n" +
            "- Real-time cost tracking and optimization recommendations";

        // Add additional metadata
        swaggerDoc.ExternalDocs = new OpenApiExternalDocs
        {
            Description = "Working Sprint Agent Documentation",
            Url = new Uri("https://docs.sprintagent.com", UriKind.Absolute)
        };

        // Add server information
        swaggerDoc.Servers = new List<OpenApiServer>
        {
            new OpenApiServer
            {
                Url = "{scheme}://{host}:{port}",
                Description = "Working Sprint Agent API Server",
                Variables = new Dictionary<string, OpenApiServerVariable>
                {
                    ["scheme"] = new OpenApiServerVariable
                    {
                        Default = "https",
                        Enum = new List<string> { "http", "https" },
                        Description = "HTTP scheme"
                    },
                    ["host"] = new OpenApiServerVariable
                    {
                        Default = "localhost",
                        Description = "Server hostname"
                    },
                    ["port"] = new OpenApiServerVariable
                    {
                        Default = "5000",
                        Description = "Server port"
                    }
                }
            }
        };
    }
}