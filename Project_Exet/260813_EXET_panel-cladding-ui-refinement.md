# Panel Cladding UI Refinement Execution

## Corresponding Plan

- Plan: `Project_Plan/260813_PLAN_panel-cladding-ui-refinement.md`
- Execution date: 2026-08-13
- User references: the two supplied UI screenshots and the ten numbered refinement notes

## Related Artifacts

- Focused tests: `Project_Test/260813_TEST_panel-cladding-ui-refinement/`
- Release bundle: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.25/`
- Installed root: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.25/`
- Rollback: `%LOCALAPPDATA%/PanelCladdingEditor/rollback/d424e40841394e65bfea3dd59c067676/prior-plugin-1.0.24/`
- Commit / PR: none created in this execution

## Execution Result

The existing latest WPF window was refined without adding Eto, Rhino.UI, WebView, HTML, or browser
hosting. All ten requested changes are implemented:

1. cladding selection uses a translucent red fill overlay and no selection-thickness border;
2. top and left dimension witness marks extend toward the panel;
3. Current Panel now presents panel ID/status, wall type, units, and a compact two-column divider list;
4. divider values use exactly five decimals and a wider code/value gap;
5. the toolbar cell-selection sentence was removed;
6. Cladding/Extrusion selected mode uses dark gray with white text;
7. the redundant product/status header inside the system window was removed;
8. dimension values edit inline in a rounded canvas field and commit on Enter/focus loss or cancel on Escape;
9. material and parent controls share Segoe UI and project-native rounded templates, with the active
   assignment mode highlighted white;
10. field and segmented-control corners now use a consistent rounded visual language.

The sidebar widened from 340 to 360 device-independent pixels after visual review so the five-decimal
offsets remain readable. Dimension badges widened from 76 to 86 pixels so the inline five-decimal field
does not clip.

## Problems Found And Fixed

1. The existing extrusion assignment section was nested inside the cladding section, causing its
   visibility to depend on a collapsed parent. The XAML hierarchy was repaired so they are siblings.
2. WPF `Tag` values declared in XAML as strings and values assigned from C# as booleans did not produce
   a reliable style trigger. Selected-state tags now use explicit `Selected` / `Unselected` strings.
3. An early selection smoke sampled a cell already selected by the fixture. The test now clears the
   selection, renders a baseline, selects a cyan cell, and compares the resulting pixel blend.
4. The original 76-pixel dimension badge clipped a five-decimal inline value. The production badge and
   focused fixture now use 86 pixels.

## Test Record

### Focused smoke

`PanelCladdingUiRefinementSmoke` passed in Debug and Release. It asserts the metadata hierarchy,
`CW-01` formatting, exact five-decimal offsets, red pixel overlay, inward witness-mark source contract,
inline editor lifecycle and commit, selected-mode colors, rounded templates, Segoe UI assignment fields,
header/readout removal, and the absence of Eto/WebView references.

Visual evidence was generated and inspected:

- `panel-cladding-refined-base-1440x900.png` — 112,861 bytes
- `panel-cladding-refined-selection-1440x900.png` — 112,861 bytes
- `panel-cladding-refined-inline-edit-1440x900.png` — 113,562 bytes

### Production and regression tests

- PanelCladdingEditor Debug build: passed, zero warnings/errors.
- PanelCladdingEditor Release build: passed, zero warnings/errors.
- Latest UI and prior WPF UI smokes: passed.
- Regions, offset sync, surface sync, clear, match, spawn, and standalone editor smokes: passed.
- The existing native-host-only region/offset probes produced their expected `[SKIP]`; every managed
  assertion passed.
- MCP_Rhino and PanelCladdingEditor assembly-identity verification passed in Debug and Release.
- PanelCladdingEditor GUID remained `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` and distinct from MCP_Rhino.

### Package and installation

Package build, registry-only Repair, and Validate all exited `0`. Rhino was confirmed closed before
activation. Installed version is `1.0.25`; the bundle and installed RHP are both 345,600 bytes with
SHA-256 `43ac85f0cf6e96dabc6282819caa7b59e084f947f5bcf868d0fe87d94a951536`.
No duplicate `PanelCladdingEditor.dll`, Eto, Rhino.UI, or WebView assembly exists in the plug-in payload.
The prior 1.0.24 payload was moved to installer-owned rollback storage.

## Deviation From Plan

No persistence contract was expanded. Inline dimension edits retain the existing reversible structural
preview boundary, and Rhino/Excel saving remains disabled while a structural preview is dirty. The user
asked for presentation and interaction refinement, not a new geometry-commit contract.

## Conclusion

The requested latest UI refinements are implemented, regression-tested, visually inspected, packaged,
and installed as PanelCladdingEditor 1.0.25. Restarting Rhino will load this version.
