# Panel Cladding Scoped Save Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-scoped-save.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-scoped-save/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.50/`
- Latest staged installation: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.50-20260819150655252`
- Commit / PR: none created

## Execution result / actual scope

### Explicit extrusion configuration

- Added `PanelCladdingKeyService.HasExplicitTopologyMasks` as the canonical test for a nonblank segment-mask and merge-mask pair.
- A panel with neither mask, only one mask, or a blank mask remains extrusion-unconfigured even though topology parsing still renders it as the complete default lattice in the editor.
- `PCSpawnCrv` now checks this explicit state before parsing/planning geometry and fails with `PANEL_CLADDING_EXTRUSION_TOPOLOGY_NOT_CONFIGURED` plus instructions to use `Save Extrusions` or `Save Both`.
- `PCSpawnSrf` remains independent and does not require topology masks.
- The application-level combined curve planner has the same fail-closed guard so future callers cannot bypass the live-command rule.

### Scoped persistence

- Added `PanelCladdingSaveScope` with `Extrusions`, `Cladding`, and backward-compatible default `Both` values.
- `Extrusions` writes canonical H/V offsets, unit dimensions, segment mask, and merge mask. It removes only obsolete offset/topology keys and never modifies cladding cells or type/signature attributes.
- `Cladding` writes surviving logical cell assignments and type/signature attributes. It reads the persisted live extrusion layout and never writes or deletes offsets, unit dimensions, or topology masks; absent masks therefore remain absent.
- `Both` performs both groups in one Rhino attribute transaction and retains the prior complete-save behavior.
- Explicit extrusion saves encode the complete default lattice as a valid mask pair, satisfying the requirement that pressing the extrusion save action intentionally configures the panel.

### Editor footer and state

- Removed the footer `Exit` button.
- Added equal-width `Save Extrusions`, `Save Cladding`, and `Save Both` buttons.
- Split dirty tracking into extrusion and cladding state. Structural edits mark both because a grid/topology change also changes the logical cladding-cell set; material/parent edits mark cladding only.
- A partial save clears only its owned dirty state, refreshes the live source fingerprint, and preserves unsaved work in the other scope.
- Cladding-only save is blocked while structural edits remain unsaved; users can save extrusions first or use `Save Both`, preventing a cladding identity from being committed against an unpersisted grid.
- Combined save retains the full reload behavior.

## Deviations from plan

- No behavioral deviation from the approved plan.
- Final installation is staged rather than installed because a Rhino process remains active.

## Problems found and fixed during construction

- The solution-level server project intentionally globs test sources and requires every standalone panel-cladding smoke directory to be excluded. The new focused test was added to that exclusion list after the first solution build exposed the omission.
- The direct-interactions regression still asserted the retired two-button `Exit / Save` footer. It was updated to require the three equal-width scoped actions and absence of `Exit`.
- The package was rebuilt after the final application refinement and staged again; `1.0.50-20260819150655252` is the latest/current staged bundle.
- An install attempt was guarded by a fresh Rhino-process check and stopped before mutation when Rhino was still active.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Release
```

Result: both exit 0.

Focused coverage:

- Missing/blank mask pairs are not explicit configurations.
- Curve planning rejects an unconfigured panel and accepts a valid explicit default pair.
- Cladding-only save leaves masks absent and does not touch extrusion attributes.
- Extrusion-only save writes both default masks and does not touch cladding attributes.
- Save Both contains both attribute groups.
- The editor exposes the three requested buttons, no Exit action, and `Save Extrusions` configures a default topology.

Sequential Release regressions:

- `260818_TEST_panel-cladding-topology-persistence`
- `260819_TEST_panel-cladding-blank-cell-persistence`
- `260819_TEST_panel-cladding-logical-cell-cleanup`
- `260819_TEST_panel-cladding-full-track-collapse`
- `260805_TEST_panel-cladding-spawn`
- `260819_TEST_panel-cladding-split-spawn-sync`
- `260813_TEST_panel-cladding-direct-interactions`
- `260819_TEST_panel-cladding-curve-topology`
- `260818_TEST_panel-cladding-pc-commands`
- `260804_TEST_standalone-panel-cladding-editor`

Result: all exit 0.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.50
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build exits 0. Debug and packaged Release PanelCladdingEditor RHPs declare plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct. Final packaged RHP SHA-256 is `F6B7E4AD9AB427B7E2F1618EE8F93C7F0F8A39144C894ADF8B6BF1B25CF41A71`.

Installer result:

```text
PanelCladdingEditor 1.0.50 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.50-20260819150655252
```

## Acceptance alignment

- Implicit default topology does not create masks on cladding-only save: passed.
- Explicit extrusion/default save writes both masks: passed.
- PCSpawnCrv rejects absent/blank masks before curve generation: passed.
- Extrusion and cladding persistence ownership remains isolated: passed.
- Save Both retains complete transaction semantics: passed.
- No Exit button and three equal-width requested actions: passed.
- Partial dirty state and live fingerprint refresh: passed by editor smoke.
- Existing topology, logical-cell, spawn, command, and UI behavior: regressions pass.
- Debug/Release builds, package, and RHP identity: passed.
- Current-user install/registry validation: pending Rhino shutdown.

## Rollback verification

- Revert the save-scope contract, explicit-mask guard, footer/state split, and manifest version, then reinstall the prior package artifact.
- No Rhino document was opened or modified by automated tests in this execution.

## Current remaining items

- Close every Rhino window.
- Run the staged `1.0.50-20260819150655252` installer, then validate the installed RHP and HKCU Rhino registration.
- Live-check the three footer actions and confirm `PCSpawnCrv` rejects a panel until `Save Extrusions` or `Save Both` has been used.

## Conclusion

Missing mask keys now unambiguously mean “extrusions not configured,” `PCSpawnCrv` fails closed on that state, and the editor provides independent extrusion, cladding, and combined persistence. Code, tests, package, and identity verification are complete; deployment is staged pending Rhino shutdown.
