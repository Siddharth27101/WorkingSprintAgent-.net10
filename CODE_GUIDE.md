# Working Sprint Agent — Complete Code Guide

> A beginner-friendly, file-by-file walkthrough of the whole project.
> Read this top to bottom and you will understand **what every file does**, **what its
> important methods do**, and **how all the pieces connect** to turn a spreadsheet into a
> PowerPoint deck.

If you just want to *run* the app, see [`README.md`](./README.md). This document explains
*how the code works*.

---

## 1. What this application is (in one paragraph)

You upload a **Jira sprint export** (a `.csv` file or a multi-sheet `.xlsx` Excel workbook).
The app reads it, calculates sprint **metrics** (completion rate, bugs, risks, a health
score, etc.), asks **OpenAI** to write short human-readable **insights** (or falls back to
built-in rule-based text when OpenAI is not configured), and then builds a **15-slide
PowerPoint presentation** with charts and plain-English explanations that you can download
and show to stakeholders.

**Tech stack:** ASP.NET Core Web API on **.NET 10**, C#, Swagger for API docs, an optional
**Microsoft Semantic Kernel** multi-agent workflow, and a zero-dependency PowerPoint
generator that writes the `.pptx` file format by hand.

---

## 2. The 30-second mental model

```
Browser / Swagger
      │  uploads CSV or XLSX
      ▼
SprintReportController            ← the web "front door" (HTTP endpoints)
      │  calls
      ▼
ISprintReportOrchestrator         ← decides HOW to do the work
   ├─ SemanticKernelSprintReportOrchestrator  (optional AI multi-agent path)
   └─ DeterministicSprintReportOrchestrator   (default, reliable path)
                    │ uses three "agents" (steps)
                    ▼
      1) FileUploadAgent    → parses the file into Tasks + Metrics
      2) AnalysisAgent      → asks for Insights (OpenAI or fallback)
      3) PresentationAgent  → builds the .pptx file
                    │
                    ▼
      Downloadable PowerPoint (bytes) returned to the browser
```

Everything else in the project (cost monitoring, token optimization, logging) is
**supporting machinery** that watches how much the OpenAI calls cost.

---

## 3. Core C# / ASP.NET ideas used everywhere

You will see these patterns repeatedly. Understanding them once makes the whole codebase easy.

| Concept | Plain-English meaning | Where you see it |
|---|---|---|
| **Interface** (`IFoo`) | A *contract*: a list of method names with no code. Classes "implement" it. Lets us swap implementations. | `ICsvSprintDataService`, `IOpenAIService`, etc. |
| **Dependency Injection (DI)** | You never `new up` your dependencies. You ask for an interface in your constructor and the framework hands you a ready-made object. Wiring lives in `Program.cs`. | Every service/controller constructor |
| **`async` / `Task<T>`** | The method may pause (e.g. waiting on the network) without blocking the thread. `await` waits for the result. | Any method returning `Task<...>` |
| **`record`** | A short, immutable data holder. `record Point(int X, int Y)` auto-creates properties. | `SprintDataSet`, `PresentationArtifact`, etc. |
| **Options pattern** (`IOptions<T>`) | Strongly-typed settings loaded from `appsettings.json`. | `OpenAIConfiguration`, `SemanticKernelOptions` |
| **Middleware** | Code that runs for *every* HTTP request, before/after your controller. | `TokenUsageLoggingMiddleware` |
| **`CancellationToken`** | A "stop signal". If the caller cancels (browser closes), long work bails out early. | Almost every async method |

---

## 4. Project layout (folder tour)

```
WorkingSprintAgent-.net10/
├── Program.cs                     ← app startup + all dependency wiring
├── appsettings.json               ← default configuration (OpenAI keys, budgets…)
├── appsettings.Development.json   ← overrides used when running locally
├── global.json                    ← pins the .NET SDK version (10.0.100)
├── WorkingSprintAgent.csproj      ← project file: target framework + NuGet packages
├── .github/workflows/dotnet-build.yml ← CI: restores + builds on every push/PR
│
├── Controllers/
│   └── SprintReportController.cs  ← all HTTP endpoints (/api/sprintreport/*)
│
├── Middleware/
│   └── TokenUsageLoggingMiddleware.cs ← logs every request + timing
│
├── Models/                        ← plain data shapes (no logic)
│   ├── SprintTask.cs              ← one work item / issue
│   ├── SprintMetrics.cs           ← all calculated numbers about the sprint
│   ├── SprintInsights.cs          ← the 6 narrative text fields
│   ├── OpenAIConfiguration.cs     ← OpenAI settings + token/cost DTOs
│   ├── SemanticKernelOptions.cs   ← settings for the optional AI agent path
│   ├── SemanticKernelWorkflow.cs  ← state passed between AI agents
│   ├── SprintReportRequests.cs    ← shapes of the incoming HTTP forms
│   └── SprintReportWorkflow.cs    ← shapes passed between the workflow stages
│
├── Services/                      ← the actual work happens here
│   ├── CsvSprintDataService.cs    ← parse CSV/XLSX → Tasks + Metrics
│   ├── XlsxWorkbookReader.cs      ← low-level Excel (.xlsx) reader
│   ├── OpenAIService.cs           ← talks to the OpenAI HTTP API
│   ├── OpenAIInsightGenerationService.cs ← AI insights + fallback wrapper
│   ├── MockInsightGenerationService.cs   ← rule-based insights (no AI)
│   ├── PresentationBuilderService.cs     ← chooses PPTX vs HTML output
│   ├── PowerPointPresentationService.cs  ← writes the raw .pptx file
│   ├── TokenOptimizationService.cs       ← shrinks prompts to save money
│   ├── InMemoryCostMonitoringService.cs  ← tracks spend, alerts, forecasts
│   ├── TokenUsageLogger.cs               ← structured logs + analytics
│   ├── I*.cs                              ← the interfaces (contracts) for the above
│   │
│   ├── Agents/                    ← the workflow "steps"
│   │   ├── FileUploadAgent.cs     ← step 1: parse file
│   │   ├── AnalysisAgent.cs       ← step 2: get insights
│   │   ├── PresentationAgent.cs   ← step 3: build deck
│   │   └── SemanticKernelAgentFactory.cs ← builds the optional AI chat agents
│   │
│   ├── Orchestration/             ← the "conductors" that run the steps
│   │   ├── DeterministicSprintReportOrchestrator.cs ← default path
│   │   ├── SemanticKernelSprintReportOrchestrator.cs ← optional AI path
│   │   ├── LocalSprintReportFallback.cs ← safety net if AI path fails
│   │   └── ScopedSprintWorkflowStateStore.cs ← holds per-request state
│   │
│   └── Plugins/                   ← safe functions the AI agents may call
│       ├── CsvSprintPlugin.cs     ← "give me the verified metrics"
│       └── PresentationPlugin.cs  ← "build the presentation"
│
└── wwwroot/
    └── index.html                 ← the browser upload page (single file, no framework)
```

---

## 5. The two workflows (and the safety nets)

This is the single most important architectural idea, so it gets its own section.

There are **two ways** the app can produce a report, chosen by a feature flag
(`SemanticKernel:Enabled` in `appsettings.json`, **off by default**):

### Path A — Deterministic (default, always works)
`DeterministicSprintReportOrchestrator` runs three steps in a straight line:
parse → analyze → present. The "analyze" step uses OpenAI **if a key is configured**,
otherwise it uses the built-in rule-based `MockInsightGenerationService`. This path is
predictable and never depends on the AI behaving well.

### Path B — Semantic Kernel multi-agent (optional, `Enabled: true`)
`SemanticKernelSprintReportOrchestrator` runs a small "team" of AI agents that talk to
each other:
1. **Analyst** reads the *verified* metrics (via a plugin) and drafts an analysis.
2. **Coach** rewrites the draft into stakeholder-friendly insights.
3. **Reviewer** grades the result 0–1 and either approves or requests revisions.
4. **Manager** (only if the reviewer escalates) decides "try once more" or "give up".

If anything goes wrong (no API key, timeout, the reviewer never approves, an exception),
it **falls back** — first to a bounded local attempt (`LocalSprintReportFallback`), and
ultimately to the deterministic path. **You always get a deck.**

### The layered fallback, visualized
```
SemanticKernel path ──fails──▶ LocalSprintReportFallback ──uses──▶ Mock insights
        │                                                            ▲
        └── disabled or no key ──▶ Deterministic path ──▶ OpenAI ────┘ (or Mock if that fails too)
```

> **Why this matters for you right now:** because your OpenAI key is currently returning a
> fallback, the app is running the **Deterministic path + Mock insights**. That is exactly
> why improving `MockInsightGenerationService` changed what you saw in the deck.

---

## 6. End-to-end walkthrough of a real request

Let's trace **"Generate & download PowerPoint"** click by click.

1. **Browser** (`wwwroot/index.html`): the form posts a `multipart/form-data` request with
   the file + options to `POST /api/SprintReport/generate`.

2. **Middleware** (`TokenUsageLoggingMiddleware.InvokeAsync`): assigns a `RequestId`, starts
   a stopwatch, logs "request started", then calls the next step. After the response it logs
   duration + status.

3. **Controller** (`SprintReportController.GenerateSprintReport`):
   - validates the file (extension `.csv`/`.xlsx`, size ≤ 25 MB, template is one of the four).
   - opens the uploaded file as a stream.
   - calls `_orchestrator.GenerateAsync(stream, options, ...)`.
   - wraps the returned bytes in `File(...)` so the browser downloads a `.pptx`.

4. **Orchestrator** (`GenerateAsync`): because Semantic Kernel is disabled by default, the
   registered orchestrator delegates to the deterministic three-step pipeline:
   - **Step 1 — `FileUploadAgent.ProcessAsync`** calls `CsvSprintDataService.ParseDataSetAsync`
     → returns a `SprintDataSet` (`Tasks` + `Metrics`).
   - **Step 2 — `AnalysisAgent.AnalyzeAsync`** calls `IInsightGenerationService`
     (`OpenAIInsightGenerationService`) → returns `AIInsightsResponse` (the 6 narrative
     fields + token/cost metadata). Internally this tries OpenAI, else Mock.
   - **Step 3 — `PresentationAgent.CreateAsync`** calls `PresentationBuilderService`
     → `PowerPointPresentationService.CreatePresentationFromTemplate` → returns the `.pptx`
     bytes wrapped in a `PresentationArtifact`.

5. **Controller** returns the file; the browser saves it.

The **"Preview analysis"** button is the same but hits `POST /api/SprintReport/preview`,
which stops after step 2 and returns the metrics + insights as **JSON** instead of a file.

---

## 7. File-by-file reference

Each entry lists the file's **job**, its **key methods** (in plain English), and **how it
connects** to the rest.

---

### 7.1 Startup & configuration

#### `Program.cs` — the app's entry point and wiring diagram
**Job:** builds and starts the web app, and registers every service so DI can hand them out.

Step by step:
1. `WebApplication.CreateBuilder(args)` — creates the app builder.
2. Reads `OPENAI_API_KEY` from configuration/environment and copies it into `OpenAI:ApiKey`
   (so either the env var or `appsettings.json` works).
3. `Configure<OpenAIConfiguration>(...)` and `AddOptions<SemanticKernelOptions>()...ValidateOnStart()`
   — binds the JSON settings into strongly-typed objects (and validates the SK options at boot).
4. `AddControllers()`, `AddSwaggerGen(...)` — enable API endpoints + Swagger docs.
5. `AddMemoryCache()`, `AddHttpClient()` — enable caching and outbound HTTP.
6. **The DI registrations** (the "wiring"): each `AddScoped/AddSingleton<IInterface, Class>()`
   line says "when someone asks for `IInterface`, give them a `Class`." This is where the
   interfaces in section 7 get matched to their implementations (see the table in section 8).
7. `AddCors(...)` — allow browser calls from anywhere.
8. Logging setup (Debug level in Development).
9. `builder.Build()` — creates the app.
10. Middleware pipeline: developer exception page → Swagger UI → a small custom middleware
    that returns JSON system info when someone requests `/` with `Accept: application/json`
    → static files (serves `index.html`) → `UseTokenUsageLogging()` → CORS → controllers.
11. `MapGet("/api/system", ...)` — a tiny endpoint reporting whether AI is enabled and which
    workflow is active.
12. `CreateSystemInformation(...)` — helper that builds that status object.

**Connects to:** everything — this is the composition root.

#### `appsettings.json` / `appsettings.Development.json`
**Job:** configuration values. `OpenAI` section holds the API key, model (`gpt-4o-mini`),
token budget, and pricing used for cost estimates. `SemanticKernel` section holds the
optional agent workflow settings (`Enabled: false` by default).
> Note: the Development file intentionally does **not** override `OpenAI:ApiKey` — a previous
> bug where an empty value there wiped out the real key was fixed.

#### `global.json`
**Job:** pins the .NET SDK to `10.0.100` (`rollForward: latestFeature`). This is why the
project must be built with the .NET 10 SDK.

#### `WorkingSprintAgent.csproj`
**Job:** the project file. Targets `net10.0`, enables nullable reference types + implicit
usings, and references three NuGet packages: `Microsoft.SemanticKernel`,
`Microsoft.SemanticKernel.Agents.Core`, and `Swashbuckle.AspNetCore` (Swagger).

#### `.github/workflows/dotnet-build.yml`
**Job:** Continuous Integration. On every push/PR to `main`, GitHub checks out the code,
installs .NET 10, restores packages, and builds in Release. This is the reliable place the
build is verified.

---

### 7.2 Web layer

#### `Controllers/SprintReportController.cs` — the HTTP "front door"
**Job:** defines every `/api/sprintreport/*` endpoint. Controllers should be *thin*: validate
input, call a service, return the result.

Key endpoints (methods):
- **`GenerateSprintReport`** (`POST generate`) — the main one: validates the upload, runs the
  orchestrator, returns the `.pptx` file. Has `try/catch` blocks that turn different errors
  into friendly HTTP codes (400 bad data, 504 timeout, 499 cancelled, 500 other).
- **`PreviewSprintData`** (`POST preview`) — runs analysis only and returns JSON: metrics,
  the new **`SprintHealthBreakdown`**, insights, and (optionally) a token-optimization analysis.
- **`DownloadSampleCsv`** (`GET sample-csv`) — hands back a tiny built-in example CSV.
- **`GetCsvFormatInfo`** (`GET csv-format`) — documents accepted columns/sheets.
- **`HealthCheck`** (`GET health`) — service status + cost snapshot.
- **`GetAIStatus`** (`GET ai-status`) — is AI on, which model, capabilities.
- **`GetTokenUsage`**, **`GetCostDashboard`**, **`GetOptimizationRecommendations`**,
  **`ExportCostReport`**, **`GetUsageAnalytics`** — read-only views into the cost/telemetry
  services.
- **`GetAvailableTemplates`** (`GET templates`) — the 4 deck styles.

Private helpers: `ValidateGenerateRequest` / `ValidateDataFile` (input checks) and
`CalculateOptimizationScore` (a small scoring helper).

**Connects to:** `ISprintReportOrchestrator` (does the work), `IInsightGenerationService`
(status), `IPresentationBuilderService` (template list), `IOpenAIService`,
`ICostMonitoringService`, `ITokenOptimizationService` (all read-only reporting).

#### `Middleware/TokenUsageLoggingMiddleware.cs`
**Job:** runs for every request. `InvokeAsync` tags the request with a GUID, times it, logs
start/finish, and for sprint-report endpoints records a `PerformanceMetrics` entry via
`ITokenUsageLogger`. `GetEndpointName` maps the URL path to a friendly label.
The `UseTokenUsageLogging()` extension method registers it in `Program.cs`.

#### `wwwroot/index.html`
**Job:** the entire front-end in one file (HTML + CSS + vanilla JavaScript, no framework).
It shows an upload drop-zone and two buttons. On **Generate** it `fetch`es
`/api/SprintReport/generate` and triggers a file download; on **Preview** it `fetch`es
`/api/SprintReport/preview` and renders the returned metrics/summary. Helper JS functions:
`validateFile`, `createFormData`, `getErrorMessage`, `getDownloadName`, `formatBytes`,
`escapeHtml`.
> Minor note: this page still says "14-slide" in a couple of labels; the generator now
> produces 15 slides. Cosmetic only.

---

### 7.3 Models (the data shapes)

These files contain **no logic** — they are the "nouns" passed around the app.

#### `Models/SprintTask.cs`
One issue/work item: `TaskId`, `Title`, `Assignee`, `Status`, `Type`, `Priority`,
`StoryPoints`, dates, etc. Two **computed** properties do the interpretation:
- `IsDone` — true if status is Done/Closed/Completed/Resolved/Finished.
- `IsBlocked` — true if flagged, or status is Blocked/Impediment/On Hold, or a critical
  unfinished item.

#### `Models/SprintMetrics.cs`
The big "results" object — every number the app calculates: totals, completion rates,
`SprintHealthScore`, bug counts, risks, dictionaries like `TasksByStatus`, and the workflow
signals added recently (`CarryOverTasks`, `NotStartedTasks`, `TopContributorSharePercent`,
`DefectDensityPercent`, `AverageCycleTimeDays`, `HealthBreakdown`, …). Also defines helper
classes: `AssigneeLoad` (per-person load), `MetricPoint` (a chart data point with an optional
comparison value), `RiskMetric`, and `HealthComponent` (one line of the transparent health
score, e.g. "Completion rate +27").

#### `Models/SprintInsights.cs`
The six narrative fields shown on the slides: `ExecutiveSummary`, `KeyHighlights`,
`RisksAndBlockers`, `Recommendations`, `TeamPerformanceNarrative`, `NextSprintFocus`.

#### `Models/OpenAIConfiguration.cs`
`OpenAIConfiguration` (settings), plus two DTOs used everywhere: `TokenUsageStats` (tokens +
cost + timing for one call) and `AIInsightsResponse` (the insights + that usage + optimization
tips + a `FromCache` flag).

#### `Models/SemanticKernelOptions.cs`
Settings for the optional AI agent workflow (model, per-agent token cap, temperature, timeout,
max reviewer revisions, approval threshold, manager escalation threshold), with validation
attributes (`[Range]`, `[StringLength]`).

#### `Models/SemanticKernelWorkflow.cs`
The state that flows between AI agents: `SprintWorkflowState` (holds the CSV bytes, parsed data,
draft/candidate insights, review result, revision count, final presentation, and a
conversation trace), `AgentReviewResult` (approved? score? issues? revision instructions),
`AgentManagerDecision` (coach vs fallback), and `AgentConversationEntry` (one line of the trace).

#### `Models/SprintReportRequests.cs`
The shapes of the incoming HTTP forms: `GenerateSprintReportRequest` (file + sprint name +
output format + template + company) and `PreviewSprintDataRequest` (file + name + optimization
flag), with validation attributes.

#### `Models/SprintReportWorkflow.cs`
Small `record`s that travel between stages: `SprintDataSet` (Tasks + Metrics),
`SprintAnalysisResult` (Data + AIResponse), `SprintReportGenerationOptions` (name/template/
company/format), `PresentationArtifact` (bytes + content-type + filename), and
`SprintReportWorkflowResult` (analysis + presentation).

---

### 7.4 Parsing layer (spreadsheet → numbers)

#### `Services/ICsvSprintDataService.cs` + `Services/CsvSprintDataService.cs`
**Job:** the heart of parsing and metric calculation. Turns a raw file stream into a
`SprintDataSet`.

Key methods:
- **`ParseDataSetAsync`** — detects whether the upload is a ZIP (`.xlsx`) or plain CSV, reads
  the rows, then calls `ComputeMetrics`. For Excel it also merges the optional analytics sheets
  via `ApplyWorkbookMetrics` and recomputes the health score.
- **`ComputeMetrics`** — the calculator. Counts done/blocked tasks, derives the work-unit label
  (story points if present, else "work items (proxy)"), builds the per-status/type/priority
  dictionaries, the per-assignee workload, velocity/burndown trend points, derived risks, then
  calls the two helpers below.
- **`ComputeFlowAndConcentration`** — the newer signals: in-progress/not-started/carry-over
  counts, top-contributor share, defect density, bugs-per-contributor, and average cycle time.
- **`FinalizeHealthScore`** — computes `SprintHealthScore` **and** records the itemized
  `HealthBreakdown` (Baseline +20, +Completion, −Blocked, −Risk, −Bug severity, −Workload).
  This is what makes the score "defensible" on the slide.
- Private helpers: `ReadCsvRowsAsync` (quote-aware CSV parser), `ParseSprintTask`,
  `ApplyWorkbookMetrics` (reads SprintSummary/Burndown/Capacity/Quality/CI-CD/Risks sheets),
  `BuildVelocityTrend`, `BuildBurndownTrend`, `BuildDerivedRisks`, plus lots of tiny
  number/date/percentage parsers.

**Connects to:** `XlsxWorkbookReader` (for Excel), and its output feeds every downstream step.

#### `Services/XlsxWorkbookReader.cs`
**Job:** a **dependency-free** Excel reader. An `.xlsx` file is really a ZIP of XML files, so
`Read` opens the archive, loads `workbook.xml` + relationships + the shared-strings table, and
turns each worksheet into a list of row dictionaries (`header → cell value`). It has strict size
limits (max sheets/rows/cells/bytes) to prevent malicious "zip bomb" files. `WorkbookSheet` is
the small record it returns.

---

### 7.5 Insight generation (numbers → words)

#### `Services/IInsightGenerationService.cs`
The contract for "give me sprint insights": `GenerateInsightsAsync`,
`GenerateEnhancedInsightsAsync` (with cost metadata), `IsAIEnabled`, `GetServiceStatus`. Also
defines `InsightServiceStatus`.

#### `Services/OpenAIInsightGenerationService.cs` — the smart wrapper
**Job:** the class registered for `IInsightGenerationService`. It decides **AI vs fallback**.
- `IsAIEnabled` → true only if an API key is present.
- `GenerateInsightsAsync` / `GenerateEnhancedInsightsAsync` → if AI is enabled and the daily
  token budget isn't exceeded, it calls `IOpenAIService`; on **any** exception it logs the error
  and returns the Mock service's output instead. This try/catch is why a bad key silently
  produces fallback text.

**Connects to:** `IOpenAIService` (real AI) and an internal `MockInsightGenerationService`
(fallback).

#### `Services/IOpenAIService.cs` + `Services/OpenAIService.cs` — the real OpenAI client
**Job:** actually calls the OpenAI Chat Completions HTTP API.
- **`GenerateInsightsAsync`** — the pipeline: optimize the data → build a compact prompt →
  check the cache → estimate cost → `CompleteChatAsync` → `ParseAIResponse` → record token
  usage/cost/logs → cache the result. If the client isn't configured, the budget is exceeded,
  or the call throws, it returns **`GenerateFallbackInsights`** (which now includes the real
  failure reason in its optimization suggestions so you can diagnose it).
- **`CompleteChatAsync`** — builds the JSON request (system + user messages, `max_tokens`,
  `response_format: json_object`) and POSTs it; on a non-200 it throws with the status + body.
- **`ParseAIResponse`** — deserializes the model's JSON into `SprintInsights`.
- Supporting: `GetTokenUsageAsync`, `EstimateTokenCost`, `IsDailyBudgetExceededAsync`,
  `GenerateCacheKey`, `CalculateActualCost`, `GetOptimizationRecommendations`.
- The file also defines `TokenUsageSummary` and `TokenCostEstimate` DTOs.

**Connects to:** `IHttpClientFactory` (network), `IMemoryCache` (caching),
`ITokenOptimizationService` (prompt shrinking), `ICostMonitoringService` + `ITokenUsageLogger`
(telemetry).

#### `Services/MockInsightGenerationService.cs` — the no-AI fallback
**Job:** produces deterministic, rule-based insights so the app works with **zero cost** and no
API key. Each of the six fields has a generator method: `GenerateExecutiveSummary`,
`GenerateKeyHighlights`, `GenerateRisksAndBlockers`, `GenerateRecommendations`,
`GenerateTeamPerformanceNarrative`, `GenerateNextSprintFocus`. These were recently upgraded to be
**data-driven** — they quote actual numbers (carry-over count, top-contributor share, critical
bugs, a right-sized next-sprint commitment) instead of generic advice.
> Because your OpenAI call currently fails, **this class is what actually writes your deck.**

---

### 7.6 Orchestration (the conductors)

#### `Services/Orchestration/ISprintReportOrchestrator.cs`
Contract with two methods: `AnalyzeAsync` (parse + insights, no file) and `GenerateAsync`
(the full pipeline including the `.pptx`).

#### `Services/Orchestration/DeterministicSprintReportOrchestrator.cs` — default path
**Job:** the reliable straight-line pipeline. `AnalyzeAsync` runs FileUploadAgent →
AnalysisAgent. `GenerateAsync` runs those plus PresentationAgent. No AI "thinking loop" — just
the three steps.

#### `Services/Orchestration/SemanticKernelSprintReportOrchestrator.cs` — optional AI path
**Job:** the multi-agent workflow (only used when `SemanticKernel:Enabled` is true and a key
exists — checked by `CanUseSemanticKernel`). `RunSemanticAnalysisAsync` drives the
Analyst → Coach → Reviewer (→ Manager) loop, enforcing token budgets, revision limits, and a
timeout. On any failure it calls `RunFallbackAnalysisAsync/RunFallbackGenerationAsync`, which use
`LocalSprintReportFallback` (a bounded attempt) or the deterministic orchestrator. It also builds
the `AIInsightsResponse` with a token-usage summary. Helper methods enforce strict JSON parsing
and treat all model output as untrusted.

#### `Services/Orchestration/LocalSprintReportFallback.cs`
**Job:** a **non-AI** implementation of the orchestrator used as the safety net after an agent
failure. Internally it just wires FileUploadAgent + AnalysisAgent(Mock) + PresentationAgent.

#### `Services/Orchestration/ISprintWorkflowStateStore.cs` + `ScopedSprintWorkflowStateStore.cs`
**Job:** hold per-request workflow state for the AI path. `Begin` creates a `SprintWorkflowState`
with a new GUID and stores the CSV bytes; `GetRequired` fetches it by id. It's registered
**scoped** (one per HTTP request) and uses a `ConcurrentDictionary`. Crucially, the raw CSV bytes
live here — they are **never** placed into the AI chat history (a safety/privacy measure).

---

### 7.7 Agents (the reusable workflow steps)

#### `Services/Agents/IFileUploadAgent.cs` + `FileUploadAgent.cs`
**Job:** step 1. `ProcessAsync` just calls `CsvSprintDataService.ParseDataSetAsync` and logs the
task count. Thin wrapper that gives the "parse" step a name in the pipeline.

#### `Services/Agents/IAnalysisAgent.cs` + `AnalysisAgent.cs`
**Job:** step 2. `AnalyzeAsync` calls `IInsightGenerationService.GenerateEnhancedInsightsAsync`
and returns the `AIInsightsResponse`. It logs whether it's using OpenAI or fallback.

#### `Services/Agents/IPresentationAgent.cs` + `PresentationAgent.cs`
**Job:** step 3. `CreateAsync` calls `IPresentationBuilderService` to build the deck (PowerPoint
or HTML), names the file (`SanitizeFileName` strips illegal characters), and returns a
`PresentationArtifact` (bytes + content type + filename).

#### `Services/Agents/ISemanticKernelAgentFactory.cs` + `SemanticKernelAgentFactory.cs`
**Job:** builds the four AI chat agents for Path B. `CreateAnalystAgent`, `CreateCoachAgent`,
`CreateReviewerAgent`, `CreateManagerAgent` each return a `ChatCompletionAgent` with a specific
system prompt and permissions (only the analyst may call plugins). `CreateKernel` wires a
Semantic Kernel to OpenAI using the configured model/key. All prompts explicitly instruct the
model to treat data as untrusted and return strict JSON.

---

### 7.8 Plugins (safe functions the AI may call)

#### `Services/Plugins/CsvSprintPlugin.cs`
**Job:** exposes two `[KernelFunction]`s to the analyst agent: `load_sprint_data` (parse the
file once and cache it in workflow state) and `get_verified_sprint_metrics` (return those
verified numbers as compact JSON). This is the **guardrail** that forces the AI to use real
computed metrics instead of inventing numbers. `SerializeMetrics` trims/limits the payload so it
can't blow past the context window.

#### `Services/Plugins/PresentationPlugin.cs`
**Job:** exposes `create_sprint_presentation` — but it **refuses** to run unless the workflow
state is marked `QualityApproved` and has approved insights. This ensures a deck is only built
from reviewer-approved content.

---

### 7.9 Presentation layer (numbers + words → a file)

#### `Services/IPresentationBuilderService.cs` + `PresentationBuilderService.cs`
**Job:** the front door for building output. `BuildPowerPointPresentation` delegates to the raw
PowerPoint writer; `BuildPresentation` builds a standalone **HTML** deck instead (a fallback
format); `GetPresentationSummary` describes the deck; `GetAvailableTemplates` lists the four
styles (professional/modern/corporate/minimal). The HTML builder has many small `Append...Slide`
helpers and inline CSS.
> Note: `GetPresentationSummary` still lists 14 topics; the real `.pptx` now has 15 (adds the
> Sprint Health Breakdown). Cosmetic mismatch only.

#### `Services/PowerPointPresentationService.cs` — the raw `.pptx` writer
**Job:** the most technical file. It writes a valid PowerPoint file **by hand** — a `.pptx` is a
ZIP archive of XML parts, and this class generates all of them with no Office dependency.
- **`CreatePresentationFromTemplate`** — the entry point: builds the slide list, picks a color
  theme, then writes the ZIP entries (content types, relationships, master/layout/theme, and one
  XML file per slide).
- **`CreateSlides`** — defines the 15 slides in order (Cover, Executive Summary, Dashboard,
  **Sprint Health Breakdown**, Velocity, Burndown, Story Completion, Team Productivity, Quality,
  Risk & Blockers, Scope Changes, Key Achievements, Challenges, AI Recommendations, Next Sprint).
  Each slide is a `SlideContent` record (title, body, kind, optional chart, explanation, cards).
- **`BuildSlide`** dispatches on slide kind to `BuildCover` / `BuildDashboard` / `BuildBarChart`
  / `BuildLineChart` / `BuildTextBody`.
- Chart drawing is done with primitive shapes: `BuildFilledShape`, `BuildRoundedRect`,
  `BuildLineShape`, `BuildTextShape` emit the DrawingML XML (positions are in EMUs — English
  Metric Units, 914400 per inch).
- **`BuildHealthCards` / `BuildHealthExplanation`** — turn the `HealthBreakdown` into the cards
  and equation on the new health slide.
- `GetTheme` returns the color palette for the chosen template; `EscapeXml` keeps the XML valid.

**Connects to:** consumes `SprintMetrics` + `SprintInsights`; produces raw bytes.

---

### 7.10 Cost & telemetry infrastructure (the "money meter")

These don't affect the deck; they track and optimize OpenAI spend.

#### `Services/ITokenOptimizationService.cs` + `TokenOptimizationService.cs`
**Job:** shrink data/prompts before sending to OpenAI to save tokens (= money).
`OptimizeSprintData` compresses metrics into a compact shape; `CreateOptimizedPrompt` renders it
at four aggressiveness levels (Conservative→Extreme); `AnalyzeAndRecommend` suggests savings from
usage history; `EstimateSavings`, `CompressData`, `CreateBatchedRequest` round it out. The file
also defines the related DTOs/enums (`OptimizedSprintData`, `OptimizationStrategy`, …).

#### `Services/ICostMonitoringService.cs` + `InMemoryCostMonitoringService.cs`
**Job:** record every token-usage event and answer questions about spend. `RecordUsageAsync`
stores usage (keeping the last 1000 entries); `GetCostAnalysisAsync`, `GetDashboardDataAsync`,
`CheckCostAlertsAsync`, `PredictCostsAsync`, `GetOptimizationOpportunitiesAsync`,
`ExportCostReportAsync` provide reports, alerts, and forecasts. It's registered as a **singleton**
so data survives across requests (in memory only — resets on restart). The interface file defines
the many report DTOs (`CostDashboard`, `CostAlert`, `PerformanceMetrics`, etc.).

#### `Services/ITokenUsageLogger.cs` + `TokenUsageLogger.cs`
**Job:** structured logging + analytics. `LogTokenUsageAsync`, `LogCostAlertAsync`,
`LogOptimizationEventAsync`, `LogPerformanceMetricsAsync` write rich log entries;
`GetStructuredLogsAsync` and `GenerateAnalyticsSummaryAsync` aggregate them into analytics. The
interface defines the analytics DTOs. Used by the controller's `usage-analytics` endpoint and by
the logging middleware.

---

## 8. How the connections are built (the DI map)

`Program.cs` is the "switchboard". This table shows the important
`interface → implementation` mappings and their lifetime:

| When code asks for… | DI gives it… | Lifetime |
|---|---|---|
| `ICsvSprintDataService` | `CsvSprintDataService` | Scoped |
| `IOpenAIService` | `OpenAIService` | Scoped |
| `IInsightGenerationService` | `OpenAIInsightGenerationService` | Scoped |
| `ITokenOptimizationService` | `TokenOptimizationService` | Scoped |
| `ICostMonitoringService` | `InMemoryCostMonitoringService` | **Singleton** (data persists) |
| `ITokenUsageLogger` | `TokenUsageLogger` | Scoped |
| `IPresentationBuilderService` | `PresentationBuilderService` | Scoped |
| `IFileUploadAgent` | `FileUploadAgent` | Scoped |
| `IAnalysisAgent` | `AnalysisAgent` | Scoped |
| `IPresentationAgent` | `PresentationAgent` | Scoped |
| `ISprintReportOrchestrator` | `SemanticKernelSprintReportOrchestrator` | Scoped |
| `ISprintWorkflowStateStore` | `ScopedSprintWorkflowStateStore` | Scoped |
| `ISemanticKernelAgentFactory` | `SemanticKernelAgentFactory` | Scoped |

**"Scoped"** = a fresh instance per HTTP request. **"Singleton"** = one shared instance for the
app's lifetime (used for the cost monitor so its history survives across requests).

Note that `ISprintReportOrchestrator` resolves to the **Semantic Kernel** orchestrator, but that
class internally delegates to the deterministic path whenever SK is disabled or unconfigured — so
with default settings you effectively get the deterministic pipeline, wrapped in the SK safety net.

---

## 9. Where to change things (common tasks)

| I want to… | Edit this file |
|---|---|
| Change how a metric (e.g. health score) is calculated | `Services/CsvSprintDataService.cs` |
| Change the fallback (no-AI) wording | `Services/MockInsightGenerationService.cs` |
| Change what OpenAI is asked / the prompt | `Services/OpenAIService.cs` (`GetSystemPrompt`, `CreateOptimizedPrompt`) |
| Add/remove/reorder slides or change chart text | `Services/PowerPointPresentationService.cs` (`CreateSlides`) |
| Change accepted columns/sheets | `Services/CsvSprintDataService.cs` (`ParseSprintTask`, `ApplyWorkbookMetrics`) |
| Add an API endpoint | `Controllers/SprintReportController.cs` |
| Change OpenAI model, budget, pricing | `appsettings.json` (`OpenAI` section) |
| Turn on the AI multi-agent workflow | `appsettings.json` → `SemanticKernel:Enabled: true` (+ a valid key) |
| Change the upload page | `wwwroot/index.html` |

---

## 10. Mini-glossary

- **Sprint metrics** — the calculated numbers (completion %, health score, bug counts…).
- **Insights** — the six short paragraphs of narrative text placed on the slides.
- **Orchestrator** — the class that decides the order of steps and handles failures.
- **Agent** — a named single step in the pipeline (upload / analyze / present), or an AI chat role.
- **Plugin** — a safe function the AI agents are allowed to call.
- **Token** — the unit OpenAI bills by (~4 characters). Fewer tokens = lower cost.
- **Fallback** — the safe alternative used when the preferred path fails (Mock insights /
  deterministic workflow).
- **EMU** — English Metric Unit, the coordinate unit inside PowerPoint XML (914,400 per inch).
- **Scoped/Singleton** — DI lifetimes: per-request vs shared-forever.

---

*This guide describes the code as of the latest changes (which added a transparent Sprint Health
Breakdown slide, data-driven fallback insights, and richer metrics). If you change the pipeline,
please keep this document in sync.*
