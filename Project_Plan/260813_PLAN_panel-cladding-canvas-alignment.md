# Panel Cladding Canvas Alignment

## Background

The WPF editor still reserves canvas space asymmetrically, so an unpanned panel sits to the right and
below the actual canvas center. The bottom interaction legend is no longer wanted, and the toolbar
places the zoom selector beside the view switch instead of with the Clear assignment action.

## Goal

Remove the bottom canvas instructions, place a red-outline Clear assignment button immediately left
of the zoom selector at the far right of the toolbar, and center the panel on the true canvas midpoint
in both cladding and extrusion views.

## Architecture Ownership

- `UI/PanelCladdingEditorWindow.xaml`: toolbar composition and removal of the instruction legend.
- `UI/PanelCladdingEditorWindow.xaml.cs`: removal of the obsolete instruction-visibility update.
- `UI/PanelCladdingGridCanvas.cs`: symmetric panel clearance and zero-pan center calculation.
- `Project_Test/260813_TEST_panel-cladding-canvas-alignment/`: focused structural, layout, render, and
  source verification.

## Key Design

1. Keep the two view buttons as the toolbar's left group. Put Clear assignment and zoom in one named,
   right-aligned action group, in that order.
2. Reuse the Exit button's `DangerButton` visual contract for Clear assignment: red text, red outline,
   white surface, and rounded corners.
3. Remove the instruction panel from XAML and its view-mode visibility mutation from code-behind.
4. Replace the asymmetric left/top inset calculation with equal horizontal and vertical clearances.
   At zero pan, calculate the panel origin directly from `(canvas size - panel size) / 2`.
5. Use the same symmetric available area in pan clamping so 80% remains the maximum visible extent and
   both views share identical placement behavior.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260813_TEST_panel-cladding-direct-interactions/Program.cs`
- `Project_Test/260813_TEST_panel-cladding-canvas-alignment/`

## Acceptance Criteria

- No instruction legend is rendered at the bottom of the canvas.
- Clear assignment and zoom occupy one far-right group; Clear assignment is immediately left of zoom.
- Clear assignment uses the same red-outline/red-text visual style as Exit.
- With no pan, panel center equals actual canvas center in both views at desktop and compact sizes.
- Switching views does not change panel bounds, center, zoom, or pan behavior.
- Focused Debug/Release tests, all affected WPF regressions, Debug/Release builds, visual inspection,
  assembly identity validation, packaging, and supported installation/staging pass.

## Risks And Rollback

- Symmetric dimension clearance slightly reduces the fitted panel size. Keep enough space for top and
  left dimensions and verify desktop and compact renders.
- Removing the legend must not remove transient operation notices; the existing toast remains intact.
- Roll back by reinstalling PanelCladdingEditor 1.0.30; this change does not migrate Rhino or workbook
  data.

## Future Extensions

- Collision-aware dimension layout for unusually narrow editor windows.
- A user-invoked shortcuts/help surface if interaction guidance is needed later.
