# Frame config/type attributes EXET

## Corresponding plan

[PLAN](../Project_Plan/261007_PLAN_frame-config-type-attributes.md).
Execution date: 2026-10-07.

## Related artifacts

[TEST, runner, logs and schema](../Project_Test/261007_TEST_frame-config-type-attributes/README.md).
No commit, pull request, installation or live document mutation was requested.

## Execution result and scope

The standalone editor now persists combined delete/merge/hide masks in
`CW_1.08_FRAME_CONFIG` as version-1 JSON. `CW_1.09_FRAME_TYPE` receives the existing
assignment JSON format, preserving all profile definitions, quantities, parent
links and length modifiers. Default grids get a valid combined configuration;
unassigned frame type is removed.

The old frame-typology generator, identity model, controller preview and UI value
were removed. The editor instead displays counts of merged, hidden and deleted
layout elements. The old typology key exists only for cleanup. Old 2.09 assignments
and separate 2.10/2.11/2.12 mask keys remain legacy read inputs, with nonblank new
values authoritative. Blank PCpid setup placeholders permit migration fallback.

Extrusion/both saves, PCMatchCrv, PCCreate, changed PCCrvTemplate and changed sync
operations write the new schema and clean obsolete keys in their owned scope.
PCClear recognizes both generations. Cladding-only saves preserve frame settings.
Spawn/Update consume the shared parser and retain generated curve code/formula
attributes. PCMatchCrv still preserves target dimensions/offsets/cladding.

The live repository checks conflicting frame-key variants before case-insensitive
normalization, and includes the new keys in stale-editor fingerprints. Planned
match deletions are honored even when a canonical replacement follows, preventing
case-variant duplicates. Existing transaction/Undo and rollback ownership remains.

## Deviations from plan

No functional scope deviation. An optional clarification about complete settings
versus a short code received no response; implementation followed the stated
complete, restorable JSON default. The configuration intentionally contains grid
counts and masks, without profile assignments, dimensions or an identity hash.

The structural-grid sync smoke had a stale source-string assertion for an older
inline layer comparison. It was updated to recognize the existing managed-layer
helper; the production layer-discovery behavior was not changed for that assertion.

## Problems found and fixed

- Reusing old sparse-mask writes would leave multiple authorities after the rename;
  all active topology writers now emit the combined configuration.
- PCpid already seeds 1.08/1.09 with blanks; those blanks must not hide older data.
- An older or differently cased assignment/type key must be deleted before writes
  to avoid revival on reload or conflicting variants.
- The old fingerprint covered CW_2/CW_4 only; new CW_1 frame values now participate
  so an open editor cannot overwrite external changes undetected.
- Changed template/sync topology validates the retained assignments before commit.
- Previous smoke expectations for separate/sparse masks and generated typology were
  updated to the new contract while keeping legacy-reader coverage.

## Test record

Executed:

```powershell
& ./Project_Test/261007_TEST_frame-config-type-attributes/Verify-Regression.ps1
```

Final exit code: 0. Fourteen suites passed in each configuration (28 smoke runs):
extrusion assignments/migration, curve topology/matching, hide masks, scoped save,
topology persistence, create, curve template/UI, legacy sparse topology, layout
reconciliation, surface sync, structural-grid sync, clear, surface match and update.
The runner records exact project paths and writes one log per suite/configuration.
Earlier failures were superseded by the final successful run after updating old
persistence assertions and the stale sync source-string check.

Both `dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo`
and the corresponding Release build passed with zero warnings/errors. The full
MCP solution was not required: this change is within the independent editor and
does not alter MCP host, Router, tool registration or transport lifecycle.
`git diff --check` passed. Detached WPF controls were exercised by existing tests;
two generated PNGs are retained in TEST and the footer was inspected. No Windows
UI automation was used.

## Acceptance alignment

Migration regressions verify legacy/current config and assignment round trips,
new-key precedence, blank fallback, schema/version/duplicate/malformed rejection,
conflicting case variants, grid-dimension validation and old-key removal. Assignment
tests verify preserved 1D/0D definitions, quantities, formulas, parents/modifiers,
hidden/merged curves, source immutability, replacement/clearing and idempotence.
Generated curves retain target dimensions and formulas. Scoped-save, template,
create, clear, sync and update regressions passed against the renamed keys.

## Rollback verification

Installed RHPs and production documents were untouched. Existing live commit/Undo
and rollback boundaries remain; the matching adapter now applies its existing
planned deletions before canonical writes. Planner failure tests return no partial
match result. Live Undo was not exercised. After future installation, reverting a
document to an older plugin requires restoring its earlier attributes through Undo
or a document backup; the prior plugin cannot read the new combined-key schema.

## Remaining items

Production packaging/installation and live Rhino command/Undo acceptance remain
separate. No bulk migration of existing documents occurred. Historical PLAN/EXET
records for separate masks and frame typology describe earlier behavior and are
superseded by this change.

## Conclusion

The new frame configuration/type attributes are implemented and pass Debug/Release
regressions and builds. The updated plugin is ready for packaging, not installed.

## User-requested installation (2026-10-07)

The user subsequently requested installation. Packaged and installed version
1.0.96, including PCMatchCrv assignment transfer under the new frame keys.

- Release publish/rebuild passed with zero warnings/errors. All 27 bundle hashes
  passed verification in a fresh product staging directory.
- Direct compiled RHP inspection verified assembly GUID
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the manifest and plugin declaration
  and distinct from the compiled MCP_Rhino plugin GUID.
- Before activation, a fresh independent host snapshot and HKEY_USERS/current-SID
  registry provider agreed on the existing 1.0.95 path/hash. The prior successful
  host write attestation was reused; no new registry probe was created.
- A hidden independent host ran the supported Install and mandatory Validate.
  After it exited, a separate hidden host ran Validate again.
- `Verify-Activation.ps1` exited 0: new existing RHP path/hash, exact 12-command
  inventory, all three registry timestamps advanced, independent registry-provider
  agreement, and the preserved 1.0.95 rollback RHP hash were verified.
- Installed at `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.96\PanelCladdingEditor.rhp`.
  SHA-256: `00e8e3eaee0d9a6e5eb2f4bc8e584a05be358d1dbc7813f89e9d9b21a553521d`.

Evidence in TEST: `package-build.log`, `package-assembly-identity.log`,
`package-summary.json`, `host-persistence.json`, `activation-before.log`,
`activation-install.log`, `activation-independent-validate.log`,
`activation-summary.json`, and the prior manifest/registry backup.

Installation and independent validation passed. Rhino was closed throughout and
was not launched. Exact loaded-RHP and post-start registry timestamp verification,
plus live command acceptance, remain pending the next Rhino launch.
