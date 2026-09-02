# PLAN — PanelCladdingEditor plug-in registration repair

Date: 2026-08-26

## Background

PanelCladdingEditor `1.0.67` is installed at the expected current-user path, but Rhino 8 does not
list it in Plug-in Manager and none of its commands are available. The installed RHP has the correct
assembly GUID and command types, while its registry entry remains in a pre-load hybrid state: both
the root `FileName` and `PlugIn\FileName` exist, but Rhino never created `CommandList`.

## Goals

- Restore Rhino's supported first-load discovery contract for PanelCladdingEditor.
- Repair the malformed `1.0.67` registration during the next install/repair operation.
- Make validation distinguish a pending shorthand registration from a Rhino-expanded registration.
- Reject the hybrid state that previously produced a false-positive validation result.
- Build, package, and activate a new version without changing any panel-editing behavior.

## Architecture ownership

- `Packaging/PanelCladdingEditor/` owns current-user installation, repair, validation, and the
  registry-only Rhino discovery contract.
- `Project_Test/260826_TEST_panel-cladding-plugin-registration/` owns the isolated installer
  regression covering pending, expanded, malformed, and repaired registration states.
- No MCP server, Router, Rhino-document operation, UI, domain model, or command implementation is
  changed.

## Key design

1. Before writing a pending first-load registration, remove only the owned plug-in key after
   verifying that any existing name/path still belongs to PanelCladdingEditor.
2. Write Rhino's abbreviated first-load registration at the plug-in GUID root. Do not pre-create
   `PlugIn`, `CommandList`, or `Panels`; Rhino owns expansion of those subkeys after loading the RHP.
3. Validation accepts exactly two states:
   - pending: the root `FileName` points to the active RHP and no expanded subkeys exist;
   - expanded: `PlugIn\FileName` points to the active RHP, root `FileName` is absent/blank, and
     `CommandList` contains the complete supported command names.
4. A root `FileName` plus an expanded `PlugIn` child is rejected as a malformed hybrid.
5. Bump the standalone package to `1.0.68`, rebuild the exact bundle, and use the normal installer
   safety guard if Rhino is still running.

## Involved files

- `Packaging/PanelCladdingEditor/Install-PanelCladdingEditor.ps1`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Packaging/PanelCladdingEditor/README.md`
- `Project_Test/260826_TEST_panel-cladding-plugin-registration/`
- `Project_Exet/260826_EXET_panel-cladding-plugin-registration.md`

## Usage

Build the package, then run its bundled installer in `Install` or `Repair` mode while Rhino is
closed. Start Rhino once so Rhino expands the pending registration and registers the `PC*` command
surface. `Validate` may be run before or after that first start and reports which valid phase exists.

## Acceptance criteria

- A clean isolated install creates only the supported pending registration shape.
- Installer validation passes for pending and correctly simulated Rhino-expanded states.
- Installer validation fails for the old hybrid state.
- Repair converts the old hybrid state back to the pending shape.
- The package contains one direct RHP with the expected non-empty GUID and all ten `PC*` commands.
- PanelCladdingEditor builds in Debug and Release, package construction succeeds, and the installed
  or staged bundle validates byte-for-byte.
- After Rhino next starts from an activated bundle, Plug-in Manager and the command table can be
  checked against the expanded registration.

## Risks and rollback

- Removing an existing GUID key is safe only after ownership verification; an unexpected product
  name or RHP filename remains a hard failure.
- Rhino may be running during construction. In that case the production installer must stage the
  bundle instead of replacing active files, and final live discovery remains pending until Rhino is
  closed and the staged installer is activated.
- Rollback uses the installer's existing owned-file backup path; registry rollback recreates the
  previous owned package only if installation fails before completion.

## Future extensions

- Add an in-Rhino release smoke that asserts `PlugIn.GetInstalledPlugIns` and every expected command
  after first-start expansion.
- Align the MCP_Rhino installer with the same explicit pending/expanded registry-state model in a
  separate capability change.

## Revision record (2026-08-26)

Live verification after activating 1.0.68 disproved the pending-first-load assumption. Rhino 8.33
started normally and loaded MCP_Rhino, but left PanelCladdingEditor's root `Name`/`FileName` record
untouched, did not load its RHP, and did not create `PlugIn` or `CommandList`. No duplicate GUID,
load-protection entry, command collision, crash event, or invalid RHP identity was present.

The revised design makes the installer authoritative for the complete Rhino registration:

- root product metadata and startup `LoadMode=1`;
- `DirectoryInstall=0`, managed plug-in type metadata, and the canonical registry path;
- `PlugIn\FileName` pointing to the one active RHP;
- exactly the ten supported `PC*` entries under `CommandList`;
- no root `FileName` shorthand dependency.

Validation now accepts only that complete state. The focused regression must reject both the
1.0.68 shorthand-only state and an incomplete command list, then prove that `Repair` reconstructs
the full record. The package version advances to 1.0.69. The live Plug-in Manager check still
requires Rhino to restart after production activation.
