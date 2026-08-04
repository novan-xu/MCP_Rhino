# 260506_EXET_rhino-chat-file-lock-safety

## Corresponding Plan

- Plan: `Project_Plan/260506_PLAN_rhino-chat-file-lock-safety.md`
- Execution date: 2026-05-06

## Associated Artifacts

- Test folder: `Project_Test/260506_TEST_rhino-chat-file-lock-safety/`
- Commit / PR: not created in this pass.

## Execution Result / Actual Scope

- Diagnosed the active desktop file:
  `<LOCAL_TEST_MODEL_PATH>`.
- Confirmed the file was not read-only (`Attributes=Archive`, `IsReadOnly=False`).
- Confirmed an exclusive open failed while the error was active.
- Used Windows Restart Manager to identify the lock owners:
  - `MCP_Rhino.Companion.exe` PID 52332
  - `claude.exe` PID 44000
  - `MCP_Rhino.Bridge.exe` PID 37020
- Observed Rhino's failed save temp file:
  `Sample - Wireframe.3dm_tmp`, last write `2026-05-06 16:26:50`.
- Added isolated per-pipe workspace helpers for companion and legacy panel launches.
- Changed Claude and Codex launch paths so their working directory is no longer the `.3dm` folder.
- Changed the Rhino-to-companion process launch to `UseShellExecute = true` so the companion tree
  does not inherit Rhino's open document file handle.
- Added Claude launch hardening:
  - `--no-session-persistence`
  - `--setting-sources user`
  - `--tools ""`
  - expanded disallowed local/project helper tools
  - prompt guard forbidding local shell/direct `.3dm` fallback
- Changed object edit and geometry mutation adapters to use the resolved `RhinoDoc` supplied by the
  live accessor instead of `RhinoDoc.ActiveDoc`.
- Added architecture guidance forbidding LLM child processes from using the model folder as
  working directory.
- Added CLI smoke slug `rhino-chat-file-lock-safety-smoke-test`.
- Reproduced the remaining failure after the first fix: the new companion/Claude/bridge tree still
  locked the `.3dm`, confirming inherited Rhino document handles were the active cause.
- The current lock-holding process tree exited before a forced stop was needed; the desktop `.3dm`
  then accepted an exclusive read/write open again.

## Differences From Plan

- Extended the bound-document fix from object edits to geometry mutation as well after finding the
  same `RhinoDoc.ActiveDoc` pattern in `LiveRhinoGeometryMutator`.
- Did not use Claude `--bare`; local help shows it disables OAuth/keychain auth, which can break
  subscription-based Claude Code installs. The implemented hardening keeps normal auth while
  removing model-folder project/session behavior.

## Issues Found And Fixed

- Root cause: the panel process tree was launched from Rhino in a way that allowed the open `.3dm`
  handle to be inherited into `MCP_Rhino.Companion`, `claude`, and `MCP_Rhino.Bridge`. The earlier
  model-folder working-directory issue made the launch unsafe as well, but the persistent lock after
  workspace isolation confirmed handle inheritance as the active save blocker.
- Secondary correctness issue: some live mutation adapters were still using `RhinoDoc.ActiveDoc`
  under panel-bound execution. Those paths now receive the resolved bound document explicitly.

## Test Record

```powershell
dotnet build .\MCP_Rhino.sln -c Release -p:OutDir=<REPO_ROOT>\.tmp-build\Release\
```

- Exit code: 0
- Result: `Build succeeded. 0 Warning(s), 0 Error(s).`

```powershell
.\.tmp-build\Release\MCP_Rhino.Server.exe rhino-chat-file-lock-safety-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] Rhino launches the companion without inherited document file handles.`
  - `[OK] Claude/Codex sessions use isolated per-pipe working directories.`
  - `[OK] Claude local project/file tools are disabled for panel sessions.`
  - `[OK] Claude prompts forbid local shell/direct .3dm fallback.`
  - `[OK] Object edit and geometry mutators use the resolved bound RhinoDoc.`

```powershell
.\.tmp-build\Release\MCP_Rhino.Server.exe rhino-chat-save-safety-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] EndSaveDocument does not start the Claude Code panel/session.`
  - `[OK] Explicit _Mcpchat panel startup path remains intact.`

```powershell
.\.tmp-build\Release\MCP_Rhino.Server.exe mcp-tool-safety-annotations-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] MCP safety annotations verified for 78 tools.`
  - `[OK] No bare method-level [McpServerTool] attributes remain.`

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- First run exit code: 1 because Rhino PID 29884 had
  `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll` loaded.
- After Rhino closed, final exit code: 0.
- Result: `Build succeeded. 0 Warning(s), 0 Error(s).`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-chat-file-lock-safety-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-chat-save-safety-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

- Exit code: 0 for all three smokes against the final Release output.

Additional live lock check after the inherited-handle process tree exited:

- `<LOCAL_TEST_MODEL_PATH>`
- `LockedExclusive=False`
- `IsReadOnly=False`

## Acceptance Alignment

- The active file-lock owner was identified exactly.
- The model file attribute was confirmed not read-only.
- The process launch path no longer uses the `.3dm` folder as CLI working directory.
- Claude local project/file tooling is disabled without breaking normal OAuth/keychain auth.
- Bound live mutation paths no longer depend on `RhinoDoc.ActiveDoc` for object and geometry edits.
- Focused source smoke passes.

## Rollback Verification

- Reverting `CompanionWorkspace` / `PanelChatWorkspace` usage would restore the document-folder
  working directory and is not recommended.
- Reverting the `RhinoDoc` parameter changes would restore `ActiveDoc` ambiguity in panel-bound
  mutation tools.

## Current Remaining Items

- Manual Rhino verification is still required with the rebuilt plug-in: open the test file, start
  `_Mcpchat`, perform a small Claude/Codex MCP mutation, and save.
- The failed save temp file `Sample - Wireframe.3dm_tmp` remains next to the model. It was not
  deleted automatically in this pass so Rhino's recovery path is not disturbed.

## Conclusion

The save failure was caused by the MCP_Rhino panel child process tree holding the active `.3dm`
while Rhino attempted to save. The implementation now isolates LLM CLI workspaces away from the
model folder, disables local Claude file/project tooling for panel sessions, and removes
`ActiveDoc` mutation ambiguity from the affected live adapters.
