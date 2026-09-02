# PCMatchSrf Explicit Parent Cells Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_pcmatch-srf-explicit-parent-cells.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_pcmatch-srf-explicit-parent-cells/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.52/`
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.52\PanelCladdingEditor.rhp`
- Commit / PR: none created

## Execution result / actual scope

### Live evidence

- Inspected the saved open `BKT - Wireframe.3dm` through the selected live Rhino route without changing document state.
- Source `PID_BKT_N1_01_07` contains eight explicit cladding assignments:
  - `0A=MPL-001`, `0B=MPL-001`, `0C=MPL-002`, `0D=MPL-001`;
  - `1A=0A`, `1B=0B`, `1C=0C`, `1D=0D`.
- Target `PID_BKT_N1_01_05` contains only the four owner assignments after the reported match; all four `1*` parent keys are absent.
- The active installed plug-in is version `1.0.47`, while later packages had remained staged.

### Match projection fix

- Retained the topology-derived logical-cell collapse used to omit genuinely absent hidden physical cells.
- Added an explicit-source-cell overlay after collapse. Every cladding key actually stored on the source and belonging to the parsed grid is now included in PCMatchSrf writes.
- Explicit parent values remain parent tokens after storage encoding; they are not resolved to material.
- A topology-hidden cell absent from source user text remains omitted, preserving the prior stale-key cleanup behavior.
- Target deletes remain limited to cladding-cell keys. Target offsets, topology masks, type/signature, identity, and unrelated metadata remain untouched.
- Package version advanced from `1.0.51` to `1.0.52`.

## Deviations from plan

- No production-behavior deviation.
- The initial installation attempt staged the package because Rhino was open with `1.0.47`. After the user closed Rhino, the staged installer activated and validated `1.0.52`.

## Problems found and fixed during construction

- Existing parent-fidelity tests covered 2x2 panels and ordinary topology. The new test reproduces the reported 2-column by 4-row graph and separately exercises an explicit parent on a topology-collapsed cell.
- The deployed-version check established that successful repository tests alone were insufficient for this report: the live process had not loaded any of the later staged packages. The new package is explicitly recorded as staged and not represented as active.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-explicit-parent-cells\PCMatchSrfExplicitParentCellsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_pcmatch-srf-explicit-parent-cells\PCMatchSrfExplicitParentCellsSmoke.csproj -c Release
```

Result: both exit 0.

Focused assertions:

- the reported 2x4 graph produces all eight writes, including `1A=0A` through `1D=0D`;
- an explicit parent survives topology collapse;
- an absent topology-hidden source cell is not synthesized;
- target-owned non-cell attributes remain unchanged.

Sequential Debug regressions:

- `260819_TEST_pcmatch-srf-logical-cells`
- `260819_TEST_pcmatch-srf-logical-cell-cleanup`
- `260819_TEST_pcmatch-parent-fidelity`
- `260818_TEST_panel-cladding-match-parent-cells`
- `260805_TEST_panel-cladding-match`
- `260818_TEST_panel-cladding-match-topology-masks`

Result: all exit 0.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.52
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build and identity validation exit 0. PanelCladdingEditor declares manifest-matched plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct. Packaged RHP SHA-256: `CE25B38ACBFB0A359547D05BAA6D32D35D4349803FFA11DCAB61694B01D4D36F`.

Installer result:

```text
PanelCladdingEditor 1.0.52 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.52-20260819152709101
PanelCladdingEditor 1.0.52 installed registry-only at C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.52.
PanelCladdingEditor 1.0.52 registry-only installation is valid.
```

## Acceptance alignment

- Reported `1A=0A` through `1D=0D` transfer: passed.
- Explicit parent values cannot be discarded by topology collapse: passed.
- Absent hidden cells remain omitted: passed.
- Target-owned non-cell attributes remain untouched: passed.
- Existing logical-cell cleanup and parent fidelity: regressions pass.
- Debug/Release builds, package, and plug-in identity: passed.
- Current-user activation and registry/hash validation: passed.
- Live command rerun: pending Rhino restart.

## Rollback verification

- Remove the explicit-source-cell overlay and restore package version `1.0.51`.
- The live diagnostic was read-only; no Rhino object or document attribute was modified.

## Current remaining items

- Reopen Rhino and rerun `PCMatchSrf` from `PID_BKT_N1_01_07` to a cleared compatible target.

## Conclusion

PCMatchSrf now treats every explicitly stored source cladding cell as configuration truth, including parent references, while preserving the prior omission of hidden cells that are genuinely absent. The reported 2x4 graph is covered directly. Code, tests, package, identity, installation, registry, and file-hash validation are complete.
