# Rhino Plug-in Assembly Identity Execution

## 对应计划

- `Project_Plan/260805_PLAN_rhino-plugin-assembly-identity.md`
- 执行日期：2026-08-05

## 关联产物

- `Project_Test/260805_TEST_rhino-plugin-assembly-identity/`
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/` (probe + package regression corrected)
- Bundles: `Packaging/MCP_Rhino/artifacts/MCP_Rhino-1.2.2`,
  `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.7`
- Commit / PR: none yet (working tree change).

## 执行结果 / 实际落地范围

Root cause, confirmed by IL inspection of `Rhino.PlugIns.PlugIn::Create` in
`C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll`: Rhino derives a managed plug-in id from
the **assembly-level** `GuidAttribute` and falls back to `Guid.Empty` when it is absent. Both RHPs
were SDK-style projects with no `AssemblyInfo.cs`, so both shipped without one and both resolved to
`Guid.Empty`. Whichever loaded second failed with `ID already in use`.

Live pre-fix measurement of the previously installed binaries:

```
PLUGIN_ASSEMBLY_IDENTITY|path=...\MCP_Rhino\plugin\1.2.1\MCP_Rhino.Server.rhp|assembly=MCP_Rhino.Server|pluginId=00000000-0000-0000-0000-000000000000|declared=False
PLUGIN_ASSEMBLY_IDENTITY|path=...\PanelCladdingEditor\plugin\1.0.6\PanelCladdingEditor.rhp|assembly=PanelCladdingEditor|pluginId=00000000-0000-0000-0000-000000000000|declared=False
```

The orphaned `HKCU\...\8.0\Plug-Ins\00000000-0000-0000-0000-000000000000` record showed both products
overwriting one another inside a single empty-id registration: `Name=MCP_Rhino.Server` alongside
`Description=Rhino plug-in that auto-updates panel unit attribute user text.` (PanelCladdingEditor's
description).

Landed:

- `src/MCP_Rhino.Server/AssemblyInfo.cs` (new): `[assembly: Guid("7A3FC2F0-24A8-4B79-BE58-5A08CFB0D10A")]`.
- `src/PanelCladdingEditor/AssemblyInfo.cs` (new): `[assembly: Guid("7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35")]`.
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`: removes `AssemblyInfo.cs` from the isolated-runtime
  and CLI-host builds, and excludes the new probe project from the linked `Project_Test` compile glob.
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/RhpExecutableProbe/Program.cs`: PLUGIN rows now
  report `pluginId` (assembly GUID) and `declared`, keeping `typeGuid` visible for divergence.
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/Verify-DirectRhpPackage.ps1`: asserts the
  assembly-level `pluginId` against the manifest and rejects an undeclared id.
- `Project_Test/260805_TEST_rhino-plugin-assembly-identity/`: PE-metadata probe plus cross-product
  regression (non-empty, manifest-matching, mutually distinct ids).
- `Packaging/MCP_Rhino/package-manifest.json` 1.2.1 → 1.2.2;
  `Packaging/PanelCladdingEditor/package-manifest.json` 1.0.6 → 1.0.7.
- `Project_Guides/MCP_Rhino Architecture.md`: assembly-level plug-in identity contract.

## 与计划的偏差

1. The plan placed the attribute in `src/MCP_Rhino.Server/AssemblyInfo.cs` unconditionally and judged
   the extra metadata on `MCP_Rhino.Server.Runtime.dll` inert. It is not: the existing packaging guard
   `Build-McpRhinoPackage.ps1` fails with `The isolated runtime contains the Rhino plug-in GUID.`
   The guard is correct, so the csproj now removes `AssemblyInfo.cs` from the isolated-runtime and
   CLI-host builds instead. Only the RHP carries the id.
2. Additionally fixed a pre-existing defect: `Verify-DirectRhpPackage.ps1`'s default `-TempRoot` was
   `$PSScriptRoot\.validation`, which its own guard rejects, so the script could not run without an
   explicit argument. The default now resolves under the repository `.validation` root.
3. Cleaning the orphaned empty-GUID registration was planned as a manual step; it was executed here
   after backing the key up to the session scratchpad
   (`empty-guid-plugin-key.reg`).

## 施工中发现并修复的问题

- The prior identity regression asserted `type.GUID` — the plug-in **class** attribute, which Rhino
  never reads for plug-in identity. That is why every earlier package passed validation while
  shipping an empty plug-in id. The probe and both assertions now check the attribute Rhino uses.
- `dotnet build .\MCP_Rhino.sln` fails intermittently under default parallelism with
  `MSB3030: Could not copy ... PanelCladdingEditor.dll`, because `PanelCladdingEditorSmoke` rebuilds
  the same project with `PanelCladdingTestHost=true` into the same output folder. Pre-existing and
  unrelated to this change; `-m:1` builds cleanly. Left as-is, noted under 当前遗留项.
- A solution build leaves `src\PanelCladdingEditor\bin\<cfg>\net8.0` holding only the test-host
  `.dll`, so `Verify-PluginAssemblyIdentity.ps1` rebuilds each plug-in project individually to
  restore the `.rhp` before probing.

## 测试记录

Build:

```
dotnet build .\MCP_Rhino.sln -c Debug   -m:1   -> Build succeeded. 0 Warning(s) 0 Error(s)
dotnet build .\MCP_Rhino.sln -c Release -m:1   -> Build succeeded. 0 Warning(s) 0 Error(s)
```

Identity regression:

```
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
[OK] MCP_Rhino|configuration=Release|pluginId=7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a
[OK] PanelCladdingEditor|configuration=Release|pluginId=7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35
[OK] rhino-plugin-assembly-identity|configuration=Release|products=2|distinctPluginIds=2

.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
[OK] MCP_Rhino|configuration=Debug|pluginId=7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a
[OK] PanelCladdingEditor|configuration=Debug|pluginId=7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35
[OK] rhino-plugin-assembly-identity|configuration=Debug|products=2|distinctPluginIds=2
```

Packaged bundle probe (isolated runtime must stay id-free):

```
...\MCP_Rhino-1.2.2\Plugin\MCP_Rhino.Server.rhp          |pluginId=7a3fc2f0-...|declared=True
...\MCP_Rhino-1.2.2\Plugin\MCP_Rhino.Server.Runtime.dll  |pluginId=00000000-...|declared=False
...\PanelCladdingEditor-1.0.7\Plugin\PanelCladdingEditor.rhp |pluginId=7c1a4d3b-...|declared=True
```

Installer regressions:

```
Verify-DirectRhpPackage.ps1 -BundleRoot .\Packaging\MCP_Rhino\artifacts\MCP_Rhino-1.2.2
[OK] direct-rhp-package-regression|installed=...\.validation\direct-rhp-plugin-identity\Product\plugin\1.2.2|duplicateRejected=true|oldVersionDiscoverable=false|canonicalRegistration=true|legacyRegistrySurfaces=false

RegistryOnlyPluginInstallationSmoke.ps1   -> [OK] registry-only-plugin-installation-smoke-test
ModernRhinoPackageRegistrationSmoke.ps1   -> [OK] modern-rhino-package-registration-smoke-test
RouterOnlyInstallerUpgradeSmoke.ps1       -> [OK] router-only-installer-upgrade-smoke-test
PanelRegistryOnlyInstallationSmoke.ps1    -> [OK] panel-registry-only-installation-smoke-test
```

Live installation (Rhino closed; two Claude-Code-owned `MCP_Rhino.Router.exe` processes were stopped
so the Router binary could be replaced — MCP clients relaunch it on next connection):

```
PanelCladdingEditor 1.0.7 installed registry-only at %LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.7
MCP_Rhino 1.2.2 installed; Router installation is valid.

installed identity:
  %LOCALAPPDATA%\MCP_Rhino\plugin\1.2.2\MCP_Rhino.Server.rhp       |pluginId=7a3fc2f0-...|declared=True
  %LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.7\PanelCladdingEditor.rhp |pluginId=7c1a4d3b-...|declared=True

HKCU\...\8.0\Plug-Ins after cleanup:
  7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a | MCP_Rhino           | ...\MCP_Rhino\plugin\1.2.2\MCP_Rhino.Server.rhp
  7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35 | PanelCladdingEditor | ...\PanelCladdingEditor\plugin\1.0.7\PanelCladdingEditor.rhp
  (00000000-0000-0000-0000-000000000000 removed)
```

## 验收判据对齐

| 判据 | 结果 |
| --- | --- |
| Debug 与 Release 方案构建通过 | 通过（`-m:1`，见 当前遗留项） |
| 两个 RHP 声明非空、互异、与清单一致的 plug-in id | 通过 |
| `Verify-DirectRhpPackage.ps1` 对 1.2.2 通过 | 通过 |
| 两个产品安装并验证 | 通过 |
| 连续两次 Rhino 启动无 `ID already in use` | **未验证**，需用户启动 Rhino 复核 |
| 不再重建空 GUID 注册项 | **未验证**，同上 |

## 回退验证

- Both installers retain versioned rollback state: `%LOCALAPPDATA%\MCP_Rhino\rollback\...\Plugin-1.2.1`
  and `%LOCALAPPDATA%\PanelCladdingEditor\rollback\...\prior-plugin-1.0.6`. Reinstalling the prior
  bundles restores the previous binaries.
- The deleted empty-GUID registry key was exported before removal to the session scratchpad
  `empty-guid-plugin-key.reg` and can be re-imported with `reg import`.
- Reverting the two `AssemblyInfo.cs` files and the csproj `Compile Remove` block restores the
  previous build exactly.

## 当前遗留项

1. Live Rhino confirmation is outstanding: launch Rhino 8 twice and confirm both plug-ins load with
   no `ID already in use` dialog and no `00000000-...` key reappears.
2. `dotnet build .\MCP_Rhino.sln` remains racy at default parallelism because
   `PanelCladdingEditorSmoke` rebuilds `PanelCladdingEditor` into the same output directory with a
   different `TargetExt`. Pre-existing; deserves its own plan (separate output path for the test host).
3. The identity assertion is not yet wired into `Build-McpRhinoPackage.ps1` /
   `Build-PanelCladdingEditorPackage.ps1`, so a future package could still be produced without a
   declared id if someone deletes `AssemblyInfo.cs`. Listed in the plan's 后续扩展方向.
4. Per-plug-in Rhino settings previously written under the empty id (for example the
   `00000000-0000-0000-0000-000000000000.PanelCladdingEditorSmoke` command counter in
   `settings-Scheme__Default.xml`) are not migrated. They are counters only; Rhino will recreate them
   under the correct ids.

## 结论

The `ID already in use` failure was a duplicate **runtime** plug-in id, not a duplicate installation
path: neither RHP declared an assembly-level `GuidAttribute`, so RhinoCommon assigned `Guid.Empty` to
both, and the second plug-in to load was rejected. Both products now declare their canonical ids,
which already match their manifests and their HKCU registrations, so no re-registration was needed.
The regression that should have caught this was asserting the plug-in class GUID that Rhino ignores;
it now asserts the assembly attribute Rhino actually reads. All static and installer validation
passes; live Rhino startup confirmation remains outstanding.
