# Panel cladding unit type EXET

## Plan and execution date

Executed 2026-10-06 under
[the approved PLAN](../Project_Plan/261006_PLAN_panel-cladding-unit-type.md).
The user's explicit change request authorized implementation.

## Related artifacts

[TEST evidence and commands](../Project_Test/261006_TEST_panel-cladding-unit-type/README.md)
and [portable results](../Project_Test/261006_TEST_panel-cladding-unit-type/verification-summary.json).
Existing role-CID and update-command smoke fixtures were extended. No commit or
pull request was requested or created.

## Implemented scope

PanelCladdingCidService now reads only CW_1.06_UNIT_TYPE for panel role. Values
corner_parent and corner_child select -P and -C; flat explicitly selects the
unsuffixed PID-derived CID. Keys/values compare without case sensitivity and
values are trimmed. Legacy parent/child flags are ignored even when conflicting.
All existing save/create/match/spawn/sync/update consumers share this service.
Recognized types normalize stored panel CIDs, including flat. Missing/unsupported
types preserve stored custom CIDs and retain the base fallback when CID is absent.
The service does not rewrite PID or unit type, and missing PID prevents a CID write.
Package documentation describes this contract; the package version is 1.0.85.

## Deviations from plan

None. Activation remains separate while Rhino is open. As a read-only follow-up,
confirmed the previous 1.0.84 installation loaded correctly after Rhino restarted;
its existing EXET and activation summary contain that evidence.

## Problems found and resolved

The previous service treated an empty suffix as an unspecified role. That would
leave a stale corner CID intact when a panel changes to flat. The interpreter now
distinguishes explicit flat (empty suffix) from absent/unsupported type (null),
so all three recognized values normalize correctly. No geometry or ownership
algorithm changes were needed.

## Test record

All eight regression invocations listed in TEST passed: role-CID and PCUpdate in
Debug/Release, plus spawn, surface sync, curve sync, and managed CID scope in Debug.
Typed fixtures cover every save scope and both sync scopes with conflicting old
flags. PCUpdate continues to process shared-PID corner pairs and skip duplicate
CIDs; the flat case clears its stale role before classification.

Standalone Debug/Release builds and Release package publish/rebuild passed with
zero warnings/errors. Product-only builds are sufficient because the MCP host,
Router, registration, and transport were unchanged. Direct assembly GUID checks
passed before packaging and on the staged RHP, including distinct product IDs.
All 27 staged bundle file hashes match. RHP SHA-256:
`c41e081a7278c5651bfebd0cfaa30ad10c3ec9fec0e882c6fd38b0bb5f55b450`.
Final whitespace validation uses git diff --check, recorded in TEST.

## Acceptance alignment

The three values exclusively govern role suffixes. Legacy flags, key/value casing,
whitespace, type transitions, custom IDs, missing/unknown types, and shared-PID
duplicate handling are covered. The existing dependency isolation rules continue
to pass. No live Rhino mutation or UI acceptance is claimed.

## Rollback verification

No installed files or registry were changed. Rhino's loaded 1.0.84 RHP remains
reachable and hash-verified by the previous installation's post-start check.
Source rollback can remove this interpretation change and associated fixture/
documentation updates while preserving all earlier fixes. No document rollback
was needed because all behavioral tests used in-memory snapshots.

## Remaining items and conclusion

Implementation, regression validation, and packaging are complete. Version 1.0.85
is staged at `%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.85-unit-type-261006`.
Installation requires Rhino to close and the existing host-attestation/install/
Validate gates. Native Rhino command acceptance remains pending after activation.
