# Panel Cladding Curve Topology PLAN

## Background

`PCCreate` currently accepts an H/V guide when its projected bounding span merely touches or overlaps a selected panel. A curve whose endpoint only touches a panel edge can therefore create an offset, and every accepted offset becomes a complete divider even when only part of the divider is geometrically present. The command also does not persist the inferred segment/merge masks.

The requested companion command, `PCMatchCrv`, does not yet exist. It must transfer curve topology masks between panels whose general H/V grid dimensions match while allowing different numeric offset values.

## Goal

- Make `PCCreate` derive panel offsets and topology from meaningful curve coverage on each selected surface.
- Retain only curve segments that participate in closed panel regions by completely covering an atomic interval between panel boundaries or perpendicular divider tracks.
- Persist canonical H/V offsets, segment mask, merge mask, and surviving logical blank cells.
- Add `PCMatchCrv` as an independent Rhino command that transfers only segment and merge masks across compatible grids.

## Architecture ownership

- Domain snapshots/plans: `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- Application planning: `PanelCladdingCreatePlanningService` and a new curve-topology match planner
- Rhino geometry association: `LivePanelCladdingGeometryPartitionService`
- Rhino mutation adapter: reuse/refine the live match transaction boundary
- Rhino commands: `UI/PanelCladdingCreateCommand.cs` and a new `UI/PanelCladdingMatchCurveCommand.cs`
- Packaging: `Packaging/PanelCladdingEditor/`

## Key design

### PCCreate

1. In the Rhino adapter, test for a meaningful guide interval on the actual Brep rather than accepting a single touching point.
2. Convert valid guides into local, axis-aligned spans clipped to the panel extents.
3. Cluster their primary coordinates into candidate H/V offsets.
4. Subdivide each candidate track into atomic bays using the perpendicular offsets and mark an atom present only when selected on-panel curve coverage spans that entire bay.
5. Remove candidate tracks with zero present atoms and recompute until stable; this prevents dangling geometry from creating secondary false tracks.
6. Encode every absent atom in the segment mask.
7. Encode a merge run only when one connected selected guide span covers adjacent present atoms on the same track.
8. Collapse blank physical cells through the inferred topology and persist only surviving logical cells.

### PCMatchCrv

1. Parse and validate the source masks against its H/V track counts.
2. Require each target to have the same horizontal and vertical track counts; do not compare numeric offset distances.
3. Overwrite only `CW_2.05_SEGMENT_MASK` and `CW_2.06_MERGE_MASK`.
4. Preserve cell assignments, offsets, unit dimensions, type/signature data, identity, and unrelated user text.
5. Reject the complete batch before mutation when any target grid count is incompatible or a source/target topology payload is invalid.

## Files involved

- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCurveMatchPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingMatchService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingMatchCurveCommand.cs`
- Existing create/match/command regressions
- `Project_Test/260819_TEST_panel-cladding-curve-topology/`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

- `PCCreate`: select panel surfaces first, then select candidate H/V guide curves. Only curve portions that form complete atomic boundaries on each panel contribute to its attributes.
- `PCMatchCrv`: select target panels, then one source panel. Compatible targets receive the source segment and merge masks without receiving its offset distances or cladding values.

## Acceptance criteria

- A curve whose endpoint only touches a panel creates no offset or topology segment for that panel.
- The screenshot arrangement produces one full vertical track, two right-hand horizontal atoms, and one left-hand horizontal atom; dangling atom portions are absent.
- Partial valid dividers generate offsets plus the correct missing-segment mask.
- A continuous selected curve spanning adjacent atoms generates the corresponding merge run; separate collinear curves do not invent a merge.
- Tracks with no complete atoms are removed, and dependent segment validity is recomputed.
- PCCreate persists canonical masks and only surviving logical blank cell keys while preserving unrelated panel metadata.
- PCMatchCrv accepts equal H/V counts with different offset values, transfers exactly two mask keys, and preserves all other target attributes.
- PCMatchCrv rejects different H/V counts and invalid payloads before mutation.
- Public command names and GUIDs remain explicit and unique.
- Focused Debug/Release tests, existing regressions, solution builds, package creation, RHP identity, and install/validate all pass.

## Risks and rollback

- Coarse geometric sampling could misclassify a very short on-surface interval. The Rhino adapter will combine local span clipping with multiple actual Brep-distance probes and a model-tolerance-derived minimum length.
- Coincident/overlapping guide fragments can make merge ownership ambiguous. Segment presence uses union coverage, while merge adjacency requires a single guide span to cover both atoms.
- Topology changes can hide physical cells. Logical blank persistence prevents obsolete physical keys from being recreated.
- Roll back by restoring the prior create planner/snapshot builder, removing `PCMatchCrv`, reverting the package version, and reinstalling the prior package artifact.

## Future extensions

- Add explicit diagnostics/preview coloring for rejected, dangling, atomic, and merged guide portions.
- Add non-orthogonal or curved-divider topology only under a separately designed layout schema; the current H/V mask contract remains axis-aligned.
