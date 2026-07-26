# Working Sprint Agent - Complete Project Explanation

> **Imagine this:** You're a team leader. Your team just finished a 2-week sprint. You have a boring spreadsheet (CSV/Excel) with all the tasks. You want to turn that into a beautiful PowerPoint presentation with smart insights - automatically! That's exactly what this project does.

---

## Table of Contents

1. [The Big Picture](#the-big-picture)
2. [How Everything Connects](#how-everything-connects)
3. [Configuration Files](#configuration-files)
4. [The Entry Point - Program.cs](#the-entry-point---programcs)
5. [Models (Data Shapes)](#models-data-shapes)
6. [Controllers (API Endpoints)](#controllers-api-endpoints)
7. [Services (The Workers)](#services-the-workers)
8. [Agents (Smart Workers)](#agents-smart-workers)
9. [Orchestrators (The Managers)](#orchestrators-the-managers)
10. [Plugins (Semantic Kernel Tools)](#plugins-semantic-kernel-tools)
11. [Middleware (The Security Guard)](#middleware-the-security-guard)
12. [Frontend (The Web Page)](#frontend-the-web-page)
13. [CI/CD (Automatic Building)](#cicd-automatic-building)
14. [Complete Flow Example](#complete-flow-example)

---

## The Big Picture


Think of this project like a **pizza restaurant**:

1. **Customer (You)** uploads a CSV/Excel file with sprint data (like ordering a pizza)
2. **Waiter (Controller)** takes your order and passes it to the kitchen
3. **Kitchen Manager (Orchestrator)** decides which chefs to use
4. **Chef 1 - File Upload Agent** reads and parses your CSV/Excel data
5. **Chef 2 - Analysis Agent** uses AI (or math rules) to generate smart insights
6. **Chef 3 - Presentation Agent** creates a beautiful 13-slide PowerPoint
7. **Customer gets the pizza (PowerPoint file)** downloaded to their computer

The restaurant has **two kitchens**:
- **Kitchen A (Deterministic)**: Always works, uses simple math rules - no AI needed
- **Kitchen B (Semantic Kernel)**: Uses multiple AI agents that review each other's work - needs an OpenAI API key

If Kitchen B fails or isn't configured, Kitchen A automatically takes over!

---

## How Everything Connects

```
USER uploads CSV/XLSX file
        |
        v
+------------------+
| SprintReport     |  <-- API Controller (the "waiter")
| Controller       |
+------------------+
        |
        v
+------------------+
| Orchestrator     |  <-- Decides which workflow to use
| (SK or Determ.)  |
+------------------+
        |
        +-----------+-----------+
        |                       |
        v                       v
+------------------+    +------------------+
| File Upload      |    | File Upload      |
| Agent            |    | (CSV Plugin)     |
+------------------+    +------------------+
        |                       |
        v                       v
+------------------+    +------------------+
| Analysis Agent   |    | Analyst Agent    |
| (OpenAI/Mock)    |    | Coach Agent      |
+------------------+    | Reviewer Agent   |
        |               | Manager Agent    |
        v               +------------------+
+------------------+            |
| Presentation     |            v
| Agent            |    +------------------+
+------------------+    | Presentation     |
        |               | Plugin           |
        v               +------------------+
+------------------+            |
| PowerPoint File  |            v
| (downloaded)     |    +------------------+
+------------------+    | PowerPoint File  |
                        +------------------+
```

---

## Configuration Files


### `global.json` - SDK Version Lock

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

**What it does:** Like telling a recipe "use THIS exact oven model." It says "use .NET 10 SDK."

**Connects to:** Every single file in the project - sets the language version.

---

### `WorkingSprintAgent.csproj` - Project Recipe Card

```xml
<TargetFramework>net10.0</TargetFramework>
<PackageReference Include="Microsoft.SemanticKernel" Version="1.78.0" />
<PackageReference Include="Microsoft.SemanticKernel.Agents.Core" Version="1.78.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.1" />
```

**What it does:** This is the "ingredients list" for the project. It says:
- "We're a web app targeting .NET 10"
- "We need Semantic Kernel (for AI agents)"
- "We need Swashbuckle (for Swagger API docs page)"

**Example:** Like a recipe card saying "You'll need: flour, eggs, butter" - without these packages, the app can't run.

**Connects to:**
- `SemanticKernelAgentFactory.cs` uses the Semantic Kernel packages
- Swagger UI at `/swagger` uses Swashbuckle

---

### `appsettings.json` - Main Configuration

**What it does:** This is the "settings panel" for the whole app. It configures:

| Setting | What It Does | Example Value |
|---------|-------------|---------------|
| `OpenAI:ApiKey` | Your OpenAI password | `sk-abc123...` |
| `OpenAI:Model` | Which AI brain to use | `gpt-4o-mini` |
| `OpenAI:MaxTokens` | Max words the AI can write | `1500` |
| `OpenAI:Temperature` | How creative the AI is (0=robot, 2=poet) | `0.3` |
| `OpenAI:EnableCaching` | Remember previous answers? | `true` |
| `OpenAI:MaxDailyTokens` | Daily spending limit | `50000` |
| `SemanticKernel:Enabled` | Use the fancy multi-agent workflow? | `false` |

**Connects to:**
- `OpenAIConfiguration.cs` - reads these values into a C# class
- `SemanticKernelOptions.cs` - reads the SemanticKernel section
- `Program.cs` - binds configuration to services
- `OpenAIService.cs` - uses API key, model, tokens settings
- `SemanticKernelSprintReportOrchestrator.cs` - checks if SK is enabled

---

### `appsettings.Development.json` - Developer Settings

**What it does:** Overrides settings ONLY when running on a developer's machine. Sets lower token limits (cheaper during testing) and enables debug logging.

**Connects to:** Same files as `appsettings.json` - it layers on top.

---


## The Entry Point - Program.cs

**File:** `Program.cs`

**What it does:** This is the **front door** of the restaurant. When you start the app, THIS file runs first. It:

1. **Reads configuration** (API keys, model settings)
2. **Registers all services** (tells the app "when someone asks for ICsvSprintDataService, give them CsvSprintDataService")
3. **Sets up middleware** (the security guard that logs every request)
4. **Configures Swagger** (the interactive API docs page)
5. **Starts listening** for requests

**Key Registration Example (Dependency Injection):**
```csharp
// "When anyone asks for ICsvSprintDataService, create a CsvSprintDataService"
builder.Services.AddScoped<ICsvSprintDataService, CsvSprintDataService>();

// "When anyone asks for ISprintReportOrchestrator, create the Semantic Kernel one"
builder.Services.AddScoped<ISprintReportOrchestrator, SemanticKernelSprintReportOrchestrator>();
```

**Think of it like:** A hotel receptionist who assigns rooms. "Oh, you need a CSV parser? Go to Room 201 (CsvSprintDataService)."

**Connects to EVERY other file** because it wires everything together:
- `SprintReportController.cs` - registered as a Controller
- All service files - registered via `AddScoped`
- `TokenUsageLoggingMiddleware.cs` - registered as middleware
- `appsettings.json` - read for configuration
- `wwwroot/index.html` - served as static files

**Special behavior at startup:**
```csharp
// Checks if OpenAI API key is present and logs a warning if not
if (string.IsNullOrWhiteSpace(openAiOptions.ApiKey))
{
    startupLogger.LogWarning("OpenAI API key NOT detected...");
}
```

---

## Models (Data Shapes)

Models are like **cookie cutters** - they define the SHAPE of data. They don't DO anything, they just describe what data looks like.

---

### `Models/SprintTask.cs` - One Task in the Sprint

**What it does:** Represents a single task from your CSV file (like "PROJ-123, Implement login, Done, John").

**Properties:**
| Property | Type | Example | What It Means |
|----------|------|---------|---------------|
| TaskId | string | "PROJ-123" | Unique ID of the task |
| Title | string | "Implement login" | What the task is about |
| Status | string | "Done" | Current state |
| Assignee | string | "John Doe" | Who's working on it |
| StoryPoints | double | 5.0 | How much effort it takes |
| IsDone | bool | true | Computed: is it finished? |
| IsBlocked | bool | false | Computed: is it stuck? |

**Smart logic built-in:**
```csharp
// Automatically figures out if a task is "done" regardless of how you spell it
public bool IsDone =>
    Status.Equals("Done", ...) ||
    Status.Equals("Closed", ...) ||
    Status.Equals("Completed", ...) ||
    Status.Equals("Resolved", ...);
```

**Connects to:**
- `CsvSprintDataService.cs` - creates SprintTask objects by parsing CSV rows
- `SprintMetrics.cs` - computed from a list of SprintTasks
- `CsvSprintPlugin.cs` - serializes task data for AI agents

---


### `Models/SprintMetrics.cs` - Sprint Statistics Dashboard

**What it does:** After parsing all tasks, we calculate statistics. This is the "report card" of the sprint.

**Key fields:**
| Field | Example | What It Means |
|-------|---------|---------------|
| TotalTasks | 12 | How many tasks total |
| CompletedTasks | 7 | How many are done |
| CompletionRatePercent | 58.3% | Success rate |
| SprintHealthScore | 72/100 | Overall health grade |
| BlockedTasks | 2 | How many are stuck |
| BugCount | 3 | How many bugs |
| TopContributor | "Alice" | Who did the most work |
| TasksByStatus | {"Done":7,"In Progress":3} | Breakdown by status |
| WorkloadByAssignee | [{Assignee:"John",...}] | Work per person |

**Contains nested classes:**
- `AssigneeLoad` - workload stats per person
- `MetricPoint` - one data point for charts (label + value)
- `HealthComponent` - one piece of the health score breakdown
- `RiskMetric` - one identified risk item

**Connects to:**
- `CsvSprintDataService.cs` - CREATES SprintMetrics from tasks
- `OpenAIService.cs` - SENDS metrics to AI for analysis
- `PresentationBuilderService.cs` - READS metrics to build slides
- `TokenOptimizationService.cs` - COMPRESSES metrics to save AI tokens
- `SprintReportController.cs` - RETURNS metrics in preview API response

---

### `Models/SprintInsights.cs` - AI-Generated Wisdom

**What it does:** The AI (or fallback rules) fills this with smart observations about the sprint.

```csharp
public class SprintInsights
{
    public string ExecutiveSummary { get; set; }        // "Sprint 15 completed 58% of tasks..."
    public List<string> KeyHighlights { get; set; }    // ["Completed 7 of 12 tasks", ...]
    public List<string> RisksAndBlockers { get; set; } // ["2 tasks are blocked", ...]
    public List<string> Recommendations { get; set; }  // ["Reduce scope next sprint", ...]
    public string TeamPerformanceNarrative { get; set; } // "A team of 8 members..."
    public string NextSprintFocus { get; set; }        // "Focus on clearing blockers..."
}
```

**Think of it like:** A sports commentator analyzing a game. "The team scored 7 goals (tasks done), but 2 players were injured (blocked)."

**Connects to:**
- `OpenAIService.cs` - AI fills this by parsing JSON from GPT
- `MockInsightGenerationService.cs` - fallback rules fill this with math-based insights
- `PresentationBuilderService.cs` - uses these to create presentation slides
- `SprintReportController.cs` - returns these in preview responses

---

### `Models/OpenAIConfiguration.cs` - AI Settings + Response Types

**What it does:** Three things in one file:

1. **OpenAIConfiguration** - Settings class mapped from `appsettings.json`
2. **TokenUsageStats** - Records how many tokens each AI call used and the cost
3. **AIInsightsResponse** - Wraps insights + token usage + optimization suggestions together

**Example flow:**
```
appsettings.json "OpenAI:Model" = "gpt-4o-mini"
    --> OpenAIConfiguration.Model = "gpt-4o-mini"
        --> OpenAIService reads this to know which model to call
```

**Connects to:**
- `Program.cs` - binds the config section to this class
- `OpenAIService.cs` - reads ApiKey, Model, MaxTokens, Temperature
- `OpenAIInsightGenerationService.cs` - checks ApiKey to know if AI is available
- `SemanticKernelAgentFactory.cs` - uses ApiKey and Model for agent creation
- `InMemoryCostMonitoringService.cs` - stores TokenUsageStats

---


### `Models/SemanticKernelOptions.cs` - AI Agent Workflow Settings

**What it does:** Controls the optional "fancy" multi-agent workflow (Kitchen B).

| Setting | Default | What It Means |
|---------|---------|---------------|
| Enabled | false | Is the multi-agent workflow turned on? |
| MaxReviewerRevisions | 2 | How many times the reviewer can ask for fixes |
| ReviewerApprovalThreshold | 0.8 | Minimum score (out of 1.0) to approve insights |
| EnableManagerSelection | true | Can a "manager" agent break ties? |
| TimeoutSeconds | 90 | Max seconds before giving up and using fallback |

**Connects to:**
- `Program.cs` - binds config and validates settings at startup
- `SemanticKernelSprintReportOrchestrator.cs` - reads Enabled flag to decide workflow
- `SemanticKernelAgentFactory.cs` - reads Model, Temperature, MaxTokensPerAgent

---

### `Models/SemanticKernelWorkflow.cs` - Multi-Agent Workflow State

**What it does:** Tracks what's happening DURING the multi-agent workflow. Think of it as a "project clipboard" that gets passed between agents.

**Key classes:**
- **SprintWorkflowState** - The clipboard with all data: CSV content, parsed data, analyst insights, review results, etc.
- **AgentReviewResult** - The reviewer's verdict: approved? score? issues?
- **AgentManagerDecision** - The manager's call: "try again" or "give up and use fallback"
- **AgentConversationEntry** - A log entry of what each agent said

**Example conversation flow:**
```
AgentConversationEntry("SprintDataAnalyst", "analysis", "Produced a verified-metrics analysis draft.")
AgentConversationEntry("SprintCoach", "coaching", "Converted the analyst draft into stakeholder-ready insights.")
AgentConversationEntry("QualityReviewer", "review", "Approved=True; score=0.85; issues=0.")
```

**Connects to:**
- `SemanticKernelSprintReportOrchestrator.cs` - creates and updates the state
- `ScopedSprintWorkflowStateStore.cs` - stores the state for the request lifetime
- `CsvSprintPlugin.cs` - reads CsvContent from state to parse data
- `PresentationPlugin.cs` - reads approved insights to create presentation

---

### `Models/SprintReportRequests.cs` - API Request Shapes

**What it does:** Defines what the user must send when calling the API.

**GenerateSprintReportRequest** (for `/api/sprintreport/generate`):
```
- CsvFile: [Required] The uploaded CSV or Excel file
- SprintName: [Optional] "Sprint 15"
- Template: [Optional] Professional/Modern/Corporate/Minimal
- CompanyName: [Optional] "Acme Corp"
```

**PreviewSprintDataRequest** (for `/api/sprintreport/preview`):
```
- CsvFile: [Required] The uploaded CSV or Excel file
- SprintName: [Optional] "Sprint 15"
- IncludeOptimization: [Optional] Show cost savings analysis?
```

**Connects to:**
- `SprintReportController.cs` - receives these as `[FromForm]` parameters

---

### `Models/SprintReportWorkflow.cs` - Workflow Result Types

**What it does:** Defines the "finished products" passed between workflow stages.

```
SprintDataSet = (Tasks list, Metrics) -- output of parsing
SprintAnalysisResult = (DataSet, AIResponse) -- output of analysis
SprintReportGenerationOptions = (SprintName, Template, CompanyName, Format) -- options
PresentationArtifact = (byte[] Content, ContentType, FileName) -- the final PowerPoint
SprintReportWorkflowResult = (Analysis, Presentation) -- everything together
```

**Think of it like:** Assembly line boxes. Box 1 has raw parts. Box 2 has assembled parts + quality report. Box 3 has the final packaged product.

**Connects to:**
- `ISprintReportOrchestrator.cs` - returns these types
- `DeterministicSprintReportOrchestrator.cs` - creates these
- `SemanticKernelSprintReportOrchestrator.cs` - creates these
- `SprintReportController.cs` - unwraps these to send to user

---


## Controllers (API Endpoints)

### `Controllers/SprintReportController.cs` - The Waiter

**What it does:** This is the ONLY controller. It's the "waiter" that takes orders from users and delivers results. It defines all the API endpoints (URLs you can call).

**Endpoints:**

| Method | URL | What It Does |
|--------|-----|--------------|
| POST | `/api/sprintreport/generate` | Upload CSV -> Get PowerPoint file |
| POST | `/api/sprintreport/preview` | Upload CSV -> Get JSON analysis (no file) |
| GET | `/api/sprintreport/sample-csv` | Download a sample CSV to test with |
| GET | `/api/sprintreport/csv-format` | Get documentation on CSV format |
| GET | `/api/sprintreport/health` | Check if the service is healthy |
| GET | `/api/sprintreport/ai-status` | Check if AI is configured and working |
| GET | `/api/sprintreport/token-usage` | See how many AI tokens you've used |
| GET | `/api/sprintreport/cost-dashboard` | See cost monitoring data |
| GET | `/api/sprintreport/optimization-recommendations` | Get cost-saving tips |
| GET | `/api/sprintreport/templates` | List available presentation styles |
| GET | `/api/sprintreport/export-cost-report` | Export cost data as CSV/JSON |
| GET | `/api/sprintreport/usage-analytics` | Detailed usage analytics |

**Example: How `/generate` works step by step:**

```
1. User uploads "my-sprint.csv" with template "Professional"
2. Controller validates: Is file present? Is it .csv or .xlsx? Is it under 25MB?
3. Controller calls: _orchestrator.GenerateAsync(stream, options, cancellationToken)
4. Orchestrator returns: SprintReportWorkflowResult (contains the PowerPoint bytes)
5. Controller returns: File(bytes, "application/...pptx", "Sprint_Report_Sprint15_20240115.pptx")
6. User's browser downloads the PowerPoint file!
```

**Error handling:** If anything goes wrong:
- Invalid file -> 400 Bad Request with helpful message
- Timeout -> 504 Gateway Timeout
- Cancelled -> 499 Client Closed
- Any other error -> 500 Internal Server Error

**Connects to:**
- `ISprintReportOrchestrator` - the main workflow engine
- `IInsightGenerationService` - for AI status checks
- `IPresentationBuilderService` - for template listing
- `IOpenAIService` - for token usage stats
- `ICostMonitoringService` - for cost dashboard data
- `ITokenOptimizationService` - for optimization analysis

---

## Services (The Workers)

Services do the actual WORK. They're like specialized workers in the restaurant kitchen.

---

### `Services/CsvSprintDataService.cs` - The Data Parser

**What it does:** Takes your raw CSV or Excel file and turns it into structured data (SprintTask objects + SprintMetrics).

**Think of it like:** A translator who reads a foreign menu and writes it in English.

**Key capabilities:**
1. **CSV Parsing** - reads comma-separated files with smart quoting support
2. **Excel (.xlsx) Parsing** - reads multi-sheet workbooks (uses `XlsxWorkbookReader`)
3. **Column Mapping** - understands many column name variations:
   - "TaskId" OR "ID" OR "Key" OR "IssueKey" -> all map to TaskId
   - "StoryPoints" OR "Points" OR "Estimate" -> all map to StoryPoints
4. **Metrics Computation** - calculates completion rates, health scores, workload distribution
5. **Multi-sheet support** - reads optional sheets: SprintSummary, Burndown, Capacity, Quality, CI-CD, Risks

**Example of how it handles different column names:**
```csharp
// Your CSV header might say any of these - they all work!
TaskId = Get(row, "taskid", "id", "key", "issuekey", "issueid");
StoryPoints = ParseNumber(Get(row, "storypoints", "points", "estimate", ...));
```

**Health Score calculation:**
The service computes a 0-100 health score based on:
- Completion rate (higher = better)
- Blocked items (more = worse)
- Bug density (more = worse)
- Scope change (more = worse)
- Build failures (more = worse)

**Connects to:**
- `FileUploadAgent.cs` - calls `ParseDataSetAsync` to get data
- `CsvSprintPlugin.cs` - calls `ParseDataSetAsync` in Semantic Kernel workflow
- `XlsxWorkbookReader.cs` - used internally for .xlsx file support
- `SprintTask.cs` - creates these objects
- `SprintMetrics.cs` - creates this object

---


### `Services/XlsxWorkbookReader.cs` - The Excel Reader

**What it does:** Reads `.xlsx` Excel files WITHOUT needing Microsoft Office installed. It directly reads the ZIP/XML format that Excel files actually are.

**Fun fact:** An .xlsx file is actually a ZIP archive containing XML files!

**Safety limits:**
- Max 25 sheets per workbook
- Max 100,000 rows per sheet
- Max 2,000,000 cells per sheet
- Max 150 MB expanded size

**Connects to:**
- `CsvSprintDataService.cs` - calls `XlsxWorkbookReader.Read(stream)` when file is .xlsx

---

### `Services/OpenAIService.cs` - The AI Brain

**What it does:** Talks to OpenAI's GPT API to generate smart insights about your sprint data.

**Think of it like:** Calling a consultant on the phone, describing your sprint numbers, and getting advice back.

**How it makes an API call:**

```
1. Check if API key exists (if not, use fallback immediately)
2. Check if daily budget is exceeded (if so, use fallback)
3. Compress the sprint data to save tokens (costs less money!)
4. Check cache - have we seen identical data before? (if yes, return cached result)
5. Build the prompt: system instructions + compressed sprint data
6. Call OpenAI API: POST https://api.openai.com/v1/chat/completions
7. Parse the JSON response into SprintInsights
8. Track token usage and cost
9. Cache the result for next time
10. Return AIInsightsResponse (insights + cost info + optimization suggestions)
```

**The system prompt tells GPT:**
```
"You are an expert Agile coach. Return JSON with: ExecutiveSummary, KeyHighlights,
RisksAndBlockers, Recommendations, TeamPerformanceNarrative, NextSprintFocus"
```

**Fallback behavior:** If anything goes wrong (no API key, budget exceeded, API error, parsing failure), it falls back to `MockInsightGenerationService` which generates rule-based insights.

**Cost tracking:**
```csharp
// Every API call is tracked
var tokenStats = new TokenUsageStats {
    InputTokens = chatCompletion.InputTokens,    // 500 tokens
    OutputTokens = chatCompletion.OutputTokens,  // 300 tokens
    EstimatedCost = 0.0002m,                     // $0.0002
    Model = "gpt-4o-mini"
};
```

**Connects to:**
- `OpenAIInsightGenerationService.cs` - wraps this service with fallback logic
- `AnalysisAgent.cs` - uses this (via IInsightGenerationService) to get insights
- `TokenOptimizationService.cs` - used to compress data before sending
- `InMemoryCostMonitoringService.cs` - receives usage records
- `TokenUsageLogger.cs` - receives detailed usage logs
- `IMemoryCache` - caches results to avoid duplicate API calls
- `appsettings.json` - reads configuration (model, tokens, temperature)

---

### `Services/OpenAIInsightGenerationService.cs` - The AI Manager

**What it does:** Wraps the OpenAI service with smart fallback logic. Like a manager who tries the expert first, but has a backup plan.

**Decision flow:**
```
Is API key configured?
  NO  -> Use MockInsightGenerationService (free, rule-based)
  YES -> Is daily budget exceeded?
    YES -> Use MockInsightGenerationService
    NO  -> Try OpenAI
      SUCCESS -> Return AI insights
      FAILURE -> Use MockInsightGenerationService
```

**Also provides:** `GetServiceStatus()` which tells the controller what's currently active (AI or Mock).

**Connects to:**
- `IOpenAIService (OpenAIService.cs)` - the actual AI caller
- `MockInsightGenerationService.cs` - the fallback
- `AnalysisAgent.cs` - is the implementation of `IInsightGenerationService`
- `SprintReportController.cs` - calls `GetServiceStatus()` for health/ai-status endpoints

---

### `Services/MockInsightGenerationService.cs` - The Backup Brain

**What it does:** Generates insights using MATH RULES instead of AI. It's free (no API calls) but less creative.

**Example rule-based insight:**
```csharp
// If completion rate < 70%, generate this recommendation:
if (metrics.CompletionRatePercent < 70)
{
    var suggestedCommitment = Math.Round(metrics.CompletedTasks * 1.1);
    recommendations.Add($"Reduce next sprint commitment to ~{suggestedCommitment} issues");
}
```

**Always available** - works without any API key or internet connection.

**Connects to:**
- `OpenAIInsightGenerationService.cs` - uses this as fallback
- `LocalSprintReportFallback.cs` - uses this directly (guaranteed no AI)
- `OpenAIService.cs` - uses this internally when API fails

---


### `Services/TokenOptimizationService.cs` - The Cost Saver

**What it does:** Makes the data SMALLER before sending it to AI, so you spend fewer tokens (= less money).

**Think of it like:** Instead of reading a full 10-page document to a consultant on the phone, you read a 2-page summary. Same information, less phone time (cheaper!).

**Optimization strategies:**

| Strategy | How It Saves Money | Savings |
|----------|-------------------|---------|
| Data Compression | Shorten names, abbreviate statuses | 30% |
| Prompt Optimization | Remove unnecessary words | 20% |
| Response Caching | Don't ask twice for same data | 40% |
| Batch Processing | Group similar requests | 15% |
| Smart Filtering | Remove low-value data | 25% |
| Model Downgrade | Use cheaper AI model | 70% |

**Example compression:**
```
BEFORE: "Sprint 2024 Q3 Team Alpha" (26 chars)
AFTER:  "S24Q3A" (6 chars)

BEFORE: {"Status": "In Progress"} 
AFTER:  {"Status": "prog"}

BEFORE: {"Assignee": "John Smith"}
AFTER:  {"Assignee": "J.Smith"}
```

**Connects to:**
- `OpenAIService.cs` - calls `OptimizeSprintData()` before every AI call
- `SprintReportController.cs` - calls `EstimateSavings()` for preview optimization info

---

### `Services/PresentationBuilderService.cs` - The Slide Designer (HTML)

**What it does:** Creates presentations. Has two output modes:
1. **PowerPoint** - delegates to `PowerPointPresentationService` (the real deal)
2. **HTML** - creates a standalone HTML page with CSS styling (simpler alternative)

**Slide topics in every presentation:**
1. Cover (title + sprint name)
2. Executive Summary
3. Sprint Metrics Dashboard
4. Sprint Health Breakdown
5. Velocity Trend
6. Story Completion
7. Team Productivity
8. Team Workload & Delivery
9. Quality Metrics
10. Risk & Blockers
11. Challenges
12. AI Recommendations
13. Next Sprint Action Items

**Also provides:** Template information (Professional, Modern, Corporate, Minimal).

**Connects to:**
- `PresentationAgent.cs` - calls `BuildPowerPointPresentation()` or `BuildPresentation()`
- `SprintReportController.cs` - calls `GetAvailableTemplates()` for template listing
- `PowerPointPresentationService.cs` - delegated to for actual .pptx creation

---

### `Services/PowerPointPresentationService.cs` - The PowerPoint Factory

**What it does:** Creates REAL .pptx files using raw ZIP + XML construction. No Microsoft Office needed!

**How it works (simplified):**
```
1. Creates a ZIP archive (because .pptx IS a ZIP file)
2. Writes XML files inside the ZIP:
   - [Content_Types].xml - describes file contents
   - _rels/.rels - relationships between parts
   - ppt/presentation.xml - the main presentation
   - ppt/slides/slide1.xml through slide13.xml - each slide
   - ppt/theme/theme1.xml - colors and fonts
3. Each slide has: title, content shapes (text, tables, charts)
4. Color themes change based on template (Professional=blue, Modern=purple, etc.)
5. Returns the ZIP bytes as a byte[] array
```

**Connects to:**
- `PresentationBuilderService.cs` - calls `CreatePresentationFromTemplate()`
- `SprintMetrics.cs` - reads data to build chart slides
- `SprintInsights.cs` - reads text for recommendation slides

---

### `Services/InMemoryCostMonitoringService.cs` - The Accountant

**What it does:** Tracks all AI spending in memory. Like a bookkeeper recording every purchase.

**Key functions:**
- `RecordUsageAsync()` - log a new AI call and its cost
- `GetDashboardDataAsync()` - get today/week/month costs
- `CheckCostAlertsAsync()` - detect if spending is too high
- `PredictCostsAsync()` - forecast future spending
- `GetOptimizationOpportunitiesAsync()` - suggest cost savings
- `ExportCostReportAsync()` - export data as CSV or JSON

**Note:** "In-memory" means all data is lost when the app restarts. Good for development, not production.

**Connects to:**
- `OpenAIService.cs` - sends usage records here after every API call
- `SemanticKernelSprintReportOrchestrator.cs` - records agent invocation costs
- `SprintReportController.cs` - reads dashboard data for cost endpoints

---

### `Services/TokenUsageLogger.cs` - The Detailed Diary

**What it does:** A more detailed logger than the cost monitor. Records everything with structured logging for analytics.

**Records:**
- Every token usage event (with context like sprint name, task count)
- Optimization events (when data was compressed, how much was saved)
- Performance metrics (response times, success rates)
- Can generate analytics summaries over time ranges

**Connects to:**
- `OpenAIService.cs` - calls `LogTokenUsageAsync()` and `LogOptimizationEventAsync()`
- `TokenUsageLoggingMiddleware.cs` - calls `LogPerformanceMetricsAsync()`
- `SprintReportController.cs` - calls `GenerateAnalyticsSummaryAsync()` for usage-analytics endpoint

---


## Agents (Smart Workers)

Agents are like SPECIALIZED workers. Each one has ONE job and does it well.

---

### `Services/Agents/FileUploadAgent.cs` - The File Reader

**What it does:** Takes a raw file stream and produces structured sprint data.

**Think of it like:** A secretary who takes a pile of papers and organizes them into neat folders.

```csharp
// Input: raw file bytes
// Output: organized SprintDataSet (tasks + metrics)
public async Task<SprintDataSet> ProcessAsync(Stream csvStream, string? sprintName, ...)
{
    return await _csvService.ParseDataSetAsync(csvStream, sprintName, cancellationToken);
}
```

**Interface:** `IFileUploadAgent`

**Connects to:**
- `ICsvSprintDataService (CsvSprintDataService)` - does the actual parsing work
- `DeterministicSprintReportOrchestrator.cs` - calls this agent first
- `LocalSprintReportFallback.cs` - calls this agent first

---

### `Services/Agents/AnalysisAgent.cs` - The Analyst

**What it does:** Takes sprint metrics and produces AI-powered (or rule-based) insights.

**Think of it like:** A data analyst who looks at numbers and writes a report.

```csharp
// Input: SprintMetrics (numbers)
// Output: AIInsightsResponse (text insights + cost info)
public async Task<AIInsightsResponse> AnalyzeAsync(SprintMetrics metrics, ...)
{
    return await _insightService.GenerateEnhancedInsightsAsync(metrics, cancellationToken);
}
```

**Interface:** `IAnalysisAgent`

**Connects to:**
- `IInsightGenerationService (OpenAIInsightGenerationService)` - generates the insights
- `DeterministicSprintReportOrchestrator.cs` - calls this agent second
- `LocalSprintReportFallback.cs` - uses this with MockInsightGenerationService

---

### `Services/Agents/PresentationAgent.cs` - The Designer

**What it does:** Takes metrics + insights and creates a downloadable presentation file.

**Think of it like:** A graphic designer who takes a report and makes it into a beautiful slide deck.

```csharp
// Input: SprintMetrics + SprintInsights + options
// Output: PresentationArtifact (bytes + filename + content type)
public Task<PresentationArtifact> CreateAsync(
    SprintMetrics metrics,
    SprintInsights insights,
    SprintReportGenerationOptions options, ...)
```

**Logic:**
- If format is PowerPoint -> calls `BuildPowerPointPresentation()` -> .pptx file
- If format is HTML -> calls `BuildPresentation()` -> .html file

**Interface:** `IPresentationAgent`

**Connects to:**
- `IPresentationBuilderService (PresentationBuilderService)` - builds the actual file
- `DeterministicSprintReportOrchestrator.cs` - calls this agent last
- `PresentationPlugin.cs` - calls this in the Semantic Kernel workflow

---

### `Services/Agents/SemanticKernelAgentFactory.cs` - The AI Team Builder

**What it does:** Creates the AI agents used in the Semantic Kernel multi-agent workflow. Each agent has specific instructions and capabilities.

**Think of it like:** An HR department that hires specialists for a project.

**Agents it creates:**

| Agent | Role | Temperature | Can Use Tools? |
|-------|------|-------------|----------------|
| SprintDataAnalyst | Reads verified metrics, produces factual JSON | 0.0 (very precise) | YES (SprintData plugin) |
| SprintCoach | Improves analyst's draft into actionable insights | 0.2 (slightly creative) | NO |
| QualityReviewer | Checks if insights match metrics, scores quality | 0.0 (very strict) | NO |
| WorkflowManager | Decides "try again" or "give up" when review fails | 0.0 (decisive) | NO |

**Security features:**
- All data is marked as "untrusted" in prompts (prevents prompt injection attacks)
- Each agent has strict JSON output format requirements
- Function calling is only allowed for the Analyst (to call the metrics plugin)

**How a Kernel is built:**
```csharp
var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(model, apiKey, organization, httpClient);
return builder.Build();
```

**Connects to:**
- `SemanticKernelSprintReportOrchestrator.cs` - calls `CreateAnalystAgent()`, `CreateCoachAgent()`, etc.
- `CsvSprintPlugin.cs` - attached to the Analyst agent as a tool
- `SemanticKernelOptions` - reads model, temperature, max tokens
- `OpenAIConfiguration` - reads API key

---


## Orchestrators (The Managers)

Orchestrators coordinate the agents. They decide WHO does WHAT and in WHAT ORDER.

---

### `Services/Orchestration/ISprintReportOrchestrator.cs` - The Manager Contract

**What it does:** Defines what ANY orchestrator must be able to do:

```csharp
public interface ISprintReportOrchestrator
{
    // Analyze data without creating a presentation
    Task<SprintAnalysisResult> AnalyzeAsync(Stream csvStream, string? sprintName, ...);
    
    // Full workflow: analyze AND create presentation
    Task<SprintReportWorkflowResult> GenerateAsync(Stream csvStream, options, ...);
}
```

**Three classes implement this:**
1. `DeterministicSprintReportOrchestrator` - simple, always works
2. `SemanticKernelSprintReportOrchestrator` - fancy multi-agent, needs API key
3. `LocalSprintReportFallback` - emergency fallback, no AI at all

---

### `Services/Orchestration/DeterministicSprintReportOrchestrator.cs` - Simple Kitchen

**What it does:** The reliable "Kitchen A." Three steps, always works, no surprises.

```
Step 1: FileUploadAgent.ProcessAsync(stream) -> SprintDataSet
Step 2: AnalysisAgent.AnalyzeAsync(metrics)  -> AIInsightsResponse
Step 3: PresentationAgent.CreateAsync(metrics, insights, options) -> PresentationArtifact
Done!
```

**Think of it like:** A simple recipe: mix ingredients, bake, serve. No fancy techniques needed.

**Connects to:**
- `IFileUploadAgent (FileUploadAgent)` - step 1
- `IAnalysisAgent (AnalysisAgent)` - step 2 (may use AI or fallback internally)
- `IPresentationAgent (PresentationAgent)` - step 3
- `SemanticKernelSprintReportOrchestrator.cs` - used as fallback when SK fails

---

### `Services/Orchestration/SemanticKernelSprintReportOrchestrator.cs` - Fancy Kitchen

**What it does:** The advanced "Kitchen B." Uses multiple AI agents that REVIEW each other's work for higher quality output.

**Full workflow:**
```
1. Check: Is SemanticKernel enabled AND is API key present?
   NO -> Fall back to DeterministicSprintReportOrchestrator
   YES -> Continue...

2. Start timeout timer (90 seconds max)

3. CsvSprintPlugin.LoadSprintDataAsync() -> Parse CSV, store in workflow state

4. SprintDataAnalyst agent -> Calls get_verified_sprint_metrics plugin -> Produces draft insights

5. SprintCoach agent -> Takes draft + metrics -> Produces polished candidate insights

6. QualityReviewer agent -> Compares candidate vs metrics -> Scores it (0-1)
   
7. LOOP (max 2 revisions):
   - If score >= 0.8 AND approved AND no issues -> DONE, move to presentation
   - If score <= 0.65 OR escalateToManager -> Invoke WorkflowManager
     - Manager says "coach" -> Go back to step 5 with reviewer feedback
     - Manager says "fallback" -> Give up, use deterministic workflow
   - Else -> Go back to step 5 with reviewer feedback

8. PresentationPlugin.CreateSprintPresentationAsync() -> Build the PowerPoint

9. Return the result!
```

**If ANYTHING goes wrong** (timeout, API error, bad JSON from AI, budget exceeded):
-> Falls back to deterministic workflow with a bounded timeout

**Connects to:**
- `DeterministicSprintReportOrchestrator.cs` - fallback
- `LocalSprintReportFallback.cs` - safe fallback (guaranteed no AI)
- `ISprintWorkflowStateStore` - stores workflow state
- `CsvSprintPlugin` - parses data
- `PresentationPlugin` - creates presentation
- `ISemanticKernelAgentFactory` - creates AI agents
- `SemanticKernelOptions` - reads settings
- `OpenAIConfiguration` - checks API key
- `ICostMonitoringService` - records costs, checks budget

---

### `Services/Orchestration/LocalSprintReportFallback.cs` - Emergency Backup

**What it does:** A guaranteed-safe fallback that NEVER calls AI. Uses `MockInsightGenerationService` directly.

**When it's used:** After the Semantic Kernel workflow fails AND the regular deterministic fallback might also time out.

**Think of it like:** The emergency generator in a hospital. If main power fails AND backup power fails, THIS always works.

**Connects to:**
- `FileUploadAgent` - parses data
- `MockInsightGenerationService` - generates rule-based insights (zero cost)
- `PresentationAgent` - creates presentation
- `SemanticKernelSprintReportOrchestrator.cs` - calls this as last resort

---

### `Services/Orchestration/ISprintWorkflowStateStore.cs` + `ScopedSprintWorkflowStateStore.cs`

**What it does:** A temporary storage locker that lives only during ONE HTTP request.

**Think of it like:** A locker at a gym. You put your stuff in when you arrive, take it out when you leave. Next visitor gets a clean locker.

```csharp
// Start a new workflow - get a locker
var state = _stateStore.Begin(csvBytes, options);  // Returns state with unique WorkflowId

// Later, plugins retrieve the state using the workflow ID
var state = _stateStore.GetRequired(workflowId);   // Same locker, same stuff inside
```

**Why it exists:** The CSV data and parsed results need to be shared between plugins (CsvSprintPlugin, PresentationPlugin) during the Semantic Kernel workflow, but we don't want to pass giant byte arrays through AI chat history.

**Connects to:**
- `SemanticKernelSprintReportOrchestrator.cs` - calls `Begin()` to start
- `CsvSprintPlugin.cs` - calls `GetRequired()` to access CSV data
- `PresentationPlugin.cs` - calls `GetRequired()` to access approved insights

---


## Plugins (Semantic Kernel Tools)

Plugins are TOOLS that AI agents can use. They keep calculations DETERMINISTIC (exact, not AI-guessed).

---

### `Services/Plugins/CsvSprintPlugin.cs` - Data Access Tool for AI

**What it does:** Gives the AI analyst agent a safe way to read verified sprint metrics. The AI CANNOT make up numbers because it MUST call this plugin to get real data.

**Functions exposed to AI:**

| Function Name | What It Does |
|---------------|--------------|
| `load_sprint_data` | Parses CSV from workflow state, stores SprintDataSet |
| `get_verified_sprint_metrics` | Returns previously parsed metrics as JSON |

**Security:** The AI agent sees data like this:
```json
{
  "SprintName": "Sprint 15",
  "TotalTasks": 12,
  "CompletedTasks": 7,
  "CompletionRatePercent": 58.3,
  "TeamMemberCount": 8,
  "BlockedTaskTitlesShown": ["TASK-005: Performance Optimization", "TASK-010: Third-party Integration"]
}
```

**Safety limit:** If serialized metrics exceed 24,000 characters, it throws an error (prevents context overflow).

**Connects to:**
- `ICsvSprintDataService` - parses the actual data
- `ISprintWorkflowStateStore` - stores/retrieves state
- `SemanticKernelAgentFactory.cs` - attached to the Analyst agent: `kernel.Plugins.AddFromObject(_csvPlugin, "SprintData")`
- `SemanticKernelSprintReportOrchestrator.cs` - called directly to load data

---

### `Services/Plugins/PresentationPlugin.cs` - Presentation Tool for AI

**What it does:** Lets the orchestrator trigger presentation creation from the approved workflow state.

**Function exposed:**

| Function Name | What It Does |
|---------------|--------------|
| `create_sprint_presentation` | Creates the final PowerPoint from approved insights |

**Safety checks before creating:**
- Sprint data must be loaded (not null)
- Quality review must be approved (`state.QualityApproved == true`)
- Candidate insights must exist

**Connects to:**
- `IPresentationAgent (PresentationAgent)` - does the actual PowerPoint creation
- `ISprintWorkflowStateStore` - reads the approved state
- `SemanticKernelSprintReportOrchestrator.cs` - calls this after quality approval

---

## Middleware (The Security Guard)

### `Middleware/TokenUsageLoggingMiddleware.cs` - The Request Logger

**What it does:** Sits between EVERY request and response. Like a security guard who writes down who came in, how long they stayed, and if anything went wrong.

**What it logs for every API request:**
- Request ID (unique identifier)
- HTTP method and path (POST /api/sprintreport/generate)
- Duration (how many milliseconds)
- Status code (200 OK, 400 Bad Request, 500 Error)
- User agent (what browser/tool is making the request)
- IP address

**Special logging for sprint report endpoints:**
- File size uploaded
- Query parameters
- Performance metrics (response time, cost efficiency)

**How it's registered:**
```csharp
// In Program.cs:
app.UseTokenUsageLogging();  // This activates the middleware
```

**Think of it like:** A security camera + stopwatch at a store entrance. Records who came, when they left, and whether they bought anything.

**Connects to:**
- `Program.cs` - registered via `UseTokenUsageLogging()` extension method
- `ITokenUsageLogger (TokenUsageLogger)` - sends performance metrics
- Every request to `/api/*` passes through this middleware

---

## Frontend (The Web Page)

### `wwwroot/index.html` - The User Interface

**What it does:** A beautiful single-page web application that lets users:
1. Upload a CSV/Excel file using drag-and-drop
2. Choose a presentation template
3. Click "Generate" to get a PowerPoint
4. See sprint metrics and AI status

**How it talks to the backend:**
```javascript
// When user clicks "Generate Report":
fetch('/api/sprintreport/generate', {
    method: 'POST',
    body: formData  // Contains the CSV file + options
})
.then(response => response.blob())  // Get the PowerPoint bytes
.then(blob => {
    // Create a download link and click it automatically
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'Sprint_Report.pptx';
    a.click();
});
```

**Connects to:**
- `SprintReportController.cs` - makes HTTP requests to all API endpoints
- `Program.cs` - served via `app.UseStaticFiles()` and `app.UseDefaultFiles()`
- Swagger at `/swagger` - linked from the navigation

---

## CI/CD (Automatic Building)

### `.github/workflows/dotnet-build.yml` - The Automatic Builder

**What it does:** Every time you push code to `main` or create a Pull Request, GitHub automatically:
1. Checks out the code
2. Sets up .NET 10
3. Restores NuGet packages (dependencies)
4. Builds the project in Release mode

**If the build fails, the PR shows a red X.**

```yaml
on:
  pull_request:
    branches: [main]
  push:
    branches: [main]

steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: 10.0.x
  - run: dotnet restore WorkingSprintAgent.csproj
  - run: dotnet build WorkingSprintAgent.csproj --configuration Release --no-restore
```

**Connects to:**
- `WorkingSprintAgent.csproj` - the file being built
- GitHub - runs on GitHub's servers automatically

---


## Sample Data

### `sample-data/dummy-sprint.csv` - Test Data

**What it does:** A small test file with 12 tasks you can use to try the app.

```csv
TaskId,Title,Assignee,Status,Type,Priority,StoryPoints,SprintName,StartDate,EndDate
TASK-001,User Authentication System,John Doe,Done,Story,High,8,Sprint 15,2024-01-01,2024-01-12
TASK-005,Performance Optimization,Charlie Wilson,Blocked,Bug,Critical,5,Sprint 15,2024-01-05,
TASK-009,Error Handling Improvements,Henry Taylor,To Do,Bug,Medium,3,Sprint 15,2024-01-10,
```

It has a mix of: Done tasks, In Progress tasks, Blocked tasks, and To Do tasks across 8 team members.

---

## Complete Flow Example

Let's trace EXACTLY what happens when a user uploads `dummy-sprint.csv` and clicks "Generate":

### Step 1: HTTP Request Arrives
```
POST /api/sprintreport/generate
Content-Type: multipart/form-data
Body: { CsvFile: dummy-sprint.csv, Template: "Professional" }
```

### Step 2: Middleware Intercepts
`TokenUsageLoggingMiddleware` logs: "API request started: POST /api/sprintreport/generate"

### Step 3: Controller Validates
`SprintReportController.GenerateSprintReport()`:
- Is file present? YES
- Is extension .csv or .xlsx? YES (.csv)
- Is size <= 25MB? YES (small file)

### Step 4: Controller Calls Orchestrator
```csharp
var result = await _orchestrator.GenerateAsync(stream, options, cancellationToken);
```

### Step 5a: IF Semantic Kernel is DISABLED (default)
`SemanticKernelSprintReportOrchestrator.CanUseSemanticKernel()` returns FALSE
-> Falls back to `DeterministicSprintReportOrchestrator`

### Step 5b: Deterministic Workflow Runs

**5b-1: FileUploadAgent.ProcessAsync()**
- `CsvSprintDataService.ParseDataSetAsync()` is called
- Reads header row: TaskId, Title, Assignee, Status, Type, Priority, StoryPoints, SprintName, StartDate, EndDate
- Maps each row to a `SprintTask` object (12 tasks total)
- Computes `SprintMetrics`:
  - TotalTasks = 12
  - CompletedTasks = 6 (Done ones)
  - CompletionRate = 50%
  - BlockedTasks = 2
  - BugCount = 2
  - SprintHealthScore = calculated from all factors
- Returns `SprintDataSet(tasks, metrics)`

**5b-2: AnalysisAgent.AnalyzeAsync()**
- `OpenAIInsightGenerationService.GenerateEnhancedInsightsAsync()` is called
- Is API key present?
  - NO -> `MockInsightGenerationService` generates rule-based insights
  - YES -> `OpenAIService.GenerateInsightsAsync()`:
    1. TokenOptimizationService compresses metrics
    2. Checks cache (miss on first call)
    3. Calls OpenAI API with system prompt + compressed data
    4. Parses JSON response into SprintInsights
    5. Caches result for 60 minutes
    6. Records token usage in CostMonitoring and TokenUsageLogger
- Returns `AIInsightsResponse` with insights + cost info

**5b-3: PresentationAgent.CreateAsync()**
- `PresentationBuilderService.BuildPowerPointPresentation()` is called
- `PowerPointPresentationService.CreatePresentationFromTemplate()`:
  1. Creates 13 slide content objects from metrics + insights
  2. Gets color theme for "Professional" template (blue)
  3. Creates ZIP archive with proper .pptx structure
  4. Writes XML for each slide (title, text boxes, tables)
  5. Returns byte array
- Creates filename: "Sprint_Report_Sprint15_20240726_143022.pptx"
- Returns `PresentationArtifact(bytes, contentType, fileName)`

### Step 6: Controller Returns File
```csharp
return File(artifact.Content, artifact.ContentType, artifact.FileName);
// User's browser downloads: Sprint_Report_Sprint15_20240726_143022.pptx
```

### Step 7: Middleware Logs Completion
"API request completed: POST /api/sprintreport/generate - Status: 200, Duration: 2340ms"

---


## Interface Files (Contracts)

These files define WHAT a service can do without saying HOW. Like a job description without the employee.

| Interface File | What It Defines | Implemented By |
|---------------|-----------------|----------------|
| `Services/IOpenAIService.cs` | AI insight generation + token tracking | `OpenAIService.cs` |
| `Services/IInsightGenerationService.cs` | Generate insights (AI or rule-based) | `OpenAIInsightGenerationService.cs`, `MockInsightGenerationService.cs` |
| `Services/ICsvSprintDataService.cs` | Parse CSV/XLSX files | `CsvSprintDataService.cs` |
| `Services/IPresentationBuilderService.cs` | Build presentations | `PresentationBuilderService.cs` |
| `Services/ITokenOptimizationService.cs` | Optimize data for fewer tokens | `TokenOptimizationService.cs` |
| `Services/ICostMonitoringService.cs` | Track and monitor AI costs | `InMemoryCostMonitoringService.cs` |
| `Services/ITokenUsageLogger.cs` | Detailed usage logging | `TokenUsageLogger.cs` |
| `Services/Agents/IFileUploadAgent.cs` | Parse uploaded files | `FileUploadAgent.cs` |
| `Services/Agents/IAnalysisAgent.cs` | Analyze sprint metrics | `AnalysisAgent.cs` |
| `Services/Agents/IPresentationAgent.cs` | Create presentations | `PresentationAgent.cs` |
| `Services/Agents/ISemanticKernelAgentFactory.cs` | Create AI agents | `SemanticKernelAgentFactory.cs` |
| `Services/Orchestration/ISprintReportOrchestrator.cs` | Coordinate full workflow | `SemanticKernelSprintReportOrchestrator.cs`, `DeterministicSprintReportOrchestrator.cs`, `LocalSprintReportFallback.cs` |
| `Services/Orchestration/ISprintWorkflowStateStore.cs` | Store workflow state | `ScopedSprintWorkflowStateStore.cs` |

---

## File Dependency Map

Here's which file depends on which (reads/calls):

```
Program.cs
  ├── reads: appsettings.json, appsettings.Development.json
  ├── registers: ALL services, controllers, middleware
  └── configures: Swagger, CORS, logging, static files

SprintReportController.cs
  ├── calls: ISprintReportOrchestrator (for generate/preview)
  ├── calls: IInsightGenerationService (for status)
  ├── calls: IPresentationBuilderService (for templates)
  ├── calls: IOpenAIService (for token usage)
  ├── calls: ICostMonitoringService (for cost data)
  └── calls: ITokenOptimizationService (for optimization)

SemanticKernelSprintReportOrchestrator.cs
  ├── calls: DeterministicSprintReportOrchestrator (fallback)
  ├── calls: LocalSprintReportFallback (safe fallback)
  ├── calls: ISprintWorkflowStateStore (state management)
  ├── calls: CsvSprintPlugin (data loading)
  ├── calls: PresentationPlugin (presentation creation)
  ├── calls: ISemanticKernelAgentFactory (agent creation)
  ├── reads: SemanticKernelOptions
  ├── reads: OpenAIConfiguration
  └── calls: ICostMonitoringService (budget checks)

DeterministicSprintReportOrchestrator.cs
  ├── calls: IFileUploadAgent
  ├── calls: IAnalysisAgent
  └── calls: IPresentationAgent

OpenAIService.cs
  ├── calls: OpenAI API (https://api.openai.com/v1/chat/completions)
  ├── calls: ITokenOptimizationService (compress data)
  ├── calls: ICostMonitoringService (record usage)
  ├── calls: ITokenUsageLogger (detailed logging)
  ├── calls: IMemoryCache (caching)
  ├── reads: OpenAIConfiguration
  └── uses: MockInsightGenerationService (fallback)

CsvSprintDataService.cs
  ├── calls: XlsxWorkbookReader (for .xlsx files)
  ├── creates: SprintTask objects
  └── creates: SprintMetrics object

PresentationBuilderService.cs
  ├── calls: PowerPointPresentationService (for .pptx)
  ├── reads: SprintMetrics (for chart data)
  └── reads: SprintInsights (for text content)
```

---

## Summary: The 3 Possible Workflows

| # | Workflow | When It's Used | AI Cost |
|---|---------|---------------|---------|
| 1 | **Semantic Kernel Multi-Agent** | SemanticKernel:Enabled=true AND API key present AND budget not exceeded | $0.001-0.01 per request |
| 2 | **Deterministic with AI** | SemanticKernel:Enabled=false AND API key present | $0.0001-0.001 per request |
| 3 | **Deterministic without AI** | No API key OR budget exceeded OR AI error | $0.00 (free!) |

All three produce the SAME output format (a 13-slide PowerPoint), but the QUALITY of insights differs:
- Workflow 1: Highest quality (reviewed by multiple AI agents)
- Workflow 2: Good quality (single AI call)
- Workflow 3: Basic quality (math rules only, but always works!)

---

*This document was generated to explain the Working Sprint Agent .NET 10 project. Every file and its connections are described above.*
