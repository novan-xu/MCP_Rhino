# PCpid panel setup EXET

## Corresponding plan and execution date

Executed 2026-10-07 under
[the PLAN](../Project_Plan/261007_PLAN_panel-cladding-pid.md). The user's explicit
request to add PCpid authorized construction.

## Related artifacts

[TEST source, fixture and validation record](../Project_Test/261007_TEST_panel-cladding-pid/README.md).
No commit, push or PR was requested or created.

## Implemented scope

Added PCpid with the four requested prompts in order. Both reference picks are
restricted to the target selection. A Rhino-independent planner derives project
directions from the selected north panel's outward normal, separates coplanar
elevations, orders them in exterior views, identifies global bottom-aligned level
rows anchored at 01, and restarts left-aligned bay numbering for every elevation.
Only observed rows/bays are counted; gaps do not imply missing floors or columns.

The live adapter reads single-face planar Breps on Rhino's UI thread using the
command document's runtime serial. It preserves face reversal, uses tight
plane-aligned bounds and document tolerances, and validates all targets before
preparing any changes. Unsupported geometry, duplicate addresses, unselected
identity conflicts and renumbering that would orphan dependencies fail first.
Hidden/locked/reference objects are included in document collision checks.

Successful writes normalize PID, CID, ELEVATION and LEVEL key casing, use the
existing UNIT_TYPE role suffix and shortened-CID name policies, and preserve all
other attributes and geometry. The adapter reuses Rhino's ambient command Undo
record or owns one when invoked without an ambient record. It restores attempted
attribute changes on write failure and reports a rollback failure explicitly.
No-op repeats do not open their own Undo record. Bay is encoded in PID/CID.

Packaging version is 1.0.89; expected installer and regression command sets now
contain all 12 commands. Production MCP tools/transport remain unchanged. The
Server project excludes the new standalone TEST folder from its smoke-source glob.

## Deviations from plan

Native commit, cancellation, failure-rollback and Undo acceptance remain pending:
the optional RhinoCore harness failed at host startup with COMException 0x80004005.
It never reached a document or mutation. Pure planning and preflight acceptance,
builds and packaging are complete. No production activation was attempted because
independent host-persistent registry access has not been established.

## Problems found and resolved

The live tutorial includes a recessed north facade on the opposite side of the
overall selection center. An outward-from-centroid guess would mislabel it; the
implementation uses oriented face normals and matches N2 correctly. Different
panel heights require bottom edges rather than center heights for level grouping.
Tolerance-aware left ties preserve bottom-to-top elevation ordering.

During test authoring, corrected tuple field names and the additional west-facing
test expectations to follow the exterior-view left-to-right direction. A first
installer wrapper read LASTEXITCODE after a PowerShell script rather than a native
command; the installer itself passed. The corrected terminating-error wrapper
and full isolated registration regression then exited 0.

## Test record

See TEST README and retained logs for exact commands. Final results:

- PCpid planning/preflight regression: Debug and Release pass.
- All 20 anonymized live tutorial snapshots match expected identities, including
  every supplied temp-ID example. Input permutations and rotations agree.
- Command inventory, role-CID and object-name regressions: Release pass.
- `dotnet build MCP_Rhino.sln -c Debug` and `-c Release`: exit 0, zero warnings/errors.
- Standalone Debug build and Release package publish/direct rebuild: pass.
- Compiled and packaged assembly GUID gates: pass, distinct product IDs.
- Isolated installer Install/Repair/Validate and exact PCpid command registration:
  pass; all 27 staged bundle file hashes verified.
- `git diff --check`: pass.
- Native harness: startup failure, exit 1; native checks not executed.

The staged RHP SHA-256 is
`28db95de7cf98f253def1f770997c7dcd37c05480ff9037a143690a031384321`.
The user's tutorial was inspected read-only; its objects were not assigned IDs by
this construction session. Test labels and object GUIDs are anonymized in the fixture.

## Acceptance alignment

The command name, prompt sequence, geometry inference, canonical keys, CID rules,
bay restart and unique computed PID requirement are implemented. Tutorial 1/2 are
N1 levels 01/02 bay 01; 3/4 are N2 levels 01/02 bay 01; 5/6, 8/7 and 9/10 are E1
bays 01, 02 and 03. Unsupported/ambiguous input produces diagnostics before writes.
Runtime mutation acceptance is explicitly separated from snapshot validation.

## Rollback verification

No production installation, registry activation, prior-version retirement or live
user-document mutation occurred. The isolated installer regression cleaned up its
own files/keys. Source rollback removes the five new production files and restores
the version, command-list, documentation and test-exclusion changes. Native Rhino
Undo and partial-write rollback were not exercised because native startup failed.

## Remaining items

The release bundle is staged at
`%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.89-pcpid-261007/PanelCladdingEditor-1.0.89`.
Production activation must follow AGENTS.md's independent host attestation,
installer Validate and post-start loaded-RHP checks. After activation, perform the
native prompt/cancellation/write/Undo acceptance in TEST README.

## Conclusion

PCpid implementation, tutorial-derived numbering regression and release staging
are complete. Production activation and native command acceptance remain pending;
this report does not claim those gates passed.

## Installation follow-up — 2026-10-07

At the user's explicit installation request, installed 1.0.89 over 1.0.88 with
Rhino closed. All 27 bundle hashes and the compiled assembly GUID passed recheck.
Independent host registry persistence was established by a same-user WMI-launched
PowerShell nonce and separate StdRegProv verification before running the installer.
The test nonce was removed. The host script verified installer move targets,
backed up the prior registry/manifest, and ran Install followed by Validate.

After installer exit, a separate host process ran Validate and verified the exact
installed RHP and expected hash, all 12 commands including PCpid, and advancement
of the root, PlugIn and CommandList timestamps. Independent Windows registry
provider reads agreed. The prior 1.0.88 rollback RHP was hash-verified. Portable
evidence is in [activation-summary.json](../Project_Test/261007_TEST_panel-cladding-pid/activation-summary.json).

Installation and registration validation passed. No Rhino process is running and
no user document was modified. Exact loaded-module/post-start timestamp attestation
and native prompt/write/Undo acceptance remain pending the next Rhino launch.

Post-start verification later on 2026-10-07 passed: Rhino process 32772 loaded the
exact installed 1.0.89 RHP, and an independent host snapshot confirmed matching
hash/commands, advanced root and CommandList timestamps and unchanged PlugIn
timestamp. The activation summary records the results. Native prompt/write/Undo
acceptance is still distinct from this successful plug-in load check.

## GitHub publication follow-up (2026-10-07)

Source commit: `e7b302502c62bb7e4a7ec21206bed57dbfa651dd`.
Pull request: [#10 — panel IDs, frame configuration, and catalogue controls](https://github.com/novan-xu/MCP_Rhino/pull/10).
