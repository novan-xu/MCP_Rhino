# 260423_EXET_geometry-edit-derived-routing

## Corresponding Plan

- Plan: `Project_Plan/260423_PLAN_geometry-edit-derived-routing.md`
- Execution date: 2026-04-26

## Related Artifacts

- Test folder: `Project_Test/260423_TEST_geometry-edit-derived-routing/`
- Commit / PR: not created in this session

## Result / Landed Scope

- Extended curve edit requests and responses:
  - `ExpectedStrategy` on Preview/Apply requests
  - derived operation fields inside `CurveEditSpec`
  - `ResolvedPointIndices`, `DerivedOperationApplied`, and preview-only `StrategyResolutionTrace`
- Added derived routing domain models:
  - `DerivedPointOperationKind`
  - `PointSelectorKind`
  - `DerivedPointOperationParameters`
  - `PointSelectorSpec`
  - `StrategyResolutionTrace`
  - `DerivedOperationApplied`
- Added application interfaces and orchestration:
  - `IDerivedPointOperationEvaluator`
  - `IGeometryEditStrategyResolver`
  - `IGeometryTransformExecutionBridge`
  - `ICurveEditOrchestrator`
  - `CurveEditOrchestrator`
- Added live implementations:
  - `LiveDerivedPointOperationEvaluator`
  - `LiveGeometryEditStrategyResolver`
  - `LiveGeometryTransformExecutionBridge`
- Updated `PreviewEditCurveGeometryTool` and `ApplyEditCurveGeometryTool` to delegate to `ICurveEditOrchestrator`.
- Added internal transform collaboration methods to `GeometryModificationSkill` and parameterized the transform undo description in `RhinoGeometryModificationService`.
- Registered all new services in DI.
- Added dedicated smoke entry points:
  - CLI slug: `geometry-edit-derived-routing-smoke-test`
  - Rhino command: `_McpGeometryEditDerivedRoutingSmoke`

## Deviations From Plan

- `CurveGeometryEditService` from sub PLAN B was left in place but no longer used by the public curve edit Tools. The new `CurveEditOrchestrator` is the active Tool delegate and preserves DirectOverride behavior.
- ExactTransform responses return `ReconstructedCurveSummary = null`; the response contract was made nullable to reflect the plan.
- `LiveDerivedPointOperationEvaluator` uses `ILiveRhinoDocumentAccessor` only for `OffsetAlongNormal`, because it must sample curve frames from the active Rhino document. Scale and translate remain descriptor-based calculations.
- ExactTransform uses the new `GeometryModificationSkill` internal methods listed in the plan. Existing public Tool and Skill method signatures were not changed.
- The smoke creates temporary live curves for exhaustive routing checks instead of depending on a fixed fixture taxonomy. Cleanup verifies object count returns to the starting value.

## Issues Found And Fixed During Execution

- B's curve edit Tool constructor needed to change from `CurveGeometryEditService` to `ICurveEditOrchestrator`; B smoke was updated accordingly.
- `ReconstructedCurveSummary` becoming nullable required a targeted B smoke assertion update.
- The transform apply path had a hard-coded undo name; `RhinoGeometryModificationService` now accepts an undo description for the internal geometry-edit bridge while preserving the public `TransformObjects` undo name.

## Test Record

Build:

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo
```

Result: exit code 0.

C CLI fallback smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-derived-routing-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Result: exit code 0.

Key output:

```text
Geometry edit derived routing smoke test completed successfully (CLI fallback mode).
- PreviewEditCurveGeometry derived route rejected in CLI fallback
- ApplyEditCurveGeometry derived route rejected in CLI fallback
```

B regression smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-curve-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Result: exit code 0.

A regression smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-molecular-foundation-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Result: exit code 0.

Live Rhino smoke:

```text
_McpGeometryEditDerivedRoutingSmoke
```

Result: command added but not executed in this non-Rhino CLI session.

## Acceptance Alignment

- Build passes.
- CLI fallback returns explicit `LIVE_RHINO_REQUIRED` for derived Preview and Apply.
- DirectOverride public Tool signatures remain source-compatible; behavior is routed through the orchestrator.
- DerivedOperation supports:
  - `ScaleAboutCentroid`
  - `TranslateByVector`
  - `OffsetAlongNormal`
  - selectors `All`, `Indices`, `Range`, and `EndpointsOnly`
- Resolver supports:
  - DirectOverride reconstruction routing
  - Translate All -> ExactTransform
  - Uniform Scale All -> ExactTransform
  - local or non-uniform operations -> reconstruction with `STRATEGY_FORCED_RECONSTRUCTION`
- Apply paths report `UndoRecordName = "MCP:EditCurveGeometry"`.

## Rollback Verification

To roll back sub PLAN C, remove the derived routing files and revert the small connection changes in:

- `PreviewEditCurveGeometryTool.cs`
- `ApplyEditCurveGeometryTool.cs`
- `DeveloperCommandHandler.cs`
- `DependencyInjection.cs`
- `GeometryModificationSkill.cs`
- `RhinoGeometryModificationService.cs`
- C extensions in `CurveEditSpec`, request DTOs, and response DTOs

Sub PLAN A/B files can remain if rolling back only C.

## Remaining Items

- Run `_McpGeometryEditDerivedRoutingSmoke` inside Rhino against `Runtime_Test/MCP_rhino_test.3dm`.
- Manually verify Ctrl+Z after ExactTransform and reconstruction Apply paths restores geometry and metadata in one undo step.
- Run the LLM dry-run prompts from the plan and record whether the model chooses `DerivedOperation` instead of expanding to `DirectOverride`.

## Conclusion

Sub PLAN C is implemented as a buildable live-first derived routing layer on top of the existing curve edit Tools. It adds selector-based derived point edits, strategy resolution, exact-transform bridging, reconstruction fallback, CLI smoke, regression smoke coverage, and a Rhino smoke command.
