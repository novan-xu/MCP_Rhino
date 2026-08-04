# Multi-Rhino Panel Pipes Smoke

## CLI

Run from the repository root after building:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- multi-rhino-panel-pipes-smoke-test
```

Expected output:

- Developer debug pipe remains `mcp_rhino`.
- Panel-bound pipe names include process id and runtime serial.
- Temporary MCP config directories are pipe-name scoped.
- Project guide documents multi-Rhino pipe isolation.

## Manual Rhino Check

1. Build `src\MCP_Rhino.Server`, `src\MCP_Rhino.Bridge`, and `src\MCP_Rhino.Companion`.
2. Launch two independent Rhino 8 processes.
3. Load the rebuilt `.rhp` in both processes.
4. Open or save one document in each process.
5. Run `_Mcpchat` in both processes.
6. Confirm each chat session connects to a distinct pipe shaped like
   `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`.
7. Confirm a second Rhino process does not spam `All pipe instances are busy` for `mcp_rhino`.
