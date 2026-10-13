# PCpid setup keys and dimensions EXET

## Corresponding plan and execution date

Executed 2026-10-07 under the user-authorized
[PLAN](../Project_Plan/261007_PLAN_panel-cladding-pid-setup-keys.md).

## Related artifacts

[TEST code and results](../Project_Test/261007_TEST_panel-cladding-pid-setup-keys/README.md)
and [activation summary](../Project_Test/261007_TEST_panel-cladding-pid-setup-keys/activation-summary.json).
The original PCpid native harness was extended in place. No commit or PR was requested.

## Implemented scope

PanelCladdingPidSetupService owns the exact 30 requested keys and prepares the
selected panel's complete setup writes. Missing/unassigned fields use the existing
Rhino-storable space convention. Existing non-generated values are retained without
trimming; case variants are canonicalized, conflicting populated variants are
rejected before mutation, and unrelated keys remain untouched.

CW_2.01 width and CW_2.02 height are measured along the panel's own horizontal and
in-plane vertical axes using the tight bounds already extracted by the live adapter.
CW_2.00 is widthxheight. All use model units, invariant culture and the editor's
existing five-decimal formatter. The generated dimensions are refreshed each run,
along with the existing PID/CID/elevation/level assignments. No unspecified PNL,
unit-type or other business defaults are invented.

The live adapter prepares these writes inside its existing selected-only boundary,
before opening Undo or modifying any objects. Full-subtree numbering and PID/CID
preflight are unchanged. Existing one-record Undo and attribute rollback cover the
additional keys. The package version is 1.0.91 and user documentation is updated.

## Deviations from plan

No behavior deviations. Native write/Undo acceptance remains pending because the
existing RhinoCore harness failed startup earlier in this session before document
creation; the extended assertions were compiled but that known failure was not retried.

## Problems found and resolved

An empty user string may remove a Rhino key, so blank fields must use the existing
persisted-space convention. World-axis bounding boxes would inflate widths on
rotated facades; projection into the panel plane avoids that. A conflicting unit-type
case variant could make the final retained role disagree with the planned CID;
setup preflight explicitly rejects this condition before mutation.

## Test record

All seven recorded smoke invocations passed: new setup metadata, original PCpid
and layer-scope regressions in Debug/Release, plus the 12-command inventory in
Release. Coverage includes exact schema, blank representation, preservation,
generated-field replacement, casing, repeat stability, selected-only preparation,
rotated/translated/tilted panels, culture and model units. Original tutorial
expectations, uniqueness and generated-dependency safeguards remain covered.

Serialized Debug/Release solution builds and standalone RHP builds passed with
zero warnings/errors. Publish/rebuild and compiled/packaged assembly GUID gates
passed. All 27 bundle hashes match. Exact commands are in the TEST README.
No user document or new synthetic .3dm was modified. Native extraction/write/Undo
assertions compile but are not claimed as executed.

## Acceptance alignment

Selected panels receive all 30 requested keys. Seven generated fields are refreshed;
the other 23 preserve existing populated values or remain visibly blank. CW_2.00–2.02
derive from geometry. Unselected panels remain read-only context. Conflicting keys,
invalid geometry and identity collisions fail before writes.

## Rollback verification

The installer preserved 1.0.90 under its product rollback directory, and its RHP
hash matches the independently observed prior installation. Source rollback removes
the setup service/integration and restores package version 1.0.90. The native
harness includes restoration of missing keys, old dimensions and original casing
through Undo; native acceptance remains unexecuted.

## Installation and remaining items

Rhino was confirmed closed. Host registry persistence was independently attested
before installation. Host Install and mandatory Validate passed; a separate host
process ran Validate after installer exit. RHP path/hash, exact command values and
all three advanced registry timestamps passed, with independent registry-provider
agreement. Final Rhino process count was zero.

Installed RHP SHA-256:
`70eb5af8aced7fd8f0b035f8bce6f0ae3a6fd9480a41271663f5a6c7bf8d5722`.
Exact loaded-module and post-start timestamp checks await the next Rhino launch.
Native prompts, persisted document writes and Undo remain pending acceptance.

## Conclusion

The requested implementation, automated regressions, package and independently
validated 1.0.91 installation are complete. Native acceptance remains explicitly open.

## GitHub publication follow-up (2026-10-07)

Source commit: `e7b302502c62bb7e4a7ec21206bed57dbfa651dd`.
Pull request: [#10 — panel IDs, frame configuration, and catalogue controls](https://github.com/novan-xu/MCP_Rhino/pull/10).
