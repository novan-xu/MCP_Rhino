# Panel cladding object names EXET

## Plan and execution date

Executed 2026-10-06 under
[the PLAN](../Project_Plan/261006_PLAN_panel-cladding-object-names.md), authorized
by the user's explicit request for short CID-based panel/dependency names.

## Related artifacts

[TEST commands and coverage](../Project_Test/261006_TEST_panel-cladding-object-names/README.md),
[focused smoke](../Project_Test/261006_TEST_panel-cladding-object-names/Program.cs),
and [portable verification](../Project_Test/261006_TEST_panel-cladding-object-names/verification-summary.json).
No commit or PR was requested or created.

## Implemented scope

The shared Application CID service now formats short names by removing a leading
CID_BKT_ prefix, or only CID_ for other identifiers. The full suffix is preserved,
including panel corner role and surface/curve code. Unprefixed custom identifiers
remain intact. Naming never changes full PID/CID user text.

Existing live panel identity normalization also updates ObjectAttributes.Name,
including when the CID already matches or unit type is absent. Missing CID leaves
the panel name alone unless existing unit-type normalization supplies its CID.
Create/save/match/spawn/sync/update paths inherit this behavior. Spawn and update
name both surfaces and extrusion curves from their complete dependency CIDs. Sync
reads current object names, detects name-only differences, and writes short names
through its existing commit/rollback transaction. Panel NameChanged is separate
from CladdingChanged. Package version is 1.0.86, including the prior unit-type fix.

## Deviations from plan

None. Prefix handling is documented for the requested BKT example without guessing
how other project prefixes should be shortened. Native acceptance and activation
remain separate while Rhino is open.

## Problems found and resolved

PCCreate previously compared only user text after identity normalization. This
would discard a correction when the name alone differed. It now includes the
normalizer's changed result in its commit guard. Sync previously lacked current
dependency names in snapshots and filtered writes only by other metadata; new
name comparisons ensure otherwise-current objects are renamed. Existing surface
sync baseline fixtures now include correct names so their no-change assertions
continue to test genuinely unchanged state.

## Test record

All eight invocations recorded in TEST passed: focused naming in Debug/Release;
surface sync, curve sync, create, unit-type roles, PCUpdate, and Undo contract in
Debug. The focused suite checks the user's exact example, role/dependency suffixes,
case/whitespace, custom/missing identities, and ten sync snapshot scenarios. It
confirms isolated/all name-only writes reach the commit interface and unchanged
names produce no writes. Live adapter routing is checked through source contracts.
The optional native Undo branch explicitly skipped; no native transaction is claimed.

Standalone Debug/Release builds and Release package publish/rebuild passed with
zero warnings/errors. Product-only builds are sufficient because MCP, Router,
plug-in host, registration, and transport did not change. Direct assembly identity
checks passed before packaging and against the staged RHP. Both products retain
distinct non-empty GUIDs matching their manifests. All 27 bundle hashes matched.
RHP SHA-256: `124598be6c9684664fe5726cfbe07b271c951aa09e0e215f765621ddd18837c0`.
Final git diff --check passed.

## Acceptance alignment

CID_BKT_S1_06_14 becomes S1_06_14; dependencies keep the remainder of their full
CID, such as S1_06_14-P-0A and S1_06_14-P-INT_B1. Existing configured panels and
their dependencies receive the names through PCUpdate. Sync repairs name-only
differences. CID/PID, material, coverage, and topology logic are unchanged by the
name formatter. Unit-type, duplicate-selection, and Undo contracts remain passing.

## Rollback verification

Original Rhino attribute snapshots already include names and are used by existing
rollback paths; PCUpdate retains its existing Undo ownership contract. No live
document or installed registration was modified during development. Rhino's
current 1.0.84 installation remains in place. This source change can be reverted
independently of earlier fixes. Native failure/Undo verification remains unclaimed.

## Remaining items and conclusion

Implementation and automated verification are complete. The verified 1.0.86
bundle is staged at
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.86-object-names-261006`.
Close Rhino before activation, then use the established host-persistence proof,
installer Install/Validate, and independent post-install/startup gates. Native
Rhino acceptance of actual panel/dependency naming remains pending activation.

## Production installation follow-up — 2026-10-06

The user explicitly requested installation. Confirmed zero Rhino processes and
reverified all 27 staged bundle hashes and the compiled assembly GUID before
activation. The existing host activation helper ran through hidden same-user
Win32_Process.Create. Its probe nonce was independently confirmed through the
Windows registry provider before any installed RHP or registration was changed;
the temporary probe key was then removed and its absence verified.

The helper checked absolute installer move targets inside the product/legacy
directories, rejected reparse targets, backed up the prior ownership manifest and
registry, and ran the staged installer in Install and mandatory Validate modes.
Both passed. After that process exited, another independent host process ran
Validate and captured the persistent registration. Acceptance passed:

- PlugIn/FileName points to the existing 1.0.86 RHP with the staged SHA-256 above.
- CommandList contains exactly the eleven expected PC commands and values.
- All three registry key timestamps advanced from the preflight snapshot.
- Windows registry provider independently agrees on the installed RHP path.
- Ownership manifest reports 1.0.86; the prior 1.0.84 RHP is preserved in the
  installer-owned rollback directory with its original hash.

See [activation-summary.json](../Project_Test/261006_TEST_panel-cladding-object-names/activation-summary.json).
Raw host snapshots, installer output, prior manifest, and registry export remain
ignored local evidence in TEST. No UI automation or live document mutation occurred.
Installation and independent registration validation are complete. Rhino remains
closed; exact loaded-module/post-start registry-lifecycle verification and native
command acceptance remain pending the next Rhino startup.

## Post-start verification and publishing follow-up — 2026-10-06

Rhino process 43292 loaded the exact installed 1.0.86 RHP. An independent host
snapshot confirmed its hash and eleven-command registration. Root/CommandList
timestamps advanced at startup while installer-owned PlugIn remained unchanged.
The activation summary records the passing check using portable path notation.
No live geometry-command or native Undo acceptance is claimed.

The user authorized committing, pushing, and merging all completed work. The
release includes the seven matching PLAN/EXET/TEST sets and source changes through
1.0.86. Raw local registry rollback exports now have a narrow ignore rule; generated
workbooks, caches, and unrelated temporary outputs remain local. Existing Debug/
Release build and regression evidence covers the unchanged production code.
