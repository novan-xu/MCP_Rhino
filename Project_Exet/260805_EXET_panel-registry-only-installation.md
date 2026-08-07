# PanelCladdingEditor Registry-Only Installation Execution

## Corresponding plan

- Plan: `Project_Plan/260805_PLAN_panel-registry-only-installation.md`
- Execution date: 2026-08-05

## Associated artifacts

- Tests: `Project_Test/260805_TEST_panel-registry-only-installation/`
- Bundle: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.6/` (generated,
  git-ignored)
- Commit / PR: none requested

## Execution result / actual scope

- Changed `PanelCladdingEditorPlugin.LoadTime` from `WhenNeeded` to `AtStartup` so command
  registration happens during Rhino startup.
- Advanced the package version from 1.0.5 to 1.0.6.
- Replaced Rhino Package Manager deployment with a registry-only current-user installation at
  `%LOCALAPPDATA%\PanelCladdingEditor\plugin\<version>`.
- Added top-level and detailed HKCU `FileName` registration, `LoadMode=1`,
  `DirectoryInstall=0`, and `IsDotNETPlugIn=1` for plug-in GUID
  `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`.
- Added migration support for both historical Package Manager roots:
  `PanelCladdingEditor` and `BayHealthPanelCladdingEditor`.
- Added installer ownership hashes, validation, rollback moves, staging while Rhino is running, and
  an ownership-aware uninstaller.
- Updated package documentation and the architecture deployment contract.
- Installed production version 1.0.6 and migrated the existing
  `%APPDATA%\McNeel\Rhinoceros\packages\8.0\BayHealthPanelCladdingEditor` tree out of Rhino
  discovery.

## Deviations from the plan

- Rhino was no longer running when the production installation step executed, so the generated
  bundle installed directly rather than creating a new 1.0.6 staged copy.
- Only `BayHealthPanelCladdingEditor` existed in the production Package Manager tree. The installer
  still supports and regression-tests migration of both historical names.
- Live command invocation remains a post-restart acceptance check; the installation and registry
  state are fully validated without UI automation.

## Issues found and fixed during execution

- The package builder used `$PSScriptRoot` in a parameter default, which is empty during parameter
  binding under Windows PowerShell. The default output root is now resolved in the script body.
- The earlier installer bundled only plug-in files. Version 1.0.6 includes the package definition and
  install/uninstall scripts, so a staged bundle is self-contained.
- Production inspection found the source product name `PanelCladdingEditor` but the active Rhino
  Package Manager root and `manifest.yml` name `BayHealthPanelCladdingEditor`. Both are now explicit
  legacy migration aliases rather than active installation identities.

## Test record

### Builds

- `dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo`
  - Exit 0; 0 warnings; 0 errors.
- `dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo`
  - Exit 0; 0 warnings; 0 errors.
- `dotnet build .\MCP_Rhino.sln -c Debug --nologo`
  - Exit 0; 0 warnings; 0 errors.
- `dotnet build .\MCP_Rhino.sln -c Release --nologo`
  - Exit 0; 0 warnings; 0 errors.

### Standalone behavior

- `dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Debug`
  - Exit 0; all seven behavior/dependency checkpoints passed; temporary artifacts cleaned.
- `dotnet run --project .\Project_Test\260804_TEST_standalone-panel-cladding-editor\PanelCladdingEditorSmoke.csproj -c Release`
  - Exit 0; all seven behavior/dependency checkpoints passed; temporary artifacts cleaned.

### Package and installer

- `powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1`
  - Exit 0; produced `PanelCladdingEditor-1.0.6`; direct Release build reported 0 warnings and
    0 errors.
- `powershell -ExecutionPolicy Bypass -File .\Project_Test\260805_TEST_panel-registry-only-installation\PanelRegistryOnlyInstallationSmoke.ps1 -BundleRoot .\Packaging\PanelCladdingEditor\artifacts\PanelCladdingEditor-1.0.6`
  - Exit 0.
  - Migrated both package-name variants in isolated roots.
  - Asserted one active registry-owned RHP plus two recoverable backups.
  - Asserted startup load, `DirectoryInstall=0`, and both canonical file paths.
  - Validation rejected a reintroduced Package Manager RHP.
  - Uninstall removed owned files and registry values while retaining backups.
- `git diff --check`
  - Exit 0; only line-ending conversion warnings from existing Windows worktree files.

### Production installation

- Installed root:
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.6`
- Active RHP SHA-256:
  `960b40a9228b0159d2839097193e46e52c14d6f6935444ef48d62978cd912c4b`
- Registry root and detailed registration both point to:
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.6\PanelCladdingEditor.rhp`
- Registry values: `LoadMode=1`, `DirectoryInstall=0`, `IsDotNETPlugIn=1`.
- Package Manager recursive `PanelCladdingEditor.rhp` count after migration: 0.
- Legacy package backup:
  `C:\Users\nxu\AppData\Local\PanelCladdingEditor\rollback\8ee4a9af170345438bde77772217751a\legacy-package-BayHealthPanelCladdingEditor`
- Production `-Mode Validate`: exit 0, reported registry-only installation valid.

## Acceptance criteria alignment

- Debug and Release panel and solution builds: passed.
- Existing standalone behavior and dependency smoke: passed in both configurations.
- Direct RHP package with no self DLL and matching deps identity: passed by builder and installer.
- Both historical Package Manager names migrate to one registry-only installation: passed in the
  isolated regression; the production BayHealth root migrated successfully.
- Startup HKCU registration and non-directory installation mode: passed.
- Reintroduced Package Manager duplicate detection: passed.
- Production hash and discovery validation: passed.
- No duplicate-ID dialog and live command registration: requires the first Rhino restart after the
  production migration.

## Rollback validation

- The isolated smoke completed the ownership-aware uninstall path successfully.
- Production 1.0.5 package files were moved, not deleted, to the recorded LocalAppData rollback path.
- Installer failure handling restores moved package roots and removes a partially activated new
  registration/tree.

## Current remaining items

- Restart Rhino once and confirm that startup contains no PanelCladdingEditor `ID already in use`
  message.
- Run `_PanelCladdingEditorSmoke` or `_PanelCladdingEditor` to confirm live command registration.
  Append the result here rather than opening a second execution document.

## Conclusion

PanelCladdingEditor 1.0.6 is built, regression-tested, installed, hash-validated, and has exactly one
Rhino discovery owner. The Package Manager copy that caused the repeated plug-in identity load was
removed from active discovery and retained as a recoverable backup. One Rhino restart is required to
complete live UI acceptance.
