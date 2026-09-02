# PCMatch Parent-Cell Fidelity Plan

## Background

`PCMatch` currently parses and validates source parent-cell assignments, but then writes the region
service's canonicalized assignment graph. Canonicalization preserves material coverage while it may
change which cell is the parent or flatten/reverse a valid reference chain. The existing regression
uses an already-canonical graph, so it cannot detect this distinction.

## Goals

- Make `PCMatch` transfer every source cell's actual material or parent-cell token.
- Continue rejecting blank required cells, missing parents, cycles, disconnected regions, and
  incompatible target grids before Rhino mutation.
- Preserve target-owned H/V offsets and existing segment/merge-mask behavior.
- Verify the exact values written by both planning and the live attribute application path.

## Architecture Ownership

- `PanelCladdingMatchPlanningService` owns validation and the exact source-to-target transfer plan.
- `LivePanelCladdingMatchService` remains the Rhino Undo-wrapped attribute commit adapter.
- Focused tests own canonical and non-canonical parent-graph regression coverage.

## Key Design

1. Parse source cells and run `PanelCladdingRegionService.Resolve` solely as validation.
2. Build cell writes from each parsed cell's original value after standard trim/uppercase
   normalization; do not use the region service's canonicalized cell map.
3. Keep topology masks and type/signature transfer unchanged.
4. Add a fixture where a higher-index cell owns a lower-index child. The target must retain that
   exact direction even though canonicalization would reverse it.
5. Exercise the same planned writes against Rhino `ObjectAttributes` through an internal test seam
   so dictionary-only simulation cannot mask a commit-path defect.

## Files Involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingMatchService.cs`
- `Project_Test/260819_TEST_pcmatch-parent-fidelity/`
- existing PCMatch regressions and package version/records

## Usage

Run `_PCMatch`, select the configured source panel, then select target panels. Parent references on
the target will match the source key/value set exactly.

## Acceptance Criteria

- A source `0A=MPL-001, 0B=MPL-001, 1A=0A, 1B=0B` produces those exact four target values.
- A valid non-canonical graph such as `0A=1A, 1A=MPL-001` retains that exact direction.
- Invalid cycles still fail before mutation.
- Target offsets remain untouched; topology masks remain copied.
- Debug/Release builds, focused tests, package identity checks, and installation staging pass.

## Risks and Rollback

- Equivalent panels may retain different but valid parent-owner choices. This is intentional because
  PCMatch promises exact configuration matching, not graph canonicalization.
- Rollback is the prior canonicalized write loop; live changes remain one Rhino Undo operation.

## Future Extensions

- Expose an explicit separate command for canonicalizing parent graphs if that workflow is later
  desired.
