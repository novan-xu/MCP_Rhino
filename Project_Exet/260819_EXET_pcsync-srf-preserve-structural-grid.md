# PCSyncSrf Preserve Structural Grid Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_pcsync-srf-preserve-structural-grid.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.55/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.55\PanelCladdingEditor.rhp`
- Commit / PR: none created

## Execution result / actual scope

### Confirmed live/deployment state

- Rhino loaded the installed `1.0.54` RHP from `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.54\PanelCladdingEditor.rhp`; the repeated failure was not caused by a stale plug-in.
- The routed Rhino document was an unsaved blank document, so the affected panel was not available for direct live inspection.

### Saved BKT evidence

- Read the saved BKT model without modifying it.
- Panel `PID_BKT_N1_01_11` stores `V0=22.5` and parent references including `1A=0A`.
- No associated extrusion-curve object exists for that PID in the saved model.
- Its `0A` cladding Brep spans the complete panel width, so cladding-surface inference exposes no boundary at `V0`.
- The prior implementation therefore rebuilt a one-column grid and removed the `1*` cells before persistence.

### Scope-aware offset fix

- Added a Rhino-independent effective-offset resolver in `PanelCladdingSurfaceSyncPlanningService`.
- For `Surfaces` scope, existing panel offsets are preserved and new geometry-inferred offsets are unioned without tolerance duplicates.
- Existing offsets win when a new inferred boundary falls within the model-derived tolerance.
- For `Curves` scope, the raw curve-inferred offset set remains authoritative, preserving `PCSyncCrv` removal behavior.
- The live surface-sync repository now uses the effective offsets before key-set creation, cell geometry construction, surface coverage resolution, parent reconstruction, and panel persistence.
- Package version advanced from `1.0.54` to `1.0.55`.

## Deviations from plan

- No production or test-scope deviation.
- Live command validation remains pending because the current Rhino document is unsaved and contains no affected panel.
- Installation initially staged because Rhino was running with `1.0.54`. After the user closed Rhino, a verified residual windowless Rhino PID attached only to the previously observed unsaved blank document was terminated; its short-lived shutdown child then exited. The staged installer subsequently activated and validated `1.0.55`.

## Problems found and fixed during construction

- The previous correction protected the final cell-write graph but could not preserve a cell that had already disappeared from the inferred grid.
- The earlier structural-grid fix relied on an associated extrusion curve. The saved production panel demonstrates that a structural offset may exist without generated curve objects.
- Surface geometry alone is ambiguous: a missing cladding boundary can mean either a spanning cladding over a structural mullion or an obsolete track. Splitting surface and curve synchronization resolves that ambiguity—surface sync preserves structural tracks; curve sync controls their removal.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcsync-srf-preserve-structural-grid\PCSyncSrfPreserveStructuralGridSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcsync-srf-preserve-structural-grid\PCSyncSrfPreserveStructuralGridSmoke.csproj -c Release
```

Result: both exit 0.

Focused assertions:

- `PCSyncSrf` preserves stored `V0=22.5` when inferred vertical offsets are empty;
- new surface boundaries are added without duplicating existing tracks;
- `PCSyncCrv` may still remove stored offsets;
- the preserved two-column grid reconstructs `1A=0A`;
- the live repository uses the scope-aware effective offsets before grid creation.

Regression suites:

- `260819_TEST_pcsync-srf-parent-persistence`: passed.
- `260819_TEST_panel-cladding-surface-sync-structural-grid`: passed; Rhino-native Brep subtest skipped outside a Rhino host.
- `260807_TEST_panel-cladding-surface-sync`: passed.
- `260818_TEST_panel-cladding-extrusion-sync`: passed.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.55
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build and identity validation exit 0. PanelCladdingEditor declares manifest-matched plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct. Packaged RHP SHA-256: `1FD89973102971DEF470749C44319645C3E398A23D7E2D5C98AE97C8DE9FE61A`.

Installer result:

```text
PanelCladdingEditor 1.0.55 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.55-20260819160105765
PanelCladdingEditor 1.0.55 installed registry-only at C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.55.
PanelCladdingEditor 1.0.55 registry-only installation is valid.
```

The current-user registry points to the installed `1.0.55` RHP. Its SHA-256 is `1FD89973102971DEF470749C44319645C3E398A23D7E2D5C98AE97C8DE9FE61A`, matching the tested package.

## Acceptance alignment

- Preserve existing structural offsets in surface scope: passed.
- Recreate `1A=0A` without an associated curve: passed.
- Add new surface-derived boundaries: passed.
- Retain curve-scope removal behavior: passed.
- Surface, parent-persistence, and extrusion-sync regressions: passed.
- Debug/Release builds, package, and identity: passed.
- Current-user installation, registry validation, and installed-file hash: passed.
- Live rerun: pending Rhino restart and affected-model selection.

## Rollback verification

- Restore direct use of geometry-inferred offsets for surface scope and reinstall package `1.0.54`.
- The BKT model inspection was read-only; no Rhino or 3DM object was modified.

## Current remaining items

- Reopen Rhino and the affected model, then rerun `PCSyncSrf`.

## Conclusion

The remaining parent-cell deletion was caused upstream of persistence: surface-only geometry inference removed structural offsets when no generated curve existed. `PCSyncSrf` now preserves the existing structural grid while adding new surface boundaries, so spanning claddings retain cells such as `1A=0A`. Package `1.0.55` is installed and validated for the next Rhino launch.
