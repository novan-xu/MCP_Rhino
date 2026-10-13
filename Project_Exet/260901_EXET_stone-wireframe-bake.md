# Stone Wireframe Bake Execution

## Corresponding Plan

- Plan: `Project_Plan/260901_PLAN_stone-wireframe-bake.md`
- Execution date: 2026-09-01

## Related Artifacts

- Test folder: `Project_Test/260901_TEST_stone-wireframe-bake/`
- Reusable script: `Scripts/stone_wireframe_bake.py`
- Installed package: `Packaging/MCP_Rhino/artifacts/MCP_Rhino-1.3.1/`
- Installed plug-in root: `%LOCALAPPDATA%\MCP_Rhino\plugin\1.3.1`

## Execution Result / Actual Scope

- Added `PreviewStoneWireframeBake` and `ApplyStoneWireframeBake` under `Tools/Blocks`.
- Added a fixed `StoneWireframeBakeSkill`, application service/interface, strict pure naming model, request
  and response contracts, and a live RhinoCommon operator.
- Recursively traversed assembly definition members while preserving complete nested transforms.
- Selected each stone definition's uppermost horizontal direct curve segment, canonicalized it left-to-right,
  and used the lower-X endpoint as the output block base point.
- Added idempotent creation/reuse of `WF_<source-name>` one-line definitions.
- Added idempotent output placement matched by definition, full transform, and `Assembly_ID`.
- Added the reusable Router-backed `Scripts/stone_wireframe_bake.py`; preview is the default and apply requires
  `--apply`.
- Added a dedicated CLI smoke slug and preview-only developer Rhino smoke command.
- Advanced and installed MCP_Rhino 1.3.1.
- Applied the requested bake in the live target document.

## Live Output

- qualifying assembly definitions: 22
- qualifying assembly instances: 570
- individual stone definitions: 36
- individual stone occurrences: 1,143
- `WF_...` definitions created: 36
- `WF_...` instances created: 1,143
- instances with exact parent `Assembly_ID`: 1,143
- preview/apply failures: 0
- second-run definitions or instances to create: 0

All source assemblies remained intact. Apply used one Rhino undo record and did not explode, delete, hide,
replace, or write the `.3dm` through a disk fallback.

## Deviations From Plan

- The script initially normalized the mapped `V:` path to its UNC target. The Router intentionally reports
  `RhinoDoc.Path` verbatim, so the client now preserves mapped-drive spelling with `os.path.abspath`.
- Live startup revealed an event-order race not seen in the 1.2.4 acceptance. Version 1.3.1 adds bounded idle
  reconciliation to the dispatcher and expands the startup regression accordingly.
- The first parallel Debug solution build hit an existing PanelCladdingEditor reference-output race. Sequential
  Debug and Release solution builds (`-m:1`) both passed with zero warnings and errors.

## Test Record

Successful validation included:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --no-restore --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --no-restore --nologo -m:1
& .\Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -Version 1.3.1
& .\Project_Test\260901_TEST_rhino-startup-discovery\StartupDiscoveryRegistrationSmoke.ps1 `
  -BundleRoot .\Packaging\MCP_Rhino\artifacts\MCP_Rhino-1.3.1
python .\Scripts\stone_wireframe_bake.py --document <target-live-3dm>
python .\Scripts\stone_wireframe_bake.py --document <target-live-3dm> --apply
```

Debug and Release CLI-host runs also passed:

- `stone-wireframe-bake-smoke-test`
- `mcp-tool-safety-annotations-smoke-test` (171 tools)
- `mcp-tool-overlap-cleanup-smoke-test` (16 Blocks tools)

Detailed observed results are in `Project_Test/260901_TEST_stone-wireframe-bake/RESULTS.md`.

## Problems Found And Fixed During Construction

- Existing tools could not inspect definition-member curves and nested transforms in one reliable workflow.
- The first reusable script path comparison broke mapped-drive identity by resolving `V:` to UNC.
- The startup dispatcher could miss the saved-document transition after loading against Rhino's temporary
  untitled document. Idle reconciliation now repairs only stale/unstarted active-document state and leaves
  stable routes untouched.
- Output responses would be too large if every created id were returned; ids are bounded to 200 with an
  explicit warning while aggregate counts remain exact.

## Acceptance Alignment

- Exact assembly and stone naming rules: passed.
- `_EL` / `_V` exclusion: passed.
- Flat and vertical upper-line selection and lower-X base point: passed for all 36 definitions.
- Full nested transform preservation: passed through one-to-one idempotency matching.
- `WF_...` naming and one-line definition compatibility: passed for all 36 definitions.
- `Assembly_ID` parent value: passed for all 1,143 instances.
- Non-destructive source handling and one undo record: passed.
- Second-run idempotency: passed with zero proposed changes.
- Startup route reliability and installed Router access: passed on 1.3.1.

## Rollback Verification

- The live apply is contained in Rhino's single `MCP: ApplyStoneWireframeBake` undo record.
- The installer retained the previous owned package in its normal rollback area.
- Incompatible existing `WF_` definitions cause apply to fail before mutation rather than being replaced.

## Current Remaining Items

- No capability or bake work remains.
- Rhino owns normal document saving; the completed live mutation leaves the active document modified until the
  user saves it.

## Conclusion

MCP_Rhino 1.3.1 is installed and startup-routable. The target live document contains 36 reusable one-line
wireframe block definitions and 1,143 correctly transformed, parent-attributed instances, with clean second-run
idempotency verification.
