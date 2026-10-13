# Live custom scripts regression

Run from the repository root on Windows with .NET 8 and licensed Rhino 8 installed:

```powershell
& .\Project_Test\261012_TEST_live-custom-scripts\Run-Validation.ps1
& .\Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -OutputRoot .\.validation\live-custom-scripts\package
dotnet run --project Project_Test\261012_TEST_live-custom-scripts\NativeChecks.csproj -c Release -- .validation\live-custom-scripts\package\MCP_Rhino-1.3.7
```

The validation script builds the solution and CLI in Debug and Release, checks the unique
`live-custom-scripts-smoke-test` slug, all 176 safety annotations and inventory, runs 30 native
assertions, and runs Router protocol regression. The dedicated development Rhino command is
`_McpLiveCustomScriptsSmoke`; production packages exclude development commands. Its in-command
branch compiles only, because execution intentionally refuses to borrow another command's Undo.

`NativeChecks.csx` runs a no-window Rhino.Inside fixture in its own process. It creates two empty
documents under `Runtime_Test/local/live-custom-scripts/<unique-id>/`, never opens user models,
and pumps queued work onto the owning thread. The target document finishes with exactly one
pre-existing user point; the unrelated document remains empty. Assertions cover compile-only
preview, identity/path checks, one execution on retry, success Undo, rollback before/after a
mutation, log/result bounds, deadlines, throwing cancellation callbacks, responsive status and
cancellation, connection scoping/disposal, expiry, closed targets, and Undo prerequisites.

With a bundle path argument, the native harness instead uses the actual production
`PluginLoadContext`, packaged runtime/compiler/globals and isolated JSON dependency. It compiles
and executes a read-only script against the synthetic RhinoDoc and checks target serial and
JSON parameters. This specifically guards against non-collectible Roslyn submissions referencing
a collectible plug-in assembly.

`Read-HostRegistration.ps1` is a read-only observer for an independently launched ordinary host
process. `Install-InHostPowerShell.ps1` verifies bundle hashes, backs up the prior reachable RHP
and registration, installs only after Rhino/Router are closed, performs separate-process Validate
and timestamp attestation, and supports `AfterRestart` / `Restore`. `Invoke-HostActivationStep.ps1`
records host activation or post-restart verification. Evidence is local under
`.validation/live-custom-scripts/`; these helpers do not launch Rhino.

Post-install acceptance additionally requires the exact RHP loaded in user-launched Rhino,
correct registry ownership timestamps, and the installed Router exposing and executing all four
script tools against the selected live document. Staging alone does not satisfy that gate.
