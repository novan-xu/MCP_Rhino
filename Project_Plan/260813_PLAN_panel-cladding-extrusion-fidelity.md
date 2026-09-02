# Panel Cladding Extrusion Fidelity

## Background

The latest WPF editor improved extrusion presentation, but the Current Panel offset values still append
units and can clip, curve-name badges sit above rather than directly on their curves, and extrusion
strokes are redrawn in a way that makes them appear heavier than the cladding grid. The thin yellow
selection overwrite also lacks a deliberate selected-state treatment.

## Goal

Implement the five requested refinements while preserving five-decimal offset precision, the shared
WPF architecture, structural-preview behavior, and delayed Rhino synchronization.

## Architecture Ownership

- `UI/PanelCladdingEditorWindow.xaml.cs`: unit-free divider-offset display strings.
- `UI/PanelCladdingEditorWindow.xaml`: wider offset value allocation.
- `UI/PanelCladdingGridCanvas.cs`: exact shared base geometry, centered curve badges, and developed
  extrusion selection presentation.
- `Project_Test/260813_TEST_panel-cladding-extrusion-fidelity/`: focused layout, pixel, geometry, and
  source verification.

## Key Design

1. Keep units exclusively in the dedicated Units indicator. Offset fields display only five-decimal
   numeric text and retain their unit metadata solely for parsing backwards-compatible pasted values.
2. Reduce the wall/units column and offset-code gutter so each two-column offset value receives enough
   width for at least `XXX.XXXXX`, exceeding the requested `XXX.XXXX` capacity.
3. Center each curve-name badge exactly at the curve midpoint in both X and Y, so its center overlaps
   the curve rather than floating above it.
4. Derive cladding cell bounds so the background frame is exactly 2 DIPs and internal dividers exactly
   1.5 DIPs. Clip extrusion base strokes to the panel and redraw them with the same shared dark brush and
   constants, preventing outward frame growth.
5. Render extrusion selection as a bright-yellow halo behind the unchanged dark curve, plus a yellow
   selected name badge. The curve itself retains the normal 2/1.5-DIP weight and reads clearly against
   both light and colored cells.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260813_TEST_panel-cladding-extrusion-fidelity/`

## Acceptance Criteria

- Divider-offset display/edit text contains no unit suffix and preserves five decimal places.
- Every offset editor can show a value at least as wide as `999.99999` without clipping.
- Every extrusion badge center equals its curve midpoint.
- Cladding and extrusion base frames/dividers occupy identical pixel bands and use identical shared
  brush/weight definitions.
- A selected extrusion preserves its dark normal-weight core, gains a visible yellow halo, and uses a
  yellow selected badge without obscuring neighboring geometry.
- Focused Debug/Release tests, all affected WPF regressions, Debug/Release builds, visual inspection,
  identity checks, packaging, and installed validation pass.

## Risks And Rollback

- A halo can imply a heavier selected curve. Preserve the original dark core and treat yellow only as
  selection feedback; test the dark core width separately from the halo.
- Changing base cell bounds can shift the panel footprint by subpixels. Verify cladding and extrusion
  pixel bands in deterministic renders.
- Roll back by reinstalling PanelCladdingEditor 1.0.29; this increment performs no Rhino or workbook
  migration.

## Future Extensions

- Shared collision-aware extrusion badge layout for very short segments.
- Optional localized unit suffixes in read-only tooltips rather than editable values.
