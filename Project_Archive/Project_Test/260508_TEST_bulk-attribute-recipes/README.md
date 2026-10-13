# Bulk Attribute Recipes Smoke

Validates the compact bulk object attribute recipe MCP surface:

- preview/apply tool methods exist and expose explicit MCP safety metadata
- request DTOs include compact recipe fields and selection filters
- recipe service resolves server-side templates and applies with one live Undo record
- no arbitrary external script execution is introduced

Run:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- bulk-attribute-recipes-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- bulk-attribute-recipes-smoke-test
```
