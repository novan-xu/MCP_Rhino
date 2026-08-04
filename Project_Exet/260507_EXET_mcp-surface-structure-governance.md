# MCP Surface Structure Governance EXET

## Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_mcp-surface-structure-governance.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Test folder: `Project_Test/260507_TEST_mcp-surface-structure-governance/`
- Commit / PR: none in this workspace execution

## Execution Result / Actual Scope

- Updated `AGENTS.md` so the entry routing layer recognizes reference-only MCP resources while still deferring detailed behavior to `Runtime_Workflow/`.
- Updated `Project_Guides/MCP_Rhino Architecture.md` with:
  - `Server/ResourceRegistration.cs` ownership
  - `Resources/` ownership and reference-only constraints
  - tool family ownership, including `Tools/Selection`, `Tools/Viewport`, `Tools/Reference`, and `Tools/Geometry/CurveOps`
  - MCP surface governance rules for assembly registration, descriptions, duplicate tool names, preview/apply conventions, and inventory validation
- Updated `Runtime_Workflow/MCP_Rhino Workflow.md` with resource-vs-tool routing rules.
- Added `src/MCP_Rhino.Server/Server/ResourceRegistration.cs` using SDK assembly scanning via `WithResourcesFromAssembly(...)`.
- Added `src/MCP_Rhino.Server/Resources/README.md` documenting the reference-only resource boundary.
- Wired `.AddRhinoResources()` into both global debug-pipe and panel-bound MCP host creation paths.
- Added `mcp-surface-structure-governance-smoke-test`, which enumerates tool/resource inventory and fails on:
  - duplicate MCP tool method names
  - missing method-level descriptions
  - missing explicit `ReadOnly`, `Destructive`, or `OpenWorld` source metadata

## Deviation From Plan

- The plan allowed either documenting resource support or rejecting it after SDK research. Local `ModelContextProtocol` 1.1.0 supports `WithResourcesFromAssembly(...)`, so resource support was accepted and a registration entry point was added.
- No resource content was added. The smoke currently reports `0` resources, which is expected for this structure-only execution.
- `AGENTS.md` was minimally updated even though the draft listed the deeper guide files first. This keeps the repository entry point aligned with the new resource routing concept without duplicating detailed rules.
- No generated Markdown inventory was committed. Inventory remains smoke output only.

## Issues Found And Fixed During Execution

- The repository already had unrelated active construction changes in CLI, DI, and tool-safety files. This execution preserved them and only added the structure-governance hook to the existing partial CLI registration pattern.
- SDK resource support was verified locally from the installed `ModelContextProtocol` package before adding `ResourceRegistration`.

## Test Record

Command:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result:

- Exit code: 0
- Output summary: `Build succeeded. 0 Warning(s), 0 Error(s).`

Command:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result:

- Exit code: 0
- Output summary: `Build succeeded. 0 Warning(s), 0 Error(s).`

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- Exit code: 0
- Key output: `[OK] MCP tool inventory discovered 106 tools.`
- Key output: `[OK] MCP resource inventory discovered 0 resources.`
- Family counts: Analysis 15, Blocks 14, Drawing 6, Editing 8, File 4, File/Export 6, File/Reference 2, Geometry 12, Geometry/Architecture 14, Geometry/Edit 5, Geometry/Rebuild 7, Layers 12, Workflow 1.

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-surface-structure-governance-smoke-test
```

Result:

- Exit code: 0
- Key output: `[OK] MCP tool inventory discovered 106 tools.`
- Key output: `[OK] MCP resource inventory discovered 0 resources.`
- Family counts matched Debug.

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- Exit code: 0
- Output summary: `[OK] MCP safety annotations verified for 106 tools.`
- Output summary: `[OK] No bare method-level [McpServerTool] attributes remain.`

Command:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

- Exit code: 0
- Output summary: `[OK] MCP safety annotations verified for 106 tools.`
- Output summary: `[OK] No bare method-level [McpServerTool] attributes remain.`

## Acceptance Criteria Alignment

- Architecture guide documents MCP resources as accepted reference-only structure.
- Runtime workflow documents when to use resources instead of tools.
- Tool family ownership and preview/apply conventions are documented.
- `Tools/Selection`, `Tools/Viewport`, `Tools/Reference`, and `Tools/Geometry/CurveOps` are documented as future family boundaries.
- No hand-maintained runtime tool catalog was introduced.
- Inventory smoke enumerates tools/resources and validates descriptions, duplicate method names, and safety metadata presence.
- Debug and Release solution builds passed.
- Debug and Release MCP tool safety smoke passed.

## Rollback Verification

Rollback path:

- Remove `.AddRhinoResources()` calls from `ServerBootstrap` and `BoundHostFactory`.
- Remove `src/MCP_Rhino.Server/Server/ResourceRegistration.cs`.
- Remove `src/MCP_Rhino.Server/Resources/README.md`.
- Remove `Project_Test/260507_TEST_mcp-surface-structure-governance/`.
- Remove `RegisterMcpSurfaceStructureGovernanceHandlers()` hook from `DeveloperCommandHandler.cs`.
- Revert the `AGENTS.md`, architecture guide, and runtime workflow rule sections added by this execution.

Existing `ToolRegistration` remains assembly-based and can continue unchanged after rollback.

## Current Remaining Items

- No reference resources are implemented yet.
- No `Tools/Selection`, `Tools/Viewport`, `Tools/Reference`, or `Tools/Geometry/CurveOps` tools were added in this plan.
- Future tool additions still need to update the existing safety expectation smoke when tool names change.

## Conclusion

The MCP surface structure governance plan is executed and validated. The project now has documented surface ownership, accepted reference-only MCP resource structure, resource registration support, and a repeatable inventory smoke for the current 106-tool surface.
