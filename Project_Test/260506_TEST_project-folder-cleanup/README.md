# 260506_TEST_project-folder-cleanup

## Scope

Verification artifact for the project folder cleanup, archive bucket creation, and rule document updates.

## Checks

### Archive Buckets

```powershell
$required = @(
  'Project_Archive\Project_Plan',
  'Project_Archive\Project_Exet',
  'Project_Archive\Project_Test'
)
foreach ($path in $required) {
  if (-not (Test-Path -LiteralPath $path -PathType Container)) {
    throw "Missing archive folder: $path"
  }
}
```

Result:

```text
Archive buckets OK: plans=20 exets=16 tests=16
```

### Debug Build

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Release build was attempted but blocked by live process file locks:

- `Rhino 8 (51864)`
- `MCP_Rhino.Companion (49512)`
- `MCP_Rhino.Bridge (53792)`
- `MCP_Rhino.Bridge (34284)`

### Release Build After Closing Rhino

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

### Smoke

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

```text
[OK] MCP safety annotations verified for 78 tools.
[OK] No bare method-level [McpServerTool] attributes remain.
```

Release re-run:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

Result:

```text
[OK] MCP safety annotations verified for 78 tools.
[OK] No bare method-level [McpServerTool] attributes remain.
```

### Archived Smoke Registration

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- geometry-analysis-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

Result:

```text
Geometry analysis smoke test completed successfully (CLI fallback mode).
```

Release re-run:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- geometry-analysis-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

Result:

```text
Geometry analysis smoke test completed successfully (CLI fallback mode).
```

### Root Generated Artifact Cleanup

Removed generated root artifacts after verifying their paths were under `C:\Projects\MCP_Rhino`:

- `.tmp-build/`
- `artifacts/`
- `_validation/`
- `_tmp_rhino_test.csx`

Added `.tmp-build/`, `artifacts/`, and `_validation/` to `.gitignore`.
