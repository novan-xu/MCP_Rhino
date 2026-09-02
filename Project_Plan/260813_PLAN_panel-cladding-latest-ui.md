# Panel Cladding Latest UI

## Background

The supplied `Design/PanelCladdingEditor/Panel-Cladding-Editor.zip` contains two HTML handoff files.
Although `DESIGN-MANIFEST.json` names `panel-cladding-editor-v2.html`, that file is the older export
(2026-08-12 19:53). The larger `panel-cladding-editor.html` was written later
(2026-08-12 22:14) and contains the current extrusion workspace, structural editing controls,
five-decimal dimension controls, divider list, and updated sidebar. The first WPF implementation
followed the stale manifest entry and therefore presented an outdated design.

## Goal

Make `panel-cladding-editor.html` the visual and interaction source of truth for the standalone
PanelCladdingEditor window. Preserve the non-web Windows WPF architecture and adopt the complete
latest cladding/extrusion layout, styling, responsive behavior, and all interactions that can be
implemented safely with the current Rhino persistence contracts.

## Architecture Ownership

- `UI/`: latest-design WPF layout, cladding/extrusion renderers, editor-session structural state,
  dimension and mullion dialogs, selection, merge/explode/delete, materials, and responsive layout.
- `Application/`: existing controller remains the boundary for load, type preview, and save/sync.
- `Domain/`: the loaded Rhino panel layout remains immutable source data during an editor session.
- `Infrastructure/Rhino/`: existing live read/save and Undo behavior remains unchanged.
- `Packaging/PanelCladdingEditor/`: versioned release staging and current-user installation.
- `Project_Test/260813_TEST_panel-cladding-latest-ui/`: structure, interaction, rendering, packaging,
  and assembly-identity evidence.

## Key Design

1. Treat `panel-cladding-editor.html` as authoritative despite the stale manifest pointer. Record the
   timestamp and feature evidence in the TEST/EXET artifacts so the selection is reproducible.
2. Keep a native Windows WPF top-level window owned by Rhino's main HWND. Do not use Eto, Rhino panel
   controls, HTML, WebView, or another browser host.
3. Move the enabled **Extrusion view / Cladding view** toggle into the canvas toolbar and reproduce
   the latest bottom action shelf: **Merge**, **Explode**, **Add H**, **Add V**, and **Delete**.
4. Extend the custom vector canvas to render frame and divider extrusion segments, segment codes,
   hover/selected states, click/modifier/marquee selection, merged collinear runs, exploded runs,
   deleted segments, and mode-specific hit testing.
5. Add editor-session horizontal/vertical divider state derived from the loaded layout. Adding a
   mullion splits the corresponding cladding cells and remaps assignments; dimension edits preserve
   the overall panel size; locks prevent redistribution through locked segments.
6. Reproduce the latest panel-detail sidebar with divider-offset lists and a mode-specific extrusion
   assignment card. Keep the cladding material/parent workflow in cladding mode.
7. Preserve undo for both value and structural editor state. Cell assignment changes continue to
   save through the existing application service. Structural extrusion mutations remain explicit
   editor-session previews because the current save request has no safe contract for geometry or
   offset mutation; the UI must disclose this boundary rather than silently claiming persistence.
8. Increment the package version so the corrected UI cannot be confused with the installed stale
   build. Keep one registry-owned current-user install and validate the compiled RHP assembly GUID.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorWindow.xaml.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingGridCanvas.cs`
- new focused WPF dialog/state files under `src/PanelCladdingEditor/UI/`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- focused project/build files only if required by compilation or packaging
- `Project_Test/260813_TEST_panel-cladding-latest-ui/`

## Usage

Run `_PanelCladdingEditor` with one supported panel Brep selected. Use **Cladding view** for cell
selection, material assignment, and parent references. Use **Extrusion view** to inspect/select
frames and dividers, merge/explode contiguous segments, add horizontal or vertical mullions, and
delete session segments. Use **Save & sync** for supported Rhino attributes and workbook output.

## Acceptance Criteria

- The view toggle is enabled and positioned in the toolbar as in the latest handoff.
- Extrusion mode renders frame/divider segments and exposes working merge, explode, add-H, add-V,
  delete, modifier selection, marquee selection, and clear-selection behavior.
- The current-panel card includes layer, Brep status, grid, units, and the latest divider-offset list.
- Cladding mode retains real material/parent assignment, drag/drop, zoom, keyboard delete, undo,
  workbook selection, type preview, and save/sync behavior.
- Dimension presentation uses five-decimal precision and supports lock-aware editor-session changes.
- The window remains WPF-only, non-web, and owned by Rhino.
- Focused Debug and Release builds succeed; the Release package is versioned uniquely.
- The RHP assembly GUID is non-empty and matches the plug-in class and package manifest.
- Off-screen latest-design screenshots cover cladding, extrusion, and the add-mullion dialog at
  desktop and compact window sizes and are visually inspected.

## Risks And Rollback

- Structural edits cannot currently commit divider geometry safely. They remain reversible session
  state and are visibly described as preview-only. Existing save behavior is not expanded beyond its
  contract.
- Splitting cells can create session-only keys not present on the source Brep. Save must not write
  those synthetic cells; the window will guard or disclose unsupported structural persistence.
- WPF owner/lifetime or high-DPI issues are covered by the existing single-window pattern and
  off-screen responsive render tests.
- Rollback is the prior WPF UI and prior package version. No construction test mutates a live Rhino
  document or production workbook.

## Future Extensions

- Add a preview/apply Rhino contract for mullion geometry, offsets, segment topology, and synthetic
  cell-key migration, then persist structural edits through one Rhino Undo record.
- Add profile/finish libraries and extrusion assignment persistence.
- Add high-DPI image baselines and accessibility automation peers for custom canvas elements.
