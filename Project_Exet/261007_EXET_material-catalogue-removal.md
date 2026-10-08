# Material catalogue removal execution

## Corresponding plan

[PLAN](../Project_Plan/261007_PLAN_material-catalogue-removal.md).
Execution date: 2026-10-07. The user's explicit request to add a trash button
authorized this construction change.

## Related artifacts

[TEST](../Project_Test/261007_TEST_material-catalogue-removal/README.md) contains
the repeatable verification script, Debug/Release logs, and offscreen images.
The existing material catalogue editing smoke owns the extended C# checks.
No commit or pull request was created.

## Result and actual scope

Material Setup now places an accessible 40-DIP trash button to the right of the
catalogue tiles. It uses the existing rounded button styling, a red vector icon,
an explanatory tooltip and the existing keyboard-focus/disabled visuals.

Selecting a material enables the action. Removing it deletes exactly that item
from the dialog's copy, clears selection/code/description, recalculates tile
width, resets the edit action, and reports that Confirm saves the pending change.
Clearing selection by editing the material code also disables removal. The button
does not automatically select another material after deletion.

Confirm persists the remaining collection through the unchanged workbook path.
Cancel discards the working copy. Existing panel assignments remain unchanged;
the editor's existing fallback for assigned codes is not altered.

## Deviations from plan

Tests extend the existing standalone WPF smoke, so no Server project exclusions
or MCP registration changes were necessary. The user's subsequent `install`
request added deployment, recorded in the plan revision and follow-up below.

## Findings and fixes

The selection handler previously returned immediately for a cleared selection.
It now refreshes action state in that branch, ensuring removal cannot remain
enabled after a code edit, catalogue reload, or deletion.

The workbook repository already supports saving zero materials as an explicit
empty Materials sheet; no persistence/schema changes were necessary.

## Test record

`Project_Test/261007_TEST_material-catalogue-removal/Verify-Regression.ps1` passed.
It runs:

```powershell
dotnet run --project Project_Test/260818_TEST_material-catalogue-editing/MaterialCatalogueEditingSmoke.csproj -c Debug -- Project_Test/261007_TEST_material-catalogue-removal
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet run --project Project_Test/260818_TEST_material-catalogue-editing/MaterialCatalogueEditingSmoke.csproj -c Release -- Project_Test/261007_TEST_material-catalogue-removal
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
```

All four commands exited 0. Both standalone builds reported zero warnings and
errors. Narrow project builds are appropriate because only the standalone
editor's dialog and its existing smoke changed; MCP host/Router/surface code and
project registration were untouched. `git diff --check` also exited 0.

Assertions passed for no-selection and repeated-removal guards, selected-only
deletion, source/workbook isolation before save, cleared edit fields, shortened
tile width, removing the last entry, and reduced/empty catalogue persistence.
Existing material edit/add/category/color/layout assertions also passed.

Both 720x740 and 620x640 offscreen renders were inspected. The trash button stays
beside the catalogue without clipping or overlapping tiles, including when the
catalogue scrolls. No Windows UI automation or Rhino document changes occurred.

## Acceptance alignment

All planned source, layout, behavior, persistence and build criteria passed.
The modal Confirm/Cancel lifecycle was not driven in a live Rhino window; tests
exercise dialog actions in process and explicitly save their material payload
through the existing repository.

## Rollback verification

The test verifies pending removal leaves both the caller's material collection
and the saved workbook unchanged, and a fresh dialog restores the original source
collection. Reverting the two dialog files removes this feature without migration.

## Current remaining items

Interactive Rhino acceptance remains pending the next launch. Installation was
subsequently authorized and completed as recorded below.

## Conclusion

The requested catalogue trash button is implemented, verified in Debug and
Release, and installed as 1.0.94. Required PLAN, EXET and TEST artifacts are present.

## Installation follow-up (2026-10-07)

The user explicitly requested installation. The product manifest was advanced
from 1.0.93 to 1.0.94, and the package README describes material removal.

The supported package builder completed Release publish and direct RHP rebuild
with zero warnings/errors. Direct PE metadata probes confirmed the non-empty
assembly GUID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` in both the prepackage and
packaged RHP. It matches the plug-in class and manifest and differs from the MCP
server's assembly GUID. All 27 bundle file hashes passed.

Bundle: `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.94-material-removal-261007/PanelCladdingEditor-1.0.94`.
RHP SHA-256: `c67a378112435300ad3a49674d23b72fabc354a0c7c8a15ea2e1dbafe41dcd4f`.

The first hidden child-process probe exposed a virtualized stale 1.0.73 registry
view and its nonce was not visible through the independent Windows registry
provider. No installation occurred in that context. A hidden process launched
through `Win32_Process.Create` saw the actual existing 1.0.93 RHP. Its nonce was
independently matched through `StdRegProv` using HKEY_USERS/current SID and removed
before activation. Evidence is saved in `activation-isolated-probe.log`,
`activation-host-probe.log`, and `host-persistence.json` in this TEST folder.

With Rhino process count zero, that independently attested host launch mechanism
ran the supported installer and mandatory `-Mode Validate`. After it exited, a
separate host process ran `-Mode Validate` again. `Verify-Activation.ps1` passed:

- Registered FileName names the existing 1.0.94 RHP and matches the bundle hash.
- All 12 command names and values are exact, including independent provider reads.
- Root, PlugIn and CommandList timestamps all advanced from the host snapshot.
- The prior 1.0.93 RHP remains in the installer rollback directory with its hash
  intact, alongside the prior manifest and registry backup evidence.
- Rhino process count remained zero after installation.

See `activation-summary.json` for the successful installation record. Exact loaded
RHP and post-start root/CommandList-versus-PlugIn timestamp checks remain pending
the next Rhino launch; no Rhino session was started or UI automated.

An optional final cleanup command for the initial isolated-context probe key was
rejected by automatic approval policy (`blocked by policy`) before execution.
It was not retried through another mechanism. The independently verified host
probe had already been removed and its absence verified before installation;
this rejection does not change the successful installation record.

## GitHub publication follow-up (2026-10-07)

Source commit: `e7b302502c62bb7e4a7ec21206bed57dbfa651dd`.
Pull request: [#10 — panel IDs, frame configuration, and catalogue controls](https://github.com/novan-xu/MCP_Rhino/pull/10).
