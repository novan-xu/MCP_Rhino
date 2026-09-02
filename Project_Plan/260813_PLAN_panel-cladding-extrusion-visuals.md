# Panel Cladding Extrusion Visuals

## Background

The WPF editor's extrusion mode still reads as a separate diagram language: selected segments become
heavy dark strokes, intersection nodes are solid, extrusion labels use small blue badges, cell text
omits material codes, and the five structural actions sit in one oversized container. The shared
dimension system also remains too close to the panel for the requested presentation.

## Goal

Implement the ten requested cladding/extrusion presentation refinements while preserving the existing
WPF window architecture, structural edit behavior, bounded canvas navigation, and preview-only Rhino
synchronization contract.

## Architecture Ownership

- `UI/PanelCladdingGridCanvas.cs`: shared line-weight constants, yellow selection overwrite, expanded
  dimension offset, hollow intersections, extrusion label placement/typography, and desaturated full
  cell labels.
- `UI/PanelCladdingEditorWindow.xaml`: compact extrusion action styles and two grouped action clusters.
- `Project_Test/260813_TEST_panel-cladding-extrusion-visuals/`: focused render, geometry, style, pixel,
  and source verification.

## Key Design

1. Define one 2-DIP frame weight and one 1.5-DIP divider weight and use them in both cladding and
   extrusion render paths. Selected extrusion strokes overwrite in bright yellow at the same weight;
   they no longer add a heavy halo.
2. Use bright yellow as the cladding cell selection fill and extrusion segment selection stroke.
   Preserve red as a dedicated invalid-input color rather than reusing selection color.
3. Render each extrusion intersection as a white-centered circle with a 1.5-DIP dark contour so it
   reads as a hollow node above crossing lines.
4. Place every extrusion name horizontally centered directly above its segment midpoint. Use black
   text at 11 DIPs, two points larger than the current 9-DIP label.
5. In extrusion view, retain the cladding cell label and resolved material code, but render both with
   muted typography over the existing desaturated material fill.
6. Move the shared dimension line offset from 20 to 60 DIPs, exactly tripling the line-to-panel gap in
   both views. Locks and dimension text continue to follow the same dimension-line anchor.
7. Replace the single action container with two rounded groups: `Merge / Explode` and
   `Add H / Add V / Delete`. Use 32-DIP compact buttons and tighter widths/padding.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260813_TEST_panel-cladding-extrusion-visuals/`

## Acceptance Criteria

- Cladding and extrusion normal frame/divider strokes share 2/1.5-DIP constants.
- Selected cladding cells and selected extrusion segments overwrite in bright yellow.
- Selected extrusion segments retain the normal stroke weight.
- Extrusion intersections have a white center and dark 1.5-DIP ring.
- Extrusion names are black, 11-DIP text centered immediately above each segment midpoint.
- Extrusion-view cells show both cell labels and resolved material codes in muted typography.
- Dimension lines sit 60 DIPs from the panel in both views, with locks/text moving together.
- Structural actions are compact and visibly separated into the requested two groups.
- Focused Debug/Release tests, affected WPF regressions, Debug/Release builds, renders, assembly
  identity, packaging, and installed validation pass.

## Risks And Rollback

- Moving dimensions can clip compact layouts. Retain the established canvas margins and verify both
  desktop and compact renders.
- White-centered intersections can obscure a small piece of the crossing line by design. Limit the
  radius and assert the contour remains visible.
- Full extrusion cell text can crowd small cells. Reuse the cladding label stack and its existing
  fit/placement conventions with muted brushes.
- Roll back by reinstalling PanelCladdingEditor 1.0.28; this increment performs no Rhino or workbook
  migration.

## Future Extensions

- User-configurable dimension clearance presets.
- Optional extrusion-name collision avoidance for extremely dense typologies.
