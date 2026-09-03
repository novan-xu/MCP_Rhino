# PCUpdate Command Undo Fix Plan

## 背景

`PCUpdate` is implemented as a normal Rhino command. Rhino opens the command's Undo record before
`RunCommand` performs document mutations, but `LivePanelCladdingUpdateService.Apply` unconditionally
calls `RhinoDoc.BeginUndoRecord`. Rhino rejects that nested request with serial number `0`, and the
command reports `PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE` before changing the document.

## 目标

- Allow `PCUpdate` to execute inside Rhino's command-owned Undo record.
- Preserve one Undo entry for a successful batch update.
- Preserve the service's ability to own an Undo record when no ambient record exists.
- Keep automatic `RhinoDoc.Undo()` rollback limited to records created and closed by the service;
  never close or undo an ambient command-owned record from inside the service.
- Package the corrected plug-in as the next patch version without activating production registry
  state from the agent execution context.

## 架构归属

- `Infrastructure/Rhino/Live/PanelCladding`: Rhino-specific ambient/owned Undo record handling.
- `Project_Test/260903_TEST_pcupdate-command-undo`: focused regression contract.
- `Packaging/PanelCladdingEditor`: patch-versioned build artifact and package validation.

No MCP tool, resource, Router, or server surface changes are required.

## 关键设计

1. Read `RhinoDoc.CurrentUndoRecordSerialNumber` before attempting to open an Undo record.
2. If the serial number is nonzero, treat it as an ambient Rhino command record and perform the
   entire reconciliation inside it.
3. If no record is active, call `BeginUndoRecord` and mark the resulting record as service-owned.
4. Call `EndUndoRecord` and automatic `Undo()` rollback only for a service-owned record. Ambient
   records remain exclusively owned by Rhino's command lifecycle.
5. Retain `PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE` only for the real no-record case where the service
   must create a record and Rhino refuses, such as disabled Undo recording.

## 涉及文件

- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingUpdateService.cs`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Packaging/PanelCladdingEditor/README.md`
- `Project_Test/260903_TEST_pcupdate-command-undo/`
- `Project_Exet/260903_EXET_pcupdate-command-undo.md`

The existing `PCUpdate` implementation and its uncommitted construction artifacts are preserved;
this fix changes only the overlapping Undo/package contract needed for the reported defect.

## 使用方式

Select one or more saved panel Breps and run `_PCUpdate`. The command should reconcile managed
surface and curve dependencies and create one normal Rhino Undo entry without printing
`PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE`.

## 验收标准

- The update service reuses a nonzero `CurrentUndoRecordSerialNumber` and does not call
  `BeginUndoRecord` in that branch.
- Only a service-owned record is ended or automatically undone.
- A refused service-owned record still fails before mutation with
  `PANEL_CLADDING_UPDATE_UNDO_UNAVAILABLE`.
- Focused Debug and Release regression smokes pass.
- PanelCladdingEditor Debug and Release builds pass with zero warnings/errors.
- Existing `PCUpdate` and affected panel-cladding regressions pass.
- The next package version builds and passes registration/assembly identity validation.
- `git diff --check` reports no errors attributable to this change.

## 风险与回退方案

- Risk: ending or undoing Rhino's ambient command record could corrupt command Undo ownership.
  Mitigation: both operations are guarded by an explicit service-ownership flag.
- Risk: a partial failure under an ambient command record cannot be synchronously rolled back by
  the service while that record is active. Mitigation: all geometry is prepared before mutation;
  Rhino retains the command-owned Undo entry for one-step user rollback, and the service does not
  manipulate an in-progress record it does not own.
- Rollback: restore the prior update-service logic and package version. No document, registry, or
  installed plug-in mutation is part of this construction execution.

## 后续扩展方向

- Add a dedicated native Rhino fixture that invokes `_PCUpdate`, verifies retained/missing/stale/
  duplicate dependency reconciliation, and confirms one-step Undo in a real command lifecycle.
- Audit the other standalone `PC*` commands for the same unconditional nested Undo pattern under a
  separately confirmed construction task.
