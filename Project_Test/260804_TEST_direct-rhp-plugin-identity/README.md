# Direct RHP Plug-in Identity Tests

This folder owns the regression artifacts for `260804_PLAN_direct-rhp-plugin-identity.md`.

The test record covers:

- a minimal executable/direct-RHP MSBuild probe;
- Debug and Release solution builds;
- server output and package identity audits;
- the `direct-rhp-plugin-identity-smoke-test` developer fallback;
- compiled Rhino identity enumeration proving the production RHP contains one plug-in type and no
  developer/smoke Rhino commands;
- isolated canonical Rhino registration, `LoadMode=1`, exact active RHP path, and removal of owned
  legacy `CommandList` / `Panels` subkeys;
- Router-only packaging and existing safety regressions.

Exact commands and observed results are recorded in the matching EXET after execution.

Package/installer regression:

```powershell
.\Project_Test\260804_TEST_direct-rhp-plugin-identity\Verify-DirectRhpPackage.ps1 `
  -BundleRoot .\.validation\direct-rhp-package\MCP_Rhino-1.1.2 `
  -TempRoot .\.validation\direct-rhp-installer-smoke
```

Development CLI identity smoke:

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj `
  -c Release -p:McpRhinoCliHost=true -- `
  direct-rhp-plugin-identity-smoke-test `
  .\.validation\direct-rhp-package\MCP_Rhino-1.1.2\Plugin
```
