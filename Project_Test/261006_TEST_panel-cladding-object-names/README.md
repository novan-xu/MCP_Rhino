# Panel and dependency object-name verification

Executed 2026-10-06. Focused naming checks pass in Debug and Release. They exercise
short names for panels, corner roles, surfaces, frame curves, and intermediate
curves; preserve zeros/suffixes; and cover casing, custom identifiers, missing CID,
and unit-type CID correction before naming.

For each sync scope, five in-memory fixtures test unchanged names, an isolated
panel/surface/curve name change, and all names changed. The tests assert name-only
detection, unchanged full IDs/materials/coverage, exact writes reaching the commit
interface, and no writes when names already match. Live adapter source contracts
cover all write paths and PCCreate's name-only commit guard. No native Rhino
geometry or attribute transaction was executed.

## Commands and results

Run from the repository root; all eight invocations exited 0:

```powershell
dotnet run --project Project_Test/261006_TEST_panel-cladding-object-names/PanelCladdingObjectNamesSmoke.csproj -c Debug
dotnet run --project Project_Test/261006_TEST_panel-cladding-object-names/PanelCladdingObjectNamesSmoke.csproj -c Release
dotnet run --project Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj -c Debug
dotnet run --project Project_Test/260818_TEST_panel-cladding-extrusion-sync/PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project Project_Test/260818_TEST_panel-cladding-create-command/PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project Project_Test/260923_TEST_panel-cladding-role-cid/PanelCladdingRoleCidSmoke.csproj -c Debug
dotnet run --project Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project Project_Test/260903_TEST_pcupdate-command-undo/PCUpdateCommandUndoSmoke.csproj -c Debug
```

The Undo smoke passed source/registration checks and explicitly skipped its
optional native Rhino branch. Source contracts do not attest native Undo.
The existing surface-sync baseline fixture now supplies already-correct names
so its no-change assertions remain meaningful under the new behavior.

```powershell
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
& Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
$packageOutput = Join-Path (Get-Location).Path '.tmp-package-object-names-261006'
# Packaging used a fresh path verified inside this workspace.
& Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -OutputRoot $packageOutput
$stagePath = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\staged\1.0.86-object-names-261006'
# The stage destination did not exist when copied.
Copy-Item -LiteralPath (Join-Path $packageOutput 'PanelCladdingEditor-1.0.86') -Destination $stagePath -Recurse
$serverRhp = Join-Path (Get-Location).Path 'src/MCP_Rhino.Server/bin/Release/net8.0/MCP_Rhino.Server.rhp'
& Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath @($serverRhp, (Join-Path $stagePath 'Plugin/PanelCladdingEditor.rhp'))
git diff --check
```

Portable build/package results are in [verification-summary.json](verification-summary.json).
Command logs are ignored local evidence in this folder. The package is staged;
installation, mandatory production Validate, and native Rhino acceptance remain
pending while the user's existing Rhino session is open.

## Installation follow-up — 2026-10-06

Installed 1.0.86 after the user requested installation and zero Rhino processes
were confirmed. All 27 staged hashes and the assembly GUID were checked again.
Used the existing `260930_TEST_panel-cladding-type-suspension/Host-PanelCladdingActivation.ps1`
through hidden same-user WMI-launched PowerShell with this bundle passed explicitly.
Probe mode plus an independent Windows registry provider read established host
persistence; its temporary nonce was removed. Install mode ran Install and Validate.
After exit, a separate host process ran Validate again and captured registry state.

[activation-summary.json](activation-summary.json) records the passing RHP path/hash,
exact eleven-command registration, three advanced timestamps, independent registry
provider agreement, and verified 1.0.84 rollback copy. Raw `.log` snapshots and the
prior `.reg` export remain ignored local evidence. No Rhino document was modified.
Rhino startup loaded-module/timestamp checks remain pending its next launch.

Post-start follow-up: Rhino process 43292 loaded the exact installed 1.0.86 RHP.
Independent host snapshot checks passed for its hash, exact command list, advanced
root/CommandList timestamps, and unchanged installer-owned PlugIn timestamp.
Portable details are recorded in activation-summary.json. Local registry backup
exports are excluded by the repository's narrow activation-backup ignore rule.
