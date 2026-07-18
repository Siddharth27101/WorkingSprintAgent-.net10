using System.Reflection;
using WorkingSprintAgent.Middleware;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services;

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

app.UseStaticFiles();
app.UseTokenUsageLogging();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", (HttpContext context, IInsightGenerationService insightService) =>
{
    var acceptsJson = context.Request.Headers.Accept.Any(value =>
        value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    if (!acceptsJson)
    {
        return Results.Redirect("/swagger");
    }

    var serviceStatus = insightService.GetServiceStatus();
    return Results.Ok(new
    {
        Service = "Working Sprint Agent API",
        Version = "2.0.0",
        Framework = ".NET 10",
        Status = "Running",
        Swagger = "/swagger",
        serviceStatus.IsAIEnabled,
        serviceStatus.ServiceType,
        serviceStatus.Model,
        Workflow = "Upload CSV -> calculate sprint metrics -> generate OpenAI summary -> download PowerPoint",
        MainEndpoints = new[]
        {
            "GET /api/sprintreport/sample-csv",
            "POST /api/sprintreport/preview",
            "POST /api/sprintreport/generate"
        }
    });
})
.WithTags("System")
.WithSummary("Get service information");

app.Run();
