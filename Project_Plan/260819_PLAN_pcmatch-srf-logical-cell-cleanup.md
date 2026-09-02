# PCMatchSrf Logical Cell Cleanup PLAN

## Background

Matching `PID_BKT_N1_08_09` to `PID_BKT_N1_07_09` copied the visible configuration, but stale blank cladding keys `0B` and `1C` remained on the target even though those physical cells were removed by source merge topology.

## Goal

Make the PCMatchSrf attribute transaction exactly represent the source's surviving logical cladding cells: obsolete hidden keys are removed, surviving logical cells are retained (including unassigned cells), and target-owned non-cell layout data is preserved.

## Architecture ownership

- Application planning: `PanelCladdingMatchPlanningService`
- Existing logical topology projection: `PanelCladdingLogicalCellService`
- Standalone regression: `Project_Test/260819_TEST_pcmatch-srf-logical-cell-cleanup/`
- Packaging/deployment: `Packaging/PanelCladdingEditor/`

## Capability

`PCMatchSrf` must write the source panel's surviving logical cladding cells, not every physical grid position. Physical cell keys hidden by source merge topology must be deleted from the target and must not be recreated as blank values.

## Scope

- Keep layout compatibility based only on the generated physical cladding cell-code lattice.
- Keep actual divider offsets, segment masks, and merge masks outside the match transfer scope.
- Resolve the source topology and collapse physical source values to logical representatives before building the target write set.
- Preserve material values, retained blank values, and parent-cell references on surviving logical cells.
- Delete stale target cladding keys such as `0B` and `1C` when those cells are hidden by the source topology.
- Preserve unrelated target attributes, divider offsets, and topology masks.

## Key design

- Parse and validate the source physical assignment graph as before.
- Collapse normalized physical source assignments through the decoded source topology.
- Encode only the returned logical representatives into the match write set.
- Continue deriving compatibility labels from all generated physical cell codes, so this fix does not add offset-value or topology-mask equality.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `Project_Test/260819_TEST_pcmatch-srf-logical-cell-cleanup/`
- Existing PCMatchSrf regression fixtures affected by the corrected contract
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`

## Usage

Run `PCMatchSrf`, select the source panel, and then select compatible target panels. The command transfers the source's logical cladding-cell values only.

## PLAN -> EXET -> TEST

### PLAN

1. Add a regression fixture with a compatible two-column, three-row grid where source merges hide `0B` and `1C`.
2. Confirm the current planner recreates those hidden keys after scheduling their deletion.
3. Collapse source cell assignments through `PanelCladdingLogicalCellService` before encoding writes.
4. Keep physical cell labels for compatibility checks so topology equality is not introduced.

### EXET

1. Update `PanelCladdingMatchPlanningService` to generate writes from logical representatives.
2. Bump the Panel Cladding Editor package to `1.0.48`.
3. Build and package the plug-in, then install or stage it according to Rhino process state.

### TEST

Acceptance criteria:

- A match whose source merge topology hides `0B` and `1C` deletes those target keys and does not rewrite them.
- Surviving material values, blank logical cells, and parent-cell references remain exact.
- Compatible targets with different offset values and topology masks are still accepted.
- Target offsets, segment/merge masks, type/signature values, and unrelated attributes remain unchanged.
- Existing PCMatchSrf, parent-cell, blank-cell, topology, command-name, build, and plug-in identity regressions pass.

## Risks

- Parent references may point to a physical cell that becomes hidden. The existing logical-cell collapse service must remap that reference to the surviving representative.
- Blank logical cells must still be stored using the retained-blank sentinel; only hidden physical cells should be absent.

## Rollback

Restore the match planner's physical-cell write loop and package version `1.0.47`, then reinstall the prior package artifact.

## Future extensions

Keep extrusion topology matching as a separate command/capability. PCMatchSrf intentionally does not transfer divider offsets or segment/merge masks.
