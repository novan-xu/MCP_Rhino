# Direct RHP Plug-in Identity Execution Report

## 对应计划

- Plan: `Project_Plan/260804_PLAN_direct-rhp-plugin-identity.md`
- Execution date: 2026-08-04

## 关联产物

- Test folder: `Project_Test/260804_TEST_direct-rhp-plugin-identity/`
- Package bundle: `.validation/direct-rhp-package/MCP_Rhino-1.1.0/`
- Production stage: `%USERPROFILE%\AppData\Local\MCP_Rhino\staged\1.1.0-20260804205619580\`
- Installed plug-in: `%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\MCP_Rhino\1.1.0\`
- Commit / PR: none created in this execution.

## 执行结果 / 实际落地范围

- `MCP_Rhino.Server` now builds its normal Debug and Release output directly as `MCP_Rhino.Server.rhp`; the post-build DLL copy was removed.
- The development CLI fallback is explicit through `McpRhinoCliHost=true`, which emits `MCP_Rhino.Server.dll` only for a developer build.
- The isolated MCP host is now a separately named `MCP_Rhino.Server.Runtime.dll` variant. Its compile excludes `Program.cs`, the Rhino `PlugIn` entry, all Rhino `Command` types, the plug-in load context, and the default-context route dispatcher.
- A runtime-only `McpRhinoPlugin.Instance` sentinel preserves historical smoke-mode detection without inheriting Rhino `PlugIn` or carrying the plug-in GUID.
- `McpRhinoPlugin.OnLoad` now loads `MCP_Rhino.Server.Runtime.dll` instead of loading a second copy of `MCP_Rhino.Server.dll`.
- Shared live-service registration was extracted to `PluginServiceRegistration`, allowing both the default RHP and isolated runtime to construct the same Router-only host without referencing the Rhino plug-in type across the load-context boundary.
- Package version was advanced to `1.1.0`.
- The package builder now combines a direct Release RHP build with the isolated Runtime publish, excludes the development apphost, and rejects duplicate or inconsistent assembly identities.
- The installer validates the same identity contract for bundle, staging, installation, and repair/validate operations.
- Version upgrades move the prior installer-owned plug-in version out of Rhino's package discovery tree before activating the new version.
- When Rhino is active, staging now removes superseded installer-owned MCP_Rhino stages before creating one replacement stage.
- Added `_McpDirectRhpPluginIdentitySmoke` and the unique `direct-rhp-plugin-identity-smoke-test` developer slug.

## 与计划的偏差

The original plan anticipated removing `MCP_Rhino.Server.dll` from the package after proving direct RHP output. Inspection during execution found that the plug-in deliberately loaded that same DLL into an isolated `AssemblyLoadContext` to avoid Rhino's older `System.Text.Json` dependency graph. Removing the file alone would therefore have broken plug-in startup.

The final implementation preserves that isolation boundary but gives the isolated host its own assembly identity and removes all Rhino plug-in/command entry types from it. This is stricter than the initial packaging-only correction and directly removes the source of the GUID collision.

## 施工中发现并修复的问题

- A minimal probe proved that `OutputType=Exe` can compile directly to `.rhp`, but neither the generated apphost nor `dotnet exec` can launch a managed `.rhp`. The explicit `McpRhinoCliHost=true` DLL output was added for development smoke fallback.
- `plugin.manifest` initially copied under `Infrastructure/Plugin/` when an isolated output path was used. Its MSBuild `TargetPath` is now the output root.
- Windows PowerShell does not expose the two-argument `String.Contains` overload used in the first package assertion draft. The package and installer checks now use `String.IndexOf(..., StringComparison)`.
- Historical smoke partials referenced `McpRhinoPlugin.Instance`, so removing the plug-in type from the Runtime variant initially failed compilation in 26 files. The GUID-free runtime sentinel preserves the mode-check contract.
- A normal full-solution rebuild encountered the already-loaded standalone editor PDB lock. An isolated-artifacts solution build avoided the live file.
- Parallel isolated Release validation then exposed a pre-existing mixed-output race where the standalone editor is built directly as `.rhp` and through its smoke host as `.dll`. Serial solution validation (`-m:1`) passed in both configurations without changing that working plug-in.

## 测试记录

### Direct-RHP executable probe

- Default probe build emitted `RhpExecutableProbe.rhp` successfully.
- `dotnet run` against the direct RHP shape failed with `Failed to create CoreCLR, HRESULT: 0x80070057`, proving that a separate developer CLI output selection is required.
- `dotnet run -p:McpRhinoCliHost=true` emitted and executed `RhpExecutableProbe.dll` successfully.
- A following direct `Rebuild` restored `.rhp` output and removed the sibling DLL.

### Server output variants

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c Debug --nologo` -> exit 0, 0 warnings, direct RHP.
- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c Release --nologo` -> exit 0, 0 warnings, direct RHP.
- Runtime variant build with `McpRhinoIsolatedRuntime=true` -> exit 0, 0 warnings.
- Runtime assembly identity: `MCP_Rhino.Server.Runtime, Version=1.0.0.0`.
- Runtime binary plug-in GUID scan: `False`.

### Debug / Release solution validation

The live standalone editor locked its normal output, so the final full solution commands used isolated artifacts and serial execution:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo `
  --artifacts-path .validation/direct-rhp-solution-debug-serial -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo `
  --artifacts-path .validation/direct-rhp-solution-release-serial -m:1
```

- Debug -> exit 0, 0 warnings, 0 errors.
- Release -> exit 0, 0 warnings, 0 errors.
- Both Server outputs contained `MCP_Rhino.Server.rhp`, no `MCP_Rhino.Server.dll`, and dependency metadata named the `.rhp` runtime asset.

### Developer CLI and Router regressions

Isolated Debug and Release CLI hosts were built with `McpRhinoCliHost=true`.

- `mcp-tool-safety-annotations-smoke-test` -> exit 0 in Debug and Release; 159 tools verified; no bare method-level attributes.
- `multi-document-rhino-router-smoke-test` -> exit 0 in Debug and Release; all discovery, pagination, attestation, selection, conflict, stale-descriptor, and concurrent-client checkpoints passed.
- `direct-rhp-plugin-identity-smoke-test <bundle Plugin directory>` -> exit 0; RHP identity `MCP_Rhino.Server`, runtime identity `MCP_Rhino.Server.Runtime`.

### Package and isolated installer regression

- `Build-McpRhinoPackage.ps1 -OutputRoot .validation/direct-rhp-package` -> exit 0.
- Corrected plug-in self files:
  - `MCP_Rhino.Server.rhp`
  - `MCP_Rhino.Server.deps.json`
  - `MCP_Rhino.Server.runtimeconfig.json`
  - `MCP_Rhino.Server.Runtime.dll`
  - `MCP_Rhino.Server.Runtime.deps.json`
  - no `MCP_Rhino.Server.dll`
  - no `MCP_Rhino.Server.exe`
- `Verify-DirectRhpPackage.ps1` -> exit 0.
- Synthetic owned 1.0.0 plug-in version was moved out of the Rhino package tree during upgrade.
- Installed 1.1.0 Router `--validate-install` passed.
- Installer `Validate` passed.
- A tampered bundle containing a copied `MCP_Rhino.Server.dll` was rejected.

### Production staging

- Rhino PID `31908` was active, so the installer staged rather than replacing loaded files.
- Four prior staged bundles were removed as superseded installer-owned stages.
- Exactly one stage remains: `1.1.0-20260804205619580`.
- Stage audit: RHP present, Runtime DLL present, duplicate Server DLL absent.
- Rhino and Router subsequently exited, clearing the deployment lock.

### Production installation

- The staged `Install-McpRhino.ps1` completed successfully.
- Installed ownership manifest version: `1.1.0`.
- Installed Router `--validate-install`: passed.
- Installer `Validate`: passed.
- Installed RHP: present.
- Installed isolated Runtime DLL: present.
- Installed duplicate `MCP_Rhino.Server.dll`: absent.
- Rhino package manifest version: `1.1.0`.
- Recursive audit of the Rhino MCP_Rhino package root found exactly one discoverable RHP, under `1.1.0`; the owned 1.0.0 version is no longer in Rhino's discovery tree.

## 验收判据对齐

- Direct Debug/Release RHP builds: met.
- No sibling Server DLL in production output/package: met.
- Isolated host has a distinct assembly identity and no Rhino plug-in GUID: met.
- RHP and Runtime dependency metadata consistency: met.
- Development CLI fallback preserved: met.
- Router-only transport behavior unchanged: met by Debug/Release Router protocol smoke.
- Tool safety regression: met in Debug and Release.
- Package install/repair validation and negative duplicate rejection: met in isolated roots.
- Safe production deployment and installation: met; deployment waited until Rhino and Router exited, then installed and validated 1.1.0.
- Post-restart live Rhino smoke and absence of the load dialog: pending until the user launches Rhino with the newly installed package.

## 回退验证

- Installer upgrade testing confirmed that the prior owned version moves under the installer rollback tree rather than remaining discoverable by Rhino.
- The installed live 1.0.0 package was not modified while Rhino held it.
- After Rhino exited, the installer moved the owned 1.0.0 version out of Rhino's discovery tree before activating 1.1.0.
- The prior version remains recoverable only through the installer-owned rollback record and cannot be discovered by Rhino as an installed package.

## 当前遗留项

- Restart Rhino, confirm no `ID already in use` error, and run `_McpDirectRhpPluginIdentitySmoke`.
- Append those live deployment results to this EXET.

## 结论

The duplicate plug-in identity is removed in code and packaging. The corrected Router-only 1.1.0 bundle passed automated regression, isolated upgrade/installation, production installation, package-tree audit, and installed validation. A fresh Rhino launch is the only remaining live confirmation.

## Live acceptance failure and 1.1.1 correction

### Failed acceptance evidence

On the first production launch of 1.1.0, Rhino still displayed `Unable to load MCP_Rhino.Server.rhp plug-in: ID already in use.` The live process nevertheless loaded the 1.1.0 RHP, loaded the GUID-free Runtime DLL, and published a READY per-document route. Registry and filesystem audits found no second GUID-bearing assembly or registered path. This proved that the first RHP load succeeded and Rhino rejected a redundant registration attempt.

A PE-level comparison then found the remaining nonstandard difference:

- installed MCP_Rhino 1.1.0 RHP managed entry point token: `100663317`;
- working PanelCladdingEditor 1.0.5 RHP managed entry point token: `0`.

The MCP_Rhino project still used `OutputType=Exe` for its production `.rhp` so `Program.cs` could remain directly runnable. File-identity tests did not detect this hybrid executable/plugin shape, and no prior test launched the final package through Rhino's real plug-in loader.

### 1.1.1 correction

- Production Debug and Release RHP builds now use `OutputType=Library` and exclude `Program.cs`.
- `McpRhinoCliHost=true` remains the only executable build and emits the developer-only DLL.
- The nonstandard root `plugin.manifest` is no longer copied or packaged; installed identity metadata is limited to the RHP and standard Yak `manifest.yml`.
- Package and installer validation now parse the PE CLI header and reject any production RHP or isolated Runtime with a nonzero managed entry point.
- Package version advanced to `1.1.1`.

### 1.1.1 validation

- Debug production RHP entry point token: `0`.
- Release production RHP entry point token: `0`.
- Developer CLI DLL entry point token: `100663317`.
- Debug and Release full solution builds passed with 0 warnings and 0 errors using isolated serial artifact roots.
- MCP tool safety smoke passed for 159 tools.
- Multi-document Router protocol smoke passed all checkpoints.
- 1.1.1 package build passed the new PE contract.
- Isolated 1.0.0-to-1.1.1 upgrade, installed validation, Router validation, duplicate-DLL rejection, and old-version discovery cleanup passed.
- Final package contains `MCP_Rhino.Server.rhp` plus `MCP_Rhino.Server.Runtime.dll`, no `MCP_Rhino.Server.dll`, no `MCP_Rhino.Server.exe`, and no `plugin.manifest`.

### Deployment state

- The user closed Rhino, and no Rhino or Router process remained during deployment.
- The corrected staged bundle `%USERPROFILE%\AppData\Local\MCP_Rhino\staged\1.1.1-20260804211328335\` installed successfully.
- Installed ownership manifest and Yak package version: `1.1.1`.
- Installed Router validation: passed.
- Installer `Validate`: passed.
- The installed plug-in root contains the production RHP and GUID-free isolated Runtime DLL, with no sibling `MCP_Rhino.Server.dll`, no executable host, and no custom `plugin.manifest`.
- A recursive package-tree audit found exactly one discoverable MCP_Rhino RHP: `%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\MCP_Rhino\1.1.1\MCP_Rhino.Server.rhp`.
- Required final action: restart Rhino and confirm the real loader no longer displays the duplicate-ID dialog.

The earlier conclusion is superseded: 1.1.0 failed live Rhino acceptance because it was still an executable RHP. Version 1.1.1 corrects that production output shape and is installed and statically validated. Fresh Rhino startup remains the final live acceptance check.

## Rhino 8.29 package-registration failure and 1.1.2 correction

### 1.1.1 live evidence

The 1.1.1 restart still displayed `ID already in use`, but the Rhino command history established the
actual sequence:

1. `MCP_Rhino direct RHP plugin startup beginning`;
2. the isolated Runtime and shared dependencies loaded;
3. the routed endpoint dispatcher started;
4. `MCP_Rhino Router-only plugin loaded for Rhino PID 43840`;
5. Rhino's separate `Done installing plug-ins` pass then reported the same RHP as an error.

The process contained the canonical 1.1.1 RHP, the GUID-free Runtime DLL, and Transport DLL. The Rhino
launch command contained only the target 3DM, the package tree contained one RHP, no matching plug-in
registry key existed while the install dialog was pending, and compiled identity enumeration found one
explicit plug-in GUID plus 29 unique implicit developer command GUIDs. This ruled out a second file,
second plug-in type, launch argument, or internal GUID collision.

Installed Rhino version `8.29.26063.11001` predates McNeel's RH-94972 package-update ordering fix,
released in 8.32 RC1. In this failure mode an `AtStartup` package plug-in loads before the pending
package-install pass, then the install pass attempts to register the same already loaded ID.

### 1.1.2 implementation

- Production RHP builds exclude every `Infrastructure\Plugin\*Command.cs` entry type. The RHP is now
  an automatic Router host only and registers no smoke/developer Rhino commands.
- The installer owns the exact HKCU Rhino registration for plug-in ID
  `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`.
- Install/repair writes `LoadMode=1`, .NET utility plug-in metadata, and the exact active versioned
  `PlugIn\FileName` while Rhino is closed.
- Install/repair removes only the MCP_Rhino registration's retired `CommandList` and `Panels` subkeys.
- Validation now requires the canonical registration and rejects a stale RHP path, non-startup load
  mode, command registration, or panel registration.
- Non-production filesystem roots must use a non-production injected registry root, preventing isolated
  tests from touching the real Rhino registration.
- Package version advanced to `1.1.2`.

### 1.1.2 validation and installation

- Direct Debug and Release server builds passed with 0 warnings and 0 errors.
- Full Debug and Release solution builds passed with 0 warnings and 0 errors using isolated artifacts.
- Compiled packaged-RHP enumeration found exactly one canonical plug-in type and zero Rhino command types.
- MCP safety annotations passed for 159 tools in Debug and Release CLI hosts.
- Multi-document Router protocol smoke passed every discovery, selection, conflict, attestation, stale
  descriptor, and concurrent-client checkpoint.
- Router-only static and simulated legacy installer-upgrade smokes passed.
- The isolated registration regression replaced a retired path, set `LoadMode=1`, removed synthetic
  `CommandList` and `Panels`, validated the exact active RHP, and rejected a tampered sibling DLL.
- Version 1.1.2 installed after Rhino exited; installed Router validation and installer `Validate` passed.
- The live package tree contains exactly one RHP under `MCP_Rhino\1.1.2`.
- The production Rhino registration contains only its `PlugIn` subkey and points exactly to the 1.1.2 RHP.

Fresh Rhino startup without the package-install error remains the final live acceptance check.
