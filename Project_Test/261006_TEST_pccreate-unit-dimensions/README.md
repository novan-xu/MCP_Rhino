# PCCreate unit dimensions TEST

Execution date: 2026-10-06.

## Regression coverage

Extended the existing create-command smoke in
`Project_Test/260818_TEST_panel-cladding-create-command/Program.cs` rather than
introducing a duplicate test host. It checks exact canonical keys/values, missing
dimensions, replacement of stale dimensions and mixed-case keys, translated local
extents, five-decimal rounding under fr-FR, two differently sized panels in one
selection, preservation of H/V offsets, and unrelated user text.

Before the production fix, the updated Debug smoke failed with exit 1 and
`Grid write count is incorrect`, reproducing the missing dimension writes.
After the fix, both Debug and Release runs passed with exit 0.

## Commands and results

All commands below completed with exit 0 after the fix:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Release
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-curve-topology\PanelCladdingCurveTopologySmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-sparse-topology\PanelCladdingSparseTopologySmoke.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -OutputRoot (Join-Path (Get-Location).Path '.tmp-package-pccreate-dimensions-261006')
```

Both standalone RHP builds and the Release package rebuild had zero warnings and
errors. These narrow builds cover the changed standalone product; the MCP server,
Router, host, and tool surface were not changed.

Existing curve-topology coverage still passes for closed atomic segments,
endpoint-only/dangling-guide rejection, and PCMatchCrv transfer. Sparse-topology
coverage still passes for independent merge/hide/delete masks and default masks.

## Package verification

Copied the fresh 1.0.80 bundle to
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.80-pccreate-dimensions-261006`.
All 27 entries in its bundle manifest match their actual SHA256 values. Verified
the staged RHP's assembly metadata directly with the identity verifier:

```powershell
$stagedBundle = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\staged\1.0.80-pccreate-dimensions-261006'
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath @(
    (Join-Path (Get-Location).Path 'src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp'),
    (Join-Path $stagedBundle 'Plugin\PanelCladdingEditor.rhp')
)
```

The panel editor assembly GUID is `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching
its manifest and plug-in class declaration and distinct from MCP_Rhino. The
staged RHP SHA256 is
`11f24bac28843c872ea162004d27b4a6e2838e08057e24faa0258ef25570badd`.

See [verification-summary.json](verification-summary.json) for recorded results.
Local command transcripts are in this folder as ignored `.log` files.

## Live validation and deployment boundaries

No production installation, registry writes, Rhino command execution, or document
mutation occurred. Installation and native command validation remain pending.
Any later activation must follow AGENTS.md's independent host registry attestation,
installer Validate, exact loaded-RHP, and post-start timestamp requirements.

After activation, a disposable saved Rhino document can verify PCCreate on two
differently sized panels with valid guides. Inspect all three dimension attributes
and their H/V offsets, then Undo and confirm the previous attributes return.
PCCreate retains its existing grid/cell reset behavior; it is not a dimension-only
refresh for an already configured production panel.
