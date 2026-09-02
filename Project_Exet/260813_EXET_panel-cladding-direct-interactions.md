# Panel Cladding Direct Interactions Execution

## Corresponding Plan

- Plan: `Project_Plan/260813_PLAN_panel-cladding-direct-interactions.md`
- Execution date: 2026-08-13

## Related Artifacts

- Focused tests: `Project_Test/260813_TEST_panel-cladding-direct-interactions/`
- Cladding render: `Project_Test/260813_TEST_panel-cladding-direct-interactions/panel-cladding-direct-cladding-1440x900.png`
- Extrusion render: `Project_Test/260813_TEST_panel-cladding-direct-interactions/panel-cladding-direct-extrusion-1440x900.png`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.27/`
- Installed version: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.27/`
- Commit/PR: none requested.

## Execution Result / Actual Scope

- Added an explicit transparent canvas background and changed empty-target selection handling so a
  blank click clears the entire selection in both cladding and extrusion modes, independent of Shift
  or Ctrl state.
- Replaced read-only Current Panel divider values with lightweight inline text fields. Enter or focus
  loss parses a five-decimal absolute H/V offset, validates it against immediate neighbors, pushes an
  undo snapshot, and rebuilds only editor-session state.
- Removed both assignment confirmation buttons. Material ComboBox changes immediately update selected
  cells in the preview dictionary. Parent Cell now uses a ComboBox generated from all non-selected
  cells and updates the preview immediately.
- Added synchronization guards so programmatic dropdown changes during load/selection refresh cannot
  write assignments. The focused smoke proves no live repository commit occurs for material, parent,
  or divider previews.
- Renamed `Save & sync` to `Save` and changed the footer to equal-width Exit/Save columns.
- Removed the dimension badge container. Dimension text now uses 12.5-DIP mono type centered at the
  original label location. Lock icons have independent 20-DIP hit areas centered directly on the
  dimension line with a small surface knockout.
- Moved scale immediately beside the view switch and added every 5% option from 80% through 150%.
  Clear Assignment now uses the rounded boxed compact-button style.
- Added Shift + right-button drag pan and middle-wheel 5% zoom. Zoom clamps at 0.80/1.50. Pan limits
  derive from the difference between current panel size and the centered 80% baseline, so pan is zero
  at 80% and remains symmetric/bounded at larger scales.
- Replaced the fixed 20-DIP background brush with canvas-rendered 10-inch physical model grid lines.
  The spacing derives from `254 mm / model-unit scale` and follows zoom/pan origin.
- Halved and aligned normal strokes: a 2-DIP panel frame and 1.5-DIP dividers in both cladding and
  extrusion views. Parent boundary clearing/dash strokes were reduced to 3/1 DIPs.
- Updated historical WPF smokes to assert the intentional direct-assignment and Save-only UI.
- Bumped, packaged, installed, and validated version `1.0.27` while Rhino remained closed.

## Differences From Plan

- The Current Panel field edits absolute divider offsets directly, while the canvas dimension editor
  continues to edit segment lengths and redistribute unlocked segments. This preserves the semantic
  distinction between displayed divider offsets and displayed bay dimensions.
- Assignment and divider previews share the existing editor undo stack. Divider changes remain
  structural session previews under the current architecture, so Save stays disabled until those
  changes are undone; the planned future Rhino structural apply contract remains separate.
- No live Rhino UI smoke was run because Rhino was intentionally closed and Windows UI automation was
  not authorized. Deterministic off-screen WPF renders and repository-capture smokes were used.

## Issues Found And Fixed During Construction

- Blank selection logic already accepted an empty hit set, but the custom Canvas had no explicit
  background, so WPF could omit blank regions from hit testing. Adding a transparent background fixed
  the pointer surface; empty targets now explicitly clear even when a modifier is held.
- The initial QA render inherited a deliberately clamped negative pan from the pan-bound test, clipping
  the top dimensions. The render fixture now returns through 80% (which resets pan to zero) before
  restoring 100% and generating visual artifacts.
- Historical UI smokes expected removed Apply and Save & sync buttons. Their assertions were updated
  to the new direct-interaction contract.

## Test Record

- Focused smoke Debug and Release — PASS:
  - blank deselection in cladding and extrusion;
  - direct material and parent updates with zero Rhino commits;
  - editable H0 update with zero Rhino commits;
  - synthetic wheel event advances 100% to 105%;
  - zoom clamps to 80–150%, pan collapses at 80%, positive/negative limits are symmetric;
  - dimension locks/labels are centered correctly;
  - toolbar adjacency, boxed Clear Assignment, equal footer buttons, 10-inch grid source, and
    half-weight strokes.
- Release regressions — PASS for standalone editor, spawn, match, clear, surface sync, regions,
  offset sync, WPF UI, latest UI, UI refinement, material catalogue, and direct interactions.
- Rhino-native Brep probes in region/offset tests — expected SKIP because Rhino was closed.
- `dotnet build MCP_Rhino.sln -c Debug --nologo` — exit 0, zero warnings/errors.
- `dotnet build MCP_Rhino.sln -c Release --nologo` — exit 0, zero warnings/errors.
- `git diff --check` — exit 0.
- Visual inspection — PASS for 1440x900 cladding and extrusion renders. Confirmed unboxed large
  dimensions, locks on dimension lines, physical grid, direct parent dropdown, editable offsets,
  boxed Clear Assignment, adjacent scale, equal footer actions, and aligned thin strokes.
- Assembly identity — PASS; PanelCladdingEditor assembly GUID
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` matches its manifest and differs from MCP_Rhino.
- Package build — `PanelCladdingEditor-1.0.27`, Release, zero warnings/errors.
- Registry-only installation — PASS at `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.27`.
- Installed RHP SHA-256:
  `C00FD9CED57EDFDE93256367B84584FB1DCBE564D218D23C2781D222A8378F73`.

## Acceptance Alignment

All nine requested behaviors are implemented. Selection, assignment controls, offset editing,
dimension presentation, toolbar/footer proportions, pan/zoom bounds, physical grid, and aligned line
weights have focused automated assertions and rendered evidence.

## Rollback Verification

- Version `1.0.26` remains available under the prior current-user plug-in version folder/package.
- This increment did not mutate the BKT workbook or Rhino document.
- All assignment/offset changes remain in memory until the existing Save/structural workflow permits
  a commit, and the test repository recorded zero early commits.

## Current Remaining Items

- Rhino must be restarted by the user to load `1.0.27`.
- Structural divider/geometry persistence is intentionally unchanged and remains a future dedicated
  Rhino geometry apply capability.

## Conclusion

The requested direct editor interactions are implemented, regression-tested, visually verified,
packaged, installed, and ready for Rhino restart.
