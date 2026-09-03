# PCUpdate Locked Managed Objects Plan

## 背景

After the command-owned Undo fix, `PCUpdate` reached reconciliation but failed with
`PANEL_CLADDING_UPDATE_REPLACE_FAILED` when an existing managed dependency was on a locked layer.
The update service deliberately discovers normal, locked, and hidden managed Breps/curves, but its
typed `ObjectTable.Replace(Guid, Brep/Curve)` calls use Rhino's default mode protection. Rhino
therefore refuses retained-object replacement when the object is locked, hidden, or belongs to a
locked/hidden layer. Stale/duplicate deletion uses the same mode-sensitive default.

## 目标

- Allow `PCUpdate` to reconcile managed dependencies regardless of object-level locked/hidden mode
  or locked/hidden managed-layer state.
- Preserve object-level locked/hidden mode on retained dependencies after geometry and attributes
  are updated.
- Delete stale or duplicate managed dependencies even when their object/layer mode is protected.
- Keep the existing PID/CID/kind/managed-root scope and one-command Undo boundary unchanged.
- Ship the correction as the next standalone plug-in patch package without changing production
  registry state from the agent context.

## 架构归属

- `Infrastructure/Rhino/Live/PanelCladding`: Rhino mode-aware replace/delete implementation.
- `Project_Test/260903_TEST_pcupdate-locked-managed-objects`: focused API/source/package regression.
- `Packaging/PanelCladdingEditor`: package version and user-facing behavior note.

No MCP tool, resource, Router, command-registration, or public service contract changes are needed.

## 关键设计

1. Resolve the retained managed `RhinoObject` before replacement and copy its
   `ObjectAttributes.Mode` into the authoritative replacement attributes.
2. Replace through the `ObjectTable.Replace(Guid, GeometryBase, bool ignoreModes)` overload with
   `ignoreModes: true`. This covers Brep and Curve dependencies and explicitly bypasses
   object/layer locked and hidden mutation gates.
3. Delete stale/duplicate objects through `ObjectTable.Delete(RhinoObject, quiet, ignoreModes)` with
   `ignoreModes: true`.
4. Do not unlock/show objects or layers as a workaround. The mutation bypass is operation-scoped,
   retained object modes are restored in the same Undo record, and layer state is never changed.
5. Continue excluding reference objects and anything outside the existing managed-root,
   selected-PID, nonblank-CID scope.

## 涉及文件

- `src/PanelCladdingEditor/Infrastructure/Rhino/Live/PanelCladding/LivePanelCladdingUpdateService.cs`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Packaging/PanelCladdingEditor/README.md`
- current package-version assertions in existing `PCUpdate` regressions
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` for one precise standalone-test exclusion
- `Project_Test/260903_TEST_pcupdate-locked-managed-objects/`
- `Project_Exet/260903_EXET_pcupdate-locked-managed-objects.md`

## 使用方式

Select source panel Breps and run `_PCUpdate` normally. Existing managed surfaces and curves may be
object-locked, object-hidden, or placed on locked/hidden managed layers; the command reconciles them
without requiring the user to change those states first.

## 验收标准

- Retained dependencies use `Replace(..., ignoreModes: true)`.
- Stale/duplicate dependencies use `Delete(..., ignoreModes: true)`.
- Retained object `Mode` is copied to the rebuilt authoritative attributes.
- The service does not unlock/show managed objects or modify layer lock/visibility state.
- Focused Debug and Release regressions pass.
- Existing PCUpdate/Undo and PC command regressions pass.
- PanelCladdingEditor and solution Debug/Release builds pass with zero warnings/errors.
- The next package builds; isolated registration, assembly GUID uniqueness, and RHP hash checks pass.
- `git diff --check` reports no errors attributable to this change.

## 风险与回退方案

- Risk: bypassing modes too broadly could mutate unrelated protected geometry. Mitigation: mode
  bypass is used only after the existing reconciliation plan has constrained objects by managed
  root, geometry kind, selected PID, and canonical nonblank CID.
- Risk: replacement attributes could normalize an object's locked/hidden mode. Mitigation: capture
  and reapply the original `ObjectAttributes.Mode` before `ModifyAttributes`.
- Risk: a hidden/locked layer could be unintentionally changed. Mitigation: no layer lock or
  visibility property is edited.
- Rollback: revert the two mode-aware mutation calls, retained-mode copy, regression/exclusion,
  README note, and package version. No production activation is part of this execution.

## 后续扩展方向

- Add a native Rhino fixture covering object-locked, object-hidden, locked-layer, and hidden-layer
  retained/stale dependencies plus one-step Undo.
- Audit other standalone `PC*` mutation commands for mode-sensitive RhinoCommon overloads under a
  separately confirmed construction task.
