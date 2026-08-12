# Panel Cladding Offset Sync

## Background

`PanelCladdingSyncFromSurfaces` currently treats panel `H*` and `V*` offset attributes as the
authoritative atomic grid. It can reconstruct owner/reference cladding cells only when manually
edited surfaces still align with that grid. The production material-surface root also needs to move
from the legacy `02_Material Surfaces` / `03_Material Surfaces (STP)` names to
`03_Material Surfaces (STEP)`.

## Goal

Make cladding surfaces authoritative for both grid offsets and owner/reference cell values during
surface sync. Spawn new cladding surfaces below `03_Material Surfaces (STEP)`, and restrict every
CID/PID-based surface discovery path in sync to that root and its descendants.

## Architecture Ownership

- `Domain/`: carry inferred-grid state and offset writes through the sync snapshot/plan/commit
  contracts.
- `Application/Services/PanelCladding/`: canonical offset/cell key generation, changed-grid
  planning, workbook identity, and owner/reference normalization.
- `Infrastructure/Rhino/Live/PanelCladding/`: extract offset candidates from naked cladding-region
  boundaries in the panel-local frame, validate coverage against the inferred grid, and replace
  panel offset/cell attributes transactionally.
- `Project_Test/260812_TEST_panel-cladding-offset-sync/`: focused regression smoke and live fixture
  instructions.

## Key Design

1. Use the same deterministic panel-local frame as spawn. Inspect only naked edges of each detected
   cladding Brep; a boundary that is constant in local X contributes a V offset, and one constant in
   local Y contributes an H offset.
2. Cluster duplicate candidates within model tolerance, discard panel perimeter coordinates, sort
   interior offsets, and build a new contiguous grid. Shared edges removed by joining no longer
   contribute an offset; partial region boundaries still contribute a global candidate boundary.
3. Reclassify every surface against the inferred atomic grid and retain the existing fail-closed
   coverage checks for partial cells, overlaps, gaps, disconnected regions, and area mismatch.
4. Rebuild the panel layout preview and v3 type identity with inferred offsets before workbook
   upsert.
5. A panel commit removes all old H/V offset keys and cladding-cell keys, then writes canonical
   contiguous offset keys, normalized material/reference cell values, type code, and signature in
   the existing single Rhino Undo transaction.
6. Define `03_Material Surfaces (STEP)` as the sole supported material root. Spawn always targets
   it. Sync ignores CID/PID Breps outside that root, including both legacy roots.
7. Keep PID ownership mandatory. CID remains repairable metadata and is normalized from the new
   canonical owner cell after coverage reconstruction.

## Files Involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSpawnPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncPlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingSurfaceSyncService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingSurfaceSyncRepository.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingSyncFromSurfacesCommand.cs`
- affected existing standalone smoke tests and packaging metadata
- `Project_Test/260812_TEST_panel-cladding-offset-sync/`

## Usage

Place the cladding Brep objects under descendants such as:

```text
03_Material Surfaces (STEP)::Surfaces-Glass::GLS-001
```

Give each object the selected panel's `CW_1.01_PID`; CID may be stale or absent. Select the panel and
run `_PanelCladdingSyncFromSurfaces`. The command derives H/V offsets from the surface boundaries,
normalizes cell owner references and surface CIDs, and updates the workbook type.

## Acceptance Criteria

- Moving, adding, or removing an axis-aligned cladding boundary rewrites contiguous panel H/V
  offset attributes and removes obsolete keys.
- A joined surface that crosses an old boundary removes that boundary when no other region uses it.
- Partial boundaries create an atomic global offset while crossing surfaces receive owner-reference
  followers across that offset.
- The inferred layout drives cell labels, preview, workbook output, and v3 signature.
- Spawn plans use only `03_Material Surfaces (STEP)`.
- Sync discovers only Breps under `03_Material Surfaces (STEP)` descendants; matching CID/PID data
  on any other layer is ignored.
- Invalid, non-grid, overlapping, incomplete, or disconnected geometry leaves the affected panel
  unchanged and reports a diagnostic.
- Focused and existing regression smokes pass in Debug and Release; the standalone RHP builds in
  both configurations and retains the required assembly GUID.

## Risks And Rollback

- Manually modeled boundaries that are not constant in the panel-local X/Y frame cannot define H/V
  offsets. Coverage validation must reject those panels rather than approximate them.
- Edge clustering that is too loose could collapse intentional narrow cells; clustering is bounded
  by model tolerance and final offsets must satisfy the existing strict extent checks.
- Restricting discovery intentionally stops legacy-layer surfaces from participating until they are
  moved under the STEP root.
- Rollback is a revert of this capability's source/test/package changes. Runtime attribute and
  workbook writes retain the existing transactional rollback and one Rhino Undo record.

## Future Extensions

- Add a pre-sync visualization of inferred boundaries and ignored wrong-layer surfaces.
- Support explicitly modeled non-orthogonal region boundaries through a separate polygonal-cell
  schema rather than overloading H/V offsets.

## Revision Record (2026-08-12)

- Limit canonical H/V offsets to five decimal places using midpoint rounding away from zero.
- Apply the precision rule before monotonic/extents validation so boundaries that collapse after
  rounding fail closed.
- Persist and display offsets with at most five fractional digits (`0.#####`) in panel attributes,
  workbook metadata, and offset portions of type signatures.
- Advance the package version to `1.0.23` and extend the existing focused offset-sync smoke.
