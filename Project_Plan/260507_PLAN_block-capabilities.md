# Background

MCP_Rhino now has a narrow block baseline from `architectural-modeling-primitives`:

- `CreateBlockDefinitionsTool` creates local Rhino block definitions from existing live document object ids.
- `InsertBlockInstancesTool` inserts local block instances by definition name with translation, Z rotation, uniform scale, layer/name/color/user-text options.
- `UpdateLinkedBlockTool` refreshes existing linked block definitions.
- Existing filtering / selection paths can identify `InstanceReference` objects as block instances.

That is enough for simple reuse workflows, but it is not yet a complete block capability set. There is no dedicated block inspection surface, preview/apply block lifecycle workflow, transform/explode/purge workflow, nested/link status reporting, or block-specific smoke coverage.

# Goal

Add a dedicated live-only `block-capabilities` capability for v1 Rhino block workflows:

- List and inspect local and linked block definitions.
- List and inspect block instances, including definition name, transform, layer, bounding box, and user text where available.
- Preview and apply block definition creation from existing live objects.
- Preview and apply block instance insertion.
- Preview and apply block instance transforms.
- Preview and apply block instance explode.
- Preview and apply purge of unused local block definitions.
- Keep linked-block refresh separate from local block lifecycle, while exposing linked definition status in read tools.
- Keep existing architectural block tools as compatibility wrappers unless execution records a deliberate migration.

Non-goals for v1:

- Disk `.3dm` read/write fallback.
- Creating, relinking, embedding, or replacing linked block definitions.
- Replacing local block definition geometry.
- Deleting source objects while creating definitions.
- Deleting block definitions that still have instances.
- Editing block instance attributes through a block-specific tool.
- Editing nested block definition contents in place.
- Full asset-library management or replacing Rhino's native BlockManager UI.

# Architecture Ownership

- `Tools/Blocks/`
  - New dedicated MCP tools for block read, preview, and mutation.
  - Tool classes end in `Tool`.
  - Tools only receive parameters, call skills/services, and return typed responses.

- `Skills/Modeling/`
  - `BlockLifecycleSkill`: fixed workflows for create / insert / transform / explode / purge.
  - The skill composes application services and does not call RhinoCommon directly.

- `Application/Services/`
  - `RhinoBlockInspectionService`: definition and instance read behavior.
  - `RhinoBlockLifecycleService`: create / insert / transform / explode / purge orchestration.
  - Existing `RhinoBlockDefinitionService` may remain for compatibility wrappers or delegate to the new lifecycle service.

- `Application/Interfaces/`
  - `ILiveBlockInspector`
  - `ILiveBlockLifecycleOperator`
  - These isolate RhinoCommon `InstanceDefinitionTable`, `ObjectTable`, and instance-reference details.

- `Infrastructure/Rhino/Live/`
  - `LiveBlockInspector`
  - `LiveBlockLifecycleOperator`
  - All RhinoCommon block API calls stay here and run through `ILiveRhinoDocumentAccessor`.

- `Domain/Models` and `Domain/Enums`
  - Pure block specs, snapshots, impact models, source policies, duplicate-name policies, purge policies, and link status enums.

- `Contracts/Requests` and `Contracts/Responses`
  - MCP-facing DTOs only.

- `Server/DependencyInjection.cs`
  - Register services and live adapters.

- `Server/AgentRegistration.cs`
  - Register `BlockLifecycleSkill`.

All paths remain live-only. No disk fallback is allowed after live access fails.

# Key Design

## 1. Separate Block Inspection From Mutation

Read tools provide the model state needed before mutation:

- `ListBlockDefinitionsTool`
- `GetBlockDefinitionDetailsTool`
- `ListBlockInstancesTool`
- `GetBlockInstanceDetailsTool`

Definition details should include:

- definition id / index / name / description
- local vs linked status
- source archive path and update status for linked definitions when available
- object count inside definition
- instance count in the document
- nested referenced definition names where detectable
- bounding box summary
- user text / metadata where available

Instance details should include:

- instance object id
- definition name / id
- transform matrix plus decomposed translation / Z rotation / scale where possible
- layer, name, visibility, and color source
- bounding box
- user text / metadata

Nested parent-chain reporting is best-effort in v1. If RhinoCommon does not expose reliable parent-chain information for the selected instance, the response should omit it or return a warning instead of guessing.

## 2. Add Block-Owned Preview / Apply Surfaces

The existing architectural tools are useful but sit under `Tools/Geometry/Architecture/`. V1 adds block-owned tools under `Tools/Blocks/` and keeps the existing architectural wrappers as compatibility entry points.

New block-owned lifecycle tools:

- `PreviewCreateBlockDefinitionsTool`
- `ApplyCreateBlockDefinitionsTool`
- `PreviewInsertBlockInstancesTool`
- `ApplyInsertBlockInstancesTool`
- `PreviewTransformBlockInstancesTool`
- `ApplyTransformBlockInstancesTool`
- `PreviewExplodeBlockInstancesTool`
- `ApplyExplodeBlockInstancesTool`
- `PreviewPurgeUnusedBlockDefinitionsTool`
- `ApplyPurgeUnusedBlockDefinitionsTool`

Preview tools must validate against the current live document and perform no mutation. Apply tools must compute validation before mutation, use one Rhino undo record per apply call, refresh views on mutation, and return per-item results plus warnings/failures.

## 3. Source-Object Policy Is Explicit And Non-Destructive In V1

Definition creation accepts a `SourceObjectPolicy`:

- `KeepVisible`
- `HideSourceObjects`

Default: `KeepVisible`.

`DeleteSourceObjects` is deferred because method-level MCP safety annotations are static. Supporting delete in the same apply method would make `ApplyCreateBlockDefinitionsTool` destructive for all calls. V1 therefore keeps the creation apply method non-destructive and rejects any unsupported source policy with a hard validation error.

## 4. Duplicate Definition Policy Is Explicit

Definition creation accepts:

- `Reject`
- `VersionedName`

Default: `Reject`.

V1 does not support `ReplaceLocalDefinition`. Replacement changes every existing instance's visual meaning and requires a separate destructive preview/apply contract. That workflow is deferred until the implementation can report exact instance impact and rollback behavior.

## 5. Block Instance Transform

Transform tools target existing block instance object ids only. Preview reports the requested transform and resolved target instance metadata. Apply uses RhinoCommon object transformation through the live adapter, preserves object attributes, and records one undo entry for the call.

Block-specific attribute editing is deferred in v1. Existing generic object attribute / user-text tools remain the preferred path for metadata edits until a block-specific wrapper adds enough value.

## 6. Explode And Purge Are Destructive Preview / Apply Workflows

V1 adds:

- `PreviewExplodeBlockInstancesTool`
- `ApplyExplodeBlockInstancesTool`
- `PreviewPurgeUnusedBlockDefinitionsTool`
- `ApplyPurgeUnusedBlockDefinitionsTool`

Rules:

- Explode is destructive because it removes instance objects and creates child geometry.
- Explode rejects unsupported nested cases rather than producing partial or misleading output.
- Purge only targets unused local definitions.
- Definitions with live instances are rejected by purge.
- Linked definitions are reported by read tools but not modified by local purge tools.

Definition deletion is deferred. Purge covers the safe v1 cleanup case: unused local definitions only.

## 7. Linked Block Handling Remains Bounded

Existing `UpdateLinkedBlockTool` remains the linked refresh path. This capability adds read visibility for linked definitions and keeps local lifecycle tools from mutating linked definitions.

Possible v2:

- `CreateLinkedBlockDefinitionTool`
- `RelinkBlockDefinitionTool`
- `EmbedLinkedBlockDefinitionTool`

These are open-world because they read caller-provided filesystem paths or external state, so they need a separate plan or explicit staged extension.

# Proposed Public Tools

Read-only closed-world:

- `ListBlockDefinitionsTool`
- `GetBlockDefinitionDetailsTool`
- `ListBlockInstancesTool`
- `GetBlockInstanceDetailsTool`
- `PreviewCreateBlockDefinitionsTool`
- `PreviewInsertBlockInstancesTool`
- `PreviewTransformBlockInstancesTool`
- `PreviewExplodeBlockInstancesTool`
- `PreviewPurgeUnusedBlockDefinitionsTool`

Mutation closed-world:

- `ApplyCreateBlockDefinitionsTool`
- `ApplyInsertBlockInstancesTool`
- `ApplyTransformBlockInstancesTool`

Destructive closed-world:

- `ApplyExplodeBlockInstancesTool`
- `ApplyPurgeUnusedBlockDefinitionsTool`

Existing open-world linked tool remains:

- `UpdateLinkedBlockTool`

# Request And Response Contracts

Likely request DTOs:

- `ListBlockDefinitionsRequest`
- `GetBlockDefinitionDetailsRequest`
- `ListBlockInstancesRequest`
- `GetBlockInstanceDetailsRequest`
- `PreviewCreateBlockDefinitionsRequest`
- `ApplyCreateBlockDefinitionsRequest`
- `PreviewInsertBlockInstancesRequest`
- `ApplyInsertBlockInstancesRequest`
- `PreviewTransformBlockInstancesRequest`
- `ApplyTransformBlockInstancesRequest`
- `PreviewExplodeBlockInstancesRequest`
- `ApplyExplodeBlockInstancesRequest`
- `PreviewPurgeUnusedBlockDefinitionsRequest`
- `ApplyPurgeUnusedBlockDefinitionsRequest`

Likely shared DTOs:

- `BlockDefinitionSourceItemRequest`
- `BlockInstancePlacementRequest`
- `BlockTransformRequest`
- `BlockInstanceTargetRequest`
- `BlockSourceObjectPolicy`
- `BlockDuplicateDefinitionPolicy`
- `BlockPurgePolicy`
- `BlockLinkStatus`

Likely response DTOs:

- `BlockDefinitionListResponse`
- `BlockDefinitionDetailResponse`
- `BlockInstanceListResponse`
- `BlockInstanceDetailResponse`
- `BlockMutationPreviewResponse`
- `BlockMutationApplyResponse`
- `BlockDefinitionOperationResultResponse`
- `BlockInstanceOperationResultResponse`
- `BlockImpactResponse`
- `BlockNestedReferenceResponse`

Responses should include:

- `FilePath`
- requested / matched / changed / failed counts
- definition ids / names / indices
- instance object ids
- layer full paths
- bounding boxes
- warnings
- per-item failures
- nested / linked status where relevant

# Involved Files

New files:

- `src/MCP_Rhino.Server/Tools/Blocks/ListBlockDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/GetBlockDefinitionDetailsTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/ListBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/GetBlockInstanceDetailsTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/PreviewCreateBlockDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/ApplyCreateBlockDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/PreviewInsertBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/ApplyInsertBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/PreviewTransformBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/ApplyTransformBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/PreviewExplodeBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/ApplyExplodeBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/PreviewPurgeUnusedBlockDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/ApplyPurgeUnusedBlockDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/BlockLifecycleSkill.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoBlockInspectionService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoBlockLifecycleService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveBlockInspector.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveBlockLifecycleOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBlockInspector.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBlockLifecycleOperator.cs`
- new block request/response DTOs under `src/MCP_Rhino.Server/Contracts/`
- new block domain models/enums under `src/MCP_Rhino.Server/Domain/`
- `Project_Test/260507_TEST_block-capabilities/DeveloperCommandHandler.BlockCapabilitiesSmokeTest.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpBlockCapabilitiesSmokeCommand.cs`

Modified files:

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- existing architectural block wrappers only if they are delegated to the new block-owned service.

Expected CLI smoke slug:

- `block-capabilities-smoke-test`

Expected Rhino command:

- `_McpBlockCapabilitiesSmoke`

# Usage

Example list definitions:

```json
{
  "filePath": "C:/model/site.3dm",
  "includeLinked": true,
  "includeNestedSummary": true
}
```

Example create definition:

```json
{
  "filePath": "C:/model/site.3dm",
  "items": [
    {
      "name": "facade-window-module-a",
      "description": "Window bay module",
      "sourceObjectIds": ["00000000-0000-0000-0000-000000000000"],
      "basePoint": { "x": 0, "y": 0, "z": 0 },
      "sourceObjectPolicy": "KeepVisible",
      "duplicateDefinitionPolicy": "Reject"
    }
  ]
}
```

Example insert instances:

```json
{
  "filePath": "C:/model/site.3dm",
  "items": [
    {
      "definitionName": "facade-window-module-a",
      "origin": { "x": 0, "y": 0, "z": 3.2 },
      "rotationDegrees": 0,
      "scale": 1
    }
  ],
  "common": {
    "layerFullPath": "A-FACADE",
    "name": "facade window module instance"
  }
}
```

# Acceptance Criteria

Build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
```

If solution-level restore remains blocked by the known environment issue, record the workaround in EXET and run:

```powershell
dotnet restore src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo
dotnet restore src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj --nologo
dotnet restore src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj --nologo
dotnet build .\MCP_Rhino.sln -c Debug --no-restore --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --no-restore --nologo -m:1
```

Tool safety:

- Add every new tool method to `mcp-tool-safety-annotations-smoke-test`.
- Run the safety smoke in Debug and Release validation context.
- Confirm read-only, mutation, destructive, and open-world annotations match the architecture guide.

CLI fallback smoke:

- `block-capabilities-smoke-test` runs outside Rhino plugin context.
- All live block tools / services return `LIVE_RHINO_REQUIRED`.
- No business mutation is attempted in CLI fallback mode.

Live smoke:

- Opens a saved active `.3dm` in Rhino.
- Creates source geometry on a known layer.
- Previews and creates one local block definition from source object ids.
- Lists block definitions and verifies the created definition is present.
- Inserts at least two instances with different transforms.
- Lists block instances and verifies definition name, object ids, layer, transform, and bounding boxes.
- Previews and applies one block instance transform.
- Previews and applies explode for one inserted instance.
- Previews and purges an unused local definition.
- Verifies linked definitions, if present, are reported but not modified by local purge tools.
- Verifies each apply tool produces one Undo record and Rhino `Undo` restores expected state.

Boundary tests:

- Empty definition / instance item list returns hard validation error.
- Missing source object id returns hard validation error.
- Duplicate definition name obeys selected policy.
- Insert missing definition returns hard validation error.
- Missing target layer returns clear error unless `AutoCreateLayers=true`.
- Purge definition with live instances is rejected.
- Linked definition mutation through local lifecycle tools is rejected.
- Explode unsupported or nested instance cases return clear warnings/failures without partial mutation.

# Risks And Rollback

Risks:

- RhinoCommon block APIs differ in exact behavior for local vs linked definitions.
- Nested blocks can make impact reporting incomplete unless recursion is carefully tested.
- Explode behavior can create mixed geometry / attributes and may not preserve all metadata.
- Linked block status APIs may expose platform-specific or file-state-dependent values.

Mitigations:

- Keep read/preview tools first.
- Stage v1 as inspect/create/insert/transform/explode/purge.
- Defer local definition replacement, definition deletion, source object deletion, attribute edits, and linked definition lifecycle.
- Reject linked definition mutation in local tools.
- Use one undo record per apply call.
- Add live smoke assertions for object counts, definition counts, instance counts, and undo behavior.

Rollback:

- Remove new `Tools/Blocks/`.
- Remove new block skills, services, interfaces, live adapters, contracts, domain models/enums, smoke command, and test folder.
- Revert DI, agent registration, CLI hook, and tool safety smoke updates.
- Existing architectural block wrappers and `UpdateLinkedBlockTool` should remain available.

# Follow-Up Extensions

- Linked block creation / relink / embed tools with open-world safety annotations.
- Local block definition replacement with impact preview.
- Block definition deletion with explicit policies for affected instances.
- Block-specific attribute editing if generic object edit workflows are not sufficient.
- Source-object deletion during definition creation via a separate destructive tool or separate destructive apply surface.
- Block asset library import from a controlled directory.
- Facade module skill built on block definitions and opening booleans.
- Nested block dependency graph visualization response.
- Block definition diff preview before replacement.
- Batch block instance distribution on grids, curves, or facade bays.

## Revision Record (2026-05-07)

- Tightened v1 scope after plan review.
- Deferred local definition replacement, definition deletion, block-specific attribute edit, linked definition lifecycle, and source-object deletion.
- Added the missing transform and purge tool inventory.
- Made `ApplyCreateBlockDefinitionsTool` non-destructive by rejecting unsupported source-object delete policy in v1.
