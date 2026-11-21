# LegacyLens – AI-assisted Classic ASP / WebForms modernization helper

## 1. Goal

LegacyLens takes a legacy web page (Classic ASP or ASP.NET WebForms), explains what it does, flags security and quality risks, and proposes an equivalent modern ASP.NET Core implementation.

It is a portfolio project. It should show:
- Solid .NET 8 backend engineering (Web API, EF Core, SQL Server, clean layering, tests).
- A React + TypeScript front end.
- Practical, *reliable* AI use: deterministic checks first, LLM second, structured output, grounding, validation of generated code, and an evaluation harness. The LLM runs locally (Ollama) behind a swappable interface: zero cost, no code leaves the machine.
- DevOps basics: Docker Compose and GitHub Actions CI.

Non-goals: a full migration tool, multi-user auth, production hosting.

## 2. Users and main flow

A developer opens the web app and:
1. Pastes code or uploads a `.asp` / `.aspx` / `.aspx.cs` / `.inc` file.
2. Clicks **Analyze** and sees:
   - **Summary**: what the page does, its inputs (QueryString/Form/Session), outputs, database access, includes and COM objects.
   - **Findings**: a list of risks with severity, line number, explanation and fix.
   - **Migration proposal**: suggested ASP.NET Core code (Razor Page or Controller + Service) using parameterized queries, with notes and the sources it relied on.
3. Opens **History** to see earlier analyses.

## 3. Architecture

```
legacy-lens/
  src/
    LegacyLens.Api/            ASP.NET Core 8 Web API (controllers, DI, config, Swagger)
    LegacyLens.Core/           Domain models, interfaces, rule engine, orchestration
    LegacyLens.Infrastructure/ EF Core (SQL Server), LLM client, retriever, Roslyn compile check
  web/                         React + TypeScript (Vite)
  web/e2e/                     Playwright end-to-end UI tests (TypeScript)
  knowledge/                   Markdown notes: Classic ASP / WebForms -> ASP.NET Core mappings
  samples/                     Hand-written legacy sample pages (no real employer code)
  evals/                       Eval cases: sample file + expected findings
  tests/
    LegacyLens.Core.Tests/     xUnit tests (rules, retriever, orchestration with mocks)
    LegacyLens.Api.Tests/      Integration tests (WebApplicationFactory)
  docker-compose.yml           api + web + sqlserver (+ optional ollama profile)
  .github/workflows/ci.yml     build + test
```

Key interfaces (in Core):
- `IRule` – one deterministic detector. Returns `Finding[]` (Id, Severity, Line, Title, Explanation, Fix).
- `IRuleEngine` – runs all rules on a `SourceFile`.
- `ILlmClient` – `Task<T> CompleteJsonAsync<T>(prompt, schema, ct)`. Implementations: `OllamaLlmClient` (a free local model through Ollama's HTTP API at `http://localhost:11434`, via `IHttpClientFactory`) and `DemoLlmClient` (canned responses). No paid API is used anywhere.
- `IRetriever` – `Task<IReadOnlyList<KnowledgeChunk>> SearchAsync(query, topK)`. First implementation: BM25 over `knowledge/*.md`. Can be swapped later for embeddings.
- `ICodeValidator` – compiles a generated C# snippet with Roslyn and returns diagnostics.
- `IAnalysisService` – orchestrates: rules → retrieval → LLM summary → LLM migration → validation → save.

API endpoints:
- `POST /api/analyses` – body: `{ fileName, language: "classic-asp" | "webforms", code }` → full analysis result.
- `GET /api/analyses` – list (id, fileName, createdAt, findingCounts).
- `GET /api/analyses/{id}` – full result.
- `GET /api/health` – health check.

## 4. Deterministic rules (MVP set)

Each rule has unit tests with positive and negative examples.

| Id | Detects | Severity |
|---|---|---|
| SQL001 | SQL built by string concatenation with `Request(...)`, `Request.QueryString`, `Request.Form` or a variable | High |
| XSS001 | `Response.Write` / `<%= %>` of request data without `Server.HTMLEncode` | High |
| SEC001 | Hard-coded connection strings, passwords or keys | High |
| ERR001 | `On Error Resume Next` without later `Err.Number` check | Medium |
| COM001 | `Server.CreateObject(...)` (lists COM dependencies) | Info |
| SES001 | Heavy `Session(...)` usage (more than N reads/writes) | Low |
| INC001 | `<!--#include file/virtual-->` (lists include dependencies) | Info |

## 5. AI part – reliability rules

- The LLM never decides findings alone. Rule findings are the source of truth; the LLM may add *suggested* findings marked `source: "ai"`, shown separately.
- All LLM calls request JSON matching a schema. Invalid JSON → one retry with the validation error in the prompt → otherwise return a clear error.
- The migration prompt includes the top-k knowledge chunks. The response must list which chunk ids it used, and the UI shows them as sources.
- Generated C# is compiled with Roslyn (as a class in a stub project context). If it fails, one repair attempt with the compiler diagnostics. The UI shows "compiles ✓" or the remaining diagnostics.
- The LLM provider, model name (default `qwen2.5-coder:7b`) and Ollama URL come from configuration (`Llm:Provider` = `Demo` | `Ollama`).
- The default is **Demo mode** (`DemoLlmClient`), so anyone can clone and run it with no model installed and no cost.
- **Cost rule: the project must never require a paid API or paid service.** Local models via Ollama only.

## 6. Evaluation harness

- `evals/` holds cases: `case-name/input.asp` + `expected.json` (expected rule ids and lines).
- A test (or a small console command) runs the rule engine on all cases and prints precision/recall per rule.
- The README shows the latest numbers.

## 6a. End-to-end UI tests (Playwright)

- Playwright Test (TypeScript) in `web/e2e/`, run against the real app (API + web) in **Demo mode**, so results are deterministic and free.
- Every screen gets tests as it is built. Minimum scenarios:
  - Home loads; code box, file upload and Analyze button are visible.
  - Paste a sample with SQL injection → Analyze → findings table shows `SQL001` with the right line number.
  - Upload a sample file from `samples/` → findings appear.
  - Empty input → a clear validation message, no request sent.
  - Summary panel shows inputs, DB access and includes (stage 3+).
  - Migration panel shows code, sources and the "compiles" badge (stage 5+).
  - History page lists a new analysis and opens it (stage 6+).
  - API error (simulated with `page.route`) → friendly error message, no crash.
- Use stable `data-testid` attributes for elements that tests target; no brittle CSS selectors.
- Run on Chromium in CI; locally also Firefox and WebKit.
- On failure CI uploads the Playwright HTML report, traces and screenshots as artifacts.
- The README screenshots are produced by a Playwright script, so they stay current.

## 7. Data

EF Core with SQL Server. Tables: `Analyses` (Id, FileName, Language, Code, SummaryJson, MigrationJson, CompilesOk, CreatedAt) and `Findings` (Id, AnalysisId, RuleId, Severity, Line, Title, Explanation, Fix, Source). Migrations are checked in.

## 8. Build stages

Work one stage at a time. Each stage ends with a green build, passing tests and a commit.

0. **Scaffold**: solution, projects, React app (Vite + TS), Playwright set up in `web/e2e` with one smoke test (home page loads), `.gitignore`, README skeleton, GitHub Actions CI (dotnet build + test, web build + lint, Playwright). Health endpoint.
1. **Rule engine**: `SourceFile`, `IRule`, the 7 rules, `IRuleEngine`, unit tests, and 4–6 hand-written samples in `samples/`.
2. **API + UI for findings**: `POST /api/analyses` runs rules only. React page with code input, file upload, and a findings table with line numbers. Playwright tests for paste, upload, empty input and API error.
3. **LLM summary**: `ILlmClient` (Ollama + Demo), JSON schema, retry on invalid JSON, summary panel in the UI. Tests use a mocked client; Playwright test for the summary panel.
4. **Retrieval**: write 8–12 short notes in `knowledge/` (e.g. ADO Recordset → Dapper/EF, Session → ASP.NET Core session, includes → partials/services, `Response.Write` → Razor). BM25 retriever + tests.
5. **Migration proposal**: grounded prompt, sources list, Roslyn validation + one repair attempt, migration panel with a code viewer and "compiles" badge. Playwright test for the migration panel.
6. **Persistence + history**: EF Core, migrations, History page. Playwright test for history.
7. **Evals + polish**: eval cases and metrics, Docker Compose (api, web, sqlserver), README with architecture diagram, screenshots and a short GIF.

## 9. Definition of done for the project

- `docker compose up` runs everything locally in Demo mode; an optional `ollama` service/profile enables the local model.
- CI is green on GitHub, including the Playwright UI tests.
- README explains the problem, the design (rules first, AI second, validation), how to run, and eval results.
