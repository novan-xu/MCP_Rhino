# Missing-only stone bake validation

The production delivery is the user-selected linked script:
`V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/01_grasshopper/05_STN/StoneSurfaceBake.cs`.
The older repository script is not regenerated or replaced by this task.

## Run

Requires the installed Rhino 8 / Grasshopper assemblies and .NET 8 SDK. From the repository root:

```powershell
& './Project_Test/261010_TEST_stone-bake-missing-only/Run-Validation.ps1' -Configuration Debug
& './Project_Test/261010_TEST_stone-bake-missing-only/Run-Validation.ps1' -Configuration Release
```

Use `-ScriptPath` to validate a separate staged delivery. This invokes the existing standalone Grasshopper harness with `--check-undo --check-assembly-surfaces --check-missing-only`. It compiles/emits the full linked script with fresh, typed, and untyped component bindings without executing the script entry point.

## Results, 2026-10-10

Both Debug and Release passed on the deployed V: file, exit code 0. Local ignored `validation-Debug.log` and `validation-Release.log` contain the output.

- Actual emitted-script matching: stable source keys; moved/renamed/edited tracked outputs; distinct source IDs with the same definition/placement; strict untracked name, Assembly_ID, owner, transform and scale checks; deterministic unused names when scoped names already exist.
- Syntax contract checks on the delivery: hidden/locked output indexing; all-existing return before traversal; only missing sources traverse; each untracked match is consumed once; no existing object/definition modification or deletion APIs; creation-only Apply and shared-definition reuse guard.
- Existing emitted-script regressions: eight Undo ownership/recovery scenarios, visible-source rules, member identities, 45 world-Z geometry-sharing combinations, and preservation of direct Script-Mode data outputs.
- Existing harness baseline and setup-script compilation also pass. Historical attribute-repair/shared-definition tests check comparison helpers; they do not mean this missing-only delivery repairs or updates existing blocks.

Delivered SHA256: `03D7692DF207A8742EA3766B97527AC03D9029490D167EF0C986C20355C04ACF`.
Original SHA256: `66E276A7EAA8F32115495D2F2E879A02FF122460E0EB54E94FF849680220874A`.

Native Rhino document mutation, a live Grasshopper solve, model object counts, and live Undo have **not** been run. The syntax checks verify API paths, not native end-to-end behavior.

## Expected live behavior

Recompute the linked file with Bake=false to preview only missing assembly outputs; Bake=true adds them. The report gives skipped and new counts. BakedIds includes existing skipped IDs and newly created IDs. A repeated bake with no new sources returns before traversal, layers or Undo. Existing outputs remain unchanged when settings, source geometry, or output attributes change.

An existing definition name alone does not mean every source occurrence is baked. Compatible definitions are reused for new instances; incompatible names are preserved and a unique scoped definition is created. Older individual-stone outputs are retained and are not migrated or deleted. A previously baked assembly is skipped as a whole, including newly added stones inside that source assembly.

## Rollback

An exact original backup was saved beside the linked script as `StoneSurfaceBake__before_missing_only_20261010.cs` using create-new semantics. Its hash matches the original above. Restore it to the original linked path to revert, then reload the script. No rollback was executed against the delivered file or any model.

Local original/delivered snapshots are in the ignored `.validation/stone-bake-missing-only/` directory with `.csx` extensions, outside the server's test compilation globs.
