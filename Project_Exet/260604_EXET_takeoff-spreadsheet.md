# Takeoff Spreadsheet EXET

## Corresponding Plan

- Plan: `Project_Plan/260604_PLAN_takeoff-spreadsheet.md`
- Execution date: 2026-06-04

## Associated Artifacts

- Test folder: `Project_Test/260604_TEST_takeoff-spreadsheet/`
- Commit: not committed in this execution pass

## Execution Result / Actual Scope

Implemented the flexible take-off spreadsheet capability as an agent-led workflow with deterministic services underneath:

- Added `TakeoffSpreadsheetAgent` under `Agents/Takeoff`.
- Added MCP tools:
  - `RunTakeoffSpreadsheetAgent`
  - `InspectTakeoffSources`
  - `PreviewTakeoffSchedule`
  - `ExportTakeoffSchedule`
- Added live Rhino takeoff snapshot reading through `ILiveTakeoffMetricReader`.
- Added declarative request/response DTOs and domain workbook/snapshot models.
- Added schedule validation, calculation, grouping, aggregation, sorting, expression evaluation, and preview/export orchestration.
- Added CSV and BCL-only XLSX spreadsheet writers behind `ISpreadsheetWorkbookWriter`.
- Added strict output destination validation requiring explicit `outputDirectory` and `outputFileName`.
- Added CLI/Rhino smoke wiring and MCP safety/inventory smoke updates.

## Deviations From Plan

- No package dependency was added for XLSX. A small BCL `ZipArchive`/XML writer was implemented instead, avoiding Excel automation and package load risk.
- The live smoke command was added, but this execution did not run the Rhino-hosted live command. CLI fallback and writer smoke paths passed.
- Normal Release solution build was blocked by running locked binaries. Release validation was rerun successfully with an alternate `BaseOutputPath`.

## Issues Found And Fixed During Execution

- Fixed a namespace collision from `MCP_Rhino.Server.Infrastructure.File` shadowing `System.IO.File` in existing Infrastructure code by moving writer classes to `MCP_Rhino.Server.Infrastructure.Spreadsheet`.
- Updated the overlap cleanup smoke from stale inventory counts to the current 156-tool surface.
- Added direct CSV/XLSX writer smoke coverage to the takeoff CLI smoke so writer runtime behavior is validated outside Rhino.

## Test Record

Builds:

- `dotnet build .\MCP_Rhino.sln -c Debug` -> exit 0
- `dotnet build .\MCP_Rhino.sln -c Release` -> exit 1, blocked by locked running processes:
  - `Rhino 8 (42760)` locking `MCP_Rhino.Server.dll`
  - `MCP_Rhino.Bridge (43304, 35304, 36576)` locking `MCP_Rhino.Bridge.exe`
  - `MCP_Rhino.Companion (31812)` locking `MCP_Rhino.Companion.exe`
- `dotnet build .\MCP_Rhino.sln -c Release -p:BaseOutputPath=C:\Projects\MCP_Rhino\_validation\build-output\` -> exit 0

Debug smokes:

- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test` -> exit 0, verified 156 tools
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-overlap-cleanup-smoke-test` -> exit 0, verified 156 tools
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test` -> exit 0, inventory found 156 tools
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- takeoff-spreadsheet-smoke-test` -> exit 0

Release smokes from alternate output:

- `dotnet C:\Projects\MCP_Rhino\_validation\build-output\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test` -> exit 0, verified 156 tools
- `dotnet C:\Projects\MCP_Rhino\_validation\build-output\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-overlap-cleanup-smoke-test` -> exit 0, verified 156 tools
- `dotnet C:\Projects\MCP_Rhino\_validation\build-output\Release\net8.0\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test` -> exit 0, inventory found 156 tools
- `dotnet C:\Projects\MCP_Rhino\_validation\build-output\Release\net8.0\MCP_Rhino.Server.dll takeoff-spreadsheet-smoke-test` -> exit 0

## Acceptance Alignment

- Agent and four MCP tools are discoverable through assembly scanning.
- Safety annotations match the plan:
  - `InspectTakeoffSources`: read-only, closed-world
  - `PreviewTakeoffSchedule`: read-only, closed-world
  - `ExportTakeoffSchedule`: destructive open-world
  - `RunTakeoffSpreadsheetAgent`: destructive open-world
- Export rejects missing output folder/file name with `TAKEOFF_OUTPUT_REQUIRED`.
- CLI fallback live paths return `LIVE_RHINO_REQUIRED` after non-live validation.
- CSV and XLSX writers produce non-empty files in CLI smoke and escape formula-like text.
- Tool inventory and safety smokes pass in Debug and alternate-output Release.

## Rollback Verification

Rollback is a normal git revert of this implementation plus the matching TEST/EXET artifacts. The capability does not mutate Rhino documents during preview/discovery and writes only caller-specified spreadsheet paths during export.

## Current Remaining Items

- Run `McpTakeoffSpreadsheetSmoke` inside Rhino against a saved active document to validate live object creation, discovery, preview, CSV export, and XLSX export through the plugin host.
- Re-run normal Release solution build after closing the currently running Rhino/Bridge/Companion processes if deployment to the locked output folder is required.

## Conclusion

The takeoff spreadsheet capability is implemented and validated for compilation, MCP surface governance, CLI fallback live-only behavior, and spreadsheet writer output. Rhino-hosted live smoke remains the final host-level verification step.
