# Stone Wireframe Bake Plan

## Background

The live target document contains repeated stone assembly block instances named with the
`STN-MET-GA...-...` convention. Each assembly nests individual flat and vertical stone block
definitions. The required output is a reusable wireframe block for each individual stone occurrence,
containing only the stone's upper horizontal line, placed with the full nested transform and carrying
the parent assembly definition name as instance user text.

The existing block tools can inspect block names and create ordinary definitions or instances, but they
cannot traverse definition members and nested transforms together or select the required definition-local
line reliably. Temporary explode-and-rebuild would alter the source model and is not an acceptable live
workflow. The user has explicitly authorized construction of a dedicated capability and requested a
reusable script because the workflow will be repeated on future files.

## Goals

- Add a live-only, preview/apply stone-wireframe bake workflow.
- Match top-level assemblies by the strict `STN-MET-GA...-...` naming convention while excluding names
  with suffixes such as `_EL` and `_V`.
- Traverse nested block definitions without exploding or editing source assemblies.
- Match flat stones as `STN-MET-1...-...` and vertical stones as `STN-MET-2...-...`.
- Select the uppermost horizontal definition-local line segment, orient it left-to-right, and use its left
  endpoint as the output block base point.
- Create or reuse `WF_<stone-definition-name>` definitions containing exactly that line.
- Insert one output block for each nested stone occurrence with the complete assembly and nested instance
  transforms preserved.
- Write `Assembly_ID=<parent-assembly-definition-name>` as object user text on every output instance.
- Make repeated runs idempotent by reusing compatible definitions and skipping equivalent attributed
  instances.
- Provide a reusable Router-backed Python script for preview and apply in an explicitly selected live
  Rhino document.

## Architecture Ownership

- `Tools/Blocks/`: MCP preview and apply entry points with explicit safety annotations.
- `Skills/Modeling/`: fixed stone-wireframe workflow composition.
- `Application/Services/`: request validation and live-operator orchestration.
- `Application/Interfaces/`: Rhino-independent operator boundary.
- `Infrastructure/Rhino/Live/`: RhinoCommon definition traversal, line selection, transforms, definition
  creation, instance insertion, and user-text writes. All RhinoCommon access runs on Rhino's main thread
  through `ILiveRhinoDocumentAccessor`.
- `Contracts/`: reusable request and response DTOs.
- `Scripts/stone_wireframe_bake.py`: thin installed-Router client; it contains no duplicate geometry logic.
- `Project_Test/260901_TEST_stone-wireframe-bake/`: deterministic and live smoke coverage.

## Key Design

### 1. Fixed Naming Contract

The workflow uses bounded, code-owned name recognition rather than caller-provided regular expressions:

- assembly: `STN-MET-GA` followed by alphanumeric type text, a hyphen, and an alphanumeric occurrence code
- flat stone: `STN-MET-1` followed by the same bounded suffix shape
- vertical stone: `STN-MET-2` followed by the same bounded suffix shape

Callers may optionally provide an exact set of assembly definition names to process. Every provided name
must satisfy the assembly contract. Without a filter, all matching assembly instances in the live document
are processed. Names ending in `_EL` or `_V` do not satisfy the strict full-name contract.

### 2. Non-Destructive Recursive Traversal

For each matching top-level assembly instance, recursively traverse its definition members. Compose the
top-level instance transform with every nested instance-reference transform. When a matching individual
stone definition is reached, record one desired output placement and stop descending through that stone's
internal hardware or annotation blocks.

The workflow does not explode, delete, hide, replace, or otherwise modify source objects or definitions.
Cycle detection and a depth bound fail closed for malformed nested definition graphs.

### 3. Deterministic Line Selection

Inspect only direct curve objects in each matched stone definition. Extract exact linear curves and linear
segments from supported segmented curve types. A candidate is nonzero and horizontal in the stone's local
XY coordinate system within document tolerance. Select the candidate with the greatest local Y coordinate;
for coincident candidates select the longest. Canonicalize its endpoints from lower X to higher X so the
lower-X endpoint is the base point.

If no candidate exists or materially distinct candidates remain tied, preview reports a failure and apply
does not mutate the document. Preview returns source object id, endpoints, length, and selection warnings so
the live result can be checked against the supplied reference images before apply.

### 4. Definition And Instance Idempotency

Create `WF_<source-stone-definition-name>` with one line and the selected left endpoint as its base point.
If the name exists, require an equivalent one-line local definition and reuse it; fail rather than silently
replace an incompatible definition.

Before insertion, compare existing output instances by output definition, full transform, and
`Assembly_ID`. Equivalent instances are skipped. This makes a second apply a no-op while preserving outputs
from other assemblies or transforms.

### 5. Preview And Apply Safety

`preview_stone_wireframe_bake` is read-only, closed-world, and performs all discovery and validation without
document mutation. `apply_stone_wireframe_bake` is non-destructive and closed-world because it only adds
definitions and instances. Each apply call owns one Rhino undo record and redraws only after successful
mutation.

Responses remain bounded: definition-level line selections and aggregate assembly/stone counts are returned,
while object ids and failures use explicit limits with truncation warnings.

### 6. Reusable Script

`Scripts/stone_wireframe_bake.py` launches the installed Router, resolves one exact live document path,
calls preview by default, and calls apply only with an explicit `--apply` flag. It supports optional exact
assembly-name filters and emits machine-readable JSON. The script never reads or writes a `.3dm` directly.

## Files Involved

Expected new production files:

- `src/MCP_Rhino.Server/Contracts/Requests/StoneWireframeBakeRequests.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/StoneWireframeBakeResponses.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveStoneWireframeBakeOperator.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoStoneWireframeBakeService.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/StoneWireframeBakeSkill.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveStoneWireframeBakeOperator.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/PreviewStoneWireframeBakeTool.cs`
- `src/MCP_Rhino.Server/Tools/Blocks/ApplyStoneWireframeBakeTool.cs`
- `Scripts/stone_wireframe_bake.py`

Expected registration and test changes:

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpStoneWireframeBakeSmokeCommand.cs`
- `Packaging/MCP_Rhino/package-manifest.json`
- `Project_Test/260901_TEST_stone-wireframe-bake/`
- MCP tool safety/inventory regression expectations where required
- `Project_Exet/260901_EXET_stone-wireframe-bake.md`

## Usage

Preview a live document:

```powershell
python Scripts/stone_wireframe_bake.py --document "V:\path\model.3dm"
```

Apply after reviewing preview:

```powershell
python Scripts/stone_wireframe_bake.py --document "V:\path\model.3dm" --apply
```

Limit the run to exact assembly definition names when needed:

```powershell
python Scripts/stone_wireframe_bake.py --document "V:\path\model.3dm" --assembly STN-MET-GA2010S-001 --apply
```

## Acceptance Criteria

- Preview is read-only and reports all qualifying live assemblies and individual stone definitions without
  altering the document.
- On the current target model, preview recognizes the previously inspected 22 assembly definitions and 36
  individual flat/vertical stone definitions, with no `_EL` or `_V` assemblies included.
- Every selected line matches the intended upper horizontal line in definition-local coordinates and has a
  deterministic lower-X base endpoint.
- Apply creates/reuses correctly named `WF_...` definitions with one line each and inserts every required
  occurrence with full nested transform preservation.
- Every inserted output instance has exactly the expected `Assembly_ID` value.
- Source assemblies and nested stone definitions/instances remain unchanged.
- A second preview/apply reports zero new instances and does not duplicate geometry.
- The reusable Python script completes preview and apply through the installed Router for the exact selected
  live document.
- A dedicated CLI slug and Rhino smoke command validate representative traversal, selection, transforms,
  idempotency, and user text.
- MCP tool safety/inventory smokes pass in Debug and Release.
- Debug and Release solution builds, package validation, and existing startup-registration regressions pass.
- The updated installed RHP loads at Rhino startup and the Router can execute the live workflow.

## Risks And Rollback

- Some stone definitions may contain multiple coincident top lines. The deterministic tie rules accept only
  geometrically equivalent duplicates; distinct ambiguous lines fail closed and are reported for review.
- Mirrored or nonuniform nested transforms can be lost by decomposed placement APIs. The implementation uses
  full Rhino transforms when inserting output instances.
- Large documents may approach the live dispatcher timeout. Discovery and response sizes are bounded, and
  exact assembly filters allow safe batching without changing geometry semantics.
- Existing `WF_` definitions might be incompatible. Apply refuses replacement and reports the conflict.
- Rollback uses the single Rhino undo record for each apply call. Source assemblies require no rollback
  because they are never mutated.

## Future Extensions

- Generalize the selection policy to named layers or user-selected definition-local curves only after a
  separate preview contract is defined.
- Add an optional report file if future production runs need a persistent audit artifact.
- Add bounded batch orchestration to the script if larger documents exceed one dispatcher call.

## Live Validation Revision - 2026-09-01

The first 1.3.0 live preview succeeded, but a subsequent normal Rhino launch exposed a startup race: the
dispatcher created a route for Rhino's temporary untitled startup document and did not receive a usable
document-open callback after the command-line model became active. The Rhino window showed the saved target,
while the route remained `ACTIVE_DOC_UNSAVED`.

Before apply, extend the dispatcher with a bounded Rhino-idle reconciliation of `RhinoDoc.ActiveDoc`. Idle
reconciliation runs only when the active document state differs from the tracked route or a saved document
still lacks a listener; it must not rewrite stable descriptors on every idle cycle. Add regression evidence,
advance the package patch version, reinstall, and prove two consecutive startup/Router connections resolve
the exact saved target before baking.
