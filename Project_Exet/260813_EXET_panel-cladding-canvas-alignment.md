# Panel Cladding Canvas Alignment Execution

## Corresponding Plan

- Plan: `Project_Plan/260813_PLAN_panel-cladding-canvas-alignment.md`
- Execution date: 2026-08-13

## Related Artifacts

- Focused tests: `Project_Test/260813_TEST_panel-cladding-canvas-alignment/`
- Desktop renders:
  - `panel-cladding-centered-1440x900.png`
  - `panel-extrusion-centered-1440x900.png`
- Compact renders:
  - `panel-cladding-centered-1024x768.png`
  - `panel-extrusion-centered-1024x768.png`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.31/`
- Staged bundle:
  `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.31-20260813161050358/`
- Active installed version:
  `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.31/`
- Commit/PR: none requested.

## Execution Result / Actual Scope

- Removed the entire bottom canvas instruction legend, including Drag, Shift, Ctrl, right-drag pan,
  and wheel-zoom text. Transient operation notices remain available and were not changed.
- Kept the view switch as the left toolbar group and created a named far-right action group containing
  Clear assignment followed immediately by the zoom selector.
- Styled Clear assignment with the compact form of the same danger-button contract used by Exit:
  rounded white surface, red text, and red outline.
- Removed the obsolete code-behind visibility mutation for the deleted instruction panel.
- Replaced the asymmetric 150-left/110-top placement over 210/190 total reserves with equal 150-DIP
  horizontal and 110-DIP vertical clearances on both sides.
- Calculated the zero-pan panel origin directly from `(actual canvas size - panel size) / 2`, making
  the panel center equal the actual canvas center instead of 45 DIPs right and 15 DIPs down.
- Reused the symmetric available area in pan clamping, preserving the 80%-to-150% zoom and bounded-pan
  behavior.
- Updated the direct-interaction regression for the new right-toolbar contract and added a focused
  desktop/compact structural and rendered-layout suite.
- Bumped and packaged PanelCladdingEditor `1.0.31`. The supported installer first staged the validated
  bundle because Rhino was running, then installed it registry-only after every Rhino process closed.

## Differences From Plan

- None in product behavior. The focused test was initially launched in Debug and Release in parallel;
  both attempted to write the same deterministic render path. It was immediately rerun serially and
  passed in both configurations. Product code was unaffected.

## Issues Found And Fixed During Construction

- The previous centering formula's constants made the displacement deterministic: horizontal center
  was `ActualWidth / 2 + 45`, and vertical center was `ActualHeight / 2 + 15`. Symmetric clearances and
  direct centering removed both offsets.
- The existing direct-interaction smoke asserted the superseded design where zoom shared the left view
  group. It now checks the requested far-right Clear-then-zoom group and the danger visual contract.

## Test Record

- Focused canvas-alignment smoke Debug and Release — PASS:
  - no instruction panel or instruction strings remain;
  - Clear assignment is immediately left of zoom in the far-right toolbar group;
  - Clear assignment matches Exit foreground, outline, and corner-radius styling;
  - cladding and extrusion panel centers equal actual canvas centers within 0.01 DIP at 1440x900 and
    1024x768;
  - view switching preserves identical panel bounds.
- Focused desktop and compact cladding/extrusion renders — visually inspected and PASS.
- Updated direct-interaction Release smoke — PASS.
- All 16 panel-cladding Release smoke projects — PASS. Rhino-native Brep probes in region/offset suites
  remain expected skips because they require a native Rhino test host.
- `dotnet build MCP_Rhino.sln -c Debug --nologo -m:1 /nodeReuse:false` — exit 0, zero warnings/errors.
- `dotnet build MCP_Rhino.sln -c Release --nologo -m:1 /nodeReuse:false` — exit 0, zero warnings/errors.
- `git diff --check` — exit 0; existing line-ending notices only.
- Debug and Release assembly identity — PASS. PanelCladdingEditor GUID
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` matches the manifest and remains distinct from MCP_Rhino.
- Package and staged RHP identity — PASS.
- Installed registry-only validation and installed RHP identity — PASS. The Rhino registry points to
  `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.31/PanelCladdingEditor.rhp`.
- Package/stage RHP SHA-256:
  `CCCACD727E97451968CEA33140B1F1CE4687C4E37510352217F3AEA2C1B2B1EB`.
- Installed RHP SHA-256 matches the package/stage hash exactly.

## Acceptance Alignment

All three requested changes are implemented: the bottom instructions are gone, Clear assignment and
zoom occupy the requested far-right positions with danger styling, and both panel views are exactly
centered on the canvas.

## Rollback Verification

- Previous `1.0.29` files remain available for rollback.
- Existing packages and staged bundles remain available.
- This increment did not mutate any Rhino document or project workbook.

## Current Remaining Items

- Restart Rhino to load `1.0.31` and register its commands in the new Rhino session.

## Conclusion

The canvas cleanup, toolbar relocation/styling, and exact two-view centering are implemented, fully
regression-tested, visually verified, packaged, identity-validated, and installed registry-only.
