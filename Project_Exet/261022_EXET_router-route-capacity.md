# Router Route Capacity Execution Record

Record level: full
Execution date: 2026-10-22
Status: code complete and validated against the harness; live Rhino check, Stage 2 cap measurement,
packaging and installation not done.

Plan: [261022_PLAN_router-route-capacity](../Project_Plan/261022_PLAN_router-route-capacity.md)
Tests: [261022_TEST_router-route-capacity](../Project_Test/261022_TEST_router-route-capacity/)

## Scope delivered

Stage 1 (Router and Transport):

- `BackendCatalog` rewritten.
  - Work connections are cached per document session, single-flight: the connect starts only after
    its entry wins the cache slot, under one lock, so no factory runs twice.
  - Failed connects use compare-and-remove, so a failure can't evict a newer entry.
  - The generation key is plug-in instance, lifecycle generation, endpoint nonce and normalized
    path. `LastUpdatedUtc` is gone from the key.
  - Retired entries take no new calls and are disposed when their per-call lease count reaches
    zero.
  - A transiently unreadable descriptor (`READ_FAILED`, `INVALID_DESCRIPTOR`,
    `OWNER_PROCESS_UNVERIFIED`) does not retire a working connection.
- Two connection kinds (`BackendAccess.Work` / `Probe`).
  - Probes are attestation-only and close at once, unless a work connection already exists.
  - Surface discovery uses one probe per plug-in instance (`PluginInstanceGeneration` +
    `PluginVersion`), or reuses a work connection's surface.
  - After a failed surface probe, the monitor waits a 5 s backoff before probing again. Explicit
    requests retry at once.
- `RouterRuntime`:
  - `select_document`, `get_selected_document` and proxied calls open work connections.
  - Each work connection must match the canonical fingerprint. On a mismatch, the instance's
    cached surface is updated and `TOOL_SURFACE_MISMATCH` is returned.
  - `list_documents` attests each canonical route with a probe and reports per-document
    diagnostics.
  - Resource reads use a probe, preferring the selected document.
  - `DetectToolSurfaceChangeAsync` is split out of `CheckForToolSurfaceChangeAsync`, so the
    monitor logic can be tested without a frontend.
- `BackendConnection`: a connect timeout is classified by listing `\\.\pipe\`, which opens no
  instance.
  - Pipe exists: `ENDPOINT_BUSY`.
  - Pipe missing: `ENDPOINT_NOT_LISTENING`.
  - Attestation failure: `ROUTE_ATTESTATION_MISMATCH`, a typed `BackendConnectException`.
- `RouterErrors`:
  - New `DOCUMENT_ENDPOINT_BUSY` (retryable).
  - An optional `diagnostic` field is added. Existing fields are unchanged, and `diagnostic` is
    omitted when null.
- Disconnect classification: any non-caller exception from a proxied call invalidates the work
  connection.
  - A JSON-RPC protocol error is the exception: it doesn't invalidate the connection.
  - Non-read-only tools return `MUTATION_OUTCOME_UNKNOWN`; reads return retryable
    `DOCUMENT_ENDPOINT_UNAVAILABLE`.
  - Real caller cancellation still propagates.
- `RouteRegistry`:
  - Readers open with `FileShare.ReadWrite | FileShare.Delete`.
  - `Write` replaces the descriptor with a POSIX-semantics rename (new `PosixFileReplace`,
    `SetFileInformationByHandle(FileRenameInfoEx, REPLACE_IF_EXISTS | POSIX_SEMANTICS)`, Windows 10
    1607+). This replaces the file even while delete-sharing readers hold it open.
  - It falls back to `File.Move(overwrite)` where the platform or file system doesn't support
    POSIX rename.
  - Either way it retries up to 20 times, 25 ms apart, for readers that don't share delete access,
    such as older Routers.

Stage 2 (plug-in): `ServerBootstrap.RouteMaxConcurrentConnections = 32` replaces the literal 8.

## Deviations from the plan

- **The descriptor write fix moved into `RouteRegistry.Write` (Transport) instead of a retry in
  `RoutedDocumentEndpointDispatcher`, and it became a POSIX rename.**
  - The harness showed that delete-sharing readers are not enough. Windows still fails
    `File.Move(overwrite)` with `UnauthorizedAccessException` while any Router has the
    destination open.
  - With 12 Routers making continuous calls, the file was open often enough that a 10 × 25 ms
    retry was used up in one Debug run. That is a realistic load, not just the plan's
    3,400 calls/s loop.
  - In the plug-in, a failed write stops the document's route, so a retry alone is not enough.
  - The shared writer now owns the fix. The plug-in gets it because Transport ships in the plug-in
    package, and the dispatcher stays unchanged.
- **Stage 2 cap set to 32 without the planned Rhino private-bytes measurement.** That needs a live
  Rhino session and was not run. It must run before packaging. If one idle connection costs more
  than 20 MB, change the constant to 16 and record why here.
- **No packaging version bump.** `package-manifest.json` stays 1.3.7.
  - The staged 1.3.8 worker license fix (branch `claude/suspicious-meninsky-72109b`) is not on the
    remote or in this checkout.
  - The user still has to decide between rebasing onto 1.3.8 and shipping 1.3.9, or one combined
    release.
- **`RouteCapacitySmoke` runs with a route cap of 8, not the production 32.** It constructs
  `McpNamedPipeServer` directly; `ServerBootstrap` can't be compiled outside Rhino. A cap of 8 is
  the harder case.
- **`RouterProtocolSmoke` needed only the snapshot rename.** It changed from `CanonicalBackend` to
  `CanonicalSurface`. The "Multiple Router clients coexist" assertion still holds unchanged.

## Validation

All commands ran from the repository root on 2026-10-22.

| Check | Result |
| --- | --- |
| `dotnet build .\MCP_Rhino.sln -c Debug` | passed, 0 warnings, 0 errors |
| `dotnet build .\MCP_Rhino.sln -c Release` | passed, 0 warnings, 0 errors |
| `dotnet run --project Project_Test\261022_TEST_router-route-capacity\RouteCapacitySmoke -c Debug` | passed, all assertions |
| Same, `-c Release` | passed, all assertions |
| `RouterProtocolSmoke` Debug and Release | passed, all 20 assertions each |
| CLI host builds (`-p:McpRhinoCliHost=true -o .validation\router-route-capacity\cli-<cfg>`), `mcp-tool-safety-annotations-smoke-test` | Debug and Release passed: 176 tools, no bare attributes. The tool surface is unchanged. |

`RouteCapacitySmoke` assertions mapped to the plan's tests:

1. **Capacity.**
   - 12 Routers list tools, then idle with monitors running: 0 server connections.
   - A 13th Router selects the document on its first attempt (35 ms in Debug).
   - Server active connections equal the work connections the Routers own.
2. **Busy.**
   - Route filled with raw holders: `DOCUMENT_ENDPOINT_BUSY`, retryable, diagnostic `ENDPOINT_BUSY`, in 3,029 ms.
   - `list_documents` shows `ENDPOINT_BUSY`.
   - A missing pipe gives `DOCUMENT_ENDPOINT_UNAVAILABLE` with `ENDPOINT_NOT_LISTENING`.
   - The server's accepted count is unchanged by the existence check.
   - Selection succeeds once a slot frees.
3. **Save.**
   - 20 same-path rewrites: 0 reconnects and no new "pipe client connected" log lines.
   - The per-connection id is unchanged.
   - A 4 s mutation in flight completes OK on the same connection.
4. **Generation change.**
   - After Save As, new calls use a new attested connection.
   - The retired connection stays open (server active 2) until its in-flight call completes, then
     is disposed.
   - After a listener restart (new nonce), the Router re-attests.
5. **No leak.**
   - With 12 contending Routers, exactly 8 get work connections.
   - Three concurrent first calls on each Router share one connection; the others get `DOCUMENT_ENDPOINT_BUSY`.
   - Four load rounds with 20 Saves. At every checkpoint, server active (8) equals the connections
     Routers own (8).
   - 0 connections remain after all Routers are disposed.
6. **Disconnect.** A listener restart during:
   - a mutation returns `MUTATION_OUTCOME_UNKNOWN` with `outcomeUnknown`;
   - a read returns retryable `DOCUMENT_ENDPOINT_UNAVAILABLE`.

   Real caller cancellation throws `OperationCanceledException`, and the Router recovers.
7. **Surface change.**
   - A new plug-in instance gets exactly one probe and exactly one change signal.
   - No connection stays held, and the newer surface is published.
8. **Registry sharing.**
   - 1,000 replaces under a looping reader: 0 write failures.
   - A replace succeeds while a delete-sharing reader holds the descriptor open, and that reader
     keeps the old content. A retry alone can't pass this.

The Debug smoke was then repeated three times. Each run passed all 46 assertions with no wait
timeouts, in about 38 s.

Limits:

- The harness compiles the production `McpNamedPipeServer.cs` unchanged against a `RhinoApp`
  shim. The per-connection host is fake, and real Rhino/RhinoCommon was not exercised.
- The smoke was not rerun against the old Router, because the catalog API changed. The plan's
  reproduction runs (in the session-notes folder it names) recorded the old failures.

## Acceptance

| Criterion | Outcome |
| --- | --- |
| Fresh Router selects on first attempt with many idle Routers | Harness: met. Live: not run. |
| Idle Routers hold no connection to unused documents | Met in the harness. |
| Save causes no reconnects and keeps per-connection state and in-flight calls | Met in the harness. |
| No orphaned connections under contention | Met in the harness. |
| Distinct retryable `DOCUMENT_ENDPOINT_BUSY` | Met in the harness. |
| Disconnect during a mutation returns `MUTATION_OUTCOME_UNKNOWN` | Met in the harness. |
| Debug and Release builds and all listed smokes pass | Met. |

## Remaining work

1. Live check (Release, user permission, a disposable saved `.3dm` under `Runtime_Test/local/`):
   - run 12 real Router processes, then confirm a 13th selects on its first try;
   - Save 10 times and confirm no new plug-in "pipe client connected" lines;
   - record Rhino private bytes with 0, 8 and 32 idle connections to confirm the cap.
2. Packaging decision with the 1.3.8 license fix, then the version bump. After that come RHP
   assembly GUID and identity checks and installer `-Mode Validate`.
3. Installation is a separate user-approved step under the production registry attestation memo.
4. Old long-lived Routers keep the old behavior until their sessions restart.

## Rollback

- Stage 1 (Router and Transport): reinstall the previous package, or point clients at the previous
  `MCP_Rhino.Router.exe`. The Transport retry and sharing change is compatible with old Routers and
  old plug-ins.
- Stage 2: reinstall the previous package and restart Rhino.
- No registry schema, route protocol version or tool surface change is involved.
