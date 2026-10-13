# MCP Tool Overlap Cleanup Plan

## Background

The current MCP surface has grown to 132 tools. A capability audit found several tool groups that expose the same live-only operation through more than one MCP method. Some overlap is intentional, such as preview/apply pairs and reference resource fallbacks. Other overlap came from the live-only migration and later feature-family expansion.

The most visible duplicates are:

- unsuffixed and `InLive` variants that now route to the same live-only service path
- thin filter wrappers that duplicate the generic object filter
- architecture-family block tools that duplicate the newer `Tools/Blocks` lifecycle tools

## Goal

Reduce MCP tool overlap without changing Rhino document behavior.

The cleanup should:

- keep one canonical MCP entry point per equivalent capability
- keep preview/apply pairs where the safety model requires them
- keep specialized tools only where they add a meaningful domain boundary
- update project rules so future tool additions avoid reintroducing duplicate live-only aliases
- keep builds and MCP surface smokes passing in Debug and Release

## Architecture Ownership

- `Tools/Analysis`: owns the canonical generic object filter.
- `Tools/Layers`: owns canonical layer reads/searches and layer mutations.
- `Tools/Editing`: owns canonical object user-text reads/previews/applies.
- `Tools/File`: owns canonical document user-string reads/mutations.
- `Tools/Blocks`: owns canonical block definition and instance lifecycle tools.
- `Tools/Geometry/Architecture`: keeps architectural primitives, booleans, openings, and architecture-specific modeling only; block lifecycle duplicate tools should be removed from MCP exposure.
- `Project_Guides/MCP_Rhino Architecture.md`: receives a small governance rule for avoiding equivalent live-only aliases.
- `Runtime_Workflow/MCP_Rhino Workflow.md`: receives runtime routing guidance to prefer canonical tools over legacy aliases.

## Key Design

### 1. Canonicalize Live-Only Duplicate Pairs

Because the repository is live-only, do not expose both `Foo` and `FooInLive` when they call the same live service path.

Keep the shorter canonical names where they are already stable and update their descriptions/return shape if needed:

- keep `GetLayers`, remove `GetLayersInLive`
- keep `FindLayerCandidates`, remove `FindLayerCandidatesInLive`
- keep `GetObjectUserStrings`, remove `GetObjectUserStringsInLive`
- keep `PreviewObjectUserTextWrites`, remove `PreviewObjectUserTextWritesInLive`
- keep `GetDocumentUserStrings`, remove `GetDocumentUserStringsInLive`
- keep `FilterObjects`, remove `FilterObjectsInLive`

### 2. Collapse Thin Filter Wrappers

Keep `FilterObjects` as the canonical object filter. Remove thin wrapper tools that only partially bind the same filter criteria:

- `FilterObjectsByType`
- `FilterObjectsByUserAttributes`
- `FilterObjectsByLayer`

Filtering by type, user text, layer query, and confirmed layer path remains available through `FilterObjects`.

### 3. Make `Tools/Blocks` Canonical

Keep the newer `Tools/Blocks` preview/apply block lifecycle surface:

- `PreviewCreateBlockDefinitions`
- `ApplyCreateBlockDefinitions`
- `PreviewInsertBlockInstances`
- `ApplyInsertBlockInstances`
- plus the existing block list/detail/transform/explode/purge tools

Remove duplicate architecture-family block lifecycle tools:

- `CreateBlockDefinitions`
- `InsertBlockInstances`

### 4. Do Not Remove Lower-Confidence Overlap Yet

Keep these surfaces for now:

- `EditControlPoints` / `PreviewEditControlPoints`
- `Geometry/Edit` descriptor-driven edit tools
- standard four-point rebuild and lower-level rebuild/flip/dir tools
- viewport capture vs file export tools
- reference resources and read-only reference tool fallback

Those overlaps either represent different routing levels, different output modes, or need a separate migration proof before removal.

## Involved Files

Likely production files:

- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsByTypeTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/FilterObjectsByUserAttributesTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/FilterObjectsByLayerTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/GetLayersInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/FindLayerCandidatesTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/FindLayerCandidatesInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/GetObjectUserStringsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/PreviewObjectUserTextWritesInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/File/GetDocumentUserStringsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/CreateBlockDefinitionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Architecture/InsertBlockInstancesTool.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Runtime_Workflow/MCP_Rhino Workflow.md`

Required test artifacts:

- `Project_Test/260508_TEST_mcp-tool-overlap-cleanup/`
- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`

## Usage

After cleanup, runtime routing should use:

- `FilterObjects` for layer/type/user-attribute object filtering
- `FindLayerCandidates` and `GetLayers` for layer discovery
- `GetObjectUserStrings`, `PreviewObjectUserTextWrites`, `ApplyObjectUserTextWrites`, and `DeleteObjectUserText` for object user text
- `GetDocumentUserStrings`, `SetDocumentUserStrings`, and `DeleteDocumentUserStrings` for document user strings
- `Tools/Blocks` preview/apply tools for block definition and instance lifecycle

## Acceptance Criteria

- The MCP tool count decreases by removing redundant surfaces.
- Removed overlap tools are absent from reflected MCP inventory.
- Canonical replacement tools remain present.
- `FilterObjects` returns the same structured live filter response shape as the former `FilterObjectsInLive`.
- `FindLayerCandidates` returns structured live layer candidates.
- MCP safety expectations match the new tool surface.
- `dotnet build .\MCP_Rhino.sln -c Debug` passes.
- `dotnet build .\MCP_Rhino.sln -c Release` passes.
- MCP safety annotation smoke passes in Debug and Release.
- MCP surface governance smoke passes in Debug and Release.
- New overlap-cleanup smoke passes in Debug and Release.

## Risks And Rollback

- Risk: existing clients may call a removed duplicate tool name.
  - Mitigation: removed tools all have canonical replacements with the same or broader capability; document replacements in EXET.
- Risk: old smoke tests may reference removed tool source files.
  - Mitigation: update tests that assert safety metadata on removed source files.
- Risk: removing architecture block duplicate tools affects architectural smoke tests.
  - Mitigation: keep underlying services and request DTOs; only remove MCP exposure from the duplicate architecture tool wrappers.

Rollback restores the removed tool wrapper files or `[McpServerTool]` exposure, restores safety expectations, and removes the overlap cleanup smoke and EXET.

## Future Extension

- Separately evaluate whether `EditControlPoints` can be fully replaced by `Geometry/Edit`.
- Consider adding an automated overlap heuristic to surface tools with identical request/service paths, without turning it into a runtime registry.
