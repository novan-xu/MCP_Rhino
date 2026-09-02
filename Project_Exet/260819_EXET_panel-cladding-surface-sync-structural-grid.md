# Panel Cladding Surface Sync Structural Grid Execution Report

## Corresponding plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-surface-sync-structural-grid.md`
- Execution date: 2026-08-19

## Related artifacts

- Tests: `Project_Test/260819_TEST_panel-cladding-surface-sync-structural-grid/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.51/`
- Staged installation: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.51-20260819151930009`
- Commit / PR: none created

## Execution result / actual scope

### Structural-grid reconstruction

- Changed `PCSyncSrf` live inference to pass both geometrically associated cladding Breps and geometrically associated extrusion curves to the existing offset-inference service.
- Structural curves now preserve H/V tracks such as `V0` even when a single cladding Brep crosses the curve and therefore has no naked edge at that location.
- `PCSyncCrv` remains curve-only because its Brep argument is still empty; its topology reconstruction behavior is unchanged.

### Cladding ownership reconstruction

- Kept surface coverage as the authority for cladding ownership.
- A single surface covering `0A` and `1A` remains one cladding region. The planner writes the material on owner `0A` and writes parent reference `1A=0A`.
- The one surface retains an owner-`0A` CID, so subsequent `PCSpawnSrf` recreates one merged cladding surface rather than two surfaces split by the structural mullion.
- Existing topology masks are preserved when the combined structural-grid dimensions match the stored panel layout.

## Deviations from plan

- No production-behavior deviation.
- RhinoCommon Brep execution is unavailable to the standalone test host without Rhino native libraries, so the focused test reports that branch as skipped. The test still validates the live adapter source contract and executes the pure application planner end-to-end. Existing Rhino-hosted geometry infrastructure is unchanged; only its already-supported surfaces-plus-curves overload is now used.
- Installation is staged instead of activated because a Rhino process remains open.

## Test record

Focused Debug and Release smoke:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-surface-sync-structural-grid\PanelCladdingSurfaceSyncStructuralGridSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-surface-sync-structural-grid\PanelCladdingSurfaceSyncStructuralGridSmoke.csproj -c Release
```

Result: both exit 0.

Validated:

- the live `PCSyncSrf` inference call includes associated curve geometry in surface scope;
- the structural layout retains `V0`;
- one spanning surface reconstructs `0A=MPL-001` and `1A=0A`;
- the region remains one surface with owner-cell CID `0A`.

Sequential Debug regressions:

- `260807_TEST_panel-cladding-surface-sync`
- `260812_TEST_panel-cladding-offset-sync`
- `260819_TEST_panel-cladding-split-spawn-sync`
- `260819_TEST_panel-cladding-full-track-collapse`
- `260819_TEST_panel-cladding-curve-topology`

Result: all exit 0.

Solution validation:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore
dotnet build .\MCP_Rhino.sln -c Release --no-restore
```

Result: both exit 0 with 0 warnings and 0 errors.

Package and identity validation:

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.51
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -PluginPath <MCP-RHP>,<packaged-PanelCladdingEditor-RHP>
```

Result: package build and identity validation exit 0. PanelCladdingEditor declares plug-in id `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; MCP_Rhino remains distinct. Packaged RHP SHA-256: `BB4C31C7EFE630B584B71D19A0E97431B7F2B69196DC5FC188DA7D4E4FAECF6A`.

Installer result:

```text
PanelCladdingEditor 1.0.51 was staged because Rhino is running.
C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.51-20260819151930009
```

## Acceptance alignment

- Structural center mullion preserves `V0`: passed by adapter contract; native standalone execution pending Rhino host.
- Spanning cladding restores parent cells such as `1A=0A`: passed.
- Spanning cladding stays one logical/spawn region: passed.
- PCSyncCrv remains isolated: regressions pass.
- Existing surface, offset, split/spawn, collapse, and curve-topology behavior: regressions pass.
- Debug/Release builds, package, and RHP identity: passed.
- Current-user activation: pending Rhino shutdown.

## Rollback verification

- Restore the curve argument in surface scope to an empty array and reinstall package `1.0.50`.
- No Rhino document was opened or modified by automated tests in this execution.

## Current remaining items

- Close every Rhino window.
- Run the staged `1.0.51-20260819151930009` installer.
- Live-check a panel with one cladding Brep crossing a retained structural mullion and confirm `V0` plus `1A=0A` after `PCSyncSrf`.

## Conclusion

`PCSyncSrf` now reconstructs the panel's structural grid from associated curves while independently reconstructing cladding ownership from surface coverage. This preserves structural offsets and correctly restores parent-cell values for merged cladding regions. Code, focused tests, regressions, builds, package, and identity validation are complete; activation is staged pending Rhino shutdown.
