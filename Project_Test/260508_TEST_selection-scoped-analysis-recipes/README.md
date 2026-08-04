# Selection Scoped Analysis Recipes Smoke

This folder contains the static smoke for `260508_PLAN_selection-scoped-analysis-recipes`.

Run after building:

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- selection-scoped-analysis-recipes-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- selection-scoped-analysis-recipes-smoke-test
```

The smoke verifies:

- five new read-only analysis-by-filter MCP tools are present
- tool descriptions and safety annotations are explicit
- request DTOs share the filter-scoped analysis base contract
- the skill composes `LiveObjectSelectionSkill` with existing analysis services
- no arbitrary external script execution was introduced
