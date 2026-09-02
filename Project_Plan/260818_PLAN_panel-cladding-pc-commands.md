# Panel Cladding PC Command Prefix

## Background

The standalone PanelCladdingEditor plug-in currently registers seven Rhino commands with the long
`PanelCladding` prefix (one historical command uses the casing `Panelcladding`). The user wants a
short, consistent `PC` prefix for quicker command-line entry and autocomplete.

## Goal

Replace the registered Rhino command prefix `PanelCladding` / `Panelcladding` with `PC` while
preserving each command's existing suffix, implementation, GUID, selection flow, Undo behavior, and
results. The supported command set becomes:

- `PCEditor`
- `PCEditorSmoke`
- `PCCreate`
- `PCClear`
- `PCMatch`
- `PCSpawn`
- `PCSyncFromSurfaces`

## Architecture Ownership

- `UI/*Command.cs`: Rhino command registration names and command-facing status messages.
- `Packaging/PanelCladdingEditor/package-manifest.json`: primary and smoke command metadata.
- Active test READMEs: current manual Rhino verification instructions.
- `Project_Test/260818_TEST_panel-cladding-pc-commands/`: compiled command inventory and legacy-name
  rejection coverage.
- `Packaging/PanelCladdingEditor/`: versioned release package and safe activation or staging.

The product, assembly, namespace, class names, plug-in GUID, registry product identity, install
root, and RHP filename remain `PanelCladdingEditor`; this change is limited to the user-invoked Rhino
command surface.

## Key Design

1. Perform a literal prefix substitution and keep suffixes unchanged:
   `PanelCladdingEditor` → `PCEditor`, `PanelcladdingCreate` → `PCCreate`, and so on.
2. Change each command's `EnglishName`, which is Rhino's registered English command name. Do not add
   alias command classes for the legacy names; the request is to replace the prefix, and duplicate
   aliases would leave the long surface registered.
3. Keep every existing command class and `[Guid]` unchanged so plug-in/toolbar identity and command
   persistence are not conflated with the visible command spelling.
4. Update command-originated success, failure, and cancellation text to name the new command.
5. Update package `command` / `smokeCommand` metadata to `PCEditor` / `PCEditorSmoke` and update
   current manual-smoke documentation for the other commands.
6. Add a focused inventory smoke that enumerates all concrete Rhino command types, verifies the
   exact seven-name mapping from source/compiled assembly ownership, checks uniqueness and GUID
   stability, rejects any registered `PanelCladding` prefix, and confirms package metadata.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingEditorCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingCreateCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingClearCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingMatchCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingSpawnCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingSyncFromSurfacesCommand.cs`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- relevant active `Project_Test/*/README.md` manual Rhino instructions
- `Project_Test/260818_TEST_panel-cladding-pc-commands/` (new)
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` (standalone test exclusion)
- `Project_Exet/260818_EXET_panel-cladding-pc-commands.md` (after verification)

## Usage

After installing and restarting Rhino, type one of the new commands, for example `_PCEditor` or
`_PCCreate`. The former `_PanelCladding...` spellings are intentionally no longer registered.

## Test Strategy

The focused smoke will assert:

- the plug-in assembly contains exactly seven concrete Rhino command classes;
- every command class keeps its existing explicit non-empty and unique GUID;
- each class's `EnglishName` implementation maps to the exact expected `PC...` name;
- all seven names are unique and start with `PC`;
- no `EnglishName` retains `PanelCladding` or `Panelcladding`;
- package metadata names `PCEditor` and `PCEditorSmoke`; and
- command source contains no command-facing legacy-name messages.

Existing standalone editor, create, clear, match, spawn, and surface-sync smokes will be updated or
rerun as appropriate. Serial Debug/Release solution builds, direct RHP builds, assembly identity
validation, `git diff --check`, package construction, and safe activation/staging remain required.

## Acceptance Criteria

- Rhino registers the exact seven `PC...` names listed above.
- No long `PanelCladding...` command alias remains.
- Primary/smoke package metadata uses `PCEditor` / `PCEditorSmoke`.
- Command class and plug-in GUIDs are unchanged, explicit, non-empty, manifest-matching, and unique.
- Command implementation behavior is unchanged.
- Focused and existing command regressions, Debug/Release builds, identity, and package checks pass.

## Risks And Rollback

- Existing macros, aliases, toolbars, or scripts that call the former long names must be updated to
  the `PC...` names. This is an intentional breaking command-surface rename.
- Rhino must restart after the new RHP is activated before its command table reflects the new names.
- Rollback restores the seven prior `EnglishName` strings and package metadata. No Rhino document,
  workbook, geometry, or user-text migration is involved.
- The plug-in/product identity is deliberately unchanged, preventing duplicate plug-in registration.

## Future Extensions

- Publish toolbar buttons or aliases that use the `PC...` command inventory.
- Add a generated end-user command reference if more Panel Cladding commands are introduced.
