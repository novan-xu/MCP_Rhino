# Suspend cladding type metadata

## Plan and related artifacts

- Plan: [260930_PLAN_panel-cladding-type-suspension.md](../Project_Plan/260930_PLAN_panel-cladding-type-suspension.md)
- Execution date: 2026-09-30
- Tests: [260930_TEST_panel-cladding-type-suspension](../Project_Test/260930_TEST_panel-cladding-type-suspension/README.md)
- No commit, push, or PR was requested or created.

## Implemented scope

Editor saves and surface/curve sync no longer generate, read as identity, return, or
write combined cladding types. The calculated type UI was removed. Save uses region
resolution directly for material normalization and owner-graph validation. Sync
validates that graph while preserving its original geometry-derived parent values.
Unsupported-projection and topology validation remain explicit.

All save scopes and changed-panel sync remove exact retired type names at 2.14,
2.13, 1.10, and 4.00, case-insensitively. PCClear recognizes the same aliases.
The key constants, identity DTOs, constructor compatibility, and direct workbook/type
utilities remain for cleanup or dormant compatibility; live flows no longer generate
their values. No masks, cladding logic, material assignments, or frame typology were
retired. Match/update/spawn do not generate this attribute and retain existing scope.

The requested live document's 850 current type entries were removed through the
existing Router-selected attribute recipe tool. No other metadata changed on those
objects: 30,363 unrelated attributes were verified equal, and all 7,822 document
objects remain. No retired aliases remain. Saving is still required in Rhino.

## Deviations and issues resolved

No substantive plan deviation. Reused the unactivated 1.0.78 release version and
refreshed its existing staging directory so it contains both the previous key
renumbering and this suspension. The scoped-save fixture needed the current spawn
planner layer argument; the material-catalog UI test needed its former type-section
ordering assertion replaced with an absence check. Both corrections passed.

A logging typo in the preserved-attribute count was corrected using the unified
append-only activity logger; verified counts are in the TEST summary.

## Test record and acceptance

All twelve listed Debug suites passed (exit 0); see TEST for exact project folders
and repeatable commands. The standalone Debug build and package's Release publish
and rebuild passed with zero warnings/errors. `git diff --check` passed.
Narrow standalone builds suffice because this change touches no MCP server, Router,
Rhino host, tool registration, or route lifecycle code.

Direct packaged-RHP metadata verification passed against both product identities.
The 1.0.78 staged bundle's 27 manifest hashes passed with no unexpected files.
No installation success or loaded-new-RHP verification is claimed.

## Rollback and outstanding activation

The live attribute recipe created one Rhino Undo record; it was not reversed during
verification. Source rollback can revert the suspension changes without reversing
the previous mask/logic renumbering. The installed 1.0.76 RHP remains reachable and
was observed loaded in Rhino. Neither its files nor production registry were changed.

AGENTS.md requires independent host-persistent registry attestation before activation.
The new package is staged only. After Rhino closes, activation must use the product
installer, `-Mode Validate`, independent registry/timestamp readback, and exact loaded
RHP verification after restart before reporting installation as passed. Until then,
the old running plug-in can still write its former schema.

## Conclusion

Source behavior, regression tests, packaging, and live metadata cleanup are complete.
Document saving and production activation remain outside the completed verification.

## Production activation follow-up — 2026-09-30

The user closed Rhino and requested continuation of the pending activation. Confirmed
no Rhino process remained. Rechecked the staged RHP's nonempty, manifest-matching GUID
and all 27 bundle hashes before installation.

### Independent host access established before mutation

The agent PowerShell process and a separately launched Node-tool PowerShell both saw
a stale 1.0.73 registry pointer. A harmless nonce written from that context was absent
from the Windows WMI registry provider's explicit `HKEY_USERS/<current user SID>` view.
That host view correctly pointed to the existing 1.0.76 RHP. This confirms that a
second child process alone is insufficient evidence in this environment.

Launched a hidden Windows PowerShell host process through `Win32_Process.Create`.
The TEST helper's Probe mode wrote a separate harmless nonce; the independent WMI
registry provider read back the exact value. The host process also captured all three
real registration timestamps and the existing RHP hash. Only after this successful
host-persistence test did activation proceed. Both temporary probe keys were removed.

### Installation and independent validation

The host helper checked resolved filesystem move targets against the product/legacy
directories, rejected reparse points, exported the prior production registration,
and copied the prior ownership manifest. It then ran the staged product installer
with `-Mode Install` followed by the mandatory `-Mode Validate`.

After that process exited, another independently launched host process ran
`-Mode Validate` and captured a fresh registry snapshot. Acceptance passed:

- `PlugIn/FileName` names the existing `plugin/1.0.78/PanelCladdingEditor.rhp`.
- RHP SHA256 is `d5094c99f48eb45a4982950aa6b3e7497c2a5a14b978fe2a0ac79782fdb17f08`,
  matching the staged bundle.
- `CommandList` contains exactly the eleven expected PC commands and values.
- Root, `PlugIn`, and `CommandList` timestamps all advanced from the preflight snapshot.
- The Windows registry provider independently agrees on the new registered RHP path.
- The installer preserved the prior 1.0.76 RHP in its owned rollback directory.

Machine-readable nonlocal evidence is in `activation-summary.json`; raw snapshots,
installer results, prior manifest, and registry export remain ignored local evidence
inside the matching TEST folder. `Host-PanelCladdingActivation.ps1` is the focused
verification/activation helper, not an MCP or UI automation capability.

Production installation and independent registration validation are now complete.
Rhino remains closed. The next startup must still verify the exact loaded 1.0.78 RHP
and root/CommandList timestamp refresh with the installer-owned PlugIn timestamp
unchanged; no live-load or PC-command execution is claimed by this follow-up.
