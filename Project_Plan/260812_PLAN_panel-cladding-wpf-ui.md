# Panel Cladding WPF UI

## Background

The existing `PanelCladdingEditorWindow` is a compact Eto form whose layout and interaction model do
not match the supplied `Design/PanelCladdingEditor/Panel-Cladding-Editor.zip` handoff. The requested
replacement must remain a desktop window, must not use Rhino/Eto native controls, and must not embed
the exported HTML as a web application.

## Goal

Replace the editor surface with a Windows WPF window that reproduces the exported Panel Cladding
Editor visual system and interaction hierarchy while retaining the existing standalone Rhino plug-in,
application services, live repository, workbook synchronization, and transactional save behavior.

## Architecture Ownership

- `UI/`: WPF window, XAML design tokens/styles, custom panel-grid renderer, material presentation,
  selection/assignment interactions, and the material setup dialog.
- `Application/`: the controller remains the boundary between WPF and existing load/project/save/type
  identity services.
- `Domain/`: existing panel layout, cell, projection, and type-identity contracts remain authoritative.
- `Infrastructure/Rhino/`: existing live-document reads and commits remain unchanged.
- `Project_Test/260812_TEST_panel-cladding-wpf-ui/`: WPF structure/style assertions, off-screen render
  fixture, and visual QA evidence.

## Key Design

1. Use WPF/XAML targeting Windows desktop. Do not host WebView, HTML, Eto controls, or a Rhino panel.
2. Reproduce the handoff's light neutral token system, typography, spacing, borders, button states,
   56-pixel header, responsive canvas/sidebar layout, fixed action footer, and material setup dialog.
3. Use a custom WPF `FrameworkElement` for the gridded preview so the panel can scale without DOM or
   bitmap assets. Draw real panel dimensions, weighted rows/columns, assignments, parent arrows,
   dashed shared boundaries, hover/selection states, zoom, click selection, modifier selection, and
   marquee selection.
4. Build the material legend from real panel assignments and a deterministic presentation palette.
   Allow session-local material definitions through the setup dialog; saved cell values remain the
   existing material-code strings.
5. Wire supported design actions to real behavior: clear assignment, multi-cell assignment, parent
   references, keyboard delete and undo, workbook selection, calculated type preview, save/sync,
   dirty-state protection, and window activation.
6. Present extrusion view and dimension-authoring controls in their designed disabled/read-only
   states because the current application contract does not safely persist extrusion or offset edits.
7. Preserve the existing controller/application/infrastructure layering. Add type-preview delegation
   to the controller rather than duplicating signature logic in the WPF layer.
8. Own the WPF window from Rhino's main HWND and keep the current single-editor lifecycle behavior.
9. Retarget direct PanelCladdingEditor test hosts to `net8.0-windows` as required by the WPF project;
   do not change their behavioral assertions except where the UI architecture contract changes.

## Files Involved

- `src/PanelCladdingEditor/PanelCladdingEditor.csproj`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorController.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingMaterial.cs`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml.cs`
- direct `Project_Test/*PanelCladding*.csproj` project references affected by the Windows TFM
- `Project_Test/260812_TEST_panel-cladding-wpf-ui/`

The obsolete Eto `PanelCladdingEditorWindow.cs` and `PanelCladdingPreviewCanvas.cs` implementations
will be removed after their WPF replacements compile.

## Usage

Run `_PanelCladdingEditor` with one panel Brep selected. The command opens or reactivates one WPF
editor window owned by Rhino. Select one or more grid cells, assign a material or parent cell, choose
an `.xlsx` workbook, and use **Save & sync** to commit through the existing save service.

## Acceptance Criteria

- No production PanelCladdingEditor UI source or assembly reference depends on Eto or an embedded web
  control.
- The window reproduces the supplied header, toolbar, gridded canvas, weighted panel grid, dimension
  annotations, material legend, panel metadata, assignment tabs, workbook/type card, footer actions,
  and material setup dialog.
- Real layouts populate cell count, grid size, panel identity, layer, geometry state, units, dimensions,
  assignments, workbook path, and calculated type code.
- Click, modifier multi-select, marquee, zoom, clear, material assignment, parent assignment, undo,
  workbook selection, save/sync, dirty confirmation, and reactivation operate without losing current
  backend safeguards.
- Unsupported extrusion and dimension mutation controls are visibly disabled/read-only and explain
  their current boundary.
- The standalone plug-in and focused smoke projects build in Debug and Release.
- The compiled Debug and Release RHP assembly GUID remains non-empty and matches the plug-in class and
  package manifest.
- An off-screen WPF render at the design's 1440 x 900 desktop size is captured and visually inspected.

## Risks And Rollback

- WPF makes this UI Windows-only, consistent with Rhino 8 on the target laptop. Direct test hosts must
  use the Windows TFM.
- Rhino/WPF lifetime and ownership mistakes can leave an orphan window. The command retains a single
  static instance, assigns Rhino's main HWND, and clears the reference on close.
- Dense grids or narrow windows may reduce label space. The custom renderer scales the stage and the
  window enforces a practical minimum size.
- Rollback consists of restoring the Eto window/canvas and prior project target settings. No live
  Rhino document or workbook mutation occurs during construction or off-screen visual testing.

## Future Extensions

- Add an application contract for editing and validating panel offsets, then enable dimension inputs.
- Add extrusion selection, mullion authoring, merge, and explode workflows when their live services
  exist.
- Persist project material libraries and preview colors rather than keeping setup additions in the
  editor session.
- Add high-DPI snapshot baselines for more compact desktop sizes.
