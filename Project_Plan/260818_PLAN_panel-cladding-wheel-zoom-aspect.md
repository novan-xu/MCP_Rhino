# Panel Cladding Wheel Zoom Aspect Fix

## Background

Panel `E1_05_48` is approximately `18.8 × 199.5 in`, so its width/height ratio is about `0.094`.
In the Panel Cladding Editor, wheel zoom changes the toolbar value from 80% toward 150%, but the
preview's narrow dimension remains visually fixed while its height grows. The result looks like a
vertical stretch rather than a uniform zoom.

The failure is in `PanelCladdingGridCanvas.CalculatePanelSize`. The method calculates an
aspect-correct fit, applies zoom, and then clamps width and height independently with `Math.Max(80,
...)` and `Math.Max(60, ...)`. At the user's canvas/DPI and the panel's extreme ratio, the calculated
width stays below the 80-DIP floor throughout the zoom range. Width is therefore frozen at 80 DIPs
while height continues to scale.

## Goal

Make wheel and toolbar zoom uniformly scale every panel preview while preserving the panel's model
width/height ratio, including extremely tall or wide panels. The existing 80–150% range, 5% wheel
step, centered baseline, bounded pan, hit testing, dimensions, and both editor views must remain
intact.

## Architecture Ownership

- `UI/PanelCladdingGridCanvas.cs`: preview sizing and wheel/pan behavior.
- `Project_Test/260818_TEST_panel-cladding-wheel-zoom-aspect/`: focused tall-panel sizing, input,
  centering, hit-geometry, and visual regression evidence.
- Existing canvas/direct-interaction/cell-topology smokes: compatibility coverage.
- `Packaging/PanelCladdingEditor/`: corrected versioned package and current-user installation when
  Rhino is not holding the RHP.

## Key Design

1. Preserve the existing aspect-correct fit rectangle derived from available canvas width/height.
2. Apply one zoom scalar to both fitted dimensions. Do not independently floor width and height
   after zoom because any unequal clamp changes the geometry ratio.
3. Retain only a negligible finite-size guard for invalid/near-zero output; valid layouts already
   have positive extents and are validated upstream.
4. Keep `SetZoom`, the 80–150% bounds, 5% steps, the toolbar synchronization event, centered zoom,
   and the existing pan envelope unchanged. Cursor-centered zoom is outside this focused fix.
5. Because all downstream drawing, dimensions, hit rectangles, extrusion geometry, selection, and
   model-grid spacing derive from `CalculatePanelBounds`, correcting that single source keeps visual
   and interaction geometry aligned.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- `Project_Test/260818_TEST_panel-cladding-wheel-zoom-aspect/` (new)
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` (exclude the standalone test from its intentional
  repository test-source compile glob)
- `Packaging/PanelCladdingEditor/package-manifest.json` (version increment if installed)
- `Project_Exet/260818_EXET_panel-cladding-wheel-zoom-aspect.md` (after verification)

## Test Strategy

The focused smoke will use an `18.75 × 199.5` panel fixture matching `E1_05_48` and a constrained
canvas where the old 80-DIP width floor activates. It will assert:

- the old sizing formula reproduces a changed preview aspect ratio between 80% and 150%;
- corrected bounds retain `18.75 / 199.5` at 80%, 100%, and 150%;
- width and height both grow for positive wheel input and shrink for negative input;
- the width and height scale factors are equal and match the zoom-factor ratio;
- wheel input remains handled and changes zoom by exactly 5%;
- panel bounds remain centered at zero pan;
- cladding cell hit rectangles and extrusion geometry use the same corrected bounds; and
- deterministic 80% and 150% tall-panel renders show uniform scaling rather than stretching.

Existing canvas-alignment, direct-interaction, cell-topology, and latest-UI tests will be rerun in
proportion to the change. Debug/Release direct builds, solution builds, `git diff --check`, and
assembly-level plug-in identity validation remain required.

## Acceptance Criteria

- Wheel zoom changes both visible panel dimensions for `E1_05_48`.
- The preview width/height ratio equals the model width/height ratio at every supported zoom value.
- The relative scale from 80% to 150% is identical on X and Y.
- Toolbar zoom and wheel zoom continue to use one shared state and 5% increments.
- Cladding and extrusion views retain identical corrected bounds.
- Selection/hit geometry, dimensions, grid, and panning remain aligned with the preview.
- Focused Debug/Release tests, relevant existing regressions, builds, identity checks, and safe
  package/install validation pass.

## Risks And Rollback

- Extremely narrow panels will render narrower at low zoom because the artificial 80-DIP distortion
  is removed. This is the correct aspect-preserving behavior; users can zoom and pan for detail.
- Text in very narrow cells can have limited room, but the preview geometry must not be widened
  independently of height. Existing text clipping/visibility rules remain unchanged.
- The change is isolated to the size calculation and can be reverted independently. No Rhino panel
  attributes, workbook data, or geometry are changed by preview zoom.
- No UI automation or production Rhino document mutation is authorized or required.

## Future Extensions

- Add optional cursor-anchored zoom while retaining the corrected uniform scale.
- Add adaptive label-detail levels for extremely narrow panels.
- Expose a Fit command distinct from the current bounded percentage zoom.
