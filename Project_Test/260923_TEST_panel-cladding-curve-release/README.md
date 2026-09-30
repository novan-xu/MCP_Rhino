# Curve release inheritance regression

As of 2026-09-30, the canonical stored key is `CW_1.05_LOT`. Internal release model
names remain for compatibility. Current fixtures assert the literal lot key and
include a conflicting former `CW_1.05_RELEASE` value to verify it is ignored.

From the repository root:

```powershell
dotnet run --project Project_Test\260923_TEST_panel-cladding-curve-release\PanelCladdingCurveReleaseSmoke.csproj -c Debug
dotnet run --project Project_Test\260923_TEST_panel-cladding-curve-release\PanelCladdingCurveReleaseSmoke.csproj -c Release
```

Both runs passed (exit 0) on 2026-09-23:

```text
[OK] frame, intermediate, and merged curve plans inherit release text; missing release remains absent
[OK] combined spawn reads canonical release key case-insensitively and preserves surface behavior
[OK] both sync scopes repair missing/stale release, preserve current values, and clear removed source release
```

The tests execute pure application planning with in-memory fixtures. They cover all
curve kinds, `007` leading zeros, trimmed values, no release, canonical-key casing,
unchanged parent CID suffixes, and release-only change detection in both sync plan
branches. No Rhino document is created, read, or modified.

Existing regressions, all exit 0 using `dotnet run --project <path> -c Debug`:

- `Project_Test/260923_TEST_panel-cladding-role-cid/PanelCladdingRoleCidSmoke.csproj`
- `Project_Test/260818_TEST_panel-cladding-extrusion-sync/PanelCladdingExtrusionSyncSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj`
- `Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj`

Builds, each exit 0 with 0 warnings and 0 errors:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo -v minimal
dotnet build .\MCP_Rhino.sln -c Release --nologo -v minimal
```

Package 1.0.77 publish and direct Release rebuild passed. Compiled assembly identity
checks before and after packaging confirmed editor GUID
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, matching its manifest and distinct from MCP_Rhino.
The exact new test folder is excluded from the server's standalone-source import.

`git -c core.safecrlf=false diff --check`: exit 0.

Live mutation/Undo and installation are separate checks; see the corresponding EXET
for current deployment status.
