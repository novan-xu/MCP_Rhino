# Rhino Plug-in Assembly Identity Plan

## 背景

After `PanelCladdingEditor` was split out of `MCP_Rhino`, every Rhino 8 startup shows the
`Done installing plug-ins` dialog with:

```
Loaded - MCP_Rhino.Server.rhp
Error loading - PanelCladdingEditor.rhp
Unable to load PanelCladdingEditor.rhp plug-in: ID already in use.
```

The registry state is already correct after `260805_PLAN_registry-only-plugin-installation`: exactly
one HKCU registration per product, each pointing at exactly one discoverable RHP.

| HKCU key | Name | FileName |
| --- | --- | --- |
| `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a` | MCP_Rhino | `%LOCALAPPDATA%\MCP_Rhino\plugin\1.2.1\MCP_Rhino.Server.rhp` |
| `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` | PanelCladdingEditor | `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.6\PanelCladdingEditor.rhp` |

So the collision is not a duplicate discovery path. It is a duplicate **runtime** plug-in id.

Decompiling `Rhino.PlugIns.PlugIn::Create` in
`C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll` (via `System.Reflection.Metadata` IL
inspection) shows how RhinoCommon derives a managed plug-in id:

```
Type::get_Assembly
ldtoken System.Runtime.InteropServices.GuidAttribute
Assembly::GetCustomAttributes
GuidAttribute::get_Value
Guid::.ctor          // else -> Guid::Empty
```

`PlugIn::SettingsDirectoryHelper` and `PlugIn::ExtractPlugInAttributes` read the same
**assembly-level** `GuidAttribute`. The `[Guid(...)]` attribute on the plug-in **class** is never
consulted; only `Rhino.Commands.Command` identities come from the type GUID.

Both plug-in projects are SDK-style and have no `AssemblyInfo.cs`. `GenerateAssemblyInfo` emits
`AssemblyTitle`/`AssemblyVersion`/... but never a `GuidAttribute`. Neither
`MCP_Rhino.Server.rhp` nor `PanelCladdingEditor.rhp` carries an assembly-level GUID, so Rhino
assigns `Guid.Empty` to both. Two independent artifacts on the live machine confirm this:

- `HKCU\Software\McNeel\Rhinoceros\8.0\Plug-Ins\00000000-0000-0000-0000-000000000000` with
  `Name=MCP_Rhino.Server` (the `AssemblyTitle` fallback also used by `PlugIn::Create`).
- `settings-Scheme__Default.xml` command counter
  `00000000-0000-0000-0000-000000000000.PanelCladdingEditorSmoke`.

While only one plug-in existed, an empty id collided with nothing. The second plug-in is the first
one to hit `ID already in use`, which is why the failure appeared only after the split.

## 目标

- Make each RHP declare its canonical plug-in id in the location RhinoCommon actually reads.
- Keep `MCP_Rhino` on `7A3FC2F0-24A8-4B79-BE58-5A08CFB0D10A` and `PanelCladdingEditor` on
  `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`, matching the two `package-manifest.json` files and the
  existing HKCU registrations, so no re-registration or user-visible id change is required.
- Correct the identity regression that validated the wrong attribute and therefore passed while the
  shipped plug-in id was empty.
- Remove the stale `Guid.Empty` registration left behind on the live machine.

## 架构归属

- `src/MCP_Rhino.Server/`: owns the MCP_Rhino RHP assembly identity.
- `src/PanelCladdingEditor/`: owns the PanelCladdingEditor RHP assembly identity.
- `Project_Guides/MCP_Rhino Architecture.md`: records the plug-in identity contract.
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/`: identity probe correction.
- `Project_Test/260805_TEST_rhino-plugin-assembly-identity/`: new cross-product identity regression.
- No change to MCP tools, resources, Router protocol, document routing, or installer topology.

## 关键设计

1. Add `src/MCP_Rhino.Server/AssemblyInfo.cs` with
   `[assembly: Guid("7A3FC2F0-24A8-4B79-BE58-5A08CFB0D10A")]`.
2. Add `src/PanelCladdingEditor/AssemblyInfo.cs` with
   `[assembly: Guid("7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35")]`.
3. Keep the existing class-level `[Guid(...)]` on both plug-in classes. It is inert for plug-in id
   resolution but keeps `type.GUID` stable; both sources must state the same value, and the
   regression asserts they agree.
4. `RhpExecutableProbe` must report the assembly-level GUID as `pluginId`, because that is the id
   Rhino uses. Emit `pluginId=<guid>` plus `typeGuid=<guid>` per plug-in row so a mismatch or an
   empty id is visible instead of silently passing.
5. `Verify-DirectRhpPackage.ps1` asserts `pluginId` (not the type GUID) equals
   `package-manifest.json`'s `pluginId`.
6. New `Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1`
   reads both built RHPs with `MetadataLoadContext`-free PE metadata inspection and fails when any
   plug-in id is empty, missing, or shared between the two products.
7. Advance `MCP_Rhino` to 1.2.2 and `PanelCladdingEditor` to 1.0.7 because the shipped plug-in
   binaries change identity-bearing metadata and both installers key replacement off version.
8. Delete the stale `HKCU\...\8.0\Plug-Ins\00000000-0000-0000-0000-000000000000` key on the live
   machine after uninstalling nothing else; it is orphaned state produced by the empty id, not an
   owned registration.

## 涉及文件

- `src/MCP_Rhino.Server/AssemblyInfo.cs` (new)
- `src/PanelCladdingEditor/AssemblyInfo.cs` (new)
- `Packaging/MCP_Rhino/package-manifest.json`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/RhpExecutableProbe/Program.cs`
- `Project_Test/260804_TEST_direct-rhp-plugin-identity/Verify-DirectRhpPackage.ps1`
- `Project_Test/260805_TEST_rhino-plugin-assembly-identity/`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Project_Exet/260805_EXET_rhino-plugin-assembly-identity.md`

## 使用方式

- Rebuild both bundles with the existing `Build-*Package.ps1` scripts and run the existing
  installers. No new commands, tools, or configuration.
- Rhino resolves each plug-in to its declared id on next start; the existing HKCU registrations
  already carry those ids, so no re-registration step is required.

## 验收标准

- `dotnet build .\MCP_Rhino.sln -c Debug` and `-c Release` pass.
- `Verify-PluginAssemblyIdentity.ps1` reports both RHPs with non-empty, distinct plug-in ids equal
  to their manifests.
- `Verify-DirectRhpPackage.ps1` passes against a freshly built 1.2.2 bundle and fails if the
  assembly GUID is removed.
- Both products install and validate.
- Two consecutive Rhino 8 launches load both plug-ins with no `ID already in use` dialog.
- No `00000000-0000-0000-0000-000000000000` plug-in key is recreated after those launches.

## 风险与回退方案

- **Risk**: a wrong assembly GUID would orphan per-plug-in Rhino settings under a new id. Mitigated
  by reusing the ids already recorded in both manifests and both HKCU registrations.
- **Risk**: `MCP_Rhino.Server.csproj` also builds `MCP_Rhino.Server.Runtime.dll` and a CLI host from
  the same sources, so all three carry the attribute. Only `.rhp` files are loaded as Rhino plug-ins,
  so the extra metadata is inert; the regression asserts the RHP specifically.
- **Rollback**: both installers keep versioned rollback state; reinstalling 1.2.1 / 1.0.6 restores
  the previous binaries. Reverting the two `AssemblyInfo.cs` files restores the previous build.

## 后续扩展方向

- Fold the assembly-identity assertion into `Build-McpRhinoPackage.ps1` and
  `Build-PanelCladdingEditorPackage.ps1` so a package can never be produced without a declared id.
- Apply the same assertion to any future third RHP in this repository.
