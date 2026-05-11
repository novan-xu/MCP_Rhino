# 260506_EXET_project-folder-cleanup

## Corresponding Plan

- Plan: `Project_Plan/260506_PLAN_project-folder-cleanup.md`
- Execution date: 2026-05-06

## Associated Artifacts

- Test folder: `Project_Test/260506_TEST_project-folder-cleanup/`
- Commit / PR: not created in this working session.

## Execution Result / Actual Scope

- Added archive buckets:
  - `Project_Archive/Project_Plan/`
  - `Project_Archive/Project_Exet/`
  - `Project_Archive/Project_Test/`
- Added a cleanup/archive rule to `AGENTS.md`.
- Extended `Project_Guides/MCP_Rhino Plan Log.md` with the archive contract:
  - archive only successful, older-than-three-days, matched PLAN / EXET / TEST triples
  - preserve original names
  - do not archive incomplete, failed, recent, unmatched, or still-active work
- Updated `Project_Guides/MCP_Rhino Architecture.md` to document the current standalone Companion and plugin launch boundaries.
- Updated `Runtime_Workflow/MCP_Rhino Workflow.md` with the current runtime shape:
  - Live Only Rhino execution
  - default bridge debug pipe
  - `_Mcpchat` standalone Companion
  - process-scoped panel-bound pipes
- Updated `README.md` so the current setup and moved sample links remain accurate.
- Updated `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` to compile archived `Project_Archive/Project_Test/**/*.cs` smoke files, preserving historical CLI / Rhino smoke command build coverage after moving tests.
- Updated `.gitignore` to ignore generated root output folders:
  - `.tmp-build/`
  - `artifacts/`
  - `_validation/`
- Moved 16 completed old triples through `260429` into the archive buckets.
- Moved 4 legacy already-archived PLAN files from `Project_Archive/` root into `Project_Archive/Project_Plan/`.
- Removed generated root artifacts after path checks:
  - `.tmp-build/`
  - `artifacts/`
  - `_validation/`
  - `_tmp_rhino_test.csx`

## Differences From Plan

- README was updated even though it was not listed in the plan because moving `260422_TEST_mcp-client-integration` would otherwise leave broken current-documentation links.
- The server csproj was updated because many TEST folders contain C# smoke registrations. Without compiling archived test C# files, archiving them would remove historical smoke handlers from the build.
- `.gitignore` was updated and generated root folders were removed as part of the follow-up cleanup after Rhino was closed.
- `Project_Archive/task-orchestration-workflow.md` was left at the archive root because it is neither a PLAN, EXET, nor TEST artifact.

## Issues Found And Fixed

- The active docs did not mention `MCP_Rhino.Companion` as a sibling project in the architecture evolution section. Added it.
- Runtime workflow did not summarize the current MCP connection shape after the companion and process-scoped panel-pipe changes. Added it.
- README still described two build artifacts and linked to samples under `Project_Test/260422_TEST_mcp-client-integration`, which has now been archived. Updated both.

## Test Record

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- Exit code: 1
- Result: blocked by live process file locks, not by compilation errors.
- Locked processes reported by MSBuild:
  - `Rhino 8 (51864)`
  - `MCP_Rhino.Companion (49512)`
  - `MCP_Rhino.Bridge (53792)`
  - `MCP_Rhino.Bridge (34284)`

After Rhino and Companion were closed, one stale Bridge process remained:

- `MCP_Rhino.Bridge (34284)`

It was stopped because it was still running from this repository's Release output.

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- Exit code: 0
- Result: build succeeded with 0 warnings and 0 errors.

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

- Exit code: 0
- Result: build succeeded with 0 warnings and 0 errors.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] MCP safety annotations verified for 78 tools.`
  - `[OK] No bare method-level [McpServerTool] attributes remain.`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] MCP safety annotations verified for 78 tools.`
  - `[OK] No bare method-level [McpServerTool] attributes remain.`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- geometry-analysis-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

- Exit code: 0
- Result: archived `geometry-analysis-smoke-test` registered and completed successfully in CLI fallback mode.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- geometry-analysis-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

- Exit code: 0
- Result: archived `geometry-analysis-smoke-test` registered and completed successfully in CLI fallback mode.

Archive structure check:

- Exit code: 0
- Result: `Archive buckets OK: plans=20 exets=16 tests=16`

## Acceptance Alignment

- Archive folders exist and contain the completed historical artifacts.
- The root `Project_Plan/`, `Project_Exet/`, and `Project_Test/` folders are reduced to recent, incomplete, or unmatched active artifacts.
- `AGENTS.md` now contains the requested cleanup rule.
- `Project_Guides`, `Runtime_Workflow`, and `README.md` now match the current live-only plugin / bridge / companion MCP setup.
- Debug and Release builds passed after closing live processes.
- Representative Debug and Release smoke verification passed.

## Rollback Verification

- No rollback command was executed.
- Practical rollback is moving archived artifacts back to their original root folders and removing the archive compile include from `MCP_Rhino.Server.csproj`.

## Current Remaining Items

- Decide later whether the legacy `Project_Archive/task-orchestration-workflow.md` should get a dedicated legacy archive bucket or remain as a one-off root archive note.

## Conclusion

The project folder cleanup and rule updates are complete. The archive structure is in place, old completed triples have been moved, generated root artifacts have been removed, current routing and architecture docs reflect the Companion / panel-bound MCP setup, and Debug plus Release builds and smoke checks passed.
