using WorkingSprintAgent.Middleware;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OpenAIConfiguration>(
    builder.Configuration.GetSection(OpenAIConfiguration.ConfigSection));

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ICsvSprintDataService, CsvSprintDataService>();
builder.Services.AddScoped<PowerPointPresentationService>();
builder.Services.AddScoped<IPresentationBuilderService, PresentationBuilderService>();
builder.Services.AddScoped<IOpenAIService, OpenAIService>();
builder.Services.AddScoped<ITokenOptimizationService, TokenOptimizationService>();
builder.Services.AddSingleton<ICostMonitoringService, InMemoryCostMonitoringService>();
builder.Services.AddScoped<ITokenUsageLogger, TokenUsageLogger>();
builder.Services.AddScoped<IInsightGenerationService, OpenAIInsightGenerationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseTokenUsageLogging();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/docs", () => Results.Content(
    """
    <!doctype html>
    <html lang="en">
    <head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Working Sprint Agent API</title></head>
    <body style="font-family:Segoe UI,Arial,sans-serif;max-width:900px;margin:40px auto;padding:0 20px">
      <h1>Working Sprint Agent API</h1>
      <p><a href="/api/docs/v1/swagger.json">OpenAPI 3.0 document</a></p>
      <h2>Report endpoints</h2>
      <ul>
        <li><code>POST /api/sprintreport/generate</code> — generate an HTML or PowerPoint report from multipart CSV data</li>
        <li><code>POST /api/sprintreport/preview</code> — preview sprint metrics and insights</li>
        <li><code>GET /api/sprintreport/csv-format</code> — CSV column requirements and examples</li>
        <li><code>GET /api/sprintreport/health</code> — service health and configuration status</li>
        <li><code>GET /api/sprintreport/ai-status</code> — AI and fallback status</li>
        <li><code>GET /api/sprintreport/templates</code> — presentation templates</li>
      </ul>
      <p><a href="/index.html">Return to the application</a></p>
    </body>
    </html>
    """,
    "text/html"));

app.MapGet("/api/docs/v1/swagger.json", () => Results.Json(new
{
    openapi = "3.0.1",
    info = new
    {
        title = "Working Sprint Agent API",
        version = "v1",
        description = "Analyze sprint CSV data and generate HTML or PowerPoint reports."
    },
    paths = new Dictionary<string, object>
    {
        ["/api/sprintreport/generate"] = new { post = new { summary = "Generate a sprint presentation", responses = new Dictionary<string, object> { ["200"] = new { description = "Presentation file" }, ["400"] = new { description = "Invalid input" } } } },
        ["/api/sprintreport/preview"] = new { post = new { summary = "Preview sprint metrics and insights", responses = new Dictionary<string, object> { ["200"] = new { description = "Sprint preview" }, ["400"] = new { description = "Invalid input" } } } },
        ["/api/sprintreport/csv-format"] = new { get = new { summary = "Get CSV format requirements", responses = new Dictionary<string, object> { ["200"] = new { description = "CSV format guide" } } } },
        ["/api/sprintreport/health"] = new { get = new { summary = "Get service health", responses = new Dictionary<string, object> { ["200"] = new { description = "Health status" } } } },
        ["/api/sprintreport/ai-status"] = new { get = new { summary = "Get AI service status", responses = new Dictionary<string, object> { ["200"] = new { description = "AI status" } } } },
        ["/api/sprintreport/token-usage"] = new { get = new { summary = "Get token usage", responses = new Dictionary<string, object> { ["200"] = new { description = "Token usage summary" } } } },
        ["/api/sprintreport/cost-dashboard"] = new { get = new { summary = "Get cost dashboard", responses = new Dictionary<string, object> { ["200"] = new { description = "Cost dashboard" } } } },
        ["/api/sprintreport/usage-analytics"] = new { get = new { summary = "Get usage analytics", responses = new Dictionary<string, object> { ["200"] = new { description = "Usage analytics" } } } },
        ["/api/sprintreport/templates"] = new { get = new { summary = "Get presentation templates", responses = new Dictionary<string, object> { ["200"] = new { description = "Template list" } } } }
    }
}));

app.MapGet("/", (HttpContext context, IInsightGenerationService insightService) =>
{
    var acceptsJson = context.Request.Headers.Accept.Any(value =>
        value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    if (!acceptsJson)
    {
        return Results.Redirect("/index.html");
    }

    var serviceStatus = insightService.GetServiceStatus();
    return Results.Ok(new
    {
        Service = "Working Sprint Agent API",
        Version = "1.0.0",
        Status = "Running",
        serviceStatus.IsAIEnabled,
        serviceStatus.ServiceType,
        serviceStatus.Model,
        Endpoints = new[]
        {
            "POST /api/sprintreport/generate",
            "POST /api/sprintreport/preview",
            "GET /api/sprintreport/csv-format",
            "GET /api/sprintreport/health",
            "GET /api/sprintreport/ai-status",
            "GET /api/sprintreport/token-usage",
            "GET /api/sprintreport/cost-dashboard",
            "GET /api/sprintreport/optimization-recommendations",
            "GET /api/sprintreport/templates",
            "GET /api/sprintreport/export-cost-report",
            "GET /api/sprintreport/usage-analytics"
        }
    });
});

app.Run();
