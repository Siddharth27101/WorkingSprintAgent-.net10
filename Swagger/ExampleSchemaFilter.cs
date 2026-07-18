using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using WorkingSprintAgent.Models;

namespace WorkingSprintAgent.Swagger;

/// <summary>
/// Swagger schema filter to add examples for data models
/// </summary>
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(SprintMetrics))
        {
            schema.Example = new OpenApiObject
            {
                ["sprintName"] = new OpenApiString("Sprint 2024-Q1"),
                ["totalTasks"] = new OpenApiInteger(25),
                ["completedTasks"] = new OpenApiInteger(20),
                ["blockedTasks"] = new OpenApiInteger(2),
                ["totalStoryPoints"] = new OpenApiDouble(42.5),
                ["completedStoryPoints"] = new OpenApiDouble(35.0),
                ["completionRatePercent"] = new OpenApiDouble(80.0),
                ["tasksByStatus"] = new OpenApiObject
                {
                    ["Done"] = new OpenApiInteger(20),
                    ["In Progress"] = new OpenApiInteger(3),
                    ["Blocked"] = new OpenApiInteger(2)
                },
                ["workloadByAssignee"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["assignee"] = new OpenApiString("John Doe"),
                        ["totalTasks"] = new OpenApiInteger(8),
                        ["completedTasks"] = new OpenApiInteger(7),
                        ["storyPoints"] = new OpenApiDouble(15.0)
                    },
                    new OpenApiObject
                    {
                        ["assignee"] = new OpenApiString("Jane Smith"),
                        ["totalTasks"] = new OpenApiInteger(10),
                        ["completedTasks"] = new OpenApiInteger(8),
                        ["storyPoints"] = new OpenApiDouble(18.5)
                    }
                }
            };
        }
        else if (context.Type == typeof(SprintInsights))
        {
            schema.Example = new OpenApiObject
            {
                ["executiveSummary"] = new OpenApiString("Sprint 2024-Q1 achieved strong performance with 80% task completion and 35.0 of 42.5 story points delivered. 2 critical blockers require immediate attention."),
                ["keyHighlights"] = new OpenApiArray
                {
                    new OpenApiString("Completed 20 of 25 planned tasks"),
                    new OpenApiString("Delivered 35.0 story points (82% of planned capacity)"),
                    new OpenApiString("Jane Smith led team performance with 8 completed tasks"),
                    new OpenApiString("Successfully closed 20 items with quality deliverables")
                },
                ["risksAndBlockers"] = new OpenApiArray
                {
                    new OpenApiString("2 tasks are currently blocked and impacting delivery"),
                    new OpenApiString("1 team member has high task allocation")
                },
                ["recommendations"] = new OpenApiArray
                {
                    new OpenApiString("Prioritize resolving blocked items in next standup meeting"),
                    new OpenApiString("Maintain current sprint velocity and team collaboration practices"),
                    new OpenApiString("Consider load balancing for next sprint")
                },
                ["teamPerformanceNarrative"] = new OpenApiString("Team of 2 members delivered strong results with an average of 12.5 tasks and 21.3 story points per person. Collaboration and task distribution patterns show high engagement across the sprint."),
                ["nextSprintFocus"] = new OpenApiString("Focus on resolving current blockers and maintaining delivery momentum.")
            };
        }
        else if (context.Type == typeof(TokenUsageStats))
        {
            schema.Example = new OpenApiObject
            {
                ["timestamp"] = new OpenApiString("2024-01-15T10:30:00Z"),
                ["requestType"] = new OpenApiString("InsightGeneration"),
                ["inputTokens"] = new OpenApiInteger(1250),
                ["outputTokens"] = new OpenApiInteger(800),
                ["totalTokens"] = new OpenApiInteger(2050),
                ["estimatedCost"] = new OpenApiDouble(0.0069),
                ["model"] = new OpenApiString("gpt-4o-mini"),
                ["responseTime"] = new OpenApiString("00:00:03.2500000"),
                ["cacheHit"] = new OpenApiBoolean(false)
            };
        }
        else if (context.Type == typeof(PresentationSummary))
        {
            schema.Example = new OpenApiObject
            {
                ["title"] = new OpenApiString("Sprint 2024-Q1 - Sprint Report"),
                ["slideCount"] = new OpenApiInteger(8),
                ["slideTopics"] = new OpenApiArray
                {
                    new OpenApiString("Title Slide"),
                    new OpenApiString("Executive Summary"),
                    new OpenApiString("Sprint Metrics Overview"),
                    new OpenApiString("Task Completion Analysis"),
                    new OpenApiString("Team Performance"),
                    new OpenApiString("Risks & Blockers"),
                    new OpenApiString("Recommendations"),
                    new OpenApiString("Next Sprint Focus")
                },
                ["chartTypes"] = new OpenApiArray
                {
                    new OpenApiString("Completion Rate Chart"),
                    new OpenApiString("Task Status Distribution"),
                    new OpenApiString("Team Performance Metrics")
                },
                ["estimatedViewingTimeMinutes"] = new OpenApiInteger(16),
                ["generatedAt"] = new OpenApiString("2024-01-15T10:30:00Z"),
                ["template"] = new OpenApiString("Professional"),
                ["estimatedFileSizeBytes"] = new OpenApiInteger(1048576)
            };
        }
        else if (context.Type == typeof(InsightServiceStatus))
        {
            schema.Example = new OpenApiObject
            {
                ["isAIEnabled"] = new OpenApiBoolean(true),
                ["serviceType"] = new OpenApiString("AI-Powered (OpenAI)"),
                ["model"] = new OpenApiString("gpt-4o-mini"),
                ["isCachingEnabled"] = new OpenApiBoolean(true),
                ["isTokenTrackingEnabled"] = new OpenApiBoolean(true),
                ["maxDailyTokens"] = new OpenApiInteger(50000),
                ["estimatedCostPerRequest"] = new OpenApiDouble(0.0069),
                ["capabilities"] = new OpenApiArray
                {
                    new OpenApiString("AI-powered insight generation"),
                    new OpenApiString("Natural language processing"),
                    new OpenApiString("Contextual recommendations"),
                    new OpenApiString("Cost optimization strategies"),
                    new OpenApiString("Token usage tracking"),
                    new OpenApiString("Response caching")
                },
                ["limitations"] = new OpenApiArray
                {
                    new OpenApiString("Daily token budget: 50,000 tokens"),
                    new OpenApiString("Max tokens per request: 1,500"),
                    new OpenApiString("Cache expiration: 60 minutes")
                }
            };
        }
    }
}