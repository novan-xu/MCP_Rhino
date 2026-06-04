# Takeoff Spreadsheet TEST

Smoke slug:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- takeoff-spreadsheet-smoke-test
```

CLI fallback verifies that discovery, preview, export, and agent preview require live Rhino after non-live output validation.

Rhino live command:

```text
McpTakeoffSpreadsheetSmoke
```

The live smoke creates two panel-like Breps with user text, previews an aggregate take-off, then writes CSV and XLSX files under `_validation/takeoff-spreadsheet-smoke-test/`.
