using System.Reflection;
using WorkingSprintAgent.Middleware;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services;
using WorkingSprintAgent.Services.Agents;
using WorkingSprintAgent.Services.Orchestration;

var builder = WebApplication.CreateBuilder(args);

// Support both the ASP.NET Core configuration key and OpenAI's conventional environment variable.
var conventionalOpenAiKey = builder.Configuration["OPENAI_API_KEY"];
if (!string.IsNullOrWhiteSpace(conventionalOpenAiKey))
{
    builder.Configuration[$"{OpenAIConfiguration.ConfigSection}:ApiKey"] = conventionalOpenAiKey;
}

builder.Services.Configure<OpenAIConfiguration>(
    builder.Configuration.GetSection(OpenAIConfiguration.ConfigSection));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFileName);
    options.IncludeXmlComments(xmlFilePath, includeControllerXmlComments: true);
});

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

// Agent workflow: upload/parse -> analyze -> build downloadable presentation.
builder.Services.AddScoped<IFileUploadAgent, FileUploadAgent>();
builder.Services.AddScoped<IAnalysisAgent, AnalysisAgent>();
builder.Services.AddScoped<IPresentationAgent, PresentationAgent>();
builder.Services.AddScoped<ISprintReportOrchestrator, SprintReportOrchestrator>();

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

// Swagger is intentionally enabled in every environment for this demonstration API.
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Working Sprint Agent API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Working Sprint Agent API - CSV to PowerPoint Demo";
    options.DisplayRequestDuration();
    options.EnableDeepLinking();
    options.EnableFilter();
});

// Preserve the original JSON root contract while serving the browser workflow by default.
app.Use(async (context, next) =>
{
    var acceptsJson = context.Request.Headers.Accept.Any(value =>
        value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    if (context.Request.Path == "/" && acceptsJson)
    {
        var insightService = context.RequestServices.GetRequiredService<IInsightGenerationService>();
        await Results.Ok(CreateSystemInformation(insightService)).ExecuteAsync(context);
        return;
    }

    await next(context);
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseTokenUsageLogging();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/system", (IInsightGenerationService insightService) =>
    Results.Ok(CreateSystemInformation(insightService)))
.WithTags("System")
.WithSummary("Get service information");

app.Run();

static object CreateSystemInformation(IInsightGenerationService insightService)
{
    var serviceStatus = insightService.GetServiceStatus();
    return new
    {
        Service = "Working Sprint Agent API",
        Version = "2.0.0",
        Framework = ".NET 10",
        Status = "Running",
        Swagger = "/swagger",
        serviceStatus.IsAIEnabled,
        serviceStatus.ServiceType,
        serviceStatus.Model,
        Workflow = "Agent Orchestrator -> File Upload Agent -> Analysis Agent (GPT-4o mini/fallback) -> PPT Agent -> Download",
        MainEndpoints = new[]
        {
            "GET /api/sprintreport/sample-csv",
            "POST /api/sprintreport/preview",
            "POST /api/sprintreport/generate"
        }
    };
}
