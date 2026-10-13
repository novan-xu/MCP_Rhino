# 261010_EXET_offline-copy-edit

## Corresponding plan
- [261010_PLAN_offline-copy-edit.md](../Project_Plan/261010_PLAN_offline-copy-edit.md)
- Execution date: 2026-10-10 (America/New_York).
- User authorized restoring offline editing, required a clearly suffixed duplicate for every offline change, then specified a script-based interface and left language selection to the implementation.

## Related artifacts
- [TEST folder](../Project_Test/261010_TEST_offline-copy-edit/)
- [Validation runner](../Project_Test/261010_TEST_offline-copy-edit/Run-Validation.ps1)
- [Standalone integration/safety test](../Project_Test/261010_TEST_offline-copy-edit/Program.cs)
- Staged bundle: `Project_Test/261010_TEST_offline-copy-edit/artifacts/MCP_Rhino-1.3.3/` (ignored generated output).
- Generated fixtures: `Runtime_Test/local/offline-copy-edit/<run-id>/`; no user model used.
- No commit, push, or PR requested or created. Pre-existing uncommitted construction work was retained.

## Implemented scope
- New layered `MCP_Rhino.Offline` library with Rhino3dm 8.32.0 and Jint 4.17.0, referenced by Router and standalone tests, never by the RHP.
- Three MCP tools generated from C# metadata: `rhino_offline_inspect_document`, `rhino_offline_preview_script`, `rhino_offline_apply_script`. They remain available with zero live Rhino endpoints. No alternative transport or live-to-offline fallback.
- Restricted JavaScript scripts operate through a JSON-only in-memory model bridge: paged inspection, object attributes/user text, document user text, layer creation/editing, affine transforms, exact-id deletion, point/line/polyline/sphere/box creation, and logs. Scripts can combine operations using loops, conditions, and functions.
- No raw Rhino3dm/CLR objects, reflection, modules, eval, filesystem, network, or process access is projected into scripts. Script execution and model calls are bounded. This is a constrained modeling interpreter, not a general Python/C# executor or an OS security boundary.
- Preview retains the exact serialized, reread-verified result for ten minutes in the same Router process. Apply checks source path/SHA-256 and writes those bytes once without rerunning the script. This also preserves nondeterministic script results exactly.
- Source access uses `FileMode.Open`, `FileAccess.Read`, `FileShare.Read`. The read handle remains open through apply. The only disk write path creates an owned staging file with `CreateNew`, flushes, and uses a non-replacing move to a generated sibling `<stem>_offline-edit_<UTC timestamp>_<unique id>.3dm`. No overwrite/output-path parameter. Previous generated suffixes are normalized to avoid name growth; every result still has a fresh unique suffix.
- Router rejects known open source paths using discovery without waiting for live backend handshakes. Live selection/conflict/ambiguity behavior remains unchanged.
- Architecture, runtime workflow, README, safety source audit, Router regression and package dependency gates updated. Router runtime validation now exercises an in-memory script and file serialization.

## Deviations from plan
- The user changed the interface to script-based before implementation; the plan has a dated revision. JavaScript in the existing .NET process was chosen to enforce a narrow host surface without requiring Python, Docker or Rhino to run. No per-operation editing MCP endpoints were added.
- Preview uses opaque one-use tokens bound to cached output bytes, source path and hash rather than exposing a deterministic script fingerprint. This prevents apply from rerunning a script with different random/time-dependent results.
- The offline suite is a standalone console/MCP integration project rather than a Rhino live smoke command, since the new library must not be loaded in Rhino. The existing Server safety smoke also statically checks offline annotations; standalone tests inspect runtime annotations.
- The existing untracked `261009_TEST_grasshopper-stone-bake` standalone project had generated `obj/**/*.cs` files caught by the Server's broad historical source glob, causing 57 solution build errors. Added an exact folder exclusion alongside the new standalone test exclusion. That independent project's `.csx` sources and project were left unchanged; no Server smoke registration was removed.

## Problems found and fixed
- Rhino3dm's object table exposes an IList shape whose LINQ indexed paging yielded null entries. Offline paging now uses the native enumerator; unnamed objects normalize to an empty string. Smoke tests cover both.
- Used the current `AllLayers`, `AllMaterials`, and `AllInstanceDefinitions` tables instead of obsolete collections. Object transforms duplicate geometry and attributes while preserving UUIDs.
- Any failed model operation marks the entire preview invalid, including when JavaScript catches the error. No partially edited result can be published.
- Kept the Rhino3dm native dependency exclusively in Router output and added package gates to detect missing managed/native files.

## Test records
`Project_Test/261010_TEST_offline-copy-edit/Run-Validation.ps1` completed with exit code 0. It ran:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
dotnet run --project Project_Test/261010_TEST_offline-copy-edit/OfflineCopyEditSmoke.csproj -c Debug --no-build
dotnet run --project Project_Test/261010_TEST_offline-copy-edit/OfflineCopyEditSmoke.csproj -c Release --no-build
dotnet run --project Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/RouterProtocolSmoke.csproj -c Debug --no-build
dotnet run --project Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/RouterProtocolSmoke.csproj -c Release --no-build
```

- Both solution builds: 0 warnings, 0 errors, including production Debug/Release RHPs.
- Server CLI hosts built separately in each configuration with `-p:McpRhinoCliHost=true`; `mcp-tool-safety-annotations-smoke-test` passed for 171 existing Server tools and offline source annotations.
- Standalone offline safety checks passed for all three offline tools: inspect/preview read-only and closed-world; apply non-read-only, non-destructive to existing files and open-world.
- Real Rhino3dm fixture: two source objects, six objects in the edited duplicate; names, user text, document metadata, layer edits, primitive creation, deletion, transformed coordinates, and object UUID preservation checked after reread. Source SHA-256 and last-write time unchanged.
- Separate block/material fixture: transformed instance preserved UUID and definition link; definition geometry and material survived, source hash unchanged; attempts to edit definition geometry rejected.
- Repeated editing of a copy and four concurrent applies created distinct outputs, preserving earlier files. Nondeterministic script output matched its preview exactly.
- Missing/consumed/stale/mistargeted/expired previews, full cache, locked source, canceled preview/apply, invalid IDs/numbers/attributes and caught edit failures rejected. Read-only source editing succeeded through a new copy.
- Direct file/CLR/process/module/eval access attempts and infinite loop rejected; failed scripts wrote no outputs and preserved the source.
- Empty-catalog Router exposed six tools (three controls + three offline tools). Fake live endpoints confirmed known-open rejection, independent selection, path conflict/ambiguity, attestation, pagination, and concurrent live routing. Live failures did not execute offline edits.
- Real stdio MCP client listed offline tools, previewed a script and applied it to a duplicate.

Release staging and runtime validation also returned exit code 0:

```powershell
Packaging/MCP_Rhino/Build-McpRhinoPackage.ps1 -OutputRoot Project_Test/261010_TEST_offline-copy-edit/artifacts
Project_Test/261010_TEST_offline-copy-edit/artifacts/MCP_Rhino-1.3.3/bin/MCP_Rhino.Router.exe --validate-install
dotnet run --project Project_Test/260805_TEST_rhino-plugin-assembly-identity/PluginAssemblyIdentityProbe/PluginAssemblyIdentityProbe.csproj -c Release --no-build -- Project_Test/261010_TEST_offline-copy-edit/artifacts/MCP_Rhino-1.3.3/Plugin/MCP_Rhino.Server.rhp
```

- PE metadata probe: `assembly=MCP_Rhino.Server`, `pluginId=7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`, `declared=True`, matching assembly source, plug-in declaration and package manifest.
- Published Router's in-memory script/serialization validation passed. Rhino3dm was absent from the packaged `Plugin` directory.
- `git diff --check` passed.

## Acceptance alignment
Script-based offline editing is reachable through the supported Router without Rhino. Source/prior-output preservation is enforced by the only disk writer, not by script instructions. Preview/apply freshness, exact-result semantics, safety metadata, regression builds, native dependency staging and required artifacts are complete.

## Rollback verification
No production installation or rollback was performed. Removing the Offline reference/tool composition restores the previous live-only Router surface. Existing RHP registration and installed files remain untouched; generated copies are ordinary files and require no runtime rollback.

## Remaining limitations
- Package is staged only. Production activation was not attempted because independent host registry attestation was not established, as required by AGENTS.md. Router `--validate-install` above validates the staged runtime; it is not evidence of a production plug-in installation.
- Advanced Rhino modeling, Grasshopper, viewports, and plug-in execution remain live-only. No full RhinoCommon parity is claimed. Unknown third-party plug-in data fidelity still requires review in Rhino; originals remain intact.
- Limits: 256 MiB source/output, 64 KiB script, 100000 statements, five-second JavaScript deadline, 32 MB JS allocation budget, 10000 model calls/mutations, four retained previews / 512 MiB. Native geometry work is bounded by operation/input limits rather than an OS-enforced process deadline.
- Offline reads represent saved bytes; Router rejects exact paths it discovers open but cannot see unsaved state in undiscovered Rhino sessions.

## Conclusion
Implemented, tested in Debug/Release, and staged. Offline edits always produce a clearly named duplicate, with no in-place save option. Installed production MCP has not been activated or replaced.
