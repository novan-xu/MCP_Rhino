# PC editor offset layout preservation TEST

Execution date: 2026-10-06.

## Reproduction and test design

Extended the existing topology-persistence smoke with a persisted 4x4 fixture
containing horizontal/vertical deleted and hidden segments, two merged runs,
cladding materials, a parent-cell link, frame/segment profiles, and a length
modifier. The pre-fix Debug run failed with
`H/Both/edit changed an indexed topology mask` (exit -532462766).

The regression tests the real editor methods on unshown in-process WPF objects
with the existing stateful fake repository. It does not control Windows, a running
editor window, or a Rhino document. The fake repository now also carries parsed
FrameAssignments through reload, as the production layout reader already does.

Each Debug/Release run covers eight edit/save combinations: direct H, direct V,
row dimension, and column dimension, each with Save Both or Save Extrusions then
Save Cladding. Checks verify byte-identical delete/merge/hide masks, retained
cladding materials/parent links, retained extrusion profile assignments/modifiers,
display IDs at the new coordinates, saved coordinates, Undo, reload, and invalid
or no-op input without an Undo entry. Existing topology/save coverage still runs.

## Commands and results

All following post-fix commands exited 0:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-topology-persistence\PanelCladdingTopologyPersistenceSmoke.csproj -c Release
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-hide-mask\PanelCladdingHideMaskSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -OutputRoot (Join-Path (Get-Location).Path '.tmp-package-pc-editor-offset-layout-261006')
```

Debug/Release product builds and Release package rebuild passed with zero warnings
and errors. Narrow builds are appropriate because MCP server/host/Router/tool
registration are unchanged. Prior PCCreate dimension/segmentation fixes remain
covered. Scoped save and hidden-mask regressions passed as well.

## Package verification

Staged bundle:
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.82-offset-layout-261006`.
It includes all three fixes in this chat. All 27 bundle-manifest file hashes match.
The compiled assembly identity check passed before packaging and on the staged
RHP with this command:

```powershell
$stagedBundle = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\staged\1.0.82-offset-layout-261006'
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath @(
    (Join-Path (Get-Location).Path 'src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp'),
    (Join-Path $stagedBundle 'Plugin\PanelCladdingEditor.rhp')
)
```

PanelCladdingEditor assembly GUID remains
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest/class and distinct
from MCP_Rhino. Staged RHP SHA256:
`ca08cd3fda9a98095f5140ab271575e30effb687b20121b4f638de35996afcb5`.
See [verification-summary.json](verification-summary.json). Local ignored `.log`
files in this folder retain command transcripts.

## Deployment and live validation

No production installation, registry mutation, or live Rhino document edit was
performed. Editor Undo is tested in process; native Rhino Undo is not tested here.
Activation must follow AGENTS.md's independent host registration attestation,
installer Validate, loaded-RHP, and post-start timestamp gates.

After activation, use a disposable saved model with nondefault masks and assigned
cladding: move H/V values, inspect retained deleted/hidden/merged states, save and
reopen PCEditor, then verify attributes and generated curve geometry. Adding or
removing a track remains an explicit topology operation.
