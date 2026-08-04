# MCP Tool Safety Annotations EXET

## Corresponding Plan

- `Project_Plan/260506_PLAN_mcp-tool-safety-annotations.md`
- Execution date: 2026-05-06

## Related Artifacts

- Test folder: `Project_Test/260506_TEST_mcp-tool-safety-annotations/`
- Commit / PR: not created in this working tree

## Execution Result / Actual Scope

- Added explicit method-level MCP safety metadata to all 78 exported tools under `src/MCP_Rhino.Server/Tools/`.
- Classified tools into:
  - 42 read/preview closed-world tools: `ReadOnly = true, Destructive = false, OpenWorld = false`
  - 21 non-destructive Rhino mutation tools: `ReadOnly = false, Destructive = false, OpenWorld = false`
  - 6 destructive Rhino mutation tools: `ReadOnly = false, Destructive = true, OpenWorld = false`
  - 2 open-world non-destructive tools: `ReadOnly = false, Destructive = false, OpenWorld = true`
  - 7 open-world destructive export tools: `ReadOnly = false, Destructive = true, OpenWorld = true`
- Added the project guideline that every future method-level `[McpServerTool]` must explicitly declare `ReadOnly`, `Destructive`, and `OpenWorld`.
- Added `mcp-tool-safety-annotations-smoke-test` to enforce the classification table and reject bare `[McpServerTool]` attributes.

## Deviations From Plan

- None in behavior scope.
- The smoke implementation had to tolerate CLI-mode assemblies where Eto-backed UI types are unavailable, so it reflects only loadable tool types and separately scans source files for bare attributes.

## Issues Found And Fixed During Work

- Initial reflection smoke failed because `Assembly.GetTypes()` tried to load Rhino UI/Eto-backed types in CLI mode before filtering to `Tools`. The smoke now uses loadable types and safe type-name access.

## Test Record

- `dotnet build .\MCP_Rhino.sln -c Release`
  - Exit code: 0
  - Result: 0 warnings, 0 errors

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe mcp-tool-safety-annotations-smoke-test`
  - Exit code: 0
  - Key output:
    - `[OK] MCP safety annotations verified for 78 tools.`
    - `[OK] No bare method-level [McpServerTool] attributes remain.`

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe llm-panel-cli-switching-smoke-test`
  - Exit code: 0
  - Codex CLI path, UTF-8 stdin, MCP status, Codex JSONL parser, and read-only user-attribute metadata assertions passed.

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe rhino-claude-code-companion-ui-smoke-test`
  - Exit code: 0

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.exe rhino-claude-code-panel-smoke-test`
  - Exit code: 0

## Updated Normal Release Outputs

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
  - Size: 931840 bytes
  - Last write: 2026-05-06 15:36:19
- `src\MCP_Rhino.Companion\bin\Release\net8.0-windows\MCP_Rhino.Companion.exe`
  - Size: 143360 bytes
  - Last write: 2026-05-06 15:26:31
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`
  - Size: 142848 bytes
  - Last write: 2026-05-06 15:01:24

## Acceptance Alignment

- All safe read/filter/preview/query tools now advertise read-only metadata to MCP clients.
- No tool relies on `ModelContextProtocol` default safety metadata.
- Future missing annotations are blocked by a dedicated smoke test and a project architecture rule.
- Existing Codex/Claude panel smoke checks still pass after the full annotation pass.

## Rollback Verification

Reverting the annotation pass would reintroduce the same default metadata shape that made Codex cancel safe read-only Rhino MCP calls. The smoke test would fail if any method-level tool returned to bare `[McpServerTool]`.

## Current Remaining Items

- Destructive tools are still correctly marked destructive. If a future Codex version refuses destructive MCP calls in non-interactive `exec` mode, that should be handled as a panel approval UX decision rather than by mislabeling delete/replace/export tools as safe reads.

## Conclusion

The Codex cancellation issue is now addressed across the tool surface, not just for `filter_objects_by_user_attributes`. The Release build exposes explicit safety metadata for every MCP tool and includes a regression smoke test to prevent this class of issue from returning.
