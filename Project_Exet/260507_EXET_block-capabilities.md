# Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_block-capabilities.md`
- Execution date: 2026-05-07

# Linked Artifacts

- Test folder: `Project_Test/260507_TEST_block-capabilities/`
- Commit / PR: not created in this execution

# Execution Result / Actual Scope

Implemented v1 live-only block capabilities:

- Block read tools:
  - `ListBlockDefinitions`
  - `GetBlockDefinitionDetails`
  - `ListBlockInstances`
  - `GetBlockInstanceDetails`
- Block preview/apply lifecycle tools:
  - `PreviewCreateBlockDefinitions` / `ApplyCreateBlockDefinitions`
  - `PreviewInsertBlockInstances` / `ApplyInsertBlockInstances`
  - `PreviewTransformBlockInstances` / `ApplyTransformBlockInstances`
  - `PreviewExplodeBlockInstances` / `ApplyExplodeBlockInstances`
  - `PreviewPurgeUnusedBlockDefinitions` / `ApplyPurgeUnusedBlockDefinitions`
- Domain / contract models for source policy, duplicate policy, purge policy, link status, transforms, bounding boxes, definition summaries, instance summaries, preview responses, and apply responses.
- `RhinoBlockInspectionService` and `RhinoBlockLifecycleService`.
- `BlockLifecycleSkill`.
- Live adapters:
  - `LiveBlockInspector`
  - `LiveBlockLifecycleOperator`
- CLI smoke slug: `block-capabilities-smoke-test`.
- Rhino smoke command: `_McpBlockCapabilitiesSmoke`.
- Existing architectural block wrappers were retained as compatibility entry points.

# Deviations From Plan

- Implementation uses shared `BlockOperationPreviewItemResponse` and `BlockOperationResultResponse` instead of separate `BlockDefinitionOperationResultResponse` / `BlockInstanceOperationResultResponse`.
- Transform apply preserves the target object id by duplicating/transformed `InstanceReferenceGeometry` and calling `document.Objects.Replace`; `ObjectTable.Transform` was avoided because RhinoCommon creates a new object id there.
- Purge apply first calls `InstanceDefinitions.Purge(index)` and falls back to `InstanceDefinitions.Delete(index, deleteReferences:false, quiet:true)` for already-validated unused local definitions when Rhino purge is blocked by undo references.
- Rhino live smoke was implemented but not run in this terminal session because it requires Rhino plugin context and a saved active document.

# Issues Found And Fixed During Execution

- The original PLAN overcommitted replacement, definition deletion, source-object deletion, and attribute-edit tooling. The plan was revised before implementation to defer those flows.
- `InstanceDefinitionUpdateType.Embedded` is obsolete and treated as a build error; linked status mapping now avoids directly referencing that enum member.
- The MCP safety smoke inventory was updated from 92 to 106 tools.

# Test Record

Build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
```

Result: exit code 0. Debug build succeeded for Bridge, Companion, and Server with 0 warnings / 0 errors.

```powershell
dotnet build .\MCP_Rhino.sln -c Release --nologo
```

Result: exit code 0. Release build succeeded for Bridge, Companion, and Server with 0 warnings / 0 errors.

Tool safety smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- mcp-tool-safety-annotations-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -- mcp-tool-safety-annotations-smoke-test
```

Key output in both configurations:

```text
[OK] MCP safety annotations verified for 106 tools.
[OK] No bare method-level [McpServerTool] attributes remain.
```

Block CLI fallback smoke:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- block-capabilities-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -- block-capabilities-smoke-test
```

Key output in both configurations:

```text
[OK] block-capabilities CLI fallback returned LIVE_RHINO_REQUIRED for live-only paths.
```

Rhino live smoke:

```text
_McpBlockCapabilitiesSmoke
```

Status: implemented, not executed in this terminal session. Expected context is a loaded MCP_Rhino plugin and a saved active Rhino document.

# Acceptance Criteria Alignment

- Debug and Release solution builds passed.
- MCP safety annotations are explicit and verified for all 106 tools.
- CLI fallback smoke confirms live-only behavior returns `LIVE_RHINO_REQUIRED`.
- Read-only tools are annotated `ReadOnly=true, Destructive=false, OpenWorld=false`.
- Create / insert / transform apply tools are annotated mutation closed-world.
- Explode / purge apply tools are annotated destructive closed-world.
- Linked block refresh remains separate through the existing `UpdateLinkedBlockTool`.
- Source object deletion, local definition replacement, definition deletion with live instances, and block-specific attribute editing remain deferred as planned.

# Rollback Verification

Rollback remains localized:

- Remove `src/MCP_Rhino.Server/Tools/Blocks/`.
- Remove block contracts, domain models/enums, services, interfaces, live adapters, skill, smoke command, and test folder added for this capability.
- Revert DI, agent registration, CLI hook, and MCP safety smoke updates.
- Existing architectural block wrappers and `UpdateLinkedBlockTool` remain available.

# Current Remaining Items

- Run `_McpBlockCapabilitiesSmoke` inside Rhino against a saved active `.3dm` to verify actual live block creation, insertion, transform, explode, and unused-definition cleanup.
- Add v2 plans for local definition replacement, block definition deletion policies, linked definition lifecycle, and block-specific attribute editing if those become required.

# Conclusion

`block-capabilities` v1 is implemented and validated at build / MCP tool metadata / CLI fallback level. The only remaining validation item is the Rhino-hosted live smoke, which requires an active saved Rhino document.
