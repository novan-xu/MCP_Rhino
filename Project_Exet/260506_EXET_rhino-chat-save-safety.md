# 260506_EXET_rhino-chat-save-safety

## Corresponding Plan

- Plan: `Project_Plan/260506_PLAN_rhino-chat-save-safety.md`
- Execution date: 2026-05-06

## Associated Artifacts

- Test folder: `Project_Test/260506_TEST_rhino-chat-save-safety/`
- Commit / PR: not created in this pass.

## Execution Result / Actual Scope

- Updated `PerDocumentPanelDispatcher.OnEndSaveDocument` so Rhino save completion no longer calls
  `TryStartDocument`.
- Kept `_host.UpdateDocumentPath(document)` so an existing panel session can refresh path/header
  state after `Save` / `SaveAs`.
- Added CLI smoke slug `rhino-chat-save-safety-smoke-test`.
- Registered the smoke through the existing `DeveloperCommandHandler` partial hook pattern.

## Differences From Plan

- No material differences.
- The test is source-level because this shell cannot drive a real Rhino `Save` / `SaveAs` event
  deterministically.

## Issues Found And Fixed

- Initial build failed because the new partial smoke defined `Require(bool, string)`, colliding with
  an existing partial smoke helper in `260505_TEST_rhino-claude-code-panel`.
- Fixed by renaming the helper to `RequireSaveSafety`.

## Test Record

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- Exit code: 0
- Result: `Build succeeded. 0 Warning(s), 0 Error(s).`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-chat-save-safety-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] EndSaveDocument does not start the Claude Code panel/session.`
  - `[OK] Explicit _Mcpchat panel startup path remains intact.`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-panel-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] CLI mode does not instantiate a live or bound Rhino accessor.`
  - `[OK] McpConfigBuilder generated parseable per-doc MCP config JSON.`
  - `[OK] ClaudeCodeAvailability probe completed: Claude Code 2.1.131 is available.`
  - `[OK] MCP_Rhino.Bridge.exe --pipe other_name --help returns usage with exit code 0.`

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- rhino-claude-code-companion-ui-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] CLI mode keeps the Rhino live accessor disabled.`
  - `[OK] Per-document MCP config JSON is parseable and contains the bound pipe name.`
  - `[OK] Claude Code availability probe completed: Claude Code 2.1.131 is available.`
  - `[OK] MCP_Rhino.Companion.exe accepts the command-line contract used by _Mcpchat.`

## Acceptance Alignment

- Build passes.
- Save handler no longer starts panel/session work.
- Explicit `_Mcpchat` startup remains intact.
- Existing Claude Code panel and companion CLI smokes still pass.

## Rollback Verification

- The behavioral rollback point is the single removed `TryStartDocument(document)` call in
  `OnEndSaveDocument`.
- Reverting only that line would restore old save-time startup behavior and is not recommended.

## Current Remaining Items

- Manual Rhino verification is still required: reload the rebuilt `.rhp`, open a saved document,
  run `_Mcpchat`, then verify `Save` / `SaveAs` no longer opens or restarts chat.
- Separate `.rws` worksession handling remains unimplemented.
- The working tree contains unrelated pre-existing `rhino-agent-panel-redesign` artifacts and
  companion UI edits; this pass did not modify or validate those changes.

## Conclusion

The save-time startup defect is patched and covered by a focused CLI regression smoke. Rhino save
completion now only updates existing chat document-path state instead of starting the Claude Code
panel/session.
