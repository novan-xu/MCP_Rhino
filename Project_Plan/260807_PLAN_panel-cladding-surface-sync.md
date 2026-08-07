# Panel Cladding Surface Sync Plan

## Background

Spawned cladding surfaces are sometimes moved manually between material sublayers to capture façade
nuances. Their `Cladding` user text and the owning panel's cell/type/signature metadata then become
stale, and manually re-entering every panel cell defeats the purpose of the modeled surfaces.

## Goal

Add `_PanelCladdingSyncFromSurfaces` to the standalone PanelCladdingEditor plug-in. For one or more
selected panels, the command discovers their spawned cladding surfaces through canonical PID/CID
metadata, treats each surface's current material layer as authoritative, refreshes surface and panel
metadata, regenerates changed cladding types/signatures, and exports the updated types into a
user-selected `.xlsx` typology workbook.

## Architecture ownership

- `UI/`: preselection/multiple-selection and the workbook file picker.
- `Application/Services/PanelCladding/`: deterministic PID/CID/layer mapping, change detection,
  signature generation orchestration, and batch workflow.
- `Infrastructure/Rhino/Live/PanelCladding/`: live object/layer discovery, preflight, one Undo record,
  attribute mutation, document workbook-path update, and rollback.
- `Infrastructure/File/`: one prepared Open XML batch update that adds/reuses all required types in
  one temporary workbook before any final file replacement.
- `Domain/` and `Application/Interfaces/`: transport-neutral sync and batch-workbook contracts.

## Key design

- Accept one or multiple preselected panel Breps; if none are preselected, prompt for them.
- Read panel ownership only from `CW_1.01_PID` and surface position only from `CW_1.02_CID`.
- An expected surface CID is exactly `<panel PID>-<cell label>`, such as `W3_06_42-0A`.
- Require unique nonblank selected panel PIDs and exactly one Brep surface for every expected panel
  cell. Require the surface's canonical PID to match the panel PID. Fail before mutation on missing,
  duplicate, or unexpected owned CIDs.
- Require each matched surface to be below
  `02_Material Surfaces::<material family>::<material code>`. The final layer segment is normalized
  to uppercase and becomes the authoritative cladding value. The command does not move surfaces or
  alter layer colors.
- Refresh each surface's canonical `Cladding` value when it differs from its layer-derived material.
- Update the corresponding panel cell keys. A panel whose normalized cell values changed receives a
  regenerated `CW_1.10_CLADDING_TYPE` and `Signature`; legacy type/signature keys are removed.
- Ask the user to choose or create an `.xlsx` workbook. Prepare every changed type against one
  temporary workbook so equal signatures reuse a type and type-code collisions resolve consistently.
- Render/export previews with the existing typology worksheet format. Only changed panel types are
  exported; a surface-key-only refresh does not invent a new type.
- Preflight document, geometry, mappings, workbook access, identities, previews, and final writes
  before live mutation. Apply all Rhino attribute changes and the document workbook path inside one
  Undo record named `Sync Panel Cladding From Surfaces`; rollback earlier attributes/document path if
  a later Rhino or workbook commit fails.

## Files involved

- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/File/OpenXmlPanelCladdingWorkbookRepository.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingSyncFromSurfacesCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/`
- `Project_Exet/260807_EXET_panel-cladding-surface-sync.md`

## Usage

Select one or more configured panel Breps, run `_PanelCladdingSyncFromSurfaces`, then choose or create
the typology `.xlsx`. The command reports matched surfaces, refreshed surface keys, changed panels,
and exported/reused types. One Rhino Undo restores the Rhino metadata batch; workbook output is an
external commit and is not controlled by Rhino Undo.

## Acceptance criteria

- The command is discoverable and has a unique explicit GUID.
- Multiple panels and preselection work.
- PID/CID matching uses only `CW_1.01_PID` / `CW_1.02_CID` and exact expected cell labels.
- Missing, duplicate, unexpected, wrong-PID, invalid-layer, unsupported-panel, locked-workbook, and
  blank-selection cases fail before live mutation.
- Surface `Cladding` values follow the current layer leaf; layers and geometry are unchanged.
- Panel cells follow matched surface layers, and only changed configurations regenerate canonical
  type/signature metadata.
- Equal new signatures share a workbook type; all changed types are prepared/committed as one
  workbook batch.
- One Rhino Undo record contains all Rhino metadata edits, with explicit in-process rollback on
  partial failure.
- Dedicated tests cover mapping/change detection/batch workbook behavior, and existing editor,
  spawn, match, and clear regressions pass in Debug and Release.
- Full solution builds pass in Debug and Release.
- The packaged RHP assembly GUID is verified directly before staging or installation.

## Risks and rollback

- Incorrect PID/CID inference could modify unrelated objects. Canonical keys, exact CIDs, selected
  unique PIDs, expected-cell membership, and one-surface-per-cell preflight bound ownership.
- Layer hierarchy mistakes could become material codes. The required root/family/material depth and
  valid material leaf fail closed.
- Sequential single-panel workbook writes could overwrite one another. One prepared batch update
  owns all new/reused types and one final file replacement.
- External workbook replacement cannot be undone by Rhino. Workbook preparation occurs before Rhino
  mutation, final replacement occurs once, and errors roll Rhino metadata back where possible.
- Code rollback is isolated to the new sync command/workflow/contracts/adapters, batch workbook API,
  smoke coverage, package version, and matching PLAN/TEST/EXET artifacts.

## Future extensions

- Optional reconciliation reports without mutation.
- Optional repair of material-family parent layers when the leaf material is valid but misplaced.
- Explicit handling for multiple physical surface pieces representing one logical CID if that
  modeling convention is adopted later.

## Revision record (2026-08-07)

Live use established that production panel PID values can carry a semantic `PID_` prefix while
surface CIDs carry the corresponding `CID_` prefix. The shared CID derivation rule is therefore:

- `PID_BKT_W3_05_25` + `0A` becomes `CID_BKT_W3_05_25-0A`;
- an identifier without leading `PID_`, such as `W3_06_42`, retains the prior
  `W3_06_42-0A` behavior.

Both spawn and sync must use the same helper. The workbook location prompt is also revised from an
Eto file dialog to Rhino command-line literal-string input so paths containing spaces can be pasted.
The document's stored workbook path is offered as the Enter-key default when present.
