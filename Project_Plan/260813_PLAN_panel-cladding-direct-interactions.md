# Panel Cladding Direct Interactions

## Background

The latest WPF PanelCladdingEditor has the correct project material-catalog architecture and visual
hierarchy, but several interactions still behave like a form rather than a direct editor. Blank
canvas space is not guaranteed to receive pointer events, material and parent-cell assignments still
have confirmation buttons, divider offsets are read-only in Current Panel, zoom is limited to three
fixed values without pan or model-space grid scaling, and dimension labels combine value and lock in
one container. Normal cladding and extrusion strokes also use different visual weights.

## Goal

Implement the nine requested direct-interaction refinements without changing the WPF architecture or
committing preview edits to Rhino before Save. Make selection clearing, assignment, offset editing,
dimension editing, zoom, pan, grid scaling, toolbar alignment, and line weights consistent and
predictable in both editor views.

## Architecture Ownership

- `UI/PanelCladdingGridCanvas.cs`: hit-test background, blank deselection, bounded pan/zoom, 10-inch
  model grid, dimension/lock rendering, and unified line weights.
- `UI/PanelCladdingEditorWindow.xaml`: toolbar arrangement, direct assignment controls, editable
  divider-offset template, and equal footer buttons.
- `UI/PanelCladdingEditorWindow.xaml.cs`: guarded selection-driven assignments, parent option list,
  divider-offset commit, zoom synchronization, and preview-only state management.
- `Project_Test/260813_TEST_panel-cladding-direct-interactions/`: focused interaction and rendered UI
  verification.

## Key Design

1. Give the custom canvas an explicit transparent background so blank regions participate in WPF hit
   testing. A plain click whose hit target set is empty clears the active view's full selection even
   if a modifier remains pressed.
2. Current Panel offset rows expose lightweight inline `TextBox` values. Enter or focus loss commits
   one absolute H/V divider offset after numeric/range validation. This mutates editor-session lists,
   rebuilds the preview, enters the undo stack, and does not touch Rhino.
3. Material selection changes immediately call the existing in-memory assignment path. A guard
   suppresses changes while controls are synchronized from selection or panel load.
4. Parent cell becomes a ComboBox containing every panel cell except all currently selected cells.
   Selecting one immediately updates the in-memory assignments. No confirmation buttons remain.
5. Footer buttons use two equal star columns. The action becomes `Save`; Exit grows to the same width
   while the previous Save allocation shrinks.
6. Dimension labels have no container. Values remain centered at their current label positions in a
   larger mono font; each lock icon is centered directly over its dimension line with a small
   background knockout and its own hit target.
7. The view segmented control and zoom selector share the toolbar's left group. Clear Assignment uses
   the same rounded boxed style as other compact buttons.
8. Zoom is clamped from 80% to 150%. Wheel input changes it by exactly 5%. Shift + right-button drag
   pans. Pan is clamped so the current scaled panel can never move beyond the footprint established by
   the centered 80% panel; at 80%, pan is always zero.
9. The background grid is rendered by the canvas at physical 10-inch intervals derived from model
   unit scale and panel pixels-per-model-unit. Its spacing and origin follow zoom and pan.
10. The normal panel frame becomes 2 DIPs and dividers 1.5 DIPs in both cladding and extrusion views,
    half of their prior 4/3-DIP treatment. Parent dashes and clearing strokes scale accordingly.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260813_TEST_panel-cladding-direct-interactions/`

## Usage

- Click blank canvas space to clear the selection for the active view.
- Click an H/V offset value in Current Panel, type a new absolute offset, and press Enter or click away.
- Select cells and choose a material or another cell from the active assignment dropdown; the canvas
  updates immediately. Use Save to commit the accumulated panel attributes to Rhino.
- Hold Shift and drag with the right mouse button to pan. Use the wheel for 5% zoom steps or choose a
  scale next to the view selector.

## Acceptance Criteria

- Blank clicks clear cladding and extrusion selections and the canvas receives pointer input anywhere.
- Sidebar offsets commit valid absolute values without a lock control or popup.
- Material and parent dropdown changes update only editor memory/preview until Save.
- Parent options exclude every selected cell and confirmation buttons are absent.
- Footer actions are equal width and labeled Exit / Save.
- Dimension label boxes are gone, values use a larger centered font, and locks sit on the line center.
- Clear Assignment is boxed; view and scale controls are adjacent.
- Zoom clamps to 0.80–1.50, wheel increments are 0.05, pan requires Shift+right drag, and pan bounds
  collapse to zero at 80%.
- Background grid represents 10 physical inches and scales/moves with the panel.
- Normal cladding and extrusion frame/divider line weights match at 2/1.5 DIPs.
- Focused interaction smoke, affected WPF regressions, Debug/Release builds, renders, package identity,
  package validation, and installed validation pass.

## Risks And Rollback

- SelectionChanged recursion can create unintended edits. Use an explicit synchronization guard and
  assert the Rhino repository is untouched until Save.
- Inline offset commit can create crossed dividers. Clamp each value between its immediate neighbors
  with a dimension minimum and restore the displayed value on failure.
- Pan math can expose content outside the intended envelope. Derive bounds from the difference between
  current scaled panel size and the 80% baseline and test both clamp extremes.
- Roll back by reinstalling PanelCladdingEditor 1.0.26; no workbook migration is part of this increment.

## Future Extensions

- Cursor-centered zoom while preserving the same bounded envelope.
- Optional grid interval preferences expressed in project units.
- Persist structural divider edits through a dedicated Rhino geometry apply contract.
