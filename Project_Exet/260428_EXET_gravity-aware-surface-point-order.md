# 260428_EXET_gravity-aware-surface-point-order

## Corresponding Plan

- Plan: `Project_Plan/260428_PLAN_gravity-aware-surface-point-order.md`
- Execution date: 2026-04-28

## Related Artifacts

- Test folder: `Project_Test/260428_TEST_gravity-aware-surface-point-order/`
- Commit / PR: not created in this execution.

## Execution Result / Actual Scope

Implemented the gravity-aware point-order rebuild correction for existing surface rebuild tools.

Actual code changes:

- `LiveSurfaceLocalCoordinateSystemBuilder` now constructs a surface-local gravity frame from:
  - front-face normal;
  - world gravity `(0, 0, -1)`;
  - gravity projected into the surface plane;
  - local right/up axes derived from front normal and projected gravity;
  - boundary centroid as origin.
- `LiveBoundaryReferenceCurveAnalyzer` now:
  - extracts front normals for direct surfaces and single-face Breps;
  - builds a local gravity frame before candidate ranking;
  - ranks lower-left candidates using local projected coordinates instead of world `Y/Z`;
  - orients the suggested reference curve from lower-left to the next clockwise point;
  - returns clear degeneracy failures when gravity projection is undefined.
- Domain models now carry the frame and candidate vertex metadata needed to audit the decision.
- Existing rebuild tools keep their public names and live-only behavior.
- Existing surface point-order smoke fixture was adjusted away from horizontal `WorldXY`, because horizontal faces are now intentionally degenerate for this workflow.
- Added new dedicated smoke artifact and Rhino command:
  - `gravity-aware-surface-point-order-smoke-test`
  - `McpGravityAwareSurfacePointOrderSmoke`

## Deviations From Plan

- No standalone local-coordinate inspection MCP tool was added, matching the plan non-goal.
- The builder interface was changed to `Build(SurfaceBoundaryLoop boundaryLoop)` rather than `Build(SurfaceRebuildDescriptor descriptor)`. This kept the change narrower while still removing the old dependency on the selected reference edge.
- Live Rhino smoke was added but not executed in this terminal session because it requires the Rhino plugin host to load the newly built assembly.

## Issues Found And Fixed During Execution

- Existing smoke setup used a horizontal `Plane.WorldXY` surface. Under the corrected gravity-projection rules, that target has no valid projected gravity vector. The fixture was changed to vertical geometry so it still tests executable rebuild behavior.
- The reference analyzer previously selected candidate edges before any local frame existed. This was fixed by injecting and using the local coordinate builder inside the analyzer before candidate ranking.

## Test Record

Commands run:

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj
```

Result:

- Exit code: 0
- Build succeeded
- Warnings: 0
- Errors: 0

```powershell
dotnet run --project src/MCP_Rhino.Server -- gravity-aware-surface-point-order-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

Result:

- Exit code: 0
- CLI fallback mode passed
- Verified `PreviewRedefineSurfacePointOrder` rejects without live Rhino using `LIVE_RHINO_REQUIRED`

```powershell
dotnet run --project src/MCP_Rhino.Server -- surface-point-order-rebuild-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

Result:

- Exit code: 0
- CLI fallback mode passed
- Verified inspect / preview / apply live-only guards

```powershell
dotnet build MCP_Rhino.sln
```

Result:

- Exit code: 0
- Solution build succeeded
- Warnings: 0
- Errors: 0

```powershell
dotnet test MCP_Rhino.sln --no-build
```

Result:

- Exit code: 0
- No test output was emitted in this repository configuration.

Live smoke to run in Rhino after loading the updated plugin:

```text
_McpGravityAwareSurfacePointOrderSmoke
```

Expected live smoke checkpoints:

- opposite-front vertical panels select the expected lower-left start;
- side-facing and sloped panels select clockwise local order;
- horizontal gravity-parallel surface reports `SURFACE_GRAVITY_PROJECTION_DEGENERATE`;
- apply executes only non-degenerate targets and keeps the normal point-order undo record;
- temporary objects are cleaned up.

## Acceptance Criteria Alignment

- World `Y/Z` scoring was removed from automatic reference selection.
- Lower-left is selected from projected local coordinates.
- Clockwise ordering remains handled by the existing point orderer using the new local right/up frame.
- Gravity-parallel surfaces now return a clear degeneracy error instead of falling back to world coordinates.
- Existing MCP tool names and live-only execution boundaries were preserved.
- Apply still reports `MCP:RedefineSurfacePointOrder`.
- Build and CLI fallback smoke checks pass.

## Rollback Verification

Rollback path:

- revert the modified files listed in this EXET;
- remove `Project_Test/260428_TEST_gravity-aware-surface-point-order/`;
- remove `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGravityAwareSurfacePointOrderSmokeCommand.cs`;
- remove this EXET and the corresponding PLAN if abandoning the capability.

No destructive migration or file-format change was introduced.

## Current Remaining Items

- Run `_McpGravityAwareSurfacePointOrderSmoke` inside Rhino after deploying/reloading the updated plugin assembly.
- Re-run a real default-layer panel rebuild on `MCP_rhino_test.3dm` only after the updated plugin is loaded, since an already-loaded Rhino MCP server may still be running older code.

## Conclusion

The code path has been changed from world-coordinate reference selection to surface-local gravity-aware selection, with compile and CLI fallback verification complete. Live verification is prepared and should be run in Rhino with the updated plugin assembly.

