# Registry-Only Plug-in Installation Execution Report

## 对应计划

- Plan: `Project_Plan/260805_PLAN_registry-only-plugin-installation.md`
- Execution date: 2026-08-05

## 关联产物

- Test folder: `Project_Test/260805_TEST_registry-only-plugin-installation/`
- Validation bundle: `.validation/registry-only-package/MCP_Rhino-1.2.0/`
- Production stage: `%LOCALAPPDATA%\MCP_Rhino\staged\1.2.0-20260805143438037\`
- Commit / PR: none created.

## 执行结果 / 实际落地范围

- Changed the installation architecture from mixed Rhino Package Manager plus registry ownership to registry-only ownership.
- Versioned RHP files now install under `%LOCALAPPDATA%\MCP_Rhino\plugin\<version>`.
- The canonical HKCU registration points to that exact RHP with `LoadMode=1`, `IsDotNETPlugIn=1`, and `DirectoryInstall=0`.
- The installer no longer writes Package Manager `manifest.txt` or `manifest.yml` metadata.
- Upgrade accepts the prior installer-owned `%APPDATA%\...\packages\8.0\MCP_Rhino\<version>` only as a migration source, moves owned files into rollback, and removes the empty legacy product root.
- Validation requires exactly one RHP under the registry-only plug-in base and rejects any RHP or active manifest rediscovered under the legacy Package Manager product root.
- Uninstall removes the registry key only when it still points at the installer-owned RHP.
- Architecture and packaging documentation now require the single-loader registry-only deployment.
- Package version advanced to 1.2.0.

## 与计划的偏差

- None. The implementation follows the planned single-loader migration.

## 施工中发现并修复的问题

- The historical Router-only legacy fixture did not record `pluginRoot`; migration correctly rejected its synthetic RHP as unowned. The fixture now records ownership, matching real installer manifests.
- Validation was strengthened to scan the entire registry-only plug-in base, preventing an older version from remaining alongside the active version.

## 测试记录

### Required Debug / Release builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo --artifacts-path .validation\registry-only-debug -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo --artifacts-path .validation\registry-only-release -m:1
```

- Debug: exit 0, 0 warnings, 0 errors.
- Release: exit 0, 0 warnings, 0 errors.

### Package build

```powershell
.\Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -OutputRoot .validation\registry-only-package
```

- Exit 0.
- Output: `.validation/registry-only-package/MCP_Rhino-1.2.0/`.

### Registry-only migration and uninstall smoke

- Exit 0.
- Migrated a synthetic owned 1.1.4 RHP out of the legacy Package Manager tree.
- Installed one 1.2.0 RHP under the registry-only product root.
- Removed the legacy MCP_Rhino Package Manager product root.
- Verified startup registration, `DirectoryInstall=0`, exact RHP path, and ownership-manifest mode.
- Installer validation passed.
- A reintroduced legacy Package Manager RHP was rejected.
- Uninstall removed the owned registry key, ownership manifest, and registry-only plug-in tree.

### Existing regressions

- Direct-RHP package regression: exit 0; identity, migration, exact registration, old-version cleanup, and duplicate-DLL rejection passed.
- Router-only installer upgrade smoke: exit 0; legacy Bridge/Companion cleanup and installed validation passed.
- Superseded registration smoke: exit 0 after updating its expectations to registry-only mode.

### Production staging

- Installer exit 0.
- Rhino was active, so no loaded file or installed registration was changed.
- Bundle staged at `%LOCALAPPDATA%\MCP_Rhino\staged\1.2.0-20260805143438037\`.

## 验收判据对齐

- Single registry-owned RHP location: met in isolated migration.
- No Package Manager metadata or RHP after migration: met in isolated migration.
- Exact startup registration outside Package Manager: met.
- Debug/Release builds: met.
- Packaging, Router, negative duplicate, and uninstall regressions: met.
- Safe production staging: met.
- Production activation and two clean Rhino launches: pending closure of active Rhino/Router processes.

## 回退验证

- Migration moves the prior owned plug-in into installer rollback before activating the new RHP.
- The production 1.1.4 installation remains untouched while Rhino is open.
- Isolated uninstall proved that only installer-owned registration/files are removed.

## 当前遗留项

- Close every Rhino instance and active MCP_Rhino Router.
- Run the staged 1.2.0 installer and validate exact registry-only state.
- Launch Rhino twice and confirm the plug-in install dialog is absent both times.

## 结论

The dual-loader installation architecture has been removed in code and fully validated in isolated migration, regression, negative, and uninstall tests. Production 1.2.0 is staged and awaits a safe process-free activation.

## Production activation

- The user closed the remaining Rhino process.
- Staged 1.2.0 installer exit: 0.
- Installer `-Mode Validate`: exit 0.
- Installed Router validation: passed.
- Installed version: 1.2.0.
- Registration mode: `registry-only`.
- Plug-in root: `%LOCALAPPDATA%\MCP_Rhino\plugin\1.2.0`.
- Canonical registration: `LoadMode=1`, `DirectoryInstall=0`, exact 1.2.0 RHP path.
- Legacy `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP_Rhino` root: absent.
- Recursive audit across Rhino Package Manager, Rhino plug-in, and MCP_Rhino registry-only roots found exactly one `MCP_Rhino.Server.rhp`.
- Installed-file integrity failures: 0.
- Remaining live acceptance: launch Rhino twice and confirm no duplicate-ID/install dialog.

## 1.2.0 startup-bootstrap failure and 1.2.1 correction

- The 1.2.0 launch showed no duplicate-ID dialog, proving Package Manager removal fixed the original conflict.
- Live process inspection showed no MCP_Rhino module and no route descriptor, so the plug-in was not automatically loaded.
- Registration contained the detailed `PlugIn\FileName` subkey but lacked the top-level shorthand `FileName` value required to bootstrap the first load of an external RHP.
- The live 1.2.0 key was repaired with top-level `Name` and exact `FileName` values.
- Installer 1.2.1 now writes and validates both the shorthand top-level `FileName` and detailed `PlugIn\FileName` paths.
- Package version advanced to 1.2.1.

### 1.2.1 validation and staging

- Debug solution build: exit 0, 0 warnings, 0 errors.
- Release solution build: exit 0, 0 warnings, 0 errors.
- Package build: exit 0; `.validation/startup-bootstrap-package/MCP_Rhino-1.2.1/`.
- Registry-only migration/uninstall smoke: exit 0; shorthand and detailed paths both matched the sole RHP.
- Direct-RHP package regression: exit 0.
- Router-only upgrade regression: exit 0.
- Production stage: `%LOCALAPPDATA%\MCP_Rhino\staged\1.2.1-20260805144048116\`.
- Rhino remained open, so installed 1.2.0 files were not replaced.

### 1.2.1 production activation

- The user closed Rhino and Router processes.
- Staged installer exit: 0.
- Installer `-Mode Validate`: exit 0.
- Installed Router validation: passed.
- Installed version: 1.2.1.
- Top-level shorthand `FileName` and detailed `PlugIn\FileName` both point to
  `%LOCALAPPDATA%\MCP_Rhino\plugin\1.2.1\MCP_Rhino.Server.rhp`.
- Registration remains `LoadMode=1`, `DirectoryInstall=0`.
- Legacy Package Manager MCP_Rhino root: absent.
- Registry-only plug-in RHP count: 1.
- Remaining acceptance: launch Rhino with the target saved document, verify the RHP and route load automatically, then perform a reversible MCP mutation.
