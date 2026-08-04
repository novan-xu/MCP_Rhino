# Router-only transport test

Run the repository-level transport audit:

```powershell
powershell -ExecutionPolicy Bypass -File Project_Test/260804_TEST_router-only-transport/RouterOnlyTransportSmoke.ps1
```

Then run the existing deterministic Router protocol smoke and both solution configurations:

```powershell
dotnet run --project Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/RouterProtocolSmoke.csproj -c Debug
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
Packaging/MCP_Rhino/Build-McpRhinoPackage.ps1 -OutputRoot Project_Test/260804_TEST_router-only-transport/artifacts
powershell -ExecutionPolicy Bypass -File Project_Test/260804_TEST_router-only-transport/AssertRouterOnlyBundle.ps1 -BundleRoot Project_Test/260804_TEST_router-only-transport/artifacts/MCP_Rhino-1.0.0
powershell -ExecutionPolicy Bypass -File Project_Test/260804_TEST_router-only-transport/RouterOnlyInstallerUpgradeSmoke.ps1 -BundleRoot Project_Test/260804_TEST_router-only-transport/artifacts/MCP_Rhino-1.0.0
```

The static and bundle smokes reject active default/configuration, solution, packaging, plugin startup, runtime workflow, architecture, or compiled Rhino command symbols that restore Bridge, Companion, `_Mcpchat`, fixed debug pipes, or panel-bound execution. The installer upgrade smoke starts from a synthetic legacy install and proves that the validated Router-only upgrade leaves no Bridge/Companion executable path, legacy rollback, or retired Rhino command symbol.
