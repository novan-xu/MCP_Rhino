# Panel Cladding Clear Plan

## Background

Configured panel Breps retain per-cell cladding values, a cladding type, and a signature. Users need
a Rhino command that clears this assigned cladding configuration without opening the editor or
removing panel offsets and identity metadata.

## Goal

Add `_PanelCladdingClear` to the standalone PanelCladdingEditor plug-in. The command accepts one or
multiple panel Breps and removes their assigned cladding cell values, cladding type, and signature.

## Architecture ownership

- `UI/`: Rhino selection and command feedback.
- `Application/Services/PanelCladding/`: pure key classification and clear-plan construction.
- `Infrastructure/Rhino/Live/PanelCladding/`: live document validation, one Undo record, attribute
  commits, rollback, and redraw.
- `Domain/` and `Application/Interfaces/`: command-neutral clear plan/result contracts.

## Key design

- Select one or more owning Brep objects; subobject selection is disabled and group selection is
  supported.
- Delete only cell keys matching `CW_X.XX_CLADDING_<column><row>`, where the cell suffix is numeric
  column plus alphabetic row.
- Also delete `CW_1.10_CLADDING_TYPE`, `Signature`, and the legacy
  `CW_4.00_CLADDING_TYPE` / `CW_4.00_CLADDING_SIGNATURE` keys so stale assignments cannot survive.
- Preserve all `CW_X.XX_OFFSET_...` keys, including the current H/V offsets, as well as PID, CID,
  release, wall type, layer, object name, geometry, and unrelated user text.
- Preflight every selected object before mutation. Apply all actual changes in one Rhino Undo record
  named `Clear Panel Cladding`; skip panels that have nothing to clear.
- Restore the original attributes of earlier modified panels if a later commit fails.

## Files involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingKeyService.cs`
- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingClearPlanningService.cs`
- `src/PanelCladdingEditor/Application/Interfaces/IPanelCladdingServices.cs`
- `src/PanelCladdingEditor/Domain/PanelCladdingModels.cs`
- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingClearService.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingClearCommand.cs`
- `src/PanelCladdingEditor/UI/PanelCladdingEditorSmokeCommand.cs`
- `Project_Test/260807_TEST_panel-cladding-clear/`
- `Project_Exet/260807_EXET_panel-cladding-clear.md`

## Usage

Run `_PanelCladdingClear`, select one or more panel Breps, and press Enter. Rhino reports the number
of panels changed and cladding keys removed. Use Rhino Undo once to restore the batch.

## Acceptance criteria

- The command is discoverable as `_PanelCladdingClear` and has a unique command GUID.
- Multiple selected panel Breps are supported.
- Canonical and legacy cell/type/signature assignments are deleted case-insensitively.
- Offset, identity, and unrelated keys are preserved exactly.
- Duplicate/empty selections are handled deterministically.
- Invalid selected objects fail before any panel is changed.
- One command execution creates at most one Undo record and redraws after successful mutation.
- Partial attribute-commit failure restores prior panels.
- Dedicated smoke tests and the existing standalone, match, and spawn regressions pass in Debug and
  Release; the solution builds cleanly in both configurations.
- Before packaging or installation, the RHP assembly GUID is verified directly against the manifest.

## Risks and rollback

- A broad cladding-key matcher could remove company metadata. The matcher is anchored to the exact
  `CW_X.XX_CLADDING_<numeric><alpha>` cell shape and explicit type/signature names.
- Attribute failure mid-batch could leave mixed state. Preflight plus original-attribute rollback
  bounds that risk, and one Rhino Undo record provides user recovery after success.
- Code rollback is isolated to the clear command/service/contracts, key classifier, smoke coverage,
  package version, and matching PLAN/TEST/EXET artifacts.

## Future extensions

- Add an optional separate command to clear offsets as well if that becomes a distinct user need.
- Add a command-line option for clearing only type/signature while retaining cell values.
