# Cladding surface release regression

As of 2026-09-30, the canonical stored key is `CW_1.05_LOT`. Internal release model
names remain for compatibility. Current fixtures assert the literal lot key and
include a conflicting former `CW_1.05_RELEASE` value to verify it is ignored.

Run from the repository root:

```powershell
dotnet run --project Project_Test/260923_TEST_panel-cladding-surface-release/PanelCladdingSurfaceReleaseSmoke.csproj -c Debug
dotnet run --project Project_Test/260923_TEST_panel-cladding-surface-release/PanelCladdingSurfaceReleaseSmoke.csproj -c Release
```

The focused suite passed in both configurations on 2026-09-23 (exit 0):

```text
[OK] spawn/update plans inherit panel release on merged and single-cell surfaces
[OK] sync commits missing/stale/removed release; current values and panel types remain stable
[OK] per-panel inheritance and curve-only scope stay isolated
```

Tests use in-memory application fixtures and a capturing live-repository substitute.
They verify merged and single-cell spawn/update plans, lower-case canonical key
lookup, whitespace trimming, leading zeros, nonnumeric release text, stale/missing
surface values, blank/removed panel release, unchanged values, metadata-only commit
filtering, stable panel types, per-panel authority, and curve-only scope isolation.
No Rhino document is read or modified by these tests.

The initial test fixture used an incorrect property name and column-key spelling;
both fixture errors were corrected before the successful runs above.

The following affected regression suites passed with exit 0 using
`dotnet run --project <path> -c Debug --no-restore`:

- `Project_Test/260923_TEST_panel-cladding-curve-release/PanelCladdingCurveReleaseSmoke.csproj`
- `Project_Test/260923_TEST_panel-cladding-role-cid/PanelCladdingRoleCidSmoke.csproj`
- `Project_Test/260805_TEST_panel-cladding-spawn/PanelCladdingSpawnSmoke.csproj`
- `Project_Test/260807_TEST_panel-cladding-surface-sync/PanelCladdingSurfaceSyncSmoke.csproj`
- `Project_Test/260825_TEST_cladding-surface-coverage-metadata/CladdingSurfaceCoverageMetadataSmoke.csproj`
- `Project_Test/260903_TEST_panel-cladding-update-command/PanelCladdingUpdateCommandSmoke.csproj`

Full solution builds passed with exit 0, zero warnings, and zero errors:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore --nologo -m:1 -v minimal
dotnet build .\MCP_Rhino.sln -c Release --no-restore --nologo -m:1 -v minimal
```

The initial Debug solution builds using the default worker count returned exit 1
without compiler diagnostics (at both minimal and normal verbosity). Running with
one MSBuild worker completed successfully in both configurations. No production
code changes were needed for the build retry.

`git -c core.safecrlf=false diff --check` passed (exit 0). The standalone test folder
is excluded by its exact path from the server's imported smoke sources.

Live Rhino attribute persistence and Undo remain unverified by this standalone suite.
