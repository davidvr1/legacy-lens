# LegacyLens

AI-assisted Classic ASP / ASP.NET WebForms modernization helper. Paste a legacy page, get a summary, a list of security and quality findings, and a proposed ASP.NET Core implementation.

See [SPEC.md](SPEC.md) for the full design and build stages.

## Status

**Stage 0 – Scaffold** (done)

What works now:
- .NET 8 solution with `LegacyLens.Api`, `LegacyLens.Core`, `LegacyLens.Infrastructure` and two test projects.
- `GET /api/health` returns `{ "status": "ok" }`, covered by an integration test.
- Swagger UI in Development.
- React + TypeScript (Vite) app shell in `web/`, with `/api` proxied to the API in dev.
- Playwright end-to-end tests in `web/e2e/` with one smoke test (home page loads).
- GitHub Actions CI: .NET build + test, web lint + build, Playwright on Chromium (report and traces uploaded on failure).

**Stage 1 – Rule engine** (done)

- `LegacyLens.Core` has `SourceFile`, `IRule`, `IRuleEngine` and seven deterministic rules:

  | Id | Detects | Severity |
  |---|---|---|
  | SQL001 | SQL built by concatenating request data or variables | High |
  | XSS001 | Request data written with `Response.Write` / `<%= %>` without HTML encoding (follows variables set from request data) | High |
  | SEC001 | Hard-coded connection strings, passwords and keys (the secret value is never repeated in the finding) | High |
  | ERR001 | `On Error Resume Next` without a later `Err.Number` check | Medium |
  | COM001 | COM dependencies from `Server.CreateObject(...)`, with a .NET replacement hint | Info |
  | SES001 | Heavy `Session(...)` usage (more than 10 reads/writes by default) | Low |
  | INC001 | `<!--#include file/virtual-->` dependencies | Info |

- Rules look only at server-side code: comments (`'`, `Rem`, `//`) and HTML outside `<% %>` are ignored, and text inside string literals is not mistaken for code.
- Five hand-written sample pages in `samples/` (four Classic ASP, one WebForms code-behind). One of them, `safe-orders.asp`, shows the safe patterns and has only Info findings. A test checks the exact findings for each sample.
- The rules are pattern-based heuristics, not a full VBScript/C# parser. Known limits: variables are tracked within one file only, and a value passed through a function (for example `x = Trim(name)`) is not followed.

**Stage 2 – API + UI for findings** (done)

- `POST /api/analyses` runs the rules (no AI and no database yet):

  ```http
  POST /api/analyses
  { "fileName": "login.asp", "language": "classic-asp", "code": "..." }
  ```

  It returns `200 { id, fileName, language, findings: [{ ruleId, severity, line, title, explanation, fix }] }`, with `severity` as a string (`"High"`). It returns `400 { error }` when the code is empty or longer than 200,000 characters, or when `language` is not `classic-asp` or `webforms`. The `id` is not stored yet; persistence comes in Stage 6.
- Web page: paste code or upload a `.asp`, `.inc`, `.aspx` or `.aspx.cs` file. The language is picked from the file extension and can be changed. Click **Analyze** to see a findings table with line, severity, rule, explanation and fix. The page shows clear messages for empty input, unsupported files and API errors.
- Integration tests for the endpoint, and Playwright tests for paste, upload, empty input and API errors.

No paid API or service is used. The LLM features (later stages) run on a free local model via Ollama, or in Demo mode by default.

**Stage 3 – LLM summary** (done)

- Each analysis now includes an AI summary: what the page does, its inputs (QueryString / Form / Session), outputs, database access, includes and COM objects. The rules still run first and remain the source of truth for findings. The summary appears in a separate panel marked "AI-generated".
- The model sits behind `ILlmClient`, which has two implementations:
  - `DemoLlmClient` (the default) returns a fixed sample summary. It needs no model and no network, so tests and new clones work right away.
  - `OllamaLlmClient` calls a free local model through Ollama (`POST /api/generate`). It sends a JSON schema so the reply comes back as structured JSON.
- Reliability: the reply is parsed and validated. If it is invalid, the model gets one retry with the error in the prompt. If the retry also fails, or the model cannot be reached, the API still returns the findings, with `summary: null` and a clear `summaryError`.
- `POST /api/analyses` now also returns `summary: { purpose, inputs, outputs, databaseAccess, includes, comObjects }` and `summaryError`.
- Tests: unit tests with a fake `ILlmClient`, tests for the Ollama client using a fake HTTP handler (no network), and Playwright tests for the summary panel.

**Stage 4 – Retrieval** (done)

- `knowledge/` holds 12 short hand-written notes, each mapping one legacy pattern to ASP.NET Core. Examples: ADO Recordset → Dapper / EF Core, `Session` → ASP.NET Core session, `#include` → partials and services, `Response.Write` → Razor, COM objects → .NET replacements, WebForms → Razor Pages.
- `IRetriever` has three implementations, picked with `Retrieval:Mode`:
  - `Bm25` (default): keyword search with a code-aware tokenizer. `Server.CreateObject` becomes `server, createobject, create, object`.
  - `Embedding`: a local Ollama embedding model (`nomic-embed-text`) with cosine similarity. The vectors are kept in memory and computed once per process. A vector database is not needed for a few dozen chunks.
  - `Hybrid`: merges both rankings with Reciprocal Rank Fusion.
- The retriever is not used in the analysis yet. Stage 5 adds it to the migration prompt.
- Retrieval eval: `evals/retrieval.json` has 22 cases (code snippets and developer questions) with the expected notes. A CI test requires BM25 recall@3 ≥ 0.9.

  | Mode | Cases | Recall@3 | MRR |
  |---|---|---|---|
  | BM25 | 22 | 1.000 | 0.955 |
  | Embedding (`nomic-embed-text`) | 22 | 1.000 | 0.970 |
  | Hybrid (RRF) | 22 | 1.000 | 0.977 |

  All three modes find every expected note in the top 3. Hybrid ranks the right note first most often. BM25 stays the default for now: it is deterministic and needs no model. The eval set will get harder, freely worded questions before the default is chosen.

  Compare all three modes with a local Ollama:

  ```bash
  ollama pull nomic-embed-text
  dotnet run --project tools/LegacyLens.Evals -- retrieval --modes bm25,embedding,hybrid
  ```

- LLM sampling settings are now configurable: `Llm:Temperature`, `Llm:TopP`, `Llm:NumCtx`.

Next: Stage 5 – Migration proposal.

## LLM configuration

Set in `src/LegacyLens.Api/appsettings.json` or with environment variables (for example `Llm__Provider=Ollama`):

| Setting | Default | Meaning |
|---|---|---|
| `Llm:Provider` | `Demo` | `Demo` (canned answers) or `Ollama` (local model) |
| `Llm:Model` | `qwen2.5-coder:7b` | Ollama model name |
| `Llm:OllamaUrl` | `http://localhost:11434` | Ollama server address |
| `Llm:TimeoutSeconds` | `120` | Timeout for one model call |
| `Llm:Temperature` / `Llm:TopP` / `Llm:NumCtx` | `0` / `1.0` / `8192` | Sampling settings sent to Ollama |
| `Retrieval:Mode` | `Bm25` | `Bm25`, `Embedding` or `Hybrid` |
| `Retrieval:TopK` / `K1` / `B` | `3` / `1.2` / `0.75` | Chunks per prompt and BM25 parameters |
| `Retrieval:EmbeddingModel` | `nomic-embed-text` | Ollama embedding model |

To use a real local model (free): install [Ollama](https://ollama.com), run `ollama pull qwen2.5-coder:7b`, then start the API with `Llm__Provider=Ollama`.

## Repository layout

```
src/
  LegacyLens.Api/            ASP.NET Core 8 Web API
  LegacyLens.Core/           Domain models, interfaces, rule engine, orchestration
  LegacyLens.Infrastructure/ EF Core, LLM client, retriever, Roslyn compile check
tests/
  LegacyLens.Core.Tests/     xUnit unit tests
  LegacyLens.Api.Tests/      Integration tests (WebApplicationFactory)
web/                         React + TypeScript (Vite)
web/e2e/                     Playwright end-to-end UI tests
knowledge/                   Migration notes used by the retriever
evals/                       Eval data (retrieval queries)
tools/LegacyLens.Evals/      Eval console app (manual, local Ollama)
samples/                     Hand-written legacy sample pages
.claude/agents/              Claude Code agent roles (developer, QA)
```

## Prerequisites

- .NET 8 SDK (or a newer SDK with the .NET 8 runtime)
- Node.js 20+ and npm

## Run locally

API (http://localhost:5080, Swagger at `/swagger`):

```bash
dotnet run --project src/LegacyLens.Api
```

Web app (http://localhost:5173, with the API running):

```bash
cd web
npm install
npm run dev
```

Open http://localhost:5173, upload a file from `samples/` (for example `samples/login.asp`) and click **Analyze**.

## Build and test

```bash
dotnet build
dotnet test

cd web
npm run lint
npm run build
```

End-to-end tests (Playwright starts the API and the web app itself; locally it runs Chromium, Firefox and WebKit):

```bash
cd web
npx playwright install   # first time only: downloads the browsers
npx playwright test
```
