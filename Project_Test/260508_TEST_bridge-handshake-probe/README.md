# TEST: bridge-handshake-probe

This test validates the stdio-to-named-pipe bridge probe used for MCP_Rhino client integration diagnostics.

## Scope

- The fixed probe uses `ReadLineAsync()` with explicit timeouts.
- The probe validates `initialize`, `ping`, and `tools/list`.
- Optional document validation calls `get_document_summary` through the live MCP server.
- The wrapper runs the probe against both Debug and Release bridge executables.

## Prerequisites

- Rhino 8 is running with an MCP_Rhino plugin loaded.
- The target pipe exists. Default: `\\.\pipe\mcp_rhino`.
- The active Rhino document is saved when `-DocumentPath` is supplied.
- Debug and Release bridge binaries have been built.

## Command

```powershell
powershell -ExecutionPolicy Bypass -File Project_Test\260508_TEST_bridge-handshake-probe\run_bridge_handshake_validation.ps1 `
  -DocumentPath "C:\Users\Novan\Desktop\Untitled.3dm"
```

## Expected Result

- Debug bridge probe exits 0.
- Release bridge probe exits 0.
- Each probe prints raw JSON-RPC responses for `initialize`, `ping`, `tools/list`, and optional `get_document_summary`.
- `tools/list` includes `get_document_summary`.

## Notes

This test intentionally does not use `StreamReader.Peek()`. `Peek()` is unreliable for this bridge path and can falsely report that no response is available after `initialize`.
