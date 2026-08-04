# MCP Tool Overlap Cleanup EXET

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_mcp-tool-overlap-cleanup.md`
- Execution date: 2026-05-08

## Related Artifacts

- Test folder: `Project_Test/260508_TEST_mcp-tool-overlap-cleanup/`
- Smoke source: `Project_Test/260508_TEST_mcp-tool-overlap-cleanup/DeveloperCommandHandler.McpToolOverlapCleanupSmokeTest.cs`
- Updated safety smoke: `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- Updated panel CLI smoke assertion: `Project_Test/260506_TEST_llm-panel-cli-switching/DeveloperCommandHandler.LlmPanelCliSwitchingSmokeTest.cs`
- Commit / PR: none in this workspace execution

## Execution Result / Actual Scope

Reduced the MCP tool surface from 132 tools to 121 tools by removing 11 duplicate wrappers.

Removed MCP tool wrappers:

- `FilterObjectsInLive`
- `FilterObjectsByType`
- `FilterObjectsByUserAttributes`
- `FilterObjectsByLayer`
- `GetLayersInLive`
- `FindLayerCandidatesInLive`
- `GetObjectUserStringsInLive`
- `PreviewObjectUserTextWritesInLive`
- `GetDocumentUserStringsInLive`
- `CreateBlockDefinitions`
- `InsertBlockInstances`

Canonical tools retained:

- `FilterObjects`
- `FindLayerCandidates`
- `GetLayers`
- `GetObjectUserStrings`
- `PreviewObjectUserTextWrites`
- `GetDocumentUserStrings`
- `PreviewCreateBlockDefinitions`
- `ApplyCreateBlockDefinitions`
- `PreviewInsertBlockInstances`
- `ApplyInsertBlockInstances`

Behavioral notes:

- `FilterObjects` now returns the structured live filter response previously exposed by `FilterObjectsInLive`.
- `FindLayerCandidates` now returns a structured live candidate list instead of formatted text.
- Underlying services, skills, request DTOs, CLI handlers, and block lifecycle implementation remain available.
- No Rhino mutation behavior, Undo behavior, resource registration, or bridge/panel host behavior was changed.

Rule updates:

- `Project_Guides/MCP_Rhino Architecture.md` now states that equivalent live-only aliases and thin single-criterion wrappers should not remain exposed unless they own a meaningful domain boundary.
- `Runtime_Workflow/MCP_Rhino Workflow.md` now names canonical routing choices for filter, layer, and block lifecycle operations.

## Deviations From Plan

- The lower-confidence `EditControlPoints` / `Geometry/Edit` overlap was intentionally left untouched, as planned. It needs a separate migration proof before removal.
- The exact Analysis family count is 13 after cleanup, not 12. The initial smoke expectation was corrected after the first Debug smoke run showed the accurate inventory.

## Issues Found And Fixed

- The first Debug overlap smoke failed because the new smoke expected `Analysis: 12`; the actual cleaned family count is `Analysis: 13`. The smoke was corrected and rerun successfully.
- The panel CLI switching smoke referenced the removed `FilterObjectsByUserAttributesTool.cs` source file for safety metadata checks. It now checks the canonical `FilterObjectsTool.cs`.

## Test Record

Debug build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result: passed with 0 warnings and 0 errors.

Debug overlap cleanup smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-overlap-cleanup-smoke-test
```

Result:

- `[OK] MCP tool overlap cleanup smoke verified canonical tools and removed duplicate wrappers.`
- `[OK] MCP tool count=121; removed duplicate wrappers=11.`

Debug safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 121 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Debug surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- `[OK] MCP tool inventory discovered 121 tools.`
- touched family counts: `Analysis: 13`, `Layers: 11`, `Editing: 6`, `File: 3`, `Geometry/Architecture: 12`, `Blocks: 14`
- `[OK] MCP resource inventory discovered 5 resources.`

Debug panel CLI switching smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- llm-panel-cli-switching-smoke-test
```

Result: passed, including the canonical object filter safety assertions.

Release build:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result: passed with 0 warnings and 0 errors.

Release overlap cleanup smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-overlap-cleanup-smoke-test
```

Result:

- `[OK] MCP tool overlap cleanup smoke verified canonical tools and removed duplicate wrappers.`
- `[OK] MCP tool count=121; removed duplicate wrappers=11.`

Release safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 121 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Release surface governance smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- `[OK] MCP tool inventory discovered 121 tools.`
- touched family counts: `Analysis: 13`, `Layers: 11`, `Editing: 6`, `File: 3`, `Geometry/Architecture: 12`, `Blocks: 14`
- `[OK] MCP resource inventory discovered 5 resources.`

Release panel CLI switching smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- llm-panel-cli-switching-smoke-test
```

Result: passed, including the canonical object filter safety assertions.

Repository check:

```powershell
git diff --check
```

Result: passed with only line-ending normalization warnings.

## Acceptance Criteria Alignment

- MCP tool count decreases: met, 132 to 121.
- Removed overlap tools are absent from reflected MCP inventory: met.
- Canonical replacement tools remain present: met.
- `FilterObjects` returns structured live filter response: met.
- `FindLayerCandidates` returns structured live layer candidates: met.
- MCP safety expectations match new surface: met.
- Debug and Release builds pass: met.
- Debug and Release safety and surface governance smokes pass: met.
- New overlap cleanup smoke passes in Debug and Release: met.

## Rollback Verification

Rollback can restore the deleted duplicate tool wrapper files, restore the old `FilterObjectsTool` and `FindLayerCandidatesTool` return shapes, re-add the removed method names to the safety expectation smoke, remove the overlap cleanup smoke hook, and remove the rule updates.

Because the underlying services and DTOs were not removed, rollback is limited to MCP wrapper exposure and test/rule metadata.

## Current Remaining Items

- `EditControlPoints` still overlaps conceptually with descriptor-driven `Geometry/Edit` tools. It was not removed because equivalence was not proven.
- Some older tool descriptions outside the cleaned wrappers still contain mojibake or legacy disk-wording. That is separate metadata cleanup.

## Conclusion

`260508_PLAN_mcp-tool-overlap-cleanup.md` is executed. The MCP surface now exposes 121 tools, with the most obvious duplicate wrappers removed and canonical routing documented.
