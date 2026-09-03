# Results — 2026-09-03

## Focused smoke

Command:

```powershell
dotnet run --project .\Project_Test\260903_TEST_pcspawncrv-curve-simplification\PCSpawnCrvCurveSimplificationSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260903_TEST_pcspawncrv-curve-simplification\PCSpawnCrvCurveSimplificationSmoke.csproj -c Release
```

Both commands exited `0` and confirmed:

- exact `WT-04` source/destination layer mapping;
- preservation of nested layer suffixes;
- rejection of unsupported and root-only source layers; and
- propagation of the derived layer to all planned curves.

The native Rhino geometry case was reported as skipped in both standalone CLI runs. An attempted
RhinoCore in-process startup on this machine returned `COMException 0x80004005 (E_FAIL)` before any
geometry was created. The opt-in `--rhino-geometry` assertion remains available for a supported
RhinoCore test host; it checks both control-point reduction and sampled maximum deviation.

## Builds

These commands exited `0` with zero warnings and zero errors:

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release
```

Narrow project builds were used because this change is confined to the standalone
`PanelCladdingEditor` plug-in and does not change the MCP server, Router, tool surface, or document
route lifecycle.

## Affected regression smokes

The following Debug commands all exited `0`:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-extrusion-sync\PanelCladdingExtrusionSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-hide-mask\PanelCladdingHideMaskSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-layout-reconciliation\PanelCladdingLayoutReconciliationSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-split-spawn-sync\PanelCladdingSplitSpawnSyncSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260820_TEST_panel-extrusion-assignment\PanelExtrusionAssignmentSmoke.csproj -c Debug
```

`git diff --check` also exited `0`; its only output was the repository's existing LF-to-CRLF
working-copy warning.

## Package installation verification

After Rhino was closed, the 1.0.69 package was rebuilt, installed from the generated bundle, and
validated with the product installer. Install and `-Mode Validate` both exited `0`.

- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69\PanelCladdingEditor.rhp`
- Bundle/installed SHA-256: `28cb5e0adf66af5300a6b3fd885fd216771c91044fc53cf2dfd3922307de9e01`
- Installed assembly GUID: `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`
