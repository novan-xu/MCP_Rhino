# Live custom Rhino scripts

## Corresponding plan

[261012_PLAN_live-custom-scripts.md](../Project_Plan/261012_PLAN_live-custom-scripts.md).
Execution date: 2026-10-12. The user explicitly requested construction of custom execution in
any document accessible through the existing MCP Router. C# is the initial supported language.

## Related artifacts

[TEST folder](../Project_Test/261012_TEST_live-custom-scripts/README.md), including contract
smoke, native fixtures, package-context check and host attestation helpers. No commit or PR was
requested. Existing unrelated workspace edits have been preserved.

Local evidence: `.validation/live-custom-scripts/validation.log`, packaged version 1.3.7 under
`.validation/live-custom-scripts/package/MCP_Rhino-1.3.7/`, and independent host evidence in
`.validation/live-custom-scripts/`. Fixtures use anonymous paths under
`Runtime_Test/local/live-custom-scripts/<unique-id>/a.3dm` and `b.3dm`.

## Result and actual scope

Added the `Tools/Scripting` family: `preview_rhino_script`, `apply_rhino_script`,
`get_rhino_script_status`, and `cancel_rhino_script`. Thin wrappers delegate to validated
application contracts and the live scripting adapter/compiler. Existing assembly scanning,
per-connection DI, bound document accessor and installed Router remain authoritative.

Compilation previews return a target serial/path, SHA256, warnings and ten-minute token without
running user code. Apply queues the exact compiled submission once and returns an execution id;
repeated apply of that token returns the same job. Execution rechecks the target and owns its
Undo boundary on Rhino's main thread. Failure or cooperative cancellation rolls back ordinary
document edits when ownership is established. Status and cancellation do not wait for that thread.

Globals expose `doc`, JSON `Parameters`, `Result`, `Log`, `CancellationToken` and `Checkpoint`.
Inline synchronous C# includes RhinoCommon and standard imports. Explicit limits cover input,
logs, JSON result, retained jobs and token lifetime. Scripts are full trust; there is no sandbox,
hard in-process cancellation or rollback of external effects. Closing the backend connection
requests cancellation without replay; job ids are scoped to that connection.

README, architecture and runtime routing instructions describe the new capability. MCP_Rhino
package version is 1.3.7. PanelCladdingEditor is unchanged.

## Deviations from the plan

No scope change. The packaged compiler requires emitting the C# submission into the existing
collectible plug-in load context instead of using Roslyn's default submission loader. The native
test harness includes a separate packaged-context mode to validate this requirement.

## Issues discovered and fixed

- Removing all metadata resolution also prevented standard framework references from resolving.
  Keep framework resolution and reject `#r`/`#load` directives explicitly before compilation.
- A custom Undo marker establishes ownership even when a script throws before its first edit.
  Native Rhino can start an inverse Undo record while replaying that marker. Close only the new
  serial created by that specific Undo call, preventing a leftover active record without touching
  preceding user work.
- Cancellation callbacks can throw. A guarded deadline callback catches aggregate exceptions,
  avoiding an unhandled timer-thread exception that could terminate Rhino.
- Roslyn's non-collectible submission loader cannot reference the collectible packaged runtime.
  Emit/load in the same runtime context, preserving RhinoCommon and isolated JSON type identity.
- UI dispatch waits indefinitely internally while apply promptly returns a job id. A finite
  dispatch timeout must not report failure and then allow a queued mutation to execute later.

## Test record

`Project_Test/261012_TEST_live-custom-scripts/Run-Validation.ps1` completed with exit code 0:

- Debug and Release full solution builds: zero warnings and zero errors.
- Debug and Release CLI builds and unique `live-custom-scripts-smoke-test`: passed.
- Safety and inventory smoke in both configurations: 176 tools, all explicit annotations,
  four new scripting tools, no duplicate wrappers.
- Native Rhino.Inside regression: 30 assertions passed. Compile-only preview, exact target,
  wrong-target rejection, one execution on retries, success Undo, early/late failure rollback,
  bounded output, running/queued cancellation, deadline, throwing callbacks, connection
  scoping/disposal, expiry, changed/closed target, disabled/active Undo, and unrelated document.
  Final target fixture contains the one preceding user point; unrelated fixture contains zero.
- Router protocol smoke: passed, including independent selections, conflict rejection without
  a backend call, duplicate-path disambiguation, private attestation and concurrent clients.

`Build-McpRhinoPackage.ps1 -OutputRoot .validation/live-custom-scripts/package` completed with
exit code 0. The builder directly validates compiled assembly GUID, production command exclusions
and bundle hashes. Roslyn scripting dependencies are present in the Plugin payload.

`dotnet run --project Project_Test/261012_TEST_live-custom-scripts/NativeChecks.csproj -c Release
-- .validation/live-custom-scripts/package/MCP_Rhino-1.3.7` completed with exit code 0. The actual
packaged runtime compiled and executed a synthetic read-only script with shared RhinoCommon and
isolated System.Text.Json 10.0.0.0; serial identity and JSON parameter value matched.

## Acceptance alignment

Construction, safety annotations, Debug/Release validation, real native mutation/Undo tests,
Router regression and packaged dependency isolation pass. Host installation and user-launched
Rhino verification are recorded below as they complete; neither staging nor agent-context
registry readback alone is treated as installed runtime acceptance.

## Rollback verification

Native tests demonstrate a failed script restores its own edit and leaves preceding user work
intact, including failure before the first edit. The installation helper preserves the previous
RHP directory and registry export before activation, validates independent host timestamps, and
restores the prior reachable RHP on failure. An installation-failure rollback has not been induced
for this version. External side effects from arbitrary trusted code remain outside Undo.

## Remaining items

Post-install exact RHP load/timestamp verification and live installed Router script execution
remain pending until Rhino is opened by the user. The stone assembly origin/CPlane operation
has not been performed as part of construction.

## Conclusion

The new capability is implemented, packaged and validated in native synthetic documents.
Deployment completion will be appended after independent host and live verification.

## Host activation — 2026-10-12

An independently launched Windows Task Scheduler process under the Rhino user's interactive
account confirmed version 1.3.6's prior reachable registration. Rhino and Router were already
closed; no user application was launched or closed by this work. A temporary limited host task
then installed 1.3.7, backed up the prior plug-in/registration, ran installer `-Mode Validate`
and launched a separate attestation process. All completed with exit code 0. The exact production
CommandList is empty, `PlugIn\\FileName` names the existing 1.3.7 RHP, and all three registration
timestamps advanced. The temporary tasks were removed. No password or administrator session
was required.

Evidence: `host-preflight.json`, `host-install-task.log` and `host-<user-sid>/before.json`,
`after.json`, `installer.log`, `attestation.log` under `.validation/live-custom-scripts/`.
Host installation is passed; exact loaded-RHP and live Router checks still await user launch.

## Live acceptance — 2026-10-12

After the user reopened Rhino, independent host verification passed with exit code 0. Rhino
process 53372 loaded the exact installed 1.3.7 RHP. Root and CommandList timestamps advanced at
startup while installer-owned PlugIn time remained unchanged. The temporary verification task
was removed. Evidence: `host-post-restart-task.log` and `host-<user-sid>/post-restart.json`.

The installed Router exposed all four new scripting tools. A read-only C# probe compiled without
diagnostics and executed against the selected MET Wireframe document, runtime serial 268435457.
It returned the exact selected path, 31,052 objects, 420 definitions, the supplied JSON parameter,
a log message, and `IsOnRhinoThread=true`. Status was Succeeded. Repeating apply returned the same
execution id; cancellation after completion left it Succeeded. Evidence is
`.validation/live-custom-scripts/installed-live-probe.json`. Geometry was not edited by this probe.

Construction, installation and live acceptance are complete. The earlier pending deployment
items are resolved. The requested stone-origin operation continues as a runtime task using this
capability, not as additional capability construction.
