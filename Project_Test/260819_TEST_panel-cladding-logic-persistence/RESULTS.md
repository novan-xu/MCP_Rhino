# Panel Cladding Logic Persistence Test Results

Execution date: 2026-08-19

## Focused smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260819_TEST_panel-cladding-logic-persistence\PanelCladdingLogicPersistenceSmoke.csproj -c Release
```

Both configurations exited `0` and reported:

- deterministic material-free `CW_2.08_CLADDING_LOGIC` encoding;
- saved-owner preservation plus geometry-authoritative split/merge fallback;
- backward-compatible missing logic and fail-closed malformed logic;
- Save, `PCCreate`, `PCMatchSrf`, `PCSyncSrf`, and `PCClear` maintenance of the attribute.

## Historical regressions

The affected standalone PanelCladding smoke projects were run in Debug, including:

- create, scoped-save, clear, spawn, and base surface-sync;
- cell topology, offset sync, extrusion sync, topology persistence, hide-mask, and region planning;
- blank-cell, logical-cell cleanup, full-track collapse, split spawn/sync, and structural-grid sync;
- PCMatch parent fidelity, topology masks, logical cells, explicit parents, cleanup, and existing-target repair;
- PCSyncSrf parent persistence and preserved structural grid.

All exited `0`. Rhino-native Brep probes reported their existing `[SKIP]` result because these console
smokes do not run inside the Rhino native host; their pure planning/contract assertions passed.

Historical expected-write assertions were updated only where the new derived logic attribute adds
one canonical write/delete beside the existing cell set. Existing material, parent-reference,
offset, topology, identity-preservation, and rollback assertions remain active.

## Builds

Commands:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

Final result in both configurations: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.

The first Debug solution build found that the new standalone test folder was not yet in the
server project's narrow standalone-test exclusion list. Adding only
`Project_Test/260819_TEST_panel-cladding-logic-persistence/**/*.cs` to that list resolved the build;
both final builds above passed.

## Package and identity

Package command:

```powershell
powershell -ExecutionPolicy Bypass -File .\Packaging\PanelCladdingEditor\Build-PanelCladdingEditorPackage.ps1 -Version 1.0.57
```

Result: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.57/`.

Packaged identity verification passed:

- MCP_Rhino plug-in id: `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`
- PanelCladdingEditor plug-in id: `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`
- both IDs are explicit, manifest-matched, non-empty, and distinct.

## Safe installation staging

Two Rhino processes were open, so the installer staged rather than overwrote a loaded plug-in:

`C:\Users\nxu\AppData\Local\PanelCladdingEditor\staged\1.0.57-20260819175439409`

The bundle and staged RHP SHA-256 values match:

`eb38281dabeb33b32b23e31567947199c77664ce806e2706acea4e0467c2153e`

Both staged and bundle manifests report version `1.0.57`.

## Closed-Rhino activation and validation

After every Rhino process was closed, the staged installer completed in `Install` mode and then
passed `Validate` mode.

- Installed root: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.57`
- HKCU registration and its `PlugIn` child both point to the installed
  `PanelCladdingEditor.rhp`.
- Installed manifest version: `1.0.57`.
- Installed RHP SHA-256:
  `eb38281dabeb33b32b23e31567947199c77664ce806e2706acea4e0467c2153e`.
- No adjacent `PanelCladdingEditor.dll` exists.
- Both legacy Rhino Package Manager roots are absent.
- Installed assembly identity verification passed with plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`.
