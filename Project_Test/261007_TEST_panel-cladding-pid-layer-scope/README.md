# PCpid panel-layer scope validation — 2026-10-07

## Reproduce

```powershell
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid-layer-scope/PanelCladdingPidLayerScopeSmoke.csproj -c Debug
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid-layer-scope/PanelCladdingPidLayerScopeSmoke.csproj -c Release
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj -c Debug
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj -c Release
dotnet run --project Project_Test/260818_TEST_panel-cladding-pc-commands/PanelCladdingPcCommandsSmoke.csproj -c Release
dotnet build MCP_Rhino.sln -c Debug --nologo
dotnet build MCP_Rhino.sln -c Release --nologo -m:1 -p:BuildInParallel=false
```

All final invocations passed, exit 0. Solution builds had zero warnings/errors.
The first parallel Release solution build hit MSB3030 because its test-host DLL and
RHP variants share an output directory; the serialized invocation above passed.
The new standalone TEST folder is precisely excluded from Server smoke-source
compilation. There are no MCP surface changes.

## Behavioral coverage

Reuses the anonymized 20-panel tutorial fixture from the original PCpid TEST.
Every panel tested individually gets its original full-context address, including
when north and first-floor references are unselected. A batch containing only
panels 4/7/10 still yields N2_02_01, E1_02_02 and E1_02_03. Every plan contains
exactly the selected IDs, with context count 20; snapshot metadata is unchanged.

Scope tests cover the exact root, direct and deeply nested sublayers, casing,
similarly named siblings, ancestors and unrelated branches. Identity tests cover
unselected/unselected duplicate PIDs (case/space normalization), selected repairs
of old duplicates, selected/unselected conflicts, blank unselected PIDs, and
ambiguous geometry outside the write selection. Original PCpid and command
inventory regressions still pass.

The live adapter enumerates normal, hidden, locked and reference source surfaces;
selected locked/hidden/reference objects remain ineligible for writes. The prior
native harness was updated to create root/nested layers and check partial writes,
unselected references, preserved unselected attributes and locked context. It
compiles but was not rerun: this session already established that RhinoCore startup
fails before document creation (COMException 0x80004005). Native selection/write/
Undo acceptance remains pending; no live document was mutated for this change.

## Release and installation state

Package 1.0.90 is staged at
`%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.90-pcpid-layer-scope-261007/PanelCladdingEditor-1.0.90`.
Publish and direct RHP rebuild passed. Assembly GUID checks passed before packaging
and on the staged RHP; the manifest/class/assembly ID is unchanged and distinct
from MCP_Rhino. All 27 bundle file hashes match. RHP SHA-256:
`352428f6df08fb62731881234b34d5ed883d5ce5d8575b396c48060e147ddc80`.
`git diff --check` passed.

Rhino currently runs the previously installed 1.0.89 RHP. Its exact loaded module
and independent host snapshot were checked during this follow-up: root/CommandList
timestamps advanced after startup while installer-owned PlugIn stayed unchanged.
That evidence is appended to the original PCpid activation summary. The new 1.0.90
package has not been activated while Rhino is open; independent host attestation,
Install, mandatory Validate and post-install host readback are required when closed.

## Installation follow-up — 2026-10-07

The user subsequently confirmed Rhino was closed. Version 1.0.90 installation and
mandatory installer Validate passed in an independently attested host process.
A separate host process repeated Validate after installer exit. Registered RHP
path/hash, all 12 exact command names and values, and advancement of root/PlugIn/
CommandList timestamps passed. Independent Windows registry-provider reads under
HKEY_USERS/current SID agreed. The prior 1.0.89 RHP rollback copy matches its
pre-install hash. See [activation-summary.json](activation-summary.json).

Rhino process count was zero at final validation. Post-start loaded-module and
timestamp checks await the next launch; native command mutation/Undo acceptance
remains pending. Raw host snapshots and the prior registry export remain local
installation evidence and are not portable test fixtures.
