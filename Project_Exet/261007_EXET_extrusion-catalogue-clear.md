# Extrusion catalogue clear execution

## Corresponding plan

[PLAN](../Project_Plan/261007_PLAN_extrusion-catalogue-clear.md).
Execution date: 2026-10-07. The user's explicit request authorized construction.

## Related artifacts

[TEST](../Project_Test/261007_TEST_extrusion-catalogue-clear/README.md) contains the
verification script, Debug/Release logs and normal/compact/cleared PNGs. Focused
checks extend the existing extrusion assignment smoke through `--clear-qa`.
No commit or pull request was created.

## Result and actual scope

Extrusion Setup now has a red-text Clear all footer button immediately before
Cancel and Confirm. Clicking it presents an owner-bound warning confirmation
covering configured and unconfigured profiles and the schedule PDF path. No is
the default response and preserves state. Yes clears the dialog's complete
working collection, PDF path, selection, preview, configuration values and parent
options, resets the displayed counts and explains how to extract again or save.

The workbook destination remains selected. The existing main Confirm action
saves the cleared collection; main Cancel discards pending changes. Geometry,
panel assignments, source PDFs and unrelated workbook content are not targeted.
The prompt follows the existing injectable UI-prompt pattern, with unchanged
constructor compatibility for existing callers.

## Deviations from plan

None. No MCP, installer, schema or workbook-repository changes were required.

## Findings and fixes

Loading an empty catalogue previously retained the prior PDF path text. This
could reintroduce an obsolete extraction path from the caller when reopening a
cleared workbook. Empty catalogue load now resets the path to an empty string.

The shared editor reset now restores the default 1D tab/fixed quantity selection,
clears quantity/spacing fields and resets the configure-button label, preventing
stale profile values from surviving clear, catalogue load or re-extraction.

## Test record

`Project_Test/261007_TEST_extrusion-catalogue-clear/Verify-Regression.ps1` exited 0.
For Debug and Release it ran:

```powershell
dotnet run --project Project_Test/260820_TEST_panel-extrusion-assignment/PanelExtrusionAssignmentSmoke.csproj -c <configuration> -- --clear-qa Project_Test/261007_TEST_extrusion-catalogue-clear
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c <configuration> --nologo
```

All four commands exited 0. Both RHP builds reported zero warnings/errors.
The standalone project is the appropriate build scope: MCP host/Router/tool
registration and solution project wiring were untouched.

Focused assertions passed for declined confirmation, accepted full clear,
dependency/editor reset, unchanged caller and workbook before save, persistence
of an empty catalogue, reopening with no inherited PDF path, path-only clearing,
and re-extraction without carrying former configuration. Existing extrusion
assignment serialization, formula, typology and source checks also passed.

Offscreen renders at 1220x820 and 1040x700 confirm the Clear all footer action is
visible without clipping/overlap; a cleared-state render confirms both lists and
the extraction path are empty. A Debug rerun passed after setting the detached
test content background to the Window background and regenerated the images.
`git diff --check` passed.

## Acceptance alignment

All planned implementation, persistence and build criteria passed. Confirmation
responses were injected in process, and the existing controller save path was
called explicitly; no modal Windows UI or live Rhino automation was used. The
native Yes/No MessageBox and final Confirm/Cancel remain for interactive acceptance.

## Rollback verification

The test confirms accepting Clear all leaves the caller's original three profiles
and workbook bytes intact before saving. Reopening from the unchanged workbook
restores all three entries. Revert the two dialog files and new prompt file to
remove this feature without migration. The initial UI implementation did not
change installed files; the later authorized installation is recorded below.

## Current remaining items

Interactive Rhino acceptance and post-start loaded-module/registry ownership
checks remain pending the next Rhino launch. Installation is complete as 1.0.95.

## Conclusion

The requested clear action, confirmation and extraction-path reset are implemented,
verified in Debug and Release, and installed as 1.0.95. Required PLAN, EXET and
TEST artifacts exist.

## Installation follow-up (2026-10-07)

The user explicitly requested installation. The product manifest was advanced
from 1.0.94 to 1.0.95, with usage documented in the package README. The supported
builder completed Release publish and direct RHP rebuild with zero warnings/errors.
Direct PE probes before and after packaging confirmed assembly GUID
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching the plug-in class/manifest and
distinct from the MCP server identity. All 27 bundle hashes passed.

Bundle: `%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.95-extrusion-clear-261007/PanelCladdingEditor-1.0.95`.
RHP SHA-256: `ded2d0f0de6361c5f036f0431e9fa02bb54c033cefce00491ed5e5aa3f257fd8`.

Installation reused the independent hidden `Win32_Process.Create` host mechanism
whose write persistence was attested during this chat's 1.0.94 installation.
Before activation a fresh host snapshot and `StdRegProv` HKEY_USERS/current-SID
read agreed on the existing 1.0.94 registration and RHP hash. Prior attestation and
fresh agreement are linked in TEST `host-persistence.json`. No new probe registry
key was created, and the previously rejected isolated cleanup was not retried.

With Rhino closed, the supported installer and mandatory `-Mode Validate` passed.
After that host exited, another hidden host ran `-Mode Validate` successfully.
`Verify-Activation.ps1` then exited 0, verifying the new existing RHP/hash, all
12 exact command values (including independent registry-provider reads), and
advancement of root, PlugIn and CommandList timestamps. The prior 1.0.94 RHP was
retained in the installer rollback directory and its hash verified. Prior manifest
and registry backups are saved beside the activation evidence.

TEST `activation-summary.json` records success. Rhino process count remained zero;
no Rhino session or Windows UI automation was started. Exact loaded RHP and
post-start ownership checks remain pending the next Rhino launch.
