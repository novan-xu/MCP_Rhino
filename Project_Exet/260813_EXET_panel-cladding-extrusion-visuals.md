# Panel Cladding Extrusion Visuals Execution

## Corresponding Plan

- Plan: `Project_Plan/260813_PLAN_panel-cladding-extrusion-visuals.md`
- Execution date: 2026-08-13

## Related Artifacts

- Focused tests: `Project_Test/260813_TEST_panel-cladding-extrusion-visuals/`
- Cladding render:
  `Project_Test/260813_TEST_panel-cladding-extrusion-visuals/cladding-yellow-selection-1440x900.png`
- Extrusion render:
  `Project_Test/260813_TEST_panel-cladding-extrusion-visuals/extrusion-yellow-selection-1440x900.png`
- Compact render: `Project_Test/260812_TEST_panel-cladding-wpf-ui/panel-cladding-wpf-ui-1024x768.png`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.29/`
- Installed version: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.29/`
- Commit/PR: none requested.

## Execution Result / Actual Scope

- Added shared public canvas constants for 2-DIP frame strokes and 1.5-DIP divider strokes. Cladding
  cell insets and extrusion segment pens now derive from those same values.
- Replaced the extrusion selection halo with a direct color overwrite at the segment's normal stroke
  width. Selected frame and divider segments no longer become 11/10-DIP lines.
- Changed cladding selection fill and extrusion selection stroke to opaque bright yellow `#FFEA00`.
  Added a separate red error brush so invalid dimension input remains an error state rather than
  inheriting the new selection color.
- Changed extrusion intersection nodes from solid dark dots to white-centered circles with a dark
  1.5-DIP contour.
- Increased extrusion names from 9 to 11 DIPs, changed them to black, and placed every label
  horizontally centered five DIPs above its segment midpoint. This same rule now applies to
  horizontal, vertical, and frame segments.
- Kept full cladding cell text in extrusion view: the cell label and resolved material code use the
  same two-line placement as cladding view, rendered with muted typography over the existing
  desaturated material fill. Parent-reference cells show their inherited resolved material code.
- Tripled the shared dimension-line offset from 20 to 60 DIPs in both views. Locks and labels remain
  anchored to their dimension lines.
- Increased the reserved left/top canvas margins and slightly reduced the maximum panel footprint so
  the expanded dimension system remains fully visible in compact layouts.
- Replaced the single structural action container with two rounded groups:
  `Merge / Explode` and `Add H / Add V / Delete`. All five actions use compact 32-DIP styles, tighter
  padding, and reduced minimum widths.
- Bumped, packaged, installed, and validated PanelCladdingEditor `1.0.29` while Rhino remained closed.

## Differences From Plan

- The expanded dimension clearance required a follow-up canvas-footprint adjustment after compact
  visual QA. The final renderer reserves 210 horizontal and 190 vertical DIPs, with 150/110-DIP panel
  insets, so the 60-DIP dimension system does not clip at 1024x768.
- No live Rhino UI smoke was run because Rhino was intentionally closed and Windows UI automation was
  not authorized. Deterministic WPF renders and direct event/geometry/pixel smokes were used.

## Issues Found And Fixed During Construction

- Rendering the new 60-DIP dimensions with the old 104-DIP panel-left inset clipped the left dimension
  values in the 1024x768 regression render. Increasing the reserved margin and re-centering the panel
  restored complete values while preserving the existing zoom/pan envelope.
- The focused canvas initially sampled cached WPF output after changing selection. Its bitmap helper
  now invokes the canvas render path into a fresh `DrawingVisual`, matching the established deterministic
  WPF test pattern.
- A 1.5-DIP yellow line at a fractional device coordinate is anti-aliased against the underlying dark
  stroke. The focused test classifies the blended yellow center and separately caps the yellow band at
  four pixels, proving color overwrite without thickness inflation.

## Test Record

- Focused extrusion-visual smoke Debug and Release — PASS:
  - cladding selection interior is exact `#FFEA00`;
  - selected extrusion line is yellow and remains at a maximum four-pixel anti-aliased band;
  - intersection center is white and its surrounding ring contains the expected dark contour;
  - dimension locks resolve exactly to panel top/left minus the 60-DIP shared offset;
  - structural actions measure 32 DIPs and belong to the requested two parent groups;
  - source contracts confirm shared 2/1.5-DIP weights, black 11-DIP midpoint labels, hollow nodes,
    and muted resolved-material cell text;
  - deterministic 1440x900 cladding/extrusion renders generated.
- All 14 panel-cladding Release smoke projects — PASS. This covers standalone editor, spawn, match,
  clear, surface sync, region/offset reconstruction, WPF UI, latest UI, refinement, catalogue, direct
  interactions, visual polish, and extrusion visuals.
- Rhino-native Brep probes in the region/offset suites — expected SKIP because Rhino was closed.
- Desktop 1440x900 and compact 1024x768 WPF renders — visually inspected and PASS.
- `dotnet build MCP_Rhino.sln -c Debug --nologo -m:1 /nodeReuse:false` — exit 0, zero warnings/errors.
- `dotnet build MCP_Rhino.sln -c Release --nologo -m:1 /nodeReuse:false` — exit 0, zero warnings/errors.
- `git diff --check` — exit 0.
- Assembly identity — PASS. PanelCladdingEditor assembly GUID
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` matches its manifest and remains distinct from MCP_Rhino.
- Package build — `PanelCladdingEditor-1.0.29`, Release, zero warnings/errors.
- Packaged and installed RHP identity checks — PASS.
- Registry-only installation and installed validation — PASS at
  `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.29`.
- Installed RHP SHA-256:
  `6116A7149F03C4EC85D909B9C882E3C0122027A39C187FED588C2E0E4AC5211C`.

## Acceptance Alignment

All ten requested refinements are implemented: aligned line weights, smaller two-group structural
actions, hollow intersections, larger black midpoint labels, yellow selection in both modes, triple
dimension clearance, and desaturated extrusion cells retaining material codes.

## Rollback Verification

- The installer preserved prior `1.0.28` under its recorded current-user rollback directory, and the
  `PanelCladdingEditor-1.0.28` package artifact remains available.
- This increment did not mutate any Rhino document or project workbook.

## Current Remaining Items

- Rhino must be restarted by the user to load `1.0.29` and register its commands.

## Conclusion

The requested cladding/extrusion visual alignment is implemented, regression-tested, visually
verified, packaged, installed, and ready for Rhino restart.
