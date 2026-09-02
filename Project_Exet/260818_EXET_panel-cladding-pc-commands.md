# Panel Cladding PC Command Prefix Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-pc-commands.md`
- Execution date: 2026-08-18

## Related Artifacts

- Command registrations: `src/PanelCladdingEditor/UI/*Command.cs`
- Package metadata: `Packaging/PanelCladdingEditor/package-manifest.json`
- Focused test: `Project_Test/260818_TEST_panel-cladding-pc-commands/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.36/`
- Installed plug-in: `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.36/PanelCladdingEditor.rhp`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

All seven user-invoked Rhino commands now use the shortened `PC` prefix:

| Previous name | Registered name |
| --- | --- |
| `PanelCladdingEditor` | `PCEditor` |
| `PanelCladdingEditorSmoke` | `PCEditorSmoke` |
| `PanelcladdingCreate` | `PCCreate` |
| `PanelCladdingClear` | `PCClear` |
| `PanelCladdingMatch` | `PCMatch` |
| `PanelCladdingSpawn` | `PCSpawn` |
| `PanelCladdingSyncFromSurfaces` | `PCSyncFromSurfaces` |

The prior long names are not retained as aliases. Each existing command class and command GUID is
unchanged. The plug-in product, assembly, namespace, RHP filename, installation root, registry
product name, and assembly-level plug-in GUID remain `PanelCladdingEditor` and
`7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` respectively.

Command-line success, failure, cancellation, and warning messages were updated to use the new
names. Package metadata now identifies `PCEditor` as the primary command and `PCEditorSmoke` as the
smoke command. Current manual Rhino test instructions were updated for the affected commands.

## Differences From Plan

None. The implementation followed the planned exact prefix replacement and deliberately did not add
legacy alias commands.

## Test Record

### Focused command-surface smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-pc-commands\PanelCladdingPcCommandsSmoke.csproj -c Release
```

Both final runs exited `0`. The smoke verifies:

- the assembly contains exactly the seven expected concrete Rhino command classes;
- every class retains an explicit non-empty and unique GUID;
- every `EnglishName` maps to the exact requested `PC...` name;
- no registered name or command-line output retains the old prefix;
- command names remain unique without regard to case; and
- package identity remains `PanelCladdingEditor` while primary/smoke metadata uses
  `PCEditor` / `PCEditorSmoke`.

### Existing command regressions

Debug and Release runs all exited `0` for:

- standalone PanelCladdingEditor smoke;
- create command smoke;
- spawn smoke;
- match smoke;
- clear smoke; and
- surface-sync smoke.

These preserve create's panel-first/curve-second selection flow, command service contracts,
metadata behavior, topology rules, and unique command GUIDs.

### Builds and plug-in identity

- `MCP_Rhino.sln` Debug serial build: PASS, zero warnings/errors.
- `MCP_Rhino.sln` Release serial build: PASS, zero warnings/errors.
- Debug direct RHP/assembly identity: PASS.
- Release direct RHP/assembly identity: PASS.
- Packaged Release assembly identity: PASS.
- PanelCladdingEditor plug-in GUID:
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`, non-empty, manifest-matching, and distinct.
- Registered-name source inventory: exact seven new names; no legacy `EnglishName` remains.
- `git diff --check`: PASS; existing line-ending notices only.

### Package and installation

- Package version: `1.0.36` (advanced from the previous `1.0.35` bundle).
- Bundle build: PASS.
- Rhino process count before installation audit: `0`.
- Supported registry-only installation: PASS.
- Installed validation mode: PASS.
- Bundle and installed RHP SHA-256:
  `33CEAE5DBCC4FD0B2B3147CC0B932742B119C67B415132F54FC4D0BE363AC759`.
- Prior active `1.0.32` plug-in was moved to the installer's rollback area.

## Acceptance Criteria Alignment

- Exact seven `PC...` command names: passed.
- No long command aliases remain: passed.
- Package primary/smoke command metadata: passed.
- Existing command classes and GUIDs preserved: passed.
- Plug-in assembly identity unchanged: passed.
- Command behavior regressions: passed in Debug and Release.
- Builds, direct and packaged identity, package construction, installation, and validation: passed.

## Rollback Verification

This change only affects command registration strings, command-facing messages, package metadata,
and documentation. It does not alter Rhino geometry, document user text, material workbooks, or
saved layouts. The supported installer retained the prior `1.0.32` installation in its rollback
area.

## Current Remaining Items

- Start or restart Rhino so it loads installed version `1.0.36` and rebuilds its command table.
- Update any external macros, toolbar buttons, aliases, or scripts that still call the old long
  command names.

No Rhino UI automation or production-document mutation was performed.

## Conclusion

PanelCladdingEditor now exposes a consistent, shorter `PC` Rhino command surface without changing
plug-in identity or command behavior. Version `1.0.36` is built, identity-validated, installed, and
installer-validated; the new names become available when Rhino next starts.
