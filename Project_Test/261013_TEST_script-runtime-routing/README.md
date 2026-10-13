# Script Runtime Routing Validation

Validation date: 2026-10-13. Scope: documentation review only; no live Rhino execution.

## Checks performed

- `git diff --check` exited `0` with no output after the policy edits.
- Reviewed `git diff -- AGENTS.md 'Runtime_Workflow/MCP_Rhino Workflow.md'` for consistent routing and preserved execution constraints.
- `git status --short` showed the pre-existing `.gitignore` change alongside the intended policy files and new PLAN; this task did not edit `.gitignore`.

## Routing scenarios reviewed

| Scenario | Required interpretation | Review |
| --- | --- | --- |
| An existing skill and an atomic chain both fit | Prefer the skill | Pass |
| No dedicated tool; a supported task script fits | Execute as runtime work without construction or PLAN | Pass |
| Neither task-specific capabilities nor available supported scripting suffice | Explain the remaining gap before proposing construction | Pass |
| Live route unavailable | Do not switch transport, use implicit offline fallback, or infer UI permission | Pass |
| Explicit offline task | Follow its existing script contract and copy-only publication | Pass |
| Repeated scripted task | Consider reuse or a skill; a new MCP tool is not automatic | Pass |
| New runner, registration, or repository capability needed | Construction still requires user confirmation and Project_Guides | Pass |

These are manual policy checks, not proof of runtime script behavior. No test code, CLI smoke slug, Debug/Release build, or Rhino session was needed because the changes affect prose routing rules only. Existing runner implementations and contracts were not changed.
