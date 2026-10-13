# Live custom Rhino scripts

## Background

The user explicitly requested custom script execution through the existing MCP document routing on 2026-10-12. Atomic tools can already reach MET - Wireframe, but cannot execute an ad hoc block-coordinate rebase. This work adds a reusable execution primitive, not a stone-specific tool or another transport.

## Goals

Compile and execute trusted synchronous C# against the exact open, saved Rhino document selected by Router. No COM, StartScriptServer, UI automation, foreground activation, offline fallback, or per-task plug-in construction. Return structured results and logs, keep ordinary document edits undoable, and report long-running execution without replay.

## Architecture ownership

- `Tools/Scripting`: thin preview, apply, status, cancellation wrappers with explicit safety annotations.
- `Application/Interfaces` and `Application/Services`: live execution boundary and input validation.
- `Contracts`: requests, compilation preview, execution status.
- `Infrastructure/Rhino/Live/Scripting`: trusted compiler, globals, bounded session job store and execution against `ILiveRhinoDocumentAccessor`.
- Existing assembly-scanned registration and Router targeting remain authoritative.

## Design

1. `preview_rhino_script` validates JSON parameters, compiles inline C# without executing it, resolves the routed document, and returns an opaque expiring token, code hash, document identity and diagnostics. This is a compilation preview, not a simulation of geometry effects. No `#r`, `#load`, async or await; built-in references/imports cover RhinoCommon and standard .NET. Scripts receive `doc`, `Parameters`, `Result`, `Log`, `CancellationToken`, and `Checkpoint`.
2. `apply_rhino_script` starts that exact compiled submission once and immediately returns an execution id. Duplicate apply returns the same execution instead of rerunning it. Execution happens synchronously on Rhino's main thread via the bound accessor; it never resolves ActiveDoc. Target path/runtime serial are rechecked immediately before execution.
3. `get_rhino_script_status` reads queued/running/completed state, bounded logs/results, elapsed time, exceptions and rollback outcome without waiting for the Rhino UI thread. `cancel_rhino_script` cancels queued work or requests cooperative cancellation of running work. Time limits are cooperative: trusted code that does not cooperate cannot safely be force-stopped in Rhino.
4. Refuse to start inside another undo record or with Undo disabled. Own one Undo record; on exceptions/cancellation roll back ordinary target-document edits only when an owned record exists, never undo preceding user work. External side effects and deliberate misuse of full-trust APIs cannot be undone or sandboxed. Apply is ReadOnly=false, Destructive=true, OpenWorld=true regardless of the supplied script.
5. Keep tokens/jobs bounded and scoped to a backend MCP connection; disconnect cancels pending work/cooperatively requests cancellation, and does not replay it. Completed results retained for a bounded time. Preview is read-only, not script execution. Execution must use short finite synchronous operations and no background Rhino access, document switching, save/close or manual undo manipulation.
6. C# is the initial supported language. Python engines, arbitrary assembly/path loading and hard in-process preemption are out of scope.

## Files

New scripting contracts/service/adapter/compiler/globals/tools; DI registrations; exclusive CLI and Rhino development smoke; safety expectations; architecture/runtime/README documentation; package version 1.3.7; this PLAN plus matching TEST and EXET.

## Usage

Select an open saved document using Router. Preview `Result = new { doc.Name, Count = doc.Objects.Count };`, then apply the token and poll its execution id. Modeling scripts use `doc.Objects`, `doc.InstanceDefinitions`, RhinoCommon geometry APIs and caller-supplied JSON parameters. A preview validates code and target only; scripts must validate task-specific assumptions before modifying geometry.

## Acceptance

Debug and Release solution/plugin builds and safety/inventory gates. Contract tests for input limits, compilation errors and directives/async rejection. Native synthetic tests for explicit document routing independent of active document, compile-only preview, one execution on retry, changed/closed target rejection, queued/running cancellation, cooperative deadline, successful Undo, failure rollback without undoing previous user work, bounded logs/results and no offline access. Package GUID/manifest/dependency isolation and installation validation. Live installed verification must report exact package/tool surface and document identities; never count staging as installation.

## Risks and rollback

Full-trust code can affect the process and external state; this is not a sandbox. Cancellation cannot interrupt arbitrary native/managed calls. Script compiler dependencies must load in the existing isolated plug-in context while sharing RhinoCommon. Preserve existing user changes. Build/stage while Rhino is open; use the already validated independent host installation route only after file locks permit activation. User launches Rhino. Prior reachable RHP and registration remain recoverable; required independent host and post-start attestation gates remain in force.

## Future extensions

Additional script languages only if specifically needed; a reusable script library or fixed workflow can be added after repeated usage demonstrates a need.
