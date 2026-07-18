using WorkingSprintAgent.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddScoped<ICsvSprintDataService, CsvSprintDataService>();
builder.Services.AddScoped<IInsightGenerationService, MockInsightGenerationService>();
builder.Services.AddScoped<IPresentationBuilderService, PresentationBuilderService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => new
{
    Service = "Working Sprint Agent API",
    Version = "1.0.0",
    Status = "Running",
    Endpoints = new[]
    {
        "POST /api/sprintreport/generate - Generate sprint presentation",
        "POST /api/sprintreport/preview - Preview sprint data",
        "GET /api/sprintreport/csv-format - CSV format help",
        "GET /api/sprintreport/health - Health check"
    }
});

app.Run();