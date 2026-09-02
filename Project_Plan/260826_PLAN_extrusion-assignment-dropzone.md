# PLAN — Extrusion assignment drop zone

Date: 2026-08-26

## Background

The extrusion catalogue currently advertises drag behavior, but the practical assignment target is
unclear and canvas dropping is not a good fit for the existing editor interaction. Assigned codes
are rendered as small text chips, while the catalogue uses image-based profile tiles.

## Goals

- Make the extrusion assignment section the only drop target for catalogue profiles.
- Remove canvas-drop and click-to-assign behavior from the main extrusion catalogue.
- Render assigned profiles with the same recognizable preview language as catalogue profiles.
- Place a compact curve-length modifier input beside the assigned profile code.
- Dynamically exclude already-assigned profiles from the available catalogue for the current curve
  selection.
- Preserve additive assignments, parent dependencies, manual dependency removal, undo, and the
  existing per-curve modifier storage contract.

## Architecture ownership

- `UI/PanelCladdingEditorWindow.xaml`: assignment drop target and assigned-profile presentation.
- `UI/PanelCladdingEditorWindow.xaml.cs`: filtered available view, drag/drop routing, assignment
  projection, modifier editing, removal, selection refresh, and undo coordination.
- `UI/PanelCladdingGridCanvas.cs`: remove catalogue-profile drop acceptance from the canvas.
- `Domain/` and `Application/`: unchanged; the modifier remains curve-scoped and assignment payload
  schema v2 remains authoritative.

## Key design

1. Catalogue tiles remain drag sources but are no longer buttons and do not assign on click.
2. The assignment panel accepts only the existing frame-extrusion drag format and only when at
   least one frame/intermediate curve is selected.
3. The assignment panel shows the union of profiles assigned to the selected curves as preview
   cards. Each card includes removal and a compact modifier input.
4. Modifier inputs reflect the selected curves' shared modifier. Editing any card applies the same
   curve modifier to all selected assignment targets, consistent with the established formula rule.
5. Available catalogue content is a filtered projection of configured project profiles. Any code
   already present in the assignment panel is omitted until removed.
6. Parent dependencies continue to be expanded only when the root code is newly assigned.

## Involved files

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `Project_Test/260826_TEST_extrusion-assignment-dropzone/`

## Usage

Select one or more frame/intermediate curves, drag an available profile from the catalogue, and
drop it inside the extrusion assignment section. Remove assigned cards to make them available again.
Enter a signed length modifier beside any assigned profile to update the selected curves.

## Acceptance criteria

- Dropping on the assignment panel adds the profile to the selected curves.
- Dropping a catalogue profile on the canvas has no effect.
- Clicking a catalogue tile has no assignment effect.
- Assigned profiles render with thumbnail, code, remove action, and compact modifier input.
- Assigned codes disappear from the available catalogue and reappear after removal.
- Modifiers, dependencies, undo, typology, save/reload, bake, and sync behavior remain valid.
- Focused UI/source tests, WPF render checks, Debug/Release builds, package GUID validation, and
  registry-only installer regression pass.

## Risks and rollback

- WPF drag events can be intercepted by nested controls; handle drag-over/drop on the assignment
  container and mark accepted events handled.
- Multiple selected curves can have mixed modifiers; show an empty input with explanatory tooltip
  until the user applies one replacement value.
- Rollback is restoring the prior catalogue and assignment templates while retaining the unchanged
  assignment payload model.

## Future extensions

- Reordering assigned profiles for presentation only.
- A single shared modifier editor above assigned cards if repeated inline inputs prove confusing.
- Category filters or search for very large ready catalogues.
