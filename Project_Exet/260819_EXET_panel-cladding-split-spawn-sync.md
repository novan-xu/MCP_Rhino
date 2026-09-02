# Panel Cladding Split Spawn and Sync Commands Execution

## Corresponding Plan

- Plan: `Project_Plan/260819_PLAN_panel-cladding-split-spawn-sync.md`
- Execution date: 2026-08-19

## Related Artifacts

- Focused regression: `Project_Test/260819_TEST_panel-cladding-split-spawn-sync/`
- Updated regressions:
  - `Project_Test/260805_TEST_panel-cladding-spawn/`
  - `Project_Test/260807_TEST_panel-cladding-surface-sync/`
  - `Project_Test/260818_TEST_panel-cladding-extrusion-sync/`
  - `Project_Test/260818_TEST_panel-cladding-pc-commands/`
  - `Project_Test/260818_TEST_panel-cladding-hide-smoke-command/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.42/`
- Installed plug-in: `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.42\PanelCladdingEditor.rhp`
- Commit / PR: not created in this execution.

## Execution Result / Actual Scope

- Replaced the combined Rhino command types with four explicit commands:
  `PCSpawnSrf`, `PCSpawnCrv`, `PCSyncSrf`, and `PCSyncCrv`.
- Added `PanelCladdingObjectScope` to spawn and sync contracts and propagated the scope through
  snapshots, plans, commit requests, and results.
- Surface spawn prepares and creates only material-region Breps. Curve spawn uses the extrusion
  planner directly and therefore succeeds when every cladding cell is blank.
- Surface sync discovers only supported material-surface Breps, reconstructs cell assignments, and
  emits no curve writes. Curve sync discovers only extrusion-layer curves, reconstructs offsets and
  topology, and emits no surface writes.
- Both scoped sync paths retain shared panel layout/type/unit-dimension updates and Rhino Undo
  transaction behavior.
- Package documentation and command-count regressions now describe exactly eight production
  commands. Package version advanced from 1.0.41 to 1.0.42.

## Deviations From Plan

- No implementation-scope deviation.
- Rhino was running during the first installation attempt, so the production installer staged
  1.0.42. After Rhino was closed, the staged installer activated and validated 1.0.42.

## Problems Found and Fixed During Construction

- The first compile found the former combined command calling the old four-argument sync contract;
  replacing it with the two scoped command adapters resolved the mismatch.
- Curve-scope planning initially reused a later local variable name and failed compilation; the
  scoped CID variable was renamed.
- Curve-only offset inference exposed a nullable surface-list enumeration; the geometry service now
  treats an absent opposite-family collection as empty.
- A parallel solution build raced the test-host DLL against the production RHP in their shared
  output directory. Serial solution builds (`-m:1`) passed in both configurations.
- The older extrusion-visuals test had a pre-existing zero-byte generated `.exe` launcher. Its
  compiled DLL executed directly and all assertions passed; this did not affect package output.

## Test Record

- `dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug`
  - Exit 0; zero warnings and zero errors.
- `dotnet build MCP_Rhino.sln -c Debug -m:1`
  - Exit 0; zero warnings and zero errors.
- All panel-cladding Debug smoke projects were executed. All assertions passed; two geometry probes
  reported their documented native-Rhino-host skips.
- `dotnet Project_Test/260813_TEST_panel-cladding-extrusion-visuals/bin/Debug/net8.0-windows/PanelCladdingExtrusionVisualsSmoke.dll`
  - Exit 0; five visual/interaction assertion groups passed.
- `dotnet build MCP_Rhino.sln -c Release -m:1`
  - Exit 0; zero warnings and zero errors.
- Release runs passed for extrusion sync, hidden-smoke-command, PC command surface, and the focused
  split-spawn/sync regression.
- Focused output:
  - combined commands absent and four scoped commands registered;
  - curve spawn planning succeeds with blank cladding assignments;
  - explicit surface/curve scope contract present.
- `Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -Version 1.0.42`
  - Exit 0; direct RHP package produced without a duplicate plug-in DLL.
- `Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release`
  - Exit 0; PanelCladdingEditor plug-in id is
    `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, non-empty, manifest-matched, and distinct.
- Packaged RHP SHA-256:
  `0B1023236856F58B05BFB08DBAE2BBA5CCB0F14AD23F3C3F54050DCC7CB13BAF`.

## Acceptance Alignment

- Exactly eight production Rhino commands: passed.
- Combined `PCSpawn` and `PCSync` commands absent: passed.
- Surface spawn and sync do not create/repair curves: passed by scoped planning and command tests.
- Curve spawn and sync do not require/create/repair surfaces: passed by blank-cell and scoped-write
  tests.
- PID/CID/CRV naming, topology masks, layer contracts, unit dimensions, Undo and rollback machinery:
  retained and covered by existing plus focused regressions.
- Debug and Release solution/package/identity validation: passed.

## Rollback Verification

- No destructive migration was added. Rhino Undo remains the mutation rollback mechanism.
- The installed 1.0.41 plug-in was not overwritten while Rhino was running. On activation it was
  moved to the installer-owned rollback area, and the registry now points only to 1.0.42.

## Current Remaining Item

- Restart Rhino so it loads 1.0.42 and registers the four scoped command names in the live session.

## Conclusion

The command and service split is implemented, isolated by object family, regression-tested, built,
identity-validated, packaged, installed, and registry-validated as version 1.0.42.
