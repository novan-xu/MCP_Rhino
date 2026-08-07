# Rhino Plug-in Assembly Identity Tests

Regression for `Project_Plan/260805_PLAN_rhino-plugin-assembly-identity.md`.

## Why

`Rhino.PlugIns.PlugIn.Create` resolves a managed plug-in id from the **assembly-level**
`GuidAttribute` and falls back to `Guid.Empty` when it is absent. The `[Guid(...)]` attribute on the
plug-in class is not used for plug-in identity — only `Rhino.Commands.Command` ids come from the type
GUID. Two SDK-style RHPs without an assembly GUID therefore both resolve to `Guid.Empty`, and Rhino
rejects the second one with `Unable to load <name>.rhp plug-in: ID already in use.`

## Contents

- `PluginAssemblyIdentityProbe/` — reads the assembly-level `GuidAttribute` straight from PE
  metadata. No Rhino install, no assembly loading, so it runs on any machine.
- `Verify-PluginAssemblyIdentity.ps1` — asserts, for every RHP this repository ships, that the
  declared plug-in id exists, is non-empty, matches the product's `package-manifest.json`, and is
  distinct from every other product's id.

## Run

```powershell
dotnet build .\MCP_Rhino.sln -c Release
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1
```

Use `-Configuration Debug` to check the Debug build output instead.

Expected tail:

```
[OK] MCP_Rhino|configuration=Release|pluginId=7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a
[OK] PanelCladdingEditor|configuration=Release|pluginId=7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35
[OK] rhino-plugin-assembly-identity|configuration=Release|products=2|distinctPluginIds=2
```

Removing either `src/*/AssemblyInfo.cs` must make this script fail.
