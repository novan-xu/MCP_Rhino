# Runtime Text Normalization Smoke

Validates that runtime-facing C# text stays English in the targeted MCP surfaces:

- active MCP tool method descriptions
- request DTO property descriptions
- edit result formatter output
- live geometry validator failures/warnings
- developer CLI handler/parser errors

Run:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- runtime-text-normalization-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- runtime-text-normalization-smoke-test
```
