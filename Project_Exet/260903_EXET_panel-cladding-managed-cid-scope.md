# Panel Cladding Managed CID Scope Execution

## 对应计划

- Plan: `Project_Plan/260903_PLAN_panel-cladding-managed-cid-scope.md`
- Execution date: 2026-09-03

## 关联产物

- Tests: `Project_Test/260903_TEST_panel-cladding-managed-cid-scope/`
- Commit / PR: not created in this execution

## 执行结果 / 实际落地范围

- Added segment-aware managed layer predicates for `04_STEP Surfaces` and `02_CW Extrusions`.
- Changed spawn CID preflight so `FindByUserString` matches block only when the matching object has
  the requested geometry class and belongs to that scope's managed root.
- Preserved in-batch CID duplicate protection.
- Changed live sync discovery to use the same managed roots. Surfaces under the legacy
  `03_Material Surfaces (STEP)` root and geometry on arbitrary detail layers are no longer live sync
  candidates.
- Kept the broader legacy layer parsing helper available for historical planning/test data; the live
  repository no longer feeds legacy-layer objects into the sync workflow.

## 与计划的偏差

None.

## 施工中发现并修复的问题

- A CID-only document query was the source of the reported false conflict. The query remains useful
  and efficient, but its results are now filtered before the conflict error is raised.
- Sync previously used a separate layer-root rule. Both spawn and sync now share application-layer
  predicates, preventing their ownership boundaries from drifting again.

## 测试记录

Full commands are recorded in
`Project_Test/260903_TEST_panel-cladding-managed-cid-scope/RESULTS.md`.

- Focused Debug and Release smoke runs: exit `0`.
- Debug and Release `PanelCladdingEditor` builds: exit `0`, 0 warnings, 0 errors.
- Six affected historical regression smoke runs: exit `0`.
- `git diff --check`: exit `0` with line-ending warnings only.

## 验收判据对齐

- Same CID outside `04_STEP Surfaces` does not satisfy the surface conflict predicate: passed.
- Same CID outside `02_CW Extrusions` does not satisfy the curve conflict predicate: passed.
- Same-scope managed geometry remains protected: passed.
- Similar root prefixes do not leak through the boundary: passed.
- Live sync repository excludes legacy/unrelated surface layers and unrelated curve layers: passed.
- Build and existing behavior regressions: passed.

## 回退验证

No Rhino document mutation or installation was performed during construction. Reverting the three
production files restores the former document-wide CID conflict and legacy sync discovery.

## 当前遗留项

None for the requested ownership rule. A live Rhino fixture can still be used for end-user workflow
confirmation, but no native geometry algorithm changed in this follow-up.

## 结论

CID uniqueness and synchronization now apply only to panel-cladding-managed output geometry. Detail
models and tracking copies on other layer trees may reuse the canonical CID without blocking spawn
or participating in sync.

## Deployment verification (2026-09-03)

After Rhino was closed, `PanelCladdingEditor` 1.0.69 was rebuilt and installed registry-only at
`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.69`. The required installer
`-Mode Validate` gate passed. The installed RHP SHA-256 exactly matched the fresh bundle
(`28cb5e0adf66af5300a6b3fd885fd216771c91044fc53cf2dfd3922307de9e01`), and direct assembly
metadata inspection confirmed GUID `7C1A4D3B-5E29-4F68-9A72-1D8C6B0F4E35`.
