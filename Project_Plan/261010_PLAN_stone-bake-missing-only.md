# 261010_PLAN_stone-bake-missing-only

## Background
The user requested that the bake script detect already-created blocks, skip them, and add only missing outputs. They identified the linked `V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/01_grasshopper/05_STN/StoneSurfaceBake.cs` as the target. It is newer than the repository delivery and includes assembly surface output, visible-source filtering and host-owned Undo handling.

## Goals
Make the linked script additive: skip existing baked assembly occurrences before source geometry traversal; bake only missing occurrences. Preserve all existing objects, shared definitions, attributes, transforms and hidden/locked outputs. Retain source visibility, direct surfaces with STN_ID/Assembly_ID, input settings, and Undo behavior for additions.

## Architecture ownership
This is a standalone Rhino/Grasshopper script change. Modify only the user-selected linked delivery plus regression artifacts. Do not regenerate it from the older repository adapter or change MCP/Router/plugin capabilities. No model bake, file save, UI automation or installation is requested.

## Key design
- Index output instances across hidden/visible and locked states. Match stable assembly occurrence keys first. For untracked assembly outputs, require matching WF assembly name/Assembly_ID and placement; a reused definition name alone must not suppress a genuinely missing source instance.
- Remove matched source occurrences before geometry traversal. Return/report their existing output IDs as skipped. Changed settings, moved tracked outputs, or edited existing attributes do not trigger a rebake.
- Existing compatible definitions may be reused without mutation. If current settings differ, choose an unused scoped definition name for new outputs; never modify an existing shared definition.
- Remove legacy migration/deletion and all existing-output update/replace paths from Apply. Add definitions and missing instances only, within existing Undo scope. All-existing runs do not start Undo or write layers/objects.
- Retain old individual stone outputs untouched; do not silently migrate or delete them. Existing tracking remains the identity authority for current assembly output.
- Stage the changed script in the task TEST folder, compile and test it before replacing the linked file. Back up the original locally and check its hash before replacement to avoid overwriting concurrent edits.

## Files
The linked project script; `Project_Test/261010_TEST_stone-bake-missing-only/`; existing standalone test harness registration for a new optional test flag; matching PLAN/EXET documents.

## Usage
Keep the existing Grasshopper file link. Refresh/recompute it with Bake=false, then Bake=true adds only missing assembly outputs. Report distinguishes new and skipped counts. Existing blocks require deliberate removal if the user later wants a new bake with different settings.

## Acceptance
Compile/emit actual updated delivery with fresh and configured input bindings in Debug and Release. Run existing visibility, member-identity, geometry-sharing, linked-output and Undo regressions. Add tests for tracked/legacy matching, distinct source occurrences with shared names, moved tracked outputs, no name-only suppression, unique definition naming, filtering before geometry traversal, and absence of existing-object/definition mutation or deletion paths. Record native Rhino bake as unexecuted unless actually run.

## Risks and rollback
Untracked manually moved outputs cannot be safely tied to a source occurrence by name alone. Older individual-stone output is retained, not migrated. Source geometry added inside an already-baked assembly is skipped with that assembly; this is create-once behavior. Restore the exact backed-up linked script to roll back. No production .3dm is opened or modified by this construction.

## Future work
An explicit refresh/rebake mode can be added only if separately requested; this change intentionally makes the default missing-only.
