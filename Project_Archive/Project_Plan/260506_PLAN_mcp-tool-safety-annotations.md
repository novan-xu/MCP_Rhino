# MCP Tool Safety Annotations Plan

## Background

Codex cancelled a pure read-only Rhino MCP query because the tool method used the default `[McpServerTool]` metadata. In `ModelContextProtocol` 1.1.0, missing hints default to a non-read-only, potentially destructive, open-world tool shape. That is too conservative for Rhino read/query tools and causes the non-interactive Codex panel to reject safe calls.

## Goal

- Add explicit MCP safety annotations to every tool exposed from `src/MCP_Rhino.Server/Tools/`.
- Ensure read and preview tools advertise `ReadOnly = true`, `Destructive = false`, and `OpenWorld = false`.
- Ensure mutation/export/logging tools advertise intentional non-read-only metadata instead of inheriting unsafe defaults by accident.
- Add a regression smoke test so future `[McpServerTool]` additions cannot omit safety metadata.

## Architecture Ownership

- Tool metadata belongs in `Tools/*Tool.cs`, because `ToolRegistration` registers tools from assembly reflection and the attribute is the client-visible MCP contract.
- The regression test belongs under `Project_Test/260506_TEST_mcp-tool-safety-annotations/`.
- The durable rule belongs in `Project_Guides/MCP_Rhino Architecture.md` under the `Tools/` guidance.

## Key Design

- Use explicit named properties on every method-level `[McpServerTool(...)]`.
- Keep Rhino live read/preview tools closed-world because they operate on the active or panel-bound Rhino document, not arbitrary external systems.
- Mark filesystem export/logging tools as open-world where they write to caller-provided paths.
- Keep destructive/non-destructive classification explicit so it is visible during code review.
- Add a CLI smoke slug `mcp-tool-safety-annotations-smoke-test` that:
  - reflects all exported MCP tool methods,
  - verifies every tool name is intentionally classified,
  - verifies runtime attribute values match the expected classification,
  - scans tool source files to reject bare `[McpServerTool]` usage.

## Affected Files

- `src/MCP_Rhino.Server/Tools/**/*.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Project_Exet/260506_EXET_mcp-tool-safety-annotations.md`

## Usage

Run:

```powershell
src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe mcp-tool-safety-annotations-smoke-test
```

## Acceptance Criteria

- No method-level tool attribute remains as bare `[McpServerTool]`.
- All exported MCP tool methods have explicit `ReadOnly`, `Destructive`, and `OpenWorld` values.
- All read and preview tools are marked read-only and non-destructive.
- The new smoke test, existing LLM panel smoke, and Release build pass.

## Risks And Rollback

- Risk: a tool is misclassified. Mitigation: explicit per-method expected classification in the smoke test makes the decision reviewable.
- Risk: a future tool bypasses metadata. Mitigation: the smoke test fails on unknown or bare tool attributes.
- Rollback: revert this Plan/TEST/EXET plus the attribute edits; this would restore previous defaults and reintroduce Codex cancellation risk for safe reads.

## Future Extension

- If Codex adds richer per-tool approval policy controls, the same classification table can drive provider-specific panel behavior.
