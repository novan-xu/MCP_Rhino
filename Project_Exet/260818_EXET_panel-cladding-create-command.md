# Panel Cladding Create Command Execution

## Corresponding Plan

- Plan: `Project_Plan/260818_PLAN_panel-cladding-create-command.md`
- Execution date: 2026-08-18

## Related Artifacts

- Focused tests: `Project_Test/260818_TEST_panel-cladding-create-command/`
- Production implementation:
  - `src/PanelCladdingEditor/UI/PanelCladdingCreateCommand.cs`
  - `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs`
  - `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingCreateService.cs`
  - `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
  - `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
  - `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
  - `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.32/`
- Installed RHP:
  `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.32/PanelCladdingEditor.rhp`
- Commit / PR: none created in this execution.

## Execution Result / Actual Delivered Scope

The standalone PanelCladdingEditor plug-in now registers the exact Rhino command
`PanelcladdingCreate`. The command requires a saved active document and uses two separate Rhino
object-selection phases:

1. select one or more panel surfaces/Breps;
2. select one or more H/V guide curves.

Cancelling either phase returns before the create service is called, so no panel attributes are
mutated. Once both selections succeed, every selected curve is sampled in every selected panel's
local frame. A stored geometry `Plane` remains the preferred frame; a planar face or gravity-oriented
best-fit plane is used when no valid stored plane exists.

Substantially horizontal curves create bottom-up H offsets and substantially vertical curves create
left-right V offsets. Curves outside the panel extents or panel depth are ignored, diagonal curves
are skipped with a command-line warning, and near-coincident guides are clustered. A selected panel
with no applicable H or V guide rejects the complete operation before mutation rather than silently
resetting that panel to one cell.

The pure planning service validates the grid through `PanelCladdingKeyService`, formats offsets at
the established five-decimal precision, and recreates canonical cells with intentional single-space
values. The reset removes prior generated offset, cladding, type, and signature attributes while
preserving unrelated PID, release, wall type, CID, layer/name, and custom metadata.

All panel snapshots and proposed attributes are prepared before mutation. Changed panels commit in
one `Create Panel Cladding Grid` Rhino Undo record. A failed or exceptional later write restores
earlier panel attributes and reports whether rollback itself failed. One redraw occurs after a
successful transaction.

## Differences From Plan

No product-behavior deviation was required. The existing package version `1.0.32` and corrected
`net8.0-windows` packaging path were already active working-tree changes, so this execution reused
that version rather than overwriting the user's packaging work with another version change.

The direct RHP command-enumeration CLI probe is a plain `net8.0` host and cannot load this WPF
plug-in's Windows Desktop assemblies. Command type, GUID uniqueness, exact English name, and prompt
order were therefore verified by the focused `net8.0-windows` smoke, while the established metadata
probe independently verified the compiled and packaged assembly-level plug-in identity.

## Problems Found And Fixed During Construction

1. The first command build used response/result property names that do not exist in this repository's
   contracts. The adapter was aligned to `OperationResponse.Message` and the selected/updated id
   collections; the next build passed with zero warnings.
2. The MCP server project deliberately includes repository test sources and excludes standalone
   panel test projects explicitly. The new create smoke and the same-day cell-topology smoke were
   initially captured by that production compile glob, causing duplicate generated assembly
   attributes during repository-wide identity validation. Both standalone test folders were added
   to the established exclusion list; Debug/Release solution and identity builds then passed.
3. The live attribute adapter initially covered an explicit `ModifyAttributes` false result only.
   It was hardened to return a controlled failure if a panel disappears after preflight, catch
   unexpected commit exceptions, restore every already-modified panel in reverse order, and report
   `ROLLBACK_FAILED` if restoration is incomplete.

## Test Record

### Focused create-command smoke

Commands:

```powershell
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Debug
dotnet run --project .\Project_Test\260818_TEST_panel-cladding-create-command\PanelCladdingCreateSmoke.csproj -c Release
```

Both final runs exited `0` and reported:

```text
[OK] H/V guides create canonical bottom-up and left-right panel cells.
[OK] Remote, exterior, diagonal, and duplicate guides are handled deterministically.
[OK] Existing grid/type/signature metadata resets while unrelated panel metadata is preserved.
[OK] PanelcladdingCreate exposes the required panel-first, curve-second Rhino command flow.
```

The smoke asserts one H guide at offset `20` and one V guide at offset `40` create exactly:

- `CW_2.03_OFFSET_H0=20`
- `CW_2.04_OFFSET_V0=40`
- cells `0A`, `0B`, `1A`, and `1B`, each with a single-space value

It also asserts duplicate collapse, remote-depth/outside filtering, diagonal warnings, multi-panel
planning, duplicate panel selection collapse, fail-closed empty/non-applicable selections, exact
command naming, panel-first prompt order, two non-empty selection phases, unique command GUIDs, live
service signature, and absence of MCP/hosting dependencies.

### Existing panel-cladding regressions

The following Debug projects exited `0` after integration:

- standalone PanelCladdingEditor smoke;
- PanelCladdingMatch smoke;
- PanelCladdingSpawn smoke;
- PanelCladdingClear smoke;
- surface-sync smoke;
- offset-sync smoke; and
- same-day cell-topology smoke.

The offset-sync native Brep inference remained its expected skip because it requires a running Rhino
native test host. All managed offset/key assertions passed.

### Production and solution builds

The direct PanelCladdingEditor Debug and Release builds each exited `0` with zero warnings/errors and
produced `PanelCladdingEditor.rhp`. `MCP_Rhino.sln` also built in Debug and Release with
`-m:1 /nodeReuse:false`, zero warnings, and zero errors. Direct panel builds were repeated afterward
because the solution's test-host build intentionally also emits a DLL.

### Plug-in identity

The established assembly identity verifier passed in Debug and Release:

```text
PanelCladdingEditor|pluginId=7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35
rhino-plugin-assembly-identity|products=2|distinctPluginIds=2
```

The packaged Release RHP was checked again and reports the same non-empty, manifest-matching,
assembly-level plug-in GUID.

### Package and installation

- Rhino process check before installation: no Rhino process running.
- Bundle build: PASS, `PanelCladdingEditor-1.0.32`.
- Supported registry-only install: PASS.
- Supported installed-state validation: PASS.
- Registry load mode: `1` (startup).
- Directory install: `0` (registry-only).
- Registry RHP path:
  `%LOCALAPPDATA%/PanelCladdingEditor/plugin/1.0.32/PanelCladdingEditor.rhp`.
- Bundle/installed RHP SHA-256:
  `9537FD13F81931BB0672235E52ED75EE187D2B21C0891106A82261F497F6BF82`.

## Acceptance Criteria Alignment

- Exact `PanelcladdingCreate` Rhino command with a unique non-empty GUID: passed.
- Panel selection before curve selection and mutation-free cancellation: passed source/contract
  assertions.
- H/V guide classification and canonical ordering: passed.
- Duplicate, remote, outside, and diagonal guide behavior: passed.
- No-applicable-guide fail-closed behavior: passed.
- Cell count/key/value contract: passed.
- Generated grid/type/signature reset with unrelated metadata preservation: passed.
- All-panel preflight, one undo record, and rollback path: implemented and compiled.
- Focused tests, relevant regressions, solution builds, direct RHP builds, and identity checks: passed.
- Package/install/registry/hash validation: passed.

## Rollback Verification

The focused tests use pure snapshots and do not mutate a Rhino document. The live adapter retains
each original `ObjectAttributes` instance before commit and restores already-written panels in
reverse order on failure. In a live document, one Rhino Undo restores the complete successful
command transaction. The versioned installed RHP remains managed by the supported installer.

## Current Remaining Items

- Restart Rhino so it loads the installed `1.0.32` RHP and registers the new command.
- Run `PanelcladdingCreate` on representative project geometry for interactive acceptance. No Rhino
  UI automation or production document mutation was performed during this execution.

## Conclusion

`PanelcladdingCreate` is implemented, regression-tested, identity-validated, packaged, and installed.
It prompts for panels first and guide curves second, derives panel-local H/V offsets, recreates and
renumbers canonical blank cells, preserves unrelated metadata, and applies all selected panels as
one undoable transaction.
