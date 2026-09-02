# Panel Cladding Visual Polish

## Background

The current WPF Panel Cladding Editor implements bounded pan, editable Current Panel offsets, parent
cell dashed boundaries, and a project material catalogue. Four remaining details weaken the intended
design: pan still requires Shift, parent dashes expose a white clearing halo and use a lighter stroke,
sidebar offset fields do not visibly enter edit mode, and the Material Setup window does not share the
main editor's compact typography and control system.

## Goal

Apply the requested interaction and visual polish while preserving the native WPF window architecture,
preview-only edit semantics, material catalogue data contract, and delayed Rhino synchronization.

## Architecture Ownership

- `UI/PanelCladdingGridCanvas.cs`: right-button pan gesture and parent-boundary rendering.
- `UI/PanelCladdingEditorWindow.xaml`: rounded divider-offset edit presentation and help text.
- `UI/PanelCladdingEditorWindow.xaml.cs`: temporary focus-state styling for offset editors.
- `UI/MaterialSetupDialog.xaml`: secondary-window typography, spacing, sizing, and reusable controls.
- `Project_Test/260813_TEST_panel-cladding-visual-polish/`: focused interaction, source, render, and
  pixel-level boundary verification.

## Key Design

1. Begin bounded pan on an unmodified right-button drag; retain the existing 80%-footprint clamp and
   wheel zoom behavior.
2. Erase the solid shared boundary with the resolved cell material at exactly the normal divider
   weight, then draw the dashed parent boundary at that same weight. This removes the white halo and
   makes dashed and solid separators visually consistent.
3. Give Current Panel offset TextBoxes the same rounded 1.5-DIP green outline used by canvas dimension
   editing. The outline and surface background appear only while focused and disappear after commit or
   cancellation without changing layout.
4. Recompose Material Setup from the main editor's WPF visual tokens: Segoe UI, compact eyebrow labels,
   40-DIP form controls and primary actions, 32/36-DIP compact selectors, six-pixel corners, consistent
   section spacing, and an uncluttered catalogue list.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/MaterialSetupDialog.xaml`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260813_TEST_panel-cladding-visual-polish/`

## Acceptance Criteria

- Right-button drag pans without Shift and remains bounded to the existing 80% envelope.
- Parent boundary gaps reveal the panel material, never a white backing line; dash and normal divider
  strokes are both 1.5 DIPs.
- Focusing an editable Current Panel offset shows a rounded green outline; Enter, Escape, or focus loss
  removes it.
- Material Setup uses the main editor's font, label hierarchy, corner radius, control heights, spacing,
  and button language without clipping at the target window size.
- Focused Debug/Release tests, affected WPF regressions, Debug/Release solution builds, rendered visual
  inspection, assembly identity checks, packaging, and installed-package validation pass.

## Risks And Rollback

- Erasing a shared edge with the wrong material could create a colored seam. Resolve the effective
  inherited material and test both dash and gap pixels inside a colored fixture.
- Focus styling can shift inline text if its border is added dynamically. Reserve the border thickness
  and switch only its transparent/green brush.
- Compacting Material Setup can impair narrow layouts. Preserve scrolling and minimum dimensions, and
  render the dialog at its declared size.
- Roll back by reinstalling PanelCladdingEditor 1.0.27; this increment performs no Rhino or workbook
  migration.

## Future Extensions

- Keyboard-accessible panning and explicit drag cursor preferences.
- Shared WPF resource dictionaries across the main and setup windows.
