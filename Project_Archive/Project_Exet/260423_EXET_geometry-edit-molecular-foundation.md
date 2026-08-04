# 260423_EXET_geometry-edit-molecular-foundation

## Corresponding Plan

- Plan: `Project_Plan/260423_PLAN_geometry-edit-molecular-foundation.md`
- Execution date: 2026-04-26

## Related Artifacts

- Test folder: `Project_Test/260423_TEST_geometry-edit-molecular-foundation/`
- Commit / PR: not created in this session

## Result / Landed Scope

- Added the editable descriptor read path:
  - `GetEditableGeometryDescriptorTool`
  - `IEditableGeometryDescriptorService`
  - `LiveEditableGeometryDescriptorService`
  - descriptor domain models and request/response contracts
- Extended `GetGeometryFramesInLive` with optional `ParameterSpec` batch sampling while preserving the legacy single-frame request shape.
- Added live molecular helper interfaces and implementations:
  - `IGeometryFrameSampler` / `LiveGeometryFrameSampler`
  - `IGeometryMetadataOperator` / `LiveGeometryMetadataOperator`
  - `IBrepSurfaceDowngrader` / `LiveBrepSurfaceDowngrader`
- Registered the new services in both plugin live DI and CLI fallback live DI. CLI fallback uses `NullLiveRhinoDocumentAccessor`, so live-only calls return `LIVE_RHINO_REQUIRED`.
- Added dedicated smoke entry points:
  - CLI slug: `geometry-edit-molecular-foundation-smoke-test`
  - Rhino command: `_McpGeometryEditMolecularFoundationSmoke`

## Deviations From Plan

- `BrepDowngradeResult` stays Domain-pure and does not store RhinoCommon `Surface` or `Func<Surface, Brep>`. The live interface returns the transient `Surface` alongside the pure result for descriptor use. Future Apply plans can add rebuild behavior at the live infrastructure boundary.
- Untrimmed single-face Brep detection uses `BrepFace.IsSurface` plus `UnderlyingSurface()` instead of an explicit outer-loop boundary comparison. This keeps the first implementation aligned with RhinoCommon's own untrimmed-face signal; stricter loop tolerance checks can be added if live fixtures expose false positives.
- Existing batch analysis semantics were preserved: `GetGeometryFramesInLive` returns a successful batch response with per-entry failures for invalid frame entries. `FRAME_PARAMETER_OUT_OF_RANGE` is therefore stored on the failed `GeometryFrameResult`.
- RhinoCommon exposes curve Greville sampling directly, but not an equivalent public surface method. Surface control-point frame sampling computes Greville-like parameters from the surface NURBS knot lists and falls back to domain-grid positions when knot indexes are not available.
- The CLI registration uses a new partial hook `RegisterGeometryEditMolecularFoundationHandlers()` because `RegisterExtensionHandlers()` already has the central implementation in the main handler.

## Issues Found And Fixed During Execution

- The repository already had `GeometryAnalysisUvSampleRequest`; the initial duplicate contract was removed and the frame parameter spec reuses the existing DTO.
- `Surface.DuplicateSurface()` is not available in the referenced RhinoCommon build; the Brep downgrader now returns the underlying surface directly for read-only descriptor construction.
- `NurbsSurface.GrevilleParameter(...)` is not available in the referenced RhinoCommon build; surface sampling was adjusted to use knot-list Greville calculation.

## Test Record

Build:

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo
```

Result: exit code 0.

CLI fallback smoke:

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-molecular-foundation-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

Result: exit code 0.

Key output:

```text
Geometry edit molecular foundation smoke test completed successfully (CLI fallback mode).
- GetEditableGeometryDescriptor rejected in CLI fallback
- GetGeometryFramesInLive sampled entry rejected in CLI fallback
```

Live Rhino smoke:

```text
_McpGeometryEditMolecularFoundationSmoke
```

Result: command added but not executed in this non-Rhino CLI session.

## Acceptance Alignment

- Build passes.
- CLI fallback returns explicit `LIVE_RHINO_REQUIRED` for the new descriptor tool and the extended frame sampler path.
- Live-only read paths do not silently fall back to offline reads.
- Descriptor responses include `ControlPointCentroidWorld` in Summary and Full modes.
- Metadata replay remains internal; no public metadata mutation tool was added.

## Rollback Verification

The implementation is mostly additive. To roll back sub PLAN A, remove the new molecular foundation files and revert the small connection changes in:

- `RhinoGeometryMetricsService.cs`
- `GeometryFrameEntryRequest.cs`
- `GeometryFrameResult.cs`
- `DeveloperCommandHandler.cs`
- `DependencyInjection.cs`
- `GetGeometryFramesInLiveTool.cs`

## Remaining Items

- Run `_McpGeometryEditMolecularFoundationSmoke` inside Rhino against `Runtime_Test/MCP_rhino_test.3dm`.
- If live fixtures reveal edge cases around trimmed Brep detection, tighten the downgrader with explicit loop/domain boundary checks.
- Future PLAN B/D Apply paths should decide where to host Brep surface rebuild behavior.

## Conclusion

Sub PLAN A is implemented as a buildable live-first foundation layer with descriptor read, frame sampling, metadata snapshot/replay helper, Brep downgrade detection, DI registration, CLI smoke, and Rhino smoke command.
