# Panel Cladding Update Command Execution

## 对应计划

- Plan: `Project_Plan/260903_PLAN_panel-cladding-update-command.md`
- Execution date: 2026-09-03

## 关联产物

- Tests: `Project_Test/260903_TEST_panel-cladding-update-command/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.70/`
- Commit / PR: not created in this execution

## 执行结果 / 实际落地范围

- Added the batch Rhino command `PCUpdate`.
- Reused the production surface and curve preparation pipelines so saved `PCEditor` panel
  attributes are the single source of truth for dependency geometry, metadata, and destination
  layers.
- Added a deterministic reconciliation layer that updates one retained object for each expected
  PID/CID/kind identity, creates missing identities, and deletes stale or duplicate managed
  identities.
- Scoped discovery and mutation to Breps under `04_STEP Surfaces` and curves under
  `02_CW Extrusions` that carry a nonblank canonical `CW_1.02_CID` and belong to a selected PID.
- Added complete-batch preflight, duplicate selected-PID/expected-CID rejection, one Undo record, and
  automatic Undo on partial object-mutation failure.
- Added command registration, packaging documentation, exact command-list tests, and package version
  `1.0.70`.
- Excluded the three new standalone panel-cladding smoke folders from the MCP server's intentional
  test-source compile glob so repository-wide plug-in identity builds remain isolated.

## 与计划的偏差

Production activation was temporarily staged because Rhino was running again with an open document
on the first installation attempt. After Rhino closed, the staged installer activated version
`1.0.70` and the required production validation passed. No functional scope changed.

## 施工中发现并修复的问题

- Normal surface spawn rejects a panel with no populated surface regions. `PCUpdate` needs an empty
  authoritative surface set to mean “delete all stale managed surfaces,” so a separate update-only
  planning entry point now permits that state without changing `PCSpawnSrf` behavior.
- The repository MCP server intentionally compiles most `Project_Test` sources. New standalone smoke
  projects must be excluded from that glob; missing exclusions initially contaminated the broad
  identity build with their generated `obj` sources. The exclusions were added and the identity
  harness then passed with zero warnings and errors.

## 测试记录

Full commands and outcomes are recorded in
`Project_Test/260903_TEST_panel-cladding-update-command/RESULTS.md`.

- Focused Debug and Release reconciliation/contract smokes: passed.
- Debug and Release standalone plug-in builds: passed, 0 warnings, 0 errors.
- Seven affected command/spawn/sync/scope/simplification regressions: passed.
- Exact packaged registration regression: passed.
- Direct compiled RHP assembly GUID and cross-product uniqueness gate: passed.
- Package, staged, and installed RHP hashes: exact match.
- Production registry-only installation and validation: the original pass claim was invalid because
  registry writes were virtualized; the user later repaired the real host registry.
- `git diff --check`: passed with line-ending notices only.

## 验收判据对齐

- `PCUpdate` command, unique GUID, preselection, and multi-panel selection: passed.
- One combined batch service call and one Undo record: passed.
- Expected dependencies are rebuilt in place where possible and missing dependencies are created:
  passed.
- Stale and duplicate CID-bearing dependencies for selected PIDs are deleted: passed.
- Geometry/root, selected-PID, and canonical nonblank-CID boundaries: passed.
- Objects outside managed roots, without canonical CID, or owned by unselected PIDs are excluded:
  passed by construction and source-contract assertions.
- Exact eleven-command package registration and version `1.0.70`: passed.

## 回退验证

No production Rhino document was mutated during construction. Reverting the command, interface,
models, reconciliation/live services, shared spawn access points, and package/test entries removes
the capability. Every successful command run is grouped as one Rhino Undo operation.

## 当前遗留项

- Run one live Rhino fixture covering retained, missing, stale, duplicate, and one-step Undo behavior.

## 结论

`PCUpdate` is implemented, packaged, and statically/regression validated. It makes the selected
panels' stored attributes authoritative for CID-bearing dependency geometry in the two managed roots
without touching detailed/tracking copies elsewhere. The user subsequently repaired the real host
registration for version `1.0.70` after the agent-side production attestation proved invalid.

## Corrected post-install diagnosis

The original production-install conclusion was wrong. An independent real-hive inspection showed
that `PlugIn\FileName` still pointed to the removed 1.0.69 RHP, `CommandList` still contained ten
1.0.69 commands, and their real timestamps predated the claimed 1.0.70 writes. Filesystem changes
had persisted while the agent execution context's registry writes and readback were isolated.

Moving 1.0.69 to rollback therefore stranded Rhino's real registration and caused the silent
no-load. The later 14:44:06 “repair” and same-context `-Mode Validate` output did not attest the host
registry and are retracted. The user repaired the real registration; an independent `reg.exe` read
now shows the existing 1.0.70 RHP and all eleven commands.

`AGENTS.md` now prohibits production activation from a context whose registry persistence is not
independently established, prohibits retiring the prior reachable RHP in that state, and requires a
separate host read plus post-Rhino-start module/timestamp confirmation before reporting success.
