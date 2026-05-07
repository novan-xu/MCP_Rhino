# Rhino Chat File Lock Safety Smoke

## CLI

Run from the repository root after building:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-chat-file-lock-safety-smoke-test
```

Expected output:

- Companion Claude session uses an isolated working directory.
- Companion Codex session uses an isolated working directory.
- Legacy panel Claude session uses an isolated working directory.
- Claude sessions disable local built-in tools and direct `.3dm` fallbacks.
- Object edit applier uses the resolved bound `RhinoDoc`, not `RhinoDoc.ActiveDoc`.

## Manual Rhino Check

1. Build and reload `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`.
2. Open a saved `.3dm`, run `_Mcpchat`, and ask Claude/Codex to make a small MCP edit.
3. Save the file in Rhino.
4. Confirm save completes without `file opened in read-only mode` / `SaveAs required`.
5. If save fails, run a Windows Restart Manager or handle check against the `.3dm` path and verify
   no `MCP_Rhino.Companion`, `claude`, or `MCP_Rhino.Bridge` process owns the file.
