# 260729_PLAN_multi-document-rhino-router

## Background

The repository currently exposes two live Rhino transport shapes:

- `\\.\pipe\mcp_rhino` is the single-owner Developer Debug Control Path. It resolves every live
  request against `RhinoDoc.ActiveDoc` in the one Rhino process that successfully owns the fixed
  pipe.
- Release-mode panel and Companion sessions use process-scoped, per-document pipes named
  `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`. These avoid cross-process collisions and resolve
  the document through `BoundLiveRhinoDocumentAccessor`.

The fixed debug pipe cannot control an arbitrary document opened in another Rhino process. Bringing
another Rhino window to the foreground does not transfer pipe ownership because each Rhino process
has its own `RhinoDoc.ActiveDoc`. The observed result is `FILE_NOT_ACTIVE` even when the requested
window is visible and focused. Releasing the pipe currently requires closing or restarting Rhino
instances, which is disruptive and unsuitable as the normal multi-document workflow.

The per-document bound execution model already solves document identity inside a Rhino process, but
it is currently started only for `_Mcpchat`, Companion, or the fallback panel. The repository MCP
configuration still launches `MCP_Rhino.Bridge.exe` without `--pipe`, so repository/test-route Codex
sessions cannot discover or route among those document endpoints.

The repository also has no install-once deployment contract for this workflow. Release `.rhp`
artifacts are loaded from build output through `_LoadPlugin`/Plug-in Manager instructions, and MCP
client examples point at repository build paths. That is suitable for development, but it does not
guarantee that every normally launched Rhino process loads the routing endpoint or that an MCP-capable
agent can start the correct gateway without the user manually running another executable.

## Goal

- Allow one repository MCP connection to discover and control any saved document open in any
  participating Rhino 8 process owned by the current Windows user.
- Allow multiple Rhino processes and documents to remain open without competing for one global
  named pipe.
- Route existing Rhino tool calls without changing or duplicating their public tool names, input
  schemas, safety annotations, or live-only behavior.
- Preserve the registered Rhino reference resources and resource templates when the repository MCP
  configuration moves from the direct bridge to the router.
- Let the client list open documents, select a document by a stable session id, and safely switch
  targets during one MCP session.
- Preserve exact document isolation for read, preview, mutation, selection, viewport, and export
  calls.
- Preserve the current Developer Debug Control Path and Debug bridge-only plugin contract for smoke,
  regression, and low-level transport debugging.
- Keep `_Mcpchat`, Companion, and the Rhino-hosted fallback panel working independently of the new
  router.
- Provide one current-user Release installation that deploys the Rhino plugin, its exact runtime
  dependencies, the Router, Bridge, and Companion as one compatible bundle outside the repository.
- Make the installed Release plugin load once at Rhino startup in every Rhino 8 process, with no
  `_LoadPlugin`, `_Mcpchat`, panel, or agent connection required to create routed document endpoints.
- Give supported MCP clients a stable installed command path that launches one Router process
  automatically for each agent MCP session. The user does not start or keep a
  `MCP_Rhino.Router.exe` daemon running manually.
- Allow multiple agents to connect concurrently, maintain independent document selections, and
  control different open documents without occupying the debug or panel/Companion connection.

## Non-Goals

- Do not read or mutate `.3dm` files directly from disk.
- Do not make `filePath` alone the identity of a Rhino session; the same file can be open in more
  than one Rhino process.
- Do not convert the global debug pipe into a multi-document server or relax its `ActiveDoc`
  semantics.
- Do not enable panel-bound pipes in the Debug bridge-only build.
- Do not add a Tool, Skill, Agent, or top-level orchestration agent for transport routing.
- Do not build a transparent or general-purpose JSON-RPC multiplexer. The router terminates MCP on
  both sides and advertises only the MCP capabilities it deliberately implements.
- Do not provide remote-machine routing, network listeners, or cross-user discovery in this scope.
- Do not make unsaved Rhino documents mutable through the router. They may be listed as unavailable
  until saved, consistent with the current `ACTIVE_DOC_UNSAVED` contract.
- Do not install or startup-load the Debug `.rhp`. Debug remains a developer-selected build and must
  not replace or register over the installed Release plugin that shares its Rhino plug-in identity.
- Do not introduce a persistent Windows service or shared Router daemon. Each MCP client launches
  and owns its lightweight stdio Router process; the Rhino-side route endpoints are the shared
  multi-client boundary.
- Do not promise automatic configuration of unknown third-party MCP clients. The installer may
  configure explicitly supported clients and must provide a generated generic stdio snippet for all
  others without silently overwriting user-managed configuration.
- Do not add an MSI, machine-wide installation, administrative privilege requirement, Yak publishing,
  or enterprise deployment system in the first implementation. The first package is current-user and
  Rhino 8 specific; those distribution forms can be layered on later.

## Architecture Ownership

- `src/MCP_Rhino.Router/`: new local stdio MCP-terminating gateway. It owns document discovery,
  client-session target binding, backend MCP client sessions, frontend MCP handlers, and router-only
  control tools. It uses the repository's pinned `ModelContextProtocol` SDK on both sides; it must
  not reference RhinoCommon or contain Rhino business logic.
- `src/MCP_Rhino.Transport/`: required BCL-only shared transport contract library for versioned
  endpoint descriptors, registry paths, session identifiers, path normalization, and route-pipe
  protocol constants. The dependency graph is `MCP_Rhino.Server -> MCP_Rhino.Transport` and
  `MCP_Rhino.Router -> MCP_Rhino.Transport`; `MCP_Rhino.Transport` must not depend on either consumer,
  RhinoCommon, the MCP SDK, or tool implementations. `MCP_Rhino.Bridge` remains BCL-only and does not
  take this dependency.
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Routing/`: Rhino-side route-endpoint and discovery
  lifecycle. This layer observes open Rhino documents, starts/stops bound route pipes, and publishes
  transport metadata only.
- `src/MCP_Rhino.Server/Infrastructure/Plugin/`: named-pipe hosting and pipe naming remain here.
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/BoundLiveRhinoDocumentAccessor.cs`: remains the
  live document authority for panel/Companion endpoints.
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/RoutedLiveRhinoDocumentAccessor.cs`: new strict
  runtime-serial accessor for external route endpoints. It resolves the bound live document and also
  rejects a conflicting top-level `filePath`; it does not change panel-bound semantics.
- `src/MCP_Rhino.Bridge/`: remains the minimal explicit-pipe stdio bridge. The router must not turn
  the existing bridge into a tool-aware business component.
- `Packaging/MCP_Rhino/`: new current-user Release bundle staging, manifest, installer/uninstaller,
  and supported-client configuration templates. It owns deployment and registration only; it must
  not contain Rhino document or MCP routing logic.
- `.mcp.json`: repository/test-route client entry changes from the fixed debug bridge to the stable
  installed Router path only after router, startup-load, installation, and rollback validation
  succeed.
- `Project_Guides/` and `Runtime_Workflow/`: document the new routing path while preserving the
  debug and panel-bound contracts, including the distinction between an installed `.rhp` and the
  automatically launched per-agent Router process.

## Key Design

### 1. Packaged Installation And Automatic Startup

- Produce one self-contained, current-user Release bundle from a clean build. The bundle contains
  the Release `.rhp`, Server runtime dependencies, `MCP_Rhino.Transport`, Router, Bridge, Companion,
  version/compatibility metadata, and installer/uninstaller assets. Never package Debug output.
- Install the Release plugin and its adjacent dependencies into Rhino 8's current-user auto-install
  plug-in location, resolved from Rhino-supported installation metadata rather than a repository or
  developer-specific path. Preserve the existing plug-in GUID and fail validation if more than one
  installed Release copy would be discoverable.
- Override `McpRhinoPlugin.LoadTime` so the packaged Release build returns
  `Rhino.PlugIns.PlugInLoadTime.AtStartup`. Keep the `MCP_RHINO_BRIDGE_PIPE_ONLY` Debug build
  manual/when-needed. Startup loading occurs once per Rhino process, not once per document; the
  routed endpoint dispatcher then tracks every existing and subsequently opened document in that
  process.
- Keep startup failure isolated. A registry, route endpoint, or Router-related initialization error
  is reported diagnostically but must not prevent Rhino from starting or disable unrelated
  Companion/panel functionality.
- Install the external executables under one stable current-user product root such as
  `%LOCALAPPDATA%\MCP_Rhino\bin\`. MCP client configuration uses the resolved absolute installed
  `MCP_Rhino.Router.exe` path, never `src/.../bin` output and never a working-directory-dependent
  command.
- The installer writes an owned installation manifest containing product version, route protocol
  version, installed paths, file hashes, and client-configuration actions. It validates that all
  packaged components are from one compatible build before registration.
- Treat upgrades as an all-component operation. If Rhino or an agent-owned Router has locked the
  installed bundle, stage the new bundle and report a restart/close requirement; do not leave a
  partially mixed Server/Transport/Router version. Retain enough manifest state for one owned-bundle
  rollback.
- For each explicitly supported MCP client, install or generate a named `mcp-rhino` stdio entry whose
  command is the resolved stable Router path. Preserve unrelated client settings and require an
  explicit opt-in before modifying a user-level configuration. Always emit a generic stdio snippet
  for clients the installer does not manage.
- Each agent MCP session automatically spawns and owns one Router process through normal MCP stdio
  lifecycle. Multiple Routers may run concurrently and connect to the same Rhino route endpoints;
  no user-started `MCP_Rhino.Router.exe` process, shared daemon, port, or foreground console is
  required.
- Uninstallation removes only files and client-configuration entries recorded as installer-owned,
  after active-process checks. It must not delete `.3dm` files, unrelated MCP entries, repository
  builds, or discovery entries belonging to a still-running matching plugin generation.

### 2. Preserve Three Explicit Transport Roles

- Developer debug pipe: `mcp_rhino`
  - single owner
  - follows `RhinoDoc.ActiveDoc`
  - used for smoke tests and direct bridge diagnostics
- Panel/Companion pipe: `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`
  - created on demand by `_Mcpchat` or the fallback panel
  - owned by one UI session
- Router pipe: `mcp_rhino_route_<ProcessId>_<RuntimeSerialNumber>`
  - created independently of panel/Companion UI
  - bound to the same `RuntimeSerialNumber`
  - discoverable by the external router

The router pipe must use a distinct name and lifecycle so a persistent repository client cannot
block or stop a Companion/panel connection for the same document.

### 3. Release-Mode Routed Endpoint Dispatcher

- In the Release plugin, start a `RoutedDocumentEndpointDispatcher` during plugin load without
  opening a panel or launching an LLM process.
- Enumerate already-open documents and subscribe to `RhinoDoc.NewDocument`,
  `RhinoDoc.EndOpenDocument`, `RhinoDoc.EndSaveDocument`, and `RhinoDoc.CloseDocument`.
- Drive initial enumeration and all document events through one idempotent `ReconcileDocument`
  state machine. Publish every document in discovery metadata, but start a route pipe only when the
  document has a non-empty saved path; mark unsaved documents as non-routable.
- First Save creates the route endpoint. Save As retains the document session id and pipe identity
  while atomically updating the current path.
- Give every descriptor a plugin-instance generation and per-document lifecycle generation. Use a
  closing tombstone/generation check so a delayed Save event or descriptor write cannot recreate a
  route after `CloseDocument`.
- Do not publish `routable: true` until the named-pipe listener reports ready. Pipe creation must
  return an observable ready/failure result rather than only starting a background task.
- On document close, first remove/mark the descriptor closing so no new calls route to it, then
  cancel the listener and connected hosts. On plugin shutdown, drain all route servers under one
  aggregate bounded timeout rather than waiting sequentially per document.
- Keep this dispatcher disabled in `MCP_RHINO_BRIDGE_PIPE_ONLY` builds so the existing Debug contract
  remains intact.
- Keep the dispatcher and Rhino event subscriptions in the plugin's default AssemblyLoadContext.
  Keep MCP hosts in the existing isolated AssemblyLoadContext, and cross that boundary only with
  primitives/JSON strings. Do not pass `MCP_Rhino.Transport` types, serializer instances, interfaces,
  or static state across the ALC boundary.
- A registry permission failure, route listener failure, or descriptor failure disables only the
  affected routing endpoint. It must not fail plugin load or disable the fixed debug pipe,
  panel/Companion, or other documents' routes.

### 4. Versioned Local Discovery Registry

- Store routing descriptors under a current-user directory such as
  `%LOCALAPPDATA%\MCP_Rhino\Routing\v1\`.
- Use one atomically replaced descriptor per Rhino process id and runtime serial number.
- Include at least:
  - registry schema version
  - opaque document session id
  - Rhino process id and process start time
  - plugin-instance and document lifecycle generations
  - document runtime serial number
  - route pipe name
  - normalized saved document path, display name, and routable state
  - plugin/server version and route protocol version
  - last update timestamp
- Treat descriptors only as discovery hints. The plugin owns creation, update, and normal removal.
  Before routing, the router must verify process liveness, process start time, route-pipe readiness,
  and bound-document identity through the private live endpoint attestation described below.
- The router may delete a descriptor only when PID plus process-start validation proves the owner is
  gone/reused, or when atomically replacing/removing the same generation it inspected. A timeout,
  busy pipe, main-thread delay, or failed handshake only quarantines the entry in that router process;
  it must not delete a live plugin's descriptor. `lastUpdate` is diagnostic metadata, not a heartbeat.
- Ignore partial, malformed, unknown-version, and access-denied descriptors without failing the MCP
  session. Report their bounded diagnostic status through the router document-list tool.
- Restrict the registry directory ACL and route pipes to the current Windows user. Route pipes use
  `PipeOptions.CurrentUserOnly`. No network transport is added.

### 5. SDK-Hosted Router MCP Session And Capability Surface

- `MCP_Rhino.Router.exe` is the stable stdio command launched by the repository MCP configuration.
- Implement it as an MCP-terminating gateway using the same pinned `ModelContextProtocol` SDK version
  as the Server. The frontend uses SDK server handlers such as `WithListToolsHandler` and
  `WithCallToolHandler`; each backend uses `StreamClientTransport` plus `McpClient.CreateAsync`.
  Frontend and backend legs negotiate independently. Do not parse/remap raw JSON-RPC request ids or
  claim transparent/lossless proxy behavior.
- Always complete frontend initialization and expose router controls, even when no healthy Rhino
  endpoint exists. When no canonical Rhino surface is available, `tools/list` returns only router
  controls and document status explains how to save/reload a Release-plugin document.
- Obtain the canonical Rhino tool list dynamically from initialized backends, including pagination.
  Preserve every backend tool field exposed by the SDK: name, description/title, input/output
  schemas, annotations, icons, execution metadata, and `_meta`. Do not maintain a second hand-written
  Rhino tool catalog.
- Canonicalize complete tool, resource, and resource-template descriptors before hashing. Choose the
  canonical compatible group deterministically: largest identical fingerprint group, then highest
  server version, then lexicographically smallest document session id. Pin that fingerprint for the
  frontend session and quarantine mismatched endpoints with `TOOL_SURFACE_MISMATCH`; never swap the
  advertised Rhino schema silently during an in-flight session.
- Append only router-owned control tools to the proxied tool list:
  - `rhino_router_list_documents`
  - `rhino_router_select_document`
  - `rhino_router_get_selected_document`
- `rhino_router_list_documents` returns bounded descriptor/attestation status for routable and
  unavailable documents. `rhino_router_select_document` requires one opaque session id and commits
  selection only after fresh attestation. `rhino_router_get_selected_document` revalidates liveness
  and clears/returns a closed selection rather than reporting stale success.
- Reserve the `rhino_router_` prefix. If a backend exposes a colliding name, keep router controls
  available but reject that backend surface as incompatible.
- Mark list/get controls read-only, non-destructive, and closed-world. Mark select
  `ReadOnly = false, Destructive = false, OpenWorld = false` because it changes connection-scoped
  routing state; it still does not activate a Rhino window or mutate a Rhino document.
- Advertise `tools.listChanged` and send `notifications/tools/list_changed` when a first canonical
  surface becomes available or the pinned compatible set changes. Clients that do not refresh must
  receive a clear reconnect instruction; stale schemas must never be used.
- Preserve the current static reference surfaces by proxying `resources/list`,
  `resources/templates/list`, and `resources/read` through a canonical compatible backend. Do not
  advertise resource subscriptions because the current resources are static and subscriptions are
  not relayed.
- Advertise only frontend capabilities the router deliberately implements: initialize/ping, tools
  list/call/list-changed, registered resource list/templates/read, cancellation, and progress if the
  SDK path proves it can be relayed end-to-end. Do not advertise sampling, roots, elicitation,
  prompts, tasks, subscriptions, or other reverse/callback features unless separately implemented
  and tested.

### 6. Route Endpoint Attestation And Strict Live Access

- Add a route-only custom MCP request, `mcp-rhino/routed-document-info`. It is not a public tool or
  resource and is registered only by routed hosts.
- Return registry/protocol version, document session id, plugin-instance generation, endpoint nonce,
  PID and process-start identity, runtime serial, pipe name, current live saved path, and server
  version. Compute tool/resource fingerprints in the router after normal MCP initialization rather
  than trusting registry metadata.
- Before making an endpoint routable, compare every attestation field with the descriptor and pipe
  identity. A forged, stale, or mismatched descriptor is quarantined without calling business tools.
- Add `RoutedHostFactory` and `RoutedLiveRhinoDocumentAccessor`. The routed accessor resolves
  `RhinoDoc.FromRuntimeSerialNumber` like the panel-bound accessor but also requires the request's
  normalized top-level `filePath` to match the current live document path. This makes direct
  `MCP_Rhino.Bridge.exe --pipe mcp_rhino_route_...` calls safe without changing the deliberate
  file-path-ignore behavior of panel/Companion endpoints.

### 7. Deterministic Routing Rules

- A selected document session is scoped to the current router/MCP client connection; never store one
  machine-global selected target.
- Snapshot the selected target atomically when each request begins. A later select call cannot
  retarget an in-flight read or mutation.
- For a Rhino tool whose top-level schema contains `filePath`:
  1. If a document is selected, require the supplied normalized path to match the selected live
     document. Otherwise return `DOCUMENT_TARGET_CONFLICT` before forwarding.
  2. If no document is selected, route by a unique normalized `filePath` match.
  3. If the path is open in multiple Rhino processes, return `DOCUMENT_TARGET_AMBIGUOUS` and require
     selection by document session id.
  4. If no open endpoint matches, return `DOCUMENT_NOT_OPEN`.
- For tools without a document path in their dynamically obtained schema, route through the selected
  endpoint only. With no selection, return `NO_DOCUMENT_SELECTED` and make no backend call. Do not
  maintain a hard-coded list of reference/logging tool names and never choose an arbitrary primary
  endpoint for execution.
- Never silently fall back to the global debug pipe or disk access after a routing failure.
- If a selected document closes, clear that selection. Reopening the same file creates a new session
  id and never inherits the prior selection.

### 8. Backend Connection, Concurrency, And Shutdown

- Each route endpoint must support bounded concurrent client connections so multiple repository
  sessions can coexist. Preserve existing single-connection behavior for the global debug pipe and
  panel/Companion pipe unless testing proves a safe reason to change them.
- Implement a route-only bounded accept mode that accepts the next client while tracking a separate
  MCP host/session and task for every connection. Listener and client tasks must all be canceled,
  observed, and disposed; no background task may survive document/plugin shutdown.
- Continue serializing RhinoCommon operations through the owning Rhino UI thread. Concurrent router
  connections must not introduce parallel document mutations outside Rhino's existing main-thread
  and Undo-record contracts.
- Document-close handlers must not block the Rhino UI for one five-second wait per client/document.
  Use the two-phase stop described above and one aggregate bounded shutdown/drain window.

### 9. Failure Contract And Transport Hygiene

- Return known router tool failures as MCP `CallToolResult` values with `isError = true` and a
  structured payload containing at least `code`, `retryable`, and `outcomeUnknown`.
- Define and test at least: `NO_DOCUMENT_SELECTED`, `DOCUMENT_NOT_OPEN`,
  `DOCUMENT_TARGET_AMBIGUOUS`, `DOCUMENT_TARGET_CONFLICT`, `DOCUMENT_ENDPOINT_UNAVAILABLE`,
  `TOOL_SURFACE_MISMATCH`, `ROUTER_CONTROL_TOOL_COLLISION`, and
  `MUTATION_OUTCOME_UNKNOWN`.
- Before backend dispatch, routing failures have `outcomeUnknown = false`. If a mutation disconnects
  or times out after dispatch, return `MUTATION_OUTCOME_UNKNOWN`, set `outcomeUnknown = true`, and
  never replay it automatically. The same no-replay rule applies to a mutation returning
  `RHINO_MAIN_THREAD_BUSY`, because queued UI-thread work may already have an uncertain outcome.
- Propagate frontend cancellation to the exact backend SDK call. Relay progress deliberately and
  test it; if exact relay is not feasible, do not advertise progress support.
- Write MCP frames only to stdout. Send all diagnostics, registry warnings, and backend status to
  stderr so the stdio transport cannot be corrupted.

### 10. Compatibility And Rollout

- Keep `MCP_Rhino.Bridge.exe` default and `--pipe` behavior unchanged.
- Keep `.mcp.json` on the bridge during development. Switch it to `MCP_Rhino.Router.exe` only after
  the router handshake, multi-document routing, packaged-install, startup-load, and rollback checks
  pass; the committed configuration must resolve the installed path rather than a repository build
  artifact.
- Require the Release plugin for participating multi-document route endpoints. The Debug plugin
  continues to support only the fixed developer pipe.
- Keep Companion-generated and panel-generated MCP configurations pointed directly at their own
  panel-bound pipes; they do not route through the new external router.
- Document that the `.rhp` is the Rhino plug-in itself: installation makes Rhino discover it and its
  `AtStartup` load-time contract activates it. The Router is a separate stdio MCP gateway that agents
  launch automatically because it cannot run inside the Rhino UI process or share one stdio stream
  across independent agent sessions.

## Planned Work Breakdown

### Phase 1: Transport Contracts And Discovery

- Define the versioned descriptor/session-id contract and current-user registry location.
- Add path normalization, lifecycle generation, ACL, atomic publication, and descriptor validation
  rules in the required BCL-only `MCP_Rhino.Transport` project.
- Add stale-entry validation using PID, process start time, and generation-safe cleanup.
- Add route-pipe naming without changing existing debug or panel pipe names.
- Add a feasibility smoke proving `MCP_Rhino.Transport.dll` copies and loads correctly across the
  plugin's default/isolated ALC packaging without sharing its runtime types across that boundary.

### Phase 2: Rhino Plugin Route Endpoints

- Add the Release-only routed endpoint dispatcher and document event lifecycle.
- Override the plugin load-time contract so installed Release builds load at Rhino startup while
  Debug bridge-only builds remain manual/when-needed.
- Add route endpoint ready/start/stop APIs to `ServerBootstrap` using a new `RoutedHostFactory` and
  strict routed document accessor.
- Add bounded concurrent connection support for route endpoints.
- Publish/update/remove discovery descriptors atomically.
- Add the private routed-document attestation request.
- Verify that route endpoint startup never opens Companion or a Rhino panel.
- Verify that routing failure is isolated from plugin load, debug-pipe ownership, panel/Companion,
  and other documents.

### Phase 3: External MCP Router

- Add the router project and stdio entry point.
- Implement SDK-based frontend handlers and backend `McpClient` sessions; do not implement raw
  JSON-RPC multiplexing.
- Implement registry discovery, live attestation, deterministic canonical-surface selection,
  endpoint quarantine, and generation-safe stale cleanup.
- Dynamically proxy the complete canonical tool metadata and registered reference resource/template
  surfaces.
- Implement the three router control tools and per-client document selection.
- Implement path-based routing, ambiguity/conflict errors, cancellation, disconnect handling, and
  no-replay mutation semantics.
- Implement list-changed notification, stdout/stderr isolation, pagination, collision detection,
  and progress relay or explicit progress non-support.

### Phase 4: Package, Install, And Client Launch Integration

- Add a deterministic Release staging target that gathers the `.rhp`, exact plugin dependencies,
  Transport, Router, Bridge, Companion, configuration templates, and version manifest from one clean
  build. Fail on Debug artifacts, missing dependencies, version skew, or duplicate destinations.
- Add current-user install, upgrade/rollback, repair/validate, and uninstall operations. Resolve
  Rhino 8's supported auto-install location and the product's stable external executable root;
  never install from or point clients back into `src/.../bin`.
- Add supported-client configuration integration that records only its owned edits, preserves
  unrelated settings, and generates a generic stdio example with the resolved absolute Router path.
- Validate automatic startup in a fresh Rhino process without `_LoadPlugin`, `_Mcpchat`, panel
  activation, Companion launch, or a pre-running agent.
- Validate two independent MCP clients automatically starting separate Router processes against the
  same installed Rhino endpoints without pipe ownership conflicts.

### Phase 5: Configuration And Documentation

- Update the solution and build outputs.
- Update `.mcp.json` after successful validation.
- Update architecture and runtime workflow documentation with the debug/panel/router transport
  split and troubleshooting guidance.
- Document install/repair/uninstall, Release startup-load verification, automatic per-agent Router
  launch, and how to list, select, switch, and verify open documents.
- State the one-time boundary clearly: a supported MCP client must be registered with the installed
  Router once, after which it launches the Router automatically for every agent session.

### Phase 6: Tests And Live Validation

- Register the unique CLI slug `multi-document-rhino-router-smoke-test` through exactly one partial
  file in `Project_Test/260729_TEST_multi-document-rhino-router/`, add its hook to
  `DeveloperCommandHandler.cs`, and add the dedicated Rhino command
  `_McpMultiDocumentRhinoRouterSmoke`.
- Add a standalone BCL/SDK console protocol-smoke project under the TEST folder for deterministic
  fake backends. Explicitly exclude that subtree from `MCP_Rhino.Server.csproj`'s broad
  `Project_Test/**/*.cs` compile glob; keep only the DeveloperCommandHandler registration partial in
  the Server compilation.
- Add Server smoke coverage for route-pipe naming, Release/Debug gating, descriptor lifecycle, and
  concurrency configuration, plus Release `AtStartup` and Debug manual-load metadata.
- Add package-manifest, clean-install, repair, version-skew rejection, owned-config preservation,
  stable-path, uninstall, and rollback smokes in disposable current-user test locations before live
  Rhino installation tests.
- Run one-process/multi-document and two-process live Rhino validation against saved test fixtures,
  not production project files.
- Re-run existing debug bridge, panel/Companion, tool inventory, and safety annotation smokes.

## Involved Files

Expected new or changed production files include:

- `MCP_Rhino.sln`
- `.mcp.json`
- `src/MCP_Rhino.Transport/MCP_Rhino.Transport.csproj`
- `src/MCP_Rhino.Transport/Routing/*`
- `src/MCP_Rhino.Router/MCP_Rhino.Router.csproj`
- `src/MCP_Rhino.Router/Program.cs`
- `src/MCP_Rhino.Router/Routing/*`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpPipeNames.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/ServerBootstrap.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/plugin.manifest`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Routing/*`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/RoutedHostFactory.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/RoutedLiveRhinoDocumentAccessor.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpMultiDocumentRhinoRouterSmokeCommand.cs`
- `Packaging/MCP_Rhino/package-manifest.json`
- `Packaging/MCP_Rhino/Install-McpRhino.ps1`
- `Packaging/MCP_Rhino/Uninstall-McpRhino.ps1`
- `Packaging/MCP_Rhino/ClientConfigs/*`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Runtime_Workflow/MCP_Rhino Workflow.md`

Required construction artifacts:

- `Project_Plan/260729_PLAN_multi-document-rhino-router.md`
- `Project_Test/260729_TEST_multi-document-rhino-router/`
- `Project_Exet/260729_EXET_multi-document-rhino-router.md`

The TEST folder will contain:

- `DeveloperCommandHandler.MultiDocumentRhinoRouterSmokeTest.cs` as the sole CLI slug registration
  partial compiled into `MCP_Rhino.Server`.
- `RouterProtocolSmoke/` as a standalone console smoke project with fake MCP backends, explicitly
  removed from the Server project's broad TEST-source compile glob.
- `README.md` with Debug/Release, protocol-smoke, direct-bridge, and live Rhino commands.

The final file list may narrow during execution. Any material deviation must be recorded in the
EXET rather than silently rewriting this plan.

## Intended Usage

Run the packaged current-user installer once. It places the Release plugin where Rhino 8 discovers
it, installs the external executables under the stable product root, and, only when requested, writes
the `mcp-rhino` entry for a supported MCP client. For all other clients it prints a ready-to-use
snippet with the actual resolved path.

The resulting client entry has this shape; the installer replaces the illustrative user segment
with the resolved absolute installed path:

```json
{
  "mcpServers": {
    "mcp-rhino": {
      "type": "stdio",
      "command": "C:\\Users\\<user>\\AppData\\Local\\MCP_Rhino\\bin\\MCP_Rhino.Router.exe",
      "args": []
    }
  }
}
```

Runtime flow:

1. Launch Rhino normally. The installed Release `.rhp` loads at startup once in that process and
   publishes route endpoints for all saved documents without opening MCP Chat or a panel.
2. Start any configured agent. Its MCP client automatically launches a private Router stdio process;
   the user does not run `MCP_Rhino.Router.exe` manually.
3. Call `rhino_router_list_documents`.
4. Choose a routable document session id and call `rhino_router_select_document`.
5. Call existing Rhino tools unchanged, using the selected document's exact saved `filePath`.
6. Call `rhino_router_get_selected_document` when verification is needed.
7. Select another session id to switch documents without closing or restarting Rhino. Other agents
   keep their own selections and may operate concurrently.

If no saved Release-plugin document is routable when the router starts, the MCP server still loads
and exposes its three control tools. After a compatible endpoint appears it emits
`notifications/tools/list_changed`; clients that do not refresh reconnect the MCP task once.

There is no shared Router service to start. Rhino hosts the multi-client route endpoints; every MCP
client starts and stops its own Router along with its agent session. Calls within one Rhino process
still enter Rhino's UI-thread execution queue, while different Rhino processes may progress
independently.

Direct developer diagnostics remain available:

```powershell
& "$env:LOCALAPPDATA\MCP_Rhino\bin\MCP_Rhino.Bridge.exe"
& "$env:LOCALAPPDATA\MCP_Rhino\bin\MCP_Rhino.Bridge.exe" --pipe mcp_rhino_route_<ProcessId>_<RuntimeSerialNumber>
```

## Acceptance Criteria

### Build And Static Validation

- `dotnet build .\MCP_Rhino.sln -c Debug` succeeds.
- `dotnet build .\MCP_Rhino.sln -c Release` succeeds.
- Both builds include the standalone Router protocol-smoke project without compiling its source into
  `MCP_Rhino.Server` through the broad TEST glob.
- Debug still produces a bridge-pipe-only `.rhp` and does not start route or panel-bound endpoints.
- Debug creates no route registry descriptors.
- Debug retains manual/when-needed plug-in load metadata and is absent from the installable bundle.
- Release declares `PlugInLoadTime.AtStartup` and starts route endpoints without opening UI or
  launching Companion.
- `MCP_Rhino.Transport` remains BCL-only, has only the required Server/Router incoming references,
  copies into plugin output, and loads in Debug and Release without an ALC/type-identity conflict.
- Existing Rhino MCP tool names and complete protocol metadata remain semantically identical through
  the router. Existing reference resources/templates remain available with equivalent metadata and
  content.
- Tool inventory and safety annotation smokes pass in Debug and Release.
- Router-specific inventory/safety smoke validates its three reserved control tools and exact
  annotations.
- The unique CLI slug `multi-document-rhino-router-smoke-test`, one matching registration partial,
  and `_McpMultiDocumentRhinoRouterSmoke` command satisfy the Plan Log entry-point contract.
- The router has no RhinoCommon reference and does not implement Rhino geometry/document business
  logic.
- A clean packaging target produces one internally compatible Release bundle containing the `.rhp`,
  required dependencies, Router, Bridge, Companion, manifest, and client templates; it rejects mixed
  configuration/version inputs and any Debug `.rhp`.
- Package validation proves all normal MCP client commands resolve to the installed absolute Router
  path and contain no repository, build-output, or current-working-directory dependency.

### Router Protocol Validation

- With no backend, the router initializes successfully, exposes only its controls, and emits a
  list-changed notification when the first canonical backend becomes available.
- Paginated fake backend tool/resource listings are fully collected.
- With multiple fake document backends, one router session can list them and call the same Rhino tool
  against each backend after switching selection.
- Frontend/backend protocol negotiation is independent, and the router advertises no unsupported
  sampling, roots, elicitation, prompts, tasks, resource-subscription, or reverse-request capability.
- Complete canonical tool metadata and resource/template metadata match the backend after canonical
  JSON comparison, excluding the three router controls.
- Canonical fingerprint-group selection is deterministic regardless of registry enumeration order.
- A backend collision with the reserved `rhino_router_` control namespace is rejected without hiding
  the router controls.
- A unique unselected `filePath` routes to the correct endpoint.
- The same path registered by two processes returns `DOCUMENT_TARGET_AMBIGUOUS` until a session id
  is selected.
- A selected session plus a conflicting `filePath` returns `DOCUMENT_TARGET_CONFLICT` and performs
  no backend call.
- A backend tool without top-level `filePath` and without an explicit selection returns
  `NO_DOCUMENT_SELECTED` and performs no backend call.
- A closed/stale endpoint returns `DOCUMENT_ENDPOINT_UNAVAILABLE` or `DOCUMENT_NOT_OPEN` without
  mutation replay.
- Forged/stale descriptors fail the private attestation field comparison and are quarantined before
  any business-tool call.
- Partial/corrupt/unknown-version descriptors are ignored and reported without failing the router.
- A transient pipe timeout does not delete a descriptor owned by a live matching process generation.
- Tool-surface mismatch quarantines only the incompatible endpoint and reports
  `TOOL_SURFACE_MISMATCH`.
- List/get controls are read-only; select is explicitly non-read-only, non-destructive, and
  closed-world.
- Multiple router clients can connect to one route endpoint without blocking panel/Companion pipes.
- Two frontend clients maintain independent selected documents. Each in-flight call retains the
  target snapshot taken at dispatch even if a later selection changes.
- Two MCP client processes can each auto-launch their own Router process, connect concurrently to the
  same endpoints, and select different documents without a global Router lock, shared stdio stream,
  or manually pre-started executable.
- Cancellation reaches the exact backend call. Progress is relayed and tested or is absent from the
  advertised capability.
- A mutation processed and then disconnected is invoked exactly once, returns
  `MUTATION_OUTCOME_UNKNOWN` with `outcomeUnknown = true`, and is never replayed.
- Router stdout contains MCP frames only; diagnostics appear only on stderr.

### Live Rhino Validation

- From a clean current-user install, launch Rhino 8 normally with no `_LoadPlugin`, `_Mcpchat`, panel,
  Companion, Bridge, Router, or agent process already running. Verify the Release plugin loads at
  startup and publishes the saved document endpoint without user action.
- Start two Rhino processes normally and verify both startup-load the same installed Release build
  once per process. Opening additional documents must add endpoints without loading another plugin
  instance inside that process.
- One Release Rhino process can expose two saved test documents when Rhino supports multiple open
  documents in-process, while a second Release process exposes a third fixture. If the installed
  Rhino build enforces one document per process, record that limitation and validate three processes
  instead; do not weaken cross-process coverage.
- `rhino_router_list_documents` returns every fixture with distinct opaque session ids and the
  correct PID, runtime serial, current path, routable state, and route pipe.
- `get_document_summary` succeeds against each document through one router MCP session without
  foreground activation, pipe transfer, Rhino restart, or `FILE_NOT_ACTIVE`.
- A reversible mutation against fixture A affects only fixture A and creates one Undo entry; the
  same verification is repeated for fixture B.
- First Save makes an unsaved listed document routable. Save As retains its session id/pipe while
  updating its verified current path.
- Closing one fixture removes its route while the others remain controllable. Reopening the same path
  receives a new session id and does not inherit stale selection.
- Starting the router after Rhino is already open discovers existing endpoints; opening/saving a new
  document after router initialization updates discovery/list state.
- Starting a configured Codex/test MCP session launches the Router automatically from its stable
  installed location. A second configured agent launches an independent Router and both can control
  different selected fixtures concurrently.
- Move or rename the source repository after packaging (or run validation from an unrelated working
  directory) and verify installed Rhino startup plus agent Router launch remain functional.
- A stale registry descriptor from a terminated Rhino process is rejected and cleaned up.
- A second Release Rhino process publishes route endpoints even while the first owns the fixed
  `mcp_rhino` debug pipe.
- Direct bridge access to a route pipe succeeds only with the actual current routed path; a conflicting
  path is rejected by the route-specific accessor.
- `_Mcpchat`/Companion can run for a document while the external router also controls that document;
  neither connection occupies or stops the other.
- Closing documents and shutting down the plugin with long-lived route clients completes within one
  aggregate bounded drain window and does not block the Rhino UI once per client/document.
- The fixed debug bridge handshake still succeeds against its one owning Rhino process.

### Documentation And Rollback Validation

- Architecture and runtime workflow documents clearly distinguish debug, panel, and router paths.
- Installation documentation states that the `.rhp` is the startup-loaded Rhino component and the
  Router is an automatically launched per-agent stdio component; no manual Router daemon step is
  shown in the normal workflow.
- A repair operation restores a deleted/corrupt owned file without duplicating the Rhino plugin or
  unrelated MCP client entries.
- An upgrade with active Rhino/Router processes either completes as one compatible bundle or stages
  and reports the required process restart; it never exposes a mixed Server/Transport/Router set.
- Client rollback restores `.mcp.json` or another owned client entry to its recorded prior state;
  dormant route endpoints may remain until a full capability rollback.
- Full rollback disables/removes the route dispatcher, waits for route endpoint shutdown, and removes
  only matching-generation registry descriptors while leaving global debug and panel/Companion
  routes intact.
- Uninstall removes the installed Release bundle and only installer-owned client configuration. On
  the next Rhino launch the plugin does not auto-load, while user `.3dm` files, unrelated MCP entries,
  source builds, and live descriptors from other generations remain untouched.
- After full rollback, the direct debug handshake and simultaneous panel/Companion smoke both pass.

## Risks And Mitigations

- **MCP gateway capability drift**: frontend and backend capabilities can be accidentally collapsed
  or over-advertised. Mitigate by using the pinned SDK on both legs, declaring a narrow supported
  capability matrix, and testing negotiation, pagination, cancellation, progress, and resources.
- **Wrong-document mutation**: a stale selection or duplicate path could route a destructive call
  incorrectly. Mitigate with connection-scoped opaque session ids, live endpoint verification, path
  conflict checks, and rejection rather than fallback.
- **Stale discovery files and cleanup races**: crashes can leave descriptors behind, while an
  over-eager router could delete a live process's entry. Mitigate with PID/start/generation checks,
  atomic writes, plugin ownership, live attestation, and quarantine on transient failures.
- **Tool-surface drift across Rhino processes**: different plugin builds can expose incompatible
  schemas or reference resources. Mitigate with complete canonical metadata fingerprints,
  deterministic group selection, and endpoint quarantine.
- **Panel/router contention**: reusing the panel pipe would allow one persistent client to block the
  other. Mitigate with a distinct route pipe and independent lifecycle.
- **Concurrent mutations**: multiple clients could submit writes close together. Mitigate by keeping
  Rhino main-thread serialization and one Undo record per apply call; never add automatic mutation
  retries, and report outcome uncertainty after post-dispatch disconnect/timeout.
- **Route shutdown stalls Rhino**: persistent clients can multiply the current synchronous five-second
  disposal wait. Mitigate with descriptor-first closing, asynchronous cancellation, task tracking,
  and one aggregate bounded drain window.
- **Plugin load/package complexity**: a shared transport assembly can create load-context or copy
  problems, especially across Rhino's default and isolated ALCs. Mitigate with a BCL-only dependency,
  primitive/JSON reflection boundaries, one-build bundle validation, adjacent dependency checks, and
  Debug/Release copy/load validation.
- **Startup-loaded plugin affects every Rhino process**: an initialization defect would be encountered
  on every normal Rhino launch. Mitigate by keeping `OnLoad` lightweight, isolating dispatcher
  failures from plugin load, never opening UI or launching external processes from startup, and
  validating clean start plus recovery with a deliberately unavailable registry/pipe path.
- **Duplicate or wrong plugin registration**: a manually registered Debug build or stale Release copy
  can win discovery for the same plug-in GUID. Mitigate with current-user install discovery checks,
  a Release-only package, manifest-based repair/uninstall, explicit duplicate diagnostics, and no
  silent reassignment of an existing conflicting registration.
- **Version skew and locked installed binaries**: Rhino and agent-owned Router processes can keep
  files loaded during upgrade. Mitigate with a single compatibility manifest, preflight process/file
  checks, staged replacement, restart-required reporting, and rollback instead of partial overwrite.
- **Client configuration drift**: MCP clients use different configuration scopes and formats, and an
  installer could overwrite unrelated settings. Mitigate with explicit supported-client adapters,
  opt-in writes, semantic merge plus backup, recorded ownership, stable absolute command paths, and
  generic snippets for unknown clients.
- **Route readiness race**: publishing before listener creation can advertise an endpoint that never
  bound. Mitigate with an explicit listener-ready/failure result and publish only after readiness.
- **Local metadata/pipe exposure**: another local account must not enumerate or invoke document
  routes. Mitigate with current-user registry ACLs and `PipeOptions.CurrentUserOnly`.
- **Mapped-drive aliases**: equivalent paths may appear with different drive/UNC forms. The first
  version uses normalized case-insensitive saved paths plus explicit selection; it must not guess
  equivalence when identity is uncertain.
- **Client tool refresh behavior**: some MCP clients may not refresh a tool list after Rhino starts.
  The router still initializes with controls, emits `tools/list_changed`, and reports a clear
  reconnect instruction when the client ignores refresh notifications.

## Rollback

- Restore every installer-owned MCP client entry from its recorded prior state; for repository debug
  use, restore `.mcp.json` to launch `MCP_Rhino.Bridge.exe` without arguments.
- Stop agent-owned Router processes and Rhino, then use the installation manifest to restore the
  previous compatible bundle or remove the installed product root and Release auto-install plugin.
  Never remove a user-managed file or client entry that no longer matches the recorded owned value.
- Disable/remove the Release routed endpoint dispatcher, stop all route listeners/clients under one
  bounded drain, and remove only descriptors matching the current plugin generations.
- Remove the Router and Transport projects from the solution after Server references are removed.
- Leave `mcp_rhino`, panel-bound pipe naming, `BoundLiveRhinoDocumentAccessor`, Companion, and panel
  behavior unchanged.
- Re-run direct debug bridge and panel/Companion coexistence validation after rollback.
- Remove only the router-specific test and documentation additions. No `.3dm` migration or document
  repair is required.

## Future Extensions

- Yak publishing, signed installer/MSI, machine-wide deployment, and managed enterprise client
  configuration after the current-user install contract is proven.
- Optional in-session migration to a newer canonical tool surface; the first version pins a surface
  and requires reconnect rather than swapping incompatible schemas.
- A Rhino/Companion document picker backed by the same discovery contract.
- Optional endpoint health and active-request diagnostics.
- Explicit support for unsaved documents after a separate review of identity and save semantics.
- Opt-in authenticated remote routing as a separate capability; it must not be inferred from this
  local current-user design.
