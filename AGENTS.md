# AGENTS

## Scope

This workspace is a pnpm monorepo with two active application packages:

- `packages/client`: React 18 + Vite + TypeScript frontend.
- `packages/server`: .NET 10 Minimal API backend with AI-assisted school analysis and file-based skills.

Default to small, local changes. Do not change business code when the task is only about tooling, prompts, or agent customization.

## Start Here

Choose the closest owning entrypoint before editing:

- Frontend app shell: `packages/client/src/App.tsx`
- Frontend page composition: `packages/client/src/pages/HomePage.tsx`
- Frontend API bridge: `packages/client/src/lib/api.ts`
- Backend HTTP surface: `packages/server/Program.cs`
- Backend configuration: `packages/server/Options/SchoolServiceOptions.cs`
- Backend contracts: `packages/server/Models/Contracts.cs`
- AI school analysis flow: `packages/server/Services/SchoolAnalysisAgent.cs`

## Build And Run

Prefer the narrowest command that matches the change:

- Full-stack dev: `pnpm dev`
- Frontend dev only: `pnpm dev:client`
- Backend dev only: `pnpm dev:server`
- Workspace build: `pnpm build`
- .NET validation from repo root: `dotnet build`

In VS Code, the workspace already exposes a `build` task backed by `dotnet build`.

## Frontend Notes

- The client uses the `@` alias for `packages/client/src`.
- Vite proxies `/api` to `http://127.0.0.1:3001` by default; check `packages/client/vite.config.ts` before changing API base behavior.
- Reuse existing hooks, shared UI, and domain types before creating new abstractions.

## Backend Notes

- Keep new API endpoints in the Minimal API style used in `packages/server/Program.cs` unless there is a strong reason to introduce a new pattern.
- Configuration flows through `SchoolServiceOptions.FromConfiguration`; if you add a new environment variable or runtime option, wire it there.
- The backend loads `.env` from the server working directory at startup. If local runs behave differently from CI or deployment, check working directory assumptions first.
- `/api/schools/search` is a server-sent events endpoint. Preserve streaming semantics and response headers when changing search behavior.

## Skills And Agent Pitfalls

- The runtime skill directory is `packages/server/skills` (plural). Do not rename it to `skill`, and keep deployment logic aligned with `skills/**`.
- Skill folders use a file-based layout such as `_meta.json`, `SKILL.md`, and `scripts/`, with optional `requirements.txt` or data directories for Python-backed skills.
- When changing `SchoolAnalysisAgent`, `LocalSkillScriptRunner`, or nearby tool invocation code, keep tool arguments in the `AIFunctionArguments` shape expected by Microsoft.Agents and Microsoft.Extensions.AI. Avoid prompting models to emit raw JSON objects or array-wrapped tool arguments.

## Change Discipline

- If an API contract changes, update the matching client types and API consumption code in the same change.
- Prefer linking behavior back to existing entrypoints over introducing parallel code paths.
- Keep deployment-specific changes consistent with `deploy/build.ps1` and `deploy/setup-server.sh`.
