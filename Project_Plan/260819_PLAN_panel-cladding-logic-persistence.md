# Panel Cladding Logic Persistence PLAN

## Background

Panel cladding assignments currently store material codes on region-owner cells and parent-cell
labels on the remaining cells. `PCSyncSrf` can reconstruct edited surface coverage and material
codes from Rhino geometry and layers, but it has no material-independent record of the owner/parent
graph that the user previously established. It therefore always chooses the top-left covered cell
as the owner, even when the saved logical owner was a different cell.

## Goal

- Persist a canonical `CW_2.08_CLADDING_LOGIC` panel attribute whenever cladding assignments are
  created, saved, matched, or synchronized.
- Store only cell-owner logic in that attribute; never store material codes.
- Let `PCSyncSrf` combine the saved logic with current Rhino surface coverage and material layers to
  preserve a valid prior owner when possible.
- Keep geometry authoritative for edited splits and merges, with deterministic fallback behavior.
- Preserve compatibility with panels that predate the new attribute.

## Architecture ownership

- The canonical attribute name remains in `PanelCladdingKeyService` with the other panel keys.
- Material-free logic encoding, decoding, validation, and owner resolution belong to a focused
  Application service under `Application/Services/PanelCladding/`.
- Save/create/match/surface-sync planning owns construction of the derived logic payload.
- The live Rhino surface-sync repository only reads existing user text and commits the prepared
  canonical payload in the existing single Undo transaction.
- Focused regression coverage belongs in
  `Project_Test/260819_TEST_panel-cladding-logic-persistence/`.

## Key design

1. Serialize the logic as deterministic JSON keyed by short cell labels, for example
   `{"0A":"","1A":"0A"}`. A material or blank owner becomes an empty value; a parent-cell token
   remains its normalized short label. Material strings never enter the payload.
2. Validate duplicate labels, invalid parent labels, unknown parent targets, and cycles before a
   payload is used or persisted.
3. During `PCSyncSrf`, resolve the prior root owner for every cell covered by one edited surface.
   Preserve that owner only when all covered cells resolve to the same saved root and that root is
   still inside the edited surface coverage.
4. When a surface merge combines multiple prior owners, or a split leaves the prior owner outside
   one new region, use the existing geometry-derived top-left owner. This makes edited geometry
   authoritative while retaining unchanged non-default owner choices.
5. A missing or blank logic attribute supplies no hint and retains current behavior. A malformed
   nonblank payload rejects that panel before mutation rather than silently applying uncertain
   assignments.
6. `PCSyncSrf` writes the newly inferred material-free graph together with the exact cell graph and
   verifies the committed attribute. This also backfills legacy panels on first successful sync.
7. `PCEditor` cladding saves, `PCCreate`, and `PCMatchSrf` maintain the same derived attribute;
   `PCClear` removes it with the cladding assignment set.

## Involved files

- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingLogicService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingSaveService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `Project_Test/260819_TEST_panel-cladding-logic-persistence/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

Save cladding in `PCEditor`, create or match panel cladding, or run `PCSyncSrf`. Inspecting panel
attributes will show `CW_2.08_CLADDING_LOGIC` as a material-free cell map. After editing spawned
Rhino cladding surfaces, run `PCSyncSrf`; current surface geometry and layer material remain the
source of physical coverage and material, while the saved map preserves a still-valid owner choice.

## Acceptance criteria

- Saving `0A=MPL-001, 1A=0A` writes `CW_2.08_CLADDING_LOGIC={"0A":"","1A":"0A"}` and the payload
  contains no `MPL-001` material text.
- An unchanged surface covering `0A,1A` preserves saved owner `1A` when the stored graph points both
  cells to `1A`, even though the geometry fallback would choose `0A`.
- A merged surface covering cells from multiple saved owners uses the deterministic geometry owner.
- A split region whose saved owner is outside the new coverage uses the deterministic geometry
  owner; the region retaining the owner preserves it.
- Missing logic remains compatible and uses current geometry-only inference.
- Malformed, cyclic, or unknown-reference logic rejects the affected panel before Rhino mutation.
- `PCSyncSrf` backfills and verifies the current logic attribute, including cases where cell values
  themselves did not otherwise change.
- `PCCreate` and `PCMatchSrf` write the derived logic, while `PCClear` deletes it.
- Focused Debug/Release tests, affected regressions, Debug/Release solution builds, package creation,
  and plug-in identity validation pass.

## Risks and rollback

- Grid-index labels can shift when new inferred offsets are inserted. Owner preservation is therefore
  accepted only when the complete edited coverage resolves to one saved root that is still covered;
  all uncertain cases fall back to geometry.
- A manually corrupted nonblank payload will skip the panel. The diagnostic identifies the logic
  attribute, and deleting that attribute restores legacy geometry-only behavior.
- Roll back by removing the derived attribute writes and saved-owner resolution, then reinstalling
  the prior package. Existing cell/material attributes remain unchanged.

## Future extensions

- Add geometric remapping metadata if owner preservation must survive arbitrary inserted or removed
  grid indices rather than the current conservative label-based rule.
- Surface the saved owner graph in the editor for direct inspection without exposing material data.
