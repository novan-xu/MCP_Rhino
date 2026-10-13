# Review Findings Fix Execution

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_review-findings-fix.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Test folder: `Project_Test/260508_TEST_review-findings-fix/`
- New smoke source: `Project_Test/260508_TEST_review-findings-fix/DeveloperCommandHandler.ReviewFindingsFixSmokeTest.cs`
- README: `Project_Test/260508_TEST_review-findings-fix/README.md`
- Commit / PR: not created in this workspace session

## Execution Result / Actual Scope

- Added guarded rollback handling to `ExecuteWithUndo` in `LiveRhinoDocumentAccessorBase`.
- Changed canonical `FilterObjects` to route through `LiveObjectSelectionSkill.Select`, preserving structured output while restoring layer-query ambiguity checks.
- Cleaned stale or malformed MCP descriptions on active tool wrappers.
- Rewrote legacy filter/selection skill failure text to clear English.
- Replaced stale runtime workflow example usage of `GetLayersInLiveTool` with `GetLayers`.
- Added and registered `review-findings-fix-smoke-test`.

## Differences From Plan

- No live Rhino failure-injection smoke was added. The new smoke verifies the rollback guard statically and the solution builds against RhinoCommon. A future live smoke can intentionally fail after a mutation and verify document object counts.
- CLI usage text cleanup was broader than the review's specific examples, replacing the remaining Chinese usage lines in the same legacy command area with English live-path wording.

## Issues Found And Fixed During Execution

- Some previously suspected mojibake in `DeveloperCommandHandler` was UTF-8 text displayed incorrectly by the terminal. It was still normalized to English to keep developer output consistent.
- `CompositeObjectFilterSkill` and `ObjectSelectionSkill` contained malformed user-facing strings, so both files were rewritten with the same behavior and clean ASCII messages.

## Test Record

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
```

Result: passed, 0 warnings, 0 errors.

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- review-findings-fix-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-overlap-cleanup-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
```

Result: all passed.

```powershell
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: passed, 0 warnings, 0 errors.

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- review-findings-fix-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-overlap-cleanup-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

Result: all passed.

```powershell
git diff --check
```

Result: passed; only existing line-ending conversion warnings were reported.

## Acceptance Criteria Alignment

- Debug and Release solution builds pass.
- Existing MCP safety, overlap cleanup, and surface governance smokes pass in Debug and Release.
- New review findings smoke passes in Debug and Release.
- `FilterObjects` still returns `OperationResponse<RhinoObjectFilterResult>` and now uses ambiguity-safe selection routing.
- Active MCP tool descriptions no longer contain the reviewed stale disk-overwrite or malformed routing fragments.

## Rollback Verification

- Code rollback path is guarded by `CurrentUndoRecordSerialNumber` so it only calls `RhinoDoc.Undo()` when the failed operation produced a new undo record.
- If `RhinoDoc.Undo()` fails, the failure response appends `ROLLBACK_FAILED` instead of hiding possible partial document mutation.
- No git rollback was performed.

## Current Remaining Items

- Add a future live Rhino smoke that intentionally fails after partial mutation and verifies automatic rollback against object/layer counts.
- Consider consolidating layer ambiguity resolution into one application service to remove duplication between selection/filter skills.

## Conclusion

The review findings are fixed at code, metadata, workflow, and regression-smoke levels. Debug and Release validation both passed.
