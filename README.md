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

No paid API or service is used. The LLM features (later stages) run on a free local model via Ollama, or in Demo mode by default.

Next: Stage 2 – API + UI for findings.

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
samples/                     Hand-written legacy sample pages
.claude/agents/              Claude Code agent roles (product manager, project manager, QA, developer)
```

## Prerequisites

- .NET 8 SDK (or a newer SDK with the .NET 8 runtime)
- Node.js 20+ and npm

## Run locally

API (http://localhost:5080, Swagger at `/swagger`):

```bash
dotnet run --project src/LegacyLens.Api
```

Web app (http://localhost:5173):

```bash
cd web
npm install
npm run dev
```

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
