# PCSyncSrf Offset Refresh EXET

## Corresponding plan

- Plan: `Project_Plan/260825_PLAN_pcsync-srf-offset-refresh.md`
- Execution date: 2026-08-25

## Related artifacts

- Tests: `Project_Test/260825_TEST_pcsync-srf-offset-refresh/`
- Test record: `Project_Test/260825_TEST_pcsync-srf-offset-refresh/RESULTS.md`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.63/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.63\PanelCladdingEditor.rhp`
- Commit / PR: not created in this execution.

## Execution result / actual delivered scope

- Confirmed the offset commit writer deletes every existing canonical H/V offset key before writing the planned indexed sequence. The reported duplicate set did not come from stale user-text keys.
- Traced the defect to surface-scope planning, which treated the full stored offset sequence as structural and unioned it with current surface geometry.
- Added a production `ResolveSurfaceScopeOffsets` overload that accepts the existing `PanelCladdingLayout` so reconciliation can use the saved material-independent owner graph.
- Classifies a stored H/V track as non-splitting only when every adjacent cell pair across the full track resolves to the same cladding owner.
- Makes current surface-inferred positions authoritative for splitting cladding boundaries.
- Reconciles only proven non-splitting tracks with curve geometry. Curve positions aligned to old splitting boundaries are suppressed, preventing stale curves from reintroducing moved surface offsets.
- Preserves proven non-splitting stored tracks when geometry does not expose them, refreshes them from aligned curves when available, and accepts genuinely unmatched new curve tracks.
- Switched the live `PCSyncSrf` repository call to the layout-aware planner. `PCSyncCrv` remains unchanged.

## Differences from the plan

No implementation deviation. Package manifests and MCP server surfaces were not changed. Packaging and installation were initially outside the execution, then completed after the user confirmed Rhino was closed; Windows retained one background Rhino process briefly, so activation waited until it exited rather than terminating it or overwriting a loaded RHP.

## Problems found and fixed during construction

- The visible five-value sequence (`19.625, 21.375, 105.75, 107.25, 169.09079`) exactly matched the sorted union produced by `CombineSurfaceScopeOffsets`; this disproved an initial possibility that old indexed keys were merely left undeleted.
- Associated extrusion curves can still remain at old cladding-boundary locations after surfaces are edited. Replacing stored values from surfaces alone was insufficient because the curve-union path could re-add the same stale values. Track-role classification is therefore applied before curve reconciliation.
- The earlier structural-grid preservation contract had to remain intact for a cladding surface spanning a mullion. The new logic preserves that case using the saved owner graph instead of preserving every stored track indiscriminately.

## Test record

Focused Debug and Release commands:

```powershell
dotnet run --project Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj -c Debug
dotnet run --project Project_Test/260825_TEST_pcsync-srf-offset-refresh/PCSyncSrfOffsetRefreshSmoke.csproj -c Release
```

Both exited `0`. The exact reported regression produced only `19.625, 105.75, 169.09079`; assertions reject survival of `21.375` or `107.25` under any index.

Existing Debug regression projects, all exit `0`:

```text
Project_Test/260819_TEST_pcsync-srf-preserve-structural-grid/PCSyncSrfPreserveStructuralGridSmoke.csproj
Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/PanelCladdingSurfaceSyncStructuralGridSmoke.csproj
Project_Test/260819_TEST_pcsync-srf-parent-persistence/PCSyncSrfParentPersistenceSmoke.csproj
Project_Test/260819_TEST_panel-cladding-layout-reconciliation/PanelCladdingLayoutReconciliationSmoke.csproj
Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj
Project_Test/260812_TEST_panel-cladding-offset-sync/PanelCladdingOffsetSyncSmoke.csproj
```

The existing Rhino-native Brep inference checks skipped outside a Rhino native host. Managed regressions for offset composition, owner persistence, reconciliation, orchestration, workbook handling, and command contracts all passed.

Build commands:

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release
```

Both exited `0` with zero warnings and zero errors. Project-level validation was used because this change affects only the standalone `PanelCladdingEditor` RHP and does not change the MCP server, Router, registration, route lifecycle, or document-session routing.

Package, identity, install, and validation commands:

```powershell
powershell -ExecutionPolicy Bypass -File Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -Version 1.0.63
Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.63/Installer/Install-PanelCladdingEditor.ps1 -Mode Install
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.63/Installer/Install-PanelCladdingEditor.ps1 -Mode Validate
```

All completed successfully. PanelCladdingEditor declares assembly GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, distinct from MCP_Rhino. Packaged and installed RHP SHA-256 both equal `B25B543CB8E0FFABA28B58B727E9F07908F0FF7C8063F0AB047B8C746E186C52`. Registry validation confirms the exact installed RHP path with `LoadMode=1` and `DirectoryInstall=0`.

## Acceptance criteria alignment

- Exact old/new Level 5 values refresh without appended duplicates: passed.
- Obsolete `21.375` and `107.25` absent from result: passed.
- Non-splitting structural track preserved without curve/surface evidence: passed.
- Structural track follows current curve: passed.
- Stale curves at old splitting boundaries are suppressed: passed in the exact numeric regression.
- Genuinely unmatched new curve track retained: passed.
- Existing structural-grid, parent-persistence, reconciliation, surface-sync, and offset-sync regressions: passed, subject to the documented native-host skips.
- Debug and Release standalone plugin builds: passed.
- Package assembly identity, current-user installation, installed hashes, and registry ownership: passed.

## Rollback verification

The implementation is isolated to one Application planner and one live call site. Reverting the layout-aware overload/call restores the prior all-stored-offset union behavior. Runtime panel mutations remain covered by the existing single Rhino Undo record. The installer retained its ownership-aware rollback record while replacing the prior same-version active root; no Rhino document was opened or mutated during deployment.

## Current remaining items

- Native live verification against the user's Rhino model was not run because this execution did not use Windows UI automation and no Rhino-native test host was invoked.

## Conclusion

The planning defect is fixed, regression-covered, packaged, identity-verified, installed, and registry-validated as version `1.0.63`. `PCSyncSrf` now refreshes moved cladding-boundary H/V offsets instead of expanding the indexed grid, while retaining the earlier protection for proven non-splitting structural tracks. The corrected plug-in is ready for the next Rhino launch.
