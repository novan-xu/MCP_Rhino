# PCCreate segmented default TEST

Execution date: 2026-10-06.

## Reproduction and coverage

The extended curve-topology smoke failed before the fix with
`Continuous PCCreate guides must default to segmented tracks without a merge mask`
(process exit -532462766, unhandled assertion). The same test passes after removing
guide-continuity merge inference from PCCreate.

The full continuous-guide fixture has 2 horizontal and 2 vertical tracks. Its
persisted attributes round-trip through PanelCladdingKeyService.Parse and the
production extrusion planner to nine cells, twelve distinct intermediate segment
curves, zero merged curves, and four perimeter frames. The partial-guide fixture
retains three missing horizontal atoms and five logical blank cells, now with no
merge runs. Mixed split/continuous input also initializes without merges. Existing
stale merge masks are deleted, and the prior dimension tests remain enabled.

## Commands and results

All following post-fix commands exited 0:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Release
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Release
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-sparse-topology\PanelCladdingSparseTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -OutputRoot (Join-Path (Get-Location).Path '.tmp-package-pccreate-segmented-261006')
```

Standalone builds and package rebuild had zero warnings/errors. Narrow product
builds are appropriate because MCP server/host/Router/registration are unchanged.
The sparse suite covers template eligibility and topology defaults; extrusion-sync
also verifies explicitly merged curves still expand correctly and scoped sync
retains dimensions. No UI automation was used.

## Package verification

Staged bundle:
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.81-pccreate-segmented-261006`.
This includes both the segmented default and the earlier unit-dimension fix.

All 27 bundle manifest hashes match actual staged files. Direct assembly metadata
verification passed before packaging and again on the staged RHP using:

```powershell
$stagedBundle = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\staged\1.0.81-pccreate-segmented-261006'
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath @(
    (Join-Path (Get-Location).Path 'src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp'),
    (Join-Path $stagedBundle 'Plugin\PanelCladdingEditor.rhp')
)
```

PanelCladdingEditor assembly GUID remains
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, manifest-matched and distinct from
MCP_Rhino. RHP SHA256:
`e62185223b9cc64b5f121b0b7fb1834aae04b64b9e285c34195f715661edeb8c`.
See [verification-summary.json](verification-summary.json). Ignored local `.log`
files in this folder contain command transcripts.

## Live/deployment limitations

No installation, registry mutation, live document edit, native Rhino command, or
Undo test was performed. Later activation requires the AGENTS.md independent host
registration attestation, installer Validate, exact loaded-RHP, and post-start
timestamp gates. Existing production panels are not migrated by this source fix.

After activation, use a disposable saved document: run PCCreate with continuous
crossing guides, inspect the segmented extrusion view and dimension keys, spawn
curves, and confirm the independent intermediate segments. A separate Undo check
should confirm native attribute rollback. PCCreate retains its existing grid/cell
reset behavior.
