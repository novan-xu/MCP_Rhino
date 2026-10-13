# 261010_EXET_stone-bake-missing-only

## Corresponding plan

[261010_PLAN_stone-bake-missing-only](../Project_Plan/261010_PLAN_stone-bake-missing-only.md). Execution date: 2026-10-10.

## Related artifacts

[TEST folder and reproduction](../Project_Test/261010_TEST_stone-bake-missing-only/README.md), including `MissingOnlyChecks.csx` and `Run-Validation.ps1`. The existing `261009_TEST_grasshopper-stone-bake` standalone project registers the new optional check. No commit or PR was requested or created.

## Execution result and delivered scope

Updated only the user-selected production script at `V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/01_grasshopper/05_STN/StoneSurfaceBake.cs`, plus construction/test artifacts. The delivery is newer than the repository script and was edited directly after staging and validation; no regeneration from the older adapter occurred.

The script now indexes existing output instances including hidden/locked outputs before source traversal. Stable `assembly/<source GUID>` identities skip already-baked source occurrences even if output geometry, placement, name or attributes have changed. Untracked fallback requires the WF assembly name (including scoped variants), Assembly_ID, acceptable owner, and exact placement within tolerance. A fallback output is consumed once so coincident source instances are not all suppressed by one output.

Only missing sources enter geometry traversal and preview. Reused block names do not suppress new source instances. Compatible existing definitions can be reused; incompatible or foreign definitions get preserved and new outputs use an unused scoped name. Apply only creates definitions/instances with fresh attributes. Removed the instance/attribute updates, shared-definition modifications, and legacy individual-stone migration/deletion paths. All-existing runs return before geometry traversal, target-layer checks or Undo. Reports distinguish skipped and new outputs; BakedIds contains skipped and created output IDs.

Visible source filtering, inch conversion, direct surface STN_ID/Assembly_ID metadata and borrowed/owned Undo behavior remain intact. No MCP registration, Router, server, plugin installation, registry state, .3dm, or live model was changed by this task.

## Deviations from plan

Temporary staged/backup script snapshots were moved out of TEST into ignored `.validation/stone-bake-missing-only/` after deployment, with `.csx` extensions. This avoids the server's broad test `.cs` compilation and leaves no maintained production implementation inside TEST. An additional backup was saved beside the linked project file for convenient rollback.

## Problems found and fixed

The prior delivery could update existing output geometry/attributes and delete old individual-stone instances. Those paths conflicted with the user's skip-existing rule and have been removed. Reusing or changing a shared definition could also alter prior outputs indirectly; comparison now allows only unchanged reuse, otherwise the new output receives a separate definition. Untracked matching is constrained by identity and placement to avoid name-only false positives.

## Test record

The staged delivery compiled and passed checks in Debug and Release before deployment. The source hash was checked against the original before backup and again immediately before replacement. The backup was created without overwriting an existing file; its content hash was verified. The deployed file hash matched the tested staging revision.

Then both commands documented in [TEST README](../Project_Test/261010_TEST_stone-bake-missing-only/README.md) ran against the actual V: path, each exiting 0. The full script compiled/emitted with three port binding configurations; actual emitted-script matching, visibility, geometry, member-identity and eight Undo scenarios passed. Syntax checks verified early skip and absence of existing-document update/delete calls. The setup-script compile and existing baseline regressions passed.

No solution/plugin rebuild was needed: this task changes a standalone external GH script and its standalone test registration, not server/Router/plugin lifecycle. No native Rhino mutation, actual GH solve or live object counts were obtained; native smoke remains unexecuted.

Delivered SHA256: `03D7692DF207A8742EA3766B97527AC03D9029490D167EF0C986C20355C04ACF`.

## Acceptance alignment

Missing-only selection, source-occurrence distinction, shared-definition preservation, clear reporting, existing geometry/Undo regressions and Debug/Release compilation are verified at the managed/helper and syntax levels. Live bake, repeated solve, hidden/locked behavior in an actual model and live Undo are not claimed as tested.

## Rollback verification

The sibling `StoneSurfaceBake__before_missing_only_20261010.cs` and ignored local snapshot both match original SHA256 `66E276A7EAA8F32115495D2F2E879A02FF122460E0EB54E94FF849680220874A`. Restoring the sibling backup to the linked path reverts the script. No actual rollback or production model operation was run.

## Remaining limitations

Untracked moved outputs cannot be inferred safely by block name alone. Old individual-stone outputs remain untouched and are not treated as complete assembly outputs. The unit of skipping is the whole source assembly occurrence: newly added stones or changed settings within an already-baked assembly do not trigger updates. The older repository script intentionally remains unchanged; future regeneration must not overwrite this newer project delivery.

## Conclusion

The requested linked script is updated and verified in Debug and Release. Existing output updates/deletions have been removed; missing outputs alone enter the bake path. Native Rhino execution remains untested and no live bake was performed.
