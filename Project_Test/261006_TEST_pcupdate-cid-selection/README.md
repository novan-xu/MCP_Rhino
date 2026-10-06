# PCUpdate CID selection verification

Executed 2026-10-06. All commands below passed. Logs are local ignored artifacts;
the portable [verification summary](verification-summary.json) records the result.

## Regression commands

Run from the repository root:

```powershell
dotnet run --project Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj -c Debug
dotnet run --project Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj -c Release
dotnet run --project Project_Test/260923_TEST_panel-cladding-role-cid/PanelCladdingRoleCidSmoke.csproj -c Debug
dotnet run --project Project_Test/260903_TEST_pcupdate-command-undo/PCUpdateCommandUndoSmoke.csproj -c Debug
dotnet run --project Project_Test/260903_TEST_pcupdate-locked-managed-objects/PCUpdateLockedManagedObjectsSmoke.csproj -c Debug
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Debug --nologo
dotnet build src/PanelCladdingEditor/PanelCladdingEditor.csproj -c Release --nologo
& Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1 -Configuration Release -SkipBuild
```

The existing update-command smoke now covers role normalization before collision
classification, same-PID parent/child selection, all members of duplicate groups,
mixed/all-duplicate batches, cross-PID CID collisions, case/whitespace handling,
missing-CID fallback, and repeated source object IDs. Ownership checks cover
parent/child surfaces and curves, stale dependencies, skipped-parent isolation,
legacy ambiguity, single-source migration, and unselected siblings. Reconciliation
retains both same-PID role dependencies. Source contracts verify normalization
order, unique-only preparation, source-panel exclusion, and skipped selection.

Role-CID regression passed for parent, child, ordinary panels, flag precedence,
all save scopes, create, spawn, sync, and update planning. Undo/mode regressions
are managed contract/API checks. The native `--rhino-undo` probe was updated for
the refactored transaction entry but **not run**. No native Rhino geometry,
selection, Undo, or production document was exercised.

## Package verification

```powershell
$packageOutput = Join-Path (Get-Location).Path '.tmp-package-pcupdate-cid-261006'
# Use a fresh output directory; the build script replaces its publish/bundle children.
& Packaging/PanelCladdingEditor/Build-PanelCladdingEditorPackage.ps1 -OutputRoot $packageOutput
```

Release publish/rebuild passed with zero warnings/errors. Copied the bundle to
`%LOCALAPPDATA%\PanelCladdingEditor\staged\1.0.83-pcupdate-cid-261006` and compared
all 27 `bundle-manifest.json` file hashes with SHA-256. Re-ran the identity verifier
with `-PluginPath` pointing to the existing server Release RHP and staged editor
RHP. Both assembly GUIDs match their manifests and are distinct. Final
`git diff --check` passed. No installer, registry mutation, or activation ran.

## Native acceptance still pending

In a saved test document, select parent/child panels sharing a PID and stale CIDs;
run PCUpdate and confirm corrected CIDs plus updates for both. Add a duplicate
parent and an unrelated unique panel: both parents should be skipped, only those
parents should remain selected, and the child/unrelated panel should update.
Confirm skipped dependencies survive, inspect the reported ambiguous legacy
objects, then test an all-duplicate selection and one-step Rhino Undo.
