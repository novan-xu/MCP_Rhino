# Online Mutation Refactor Execution

## Scope

Executed `Project_Plan/260420_PLAN_online-mutation-refactor.md` against the current codebase and aligned the implementation with `.clinerules/MCP_Rhino Architecture.md`.

## Implemented

- Switched mutation services from offline `File3dm.Write` to live `RhinoDoc` access through `ILiveRhinoDocumentAccessor`.
- Split edit validation into spec-only and live-document validators.
- Added live selection, live geometry mutation, live object edit apply, live document/object user string mutation, and live undo wrapping.
- Preserved offline read / preview-of-read flows and added `OFFLINE_READ_STALE` warning support to the relevant read responses.
- Removed archive / preflight / file-open-state interfaces, implementations, tools, skills, responses, and agent code.
- Added Rhino plugin host scaffolding under `src/MCP_Rhino.Server/Infrastructure/Plugin/`.
- Added `src/MCP_Rhino.Bridge/` as a standalone stdio-to-named-pipe bridge project.
- Added CLI fallback smoke coverage under `Project_Test/260420_TEST_online-mutation-refactor/DeveloperCommandHandler.OnlineMutationRefactorSmokeTest.cs`.

## Verification

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo`
  - Passed
- `dotnet build src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj --nologo`
  - Passed
- `dotnet run --project src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -- online-mutation-refactor-smoke-test Runtime_Test/MCP_rhino_test.3dm`
  - Passed
  - Verified offline read path remains usable in CLI fallback.
  - Verified live-only mutation paths return `LIVE_RHINO_REQUIRED` in CLI fallback.
  - Verified working copy object count stayed unchanged.
- `dotnet run --project src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj`
  - Returned the expected friendly connection error because Rhino/plugin was not running.

## Remaining Manual Validation

- Load `src/MCP_Rhino.Server/bin/Debug/net8.0/MCP_Rhino.Server.rhp` in Rhino 8.
- Confirm the plugin starts the named pipe server at `\\.\pipe\mcp_rhino`.
- Run `_McpDevSmoke` inside Rhino against a saved active document.
- Connect an MCP client through `MCP_Rhino.Bridge` and validate end-to-end live mutation plus Rhino Undo behavior.

## Post-Review (2026-04-21)

### Audit Result

Cross-checked the tree against every bullet in `260420_PLAN_online-mutation-refactor.md`:

- **Plugin host / transport** — `Infrastructure/Plugin/{MCP_Rhino.RhinoPlugin.cs, McpNamedPipeServer.cs, McpDevSmokeCommand.cs, plugin.manifest}` and the standalone `src/MCP_Rhino.Bridge/` project are in place. `.csproj` copies `MCP_Rhino.Server.dll → .rhp` and `<Reference Include="RhinoCommon" Aliases="rhinocommon">` is wired.
- **Live-only adapters** — `Infrastructure/Rhino/Live/` contains `LiveRhinoDocumentAccessor`, `LiveRhinoGeometryBuilder`, `LiveRhinoGeometryMutator`, `LiveRhinoGeometryValidator`, `LiveRhinoObjectEditOperationApplier`, `LiveRhinoObjectEditValidator`. `NullLiveRhinoDocumentAccessor` lives at the adapter root as prescribed.
- **Interface shape** — `IGeometryMutator`, `IObjectEditOperationApplier`, `IObjectEditSpecValidator`, `ILiveObjectEditValidator`, `ILiveGeometryValidator`, `ILiveRhinoDocumentAccessor` all match the plan signatures (no `File3dm` in mutation signatures). `IRhinoDocumentRepository` is narrowed to `Exists` + `Read`.
- **Services** — creation / modification / editing / user-text / document-user-string services route through `ILiveRhinoDocumentAccessor.Execute` / `ExecuteWithUndo`; offline read paths (`RhinoObjectFilterService`, `RhinoDocumentUserStringService.Read`, `RhinoObjectUserTextService.Read/Preview`) emit `OFFLINE_READ_STALE` via `TryGetActiveDocumentState`.
- **DI partitioning** — `AddOfflineRhinoAdapters` / `AddLiveRhinoAdapters` / `AddCliFallbackLiveRhinoAdapters` + `AddRhinoApplication` + `AddRhinoAgents` split exactly as the plan asks. `Program.Main` wires CLI fallback; plugin wires full live stack.
- **Archive / preflight / file-open-state removal** — `Grep` for `Archive|FileMutation|FileOpenState|Safeguard` inside `src/` returns no matches. Removed tools, skills, services, responses, and enums are gone.
- **Selection-skill split** — `OfflineObjectSelectionSkill` (the file is still named `ObjectSelectionSkill.cs` at the namespace boundary, but the class is the offline filter-path-backed variant) coexists with `LiveObjectSelectionSkill`; the apply/preview skills inject `LiveObjectSelectionSkill` as intended.

### Discrepancies Found and Resolved

1. **`ExecuteWithUndo` pushed empty Undo records on failure / no-mutation paths.** The prior implementation always fell through to `EndUndoRecord` in `finally` even when validation failed or `payload.Mutated == false`, so every failed Apply would still leave an empty entry on Rhino's Undo stack. Refactored `LiveRhinoDocumentAccessor.ExecuteWithUndo` to close the undo record exactly once per call and added a `CloseUndoRecord` helper that guards against `undoRecord == 0`.

2. **Plan prescribed `RhinoDoc.CancelUndoRecord` but RhinoCommon 8.17.0 does not expose it.** Verified via `RhinoCommon.xml` (`Rhino.RhinoDoc` only publishes `BeginUndoRecord`, `EndUndoRecord`, `ClearUndoRecords`, `UndoRecordingEnabled`, `UndoRecordingIsActive`). Kept the single-close semantics via `EndUndoRecord` and left an inline comment documenting that empty records are discarded by Rhino's UndoManager rather than explicitly cancelled. This is a plan-vs-reality deviation worth surfacing: the "no empty Undo entries" guarantee in Key Design #3 is best-effort (dependent on UndoManager auto-collapse), not API-enforced.

### Verification

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` → succeeded (0 warnings, 0 errors).
- `dotnet build src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj --nologo` → succeeded.
- `dotnet run --project src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --no-build -- online-mutation-refactor-smoke-test Runtime_Test/MCP_rhino_test.3dm` → all six checkpoints green; `Initial objects: 1014 / Final objects: 1014`; `LIVE_RHINO_REQUIRED` returned for `SetDocumentUserStrings`, `CreatePoints`, `ApplyObjectUserTextWrites`.
- `dotnet run --project src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj --no-build` → exited with the expected friendly `\\.\pipe\mcp_rhino` connect-timeout message (Rhino not running).

### Outstanding Notes

- Plan's `CancelUndoRecord` expectation cannot be met without a callback-level "did-mutate" signal before `BeginUndoRecord`. Consider a follow-up if manual validation in Rhino shows spurious empty Undo entries after failed Applies.
- Manual Rhino-in-the-loop smoke (plugin load → `_McpDevSmoke` → Bridge → MCP Client) remains pending; CLI-only smoke cannot cover the Undo stack, main-thread marshalling, or pipe transport.

