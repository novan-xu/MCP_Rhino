# 260423_EXET_geometry-edit-curve-composite

## Corresponding Plan

- Plan: `Project_Plan/260423_PLAN_geometry-edit-curve-composite.md`
- Execution date: 2026-04-26

## Related Artifacts

- Test folder: `Project_Test/260423_TEST_geometry-edit-curve-composite/`
- Commit / PR: not created in this session

## Result / Landed Scope

- Added curve edit request/response contracts:
  - `PreviewEditCurveGeometryRequest`
  - `ApplyEditCurveGeometryRequest`
  - `GeometryEditPreviewResponse`
  - `GeometryEditApplyResponse`
- Added curve edit domain models/enums:
  - `GeometryEditStrategyKind`
  - `GeometryEditOperationKind`
  - `CurveEditSpec`
  - `EditablePointInput`
  - `ReconstructedCurveSummary`
- Added the curve edit use case service:
  - `CurveGeometryEditService`
- Added live interfaces and implementations:
  - `IGeometryEditValidator` / `LiveGeometryEditValidator`
  - `IGeometryReconstructor` / `LiveGeometryReconstructor`
  - `IGeometryMutationService` / `LiveGeometryMutationService`
- Added MCP tools:
  - `PreviewEditCurveGeometryTool`
  - `ApplyEditCurveGeometryTool`
- Registered new services in live DI and CLI fallback live DI.
- Added dedicated smoke entry points:
  - CLI slug: `geometry-edit-curve-composite-smoke-test`
  - Rhino command: `_McpGeometryEditCurveCompositeSmoke`

## Deviations From Plan

- Added `CurveGeometryEditService` even though it was not listed explicitly in the plan. This keeps orchestration out of the Tool layer and matches the repository architecture guide.
- Live smoke creates temporary Line/Polyline/Nurbs curves instead of relying on fixture object taxonomy. It deletes those objects before exit and verifies object count returns to the starting value.
- `LiveGeometryMutationService` implements best-effort rollback on metadata replay failure by replacing the original duplicated geometry and replaying the original metadata snapshot inside the same undo scope. RhinoCommon does not expose a cancel API in this project path.
- Reconstructor preserves NurbsCurve degree/knots/weights by duplicating the original curve and setting control point locations in place. No `PARAMETERIZATION_CHANGED` warning is emitted for this path because parameterization is intentionally retained.

## Issues Found And Fixed During Execution

- `DeveloperCommandHandler` already centralizes `RegisterExtensionHandlers()`, so this capability follows the same extension-hook pattern introduced by sub PLAN A: `RegisterGeometryEditCurveCompositeHandlers()`.
- The curve edit Tool flow needed an Application service to avoid duplicating preview/apply preparation logic in two Tool classes.

## Test Record

Build:

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo
```

Result: exit code 0.

CLI fallback smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-curve-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Result: exit code 0.

Key output:

```text
Geometry edit curve composite smoke test completed successfully (CLI fallback mode).
- PreviewEditCurveGeometry rejected in CLI fallback
- ApplyEditCurveGeometry rejected in CLI fallback
```

Foundation regression smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-molecular-foundation-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Result: exit code 0.

Live Rhino smoke:

```text
_McpGeometryEditCurveCompositeSmoke
```

Result: command added but not executed in this non-Rhino CLI session.

## Acceptance Alignment

- Build passes.
- CLI fallback returns explicit `LIVE_RHINO_REQUIRED` for Preview and Apply.
- Preview path uses live read access and does not call mutation APIs.
- Apply path uses live `ExecuteWithUndo`, `doc.Objects.Replace`, metadata replay, and redraw.
- Apply response preserves the target `ObjectId` and reports `UndoRecordName = "MCP:EditCurveGeometry"`.
- DirectOverride validation covers operation kind, point count, point index order, and invalid numeric coordinates.

## Rollback Verification

The implementation is additive except DI and CLI registration. To roll back sub PLAN B, remove the new curve composite files and revert the small connection changes in:

- `DependencyInjection.cs`
- `DeveloperCommandHandler.cs`

Sub PLAN A files are not required to be reverted for B rollback.

## Remaining Items

- Run `_McpGeometryEditCurveCompositeSmoke` inside Rhino against `Runtime_Test/MCP_rhino_test.3dm`.
- Exercise Rhino Ctrl+Z manually after Apply to verify geometry and metadata return together in one undo step.
- Add stricter live assertions if real fixture-specific LineCurve/PolylineCurve/NurbsCurve objects are later standardized.

## Conclusion

Sub PLAN B is implemented as a buildable live-first curve edit composite layer with DirectOverride preview/apply, curve reconstruction, metadata-preserving mutation, DI registration, CLI smoke, and Rhino smoke command.
