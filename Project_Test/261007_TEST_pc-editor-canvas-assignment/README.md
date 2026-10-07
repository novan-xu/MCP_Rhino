# PC editor canvas assignment TEST

Execution date: 2026-10-07.

## Method and coverage

`PanelCladdingCanvasAssignmentSmoke.csproj` instantiates the real WPF editor on an
STA thread, loads a synthetic layout through a fake repository, and renders its
unshown visual tree. It does not automate Windows, show a window, or access a live
Rhino document. Synthetic profile thumbnails are test illustrations.

The Debug and Release runs passed these checks:

- Stored CID takes priority over the object name/PID, including case-insensitive
  metadata keys and preserved corner suffixes. Full CID is available in a tooltip.
- Legacy PID/unit-type fallback, bracket prefix shortening, and absent identifiers.
- Width/height occupy separate lines with invariant five-decimal formatting,
  rounding, trailing zeros, and units, including a comma-decimal current culture.
- Floating assignment belongs to CanvasWorkspace; curve selection reveals it,
  blank selection and Escape hide it, view changes restore/hide it correctly,
  and panel reload clears the previous selection.
- Routed preview drag/drop events reach the new drop surface; profile cards and
  catalogue filtering update. An empty selection rejects a drag.
- Modifier editing, the actual remove/clear buttons, Undo, changing selected
  curves, intermediate curves, and multi-selection preserve assignment behavior.
- Seven assigned profiles scroll at 1440x900 and 980x680; controls fit, the size
  readout and bottom action bar remain clear, and the list can reach its end.
- Preview interactions perform zero repository commits.

## Commands and results

All following final commands exited 0:

```powershell
dotnet run --project .\Project_Test\261007_TEST_pc-editor-canvas-assignment\PanelCladdingCanvasAssignmentSmoke.csproj -c Debug
dotnet run --project .\Project_Test\261007_TEST_pc-editor-canvas-assignment\PanelCladdingCanvasAssignmentSmoke.csproj -c Release
dotnet run --project .\Project_Test\260820_TEST_panel-extrusion-assignment\PanelExtrusionAssignmentSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260826_TEST_extrusion-assignment-dropzone\ExtrusionAssignmentDropZoneSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-scoped-save\PanelCladdingScopedSaveSmoke.csproj -c Debug
dotnet run --project .\Project_Test\261006_TEST_panel-cladding-object-names\PanelCladdingObjectNamesSmoke.csproj -c Debug
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --nologo
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
```

All four product builds reported zero warnings and errors. These focused project
builds cover the editor and server test-inclusion change; no Router, transport,
host, tool registration, or runtime routing behavior changed. The existing
assignment smoke ran its default synthetic assertions, without the optional real
PDF/workbook import scenario. The new smoke exercises WPF assignment interactions
without that optional external fixture.

The first new smoke run reached Escape but failed because its synthetic key event
lacked a RoutedEvent. Setting PreviewKeyDownEvent fixed the test fixture. The
initial server Debug build exposed 15 missing editor-type errors from the existing
standalone object-names test. Explicitly excluding that test folder, alongside the
new standalone test, from the server's recursive compile glob fixed the build;
both test projects remain independently runnable and passed.

## Visual review

- [Empty assignment](empty-selection-1440x900.png)
- [Assigned profile](assigned-1440x900.png)
- [Long list, normal window](scrolling-1440x900.png)
- [Long list, minimum window](scrolling-980x680.png)

The assigned normal-size and minimum-size long-list images were inspected. The
overlay is intentionally positioned over the right side of the canvas; at smaller
sizes or higher zoom it can cover some drawing content. Clearing selection hides
it. Size readout, profile controls, and bottom actions remain accessible.

## Limits

No production installation, registry change, package release, or live Rhino
validation was performed. These results verify source behavior and offscreen WPF
layout, not a running desktop Rhino deployment.

## Installation follow-up (2026-10-07)

At the user's request, built and installed version 1.0.87. The shared bundle is
`.tmp-package-canvas-assignment-261007/PanelCladdingEditor-1.0.87`. Build and
identity verification commands:

```powershell
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -OutputRoot 'C:\01_Projects\MCP_Rhino\.tmp-package-canvas-assignment-261007'
```

The actual build used the absolute equivalent of that output root:
`C:\01_Projects\MCP_Rhino\.tmp-package-canvas-assignment-261007` (fresh directory).
Direct PE verification also ran with explicit PluginPath arguments for the server
Release RHP and the final bundle's editor RHP. All 27 manifest file hashes passed;
both GUIDs were present, non-empty, correct, and distinct.

Used `Project_Test/260930_TEST_panel-cladding-type-suspension/Host-PanelCladdingActivation.ps1`
from hidden, same-user WMI-created PowerShell host processes. Probe mode passed a
nonce visibility check against explicit HKEY_USERS/current SID through StdRegProv
before any installation. The shared bundle was supplied as BundleRoot because
the initial agent-local LocalAppData copy was invisible to the real host.
The nonce key was removed. Install mode ran Install and Validate; a subsequent
host process ran Validate after the installation process exited. The Windows
registry provider separately confirmed the new RHP path.

[activation-summary.json](activation-summary.json) records the accepted identity,
hash, exact commands, and timestamps. Raw `.log` evidence and the installer-owned
prior registration export are ignored machine-local files. Prior version was
1.0.76. Version 1.0.87 installation and independent validation passed; Rhino was
closed, so live-load/post-start timestamp verification remains pending.

## Two-column follow-up (2026-10-07)

The assigned-profile template now uses two columns within the original panel
width. The existing smoke passes in Debug and Release, including the minimum
40-DIP modifier input width, long-list scrolling at both window sizes, and all
assignment interactions. Product Debug/Release builds pass without warnings or
errors. Render fixtures above were refreshed; the new
[three-profile preview](two-column-three-profiles-1440x900.png) and minimum-size
scrolling image were visually reviewed.

Version 1.0.87 was observed loaded at its exact registered RHP path in Rhino
process 13780; host snapshots confirmed the required post-start timestamp pattern.
Rhino closed during this follow-up. Version 1.0.88 was then built in the fresh
`.tmp-package-canvas-assignment-two-columns-261007` directory, using the same
Build-PanelCladdingEditorPackage and Verify-PluginAssemblyIdentity commands with
that output root and the new bundle path. Publish/rebuild and direct PE identity
verification passed.

The existing host helper was invoked in Probe, Install, and Validate modes with
the shared 1.0.88 bundle. A fresh nonce was independently confirmed and removed
before activation. Install/Validate and a separate post-exit host Validate passed.
RHP hash/path, exact commands, all three timestamps, and independent Windows
registry-provider readback passed. Results are in
[two-column-activation-summary.json](two-column-activation-summary.json). No Rhino
process remained after installation, so 1.0.88 live-load verification awaits the
next startup.
