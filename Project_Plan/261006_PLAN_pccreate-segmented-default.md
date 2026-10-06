# PCCreate segmented default PLAN

## Background

The user reports that PCCreate initializes curves merged instead of segmented and
explicitly requests investigation and a fix. The create planner currently turns
continuous guide coverage across grid junctions into MergeRuns. This behavior was
encoded in the earlier curve-topology regression expectations. The requested
default supersedes that continuity-to-merge behavior for PCCreate.

## Goals

- Initialize every present intermediate extrusion atom as an independent segment.
- Infer missing segments and logical cells from guide coverage as before.
- Remove stale merge state on rerun without writing a new merge mask.
- Preserve the preceding unit-dimension fix and explicit merge workflows.

## Architecture ownership

PanelCladdingCreatePlanningService owns guide-to-grid initialization. Keep the
change there; do not change shared topology decoding, extrusion spawning, editor
merging, curve templates, matching, or synchronization behavior.

## Key design

Remove merge-run inference from BuildAxis/BuildTrackTopology. Guides determine
covered atoms only. A newly constructed topology defaults to empty MergeRuns and
HiddenSegments while retaining inferred MissingSegments. Sparse persistence
therefore omits CW_2.10_MERGE_MASK, and the existing delete plan clears prior masks.
The shared extrusion planner then emits one intermediate curve per present atom.
The four perimeter frame curves retain their established behavior.

## Files

- src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs
- Project_Test/260818_TEST_panel-cladding-create-command/Program.cs
- Project_Test/260819_TEST_panel-cladding-curve-topology/Program.cs and README.md
- Packaging/PanelCladdingEditor/README.md and package-manifest.json
- Matching 261006 PLAN, EXET, and TEST artifacts for this fix.

## Usage

PCCreate's selection flow remains unchanged. Full-length, split, and overlapping
guides initialize segmented intermediate tracks. Users may merge afterward in
PCEditor or apply PCCrvTemplate deliberately.

## Acceptance criteria

- Continuous H/V guides yield no merge mask and decode with zero merge runs.
- A full 2H/2V grid yields twelve individual intermediate curves in the downstream
  extrusion plan, with no merged curve; perimeter frames remain unchanged.
- Split/continuous guide input has the same segmented default.
- Partial coverage still yields the same missing atoms and logical cells, with no
  automatic merging. Existing stale masks are cleared and unrelated text retained.
- Create and curve-topology tests pass in Debug and Release, including dimensions.
- Sparse-topology, explicit template/merge, and extrusion regression checks pass.
- Standalone Debug and Release builds pass; the MCP host/Router is unchanged, so
  solution-level builds are unnecessary.
- Verify compiled assembly identity, build/stage version 1.0.81 containing both
  fixes, and verify all bundle hashes. No production activation is planned.

## Risks and rollback

Automatic initialization intentionally no longer preserves a guide's continuity
as merge intent. Explicit editor/template/sync merging remains supported. Revert
only this follow-up's changes to restore the prior default while keeping the
dimension fix. No live document migration or native Undo claim is part of staging.

## Future extensions

An optional preserve-guide-merges mode would require a separate user request.
