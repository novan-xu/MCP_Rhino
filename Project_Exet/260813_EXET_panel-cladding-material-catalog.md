# Panel Cladding Material Catalog Execution

## Corresponding Plan

- Plan: `Project_Plan/260813_PLAN_panel-cladding-material-catalog.md`
- Execution date: 2026-08-13

## Related Artifacts

- Focused tests: `Project_Test/260813_TEST_panel-cladding-material-catalog/`
- Updated package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.26/`
- Installed version: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.26/`
- BKT workbook: `V:/01 Project Folders/P00020 BayHealth Kent Tower BKT/03-Design-Eng/03-BIM/05-Wireframe/WF Typology.xlsx`
- Pre-migration backup: `WF Typology.pre-material-catalog-20260813-022619.xlsx`
- Commit/PR: none requested.

## Execution Result / Actual Scope

- Replaced cladding-cell selection blending with an opaque `#FF1F1F` fill and white labels.
- Rebuilt parent-boundary rendering around the actual shared edge: a surface-colored clearing stroke
  removes the normal separator and a centered `3,3` dashed stroke replaces it.
- Kept the main WPF window architecture. No Eto, WebView, browser, or Rhino-native dialog dependency
  was introduced.
- Moved Drag Materials below Cell Assignment and above Cladding Type. Chips now use 11-DIP swatches,
  32-DIP minimum chip height, and reduced padding/contours.
- Moved workbook selection and project catalogue status into Material Setup. Workbook, browse,
  parent-reference, Exit, and Save controls use a 40-DIP control height and rounded templates.
- Replaced `EXCEL TYPOLOGY RECORD` with `CLADDING TYPE`; the main window now shows only the calculated
  key and contains no workbook control.
- Added domain/application contracts and Open XML persistence for a project `Materials` catalogue.
  Catalogue updates validate unique codes and `#RRGGBB` colors, use a prepared atomic replacement,
  and remove only sheets owned by the legacy `_CLADDING_INDEX`.
- Loading a panel reads the associated workbook catalogue and merges unknown active-panel codes as
  fallback entries.
- Normal editor Save and surface sync now calculate identities and write panel key/value data only.
  Neither workflow creates, reuses, or prunes workbook type sheets.
- Updated the surface-sync command so it no longer prompts for a typology workbook.
- Migrated BKT from 59 managed sheets to one formatted `Materials` table populated from seven Rhino
  material layers and their exact colors.
- Bumped, packaged, installed, and validated `PanelCladdingEditor` version `1.0.26` while Rhino was closed.

## Differences From Plan

- The BKT material extraction used the available read-only `rhino3dm` Python package because
  `RhinoCommon` cannot open a 3DM in a standalone process without its native Rhino host. No Rhino UI
  or Windows automation was used.
- The existing legacy type-sheet repository operations remain as compatibility APIs for historical
  regression coverage, but production editor/sync code no longer invokes them.
- The legacy surface-sync test retains separate direct workbook-repository coverage, while its
  orchestration assertions were updated to require no workbook mutation.

## Issues Found And Fixed During Construction

- Existing parent boundaries were drawn beside the cell separator because cell rectangles include a
  small inset. The shared-edge midpoint plus clearing stroke removes the double-line artifact.
- The first generated BKT catalogue inferred category from a missing JSON property. Verification
  caught `undefined finish`; the builder now derives category from the authoritative layer path and
  was rerun before installation.
- The first installation command inherited an unrelated PowerShell `$LASTEXITCODE` even though the
  installer completed. A separate `Validate` run confirmed the registry-only installation and hash.
- The prior surface-sync smoke expected a workbook commit. It was updated to assert the new intended
  Rhino-only behavior and then passed.

## Test Record

- `dotnet build MCP_Rhino.sln -c Debug --nologo` — exit 0, 0 warnings/errors.
- `dotnet build MCP_Rhino.sln -c Release --nologo` — exit 0, 0 warnings/errors.
- Focused material-catalog smoke, Release — PASS: catalogue round trip, Rhino-only save, UI source checks.
- UI refinement smoke, Debug and Release — PASS: five-decimal offsets, bright-red overwrite,
  panel-facing dimension marks, inline dimension edit, mode styles, rounded controls.
- Latest UI smoke, Debug and Release — PASS: WPF action model, no Eto/WebView, extrusion behaviors,
  1440x900 and compact renders.
- WPF UI smoke, Debug and Release — PASS: editor and Material Setup renders.
- Standalone editor, spawn, match, clear, surface-sync, region, and offset-sync Release regressions — PASS.
- Rhino-native Brep probes in the regions/offset tests — SKIP as expected because Rhino remained closed.
- Spreadsheet verification — imported output and installed BKT workbook each report one `Materials`
  sheet, one table, `A1:E8`, seven materials, and zero formula-error matches.
- Visual verification — inspected the legacy hidden index, a legacy type preview, the new Materials
  catalogue, the 1440x900 selected-cell render, the parent-boundary render, and Material Setup.
- Assembly identity — PASS; PanelCladdingEditor GUID
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino.
- Package build — `PanelCladdingEditor-1.0.26`, Release, 0 warnings/errors.
- Installed validation — PASS at `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.26`;
  RHP SHA-256 `29D703057AA8F9723A217385481B162ECBDBB14312F8860E95CA2AD7EAE92BEB`.
- BKT pre-migration backup SHA-256:
  `BBF584A29277510261373C522C00661EBD3B73329CE0C063FB85EC48BFC39D90`.
- Migrated BKT workbook SHA-256:
  `409A62F812849CA0DC312649D1FB63606153082082DB7EE05CE8B04B300B50B2`.

## Acceptance Alignment

All acceptance criteria in the plan are met. The catalogue contains GLS-003/004/005, STN-001/002/003,
and TER-001 with exact BKT layer colors. Panel save no longer depends on Excel. Workbook controls are
contained in Material Setup, and the main editor reflects the requested visual hierarchy.

## Rollback Verification

- Plug-in rollback remains available through the previously installed `1.0.25` package/version.
- Workbook rollback is a direct restore from the timestamped sibling backup whose hash matches the
  original pre-migration workbook.
- Catalogue persistence uses a prepared temporary file and atomic replace; a locked or concurrently
  changed workbook fails before replacement.

## Current Remaining Items

- Rhino-native geometry probes were not run because the user closed Rhino and this task did not
  authorize UI automation. They are unchanged from existing behavior and their host-required skips
  are recorded above.
- Rhino must be restarted by the user to load version `1.0.26`.

## Conclusion

The latest WPF editor design and project material-catalog architecture are implemented, verified,
packaged, installed, and ready for Rhino restart.
