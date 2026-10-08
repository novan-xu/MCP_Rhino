# PCpid panel-layer scope EXET

## Corresponding plan and execution date

Executed 2026-10-07 under
[the user-authorized PLAN](../Project_Plan/261007_PLAN_panel-cladding-pid-layer-scope.md).

## Related artifacts

[Scoped TEST code and results](../Project_Test/261007_TEST_panel-cladding-pid-layer-scope/README.md).
The original anonymized tutorial fixture and native test harness are reused.
No commit or PR was requested or created.

## Implemented scope

PCpid now detects every source surface/Brep at the exact
`01_CW Panels::Surfaces-PNL` root and any descendant layer. Matching is
case-insensitive and requires the `::` separator, excluding similarly named
siblings and unrelated branches. Hidden, locked and reference panels participate
as read-only context. Non-panel curves/annotations do not participate. Unsupported
source Breps fail preflight instead of silently disappearing from the geometry set.

The planner uses the full context for directions, planes, level rows and bay
columns, then returns only selected assignments. The live adapter also checks
each prepared write against the selected IDs. The UI restricts targets to that
scope and permits north/first-floor references anywhere in the scope. Selected
panels still receive coherent PID/CID/elevation/level/name changes; unselected
panels receive no attribute or geometry writes.

Before mutation, the PID audit projects selected proposed IDs onto all context
panels' retained stored IDs. It detects even duplicates between two unselected
panels; blanks are ignored. Selected changes that fix old duplicates are allowed.
Existing document-wide collision and generated-dependency checks remain active.
Undo and attribute rollback behavior are unchanged. Command output separates
context count, selected assignment count and actual change count.

## Deviations from plan

No behavior deviations. Native acceptance remains pending because the existing
RhinoCore host could not initialize earlier in this session. Rhino is running, so
the new package is staged; activation is deferred until it is closed.

## Problems found and resolved

The old planner required selected IDs to equal all input snapshot IDs, which both
prevented full context and made selection size change elevation/row/bay numbering.
It now requires selection to be a subset and filters only at the write-plan boundary.
The previous collision loop checked selected assignments only; a separate effective
full-scope PID audit now catches unselected duplicate pairs without rewriting them.

The first parallel Release solution build encountered a shared DLL/RHP output
copy race (MSB3030). Rebuilding with `-m:1 -p:BuildInParallel=false` passed without
source changes. A direct RHP build restored the packaging output afterward.

## Test record

Both Debug/Release scoped tests and original PCpid regressions passed (exit 0).
All 20 singleton tutorial selections and a partial 4/7/10 selection retain the
full-context expected addresses. Exact write IDs, unselected snapshot preservation,
layer boundaries, duplicate detection and selected repairs are covered.
The 12-command registration regression passed in Release.

Debug solution build and serialized Release solution build passed with zero
warnings/errors. Standalone Debug/Release RHP builds and package publish/rebuild
passed. Compiled and staged assembly identity gates passed; all 27 bundle hashes
match. `git diff --check` passed. TEST README records exact commands and paths.

Staged RHP SHA-256:
`352428f6df08fb62731881234b34d5ed883d5ce5d8575b396c48060e147ddc80`.
No user Rhino document was modified and no new `.3dm` was created by these tests.

## Acceptance alignment

Numbering and PID uniqueness now consider all panels in the specified subtree.
Only selected panels enter the mutation plan. North/first-floor references may be
unselected. Missing/invalid scope, duplicate addresses or duplicate effective PIDs
fail before writes. No unselected PID is silently changed to clear a conflict.

## Rollback verification

The currently installed 1.0.89 RHP remains in place. Its exact module was observed
loaded in Rhino process 32772; an independent host snapshot verified matching hash,
command names, advanced root/CommandList timestamps and unchanged PlugIn timestamp.
The original PCpid activation summary now records these post-start checks.
Source rollback restores selected-only inference and the prior packaging version;
no live document rollback was needed. Native partial-write rollback remains untested.

## Remaining items

Version 1.0.90 is staged under
`%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.90-pcpid-layer-scope-261007/PanelCladdingEditor-1.0.90`.
Rhino must close before installation. Production activation must repeat the host
persistence/Install/Validate/independent-readback gates; native prompt/write/Undo
acceptance follows activation.

## Conclusion

Implementation, scoped identity tests and package staging are complete. Installation
is pending Rhino closure; no 1.0.90 activation or native acceptance is claimed yet.

## Installation follow-up — 2026-10-07

After the user confirmed Rhino was closed, version 1.0.90 was installed. A fresh
same-user host PowerShell nonce was independently verified through the Windows
registry provider before activation. The host installer completed Install and
mandatory Validate; a separate host process repeated Validate after installer exit.
The registered RHP path and SHA-256 match the tested 1.0.90 package, the command
list contains exactly the expected 12 commands, and all three registry timestamps
advanced. Independent HKEY_USERS/current-SID provider reads agree with the host
snapshot. The previous 1.0.89 RHP was preserved in the installer's rollback folder
and its hash was verified.

Portable evidence is recorded in
[activation-summary.json](../Project_Test/261007_TEST_panel-cladding-pid-layer-scope/activation-summary.json).
Zero Rhino processes remained at final validation. Installation is complete;
exact loaded-module/post-start timestamp verification awaits the next Rhino launch.
Native prompt/write/Undo acceptance remains pending. No user document was modified.
