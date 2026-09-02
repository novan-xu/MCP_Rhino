# PCSpawnSrf Boundary, Parent Order, And Material Color Execution

## Corresponding Plan

- Plan: `Project_Plan/260820_PLAN_pcspawnsrf-boundary-parent-order.md`
- Execution date: 2026-08-20
- The plan contains a dated revision for the Material Setup color requirement added by the user
  during execution.

## Associated Artifacts

- Test folder: `Project_Test/260820_TEST_pcspawnsrf-boundary-parent-order/`
- Focused results: `Project_Test/260820_TEST_pcspawnsrf-boundary-parent-order/RESULTS.md`
- Existing region seam assertion:
  `Project_Test/260812_TEST_panel-cladding-regions/Program.cs`
- Commit / PR: none created

## Execution Result / Actual Scope

### Boundary-first PCSpawnSrf geometry

- Added a pure `PanelCladdingRegionBoundaryService`. It converts configured atomic cells into grid
  edges, cancels every edge shared by two cells in the same logical region, and marks which
  remaining edges already belong to the panel perimeter.
- Replaced the live `JoinRegion` result path with `CreateRegionSurface`.
- The live adapter resolves exact Rhino curves only for internal planned boundary segments, using
  the already-trimmed atomic cell edges so curved source geometry is preserved.
- `BrepFace.Split` now splits the original single panel face once with the complete internal
  boundary network. Each fragment is checked with existing sample-and-area coverage logic, and the
  only single face matching the exact region-cell set is returned.
- A region covering the complete grid duplicates the original single-face Brep directly. Multi-face
  sources, missing curves, failed splits, and ambiguous coverage fail during preparation before the
  Undo-wrapped creation phase.
- The region smoke's Rhino-host branch now asserts `Faces.Count == 1` for parent-linked `0A/1A`.

### Parent-reference dropdown order

- Isolated parent-reference ordering in `OrderParentReferenceCells`.
- Choices now sort by numeric `Column`, then `Row`, then label as a deterministic tie-breaker.
- A 2-by-4 grid therefore renders `0A, 0B, 0C, 0D, 1A, 1B, 1C, 1D`.

### Material Setup layer colors

- Added application-level parsing of persisted Material Setup `#RRGGBB` values into
  `PanelColorRgb` without WPF or RhinoCommon dependencies.
- `PCSpawnSrf` reads the document-associated workbook material catalog and injects the resulting
  case-insensitive color map into spawn planning.
- An exact configured material code overrides the legacy deterministic family palette. Materials
  absent from the catalog retain that palette for backward compatibility.
- An associated workbook that cannot be read or validated returns a diagnostic before geometry
  mutation; a document with no workbook association retains legacy fallback behavior.
- The existing live layer application already updates both new and existing layer colors from the
  region plan, so spawned material layers now match Material Setup exactly.

## Variance From Plan

- The user added Material Setup color alignment after the initial PLAN was written. The PLAN was
  revised with a dated record before that scope was implemented.
- RhinoCommon native geometry cannot initialize in the standalone `dotnet run` host, so face-count
  and exact-coverage execution reports a documented skip. The assertion is compiled and remains
  ready for Rhino-host execution.
- No package build, installation, or live Rhino mutation was performed because this request
  authorized source construction and tests, not deployment or Windows UI automation.

## Problems Found And Fixed During Construction

- The prior region implementation intentionally accepted internal seams and returned joined atomic
  Breps. That old acceptance was explicitly superseded; the spawned result now comes from the
  source face and configured boundary.
- Material layers previously used only a hard-coded derived palette even though Material Setup
  persisted exact colors. Spawn planning now accepts configured catalog colors while preserving a
  missing-entry fallback.
- The existing `260813_TEST_panel-cladding-material-catalog` executable stops at an unrelated
  incoming Save-signature assertion. Its catalog operations execute before that failure. A new
  isolated workbook create/write/read regression proves the requested persisted-color path without
  altering the unrelated dirty Save implementation.

## Test Record

Full command output and assertions are recorded in
`Project_Test/260820_TEST_pcspawnsrf-boundary-parent-order/RESULTS.md`.

Verified exit-0 groups:

- focused Debug and Release boundary/order/persisted-color smoke;
- existing region Debug and Release smoke;
- existing spawn Debug and Release smoke;
- Material Setup color-picker Debug and Release smoke;
- direct-interaction, full-track-collapse, surface-sync structural-grid, split-spawn-sync, and
  cladding-logic regressions in Debug;
- standalone PanelCladdingEditor RHP Debug and Release builds;
- serial solution Debug and Release builds;
- Debug and Release assembly-level plug-in identity validation.

All four recorded builds completed with 0 warnings and 0 errors. The final Release RHP SHA-256 is
`5082EB190039AE5E67E17EADB851C80834BA2C9F16F7455F9040A23A07ECBFAD`.

## Acceptance Criteria Alignment

- Shared parent-region divider is cancelled in pure boundary planning: passed.
- Panel-perimeter versus internal split segments are deterministic: passed.
- Complete parent region requires no internal split curves: passed.
- Partial parent region returns exactly one source-trimmed face: compiled Rhino-host assertion;
  standalone native execution skipped pending live Rhino verification.
- Exact coverage selection retains only configured region cells: compiled Rhino-host assertion;
  existing pure coverage planning regressions passed.
- Dropdown order is `0A-0D`, then `1A-1D`: passed in Debug and Release.
- Persisted `XX=#123456` yields spawn RGB `(18, 52, 86)`: passed through actual temporary workbook
  create/write/read and spawn planning in Debug and Release.
- Missing catalog item retains deterministic fallback: passed.
- Relevant regressions, direct RHP builds, solution builds, and GUID validation: passed.

## Rollback Verification

- Revert the boundary service and restore the former `JoinRegion` call to recover the previous
  seam-bearing joined-Brep behavior.
- Restore row-first sorting in `UpdateSelectionUi` to recover the prior dropdown sequence.
- Remove configured color injection from `PanelCladdingSpawnPlanningService` and
  `PanelCladdingSpawnSrfCommand` to restore fallback-only layer colors.
- No live objects, workbook files, installed packages, or registry entries were changed. Temporary
  workbook fixtures were created under a unique system-temp folder and deleted by the focused test.
- Successful runtime spawn remains one Rhino Undo entry; all new failures occur during preparation
  before any object is added.

## Current Remaining Items

- In a running Rhino session, use the focused TEST README to spawn `0A=XX, 1A=0A` from a single-face
  panel and confirm the result reports one Brep face with no former divider seam.
- Confirm the corresponding material layer displays the exact conspicuous color saved in Material
  Setup, then run `PCSyncSrf` and verify `1A=0A` remains reconstructable.
- The unrelated existing material-catalog Save-signature regression may be handled under its own
  confirmed construction scope if desired.

## Conclusion

The requested source changes are implemented and build-clean: PCSpawnSrf now plans a configured
region boundary and reconstructs a single source face, parent choices are column-first, and spawned
material layers consume exact persisted Material Setup colors. Pure, workbook, UI, planning,
regression, binary, and assembly-identity validation passed. Rhino-host visual/native acceptance is
the only remaining manual verification because UI automation was not authorized.
