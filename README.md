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
- GitHub Actions CI: .NET build + test, web lint + build.

Next: Stage 1 – rule engine.

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
