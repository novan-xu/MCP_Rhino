# Multi-Document Rhino Router EXET

## Corresponding Plan

- Plan: `Project_Plan/260729_PLAN_multi-document-rhino-router.md`
- Execution date: 2026-07-29

## Associated Artifacts

- Test folder: `Project_Test/260729_TEST_multi-document-rhino-router/`
- Package source: `Packaging/MCP_Rhino/`
- Staged current-user bundle:
  `%USERPROFILE%\AppData\Local\MCP_Rhino\staged\1.0.0-20260729200817170`
- Commit: not committed in this execution pass

## Execution Result / Actual Scope

Implemented the Release startup plug-in, per-document routed endpoints, per-agent stdio Router, and
current-user packaging flow:

- Added BCL-only `MCP_Rhino.Transport` with route protocol v1, normalized paths, atomic descriptor
  registry, endpoint nonce, process/generation identity, structural validation, and generation-safe
  cleanup.
- Made Release `MCP_Rhino.Server.rhp` `AtStartup` while retaining Debug `WhenNeeded` bridge-only
  behavior.
- Added a Release-only dispatcher that tracks existing/new/opened/saved/closed Rhino documents,
  publishes one route listener per saved runtime serial number, updates the same session on Save As,
  and marks descriptors unavailable before bounded shutdown.
- Added route-bound live document resolution through `RhinoDoc.FromRuntimeSerialNumber`, including
  strict `DOCUMENT_TARGET_CONFLICT` enforcement.
- Extended the named-pipe host with listener readiness, `PipeOptions.CurrentUserOnly`, bounded
  concurrent route clients, per-connection MCP hosts, tracked cancellation, and aggregate route
  shutdown.
- Added `MCP_Rhino.Router` as a RhinoCommon-free stdio MCP server/client gateway. It independently
  initializes each backend, verifies private attestation, fully collects paginated tools/resources/
  templates, selects a deterministic canonical surface, and never replays a mutation.
- Added Router controls `rhino_router_list_documents`, `rhino_router_select_document`, and
  `rhino_router_get_selected_document` with the planned safety annotations. Selection is local to one
  Router process, so separate agents can choose different documents concurrently.
- Added Release bundle build, hash manifest, current-user install/repair/validate/uninstall,
  all-component active-process staging, stable executable discovery, rollback ownership, opt-in JSON
  client entry merging, and generic client templates.
- Added the required CLI slug `multi-document-rhino-router-smoke-test`, dedicated Rhino command
  `_McpMultiDocumentRhinoRouterSmoke`, and a standalone fake-backend protocol harness.
- Updated architecture, runtime workflow, package, and repository documentation to distinguish the
  debug Bridge, panel/Companion, and external Router paths.

## Deviations From Plan

- The actual current-user upgrade was not applied because seven Rhino processes were active. The
  installer staged the complete compatible bundle and replaced no loaded component, as required by
  the upgrade-safety contract.
- Repository `.mcp.json` was not switched from the development Bridge path. The plan explicitly
  gates that change on successful packaged startup and live rollback validation; those checks require
  closing/restarting Rhino with the staged Release plug-in.
- Live Rhino fixture checks (startup auto-load, multi-process discovery, first Save, Save As, close,
  reversible mutation, Undo, and simultaneous Companion/Router use) were not run against the user's
  open project documents. They remain host-level validation against disposable saved fixtures after
  installation.
- The route registry uses the inherited current-user profile ACL of `%LOCALAPPDATA%` and route pipes
  additionally use `PipeOptions.CurrentUserOnly`; no extra ACL package was added to the BCL-only
  Transport assembly.

## Issues Found And Fixed During Execution

- The first wire-level attestation test exposed missing `JsonSerializerOptions.TypeInfoResolver`
  configuration required by the pinned MCP SDK. Added `DefaultJsonTypeInfoResolver`.
- RhinoCommon documents expose `OpenDocuments()` as a method, not an `OpenDocuments` property.
  Corrected startup enumeration so documents already open when the Release plug-in loads are routed.
- Tightened attestation so the endpoint nonce is stored in the descriptor and must match the private
  endpoint response, rather than merely being a well-formed GUID.
- `dotnet publish` did not copy the post-build `.rhp` alias. Package staging now creates the `.rhp`
  from the exact published Server DLL and fails if the result is absent.
- Windows PowerShell 5 lacks `Path.GetRelativePath`; package scripts now use a validated local
  relative-path helper and pass syntax/runtime validation on the installed shell.
- Repair initially risked losing client-entry ownership metadata. It now preserves the original
  prior entry and records owned rollback files for safe uninstall.

## Test Record

Builds:

- Normal Debug output build before implementation -> blocked by running Rhino PID 49908 locking
  `src/MCP_Rhino.Server/bin/Debug/net8.0/MCP_Rhino.Server.rhp`; the process was not closed because it
  could contain unsaved user work.
- `dotnet build .\MCP_Rhino.sln -c Debug --artifacts-path .\.tmp-build\final2-debug --nologo`
  -> exit 0, 0 warnings, 0 errors.
- `dotnet build .\MCP_Rhino.sln -c Release --artifacts-path .\.tmp-build\final2-release --nologo`
  -> exit 0, 0 warnings, 0 errors.

Router protocol and CLI:

- Debug `RouterProtocolSmoke.dll` -> exit 0.
- Release `RouterProtocolSmoke.dll` -> exit 0.
- Debug Server `multi-document-rhino-router-smoke-test` -> exit 0.
- Checks passed for paginated tools/resources/templates, canonical proxy surface, unique path routing,
  no-selection error, independent selections, selected-path conflict without backend dispatch,
  duplicate-path ambiguity, stale cleanup, corrupt descriptor reporting, forged attestation
  quarantine, and multiple concurrent Router clients.
- Packaged Router stdio handshake -> initialize/ping/tools-list exit 0; initialize advertised only
  resources (`subscribe=false`, `listChanged=false`) and tools (`listChanged=true`), and no-backend
  tools-list contained exactly the three controls. Diagnostics remained on stderr.

Packaging:

- `Build-McpRhinoPackage.ps1 -OutputRoot .\.tmp-package-final` -> exit 0; produced the Release-only
  `MCP_Rhino-1.0.0` bundle.
- Disposable install with opt-in client merge -> exit 0; unrelated settings preserved.
- `-Mode Validate` -> exit 0; 126 installed owned files matched SHA-256 records in the earlier full
  hash check and Router `--validate-install` passed.
- Disposable repair restored removed installer-owned metadata and preserved client ownership -> exit
  0.
- Disposable uninstall -> exit 0; prior `mcp-rhino` entry restored, unrelated entry preserved, bin,
  plug-in version, and ownership manifest removed, and no `.3dm` path touched.
- Actual current-user installer with active Rhino processes -> exit 0 with staged-only warning at
  `%USERPROFILE%\AppData\Local\MCP_Rhino\staged\1.0.0-20260729200817170`; no installed component or
  client entry was replaced.

Regression:

- MCP safety annotations smoke in Debug and Release -> exit 0, 156 tools.
- MCP tool overlap cleanup smoke in Debug and Release -> exit 0, 156 canonical tools.
- MCP surface structure/resource inventory smoke in Debug and Release -> exit 0, 156 tools and 5
  resources.
- Direct debug Bridge handshake could not acquire `mcp_rhino` because long-lived
  `MCP_Rhino.Bridge` PID 45772 already occupied the single-client debug endpoint. That process was
  left untouched; the failure is recorded as an environmental live-regression limitation.

## Acceptance Alignment

- Release plug-in startup and Debug manual-load behavior are separated at compile time without
  changing the plug-in GUID.
- Debug, panel-bound, and routed pipe names/lifecycles remain distinct.
- Route identity includes session, plug-in generation, document lifecycle generation, endpoint
  nonce, PID/start time, runtime serial, current path, protocol, and plug-in version.
- One Router can route across multiple fake documents; two Routers retain different selections and
  share endpoints concurrently.
- Path ambiguity and selection conflict fail closed. Forged/stale/corrupt discovery data cannot reach
  a business tool.
- Router frontend/backend negotiation is independent, proxy surface pagination is complete, resource
  subscription is not advertised, mutation failure is not replayed, and stdout is protocol-only.
- Normal client configuration uses an absolute installed Router command and requires no manually
  started Router daemon.
- Install/repair/uninstall operate only on manifest-owned files/client entry and preserve unrelated
  configuration and Rhino documents.

## Rollback Verification

Disposable uninstall verified owned-file deletion and semantic restoration of the prior client entry.
The current-user installation now owns only the files recorded in its install manifest and remains
reversible through the packaged uninstall mode. Source rollback remains a git revert of this
PLAN/TEST/EXET set and the associated Server, Transport, Router, packaging, solution, and
documentation changes.

## Live Activation Follow-Up (2026-07-29)

- After the user closed all Rhino instances, installed the previously staged bundle successfully to:
  - Router: `%USERPROFILE%\AppData\Local\MCP_Rhino\bin\MCP_Rhino.Router.exe`
  - Plug-in: `%USERPROFILE%\AppData\Roaming\McNeel\Rhinoceros\packages\8.0\MCP_Rhino\1.0.0\MCP_Rhino.Server.rhp`
- Packaged validation passed for version `1.0.0`, protocol `1`, all 126 manifest-owned files, the
  package version selector, and the installed Router.
- Launched Rhino 8 normally with `20260729_Lot visualization.3dm`; Rhino PID 4000 published a READY
  route without `_LoadPlugin` or `_Mcpchat`, confirming Release `AtStartup` activation.
- The published route used session `e2829c9ea6664ac18c9dfba6afa3a5b3`, runtime serial `268435457`,
  and pipe `mcp_rhino_route_4000_268435457` for the exact requested document path.
- An installed-Router MCP initialize/tools/call probe passed end to end. Routed
  `get_document_summary` returned the live document with 1,167 objects, 14 layers, Inches units, and
  current layer `01_SAMPLE-CW Panels`.
- Rhino remains open on the requested document. No MCP client configuration was changed; client
  opt-in remains a separate explicit step.

## Current Remaining Items

- Opt in to the desired MCP client configuration (or update repository `.mcp.json`) using the installed
  `%LOCALAPPDATA%\MCP_Rhino\bin\MCP_Rhino.Router.exe` path.
- Run the remaining live multi-document lifecycle cases against saved disposable fixtures when desired:
  multiple documents/processes, first Save, Save As, close/reopen, reversible mutation plus Undo,
  Companion coexistence, and shutdown drain.
- Re-run the fixed debug Bridge handshake after the existing long-lived Bridge releases the
  single-client debug pipe.

## Conclusion

The multi-document Router architecture, tests, packaging, and current-user deployment are implemented.
Deterministic protocol, build, disposable install/rollback, packaged validation, Release startup
auto-load, route publication, and an installed-Router live read all pass. Broader live lifecycle and
mutation checks remain optional follow-up validation; MCP client registration remains intentionally
opt-in.
