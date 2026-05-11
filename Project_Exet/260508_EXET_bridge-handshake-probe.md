# 260508_EXET_bridge-handshake-probe

## Corresponding PLAN

- `Project_Plan/260508_PLAN_bridge-handshake-probe.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Modified: `Project_Archive/Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1`
- Added: `Project_Test/260508_TEST_bridge-handshake-probe/README.md`
- Added: `Project_Test/260508_TEST_bridge-handshake-probe/run_bridge_handshake_validation.ps1`
- Commit hash: not applicable in this workspace state

## Execution Scope

Fixed the existing handshake validation path used to diagnose Debug bridge behavior and added a current regression wrapper that validates both Debug and Release bridge executables against the same live Rhino MCP pipe.

No bridge transport or server runtime code was changed. The issue was isolated to the archived diagnostic probe.

## Changes Made

1. Reworked `handshake_probe.ps1` to use timeout-bounded `ReadLineAsync().Wait(...)` instead of `StandardOutput.Peek()`.
2. Added validation for `initialize`, `notifications/initialized`, `ping`, `tools/list`, and an optional `get_document_summary` tool call.
3. Added parameters for pipe name, timeout, protocol version, expected tool name, minimum tool count, and document path.
4. Added a current test wrapper that runs the probe against both:
   - `src/MCP_Rhino.Bridge/bin/Debug/net8.0/MCP_Rhino.Bridge.exe`
   - `src/MCP_Rhino.Bridge/bin/Release/net8.0/MCP_Rhino.Bridge.exe`
5. Added test README documentation for prerequisites, commands, and expected results.

## Deviations From PLAN

- The implementation did not change production transport code because validation proved the runtime bridge can return JSON-RPC responses correctly.
- Server build validation used temporary `OutputPath` values instead of normal output folders for the server project because a running Rhino process can lock the normal `.rhp` output.
- Full live Release server plugin load was not performed in the same Rhino session because the `mcp_rhino` named pipe is single-owner and was already held by the loaded Debug plugin. Release server build was validated, and the Release bridge executable was live-validated against the same protocol path.

## Issues Found And Resolved

- `StandardOutput.Peek()` produced a false timeout after `initialize`; replacing it with `ReadLineAsync()` fixed the probe.
- The probe needed to use the exported snake_case tool name `get_document_summary`.
- A temporary build attempt using both `BaseOutputPath` and `BaseIntermediateOutputPath` created duplicate assembly attribute errors because moving the intermediate folder changed default `obj` exclusion behavior. The corrected validation used `OutputPath` only.
- Rhino was not running during the first live wrapper attempt; Rhino 8 was started with `C:\Users\Novan\Desktop\Untitled.3dm`, and the Debug plugin was loaded with Rhino's `LoadPlugin` command.

## Test Record

### Build Validation

```powershell
dotnet build .\src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Debug --nologo
dotnet build .\src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
```

Result: both bridge builds completed with 0 warnings and 0 errors.

```powershell
$out = Join-Path $env:TEMP 'mcp-rhino-server-debug-output'
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --nologo -p:OutputPath="$out\"

$out = Join-Path $env:TEMP 'mcp-rhino-server-release-output'
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo -p:OutputPath="$out\"
```

Result: both server builds completed with 0 warnings and 0 errors.

Validated `.rhp` outputs:

- `C:\Users\Novan\AppData\Local\Temp\mcp-rhino-server-debug-output\MCP_Rhino.Server.rhp`
- `C:\Users\Novan\AppData\Local\Temp\mcp-rhino-server-release-output\MCP_Rhino.Server.rhp`

### Live Runtime Validation

Rhino 8 was started with:

```powershell
C:\Users\Novan\Desktop\Untitled.3dm
```

Debug plugin was loaded into Rhino with:

```text
_-LoadPlugin "C:\01_Projects\MCP_Rhino\src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.rhp"
```

Regression wrapper command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Project_Test\260508_TEST_bridge-handshake-probe\run_bridge_handshake_validation.ps1 -DocumentPath C:\Users\Novan\Desktop\Untitled.3dm -TimeoutSeconds 20
```

Result: exit code 0.

Observed live `get_document_summary` result:

- `success`: true
- `filePath`: `C:\Users\Novan\Desktop\Untitled.3dm`
- `objectCount`: 202
- `layerCount`: 18
- `units`: Millimeters

The wrapper completed both Debug and Release bridge probes and printed:

```text
Debug and Release bridge probes completed successfully.
```

### Patch Validation

```powershell
git diff --check -- Project_Archive/Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1 Project_Test/260508_TEST_bridge-handshake-probe Project_Plan/260508_PLAN_bridge-handshake-probe.md
```

Result: exit code 0. Git reported only a line-ending normalization warning for `handshake_probe.ps1`.

## Acceptance Criteria Alignment

- Debug bridge executable builds and completes live JSON-RPC handshake validation.
- Release bridge executable builds and completes live JSON-RPC handshake validation.
- Debug server plugin builds.
- Release server plugin builds.
- The probe no longer depends on `Peek()` for stream readiness.
- The test wrapper fails fast when either bridge executable is missing, handshake validation fails, expected tools are absent, or optional document summary fails.

## Rollback Validation

The production runtime was not changed. Rolling back this task consists of removing the new PLAN/TEST/EXET artifacts and restoring the archived probe script to its prior implementation. No production assemblies, registration files, or Rhino-facing tool contracts were modified.

## Residual Notes

The Release server plugin was build-validated but not loaded live in the same session because the Debug plugin already owned the `mcp_rhino` pipe. To validate the Release plugin live as the pipe owner, close or unload the Debug plugin owner, load the Release `.rhp`, then rerun the same wrapper.

## Conclusion

The Debug failure was a diagnostic probe bug, not a confirmed bridge runtime bug. The probe now uses a blocking line-read with an explicit timeout, and both Debug and Release bridge executables pass the live handshake and tool-call validation against the open Rhino document.
