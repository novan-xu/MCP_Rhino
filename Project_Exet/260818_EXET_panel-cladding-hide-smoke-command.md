# Panel Cladding Hide Smoke Command Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-hide-smoke-command.md`
- Execution date: 2026-08-18

## Related Artifacts

- Focused test: `Project_Test/260818_TEST_panel-cladding-hide-smoke-command/`
- Updated command inventory: `Project_Test/260818_TEST_panel-cladding-pc-commands/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.37/`
- Installed plug-in: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.37/PanelCladdingEditor.rhp`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

`PanelCladdingEditorSmokeCommand.cs` was deleted from the production plug-in. The compiled
PanelCladdingEditor assembly now contains exactly six concrete Rhino command classes:

- `PCEditor`
- `PCCreate`
- `PCClear`
- `PCMatch`
- `PCSpawn`
- `PCSyncFromSurfaces`

`PCEditorSmoke` is absent from the production source, compiled command inventory, package manifest,
and package README. The `smokeCommand` manifest property was removed rather than left blank.

Internal validation remains available through standalone `dotnet run` projects under
`Project_Test/`. The `PanelCladdingEditorSmoke.dll` produced by the solution is the external test
executable only; it is not copied into the package and does not inherit `Rhino.Commands.Command` in
the production RHP.

The plug-in product/assembly identity and the six remaining command classes/GUIDs were not changed.

## Differences From Plan

None. The command class was deleted entirely, and no hidden or legacy alias was introduced.

## Problems Found And Fixed During Construction

None. The smoke command had no production dependencies outside its own command class, package
metadata/documentation, and test assertions.

## Test Record

### Focused retirement smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-hide-smoke-command\PanelCladdingHideSmokeCommandSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-hide-smoke-command\PanelCladdingHideSmokeCommandSmoke.csproj -c Release
```

Both runs exited `0` and reported:

```text
[OK] PanelCladdingEditor exposes exactly six production Rhino commands.
[OK] PCEditorSmoke is absent from source, compiled types, metadata, and package documentation.
[OK] Internal standalone tests remain external to Rhino's command table.
```

The smoke asserts the exact six concrete `Rhino.Commands.Command` types, absence of the retired
compiled type/source file, non-empty and unique remaining command GUIDs, absence of `smokeCommand`
metadata, and absence of the command from current package documentation.

### Existing command regressions

The following all passed in Debug and Release:

- six-command `PC` inventory smoke;
- standalone PanelCladdingEditor smoke executable;
- create command smoke;
- spawn smoke;
- match smoke;
- clear smoke; and
- surface-sync smoke.

### Builds and plug-in identity

- `MCP_Rhino.sln` Debug serial build: PASS, zero warnings/errors.
- `MCP_Rhino.sln` Release serial build: PASS, zero warnings/errors.
- Debug direct RHP/assembly identity: PASS.
- Release direct RHP/assembly identity: PASS.
- Packaged Release assembly identity: PASS.
- PanelCladdingEditor plug-in GUID:
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, non-empty, manifest-matching, and distinct.
- Production source audit: zero `PCEditorSmoke`, `PanelCladdingEditorSmokeCommand`, or
  `smokeCommand` references.
- Packaged plug-in directory contains the production RHP/dependencies only; no test executable.
- `git diff --check`: PASS; existing line-ending notices only.

### Package and installation

- Version increment: `1.0.36` → `1.0.37`.
- Bundle build: PASS.
- Rhino process count before installation: `0`.
- Supported registry-only installation: PASS.
- Installed validation mode: PASS.
- Bundle and installed RHP SHA-256:
  `824040A9D73D522735E9751B6718B8DB3FEAC0DB7389D0723C5299DB3680B5EB`.
- Prior `1.0.36` installation was moved to the installer's rollback area.

## Acceptance Criteria Alignment

- Exactly six compiled production commands: passed.
- Retired command class/source absent: passed.
- Package metadata and documentation do not expose the command: passed.
- Remaining command GUIDs and plug-in identity unchanged: passed.
- Focused/existing Debug and Release tests: passed.
- Solution builds, direct/packaged identity, package installation, and validation: passed.

## Rollback Verification

Removing the command affects only Rhino command registration and related metadata/documentation. It
does not mutate Rhino geometry, document user text, workbooks, or saved layouts. The supported
installer retained version `1.0.36` in its rollback area.

## Current Remaining Items

- Start Rhino so it loads installed PanelCladdingEditor `1.0.37` and builds the six-command table.

No Rhino UI automation or production-document mutation was performed.

## Conclusion

`PCEditorSmoke` has been removed completely from the user-visible Rhino plug-in. Version `1.0.37`
is built, tested, identity-validated, installed, and installer-validated with exactly six supported
`PC...` commands.
