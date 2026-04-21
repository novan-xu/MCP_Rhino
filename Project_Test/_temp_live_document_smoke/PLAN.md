# PLAN: Live-Document Smoke Unit (Temp)

> This plan does NOT follow `Project_Plan/` naming convention on purpose. It lives inside the test folder so that removing the folder removes the plan together. It is NOT expected to sink an `EXET` counterpart under `Project_EXET/`.

## Background

After the `online-mutation-refactor`, every mutation and preview-of-mutation path now routes through `ILiveRhinoDocumentAccessor` → `RhinoDoc.ActiveDoc`. Existing smoke tests prove these paths reject correctly in CLI fallback (`LIVE_RHINO_REQUIRED`), but the real Live behavior — writing into an opened document, round-tripping through the Undo stack, reading back through RhinoCommon — has never been exercised end-to-end. This unit fills that gap.

## Goal

Single CLI command / single Rhino command that exercises **every implemented Live-path (edit-opened-document) capability** in one run, produces a machine-readable Markdown report that any downstream LLM can consume to diagnose failures without needing to read the source.

## Hard Constraint (user-specified)

**Deleting `Project_Test/_temp_live_document_smoke/` must have zero effect on the rest of the project.** Verification is part of the acceptance criteria (build must still succeed after the folder is removed).

## Capability Coverage (6 stages, ~34 Live checkpoints)

| Stage | Tools exercised | Checkpoints |
|---|---|---|
| 1. Layer Management | `GetLayers`, `PreviewModify/Delete/Purge`, `Create/Modify/Delete/PurgeLayers` | 8 |
| 2. Geometry Creation | `CreatePoints`, `CreateLines`, `CreateArcs` ×3 modes, `CreateSurfaces` ×2 modes | 7 |
| 3. Geometry Modification | `PreviewTransform+Transform` ×3 kinds, `PreviewReplace+Replace`, `PreviewDelete+Delete`, `PreviewEditCP+EditControlPoints` | 10 |
| 4. Object Edits | `RhinoObjectEditingAgent.Preview+Apply` × (SetUserText / RemoveUserText / SetLayer / SetDisplayColor) | 4 |
| 5. Object UserText | `PreviewObjectUserTextWrites`, `ApplyObjectUserTextWrites`, `DeleteObjectUserText`, `GetObjectUserStrings` | 4 |
| 6. Document UserString | `SetDocumentUserStrings`, `DeleteDocumentUserStrings`, `GetDocumentUserStrings` | 3 |

Cross-cutting assertion: every Apply call increments `RhinoDoc.UndoRecordSerialNumber` by at least 1; every Preview call leaves it unchanged.

## Architecture — Isolation (how the hard constraint is satisfied)

Everything that has to touch files outside `Project_Test/_temp_live_document_smoke/` uses a **no-op-after-deletion** mechanism:

1. **`src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`** — extended once with:
   - A `Dictionary<string, Func<string[], bool>> _extensionHandlers` field (empty by default).
   - A `partial void RegisterExtensionHandlers();` declaration.
   - A constructor call to `RegisterExtensionHandlers()`.
   - A lookup in `TryHandle` that tries the dictionary before the fixed `switch`.

   Deleting the temp folder strips the partial implementation and `RegisterExtensionHandlers()` becomes a compile-time no-op. The dictionary stays empty, TryHandle falls straight through to the original switch. Behavior reverts exactly.

2. **`.gitignore`** — one line appended: `_validation/live-document-smoke/`. After the temp folder is deleted, the line points to a non-existent path. Harmless.

3. **`src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs`** — **UNTOUCHED**. The existing `_McpDevSmoke` keeps forwarding to `layer-management-smoke-test`. A NEW `_McpLiveDocumentSmoke` command is added as a separate `RhinoCommand` class living inside the temp folder.

## Temp Folder Layout

```
Project_Test/_temp_live_document_smoke/
├── PLAN.md                                                        # this file — covers goals, coverage, run instructions
├── LiveSmokeCheckpoint.cs                                         # checkpoint metadata record + skip exception
├── LiveSmokeReport.cs                                             # .md report builder + writer
├── DeveloperCommandHandler.LiveDocumentSmokeTest.cs               # partial class: entry + CLI fallback + shared helpers
├── DeveloperCommandHandler.LiveDocumentSmokeTest.LiveStages.cs    # partial class: 6 Live stages + cleanup
└── McpLiveDocumentSmokeCommand.cs                                 # Rhino command _McpLiveDocumentSmoke
```

All code uses `internal` / `private` visibility where possible and does NOT add any public surface to the main assembly that outlives the temp folder.

## Report Format

Output file: `<repo>/_validation/live-document-smoke/report_<YYYYMMDD-HHmmss>.md` (gitignored).

### Header
- Run mode: `Live` or `CliFallback`
- Run timestamp (start / finish)
- Environment: document path, initial/final object count, initial/final layer count, Undo record serial delta
- Overall status: PASS / FAIL with checkpoint totals

### Per-checkpoint section
```
## [FAIL] <Stage> / <FeatureName>
**Tool**          / **Service** / **Live Adapter** — file paths
**Input**         — summarized request (JSON-ish)
**Expected**      — what a passing run should observe
**Observed**      — Success / Message / returned data snapshot / UndoDelta
**Suspects**      — pre-filled list of code paths or validators to inspect
**Failure reason**— the specific assertion or thrown exception
```

Passing checkpoints are collapsed to `## [PASS] <Stage> / <FeatureName>` with a short `**Evidence**` line.

## Acceptance Criteria

1. `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` → 0 warnings / 0 errors with the temp folder **present**.
2. `dotnet run --project src/MCP_Rhino.Server -- live-document-smoke-test test-files/MCP_METtest.3dm` from a shell (no Rhino) → exits with code 0, writes a CLI-fallback report that marks mutation checkpoints as `LIVE_RHINO_REQUIRED` "expected rejection" PASS entries and offline read checkpoints as real PASS.
3. In Rhino 8: load `MCP_Rhino.Server.rhp`, open + save `test-files/MCP_METtest.3dm`, run `_McpLiveDocumentSmoke` → all 34 Live checkpoints pass, Undo stack shows one entry per Apply, cleanup leaves document with pre-run layer/object count.
4. **Removal safety**: `rm -rf Project_Test/_temp_live_document_smoke/` → `dotnet build` still 0 errors; `_McpDevSmoke` still works; the dormant `_extensionHandlers` infrastructure stays but has no effect.
5. Any single checkpoint failure does NOT abort remaining stages; cleanup runs unconditionally; final `ExitCode` is non-zero.

## Non-Goals

- Hard-error Live coverage (already covered by CLI fallback smoke in the other test units).
- xUnit / NUnit integration.
- Performance / concurrency testing.
- Generating an `EXET` document (temp unit, not part of the permanent Plan→EXET→Test triple).
