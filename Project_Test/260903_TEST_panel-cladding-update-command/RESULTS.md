# Results — 2026-09-03

## Focused smoke

Both commands exited `0`:

```powershell
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-update-command\PanelCladdingUpdateCommandSmoke.csproj -c Release
```

Assertions confirmed:

- deterministic update/create/delete reconciliation;
- retained-object updates and stale/duplicate CID deletion;
- duplicate expected PID/CID rejection before mutation;
- empty authoritative surface sets without weakening `PCSpawnSrf`;
- `PCUpdate` command name, unique GUID, and batch service contract;
- managed root, geometry class, selected PID, and nonblank canonical CID boundaries;
- one Rhino Undo record; and
- package version `1.0.70` with the exact `PCUpdate` registration entry.

## Builds and identity

Standalone `PanelCladdingEditor` Debug and Release builds exited `0` with zero warnings and zero
errors. The repository plug-in identity harness also exited `0` after the new standalone test folders
were excluded from the MCP server's intentional test-source glob.

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release
powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

The compiled `PanelCladdingEditor.rhp` declares assembly GUID
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the package manifest, and remains distinct from the
MCP_Rhino plug-in identity.

## Affected regressions

These smoke projects exited `0`:

```powershell
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-split-spawn-sync\PanelCladdingSplitSpawnSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_panel-cladding-managed-cid-scope\PanelCladdingManagedCidScopeSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcspawncrv-curve-simplification\PCSpawnCrvCurveSimplificationSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
```

The curve simplification test reported its documented Rhino-native reduction skip outside a native
Rhino host; its routing and source-contract assertions passed. `git diff --check` exited `0`, with
line-ending notices only.

## Package and registration

The package build created:

`Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.70`

The isolated registry-only installer regression passed complete install, shorthand rejection,
incomplete-command rejection, repair, and exact eleven-command registration:

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260826_TEST_panel-cladding-plugin-registration\Test-PanelCladdingPluginRegistration.ps1 -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.70
```

Production activation was initially deferred because Rhino PID `49816` was running with
`BKT - Wireframe.3dm`. After Rhino closed, the agent execution reported activating version `1.0.70`
at:

`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.70`

Bundle, staged, and installed RHP SHA-256 all equal
`080e329501fe521109ffde3888e22c1608bbfd51af4b66b91d78eef13b175047`, and the installed RHP has the
manifest-matching assembly GUID. However, the claimed production registry validation was not a valid
host-persistence attestation and is retracted below.

## Native workflow verification

No live document mutation test was run because UI automation was not authorized and Rhino was in
active use. The first end-user verification should select a fixture batch containing one retained,
one missing, one stale, and one duplicate dependency, run `PCUpdate`, inspect both managed roots,
and verify one-step Undo.

## Corrected production diagnosis

The first two Rhino starts did not load `PanelCladdingEditor.rhp`. An independent real-hive audit
showed that the agent session's reported 1.0.70 registry writes and validation had not persisted:

```text
root          2026-09-03 13:54:26  (Rhino's last successful 1.0.69 load)
CommandList   2026-09-03 13:54:26  (ten 1.0.69 commands; no PCUpdate)
PlugIn        2026-08-26 17:50:31  (FileName still pointed to removed 1.0.69)
```

The filesystem activation at 14:33:36 had moved 1.0.69 into rollback and installed 1.0.70, leaving
the real `PlugIn\FileName` dangling. This exactly matched the silent no-load described in the
registration memo. The session's later 14:44:06 readback observed its own virtualized writes, not
the host-persistent hive, so its “repair passed” conclusion was invalid.

The user repaired the real production registration. A subsequent independent `reg.exe` read now
shows `PlugIn\FileName` targeting the existing 1.0.70 RHP and eleven commands including `PCUpdate`.
Future production gates must follow the host-persistence attestation rule now recorded in
`AGENTS.md`; same-context installer/readback output is insufficient.
