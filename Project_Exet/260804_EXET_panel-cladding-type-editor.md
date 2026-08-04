# Panel cladding type editor execution

## Corresponding plan

- Plan: [Project_Plan/260804_PLAN_panel-cladding-type-editor.md](../Project_Plan/260804_PLAN_panel-cladding-type-editor.md)
- Execution date: 2026-08-04

## Related artifacts

- Tests: [Project_Test/260804_TEST_panel-cladding-type-editor/](../Project_Test/260804_TEST_panel-cladding-type-editor/)
- Shared Grasshopper script: `<PROJECT_ROOT>\02_grasshopper\WF setup\WT01_panel_mullion_attributes.py`
- Verified delivery bundle: `<REPO_ROOT>\_validation\panel-cladding-type-editor\delivery\MCP_Rhino-1.0.3`
- Staged production install: `%USERPROFILE%\AppData\Local\MCP_Rhino\staged\1.0.3-20260804192138899`
- Commit / PR: not created

## Execution result / actual implementation

- Added the local Rhino command `_McpPanelCladdingEditor`; no MCP tool, Resource, transport, chat, Companion, or external process was added.
- Added a modeless Eto editor pinned to one saved Rhino document and panel object. It supports clickable cells, exact Rhino key display, text entry, Tab/Shift+Tab and arrow navigation, Reload Panel, Load Selected Panel, Choose Workbook, Save Type, Close, and unsaved-edit confirmation.
- Added live panel descriptor generation from a stored `Plane`, planar face, or gravity-oriented best-fit curved-panel plane.
- Added bounded mesh projection that classifies planar, singly projected curved, and unsupported multi-depth geometry.
- Added a fixed orthographic axonometric renderer shared by the Eto preview and workbook PNG.
- Centralized parsing/writing for H/V offsets, cladding cell keys, `CW_4.00_CLADDING_TYPE`, `CW_4.00_CLADDING_SIGNATURE`, and `MCP_Rhino.CladdingWorkbookPath`.
- Added deterministic SHA-256 type identity using millimeter-normalized dimensions, offsets, grid, normalized cell values, geometry class, and curved depth samples. Identical signatures reuse a type; curved and planar geometry cannot collide by design.
- Added an Open XML `.xlsx` repository that preserves unrelated tabs, maintains hidden `_CLADDING_INDEX`, creates one proportional top-to-bottom visual panel sheet per unique type, embeds the axonometric PNG, detects collisions, and refuses locked/malformed workbooks.
- Coordinated Rhino attribute writes and workbook replacement. Rhino changes use one `Assign Panel Cladding Type` Undo record; a failed workbook commit restores original Rhino attributes and document workbook configuration. A Rhino no-op does not create an Undo record.
- Extended the existing zero-output Grasshopper script to accept singly projected curved panels using a best-fit frame and projected-mesh validation while still rejecting folds/overhangs. Inputs remain `brep`, `lines`, `toggle`; referenced-object attribute mutation remains the only effect.
- Added the unique Rhino smoke command `_McpPanelCladdingTypeEditorSmoke` and CLI slug `panel-cladding-type-editor-smoke-test`.
- Pinned `DocumentFormat.OpenXml` 3.5.1 and packaged its two runtime assemblies.

## Deviations from plan

- The first version derives the system token from the selected panel layer's final segment and exposes it as an editable field. No separate document-level system-code setting was added.
- The offscreen workbook preview is verified structurally as an embedded PNG and independently as a rendered PNG. The bundled spreadsheet renderer imported and detected the image drawing but did not paint imported drawings in its worksheet raster; this is a verifier limitation, not a missing Open XML image part.
- Production live UI/mutation acceptance could not run in the already-open Rhino instance because the newly built `.rhp` cannot replace a loaded plug-in. The safe installer staged the audited bundle instead of terminating Rhino or overwriting loaded files.

## Issues found and fixed during construction

- Initial workbook styles used six-digit RGB values. Open XML validation correctly rejected them because spreadsheet colors require eight-digit ARGB; all colors were corrected.
- Initial workbook metadata labels were clipped in a rendered visual pass. Column A was widened and long orientation/signature metadata received reserved merged space.
- The initial editor could replace unsaved edits when the command was run again. Dirty-state confirmation and explicit panel reload/load actions were added.
- The initial live commit opened a Rhino Undo record even for a Rhino no-op. External workbook finalization now runs without an Undo record when no Rhino/document attribute changes are needed.
- CLI reflection over Eto-derived types failed outside Rhino because Eto is intentionally not copied to the CLI output. The surface smoke was narrowed to loadable application/domain/file types while the command classes remain checked by direct assembly type lookup and packaged binary scanning.
- The live Router tool surface was discoverable, but calls against the currently open Rhino returned `Transport closed`; no alternate or retired connection path was used.

## Test record

### Grasshopper script syntax

```powershell
python -m py_compile "<PROJECT_ROOT>\02_grasshopper\WF setup\WT01_panel_mullion_attributes.py"
```

- Exit code: 0

### Debug and Release solution builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
dotnet build .\MCP_Rhino.sln -c Release --nologo
```

- Both exit codes: 0
- Both builds: 0 warnings / 0 errors
- Built Server, Transport, Router, and RouterProtocolSmoke.

### Debug and Release panel-cladding smoke

```powershell
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- panel-cladding-type-editor-smoke-test
dotnet run --project .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- panel-cladding-type-editor-smoke-test
```

- Both exit codes: 0
- Checkpoints passed:
  - key parsing, ordering, normalization, and hard-error validation
  - planar, curved/projectable, and multi-depth unsupported classification
  - deterministic, curvature-sensitive, unit-independent SHA-256 identity
  - fixed orthographic axonometric PNG rendering
  - unrelated workbook preservation, hidden index, visual matrix, embedded preview, and identical-type reuse
  - locked-workbook preflight failure
  - local command-only surface with no MCP tool or retired command registration

### Workbook visual verification

- Artifact: `<REPO_ROOT>\_validation\panel-cladding-type-editor\Debug3\panel-cladding-types.xlsx`
- The spreadsheet verifier inspected all three sheets and found no formula errors.
- `Read Me` remained unchanged.
- `_CLADDING_INDEX` remained hidden and contained the full digest/signature mapping.
- The type sheet displayed row B above row A and columns 0 then 1, matching the Rhino panel convention.
- The proportional cells displayed `TER01 / GL01` above `GL02 / STN02`.
- The embedded drawing was detected as an 800 x 551 px PNG.
- Independent PNG visual QA confirmed a curved 2 x 2 orthographic axonometric panel with deformed green grid and orange `0A`, `0B`, `1A`, `1B` labels.

### Package build, isolated install, and command audit

```powershell
Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -OutputRoot _validation\panel-cladding-type-editor\delivery -Version 1.0.3
```

- Exit code: 0
- Isolated installer `Install` and `Validate` modes both exited 0.
- Router `--validate-install` passed.
- Bundle declares `transportMode: router-only`, route protocol 1.
- Package contains `DocumentFormat.OpenXml.dll` and `DocumentFormat.OpenXml.Framework.dll`.
- Binary command scan found only the new `McpPanelCladdingEditor` and `McpPanelCladdingTypeEditorSmoke` among the audited names.
- `McpChat`, Companion panel commands, and Debug Bridge command names were absent.

### Production staging boundary

- Default installer detected active Rhino/Router processes and did not overwrite the installation.
- Final audited bundle was staged at `%USERPROFILE%\AppData\Local\MCP_Rhino\staged\1.0.3-20260804192138899`.
- No client configuration was changed.

## Acceptance criteria alignment

- Existing H/V/cladding key contract: satisfied.
- Referenced-object, toggle-controlled, zero-output Grasshopper behavior: preserved.
- Curved/projectable preview and curved-sensitive identity: satisfied by deterministic smoke and PNG QA.
- Unsupported fold/overhang rejection: satisfied by multi-depth smoke.
- Manual per-cell visual entry and deterministic navigation: implemented and compiled.
- Type code/signature attribute commit with one Undo record and stale-object check: implemented.
- Per-document workbook choice, unrelated-tab preservation, one sheet per type, hidden full-signature index, proportional cell matrix, and embedded preview: satisfied.
- Workbook lock/malformed/change-during-save safety: implemented; locked-file hard error tested.
- Router-only external connection and retired path removal: preserved; binary audit passed.
- Debug/Release and package validation: satisfied.
- Live production command acceptance in the user's open Rhino: pending restart/install only.

## Rollback verification

- No production Rhino document or user workbook was mutated during construction testing.
- The default installer staged rather than replacing the active plug-in, so the currently running Rhino remains on its prior installed version.
- All Rhino attribute mutations are contained in one Undo record; workbook failure restores the pre-save attribute/document-string snapshot.
- New source files are isolated under `PanelCladding` folders plus one command/test hook and can be removed by a separately approved rollback.

## Current remaining item

- Close Rhino and Router processes, run
  `%USERPROFILE%\AppData\Local\MCP_Rhino\staged\1.0.3-20260804192138899\Installer\Install-McpRhino.ps1`,
  restart Rhino, reopen `test.3dm`, then run `_McpPanelCladdingTypeEditorSmoke` followed by `_McpPanelCladdingEditor` on one WT-01 panel. This is required because Rhino cannot hot-replace the loaded `.rhp` and UI automation was not authorized.

## Conclusion

The panel cladding type editor is implemented, tested in Debug and Release, visually verified, packaged under the Router-only contract, and safely staged for the next Rhino restart. The only unexecuted acceptance step is loading the staged plug-in in Rhino and exercising its two new commands against the open project document.

## Superseded on 2026-08-04

The user clarified that this editor must be a standalone Rhino plug-in. Do not install the staged
MCP_Rhino 1.0.3 editor bundle described above. The active implementation, package, commands, and
verification record are now owned by `260804_EXET_standalone-panel-cladding-editor.md`; the
MCP-hosted editor sources and registrations were removed.
