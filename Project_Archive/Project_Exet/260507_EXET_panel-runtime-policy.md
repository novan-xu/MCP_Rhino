# 260507_EXET_panel-runtime-policy

## Corresponding Plan

- Plan: `Project_Plan/260507_PLAN_panel-runtime-policy.md`
- Execution date: 2026-05-07

## Associated Artifacts

- Test folder: `Project_Test/260507_TEST_panel-runtime-policy/`
- Commit / PR: not created in this working session.

## Execution Result / Actual Scope

- Added the approved English runtime policy bundle:
  - `src/MCP_Rhino.Server/Prompts/Runtime/McpRhinoRuntimePolicyBundle.md`
- Added prompt loaders:
  - `src/MCP_Rhino.Companion/RuntimePolicyPrompt.cs`
  - `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/RuntimePolicyPrompt.cs`
- Injected the runtime policy into:
  - Companion Claude Code bound-document prompt
  - Companion Codex bound-document prompt
  - Rhino-hosted fallback panel bound-document prompt
- Updated project files so the policy bundle is copied to both Server and Companion outputs.
- Added a focused smoke test:
  - CLI slug: `panel-runtime-policy-smoke-test`
  - Rhino command: `_McpPanelRuntimePolicySmoke`
- Added documentation references in:
  - `Runtime_Workflow/MCP_Rhino Workflow.md`
  - `Project_Guides/MCP_Rhino Architecture.md`
- Added an isolation guard to the smoke test so repository-workspace / debug-pipe routes fail the smoke if they start loading the panel runtime policy.

## Differences From Plan

- Added a Rhino smoke command wrapper in addition to the CLI smoke to match the existing capability smoke pattern.
- Added small documentation references so the runtime policy bundle has a discoverable canonical location.
- Added a follow-up isolation assertion after user clarified that project-workspace/test-route tasks must continue to use repository rules rather than panel runtime policy.

## Issues Found And Fixed

- First Release build failed because the new smoke helper was named `FindRepositoryRoot`, colliding with an existing `DeveloperCommandHandler` partial helper. Renamed it to `FindPanelRuntimePolicyRepositoryRoot`.

## Test Record

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- First run exit code: 1
- Reason: helper name collision in `DeveloperCommandHandler.PanelRuntimePolicySmokeTest.cs`.

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- Final exit code: 0
- Result: build succeeded with 0 warnings and 0 errors.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- panel-runtime-policy-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] Runtime policy bundle copied to Server output.`
  - `[OK] Runtime policy bundle copied to Companion output.`
  - `[OK] Companion and fallback panel prompts load the runtime policy.`
  - `[OK] Project workspace and debug-pipe routes do not inject the panel runtime policy.`

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] MCP safety annotations verified for 78 tools.`
  - `[OK] No bare method-level [McpServerTool] attributes remain.`

## Acceptance Alignment

- The runtime policy bundle is English-only and ASCII.
- The same policy file is copied to both Server and Companion outputs.
- Companion Claude Code, Companion Codex, and the fallback Rhino panel append the policy to their bound-document prompts.
- Project-workspace / test-route entry points do not load or inject the panel runtime policy.
- The policy permits explicit user-approved external runtime files, spreadsheets, and slides when appropriate runtime tools or connectors are available.
- The policy keeps repository construction and project-file mutation outside panel scope.

## Rollback Verification

- No rollback command was executed.
- Practical rollback is scoped to removing the policy file, prompt-loader helpers, csproj content includes, prompt append calls, smoke test, and Rhino smoke command.

## Current Remaining Items

- Spreadsheet and slide manipulation is now allowed by panel policy, but still depends on available runtime tools or external connectors. If those tools are absent, the panel model should report a runtime capability gap.

## Conclusion

The panel runtime policy bundle is now a real prompt artifact loaded by standalone Companion and fallback panel sessions. Release build and smoke verification passed.
