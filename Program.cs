using Microsoft.OpenApi.Models;
using WorkingSprintAgent.Models;
using WorkingSprintAgent.Services;
using WorkingSprintAgent.Swagger;
using WorkingSprintAgent.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Configure OpenAI settings
builder.Services.Configure<OpenAIConfiguration>(
    builder.Configuration.GetSection(OpenAIConfiguration.ConfigSection));

// Add services
builder.Services.AddControllers();
builder.Services.AddMemoryCache(); // For OpenAI response caching
builder.Services.AddHttpClient(); // For OpenAI HTTP client

// Configure Swagger/OpenAPI with comprehensive documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Working Sprint Agent API",
        Description = "AI-powered sprint data analysis and PowerPoint presentation generation API. " +
                     "Upload CSV sprint data to get automated insights and stakeholder-ready presentations.",
        Contact = new OpenApiContact
        {
            Name = "Sprint Agent Support",
            Email = "support@sprintagent.com"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Add comprehensive API documentation
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "WorkingSprintAgent.xml"), true);
    
    // Configure file upload support in Swagger UI
    options.OperationFilter<FileUploadOperationFilter>();
    
    // Add security definitions for future API key authentication
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Description = "API Key needed to access the endpoints (if required)",
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Type = SecuritySchemeType.ApiKey
    });

    // Configure examples and schemas
    options.SchemaFilter<ExampleSchemaFilter>();
    options.DocumentFilter<TagDocumentFilter>();
    
    // Group endpoints by functionality
    options.TagActionsBy(api => new[] { api.GroupName ?? "Sprint Reports" });
    options.DocInclusionPredicate((name, api) => true);
});

// Register services with proper dependency injection
builder.Services.AddScoped<ICsvSprintDataService, CsvSprintDataService>();
builder.Services.AddScoped<PowerPointPresentationService>(); // PowerPoint generation service
builder.Services.AddScoped<IPresentationBuilderService, PresentationBuilderService>();

// Register OpenAI services
builder.Services.AddScoped<IOpenAIService, OpenAIService>();

// Register cost optimization services
builder.Services.AddScoped<ITokenOptimizationService, TokenOptimizationService>();
builder.Services.AddSingleton<ICostMonitoringService, InMemoryCostMonitoringService>();

// Register token usage logging services
builder.Services.AddScoped<ITokenUsageLogger, TokenUsageLogger>();

// Register insight generation service (AI-powered with fallback)
builder.Services.AddScoped<IInsightGenerationService, OpenAIInsightGenerationService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// Configure comprehensive logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
    // Add detailed logging for AI services in development
    builder.Logging.AddFilter("WorkingSprintAgent.Services.OpenAIService", LogLevel.Information);
    builder.Logging.AddFilter("WorkingSprintAgent.Services.TokenUsageLogger", LogLevel.Information);
    builder.Logging.AddFilter("WorkingSprintAgent.Middleware.TokenUsageLoggingMiddleware", LogLevel.Information);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
    // Reduce noise in production but keep important AI metrics
    builder.Logging.AddFilter("WorkingSprintAgent.Services", LogLevel.Information);
}

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    
    // Enable Swagger in development
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "api/docs/{documentname}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/api/docs/v1/swagger.json", "Working Sprint Agent API v1");
        c.RoutePrefix = "api/docs";
        c.DocumentTitle = "Working Sprint Agent API Documentation";
        c.DefaultModelsExpandDepth(2);
        c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
        c.DisplayRequestDuration();
        c.EnableTryItOutByDefault();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
    });
}

// Also enable Swagger in production for API documentation
app.UseSwagger(c =>
{
    c.RouteTemplate = "api/docs/{documentname}/swagger.json";
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/docs/v1/swagger.json", "Working Sprint Agent API v1");
    c.RoutePrefix = "api/docs";
    c.DocumentTitle = "Working Sprint Agent API Documentation";
});

app.UseHttpsRedirection();
app.UseStaticFiles(); // Enable static file serving

// Add token usage logging middleware before other middleware
app.UseTokenUsageLogging();

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Enhanced root endpoint with service status and redirect to Swagger
app.MapGet("/", async (HttpContext context, IInsightGenerationService insightService) => 
{
    // Check if request prefers JSON (API client) or HTML (browser)
    var acceptHeader = context.Request.Headers.Accept.ToString();
    var isApiRequest = acceptHeader.Contains("application/json") && !acceptHeader.Contains("text/html");
    
    if (isApiRequest)
    {
        var serviceStatus = insightService.GetServiceStatus();
        
        return Results.Ok(new
        {
            Service = "Working Sprint Agent API",
            Version = "1.0.0",
            Status = "Running",
            AIEnabled = serviceStatus.IsAIEnabled,
            ServiceType = serviceStatus.ServiceType,
            Model = serviceStatus.Model,
            Documentation = new
            {
                SwaggerUI = "/api/docs",
                OpenAPISpec = "/api/docs/v1/swagger.json"
            },
            Endpoints = new[]
            {
                "POST /api/sprintreport/generate - Generate sprint presentation (PowerPoint/HTML)",
                "POST /api/sprintreport/preview - Preview sprint data with AI insights", 
                "GET /api/sprintreport/csv-format - CSV format requirements and examples",
                "GET /api/sprintreport/health - Comprehensive system health check",
                "GET /api/sprintreport/ai-status - AI service status and configuration",
                "GET /api/sprintreport/token-usage - Token usage statistics and cost analysis",
                "GET /api/sprintreport/cost-dashboard - Real-time cost monitoring dashboard",
                "GET /api/sprintreport/optimization-recommendations - Cost optimization suggestions",
                "GET /api/sprintreport/templates - Available presentation templates",
                "GET /api/sprintreport/export-cost-report - Export detailed cost analysis"
            },
            Configuration = new
            {
                AIEnabled = serviceStatus.IsAIEnabled,
                CachingEnabled = serviceStatus.IsCachingEnabled,
                TokenTrackingEnabled = serviceStatus.IsTokenTrackingEnabled,
                EstimatedCostPerRequest = $"${serviceStatus.EstimatedCostPerRequest:F4}"
            }
        });
    }
    else
    {
        // Redirect browsers to Swagger UI for better user experience
        context.Response.Redirect("/api/docs");
        return Results.Empty;
    }
})
.WithName("GetApiInfo")
.WithTags("System")
.WithSummary("Get API information and status")
.WithDescription("Returns API information, service status, and available endpoints. Browsers are redirected to Swagger documentation.")
.WithOpenApi();

app.Run();