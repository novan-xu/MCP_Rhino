# Modern Rhino Package Registration Plan

## 背景

MCP_Rhino 1.1.2 installs its Yak-style package under the Rhino 8 current-user package root and also writes the plug-in registration directly to `HKCU`. That registration was introduced as a workaround for the package-update ordering defect affecting Rhino 8.29. The workstation now runs Rhino 8.33, which contains McNeel's package-ordering correction, but the legacy manual registration causes the startup package-install pass to load the already-loaded `MCP_Rhino.Server.rhp` again and display `ID already in use` on every launch.

Live inspection confirmed that the registered and loaded RHP paths are identical, only one discoverable MCP_Rhino RHP exists, and the Router endpoint starts successfully before Rhino displays the redundant-install dialog.

## 目标

- Stop writing a redundant installer-owned Rhino plug-in registration on Rhino 8.32 and newer.
- Remove the owned legacy registration during install/repair on modern Rhino so Package Manager can establish its own state once.
- Preserve the 1.1.2 manual-registration workaround for Rhino 8.31 and older.
- Keep validation compatible with either an absent pre-first-launch registration or a canonical Package Manager registration after launch.
- Package and stage a corrected version without modifying the RHP currently loaded by Rhino.

## 架构归属

- `Packaging/MCP_Rhino/`: Rhino-version-aware installation and validation policy.
- `Project_Test/260805_TEST_modern-rhino-package-registration/`: isolated registry/package regression coverage.
- No MCP tool surface, Rhino document behavior, Router protocol, or plug-in GUID changes.

## 关键设计

1. Resolve the installed Rhino version from the Rhino executable, with an injectable version used by isolated tests.
2. Treat Rhino 8.32 as the boundary at which the legacy manual registration is no longer required.
3. On modern Rhino, remove only a registration whose name and RHP filename prove it is the MCP_Rhino-owned legacy entry; reject an unrelated owner using the same GUID.
4. On legacy Rhino, retain the exact canonical `LoadMode=1` registration behavior.
5. Modern validation accepts no registration before first launch or a canonical active-path registration written by Rhino after Package Manager completes.
6. Advance the package patch version and stage installation while Rhino or Router is active.

## 涉及文件

- `Packaging/MCP_Rhino/Install-McpRhino.ps1`
- `Packaging/MCP_Rhino/package-manifest.json`
- `Packaging/MCP_Rhino/README.md`
- `Project_Test/260805_TEST_modern-rhino-package-registration/ModernRhinoPackageRegistrationSmoke.ps1`
- `Project_Exet/260805_EXET_modern-rhino-package-registration.md`

## 使用方式

- Build the normal package with `Packaging/MCP_Rhino/Build-McpRhinoPackage.ps1`.
- Run the bundled installer normally. It detects the installed Rhino version automatically.
- Tests inject old and modern Rhino versions to exercise both branches without touching the production Rhino registry.

## 验收标准

- Rhino 8.31 simulation retains the installer-owned canonical startup registration.
- Rhino 8.32+ simulation removes an owned legacy registration and does not recreate it.
- Modern validation accepts both the pre-first-launch absent key and a canonical active-path key, while rejecting a conflicting path or owner.
- Package build and the dedicated installer smoke pass.
- `dotnet build .\MCP_Rhino.sln -c Debug` and `-c Release` pass.
- The corrected production bundle is staged while Rhino remains open; no loaded file is overwritten.

## 风险与回退方案

- Removing the key before Package Manager has run means the plug-in is established on the next Rhino startup rather than by the installer. The versioned package files and `manifest.txt` remain intact and are the authoritative discovery source.
- Older Rhino releases still need the workaround, so the legacy branch is retained and regression-tested.
- Key removal is limited to the canonical plug-in GUID after ownership checks. The current registry entry can be exported before production cleanup, and reinstall/repair can reconstruct the legacy registration when required.
- The prior installer-owned package version remains in rollback storage until the corrected Router validates.

## 后续扩展方向

- Remove the legacy branch after the project formally raises its minimum Rhino version to 8.32 or newer.
- Add a non-UI Rhino restart harness when a supported loader integration runner becomes available.

## 修订记录（2026-08-05）

The 1.1.3 live launch disproved the version-only registration policy. With the manual key absent on
Rhino 8.33, the package RHP still loaded before the `Done installing plug-ins` pass, and the install
pass then rejected the already loaded ID. While the dialog was present, the MCP_Rhino registry key
was absent, proving this was not a second registered path.

Comparison with Rhino's successfully completed `PanelCladdingEditor` package registration exposed the
missing contract: package-installed plug-ins have `DirectoryInstall=1`; MCP_Rhino 1.1.2 had explicitly
written `DirectoryInstall=0`. That shorthand registration loads an AtStartup plug-in but does not tell
Rhino the directory/package installation is complete.

The revised design supersedes the version split:

1. Install and repair always write the canonical AtStartup registration with `DirectoryInstall=1`.
2. Validation requires `LoadMode=1`, `DirectoryInstall=1`, .NET plug-in metadata, and the exact active RHP path.
3. The package version advances to 1.1.4.
4. The dedicated smoke must reproduce and reject the old `DirectoryInstall=0` shorthand state.
5. Live acceptance requires the completed directory registration to remain present and the install dialog to be absent on restart.
