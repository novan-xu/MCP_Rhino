# Modern Rhino Package Registration Execution Report

## 对应计划

- Plan: `Project_Plan/260805_PLAN_modern-rhino-package-registration.md`
- Execution date: 2026-08-05

## 关联产物

- Test folder: `Project_Test/260805_TEST_modern-rhino-package-registration/`
- Validation bundle: `.validation/modern-registration-package/MCP_Rhino-1.1.3/`
- Production stage: `%LOCALAPPDATA%\MCP_Rhino\staged\1.1.3-20260805135833122\`
- Commit / PR: none created.

## 执行结果 / 实际落地范围

- Added installed-Rhino version resolution to the MCP_Rhino installer, with an injectable version for isolated tests.
- Rhino 8.31 and older retain the installer-owned `LoadMode=1` registration workaround.
- Rhino 8.32 and newer remove an owned legacy registration and allow Rhino Package Manager to own registration.
- Modern validation accepts either an absent registration before the first Rhino launch or a canonical registration pointing at the active RHP after Package Manager runs.
- Conflicting ownership and stale registered RHP paths remain fail-closed.
- The ownership manifest records the detected Rhino version and registration mode.
- Package version advanced from 1.1.2 to 1.1.3.
- The 1.1.3 bundle was staged because Rhino and Router were active; no loaded production file or registry entry was modified.

## 与计划的偏差

- None. The implementation follows the planned version-aware legacy/modern split.

## 施工中发现并修复的问题

- Existing historical installer tests implicitly used the workstation's Rhino version. They now inject Rhino 8.31 for the legacy registration contract and Rhino 8.33 for the modern Router-only upgrade contract, making both expectations deterministic.
- Live inspection showed the workstation had upgraded from the 8.29 environment documented by the 1.1.2 workaround to Rhino 8.33. The installed workaround therefore outlived the defect it addressed.

## 测试记录

### PowerShell parsing and diff hygiene

- `Install-McpRhino.ps1`: 0 parser errors.
- `Build-McpRhinoPackage.ps1`: 0 parser errors.
- `ModernRhinoPackageRegistrationSmoke.ps1`: 0 parser errors.
- `git diff --check`: exit 0.

### Required solution builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo --artifacts-path .validation\modern-registration-debug -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo --artifacts-path .validation\modern-registration-release -m:1
```

- Debug: exit 0, 0 warnings, 0 errors.
- Release: exit 0, 0 warnings, 0 errors.
- Both configurations emitted `MCP_Rhino.Server.rhp`.

### Package build

```powershell
.\Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -OutputRoot .validation\modern-registration-package
```

- Exit 0.
- Output: `.validation/modern-registration-package/MCP_Rhino-1.1.3/`.

### Dedicated registration smoke

```powershell
.\Project_Test\260805_TEST_modern-rhino-package-registration\ModernRhinoPackageRegistrationSmoke.ps1 `
  -BundleRoot .validation\modern-registration-package\MCP_Rhino-1.1.3
```

- Exit 0.
- Rhino 8.33 simulation removed the owned legacy registration.
- Modern validation accepted absent and canonical active-path registration states.
- Modern validation rejected a stale RHP path.
- Rhino 8.31 simulation retained and validated the installer-owned startup registration.

### Existing packaging regressions

- `Verify-DirectRhpPackage.ps1`: exit 0; direct RHP identity, legacy registration, old-version cleanup, and duplicate DLL rejection passed.
- `RouterOnlyInstallerUpgradeSmoke.ps1`: exit 0; Router-only legacy cleanup and installed validation passed.

### Production staging

```powershell
.\.validation\modern-registration-package\MCP_Rhino-1.1.3\Installer\Install-McpRhino.ps1
```

- Exit 0.
- Rhino was active, so the installer staged the bundle at `%LOCALAPPDATA%\MCP_Rhino\staged\1.1.3-20260805135833122\`.
- Installer output confirmed that no installed component was replaced.

## 验收判据对齐

- Version-aware legacy and modern behavior: met by isolated smoke.
- Ownership-safe removal: met by the owned-registration fixture and fail-closed checks.
- Modern pre/post-first-launch validation: met.
- Debug and Release builds: met.
- Package and historical installer regressions: met.
- Safe production staging while Rhino is open: met.
- Production activation and clean Rhino restart: pending user closure of Rhino and Router.

## 回退验证

- The production installation remains at 1.1.2 and was not modified while loaded.
- Version 1.1.3 exists only in the installer-owned staging root until activation.
- Normal installation retains the prior owned package under rollback storage until the replacement Router validates.

## 当前遗留项

- Start Rhino once so Package Manager establishes the modern registration, then restart once more to confirm the install dialog no longer recurs.

## 结论

The permanent version-aware fix is implemented, fully validated in isolated builds/tests, and installed in production. Static installed-state validation passes; a Rhino launch/restart remains for final live acceptance.

## Production activation and installed validation

- The user closed Rhino after saving work.
- The active MCP_Rhino Router was stopped; after its process finished, the staged installer ran with no Rhino or Router process active.
- Staged `Install-McpRhino.ps1` exit: 0.
- Installed version: `1.1.3`.
- Detected Rhino version: `8.33.26188.13001`.
- Recorded registration mode: `package-manager`.
- Installer `-Mode Validate`: exit 0.
- Installed Router `--validate-install`: passed.
- The obsolete installer-owned plug-in registry key is absent.
- Active package manifest selects `1.1.3`.
- Recursive live package audit found exactly one discoverable RHP:
  `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP_Rhino\1.1.3\MCP_Rhino.Server.rhp`.

## 1.1.3 live acceptance failure

- Rhino 8.33 still displayed `ID already in use` on the first 1.1.3 launch.
- While the dialog was present, the MCP_Rhino RHP had already loaded and the canonical plug-in registry key was absent.
- This disproved the assumption that removing the 1.1.2 registration would let the package-install pass run first.
- The loaded package was still discovered as an AtStartup directory plug-in before Rhino's separate install pass.
- A completed successful package registration (`PanelCladdingEditor`) had `DirectoryInstall=1`; the 1.1.2 MCP_Rhino workaround had explicitly written `DirectoryInstall=0`.
- The version-only approach is superseded by the 1.1.4 completed-directory registration correction below.

## 1.1.4 completed-directory registration correction

- Installer registration now sets `DirectoryInstall=1` while preserving `LoadMode=1`, `IsDotNETPlugIn=1`, and the exact active RHP path.
- Installer validation rejects a missing registration, `DirectoryInstall=0`, a stale RHP path, or inconsistent .NET/startup metadata.
- The ownership manifest records `pluginRegistrationMode=directory-install`.
- The Rhino-version branch and injected version parameter were removed because the completed directory-registration contract applies across supported Rhino 8 releases.
- Package version advanced to 1.1.4.

### 1.1.4 validation

- Debug solution build: exit 0, 0 warnings, 0 errors.
- Release solution build: exit 0, 0 warnings, 0 errors.
- Package build: exit 0; output `.validation/directory-registration-package/MCP_Rhino-1.1.4/`.
- Dedicated registration smoke: exit 0; completed registration written and validated; shorthand `DirectoryInstall=0` and stale-path states rejected.
- Direct-RHP package regression: exit 0; canonical directory registration, identity, upgrade cleanup, and duplicate-DLL rejection passed.
- Router-only installer upgrade smoke: exit 0.
- Production stage: `%LOCALAPPDATA%\MCP_Rhino\staged\1.1.4-20260805141443959\`.
- Rhino was active, so 1.1.4 was staged without replacing the loaded 1.1.3 installation.

### 1.1.4 production activation

- The user closed both active Rhino processes.
- The staged installer completed with exit 0 while no Rhino or Router process was active.
- Installed version: 1.1.4.
- Installer `-Mode Validate`: exit 0.
- Installed Router validation: passed.
- Installed-file integrity failures: 0.
- Canonical registration: `LoadMode=1`, `DirectoryInstall=1`, `IsDotNETPlugIn=1`.
- Registered RHP: `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP_Rhino\1.1.4\MCP_Rhino.Server.rhp`.
- Recursive package audit found that same single discoverable RHP and no older active version.
- Remaining acceptance: start Rhino normally and confirm the `Done installing plug-ins` / duplicate-ID dialog is absent.

### 1.1.4 live acceptance failure

- Rhino still displayed `ID already in use` with the completed `DirectoryInstall=1` registration.
- Live inspection confirmed one loaded RHP and the matching canonical registration, both pointing into the Rhino Package Manager tree.
- This disproved registration-shape corrections as a sufficient fix. The stable conflict is mixed ownership of an AtStartup RHP by Package Manager discovery and the startup registry.
- The capability is superseded by `260805_PLAN_registry-only-plugin-installation.md`, which removes the RHP from Package Manager discovery entirely.
