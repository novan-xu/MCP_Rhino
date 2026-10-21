# Offline open-source guidance

Record level: lightweight. Execution date: 2026-10-21. Status: complete in source; not installed.

## Reason and scope

During the 2026-10-20/21 MET Tang session, the user saw agents apparently trying to close their
open Rhino file to do offline work. Review of that session found no agent close or kill command.
Rhino exited three times (one logged Windows app hang, two with no log entry), and each time the
agent switched to offline tools. Once it moved its offline output over the user's canonical model
`MET - Wireframe_STN.3dm`, keeping a backup. The Router's `OFFLINE_SOURCE_OPEN` error text also
suggested "or close it before offline editing", which nudged agents toward closing the user's file.

Scope: remove that nudge and write down the rule. No tool, schema, error code, routing behavior or
safety annotation changed.

## Changes

- `src/MCP_Rhino.Router/Routing/RouterRuntime.cs` (`ValidateOfflineSource`): the
  `OFFLINE_SOURCE_OPEN` message is now "This source is open in Rhino. Select its live session and
  use live tools. Do not close, save, or replace the user's document; if offline work on this file
  is really needed, ask the user." The error code and rejection condition are unchanged.
- `Runtime_Workflow/MCP_Rhino Workflow.md` (offline bullets): new hard rule. Agents never close a
  Rhino document or Rhino, kill Rhino, or ask the user to close a file just to enable offline tools.
  On `OFFLINE_SOURCE_OPEN` they switch to the live session. Offline output is returned at its
  suffixed path and is never renamed or moved over the user's canonical `.3dm` unless the user
  explicitly asks in the current conversation.

No test asserted the old message text. The existing Router protocol smoke asserts only the
`OFFLINE_SOURCE_OPEN` code.

## Validation

- `dotnet build .\MCP_Rhino.sln -c Debug --nologo` and `-c Release`: exit 0, zero warnings/errors.
- `Project_Archive/Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke`
  (Debug and Release, `--no-build`): exit 0. Its assertion that a known open source is rejected
  with `OFFLINE_SOURCE_OPEN`, and that a closed path needs no live backend, still passes.
- `Project_Test/261010_TEST_offline-copy-edit/OfflineCopyEditSmoke` (Debug and Release): exit 0,
  including the real stdio MCP duplicate-only apply.
- Built `MCP_Rhino.Router.dll` (Debug and Release) contains the new message and not the old text.
- `git diff --check`: exit 0.

Logs are in ignored `.validation/offline-open-guidance/`. The CLI-host MCP safety-annotation smoke
was not rerun because no tool registration or annotation changed.

## Remaining work and limits

The new message reaches agents only after the user installs a Router built from this source. The
installed Router and plug-in were not reinstalled, restarted or modified, and the user's Rhino was
not touched. Installing is a separate step for the user, under the existing installer
`-Mode Validate` and registry-attestation gates. The workflow rule applies to agents that read
`Runtime_Workflow/` now; the Router's MCP server instructions were not changed.

Rollback: revert this change's commit; it touches only a message string and documentation.
