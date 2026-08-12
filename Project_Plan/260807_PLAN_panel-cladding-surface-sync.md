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

- `UI/`: preselection/multiple-selection, command-line workbook-path input, issue reporting, and
  skipped-panel selection feedback.
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
  cell. Require the surface's canonical PID to match the panel PID. Isolate missing, duplicate, or
  unexpected owned CIDs to that panel and continue other fully valid panels.
- Require each matched surface to be below
  `02_Material Surfaces::<material family>::<material code>`. The final layer segment is normalized
  to uppercase and becomes the authoritative cladding value. The command does not move surfaces or
  alter layer colors.
- Refresh each surface's canonical `Cladding` value when it differs from its layer-derived material.
- Update the corresponding panel cell keys. A panel whose normalized cell values changed receives a
  regenerated `CW_1.10_CLADDING_TYPE` and `Signature`; legacy type/signature keys are removed.
- Ask the user to type or paste an `.xlsx` workbook path. Prepare every changed type against one
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

Select one or more configured panel Breps, run `_PanelCladdingSyncFromSurfaces`, then type or paste
the typology `.xlsx` path. The command reports matched surfaces, refreshed surface keys, changed and
skipped panels, and exported/reused types. Skipped panels are selected for inspection. One Rhino
Undo restores the Rhino metadata batch; workbook output is an external commit and is not controlled
by Rhino Undo.

## Acceptance criteria

- The command is discoverable and has a unique explicit GUID.
- Multiple panels and preselection work.
- PID/CID matching uses only `CW_1.01_PID` / `CW_1.02_CID` and exact expected cell labels.
- Missing, duplicate, unexpected, wrong-PID, invalid-layer, and unsupported-panel cases skip only
  the affected panels; valid panels continue. Locked-workbook and blank-selection cases fail before
  live mutation.
- Hidden Brep surfaces and surfaces on hidden layers participate in canonical PID/CID matching.
- Skipped panels receive no partial writes and are selected after successful completion.
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

## Revision record (2026-08-07): hidden surfaces and partial-batch recovery

Live use requires sync to discover valid cladding surfaces even when the objects themselves or their
layers are hidden. Mapping defects must also be isolated to their owning panel instead of aborting a
mixed selection.

- Enumerate candidate Brep surfaces with explicit Rhino object-enumerator settings that include
  normal, locked, and hidden objects, including objects on hidden layers; continue excluding
  reference objects.
- Treat panel layout/read failures and PID/CID mapping failures as per-panel issues. Build a complete
  temporary mapping for one panel and append its writes only when every expected cell has exactly
  one valid owned surface on a valid material layer.
- A missing CID, duplicate CID, unexpected owned CID, wrong PID, invalid material layer, duplicate
  selected PID, unsupported panel, or unreadable selected panel skips that panel only. Other valid
  panels continue through surface refresh, panel update, and workbook export.
- Do not perform partial surface or panel writes for a skipped panel.
- Return structured issue details and skipped panel object ids through planning, commit, and command
  results. After a successful command, clear the current selection and select the skipped panels for
  inspection. Leave selection unchanged when no panels were skipped.
- If every selected panel is skipped, perform no workbook or attribute mutation, still complete the
  command successfully, report all issues, and select all skipped panels.
- Add tests for hidden-object enumeration, valid-plus-invalid mixed batches, missing and duplicate
  CID isolation, zero-valid-panel completion, and skipped-panel result propagation.

## Revision record (2026-08-07): model-authoritative workbook pruning

Every `_PanelCladdingSyncFromSurfaces` run must reconcile the typology workbook with cladding types
still assigned anywhere in the active Rhino model.

- During live read, enumerate all normal, locked, and hidden Brep objects, including hidden-layer
  objects, and collect canonical `CW_1.10_CLADDING_TYPE` plus `Signature` assignments model-wide.
- Before workbook preparation, exclude the prior assignments of selected panels whose cladding type
  will change. The final retained set is all other model assignments plus the prepared identities of
  successfully changed panels.
- In the temporary Open XML workbook, remove every `_CLADDING_INDEX` record whose type code and
  stored signature are no longer retained by the Rhino model, and delete its indexed type worksheet.
- Delete only worksheets owned by `_CLADDING_INDEX`; preserve unrelated/project worksheets and the
  hidden index sheet itself.
- Run pruning even when no panel material changes and even when all selected panels are skipped, so
  invoking the command still reconciles stale workbook types.
- Keep the existing temporary-file, exclusive-access, validation, atomic replacement, and Rhino
  rollback sequence. A locked/changed/invalid workbook prevents Rhino metadata commit.
- Return and report the removed workbook type codes/count in the command result.
- Add workbook tests proving one unused managed type is deleted, one model-used type is retained,
  unrelated sheets survive, index rows match remaining managed sheets, and a no-panel-change sync
  still commits pruning.
