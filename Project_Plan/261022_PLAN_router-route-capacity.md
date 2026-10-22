# Router Route Capacity Plan

Status: approved by the user on 2026-10-22 and implemented on branch
`claude/router-route-capacity`; see [261022_EXET_router-route-capacity](../Project_Exet/261022_EXET_router-route-capacity.md)
for deviations, validation and the remaining live, cap-measurement and packaging steps.

## Background

On 2026-10-22 a freshly started Router (`%LOCALAPPDATA%\MCP_Rhino\bin\MCP_Rhino.Router.exe`,
1.3.7) got retryable `DOCUMENT_ENDPOINT_UNAVAILABLE` from `rhino_router_select_document` for an
open, saved test document. It kept failing through about 6 minutes of retries. In the same period,
a long-running Router in another session selected the same document on its first try. Found while
validating the 1.3.8 worker license fix (`261022_EXET_worker-license-identity`, branch
`claude/suspicious-meninsky-72109b`). That fix is separate from this work.

Each agent session launches its own Router, and the user normally keeps many sessions open. A
read-only check on 2026-10-22 found 11 `MCP_Rhino.Router.exe` processes, all with live parents:
10 Claude Code sessions started between 10-20 and 10-22, plus one other MCP client
(`DeepSeek Harness.exe`) running since 10-19. The route registry happened to be empty at that
moment (no saved document was published), so no live route was probed.

## Findings

The hypothesis is confirmed. Reproduction also found three more defects that make starvation
worse and affect correctness.

1. **Slots are owned for a Router's whole lifetime.** `ServerBootstrap` starts every document
   route with `maxConcurrentConnections: 8`. Answering the client's first `tools/list` makes
   `BackendCatalog.RefreshAsync` connect to every routable document. It caches each connection
   until the descriptor disappears or changes. `RouterCatalogMonitor` then refreshes every second,
   so an idle Router holds one slot on every saved open document, and gets it back within seconds
   if it loses it. A ninth Router's `pipe.ConnectAsync(3000)` times out. That Router's `tools/list`
   then contains no live tools, and `select_document` returns retryable
   `DOCUMENT_ENDPOINT_UNAVAILABLE` with "not open and attested". Retrying cannot succeed while the
   eight holders stay alive.
2. **Every Save makes every Router reconnect.** On `EndSaveDocument` the plug-in rewrites the
   descriptor with a new `LastUpdatedUtc`. `BackendCatalog`'s cache key includes
   `LastUpdatedUtc.UtcTicks`, so every Router drops its connection and reconnects. Effects:
   - Slot ownership reshuffles at random, so a Router with a working selection can lose its slot.
   - Per-connection server state is discarded. `LiveRhinoScriptExecutor` and
     `GrasshopperDefinitionRegistry` are singletons of each connection's host. The
     `apply_rhino_script` contract says "status/token retention is scoped to this backend
     connection", and disposing the host requests cancellation of running scripts.
   - A call in flight on the replaced client throws `TaskCanceledException`. `ProxyToolAsync`
     rethrows it, so the call skips the `MUTATION_OUTCOME_UNKNOWN` guard that the Runtime Workflow
     promises.
3. **Connections leak under contention.** `ConnectForSnapshotAsync` passes `AddOrUpdate`
   factories that have side effects: they start a real `ConnectAsync`. `AddOrUpdate` can run those
   factories more than once, and it discards the losing results without disposing them. The
   failure path calls `TryRemove(sessionId)` without checking which entry failed, so it can evict a
   newer pending entry. While the route is full, connects wait up to 3 s, so these races are likely.
   An evicted pending connect that later wins a freed slot holds it, owned by nobody, until the
   Router process exits. This drains capacity below 8.
4. **A registry read can stop a route (minor, rare).** `RouteRegistry.ReadAll` uses
   `File.ReadAllText`, which does not allow delete sharing, so a concurrent read can make the
   plug-in's `File.Move(..., overwrite: true)` fail. When that write fails, `TryWriteDescriptor`
   stops the document's route pipe. The harness hit this only with an unrealistic loop of about
   3,400 calls/s. At about 1 Hz per Router it was not observed, but its probability is not zero.

### Reproduction (no Rhino, user's files untouched)

The harness compiles the production `McpNamedPipeServer.cs` unchanged against a `RhinoApp` shim
aliased as `rhinocommon`, using `ServerBootstrap`'s exact route arguments. It drives the real Router
`BackendCatalog`, `RouterRuntime`, and `RouteDiscovery` against a private temporary
`RouteRegistry`, so real Routers on the machine never see the fake endpoint. Only the
per-connection MCP host is fake. The monitor loop calls `RefreshAsync` every second, which is the
same call `RouterCatalogMonitor` makes. Source and logs are kept outside the repository in the
session notes folder (see Related evidence).

| Run | Scenario | Observed |
| --- | --- | --- |
| 1 and 1b | 8 Routers list tools, then idle | 8 server connections stay held. |
| 1 and 1b | 9th Router starts | `tools/list` took 3,007 ms and returned 8 tools (3 Router controls and 5 offline; no live tools). `list_documents` reported `routable=false`, status "The operation has timed out." 5/5 `select_document` attempts returned `DOCUMENT_ENDPOINT_UNAVAILABLE`, retryable, after 2.0-3.0 s each. The long-running R1 selected successfully. |
| 1 | One holder exits | The 9th Router still failed for 15 s with 8 active connections, because the freed slot went to an orphan. In 1b it succeeded 5 ms after the exit. |
| 1 | 9 Routers for 8 slots, 10 Saves | Each Save caused 7-9 reconnects, and the starved set changed every time. The number of starved Routers grew from 2 to 4. R1, which had a working selection, lost its slot after Save 10. |
| 1b | Same | The starved Router changed 5 times; R1 kept its slot. |
| 1 / 1b | All Routers disposed | 3 / 1 server connections still active: orphans. |
| 2 (control) | 1 Router, calls at 10 Hz, 20 Saves | 1 active connection throughout, 0 failed calls, 0 orphans, and 0 write collisions. One reconnect per Save. |
| 2 | One Save | The per-connection server identity changed, so per-connection state was reset. |
| 2 | Save 1 s into a 4 s non-read-only call | The Router threw `TaskCanceledException` out of `tools/call`, with no `MUTATION_OUTCOME_UNKNOWN`. The backend call completed (cancelled 0, completed 1). |

## Goal

- An idle agent session holds no route connections.
- A new Router can select any open saved document on its first attempt, however many sessions are
  open.
- A Save never disturbs existing connections.
- A route that really is full returns a distinct, actionable error.

## Options weighed

| Option | Fixes | Limits | Decision |
| --- | --- | --- | --- |
| A. Raise the per-route limit, or make it configurable (plug-in) | Gives immediate headroom after Rhino restarts, whatever Router versions are running. | Does not fix defects 2-4. Rhino-side cost grows with idle sessions × documents (each connection builds a full host and DI container with the whole tool set). The OS limit is 254 instances. A setting would need an owner, and it invites very large values. | Adopt as headroom only: a constant 32, no setting, after measuring per-connection cost. |
| B. Connect on demand; release idle connections (Router) | Removes the root cause: connections follow use, not Router lifetime. | Releasing a connection that was used for work loses per-connection script and Grasshopper state, and can cancel a running script. | Adopt on-demand connections. Release only probe connections; keep connections used for work for the Router's lifetime (bounded by the documents the session actually used). |
| C. Detect surface changes without holding connections | Lets the monitor read descriptor files only. Probes connect only when a new plug-in instance appears. | Assumes every document in one plug-in instance has the same surface. The fingerprint is still checked on every work connection. | Adopt. |
| D. Clearer error for a full route | `DOCUMENT_ENDPOINT_BUSY` tells agents and users what to do, instead of a misleading "not open". | Needs a check that does not consume a pipe instance. | Adopt. |
| E. Shared Router daemon or multiplexer | Would remove per-session connections. | Contradicts the Router-only contract ("no shared Router daemon"). | Reject. |
| F. Unlimited instances, or a slim shared host per connection in the plug-in | Removes the cap. | Unbounded Rhino resources. Per-connection isolation is part of the scripting contract. | Reject. |

## Design

### Stage 1: Router and Transport (takes effect for each new agent session; no Rhino restart)

1. **Correct the connection cache** (`BackendCatalog`):
   - Single-flight connect per document. Use factories with no side effects, so a connect starts
     only after winning the cache slot; dispose any loser.
   - Remove entries only when they are the exact failed entry (compare-and-remove).
   - Build the generation key from `PluginInstanceGeneration`, `DocumentLifecycleGeneration`,
     `EndpointNonce` and the normalized `FilePath`. Drop `LastUpdatedUtc`, so a plain Save reuses
     the existing connection.
   - When the key really changes (Save As, listener restart), retire the old connection: give it no
     new calls, and dispose it only after its in-flight calls drain (a reference-counted lease per
     call).
2. **Two connection kinds**:
   - *Probe* connections (surface discovery, and attestation for `list_documents`): connect, do the
     work, close at once, never cache.
   - *Work* connections: opened by `select_document`, or by a proxied call that targets a document
     through `filePath`. Cached and held until the document closes, its generation changes, or the
     Router exits.
   - An idle Router therefore holds only the work connections for documents it has actually used.
     A session that never touched Rhino holds none.
3. **Surface cache** (Option C):
   - Cache the tools, resources, templates and fingerprint per (`PluginInstanceGeneration`,
     `PluginVersion`), filled by a single probe of one document in that instance.
   - Choose the canonical group by fingerprint, weighted by document count, as today.
   - `RouterCatalogMonitor` re-reads descriptors only. It probes when a new plug-in instance key
     appears, and sends `tools/list_changed` when the canonical fingerprint changes.
   - Every new work connection still verifies attestation, and checks that its fingerprint matches
     the cached surface before calls are routed.
4. **Errors** (Option D):
   - When a connect times out, decide whether the pipe exists without consuming an instance
     (enumerate `\\.\pipe\` or call `WaitNamedPipe`, and prove in a test that no instance is
     consumed).
   - Pipe exists → `DOCUMENT_ENDPOINT_BUSY`, retryable. The message says other agent sessions hold
     every slot, that ending idle sessions frees them, that the document is open, and that offline
     tools are not a substitute.
   - Pipe missing → `DOCUMENT_ENDPOINT_UNAVAILABLE`, saying the listener is not running.
   - `select_document` and `list_documents` report the document's own diagnostic: `ENDPOINT_BUSY`,
     `ENDPOINT_NOT_LISTENING`, `ROUTE_ATTESTATION_MISMATCH`, `OWNER_PROCESS_GONE`, and so on.
     "Not open and attested" is no longer the catch-all.
   - Keep the existing payload fields.
5. **Disconnect classification** (`ProxyToolAsync`): an `OperationCanceledException` while the
   caller's token is not cancelled is a disconnect. It returns `MUTATION_OUTCOME_UNKNOWN` for
   non-read-only tools and retryable `DOCUMENT_ENDPOINT_UNAVAILABLE` for reads. Real caller
   cancellation still propagates.
6. **Registry reads** (`RouteRegistry.ReadAll`): open with
   `FileShare.ReadWrite | FileShare.Delete`, so readers never block the plug-in's atomic replace.

### Stage 2: plug-in (needs package installation and a Rhino restart)

1. `ServerBootstrap`: add a named constant `RouteMaxConcurrentConnections = 32`, replacing the
   literal 8.
   - Measure Rhino private bytes with 0, 8 and 32 idle connections on a disposable document first.
   - If one idle connection costs more than 20 MB, use 16 and record why.
   - This is headroom for the rollout period, while old long-lived Routers still hold slots, and
     for several sessions working on one document.
2. `RoutedDocumentEndpointDispatcher.TryWriteDescriptor`: retry a sharing violation briefly (for
   example, 5 attempts over 250 ms) before treating the write as failed and stopping the listener.

## Affected files

- `src/MCP_Rhino.Router/Routing/BackendCatalog.cs`, `BackendConnection.cs`, `RouterRuntime.cs`,
  `RouterCatalogMonitor.cs`, `RouterErrors.cs`
- `src/MCP_Rhino.Transport/Routing/RouteRegistry.cs` (Transport ships in both the Router and the
  plug-in package)
- `src/MCP_Rhino.Server/Infrastructure/Plugin/ServerBootstrap.cs`,
  `Infrastructure/Plugin/Routing/RoutedDocumentEndpointDispatcher.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`: exclude the new smoke folder from the Server's
  `Project_Test/**/*.cs` compile. `MCP_Rhino.sln`: add the smoke project.
- `Project_Test/261022_TEST_router-route-capacity/` (new): `RhinoShim/` and `RouteCapacitySmoke/`,
  built from the reproduction harness.
- `Project_Archive/Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/Program.cs`:
  update the assertion "Multiple Router clients coexist" if on-demand connections change when
  clients overlap.
- `Project_Guides/MCP_Rhino Architecture.md`: Router-only transport contract (connection policy,
  route capacity, `DOCUMENT_ENDPOINT_BUSY`).
- `Runtime_Workflow/MCP_Rhino Workflow.md` and `README.md`: how agents handle
  `DOCUMENT_ENDPOINT_BUSY`.
- Packaging version bump. Coordinate with the staged, not yet installed 1.3.8: either rebase on it
  and ship 1.3.9, or fold both into one release, as the user prefers.

## Tests

`RouteCapacitySmoke` reuses the reproduction approach: production `McpNamedPipeServer`, real
Router types, and a private registry. It must assert:

1. **Capacity.** 12 Routers list tools and idle with monitors running. They hold 0 route
   connections after settling. A 13th Router selects the document on its first attempt. Server
   active connections equal the number of Routers that have work connections.
2. **Busy error.** Fill a route with 8 raw client holders.
   - `select_document` returns retryable `DOCUMENT_ENDPOINT_BUSY` in ≤ 3.5 s, and
     `list_documents` shows `ENDPOINT_BUSY`.
   - A missing pipe returns `DOCUMENT_ENDPOINT_UNAVAILABLE` with `ENDPOINT_NOT_LISTENING`.
   - The existence check leaves the server's accepted count unchanged.
3. **Save.** 20 same-path descriptor rewrites cause 0 reconnects. The per-connection id is
   unchanged, and a 4 s mutation in flight completes OK.
4. **Real generation change.** For a path change and a listener restart (new nonce), the Router
   re-attests. A call in flight on the retired connection completes before that connection is
   disposed, and new calls use the new connection.
5. **No leak.** Cap 8, 12 Routers with concurrent monitors and calls, 20 Saves. At every
   checkpoint, server active equals connections owned by Routers; 0 after all Routers are disposed.
6. **Disconnect classification.** A backend disconnect during a mutation returns
   `MUTATION_OUTCOME_UNKNOWN`, during a read returns retryable `DOCUMENT_ENDPOINT_UNAVAILABLE`, and
   real caller cancellation still cancels.
7. **Surface change.** A new plug-in instance key causes one probe and one `tools/list_changed`,
   and leaves no connection held.
8. **Registry sharing.** A reader looping `ReadAll` while the writer replaces the descriptor 1,000
   times causes 0 write failures.

Keep `RouterProtocolSmoke` passing, updated only where the connection policy changes the
observation.

## Validation (Architecture guide)

- `dotnet build .\MCP_Rhino.sln -c Debug` and `dotnet build .\MCP_Rhino.sln -c Release`.
- `RouterProtocolSmoke` and `RouteCapacitySmoke`, each in Debug and Release.
- `mcp-tool-safety-annotations-smoke-test` in Debug and Release. The tool surface should not
  change; run it anyway as a regression gate, because the Router and plug-in host change.
- Packaging (Stage 2): RHP assembly GUID and identity checks, then the installer's
  `-Mode Validate`. Installation stays a separate user-approved step under the production registry
  attestation memo.
- Live check (Release, with user permission, on a disposable saved `.3dm` under
  `Runtime_Test/local/` in a Rhino the user agrees to start; never the user's files):
  - start 12 real Router processes over stdio that list tools and idle;
  - a 13th selects first try;
  - Save 10 times and confirm the plug-in logs no new "pipe client connected" lines;
  - record Rhino private bytes for the Stage 2 cap decision.

## Acceptance criteria

- With any number of long-lived, idle Routers, a fresh Router selects an open saved document on its
  first attempt (harness and live).
- Idle Routers hold no connection to documents they have not used.
- A plain Save causes no reconnects, and keeps per-connection script and Grasshopper state and
  calls in flight.
- No orphaned connections under contention.
- A full route returns retryable `DOCUMENT_ENDPOINT_BUSY`, distinct from a missing listener and
  from an attestation failure.
- A disconnect during a mutation always returns `MUTATION_OUTCOME_UNKNOWN`.
- Debug and Release builds and all listed smokes pass.

## Rollout and risks

- Old Routers keep the old behavior until their sessions restart. Stage 2's higher cap covers that
  period. After installing, the user can end idle sessions to free slots at once.
- On-demand connects add about tens of ms to the first call that needs a document.
- `list_documents` probes take a slot only for milliseconds, but a full route still reports busy.
- A Router keeps its work connections until its session ends, so a session that used a document
  long ago still holds one slot. With a cap of 32 this is bounded. Releasing work connections after
  long idle is deferred, see below.
- If a plug-in instance ever served different surfaces for different documents, the surface cache
  would be wrong. Per-connection fingerprint checks catch this, and the Router then refreshes the
  cache.

Operational mitigation before any fix, the user's choice: end agent sessions that do not need
Rhino. Or scope the `mcp-rhino` MCP server entry to the projects that use it, so unrelated
sessions (for example other clients, or projects that never touch Rhino) do not start Routers.

## Rollback

- Stage 1 is Router and Transport only: reinstall the previous package, or point clients at the
  previous Router.
- Stage 2 is plug-in only: reinstall the previous package and restart Rhino.
- No document, registry-schema or protocol-version change is involved.

## Future extensions

- Release work connections after long idle. This needs a private backend query (like the
  attestation method) that reports retained connection-bound state, such as script submissions and
  executions or Grasshopper definition ids. Use it rather than a hand-maintained list of tool
  names.

## Related evidence

- Reproduction source and logs (outside the repo):
  `%APPDATA%\Claude\side-session-notes\local_c4f6cb6a-f647-46bf-ad03-ea8ade601478\router-route-capacity\`
  (`RouteCapacityRepro`, `RouteChurnRepro`, `RhinoShim`, `run1.log`, `run1b.log`, `run2.log`).
- Existing protocol smoke:
  `Project_Archive/Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke`. Its
  fake endpoint allows 16 instances with no slot semaphore, so it never exercised the cap.
