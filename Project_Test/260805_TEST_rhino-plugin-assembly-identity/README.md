# Plug-in assembly identity validation

`PluginAssemblyIdentityProbe` reads the assembly-level GuidAttribute directly from
compiled PE metadata, without loading Rhino. The verifier checks that this
repository's product declares a non-empty ID matching its package manifest.

From the repository root:

```powershell
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Debug
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release
```

Use `-PluginPath <RHP>` to verify a package instead. A missing, empty or mismatched
assembly-level GUID must fail validation. A GUID on the plug-in class alone is not
sufficient for Rhino identity.

As of the 2026-10-13 repository split, each product owns an independent copy of this
verifier. PanelCladdingEditor uses 7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35;
MCP_Rhino uses 7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a. Historical PLAN/EXET records
retain the original combined-product validation evidence.
