# CLAUDE.md – working rules for this repo

## Project
LegacyLens: see `SPEC.md` for the goal, architecture and the numbered build stages. It is the source of truth.

## How to work
- Work on ONE stage from SPEC.md section 8 at a time. Do not start the next stage unless asked.
- Before writing code for a stage, briefly list the files you plan to create or change, then proceed.
- Keep changes small and focused. Prefer clear code over clever code.
- Do not add NuGet or npm packages beyond what the current stage needs; say which package and why when you add one.
- When something in SPEC.md is unclear or seems wrong, stop and ask instead of guessing.

## Before every commit
1. `dotnet build` – no errors, no new warnings.
2. `dotnet test` – all tests pass.
3. In `web/`: `npm run build` and `npm run lint` pass (once the web app exists).
4. Commit with a conventional message, e.g. `feat(rules): add SQL001 string-concatenation detector`.

## C# / .NET conventions
- .NET 8, C# 12, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
- File-scoped namespaces, `async`/`await` all the way, pass `CancellationToken`.
- Dependency injection for everything; no static service locators.
- Options pattern (`IOptions<T>`) for configuration.
- `IHttpClientFactory` for outgoing HTTP.
- Records for DTOs and immutable domain values.
- Every rule and every non-trivial service gets xUnit tests. Mock `ILlmClient` in tests; tests must never call a real API.

## React / TypeScript conventions
- Vite + React + TypeScript with `strict: true`.
- Function components and hooks. Keep API calls in one `api.ts` module.
- Simple, clean UI; no heavy UI framework unless asked.

## Security
- Never commit secrets. The Anthropic API key comes from `dotnet user-secrets` or the `ANTHROPIC_API_KEY` environment variable.
- If no key is configured, use `DemoLlmClient` (Demo mode).
- Samples in `samples/` and `evals/` are hand-written examples only. Never paste real code from any employer.

## Docs
- Keep README.md up to date at the end of each stage: what works now and how to run it.
