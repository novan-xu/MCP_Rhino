# Results — PanelCladdingEditor plug-in registration repair

Date: 2026-08-26

## Script syntax

PowerShell parser checks passed for:

- `Packaging/PanelCladdingEditor/Install-PanelCladdingEditor.ps1`;
- `Project_Test/260826_TEST_panel-cladding-plugin-registration/Test-PanelCladdingPluginRegistration.ps1`.

`git diff --check` passed for the focused implementation and artifact set.

## Debug and Release builds

Commands:

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo
```

Both exited `0` with zero warnings and zero errors.

## Package build

Command:

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.68
```

Result: exit `0`. Bundle:

```text
Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.68
```

## Isolated registry lifecycle regression

Command:

```powershell
powershell -ExecutionPolicy Bypass -File .\Project_Test\260826_TEST_panel-cladding-plugin-registration\Test-PanelCladdingPluginRegistration.ps1 -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.68
```

Result: exit `0`.

Validated:

- install writes a pending root `Name`/`FileName` registration without `PlugIn`, `CommandList`, or
  `Panels` children;
- `Validate` accepts that pending state;
- `Validate` accepts a simulated Rhino-expanded state with the active `PlugIn\FileName`, startup
  metadata, and exactly all ten supported commands;
- `Validate` rejects the former root/child hybrid when `CommandList` is absent;
- `Repair` removes the malformed expanded children and restores the pending first-load state.

## Production activation

Rhino was closed at activation time. The bundled installer activated:

```text
C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.68
```

Production `Validate` passed and reported `pending first load registration`. The production registry
key contains only the expected root `Name` and `FileName`; no pre-created child keys remain.

The packaged and installed RHP SHA-256 values match:

```text
8C86BAC88C68AD3218F102933E19E0DC1407E8D59658ED652A468CD8420B88E7
```

Installed assembly inspection found 504 managed types and ten concrete Rhino command types.

## Plug-in identity

The existing assembly identity probe passed against the installed MCP_Rhino and
PanelCladdingEditor RHPs:

- MCP_Rhino: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`;
- PanelCladdingEditor: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`;
- both IDs are declared, non-empty, correct, and distinct.

## Pending live verification

Rhino has not been launched after activating 1.0.68. On the next Rhino start, Rhino should consume
the pending shorthand record, load the RHP, and write the expanded `PlugIn`/`CommandList` state.
Plug-in Manager presence and live command lookup therefore require that one user-initiated restart.

## Second-stage full-registration result

Live Rhino verification after the first repair showed that Rhino 8.33 did not consume the 1.0.68
shorthand record. The production key remained root-only, the RHP was absent from PID `41128`, and
there was no `PlugIn` or `CommandList` state. MCP_Rhino was loaded in that same process, ruling out a
general Rhino/MCP startup failure.

The installer was revised to write the complete registration and the package advanced to `1.0.69`.
The focused regression now verifies:

- a clean install contains root product/startup metadata, `PlugIn\FileName`, and exactly ten
  `CommandList` values;
- shorthand-only registration fails validation;
- deleting `PCCrvTemplate` from `CommandList` fails validation;
- `Repair` reconstructs the complete record in both cases.

The revised smoke exited `0` with:

```text
PASS: complete install, shorthand rejection, incomplete-command rejection, and full repair behaved as expected.
```

Debug and Release builds again passed with zero warnings/errors. Package assembly identity remained
declared, correct, non-empty, and distinct. The final source/bundled installer SHA-256 comparison
returned `InstallerHashMatch=True`.

Rhino was closed before final activation, so 1.0.69 was installed directly at:

```text
C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69
```

Production validation passed as `complete registration`. The current production registry already
contains all ten command names and the canonical active RHP path; it no longer depends on Rhino to
expand anything on first startup. Packaged and installed RHP SHA-256 remains:

```text
8C86BAC88C68AD3218F102933E19E0DC1407E8D59658ED652A468CD8420B88E7
```

The remaining live check is to start Rhino and confirm the already-complete record appears in
Plug-in Manager and loads the RHP.

## Third-stage live load verification

Date: 2026-08-26, 17:45–17:55 local.

### Pre-check: production record had drifted

```powershell
reg query "HKCU\SOFTWARE\McNeel\Rhinoceros\8.0\Plug-ins\7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35\PlugIn" /s
```

```text
FileName    REG_SZ    C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.66\PanelCladdingEditor.rhp
```

`Test-Path` on that file returned `False`. The only installed version directory was `1.0.69`.

```powershell
.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.69\Installer\Install-PanelCladdingEditor.ps1 -Mode Validate
```

```text
VALIDATE FAILED: The complete PanelCladdingEditor registry registration is inconsistent.
```

### Repair

```powershell
.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.69\Installer\Install-PanelCladdingEditor.ps1 -Mode Repair
.\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.69\Installer\Install-PanelCladdingEditor.ps1 -Mode Validate
```

```text
PanelCladdingEditor 1.0.69 installed registry-only at C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69.
PanelCladdingEditor 1.0.69 registry-only installation is valid (complete registration).
```

Pre-repair registry key backup retained for the session at
`<scratchpad>/panelcladding-plugin-key-backup.reg`; post-repair baseline at
`<scratchpad>/baseline-after-repair.reg`.

### Live load

Rhino 8 launched via `Start-Process`, then the live process was polled for the plug-in module. No
UI automation was used.

```text
LOADED MODULE: C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69\PanelCladdingEditor.rhp
PanelCladdingEditor module loaded: True
```

### Registry ownership observation

Key last-write times immediately after the successful load:

```text
(root)         2026-08-26 17:54:36
\CommandList   2026-08-26 17:54:36
\PlugIn        2026-08-26 17:50:31
```

Rhino rewrote the root record and `CommandList` on load, and left `PlugIn\FileName` at the
installer's write. Regression value: `PlugIn\FileName` is installer-owned and never self-heals, so
a stale pointer there is detectable only by `-Mode Validate`. Comparing these three timestamps
after an install and again after the first Rhino start is the cheapest reproduction for any future
drift.

Post-load record matches the shape carried by the loading sibling `MCP_Rhino.Server`: root identity
plus `LoadMode=1`, `Type=16`, `IsDotNETPlugIn=1`, `DirectoryInstall=0`, and one `PlugIn\FileName`
targeting the active versioned RHP.
