# Cladding type suspension verification

Executed 2026-09-30. PLAN and EXET use the matching `panel-cladding-type-suspension` name.

## Automated results

All twelve existing smoke suites passed in Debug with exit code 0:

| TEST folder | Coverage |
| --- | --- |
| 260804_TEST_standalone-panel-cladding-editor | Keys, layout, dormant identity/workbook utilities |
| 260807_TEST_panel-cladding-clear | Exact retired type aliases and unrelated-key preservation |
| 260807_TEST_panel-cladding-surface-sync | Cells/logic without generated types or signatures; no-change sync |
| 260813_TEST_panel-cladding-material-catalog | Material catalog and save without a type; removed UI section |
| 260818_TEST_panel-cladding-topology-persistence | Deleted boundaries, merged runs, save/reload |
| 260819_TEST_panel-cladding-hide-mask | Hidden segment persistence and editor behavior |
| 260819_TEST_panel-cladding-logic-persistence | Material-independent owner graph across commands |
| 260819_TEST_panel-cladding-scoped-save | All save scopes remove four aliases, no type result, frame preview retained |
| 260819_TEST_panel-cladding-curve-topology | Independent curve topology and matching |
| 260819_TEST_pcsync-srf-parent-persistence | Exact geometry-derived owner graph and postconditions |
| 260820_TEST_panel-cladding-sparse-topology | Default mask omission and sync persistence |
| 260903_TEST_panel-cladding-update-command | Update behavior and command registration |

Run each existing `.csproj` with `dotnet run --project <project> -c Debug --no-restore`.
Console output is retained locally in ignored logs in this folder. Tests that explicitly
exercise the old identity utility remain valid compatibility tests; active save/sync
does not call that utility.

The first scoped-save attempt exposed an existing stale fixture call missing the
spawn planner's source layer parameter; passing the fixture's actual layer fixed it.
The material-catalog UI assertion was updated to expect the type section's removal.
Both suites passed after these corrections.

## Build and package

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --no-restore --nologo
./Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -OutputRoot "$PWD/.tmp-package-panel-cladding-type-suspension-260930"
```

Debug build, Release publish, and Release direct-RHP rebuild succeeded with zero
warnings/errors. The package's assembly metadata passed
`Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1`
with `-Configuration Release -SkipBuild -PluginPath` pointing to the existing MCP RHP
and newly packaged PanelCladdingEditor RHP. Both assembly GUIDs were nonempty,
manifest-matching, and distinct. The PanelCladdingEditor ID is
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.

Refreshed staged version 1.0.78 at
`%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.78-key-renumbering-260930`.
All 27 manifest file hashes verified, with no extra files beyond the bundle manifest.
RHP SHA256: `d5094c99f48eb45a4982950aa6b3e7497c2a5a14b978fe2a0ac79782fdb17f08`.
The installed files and registry were untouched. This is stage verification, not
installation validation or a live test of the new RHP.

## Live document cleanup

The Router selected the user's exact requested open BKT wireframe document.
Existing attribute tools previewed and applied one remove-only recipe covering the
four exact current/former cladding type keys. Fresh preflight found 850 current keys
and no former aliases. Apply updated all 850 objects with zero failures.

Readback resolved every affected object and compared all other key/value pairs and
returned object metadata. Result: 850 entries removed, 30,363 unrelated attributes
preserved, zero unexpected differences, zero remaining type keys, and 7,822 total
objects before/after. See `live-cleanup-summary.json` for the machine-readable counts.
The activity log's initial preserved-attribute count typo has an explicit correction.

The mutation is in Rhino memory with tool-provided Undo. No disk `.3dm` access,
document save, or Windows UI automation was performed. Undo was not exercised on the
user's document because that would reverse the requested cleanup.

## Activation after the user closed Rhino

Completed 2026-09-30. See `activation-summary.json` and the matching EXET follow-up.
The staged GUID and 27 bundle hashes were revalidated. A Windows-service-launched
PowerShell process proved host-persistent registry access with a temporary nonce,
then used the existing product installer in Install and Validate modes. A second
host process validated again after the installer exited. All eleven commands,
the exact existing 1.0.78 RHP hash/path, and all three advancing registry timestamps
passed. The prior RHP remains in the installer's rollback directory.

`Host-PanelCladdingActivation.ps1` records Probe, Snapshot, Install, or Validate
results to an explicit output path. During this run it was launched hidden using
`Win32_Process.Create`, after confirming host nonce visibility through the WMI
registry provider under the current user's HKEY_USERS SID. This distinction matters:
ordinary agent subprocesses read a stale virtualized registry view and cannot attest
production activation. Do not run Install from such a context.

Raw preflight/install/independent-validation snapshots are ignored `.log` files;
the prior registry export is an ignored `.reg` file. Temporary nonce keys were removed.
No Windows UI automation was used. Rhino remains closed, so loaded-module and
post-start timestamp validation are pending the next Rhino startup.
