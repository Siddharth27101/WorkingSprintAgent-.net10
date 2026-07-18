# Working Sprint Agent

A .NET 8 ASP.NET Core API that reads sprint data from CSV, generates rule-based or OpenAI-assisted insights, and creates HTML or PowerPoint presentations.

## Requirements

- Visual Studio 2022 17.8 or later with the **ASP.NET and web development** workload, or the .NET 8 SDK
- An OpenAI API key is optional; the application uses local rule-based insights when no key is configured

The project uses only the ASP.NET Core shared framework, so it does not require third-party NuGet packages.

## Run in Visual Studio

1. Open `WorkingSprintAgent.csproj` in Visual Studio.
2. Select either `WorkingSprintAgent (HTTP)` or `WorkingSprintAgent (HTTPS)`.
3. Run the project. The browser opens the landing page automatically.

Do not put secrets in `appsettings.json`. To enable OpenAI locally, use user secrets:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key"
```

## Command-line quick start

```bash
dotnet restore
dotnet build
dotnet run --launch-profile "WorkingSprintAgent (HTTP)"
```

The HTTP profile listens at `http://localhost:5080`.

Check application health:

```bash
curl http://localhost:5080/api/sprintreport/health
```

Generate an HTML report:

```bash
curl -X POST http://localhost:5080/api/sprintreport/generate \
  -F "csvFile=@sample-data/dummy-sprint.csv" \
  -F "sprintName=Sprint 15" \
  -F "outputFormat=html" \
  --output sprint-report.html
```

Generate a PowerPoint report by changing `outputFormat` to `powerpoint` and the output filename to `sprint-report.pptx`.

## Main endpoints

- `POST /api/sprintreport/generate` — Generate an HTML or PowerPoint presentation
- `POST /api/sprintreport/preview` — Preview metrics and insights
- `GET /api/sprintreport/csv-format` — View CSV requirements and examples
- `GET /api/sprintreport/health` — Check system health
- `GET /api/sprintreport/ai-status` — Check AI/fallback status
- `GET /api/sprintreport/token-usage` — View token and cost data
- `GET /api/sprintreport/cost-dashboard` — View cost-monitoring data
- `GET /api/sprintreport/usage-analytics` — View usage analytics

## CSV format

Required columns are `TaskId`, `Title`, `Status`, and `Assignee`. Optional columns include `Type`, `Priority`, `StoryPoints`, `SprintName`, `StartDate`, and `EndDate`. Column aliases and examples are available from `/api/sprintreport/csv-format`.

## Architecture

```text
CSV upload -> Parse and validate -> Compute metrics -> Generate insights -> Build presentation
```

When `OpenAI:ApiKey` is empty, all report-generation endpoints remain functional and use `MockInsightGenerationService`. When configured, `OpenAIService` calls the OpenAI REST API directly, tracks token usage, applies daily budget limits, and falls back automatically if a request fails.
