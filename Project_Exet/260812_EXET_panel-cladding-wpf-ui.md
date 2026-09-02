# Panel Cladding WPF UI Execution

## Corresponding Plan

- Plan: `Project_Plan/260812_PLAN_panel-cladding-wpf-ui.md`
- Execution date: 2026-08-12

## Associated Artifacts

- Test folder: `Project_Test/260812_TEST_panel-cladding-wpf-ui/`
- Desktop render: `Project_Test/260812_TEST_panel-cladding-wpf-ui/panel-cladding-wpf-ui-1440x900.png`
- Compact render: `Project_Test/260812_TEST_panel-cladding-wpf-ui/panel-cladding-wpf-ui-1024x768.png`
- Material dialog render:
  `Project_Test/260812_TEST_panel-cladding-wpf-ui/panel-cladding-material-setup-680x690.png`
- Release bundle: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.23/`
- Commit / PR: none created in this execution

## Execution Result / Actual Scope

- Replaced the production Eto form and drawable preview with a WPF `Window`, XAML design system,
  custom `FrameworkElement` grid renderer, and WPF material setup dialog. No WebView, HTML runtime,
  Rhino panel, or Eto production reference remains.
- Reproduced the exported product header, source status, toolbar, 20-pixel canvas grid, view switch,
  total-size badge, weighted panel grid, dimension badges/locks, material legend, current-panel card,
  assignment tabs, calculated type card, workbook state, fixed action footer, and setup modal.
- The custom grid draws real horizontal/vertical offset proportions, assignment colors, parent arrows,
  dashed shared boundaries, hover and selected states, and model-unit dimension labels.
- Added click, Shift/Ctrl multi-selection, directional marquee selection, legend drag/drop, zoom, clear,
  material assignment, parent-cell assignment, Delete, Ctrl+Z undo, dirty-state protection, workbook
  selection, type preview, transactional save/sync, and toast/status feedback.
- Material definitions are derived from real assignment codes using the exported category palettes.
  The setup dialog supports category, description, suggested palette, custom hexadecimal color, and
  session-local material additions.
- Kept the existing application, domain, live Rhino repository, workbook repository, preview renderer,
  and save service. The controller now delegates type preview to the existing signature service.
- The Rhino command owns the WPF window through `RhinoApp.MainWindowHandle()`, maintains one editor
  instance, restores a minimized window, and activates it when the command is run again.
- Retargeted the plug-in and its direct smoke hosts to `net8.0-windows`, updated packaging and GUID
  verification paths, and removed production `Rhino.UI` / `Eto` references.

## Variance From Plan

- `GlobalUsings.cs` was added because WPF's generated temporary project does not propagate the prior
  implicit `System.IO` imports used across existing source files.
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` received one exact test-folder exclusion because the
  server intentionally compiles `Project_Test/**/*.cs`; without it, the standalone WPF smoke program
  was incorrectly compiled into the server.
- Packaging and shared plug-in identity verification required their PanelCladdingEditor output paths
  to change from `net8.0` to `net8.0-windows`.
- No live Rhino launch, plug-in installation, open-document write, or workbook mutation was performed.
  The request authorized implementation; final live acceptance remains a deployment step.

## Problems Found And Fixed During Construction

- Removed WPF-incompatible `CharacterSpacing` and `TextTrimming` declarations surfaced by XAML
  compilation while preserving the intended typography and truncation layout.
- Made `System.IO` explicit at project scope to keep all existing file/workbook code visible to the
  WPF markup compiler's temporary project.
- Added the exact standalone-test exclusion after the first solution build detected duplicate
  assembly attributes and WPF namespaces under the server project.
- Updated the package build and plug-in identity scripts after the Windows TFM changed the output
  folder.
- The first off-screen modal render exposed a transparent root. The modal now owns an explicit white
  surface token, matching both the handoff and actual Window rendering.
- Replaced process-randomized material color selection with the handoff's canonical category colors.

## Test Record

All listed commands completed with exit code 0 unless noted.

### Focused WPF smoke and visual renders

```powershell
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-wpf-ui\PanelCladdingWpfUiSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-wpf-ui\PanelCladdingWpfUiSmoke.csproj -c Release
```

Both configurations reported:

- WPF window loaded the real 4 x 3 fixture.
- design-critical header, canvas, assignment, workbook, and footer actions were present.
- the production assembly referenced WPF and did not reference Eto.
- 1440 x 900, 1024 x 768, and 680 x 690 PNGs rendered successfully.

Final artifact sizes were 99,726 bytes, 82,649 bytes, and 42,410 bytes respectively. Visual review
confirmed the reference desktop hierarchy, compact desktop fit without horizontal overflow, fixed
sidebar footer, weighted cell geometry, dimension labels, material palette, and white material dialog.

### Existing PanelCladdingEditor regressions

Each command was run in both Debug and Release:

```powershell
dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c <Configuration>
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-spawn\PanelCladdingSpawnSmoke.csproj -c <Configuration>
dotnet run --project .\Project_Test\260805_TEST_panel-cladding-match\PanelCladdingMatchSmoke.csproj -c <Configuration>
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-clear\PanelCladdingClearSmoke.csproj -c <Configuration>
dotnet run --project .\Project_Test\260807_TEST_panel-cladding-surface-sync\PanelCladdingSurfaceSyncSmoke.csproj -c <Configuration>
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-offset-sync\PanelCladdingOffsetSyncSmoke.csproj -c <Configuration>
dotnet run --project .\Project_Test\260812_TEST_panel-cladding-regions\PanelCladdingRegionsSmoke.csproj -c <Configuration>
```

All deterministic assertions passed. Offset-sync and region geometry probes retained their expected
`[SKIP]` because Rhino native geometry requires a running Rhino host.

### Builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug -m:1 --nologo
dotnet build .\MCP_Rhino.sln -c Release -m:1 --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug -t:Rebuild --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release -t:Rebuild --nologo
```

Both serial solution builds and both direct RHP rebuilds completed with 0 warnings and 0 errors.

### Plug-in identity

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug -SkipBuild
powershell -NoProfile -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
```

Both configurations reported the declared PanelCladdingEditor assembly GUID
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the plug-in class and package manifest and remaining
distinct from MCP_Rhino.

### Package

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1
```

The version `1.0.23` bundle built successfully. The final RHP is 292,864 bytes with SHA-256
`8dd0a879860ab45d012b9e28cebc673f0903bca4068654c0ac04fcbdec9c8d07`. The plug-in folder contains
the RHP, dependency manifest, Open XML assemblies, and `System.IO.Packaging`; it contains no Eto,
Rhino.UI, WebView, MCP, Router, or hosting assembly.

## Deployment Verification (2026-08-13)

The user confirmed every Rhino window was closed. A process check found no running `Rhino` process,
then the built `1.0.23` bundle was installed through the repository-owned repair transaction:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.23\Installer\Install-PanelCladdingEditor.ps1 -Mode Repair -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.23
powershell -NoProfile -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.23\Installer\Install-PanelCladdingEditor.ps1 -Mode Validate -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.23
```

Both commands exited 0. The validator reported a valid registry-only installation at
`%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.23`. Independent checks confirmed:

- the installed and bundle RHP SHA-256 values both equal
  `8dd0a879860ab45d012b9e28cebc673f0903bca4068654c0ac04fcbdec9c8d07`;
- the installed RHP is 292,864 bytes and declares assembly plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`;
- the compiled id matches `AssemblyInfo.cs`, the `PanelCladdingEditorPlugin` class, the package
  manifest, and the HKCU registration key;
- the root and `PlugIn` registry values both point to the installed RHP, with `LoadMode=1`,
  `DirectoryInstall=0`, and `IsDotNETPlugIn=1`;
- exactly one discoverable `PanelCladdingEditor.rhp` exists under the registry-owned plug-in tree and
  none exists under the Rhino Package Manager root;
- the active installation contains five owned files and no Eto, Rhino.UI, WebView, or Cef file;
- the prior active `1.0.23` payload was moved transactionally to installer-owned rollback id
  `19737dc69f074860b6831301abe1cc1c`.

The install manifest records `installedUtc=2026-08-13T04:04:26.7936119Z`. Rhino was not launched and
no live document or workbook was mutated during deployment verification.

## Acceptance Alignment

- Non-Eto, non-web desktop window: passed.
- Exported visual hierarchy and tokens reproduced in WPF: passed at 1440 x 900 and 1024 x 768.
- Real panel identity, grid, units, dimensions, assignments, workbook, and type state: passed.
- Selection, assignment, parent reference, drag/drop, zoom, clear, undo, setup, workbook, save, and
  dirty-state interactions: implemented and structurally exercised; live save remains a Rhino-host
  acceptance step.
- Unsupported extrusion and dimension mutation are visibly disabled/read-only with explanatory copy:
  passed.
- Debug/Release focused smokes, regressions, solution builds, direct builds, packaging, and plug-in
  identity: passed.
- Registry-only repair deployment, owned-file validation, hash equality, single-copy discovery, and
  installed assembly GUID: passed.

## Rollback Verification

- Source rollback restores the deleted Eto window/canvas, the prior controller and command lifecycle,
  `net8.0` target paths, and the prior smoke target frameworks.
- The live repository, domain models, save service, workbook implementation, plug-in GUID, registry
  contract, and command GUIDs were not changed.
- Deployment replaced the prior active `1.0.23` payload using the installer transaction. The previous
  active files remain under `%LOCALAPPDATA%\PanelCladdingEditor\rollback\19737dc69f074860b6831301abe1cc1c\prior-active`.
- No Rhino document or workbook was mutated, so no model-data rollback is required.

## Current Remaining Items

- Restart Rhino and perform a live `_PanelCladdingEditor` acceptance pass against a saved test
  document.
- Add safe application contracts for editable panel dimensions and extrusion/mullion authoring before
  enabling those designed controls.
- Persist project material descriptions/colors when a material-library storage contract is defined.

## Conclusion

The supplied Panel Cladding Editor design is implemented and installed as a real WPF desktop window
with no Eto or web surface. Existing panel/workbook behavior remains intact, the registry-only release
deployment is valid, and only the post-restart live Rhino acceptance pass remains.
