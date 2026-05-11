# PLAN: bridge-handshake-probe

## Background

The archived MCP client integration probe used `StreamReader.Peek()` to poll bridge output. Against the named-pipe bridge this can report no available data even when the server has already written a newline-delimited JSON-RPC response. That made a healthy debug pipe look stalled after `initialize`.

## Goals

- Replace the unreliable probe read loop with timeout-bounded `ReadLineAsync()` reads.
- Validate the MCP handshake beyond `initialize` by checking `ping` and `tools/list`.
- Keep the probe usable for both Debug and Release bridge executables.
- Record a current test artifact that can be rerun without relying on the archived README alone.

## Architecture Ownership

- `Project_Archive/Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1`: existing diagnostic sample to repair.
- `Project_Test/260508_TEST_bridge-handshake-probe/`: current validation wrapper and usage notes.
- No changes to `MCP_Rhino.Bridge`, plugin transport, tools, resources, or Rhino live adapters are planned.

## Key Design

- The probe reads one JSON-RPC response at a time with `ReadLineAsync().Wait(timeout)`.
- The probe exits nonzero when `initialize`, `ping`, or `tools/list` does not return.
- `tools/list` is validated for a minimum count and can optionally require a named tool.
- Optional `-DocumentPath` validation calls `get_document_summary` to prove live document access through the active debug pipe.
- The validation wrapper runs the same probe against Debug and Release bridge binaries.

## Involved Files

- `Project_Archive/Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1`
- `Project_Test/260508_TEST_bridge-handshake-probe/README.md`
- `Project_Test/260508_TEST_bridge-handshake-probe/run_bridge_handshake_validation.ps1`
- `Project_Exet/260508_EXET_bridge-handshake-probe.md`

## Usage

```powershell
powershell -ExecutionPolicy Bypass -File Project_Test\260508_TEST_bridge-handshake-probe\run_bridge_handshake_validation.ps1 `
  -DocumentPath "C:\Users\Novan\Desktop\Untitled.3dm"
```

## Acceptance Criteria

- Debug bridge build succeeds.
- Release bridge build succeeds.
- Debug server plugin build succeeds in an isolated output path when Rhino has the normal Debug output locked.
- Release server plugin build succeeds.
- Probe succeeds with Debug bridge executable.
- Probe succeeds with Release bridge executable.
- When `-DocumentPath` is supplied, `get_document_summary` returns success for the live active Rhino document.

## Risks And Rollback

- If Rhino is not running with the MCP plugin loaded, the probe should fail clearly before reporting any false stall.
- If another Rhino process owns `\\.\pipe\mcp_rhino`, the validation may target that owner by design; the script should print the requested pipe name.
- Rollback is limited to restoring the archived probe and deleting this current test artifact.

## Future Extension

- Add an optional panel-bound release validation mode when a known `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>` pipe is supplied.
