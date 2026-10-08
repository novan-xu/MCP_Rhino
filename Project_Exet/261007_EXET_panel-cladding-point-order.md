# Panel command point-order EXET

## Corresponding plan and execution date

Executed 2026-10-07 under the user-authorized
[PLAN](../Project_Plan/261007_PLAN_panel-cladding-point-order.md).

## Related artifacts

[TEST source and results](../Project_Test/261007_TEST_panel-cladding-point-order/README.md)
and [activation summary](../Project_Test/261007_TEST_panel-cladding-point-order/activation-summary.json).
No commit or PR was requested or created.

Requirements were read from the current
`src/MCP_Rhino.Server/Skills/Modeling/StandardFourPointSurfaceRebuildSkill.cs`, its
point-order skill/orchestrator, gravity-frame/candidate analyzer/reconstructor,
front/back flip and direction-tweak implementations, and their archived PLAN/EXETs.

## Implemented scope

Added a pure Application corner-order service and a standalone Rhino adapter.
The algorithm projects gravity into the oriented front-face plane, finds the
normalized lower-left corner, then orders corners clockwise. The adapter reads
the actual straight quad boundary, rebuilds with CreateFromCorners, flips the
Brep front/back and transposes U/V, matching the current standard skill sequence.
The adapter verifies validity and preservation of the original front normal.
It copies geometry user data/user strings and detects already-canonical bilinear
parameterization to avoid replacing geometry on repeat runs.

PCpid stages point-order geometry only for its selected write set after identity
preflight. Geometry and the existing 30-key setup commit share the same Undo record;
failure rollback restores both original geometry and attributes. Read-only layer
context is never reordered. PCUpdate prepares only processable selected sources,
keeps duplicate-CID skips, and applies the source geometry before generating
dependencies. Both commands report reordered counts and per-object skip reasons.

Unsupported curved, holed, nonquad, nonconvex, horizontal/gravity-degenerate or
ambiguous boundaries are reported as point-order skips and retain their geometry.
The commands' other supported operations continue. No bounding-box corners are
used for reconstruction. Selected locked/hidden/reference Update sources fail
before changes; managed locked/hidden dependencies retain the existing update support.

## Deviations from plan

No functional deviations. A final review added saved object IDs to CID change
snapshots so rollback does not depend on RhinoObject wrappers invalidated by
geometry replacement. Native validation was attempted in an independent hidden
host, but failed at RhinoCore startup before executing native assertions.

## Problems found and resolved

Archived standard-skill descriptions mentioned FlipNormal plus SwapUV. The current
implementation instead uses a separate Brep front/back flip followed by SwapUV;
the adapter follows current code. A standalone implementation avoids importing
MCP host dependencies into the separately packaged editor.

PCUpdate already owns/reuses a command Undo record. Applying the MCP skill as a
separate live workflow would split those transactions, so geometry is prepared
without document writes and committed inside each command. Owned PCUpdate failure
rollback uses its existing complete Undo path; ambient failure explicitly restores
the newly introduced source geometry and CID edits without ending the caller's
record. Existing downstream partial-dependency behavior under ambient failure is
unchanged; the native harness tests a failure before dependency mutation.

## Test record

New Debug/Release pure tests passed 480 orientation/permutation cases per run,
plus ambiguity and unsupported-input checks. Original PCpid, layer-scope and
setup-key regressions passed in both configurations. PCUpdate reconciliation and
Undo contract regressions passed in both configurations and were repeated after
the saved-ID correction. Locked-managed-object and command-inventory regressions
passed in Release. The command count remains 12.

Serialized Debug/Release solution builds and standalone RHP builds passed with
zero warnings/errors. Final standalone builds passed again after the isolated
saved-ID correction. Compiled and packaged assembly identities passed. Publish,
direct RHP rebuild and all 27 bundle hashes passed. See TEST README for commands.

The independent native host returned COMException `0x80004005` in
`StartupInProcess`; recorded exit code `-532462766`. No geometry checks or synthetic
document creation were reached. Native parameterization/metadata/Undo assertions
compile but remain unverified. No user document was modified during development.

## Acceptance alignment

Both commands now include the requested standard point-order preparation for
eligible selected surfaces. Pure tests verify lower-left/clockwise semantics for
opposite fronts, rotated and sloped panels. Normal preservation, real geometry
metadata, selected-only writes and single Undo are asserted in the native harness,
with execution explicitly pending. Unsupported targets are surfaced to the user.

## Rollback verification

The installer retained the prior 1.0.91 RHP and its hash matches the independently
attested prior installation. Source rollback removes this integration and its
geometry services/results and restores package 1.0.91. Command runtime rollback
uses staged geometry snapshots and the owning command Undo as described above;
native rollback verification remains pending.

## Installation and remaining items

Version 1.0.92 installed while Rhino was closed. Independent host persistence was
attested before activation; host Install/Validate and a separate post-exit Validate
passed. Registered RHP path/hash, exact command values and all three advanced
registry timestamps passed, corroborated through an independent registry provider.
Final Rhino process count was zero. RHP SHA-256:
`7ce0a85adf187f688eb9638a0245b375b89efc6a9c87c0dfa24e4ab8b164456f`.

After Rhino starts, verify the exact RHP module and expected root/CommandList vs
PlugIn timestamp ownership. Native geometry/Undo and interactive acceptance remain
pending because the independent headless test host could not start.

Post-start recheck on 2026-10-07 passed: the exact 1.0.92 RHP was loaded in the
live Rhino process; its hash matched the installed artifact. Independent host
registry inspection found root and CommandList advanced at load, while PlugIn
retained the installer timestamp. Recorded in TEST activation-summary.json and
post-start-snapshot.log. The user's subsequent PCpid planarity report is handled
by the separate panel-cladding-pid-planar-bounds correction.

## Conclusion

Implementation, pure regressions, build/package checks and independently validated
1.0.92 installation are complete. Native acceptance remains explicitly open.

## GitHub publication follow-up (2026-10-07)

Source commit: `e7b302502c62bb7e4a7ec21206bed57dbfa651dd`.
Pull request: [#10 — panel IDs, frame configuration, and catalogue controls](https://github.com/novan-xu/MCP_Rhino/pull/10).
