# Panel Cladding Create Command

## Background

The project already has a Grasshopper Python implementation at
`WT01_panel_mullion_attributes.py` that derives horizontal and vertical panel-divider offsets from
selected guide curves, clears stale panel grid attributes, and creates intentionally blank cladding
cell keys. That workflow currently depends on a Grasshopper Script component and referenced GH Brep
inputs. The standalone PanelCladdingEditor plug-in needs the same goal exposed as a native Rhino
command.

The requested command name is `PanelcladdingCreate`. It must first prompt for panel surfaces and then
prompt for the curves used to derive H/V offsets. No mutation may occur until both selections and
all per-panel calculations succeed.

## Goal

Add the native Rhino command `_PanelcladdingCreate`. The command will accept one or more live panel
Brep/surface objects, accept one or more live guide curves, classify panel-local horizontal and
vertical guides, write canonical H/V offsets, recreate the corresponding blank cladding cell keys,
and remove stale grid/type/signature attributes in one Rhino Undo record.

## Architecture Ownership

- `UI/PanelCladdingCreateCommand.cs`: thin two-stage Rhino selection and result-reporting adapter.
- `Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs`: deterministic,
  RhinoCommon-free guide classification, offset clustering, key reset/write planning, and validation.
- `Application/Interfaces/IPanelCladdingServices.cs`: live create-service contract.
- `Domain/PanelCladdingModels.cs`: create snapshots, guide samples, plans, warnings, and result DTOs.
- `Infrastructure/Rhino/Live/PanelCladding/`: live object resolution, panel-frame/curve sampling,
  all-panel preflight, and one-record attribute commit.
- `Project_Test/260818_TEST_panel-cladding-create-command/`: focused planning, command-surface, key,
  GUID, and regression evidence.
- `Packaging/PanelCladdingEditor/`: versioned release staging/current-user repair if Rhino is not
  holding the installed payload.

## Key Design

1. `PanelcladdingCreate` uses two separate `GetObject` phases:
   - panel prompt: one or more Brep/surface objects;
   - guide prompt: one or more curve objects.
   Cancelling either phase returns `Cancel` and performs no mutation.
2. The live adapter resolves only the selected ids in the saved active document. It builds each
   panel's gravity-oriented local frame from the stored `Plane` geometry user string when valid,
   otherwise from a planar face or best-fit mesh plane.
3. Curves are sampled in every panel-local frame. Guides substantially vertical in that frame create
   V offsets; guides substantially horizontal create H offsets. Remote-depth, exterior, or
   non-spanning curves are ignored; diagonal curves produce warnings. Near-coincident guides are
   clustered into one stable offset.
4. Every selected panel must receive at least one applicable H or V guide. This prevents a mistaken
   remote-curve selection from silently resetting a panel to a 1x1 grid.
5. Application planning validates offsets with `PanelCladdingKeyService`, uses the repository's
   five-decimal canonical offset format, and creates cells bottom-to-top then left-to-right.
6. Each planned panel removes existing canonical `CW_X.XX_OFFSET_*`, `CW_X.XX_CLADDING_*`, and
   current/legacy signature keys, then writes:
   - `CW_2.03_OFFSET_H0...` bottom-to-top;
   - `CW_2.04_OFFSET_V0...` left-to-right;
   - `CW_4.<column>_CLADDING_<column><row>` with one space as the intentional blank value.
   Unrelated PID, release, wall-type, CID, layer, name, and custom attributes are preserved.
7. All panels are calculated and attributes duplicated before mutation. The live adapter opens one
   `Create Panel Cladding Grid` undo record, commits every changed panel, restores earlier attributes
   if a later modify fails, and redraws once.
8. The command remains part of the existing assembly-scanned Rhino command surface; no MCP tool,
   Router endpoint, alternate transport, or Grasshopper runtime dependency is introduced.

## Files Involved

- `src/PanelCladdingEditor/UI/PanelCladdingCreateCommand.cs` (new)
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs` (new)
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingCreateService.cs` (new)
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingGeometryPartitionService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs`
- `Project_Test/260818_TEST_panel-cladding-create-command/` (new)
- focused packaging files only if a safe versioned install is completed
- `Project_Exet/260818_EXET_panel-cladding-create-command.md` (after verified execution)

## Usage

1. Run `_PanelcladdingCreate` in a saved active Rhino document.
2. At the first prompt, select one or more panel surfaces/Breps and press Enter.
3. At the second prompt, select the horizontal and vertical guide curves and press Enter.
4. The command reports panels updated, H/V offsets created, cells initialized, and any skipped
   diagonal guides. Use Rhino Undo once to restore the prior attributes for the complete command.

## Acceptance Criteria

- The plug-in registers a non-empty, uniquely GUIDed Rhino command whose exact English name is
  `PanelcladdingCreate`.
- The panel selection occurs before the guide-curve selection and cancellation is mutation-free.
- Horizontal guides create ordered H keys; vertical guides create ordered V keys in panel-local
  coordinates.
- Duplicate guides collapse; remote, outside, and diagonal guides do not create offsets.
- Panels with no applicable selected guide fail before any panel is modified.
- The created cell count equals `(H count + 1) * (V count + 1)` and labels follow the existing
  bottom-to-top/left-to-right key contract.
- Stale offset/cladding/type/signature keys are removed while unrelated panel metadata is preserved.
- Multiple selected panels commit within one Rhino Undo record after all-panel validation.
- Focused tests, existing PanelCladdingEditor regressions, Debug/Release production builds, and
  Debug/Release plug-in assembly identity validation pass.
- If installation is safe, a uniquely versioned current-user package is built, repaired, and
  validated without creating a Rhino Package Manager duplicate.

## Risks And Rollback

- Guide classification is panel-frame dependent. Stored `Plane` data remains the preferred
  orientation authority, followed by the same gravity-oriented planar/best-fit behavior used by the
  existing panel geometry services.
- Applying the command intentionally replaces the selected panels' prior grid and cladding/type
  attributes. The two-stage selection, all-panel preflight, and one Rhino Undo record are the
  rollback controls.
- The working tree already contains active PanelCladdingEditor work. This capability will preserve
  those changes and only modify the focused files listed above.
- Tests use pure planning inputs or stub/source inspection unless a Rhino native host is explicitly
  available; they do not mutate a production document.
- If Rhino has loaded the installed RHP, packaging may be built but installation will be deferred
  rather than forcing replacement of an in-use plug-in.

## Future Extensions

- Add an explicit preview/highlight phase showing accepted H/V curves per panel before apply.
- Share the same create plan with an MCP preview/apply tool if a future routed automation surface is
  requested.
- Persist stable logical-cell identities independently of row/column display labels.
