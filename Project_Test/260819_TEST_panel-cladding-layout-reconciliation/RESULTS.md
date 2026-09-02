# Panel Cladding Layout Reconciliation TEST RESULTS

Date: 2026-08-19

## Focused behavior

Debug and Release both passed:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-layout-reconciliation\PanelCladdingLayoutReconciliationSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-layout-reconciliation\PanelCladdingLayoutReconciliationSmoke.csproj -c Release
```

Verified:

- ordered insertion remaps retained tracks and expands only the divided bay;
- partial inserted `CW_2.05` state is inferred from adjacent edited-surface ownership;
- retained full-height merged curves reindex from old to new track numbers without fixture-specific rules;
- the same reconciliation handles multiple H/V insertions and moved retained tracks;
- semantic `PCMatchSrf` collapses redundant source tracks and absorbs extra target tracks;
- a target missing a required divider segment is rejected before mutation;
- touched match targets remove canonical and legacy Signature keys;
- save/sync production sources contain no Signature user-text writer;
- production reconciliation contains no fixture state, coordinate, grid-size, or literal `INT_*` transition.

## Regression suites

The following Debug suites passed:

- `260805_TEST_panel-cladding-match`
- `260818_TEST_panel-cladding-match-topology-masks`
- `260818_TEST_panel-cladding-match-parent-cells`
- `260819_TEST_pcmatch-srf-logical-cells`
- `260819_TEST_pcmatch-srf-logical-cell-cleanup`
- `260819_TEST_pcmatch-srf-explicit-parent-cells`
- `260819_TEST_pcmatch-srf-repair-existing-targets`
- `260819_TEST_pcmatch-parent-fidelity`
- `260819_TEST_pcsync-srf-preserve-structural-grid`
- `260819_TEST_pcsync-srf-parent-persistence`
- `260819_TEST_panel-cladding-surface-sync-structural-grid`
- `260807_TEST_panel-cladding-surface-sync`
- `260818_TEST_panel-cladding-extrusion-sync`
- `260819_TEST_panel-cladding-curve-topology`
- `260819_TEST_panel-cladding-logic-persistence`
- `260819_TEST_panel-cladding-scoped-save`
- `260818_TEST_panel-cladding-create-command`
- `260807_TEST_panel-cladding-clear`
- `260819_TEST_panel-cladding-split-spawn-sync`
- `260805_TEST_panel-cladding-spawn`

The Rhino-native Brep subtest in the structural-grid suite was skipped outside a Rhino native host; its application and source-contract assertions passed.

## Solution builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore -m:1
dotnet build .\MCP_Rhino.sln -c Release --no-restore -m:1
```

Both passed with zero warnings and zero errors. Serial MSBuild was used because the solution invokes `PanelCladdingEditor` with two output modes; an initial parallel build raced over the shared reference-output directory.

## Package and identity

- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.58/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.58\PanelCladdingEditor.rhp`
- SHA-256: `4C082A6006A88812AA0C7560E039127CEA6B33511FC68460F41CAE6261FB7D72`
- PanelCladdingEditor plug-in ID: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`
- MCP_Rhino plug-in ID remains distinct: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`

Package build, assembly identity validation, registry-only install, install validation, and installed/package hash comparison all passed.

## Live status

`Untitled 1.3dm` supplied the read-only state-1/state-2 regression evidence. Production code does not reference that file or its values. Rhino was closed when `1.0.58` was installed, so the installed command rerun awaits the next Rhino restart. The original model was not modified during construction or testing.
