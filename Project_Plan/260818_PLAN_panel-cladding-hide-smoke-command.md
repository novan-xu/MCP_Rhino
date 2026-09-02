# Panel Cladding Hide Smoke Command

## Background

PanelCladdingEditor currently registers `PCEditorSmoke` as a normal Rhino command. Although it was
created as an internal validation entry point, Rhino exposes every registered command to users and
autocomplete. The user has requested that this command be deleted and not remain visible.

## Goal

Remove `PCEditorSmoke` from the production PanelCladdingEditor RHP entirely while retaining internal
standalone test executables outside Rhino. The supported Rhino command surface becomes exactly:

- `PCEditor`
- `PCCreate`
- `PCClear`
- `PCMatch`
- `PCSpawn`
- `PCSyncFromSurfaces`

## Architecture Ownership

- `src/PanelCladdingEditor/UI/`: production Rhino command registration; delete the smoke command
  class rather than hiding or aliasing it.
- `Packaging/PanelCladdingEditor/`: remove smoke-command metadata and user documentation.
- Existing standalone `Project_Test/` projects: retain internal test coverage without a Rhino
  command entry point.
- `Project_Test/260818_TEST_panel-cladding-hide-smoke-command/`: focused absence and six-command
  inventory regression.

The plug-in identity, assembly, namespace, installation root, RHP name, product name, and remaining
command classes/GUIDs remain unchanged.

## Key Design

1. Delete `PanelCladdingEditorSmokeCommand.cs`, eliminating its concrete `Rhino.Commands.Command`
   type and therefore its Rhino registration.
2. Remove `smokeCommand` from package metadata and remove `_PCEditorSmoke` from current package
   documentation.
3. Update standalone command-contract tests so they require the exact six supported production
   commands and no longer expect the retired smoke type/GUID.
4. Update the existing `PC` command inventory test from seven commands to six and require the
   manifest to omit `smokeCommand`.
5. Add a focused retirement smoke that verifies the source file and compiled type are absent,
   enumerates the exact six production command classes, checks explicit/unique GUIDs, and confirms
   package metadata/documentation do not expose `PCEditorSmoke`.
6. Build, identity-check, package, and install a new version through the supported registry-only
   packaging path.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs` (delete)
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Packaging/PanelCladdingEditor/README.md`
- existing standalone PanelCladdingEditor command-contract tests
- `Project_Test/260818_TEST_panel-cladding-pc-commands/`
- `Project_Test/260818_TEST_panel-cladding-hide-smoke-command/` (new)
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` (standalone test exclusion)
- `Project_Exet/260818_EXET_panel-cladding-hide-smoke-command.md` (after verification)

## Usage

After installing the new package and restarting Rhino, users can invoke the six supported `PC...`
commands. `PCEditorSmoke` will not exist in Rhino command autocomplete or execution.

Internal verification continues through `dotnet run` test projects under `Project_Test/`; it does
not require a production Rhino smoke command.

## Acceptance Criteria

- The compiled PanelCladdingEditor assembly has exactly six concrete Rhino command types.
- No type named `PanelCladdingEditorSmokeCommand` exists in the assembly.
- No production source file registers `PCEditorSmoke`.
- Package metadata has no `smokeCommand` property.
- Current package documentation does not advertise `_PCEditorSmoke`.
- Remaining command GUIDs are explicit, non-empty, and unique.
- Focused and existing command regressions pass in Debug and Release.
- Debug/Release solution builds and direct/packaged RHP identity checks pass.
- A new package installs or stages safely through the supported installer.

## Risks And Rollback

- Any developer macro that invokes `PCEditorSmoke` will stop working; this is intentional because
  the command must no longer be user-visible.
- Removing one command type changes only Rhino's command table. It does not alter Rhino document
  geometry, user text, material workbooks, or saved layouts.
- Rollback reinstates the deleted command class and `smokeCommand` metadata, or installs the prior
  version through the supported rollback path.

## Future Extensions

- Keep future developer diagnostics in standalone test executables or non-production test hosts.
- If runtime diagnostics are ever required, expose them only through an explicitly developer-only
  build or external test harness that cannot enter the production RHP command table.
