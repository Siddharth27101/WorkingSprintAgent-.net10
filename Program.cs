using System.Reflection;
using Microsoft.Extensions.Options;
using WorkingSprintAgent.Middleware;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services;
using WorkingSprintAgent.Services.Agents;
using WorkingSprintAgent.Services.Orchestration;
using WorkingSprintAgent.Services.Plugins;

var builder = WebApplication.CreateBuilder(args);

// Support both the ASP.NET Core configuration key and OpenAI's conventional environment variable.
var conventionalOpenAiKey = builder.Configuration["OPENAI_API_KEY"];
if (!string.IsNullOrWhiteSpace(conventionalOpenAiKey))
{
    builder.Configuration[$"{OpenAIConfiguration.ConfigSection}:ApiKey"] = conventionalOpenAiKey;
}

builder.Services.Configure<OpenAIConfiguration>(
    builder.Configuration.GetSection(OpenAIConfiguration.ConfigSection));
builder.Services.AddOptions<SemanticKernelOptions>()
    .Bind(builder.Configuration.GetSection(SemanticKernelOptions.ConfigSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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

// Deterministic workflow retained as the default and automatic fallback.
builder.Services.AddScoped<IFileUploadAgent, FileUploadAgent>();
builder.Services.AddScoped<IAnalysisAgent, AnalysisAgent>();
builder.Services.AddScoped<MockInsightGenerationService>();
builder.Services.AddScoped<IPresentationAgent, PresentationAgent>();
builder.Services.AddScoped<DeterministicSprintReportOrchestrator>();
builder.Services.AddScoped<LocalSprintReportFallback>();

// Optional Semantic Kernel workflow: scoped state, least-privilege plugins, and role-specific agents.
builder.Services.AddScoped<ISprintWorkflowStateStore, ScopedSprintWorkflowStateStore>();
builder.Services.AddScoped<CsvSprintPlugin>();
builder.Services.AddScoped<PresentationPlugin>();
builder.Services.AddScoped<ISemanticKernelAgentFactory, SemanticKernelAgentFactory>();
builder.Services.AddScoped<ISprintReportOrchestrator, SemanticKernelSprintReportOrchestrator>();

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

// Startup diagnostics: confirm at boot whether the OpenAI key is actually loaded.
// This makes it obvious whether the app is in "key missing" mode vs "key present but
// the API call is failing", which is the common source of unexpected fallback output.
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup.OpenAI");
    var openAiOptions = app.Services.GetRequiredService<IOptions<OpenAIConfiguration>>().Value;
    if (string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
    {
        startupLogger.LogWarning(
            "OpenAI API key NOT detected in '{Environment}'. Insights will use the deterministic fallback. " +
            "Set OpenAI:ApiKey in appsettings or the OPENAI_API_KEY environment variable.",
            app.Environment.EnvironmentName);
    }
    else
    {
        var key = openAiOptions.ApiKey.Trim();
        var masked = key.Length <= 8 ? "****" : $"{key[..4]}...{key[^4..]}";
        startupLogger.LogInformation(
            "OpenAI API key detected ({Masked}, length {Length}) in '{Environment}'. Model: {Model}. AI-powered insights enabled.",
            masked, key.Length, app.Environment.EnvironmentName, openAiOptions.Model);
    }
}

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
    options.DocumentTitle = "Working Sprint Agent API - CSV/XLSX to 14-slide PowerPoint";
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
        var semanticKernel = context.RequestServices
            .GetRequiredService<IOptions<SemanticKernelOptions>>()
            .Value;
        await Results.Ok(CreateSystemInformation(insightService, semanticKernel)).ExecuteAsync(context);
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

app.MapGet(
    "/api/system",
    (IInsightGenerationService insightService, IOptions<SemanticKernelOptions> semanticKernel) =>
        Results.Ok(CreateSystemInformation(insightService, semanticKernel.Value)))
.WithTags("System")
.WithSummary("Get service information");

app.Run();

static object CreateSystemInformation(
    IInsightGenerationService insightService,
    SemanticKernelOptions semanticKernel)
{
    var serviceStatus = insightService.GetServiceStatus();
    var semanticKernelActive = semanticKernel.Enabled && serviceStatus.IsAIEnabled;
    return new
    {
        Service = "Working Sprint Agent API",
        Version = "3.0.0",
        Framework = ".NET 10",
        Status = "Running",
        Swagger = "/swagger",
        serviceStatus.IsAIEnabled,
        serviceStatus.ServiceType,
        serviceStatus.Model,
        SemanticKernel = new
        {
            semanticKernel.Enabled,
            Model = semanticKernel.Model,
            semanticKernel.MaxReviewerRevisions,
            semanticKernel.ReviewerApprovalThreshold,
            semanticKernel.EnableManagerSelection,
            ActiveWorkflow = semanticKernelActive
                ? "Semantic Kernel multi-agent"
                : "Deterministic fallback"
        },
        Workflow = semanticKernelActive
            ? "CSV/XLSX plugin -> ChatCompletionAgent analyst -> coach -> quality reviewer -> optional manager -> presentation plugin"
            : "Deterministic CSV/XLSX parse -> analysis -> 14-slide presentation",
        MainEndpoints = new[]
        {
            "GET /api/sprintreport/sample-csv",
            "POST /api/sprintreport/preview",
            "POST /api/sprintreport/generate"
        }
    };
}
