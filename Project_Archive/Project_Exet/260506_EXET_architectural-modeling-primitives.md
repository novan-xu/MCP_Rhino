# Corresponding Plan

- Plan: `Project_Plan/260506_PLAN_architectural-modeling-primitives.md`
- Execution date: 2026-05-07

# Related Artifacts

- Test folder: `Project_Test/260506_TEST_architectural-modeling-primitives/`
- Tool safety smoke updated: `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- Commit / PR: not available in this workspace.

# Execution Result / Actual Scope

Implemented the live-only `architectural-modeling-primitives` capability as an additive MCP tool surface:

- Generic primitives: `CreateBoxes`, `CreateExtrusions`, `CreatePlanarBreps`
- Architectural presets: `CreateSlabs`, `CreateWalls`, `CreateColumns`, `CreateBeams`
- Boolean/opening flow: `PreviewBooleanObjects`, `ApplyBooleanObjects`, `PreviewOpenings`, `ApplyOpenings`
- Local block flow: `CreateBlockDefinitions`, `InsertBlockInstances`
- Workflow: `CreateBuildingMassing`

Added request/response DTOs, domain specs/enums, application services, live Rhino adapters, skill registration, DI registration, tool safety expectations, a CLI/Rhino smoke partial, and `_McpArchitecturalModelingPrimitivesSmoke`.

# Deviations From Plan

- The PLAN was revised before execution to split openings into `PreviewOpenings` / `ApplyOpenings` and to clarify destructive safety semantics.
- `CreateBuildingMassing` creates slabs/columns/walls/beams in one undoable transaction. Opening specs passed to this workflow currently produce a warning and should be applied through `ApplyOpenings`; this keeps the v1 workflow atomic for newly created primitives whose ids are not known before creation.
- Full solution build with implicit solution restore (`dotnet build .\MCP_Rhino.sln -c Debug --nologo`) and explicit solution restore (`dotnet restore .\MCP_Rhino.sln --nologo`) failed in this environment with no reported compiler errors. Each project restores successfully, and the solution builds successfully with `--no-restore -m:1` after project restore.
- In-Rhino live smoke was added but not executed in this headless CLI session.

# Issues Found And Fixed During Construction

- Updated `GeometryCreationCommonOptions` usage so architectural metadata is passed separately and canonical `mcp.*` user-text keys are written by the live adapter.
- Updated the MCP tool safety smoke expectation list so the new tool surface cannot be added without explicit `ReadOnly`, `Destructive`, and `OpenWorld` validation.
- Added CLI fallback smoke coverage to ensure live-only capability paths return `LIVE_RHINO_REQUIRED` outside Rhino.

# Test Record

Passed:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo
dotnet restore src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo
dotnet restore src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj --nologo
dotnet restore src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj --nologo
dotnet build .\MCP_Rhino.sln -c Debug --no-restore --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --no-restore --nologo -m:1
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- mcp-tool-safety-annotations-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- architectural-modeling-primitives-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -- mcp-tool-safety-annotations-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -- architectural-modeling-primitives-smoke-test
```

Key outputs:

- `[OK] MCP safety annotations verified for 92 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`
- `[OK] architectural-modeling-primitives CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.`
- Debug and Release no-restore solution builds completed with `0 Warning(s)` and `0 Error(s)`.

Not run:

```text
_McpArchitecturalModelingPrimitivesSmoke
```

Reason: requires Rhino plugin context and a saved active `.3dm` document.

# Acceptance Alignment

- Live-only contract is preserved through `ILiveRhinoDocumentAccessor`; CLI fallback returns `LIVE_RHINO_REQUIRED`.
- New MCP tools have explicit safety annotations and are covered by the safety smoke.
- Atomic creation tools reject missing layers unless `AutoCreateLayers=true`.
- Boolean and opening apply paths compute result Breps before document mutation and run inside one undo record.
- Block definition creation is local-document only and rejects duplicate definition names.
- Source object deletion during block definition creation is intentionally omitted in v1.

# Rollback Verification

Rollback remains additive:

- Remove `Tools/Geometry/Architecture/`
- Remove architectural skills, services, interfaces, live adapters, contracts, domain specs/enums, smoke command, and `Project_Test/260506_TEST_architectural-modeling-primitives/`
- Revert DI, agent registration, `DeveloperCommandHandler`, PLAN revision, and MCP tool safety smoke changes

Existing geometry, analysis, layer, export, drawing, panel, and bridge code paths were not intentionally modified beyond shared registration.

# Current Remaining Items

- Run `_McpArchitecturalModelingPrimitivesSmoke` inside Rhino with a saved active document.
- Investigate the silent solution restore failure separately if strict `dotnet build .\MCP_Rhino.sln -c <Config>` without `--no-restore` remains required for CI.
- Consider a second pass to make `CreateBuildingMassing` apply opening specs against newly created semantic targets rather than warning.

# Conclusion

Implementation, CLI fallback validation, tool safety validation, and Debug/Release build validation are complete. Full Rhino live acceptance remains pending until the plugin smoke is run inside Rhino.

