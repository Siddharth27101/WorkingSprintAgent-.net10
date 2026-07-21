# Working Sprint Agent (.NET 10)

An ASP.NET Core .NET 10 API that accepts Jira sprint data as CSV or a multi-sheet Excel workbook, calculates delivery/quality/risk metrics, asks OpenAI for concise insights, and returns a stakeholder-ready 15-slide PowerPoint presentation with charts and graph explanations.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Visual Studio 2026 with the **ASP.NET and web development** workload, or another editor that supports .NET 10
- Internet access for the initial NuGet restore and for OpenAI requests
- An OpenAI API key for AI-generated insights

## Configure the OpenAI key

User secrets are recommended because they keep the key out of source control:

```bash
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key"
```

In Visual Studio, right-click the project and select **Manage User Secrets**, then add:

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key"
  }
}
```

You can also use the conventional OpenAI environment variable:

```bash
export OPENAI_API_KEY="your-openai-api-key"
```

The ASP.NET Core-style `OpenAI__ApiKey` environment variable is supported as well.

Never commit a real API key. Restart the API after changing its configuration. If no key is configured—or if OpenAI is unavailable—the API still works using local fallback insights.

## Run in Visual Studio

1. Open `WorkingSprintAgent.csproj` in Visual Studio 2026.
2. Restore NuGet packages if Visual Studio does not do so automatically.
3. Select `WorkingSprintAgent (HTTPS)` or `WorkingSprintAgent (HTTP)`.
4. Run the project. Visual Studio opens Swagger at `/swagger`.

Command-line alternative:

```bash
dotnet restore
dotnet build
dotnet run --launch-profile "WorkingSprintAgent (HTTP)"
```

Swagger is then available at `http://localhost:5080/swagger`.

## Demonstrate sprint data to PowerPoint in Swagger

1. Open `GET /api/SprintReport/sample-csv`, select **Try it out**, and download the sample CSV.
2. Open `POST /api/SprintReport/preview`, select **Try it out**, upload the CSV, and execute it. This displays parsed metrics, the generated summary, and AI token metadata.
3. Open `POST /api/SprintReport/generate`, select **Try it out**, upload the CSV, and keep `outputFormat` set to `powerpoint`.
4. Optionally choose `professional`, `modern`, `corporate`, or `minimal`, then execute the request.
5. Download the returned `.pptx` file from the Swagger response.

The complete processing flow is:

```text
CSV or XLSX upload
  -> parse Issues and optional workbook analytics sheets
  -> calculate delivery, quality, capacity, scope, and risk metrics
  -> request structured OpenAI insights (or deterministic fallback)
  -> create an exact 15-slide PowerPoint with charts and graph explanations
  -> return the .pptx download
```

## CSV columns

Required columns:

- `TaskId`
- `Title`
- `Status`
- `Assignee`

Optional columns:

- `Type`
- `Priority`
- `StoryPoints`
- `SprintName`
- `StartDate`
- `EndDate`

Column names are case-insensitive and common aliases are accepted. Excel workbooks require an `Issues` sheet and can optionally include `SprintSummary`, `Burndown`, `Capacity`, `Quality`, `CI-CD`, and `Risks` sheets. `GET /api/SprintReport/csv-format` returns the complete format guide and sample data. Uploads must be `.csv` or `.xlsx` files no larger than 25 MB; up to 20,000 issues are supported.

## Main endpoints

| Method | Endpoint | Purpose |
|---|---|---|
| GET | `/api/SprintReport/sample-csv` | Download a demonstration CSV |
| GET | `/api/SprintReport/csv-format` | View supported columns and examples |
| GET | `/api/SprintReport/health` | Check API and AI configuration status |
| GET | `/api/SprintReport/ai-status` | Check OpenAI/fallback mode |
| GET | `/api/SprintReport/templates` | List presentation templates |
| POST | `/api/SprintReport/preview` | Upload CSV and preview metrics/AI insights as JSON |
| POST | `/api/SprintReport/generate` | Upload CSV and download PowerPoint or HTML |

The OpenAI integration calls the Chat Completions REST endpoint with JSON output instructions, tracks token usage and estimated cost, caches successful responses, and falls back safely if an OpenAI call fails.

## References

- [Download .NET 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Swashbuckle and ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle)
- [OpenAI structured outputs](https://developers.openai.com/api/docs/guides/structured-outputs)

Content was rephrased for compliance with licensing restrictions.
