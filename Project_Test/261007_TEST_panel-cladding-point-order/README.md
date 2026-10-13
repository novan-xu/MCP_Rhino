# Panel command point-order tests — 2026-10-07

## Requirements reference

Matches the current repository StandardFourPointSurfaceRebuildSkill: local
gravity frame, lower-left anchor, clockwise four-point reconstruction, explicit
Brep front/back flip, then the current default SwapUV. The historical combined
FlipNormal/SwapUV wording was superseded by the separate front/back operation.
This standalone implementation does not add an MCP_Rhino runtime dependency.

## Pure tests and regressions

```powershell
dotnet run --project Project_Test/261007_TEST_panel-cladding-point-order/PanelCladdingPointOrderSmoke.csproj -c Debug
dotnet run --project Project_Test/261007_TEST_panel-cladding-point-order/PanelCladdingPointOrderSmoke.csproj -c Release
```

Both passed (exit 0): 480 cases per run cover all 24 input permutations, five
azimuths, vertical/sloped geometry, opposite front normals and large translation.
Tests also reject horizontal gravity degeneracy, ambiguous diamond anchors,
nonquads, nonplanar corners and concave quads.

The following existing projects also passed with `dotnet run --project <path> -c
<configuration>`:

- `261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj`: Debug/Release.
- `261007_TEST_panel-cladding-pid-layer-scope/PanelCladdingPidLayerScopeSmoke.csproj`: Debug/Release.
- `261007_TEST_panel-cladding-pid-setup-keys/PanelCladdingPidSetupKeysSmoke.csproj`: Debug/Release.
- `260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj`: Debug/Release.
- `260903_TEST_pcupdate-command-undo/PCUpdateCommandUndoSmoke.csproj`: Debug/Release; native branch explicitly skipped.
- `260903_TEST_pcupdate-locked-managed-objects/PCUpdateLockedManagedObjectsSmoke.csproj`: Release.
- `260818_TEST_panel-cladding-pc-commands/PanelCladdingPcCommandsSmoke.csproj`: Release.

Paths above are relative to `Project_Test/`. Update and Undo regressions were
repeated after the final saved-object-ID rollback correction.

## Native harness and limitation

`NativePointOrderSmoke.cs` compiles into the new test project. With `--native`, it
initializes an invisible RhinoCore, then tests standard skill geometry parity,
front normals, final U-right/V-up directions, area, user strings/UserDictionary,
repeat no-op, explicit unsupported skips, PCpid selected-only mutation and single
Undo, and PCUpdate generation-after-ordering plus owned/ambient failure rollback.
Synthetic saved files, if reached, stay under ignored
`Runtime_Test/local/panel-point-order/`.

An independent same-user hidden host ran `Run-NativeHost.ps1` on 2026-10-07. Pure
cases passed, then RhinoCore construction failed with COMException `0x80004005`
in `StartupInProcess`, before any native geometry assertions or document creation.
Recorded exit code: `-532462766`. Raw local evidence: `native-host.log`,
`native-host-error.log`, `native-host-result.log`. An earlier capture wrapper did
not retain stderr; the corrected wrapper captured the same startup failure.
No user document was modified. Native geometry/Undo acceptance remains pending.

## Builds

Serialized Debug/Release solution builds passed (zero warnings/errors):
`dotnet build MCP_Rhino.sln -c <configuration> --nologo -m:1 -p:BuildInParallel=false`.
Standalone RHP Debug/Release builds also passed. The final saved-ID correction is
isolated to PanelCladdingEditor; its standalone builds and Update/Undo regressions
were repeated in both configurations. The Server change only excludes this
standalone TEST folder from the Server's smoke-source glob; no MCP surface changed.

## Package and installation

Package publish/rebuild passed for 1.0.92 at
`%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.92-point-order-261007/PanelCladdingEditor-1.0.92`.
Compiled and packaged assembly GUID checks passed against the manifest and distinct
MCP_Rhino identity. All 27 bundle hashes match. RHP SHA-256:
`7ce0a85adf187f688eb9638a0245b375b89efc6a9c87c0dfa24e4ab8b164456f`.

Installation followed the existing session authorization with Rhino closed.
Independent host nonce attestation passed before activation. Install and mandatory
Validate passed in the host process; a separate host process repeated Validate
after installer exit. Exact RHP path/hash and all 12 command names/values passed,
and root/PlugIn/CommandList timestamps all advanced. Independent HKEY_USERS/current
SID registry-provider reads agree. The 1.0.91 rollback copy matches its original
RHP hash. See [activation-summary.json](activation-summary.json).

Final Rhino process count was zero. Loaded-module/post-start registry attestation
and native command acceptance await the next Rhino launch. Installation checks do
not establish native geometry, prompt or Undo behavior.

Subsequent 2026-10-07 post-start attestation passed for the exact loaded 1.0.92
RHP and expected root/CommandList advancement with unchanged PlugIn timestamp.
See updated activation-summary.json and post-start-snapshot.log. The user's false
PCpid planarity rejection is addressed in the separate planar-bounds correction.
