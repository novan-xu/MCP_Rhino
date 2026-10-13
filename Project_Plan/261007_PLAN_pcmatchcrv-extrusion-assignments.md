# PCMatchCrv extrusion assignment transfer

## Background

The user reports PCMatchCrv does not match assigned extrusions and explicitly
requests extending it. The current planner writes only the source's sparse
delete/merge/hide masks, leaving target frame assignments unchanged.

## Goal

Match complete extrusion assignments alongside the existing topology transfer,
while preserving target dimensions, offsets, cladding and unrelated metadata.

## Architecture ownership

The standalone Application curve-match planner owns transfer and derived frame
typology. Existing assignment and typology services own validation/serialization.
Domain match snapshots carry the target system code, read from the layer through
the existing live Rhino repository. The existing live match transaction continues
to own preflight, attribute commit verification and rollback.

## Key design

- Keep the existing equal horizontal/vertical track-count requirement and map
  assignments by the same frame/track/bay indices as source topology.
- Transfer normalized `CW_2.09_FRAME_ASSIGNMENTS` including additive profile codes,
  1D/0D definitions, quantities/formulas, parent references and curve modifiers.
- Replace target assignments completely. Empty source assignments remove both
  target assignment payload and stale frame typology.
- Recompute `CW_1.5D_FRAME TYPOLOGY` from target geometry/offsets/system and source
  topology/assignments; never copy source or retain target's old typology text.
- Preserve target offsets, cladding material/owner data, unit dimensions, IDs and
  unrelated metadata. No workbook edits or direct baked-curve changes are added.
- Retain existing source/target parse guards and all-target planning before writes.
- Update command prompts/status to describe extrusion-assignment matching.

## Files

- Application `PanelCladdingCurveMatchPlanningService.cs`.
- Domain `PanelCladdingModels.cs` and live `LivePanelCladdingRepository.cs`.
- UI `PanelCladdingMatchCurveCommand.cs`.
- Existing curve-topology smoke/README and matching PLAN/EXET/TEST artifacts.
- Package README usage text; deployment remains separate from implementation.

## Usage

Run PCMatchCrv, select target panel Breps, then the source panel. Existing grid
counts must match. Saved extrusion assignments now follow the source alongside
the masks. PCEditor displays them, and normal PCSpawnCrv/PCUpdate workflows use
the transferred definitions when generating/rebuilding curves.

## Acceptance criteria

- Assignments copy on perimeter, horizontal and vertical curves, including merged
  and hidden segments, additive codes, 1D quantities, 0D fixed/spacing definitions,
  parent references and modifiers; old target assignments are replaced.
- Resulting user text parses with the copied topology and generates assigned
  extrusion curves using target dimensions and copied formulas.
- Different target geometries/system codes produce target-derived frame typology.
- Empty source assignments clear target payload/typology without blank JSON.
- Malformed source/target payloads, inconsistent merged assignments and mismatched
  grids fail preflight; source is unchanged and repeated match is idempotent.
- Existing topology/hide/match/assignment regressions and standalone Debug/Release
  builds pass; no Windows UI automation or production document mutation.

## Risks and rollback

Match replaces target extrusion assignments as requested. The existing Rhino
transaction/Undo behavior remains in place. Revert the planner and snapshot
addition to restore mask-only transfer; no schema migration is required. Existing
worktree changes are preserved. Live command/Undo acceptance needs a Rhino session.

## Future extensions

Different-grid assignment remapping or automatic baked-curve reconciliation would
require a separate scoped change.
