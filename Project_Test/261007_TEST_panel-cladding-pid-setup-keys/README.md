# PCpid setup keys and dimensions validation — 2026-10-07

## Automated behavior checks

```powershell
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid-setup-keys/PanelCladdingPidSetupKeysSmoke.csproj -c Debug
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid-setup-keys/PanelCladdingPidSetupKeysSmoke.csproj -c Release
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj -c Debug
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj -c Release
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid-layer-scope/PanelCladdingPidLayerScopeSmoke.csproj -c Debug
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid-layer-scope/PanelCladdingPidLayerScopeSmoke.csproj -c Release
dotnet run --project Project_Test/260818_TEST_panel-cladding-pc-commands/PanelCladdingPcCommandsSmoke.csproj -c Release
```

All seven invocations passed, exit 0. The new smoke checks the user's exact 30-key
schema independently, including 23 blank fields and seven generated values on a
fresh panel. It verifies retained non-generated values, stale generated-value
replacement, canonical casing, conflicting-value rejection, coherent corner CID,
repeat stability, and unchanged source snapshots. Geometry tests cover four
cardinal directions, an arbitrary azimuth, small permitted facade tilt, large
translations, model-unit scaling and French numeric culture. Dimensions retain
the established invariant F5 widthxheight syntax. Partial-selection setup uses
unselected lower-floor context without adding that context to the write set.

Existing regressions retain all 20 tutorial expectations, singleton/partial
selection invariance, subtree boundaries, PID/CID collision checks and dependency
protection. The command inventory remains exactly 12 commands.

## Native checks

The original PCpid `NativeSmoke.cs` now additionally asserts real ObjectAttributes
contain all 30 canonical keys, blank keys persist, populated values survive,
dimensions replace stale values, repeat is a no-op, Undo restores missing keys and
old dimensions/casing, and partial selection leaves unselected dimensions/keys
untouched. It compiled in Debug/Release as part of the original smoke above.

Native assertions were not rerun: the previously attempted RhinoCore startup in
this session failed with COMException 0x80004005 before document creation. These
native checks remain pending; pure tests do not establish Rhino write/Undo acceptance.
No live user document or synthetic .3dm was modified by this follow-up.

## Build, package and installation

```powershell
dotnet build MCP_Rhino.sln -c Debug --nologo -m:1 -p:BuildInParallel=false
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build MCP_Rhino.sln -c Release --nologo -m:1 -p:BuildInParallel=false
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
./Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
./Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -OutputRoot "$env:LOCALAPPDATA/PanelCladdingEditor/staged/1.0.91-pcpid-setup-keys-261007"
```

Both solution and standalone builds passed, zero warnings/errors. Builds were
serialized to avoid the previously observed DLL/RHP output race. Package publish
and direct RHP rebuild passed. Compiled and staged assembly identity gates passed:
nonempty assembly GUID matches the class/manifest and remains distinct from MCP_Rhino.
All 27 bundle file hashes passed. The staged/installed RHP SHA-256 is
`70eb5af8aced7fd8f0b035f8bce6f0ae3a6fd9480a41271663f5a6c7bf8d5722`.

Version 1.0.91 was installed while Rhino remained closed, under the session's
existing installation authorization. Before activation, a nonce written by a
hidden same-user host PowerShell was independently verified through StdRegProv
under HKEY_USERS/current SID and removed. Host Install and mandatory Validate
passed, followed by Validate in a separate host process after the installer exited.
The registered RHP exists with the expected hash; the exact 12 command names/values
and all three advanced registry timestamps passed. Independent registry-provider
readback agrees. The previous 1.0.90 rollback RHP hash matches the prior installation.
Portable evidence is in [activation-summary.json](activation-summary.json).

Final Rhino process count was zero. Loaded-module/post-start timestamp verification
awaits the next Rhino launch; native command acceptance remains pending.
