---
applyTo: "packages/server/**"
description: "Use when editing the .NET API, school analysis agent, or packaged skills under packages/server."
---

- Preserve the current Minimal API style in `Program.cs`; add new backend behavior close to the route group or service that already owns it.
- Route new configuration through `Options/SchoolServiceOptions.cs` instead of scattering environment reads.
- Keep `skills/` as the runtime content root and preserve each skill folder's `_meta.json` + `SKILL.md` + `scripts/` structure.
- If you change AI tool prompts or manual tool invocation code, express arguments in the `AIFunctionArguments` shape expected by the agent stack instead of raw JSON examples.
- When backend contracts change, update both `Models/Contracts.cs` and the matching client-side types or API adapter code.
