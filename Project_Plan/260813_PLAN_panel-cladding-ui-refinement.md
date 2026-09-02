# Panel Cladding UI Refinement

## Background

The installed WPF PanelCladdingEditor reproduces the latest handoff, but real-window review identified
ten visual and interaction refinements: cladding selection needs a red overlay, dimension witness marks
face toward the panel, the panel-information card has the wrong fields and offset precision, the
toolbar repeats selection text, the in-window product header is redundant, dimensions use a modal
editor, segmented controls lack distinct active states, assignment fields use inconsistent native
typography/templates, and form corner radii are inconsistent.

## Goal

Refine the existing non-web WPF window to match the supplied screenshots and notes without changing
the established Rhino/workbook persistence boundary. Apply one coherent visual system to navigation,
assignment controls, dimensions, metadata, selection feedback, and form fields.

## Architecture Ownership

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`: window hierarchy, rounded control
  templates, segmented active states, compact panel-information card, and removal of redundant UI.
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`: panel-card values, active-state
  synchronization, inline dimension commit handling, and removal of obsolete header/readout logic.
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`: red cladding-selection overlay,
  panel-facing dimension witness marks, and in-canvas dimension editor.
- `Project_Test/260813_TEST_panel-cladding-ui-refinement/`: focused structural assertions,
  interaction checks, and off-screen visual evidence.
- `Packaging/PanelCladdingEditor/`: unique corrected package version and registry-owned deployment.

## Key Design

1. Replace the cladding selection outline with a translucent red fill overlay that preserves cell
   labels and material recognition.
2. Reverse the top and left dimension witness ticks so they extend from the dimension baseline toward
   the panel.
3. Replace the panel metadata grid with the reference hierarchy: panel ID/status, left-side wall type
   and units, vertical divider, and right-side two-column divider offsets. Format every offset with
   exactly five decimal places and reserve a wider gap between `H0`/`V0` codes and their values.
4. Remove the toolbar cell-selection sentence and the complete redundant in-window product/status
   header. Retain the system window title bar.
5. Give the active cladding/extrusion view a dark-gray background with white text. Give the active
   Material/Parent cell assignment tab a white raised background while leaving its inactive sibling
   on the neutral segmented-control surface.
6. Replace dimension popups with an in-canvas rounded text field. Clicking a numeric dimension enters
   edit mode; Enter or focus loss validates and commits, Escape cancels. Lock buttons remain separate.
7. Replace default native TextBox/ComboBox appearances with rounded WPF templates using the window's
   Segoe UI typography, border tokens, focus state, and consistent padding. Apply these explicitly to
   material and parent assignment controls.
8. Advance the package version. If Rhino is open at deployment time, build and validate the bundle
   but do not replace the loaded plug-in; stage or defer activation safely.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` for one exact WPF-smoke exclusion
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260813_TEST_panel-cladding-ui-refinement/`

## Usage

Open `_PanelCladdingEditor`, select cladding cells to see the red overlay, switch views through the
dark active tab, and click a dimension number to edit it inline. The panel card shows wall type,
units, and five-decimal divider offsets. Material and parent controls use the same rounded visual
language as the rest of the window.

## Acceptance Criteria

- Selected cladding cells receive red translucent fill and no thick selection outline.
- Dimension witness ticks extend toward the panel.
- The panel card contains only panel ID/status, wall type, units, and two-column five-decimal offsets.
- The redundant in-window header and toolbar selection sentence are absent.
- View and assignment segmented controls have the requested distinct active styles.
- Clicking a dimension number creates an inline editor in the canvas; no numeric popup opens.
- Material ComboBox and parent TextBox use Segoe UI and rounded custom templates.
- Form fields and segmented containers use consistent rounded corners.
- Debug/Release builds, focused smokes, prior regressions, RHP identity, bundle validation, and visual
  inspection pass.

## Risks And Rollback

- A custom inline field inside the drawing surface must correctly commit/cancel on keyboard and focus
  changes. The focused smoke will drive the editor and assert field lifetime and value propagation.
- Removing the in-window header eliminates its status text. Errors and save state remain available in
  the footer, toast, dirty badge, modal errors, and command result.
- A custom ComboBox template must retain selection and popup behavior; the smoke will assert item
  selection and template presence.
- Rollback is the prior `1.0.24` bundle. No UI smoke mutates a live Rhino document or workbook.

## Future Extensions

- Add keyboard traversal and accessibility peers for custom canvas dimensions and cells.
- Add persistent structural preview/apply contracts before enabling Rhino geometry save for edited
  offsets and mullions.
