# Reference Image Accurate Product Modeling EXET

## Corresponding Plan

- Plan: `Project_Plan/260509_PLAN_reference-image-accurate-product-modeling.md`
- Execution date: 2026-05-09

## Related Artifacts

- Test folder: `Project_Test/260509_TEST_reference-image-accurate-product-modeling/`
- Commit / PR: not created in this working copy

## Execution Result / Actual Scope

- Extended structured reference-image parts with local frame, taper, anchors, and profile-point fields.
- Added product geometry preview planning:
  - `ReferenceImageProductGeometryPlanningService`
  - `ReferenceImageProductGeometryPlanningSkill`
  - `PreviewReferenceImageProductGeometryPlan`
- Updated `ReferenceImageObjectModelingAgent` to run product geometry planning before initial massing and store the plan in `ReferenceImageObjectModelingPlan`.
- Updated initial massing to preserve local frame orientation for rounded boxes, ellipsoids, cylinders, cones, capsules, and tori where applicable.
- Added `TaperedBox` support through the existing general primitive path:
  - `CreateTaperedBoxes`
  - `GeneralPrimitiveKind.TaperedBox`
  - `LiveGeneralPrimitiveBuilder` tapered mesh builder
- Added point-array curve-derived product tools:
  - `CreateProfileExtrusionsFromPoints`
  - `CreateLoftsFromProfiles`
  - `CreatePipesFromPoints`
- Updated MCP safety governance expectations for the new tool surface.

## Deviations From Plan

- Implemented the first slice by extending existing `GeneralPrimitive` and `CurveOps` services instead of adding a separate `ProductGeometry` service/operator. This avoided a parallel geometry stack and kept tool ownership aligned with existing families.
- `MirrorObjectsByPlane` was not implemented in this slice; symmetric furniture assemblies still require existing transform workflows or a follow-up preview/apply pair.
- The agent now previews product geometry strategy and honors orientation/taper during massing, but it does not yet execute loft/profile/SubD strategies from the agent loop. Those are available as atomic tools and visible in the preview plan.

## Issues Found And Fixed During Work

- The archived MCP safety smoke had not yet been updated for recent material/reference-image tools, so the safety expectation count was stale. Updated it to cover all 152 reflected tools.
- Product planning now explicitly classifies woven fabric, wood grain, labels, cast shadows, highlights, lighting, and room/floor context away from geometry.

## Test Record

Builds:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

Both builds completed with `0 Warning(s)` and `0 Error(s)`.

Plugin outputs:

- `src/MCP_Rhino.Server/bin/Debug/net8.0/MCP_Rhino.Server.rhp`, 1,905,664 bytes
- `src/MCP_Rhino.Server/bin/Release/net8.0/MCP_Rhino.Server.rhp`, 1,793,536 bytes

Debug smokes:

```powershell
dotnet .\src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll reference-image-accurate-product-modeling-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
```

Key output:

- `[OK] reference-image product brief preserves local frames, taper, and cue classification.`
- `[OK] product geometry preview selects tapered/profile-capable strategies and excludes material/shadow cues from geometry.`
- `[OK] MCP safety annotations verified for 152 tools.`
- `[OK] MCP tool inventory discovered 152 tools.`

Release smokes:

```powershell
dotnet .\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll reference-image-accurate-product-modeling-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
```

Key output:

- `[OK] reference-image product brief preserves local frames, taper, and cue classification.`
- `[OK] MCP safety annotations verified for 152 tools.`
- Surface inventory includes `Geometry: 24`, `Geometry/CurveOps: 13`, `Geometry/SubD: 5`, `Modeling: 2`.

## Acceptance Alignment

- Local-frame and taper fields are accepted, preserved, planned, and validated by smoke tests.
- Tapered furniture members can be created through a dedicated tool instead of being forced into vertical boxes.
- Product preview reports material-only and reference-only cues before mutation.
- New MCP tool methods include explicit safety annotations and descriptions.
- Debug and Release plugin builds pass.

## Rollback Verification

Rollback remains straightforward: remove the new DTO fields, product planning service/skill/tool, TaperedBox general primitive support, point-array curve-derived methods/tools, smoke folder, and safety-smoke expectations. Existing V1 reference-image rounded-box massing remains independent.

## Current Residual Items

- Add mirror preview/apply tooling for furniture side-frame duplication.
- Teach the agent execution loop to call point-array profile/loft/pipe tools directly when the product plan selects them.
- Add a live Rhino Kyajah chair fixture smoke once a stable saved `.3dm` fixture is available.

## Conclusion

The plan is executed as a validated first product-geometry slice. It fixes the core issue of axis-aligned primitive-only massing and introduces a previewable path for accurate product geometry while keeping material and shadow cues out of geometry.
