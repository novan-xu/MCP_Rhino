# Registry-Only Plug-in Installation Plan

## 背景

Live acceptance of MCP_Rhino 1.1.2, 1.1.3, and 1.1.4 consistently showed the same sequence: Rhino loaded the sole `MCP_Rhino.Server.rhp` from its current-user Package Manager tree, started the Router endpoint successfully, and then the separate `Done installing plug-ins` pass attempted to load that identical RHP again and reported `ID already in use`.

Neither a shorthand startup registration, removing that registration on Rhino 8.33, nor a completed `DirectoryInstall=1` registration prevented the duplicate package-install pass. The stable root cause is mixed deployment ownership: MCP_Rhino uses Package Manager's discovery directory while also requiring unconditional AtStartup loading for Router endpoint publication.

## 目标

- Give the Rhino plug-in exactly one loader and one installation owner.
- Install the RHP outside every Rhino Package Manager discovery root.
- Preserve automatic `PlugInLoadTime.AtStartup` behavior through one canonical HKCU registration.
- Atomically migrate the owned `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP_Rhino` installation out of Package Manager discovery.
- Keep Router installation, per-document routes, rollback, staging, ownership hashes, and client configuration behavior unchanged.

## 架构归属

- `Packaging/MCP_Rhino/`: registry-only plug-in deployment, migration, validation, and uninstall ownership.
- `Project_Guides/MCP_Rhino Architecture.md`: installation-location contract.
- `Project_Test/260805_TEST_registry-only-plugin-installation/`: isolated migration and duplicate-discovery regression.
- No changes to MCP tools, resources, Router protocol, document routing, or plug-in GUID.

## 关键设计

1. Install versioned plug-in files under `%LOCALAPPDATA%\MCP_Rhino\plugin\<version>`.
2. Register the exact RHP at HKCU with `LoadMode=1`, `IsDotNETPlugIn=1`, and `DirectoryInstall=0`; it is a registry-owned installation, not a Rhino Package Manager directory install.
3. Do not create `manifest.txt` or `manifest.yml` in the installed plug-in tree.
4. On upgrade, accept the prior installer-owned Package Manager path only as a migration source, move it into rollback, and remove the empty legacy MCP_Rhino package root.
5. Search all legacy Package Manager and Rhino plug-in roots for any remaining `MCP_Rhino.Server.rhp`; validation fails if a second discoverable copy remains.
6. Record `pluginRegistrationMode=registry-only` and the legacy package root in the ownership manifest.
7. Update uninstall to remove the owned registration when it still points at the installer-owned RHP.
8. Advance the package version to 1.2.0 because the installation architecture changes.

## 涉及文件

- `Project_Guides/MCP_Rhino Architecture.md`
- `Packaging/MCP_Rhino/Install-McpRhino.ps1`
- `Packaging/MCP_Rhino/Uninstall-McpRhino.ps1`
- `Packaging/MCP_Rhino/package-manifest.json`
- `Packaging/MCP_Rhino/README.md`
- Installer regression scripts under `Project_Test/260804_TEST_*`
- `Project_Test/260805_TEST_registry-only-plugin-installation/`
- `Project_Exet/260805_EXET_registry-only-plugin-installation.md`

## 使用方式

- Build and run the normal MCP_Rhino bundle installer.
- When Rhino or Router is active, installation remains staged.
- After all Rhino/Router processes close, install/repair migrates the old package-discovered RHP and activates the registry-only copy.

## 验收标准

- Debug and Release solution builds pass.
- The isolated migration smoke starts with an owned legacy Package Manager RHP and ends with one RHP under the registry-only root.
- No MCP_Rhino RHP, `manifest.txt`, or version folder remains in the legacy Rhino Package Manager tree.
- Canonical registration points at `%LOCALAPPDATA%\MCP_Rhino\plugin\<version>\MCP_Rhino.Server.rhp` with startup load enabled and directory-package mode disabled.
- Installer validation, direct-RHP identity regression, Router-only upgrade regression, and uninstall regression pass.
- Production 1.2.0 installation validates with exactly one RHP outside Package Manager discovery.
- Two consecutive Rhino launches show no `Done installing plug-ins` duplicate-ID dialog.

## 风险与回退方案

- The location change is an installer migration. Prior owned files move into the existing rollback tree before the new RHP is activated.
- Installation remains blocked/staged while Rhino or Router has files loaded.
- Registry ownership checks reject another product using the canonical GUID.
- If activation fails, the prior RHP remains recoverable from installer rollback but is not left inside a live Package Manager discovery root.

## 后续扩展方向

- Rename bundle terminology from package to distribution in a future cleanup; this plan keeps existing bundle filenames and commands for compatibility.
- Add a supported non-UI Rhino startup harness when available.

## 修订记录（2026-08-05）

The first 1.2.0 launch proved that removing Package Manager discovery eliminated the duplicate-ID
dialog, but the external RHP did not load automatically. The detailed `PlugIn\FileName` subkey alone
does not bootstrap an unregistered external plug-in. Rhino's shorthand registration contract requires
top-level `Name` and `FileName` values; Rhino loads that RHP at startup and then maintains its detailed
registration. Version 1.2.1 adds and validates the top-level `FileName` while retaining the detailed
subkey, registry-only location, and single-loader guarantees.
