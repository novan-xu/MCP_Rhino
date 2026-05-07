# Rhino Chat Save Safety Smoke

## CLI

Run from the repository root after building:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-chat-save-safety-smoke-test
```

Expected output:

- `EndSaveDocument` does not start the Claude Code panel/session.
- Explicit `_Mcpchat` panel startup remains available.

## Manual Rhino Check

1. Build and reload `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`.
2. Open a saved Rhino document.
3. Run `_Mcpchat` only when chat is needed.
4. Save and SaveAs the document while the plug-in is loaded.
5. Confirm save completes and does not open or restart the Claude Code panel/session as a side effect.
