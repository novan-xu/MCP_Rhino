# Panel cladding key renumbering verification

Execution date: 2026-09-30. Source, package staging, and live migration passed.
Production activation and post-restart verification remain pending.

The final user-approved schema is `CW_2.10_MERGE_MASK`, `CW_2.11_HIDE_MASK`,
`CW_2.12_DELETE_MASK`, `CW_2.13_CLADDING_LOGIC`, and `CW_2.14_CLADDING_TYPE`.
Source, the staged 1.0.78 bundle, and the live document are updated and verified.
The migration table below records the first pass and its interim key names;
the final numbering revision is recorded at the end of this document.

## Builds and existing regression suites

The production change is four canonical key constants; existing behavior suites
exercise their consumers without adding a duplicate test project or MCP command.

```powershell
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Debug --nologo -m:1 -v minimal
```

Passed: exit 0, zero warnings/errors. The package builder below also performs
Release publish and a direct Release RHP rebuild: exit 0, zero warnings/errors.
Only the standalone plug-in changed; MCP server/Router/host and tool surface are
unchanged, so no full-solution or MCP safety-surface rebuild was required.

All seven existing suites passed with exit 0 using
`dotnet run --project <path> -c Debug --no-restore`:

- `Project_Test/260804_TEST_standalone-panel-cladding-editor/PanelCladdingEditorSmoke.csproj`
- `Project_Test/260818_TEST_panel-cladding-topology-persistence/PanelCladdingTopologyPersistenceSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-hide-mask/PanelCladdingHideMaskSmoke.csproj`
- `Project_Test/260819_TEST_panel-cladding-logic-persistence/PanelCladdingLogicPersistenceSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-clear/PanelCladdingClearSmoke.csproj`
- `Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj`
- `Project_Test/260820_TEST_panel-cladding-type-code-format/PanelCladdingTypeCodeFormatSmoke.csproj`

Coverage includes canonical type naming, type reuse and workbook persistence,
topology save/reload, sparse masks, hidden/merged curves, material-free logic,
surface sync, clear classification, dependency updates, and stable type digests.
Historical RESULTS files were preserved; current runnable expectations and README
instructions use the new names.

## Package verification

```powershell
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -OutputRoot .\.tmp-package-panel-cladding-keys-260930
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild -PluginPath @(
  '.\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp',
  '.\.tmp-package-panel-cladding-keys-260930\PanelCladdingEditor-1.0.78\Plugin\PanelCladdingEditor.rhp'
)
git -c core.safecrlf=false diff --check
```

Passed: direct metadata probe found assembly GUID
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the plug-in class and manifest,
and distinct from MCP_Rhino. The package contains the direct RHP without a duplicate
plug-in DLL. All 27 staged manifest file hashes match.

Staged bundle: `%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.78-key-renumbering-260930`.
Staging used a fresh destination and did not run installation, modify the production
registry, or move/retire the loaded 1.0.76 RHP.

## Live document migration

Used existing Router list/select, filter, document-user-string read, bulk recipe
preview/apply, and activity-log tools against the user's exact saved open document.
No disk .3dm access, scripting injection, or Windows UI automation was used.

| Old key | New key | Verified entries | Old keys remaining |
| --- | --- | ---: | ---: |
| `CW_2.06_MERGE_MASK` | `CW_2.10_MERGE_MASK` | 748 | 0 |
| `CW_2.07_HIDE_MASK` | `CW_2.11_HIDE_MASK` | 531 | 0 |
| `CW_2.08_CLADDING_LOGIC` | `CW_2.12_HIDE_MASK` | 901 | 0 |
| `CW_1.10_CLADDING_TYPE` | `CW_2.13_CLADDING_TYPE` | 849 | 0 |

All four previews passed. Their first-20-object display limit was supplemented by
full before/after attribute reads. No destination key existed before migration.
Each per-key apply used `{user:<old-key>}` to copy the current original value and
remove the old name in the same attribute commit; all four had zero failures.

Before mutation, revalidation detected unrelated concurrent document edits:
the document total changed from 5,611 to 1,321, and unrelated panel metadata
changed. No requested source key or value changed. The baseline was refreshed
before any apply, preserving those concurrent changes. The final comparison proves:

- 907 affected objects retained their IDs, names, layers, and geometry type metadata.
- All 3,029 renamed values exactly equal their baseline strings.
- All 31,001 unrelated attribute key/value pairs on those objects are unchanged.
- Total object count remained 1,321 across the mutation interval.
- No original key remains; no extra or missing attribute was detected.
- No document-level matching key existed; the workbook-path setting was untouched.

Four native Undo records were created through the existing tool contract. Undo
itself was not exercised on the production document. Save remains the user's Rhino
operation; on-disk persistence is not claimed. Live execution of the new PC commands
requires activation of the staged RHP and a Rhino restart.

## Activation handoff

Save the migrated document and close every Rhino window. From ordinary user
PowerShell (outside the agent execution context), run:

```powershell
$bundle = Join-Path $env:LOCALAPPDATA 'PanelCladdingEditor\staged\1.0.78-key-renumbering-260930'
& "$bundle\Installer\Install-PanelCladdingEditor.ps1" -Mode Install -BundleRoot $bundle
& "$bundle\Installer\Install-PanelCladdingEditor.ps1" -Mode Validate -BundleRoot $bundle
```

Follow AGENTS.md's production registry attestation gate: independently check the
new existing `PlugIn\FileName`, exact eleven-command `CommandList`, and all three
key timestamps before accepting installation. After reopening Rhino, verify the
exact 1.0.78 RHP is loaded and root/CommandList timestamps advanced while the
installer-owned PlugIn timestamp did not. Do not run the old PC commands on the
migrated document. Activation, validation, and post-restart checks were not run
in this task because host-persistent registry access was not independently established.

## Follow-up correction verification (2026-09-30)

Corrected `CW_2.12_HIDE_MASK` to `CW_2.12_CLADDING_LOGIC` in production and current
logic test documentation. Re-ran standalone Debug and Release builds, the existing
logic-persistence regression, and the Release assembly-identity verifier: all exit 0,
with zero build warnings/errors. Rebuilt the same 1.0.78 bundle and refreshed the
existing staged destination; all 27 hashes match. `git diff --check` passed.

The previous session disappeared during the follow-up. The same saved document
then reopened and was explicitly selected through its new Router session. Live
inspection found 901 interim keys and zero corrected keys. Preview and apply of
the existing bulk attribute recipe succeeded with zero failures. Full readback
verified all 901 values, zero remaining interim keys, 32,943 unchanged unrelated
attributes, stable object metadata, and a stable total of 1,321 objects. The
machine-readable result is in `correction-summary.json`; `migration-summary.json`
retains the first pass for audit history. The follow-up creates one Undo record
and requires the user's Rhino Save. The restarted Rhino process still loaded
the unchanged 1.0.76 plug-in; corrected 1.0.78 remains staged only.

## Final numbering revision verification (2026-09-30)

| Previous key | Final key | Exact value transfers | Previous keys remaining |
| --- | --- | ---: | ---: |
| `CW_2.05_SEGMENT_MASK` | `CW_2.12_DELETE_MASK` | 12 | 0 |
| `CW_2.12_CLADDING_LOGIC` | `CW_2.13_CLADDING_LOGIC` | 901 | 0 |
| `CW_2.13_CLADDING_TYPE` | `CW_2.14_CLADDING_TYPE` | 849 | 0 |

The delete mask is a storage-name change; binary representation, bit polarity,
sparse-mask rules, topology interpretation, and generated type digests stay intact.
Source changes remain confined to the central constants and current test/docs names.

Re-ran the Debug build command above, all seven listed Debug regression suites,
and the additional sparse-topology suite:

```powershell
dotnet run --project .\Project_Test\260820_TEST_panel-cladding-sparse-topology\PanelCladdingSparseTopologySmoke.csproj -c Debug --no-restore
dotnet build .\src\PanelCladdingEditor\PanelCladdingEditor.csproj -c Release --nologo -m:1 -v minimal
.\Project_Test\260805_TEST_rhino-plugin-assembly-identity\Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
.\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -OutputRoot .\.tmp-package-panel-cladding-keys-260930
git -c core.safecrlf=false diff --check
```

All passed with exit 0; builds had zero warnings/errors. Sparse-topology diagnostics
were updated to the current key numbers and that suite passed again. Identity was
verified directly from the compiled Release RHP before packaging. Refreshed the
same 1.0.78 staged destination and verified all 27 bundle hashes. Installed files
and production registration were not changed.

Live preflight found zero destination keys and no change between initial and
immediate pre-apply snapshots. All three previews and applies succeeded with zero
failures. Full readback verified 1,762 transferred values on 907 objects, 31,361
unchanged unrelated attributes, unchanged names/layers/IDs/type metadata, and a
stable total count of 1,321 objects. See `final-numbering-summary.json`.

Three additional native Undo records were created through the tool contract;
Undo was not exercised. Save is still the user's operation. The activation
instructions above remain valid for the refreshed bundle. The observed loaded
plug-in remains 1.0.76; no new-version live command execution is claimed.

## Lot key follow-up verification (2026-09-30)

Changed the canonical inheritance key to `CW_1.05_LOT` and required-metadata
diagnostic to `LOT_NUMBER_REQUIRED`. The shared internal `ReleaseUserTextKey` and
release DTO names remain for compatibility. Production source has no references to
the old literal key. No Rhino data was read or modified for this change.

Seven Debug suites passed with exit 0 using
`dotnet run --project <existing csproj> -c Debug --no-restore`:

- `260805_TEST_panel-cladding-spawn`
- `260923_TEST_panel-cladding-surface-release`
- `260923_TEST_panel-cladding-curve-release`
- `260903_TEST_panel-cladding-update-command`
- `260805_TEST_panel-cladding-match`
- `260807_TEST_panel-cladding-clear`
- `260818_TEST_panel-cladding-create-command`

The focused inheritance fixtures assert the literal lot key, preserve `007`, exercise
missing/blank/stale/current values, and include conflicting former release values
to verify they are neither read as fallback nor written to new dependencies.
The missing-lot spawn fixture fails even when the former release key is populated.

Standalone Debug and Release builds passed with zero warnings/errors. Direct compiled
RHP GUID verification passed before packaging. Package 1.0.79 was built with
`Build-PanelCladdingEditorPackage.ps1 -OutputRoot .tmp-package-panel-cladding-lot-260930`;
Release publish/rebuild and all 27 staged file hashes passed. Detailed outputs are
local ignored `lot-*.log` files; `lot-key-summary.json` holds nonlocal summary evidence.

Rhino closed during the work, allowing activation after a fresh host-persistent
registry probe. The existing host helper now accepts an optional explicit BundleRoot,
retaining its original 1.0.78 default; 1.0.79 used the explicit new staged bundle.
The host installer ran Install and Validate. After its process exited, a separate
host process ran Validate and confirmed the new existing RHP/hash, exact eleven
commands, and all three advanced registry timestamps. The Windows registry provider
also confirmed the new path. Temporary probe keys were removed and the previous
1.0.78 RHP was retained in the installer's rollback directory.

Installed RHP SHA256:
`60c20aefb88f09d0c83d3b26ab911e9e3f42919f2c06d260de30da6eb69df411`.
Rhino was closed at final verification; exact module loading and post-start registry
timestamps remain to be checked when it reopens. No new live PC command was executed.
