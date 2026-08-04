# Bulk Attribute Recipes Plan

## Background

Large object attribute updates are currently possible through object edit and object user text tools, but some workflows still push the LLM toward building very large per-object payloads. When thousands of objects need the same rule-driven attribute changes, this creates long MCP calls, repeated batching, and avoidable model-side data transport.

The faster shape is to send compact selection criteria and compact attribute recipes, then let the Rhino server resolve matching objects and apply derived values locally inside one live document operation.

## Goals

- Add a compact bulk object attribute recipe capability for live Rhino documents.
- Support preview/apply tools so broad edits can be inspected before mutation.
- Apply recipe changes in one Rhino undo record during apply.
- Keep execution in typed C# services first; do not add arbitrary Python/C# script execution.
- Avoid per-object request payloads for common cases such as setting the same user text values across a filtered selection or deriving values from object/layer metadata.

## Architecture Ownership

- `Contracts/Requests`: request DTOs for recipe writes, selection filters, and template-based values.
- `Tools/Editing`: direct MCP preview/apply wrappers.
- `Skills/Editing`: selection + recipe orchestration.
- `Application/Services`: recipe validation, preview formatting data, and apply flow.
- `Infrastructure/Rhino/Live`: no new Rhino adapter is required for the first scope; the service will use the existing `ILiveRhinoDocumentAccessor` and RhinoCommon attribute APIs through the live document boundary.
- `Project_Test/260508_TEST_bulk-attribute-recipes/`: smoke coverage and README.

## Key Design

- Add `PreviewBulkObjectAttributeRecipe` and `ApplyBulkObjectAttributeRecipe`.
- Reuse existing live selection criteria:
  - `LayerQueries`
  - `ConfirmedLayerFullPaths`
  - `ObjectTypes`
  - `UserAttributeConditions`
  - `MatchMode`
  - `UserAttributeMatchMode`
- Add recipe fields:
  - `UserTextWrites`: list of `{ Key, ValueTemplate }`
  - `RemoveUserTextKeys`: list of keys to remove from every matched object
  - `TargetLayerFullPath`: optional common target layer
  - `DisplayColor`: optional common object display color
  - `ObjectNameTemplate`: optional object name template
- Template values are resolved server-side per object. Supported tokens:
  - `{objectId}`
  - `{objectName}`
  - `{layerName}`
  - `{layerFullPath}`
  - `{objectType}`
  - `{geometryType}`
  - `{index}`
  - `{user:<key>}`
- Preview returns only a bounded sample while still reporting total matched object count and operation count.
- Apply mutates matched objects inside one `ExecuteWithUndo` call and returns a bounded result sample.

## Involved Files

- `src/MCP_Rhino.Server/Contracts/Requests/ObjectAttributeRecipeRequest.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectAttributeRecipeService.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectAttributeRecipeSkill.cs`
- `src/MCP_Rhino.Server/Tools/Editing/PreviewBulkObjectAttributeRecipeTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/ApplyBulkObjectAttributeRecipeTool.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs`
- `Project_Test/260508_TEST_mcp-tool-overlap-cleanup/DeveloperCommandHandler.McpToolOverlapCleanupSmokeTest.cs`
- `Project_Test/260508_TEST_bulk-attribute-recipes/`

## Usage

Example MCP call intent:

```json
{
  "filePath": "C:\\Models\\project.3dm",
  "confirmedLayerFullPaths": ["Level 01::Walls"],
  "objectTypes": ["Brep"],
  "userTextWrites": [
    { "key": "Phase", "valueTemplate": "Existing" },
    { "key": "SourceLayer", "valueTemplate": "{layerFullPath}" },
    { "key": "BatchIndex", "valueTemplate": "{index}" }
  ],
  "objectNameTemplate": "{layerName}-{index}"
}
```

The preview tool must be used before broad apply workflows when there is any uncertainty in selection criteria or recipe output.

## Acceptance Criteria

- Debug and Release solution builds pass.
- `mcp-tool-safety-annotations-smoke-test` passes in Debug and Release with the new tools registered.
- `mcp-tool-overlap-cleanup-smoke-test` passes in Debug and Release with updated expected tool counts.
- `mcp-surface-structure-governance-smoke-test` passes in Debug and Release.
- New `bulk-attribute-recipes-smoke-test` passes in Debug and Release.
- The new tool methods have method-level descriptions and explicit MCP safety annotations.
- Apply uses one live document undo record and does not read or write external files.

## Risks And Rollback

- Broad selection criteria can update many objects. The preview/apply split and existing layer ambiguity handling reduce that risk.
- Template token expansion can surprise callers if source metadata is missing. Missing user text source tokens resolve to an empty string rather than failing the entire batch.
- Rollback is a normal git revert of the touched source files plus the PLAN / EXET / TEST artifacts.

## Future Extensions

- Add an `ObjectIds` selection mode if explicit compact sets are needed without filter criteria.
- Add controlled CSV/JSON mapping support as a separate `OpenWorld = true` capability if per-object unique values remain a bottleneck.
- Add built-in named recipes for common firm standards once real workflows stabilize.
