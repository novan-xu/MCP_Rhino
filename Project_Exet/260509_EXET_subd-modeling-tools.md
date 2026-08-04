# SubD Modeling Tools EXET

## Corresponding Plan

- Plan: `Project_Plan/260509_PLAN_subd-modeling-tools.md`
- Execution date: 2026-05-09

## Related Artifacts

- Test folder: `Project_Test/260509_TEST_subd-modeling-tools/`
- Commit / PR: not created in this working copy

## Execution Result / Actual Scope

- Added `Tools/Geometry/SubD` ownership text to the architecture guide.
- Added SubD DTOs and responses:
  - `SubDModelingRequests.cs`
  - `SubDModelingResponses.cs`
- Added domain specs/enums:
  - `SubDModelingSpecs.cs`
  - `SubDModelingEnums.cs`
- Added application/live boundary:
  - `ILiveSubDModelingOperator`
  - `RhinoSubDModelingService`
  - `LiveSubDModelingOperator`
- Added MCP tools:
  - `PreviewSubDCage`
  - `CreateSubDCage`
  - `CreateSubDBox`
  - `CreateSubDCushions`
  - `InspectSubDObjects`
- Registered SubD services in Debug and Release DI paths.
- Integrated SubD as a product geometry planning strategy for explicit soft/organic reference-image parts.

## API Probe Result

The local standalone PowerShell reflection probe could not fully enumerate `RhinoCommon.dll` types outside Rhino because the load context could not resolve `System.Runtime, Version=7.0.0.0`. A binary string scan showed SubD native/API symbols are present, but compile-time direct `Rhino.Geometry.SubD` binding was not reliable in the CLI context.

Implementation choice:

- Compile against stable RhinoCommon `Mesh` and `GeometryBase`.
- Create SubD through runtime reflection against `Rhino.Geometry.SubD.CreateFromMesh(...)` inside the live Rhino process.
- Return `SUBD_API_UNAVAILABLE` if the live runtime does not expose that API shape.

## Deviations From Plan

- First slice does not implement crease editing, SubD replacement, or conversion to Brep/Mesh.
- `CreateSubDCushions` implements crown, side bulge, and edge compression in the cage generator. `BackTiltDegrees` is reserved in the DTO but not applied in this first implementation.
- Live object creation is validated through CLI live-boundary smoke, not through an in-Rhino object-count smoke, because no stable saved `.3dm` SubD fixture was part of this plan execution.

## Issues Found And Fixed During Work

- Added MCP safety expectations for both this SubD slice and earlier material/reference-image tools so the safety smoke matches the full reflected tool inventory.
- Kept SubD tools in a dedicated `Tools/Geometry/SubD` family to avoid mixing soft-form tools into hard primitive or curve-derived families.

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
dotnet .\src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll subd-modeling-tools-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
```

Key output:

- `[OK] SubD cage preview accepts valid topology and rejects invalid face indices.`
- `[OK] SubD create/inspect tools route to live Rhino in CLI fallback mode.`
- `[OK] reference-image product planning can select SubD for soft cushion geometry without using SubD for materials or shadows.`
- `[OK] MCP safety annotations verified for 152 tools.`
- Surface inventory includes `Geometry/SubD: 5`.

Release smokes:

```powershell
dotnet .\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll subd-modeling-tools-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
dotnet .\src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
```

Key output:

- `[OK] SubD cage preview accepts valid topology and rejects invalid face indices.`
- `[OK] SubD create/inspect tools route to live Rhino in CLI fallback mode.`
- `[OK] MCP safety annotations verified for 152 tools.`
- Surface inventory includes `Geometry/SubD: 5`.

## Acceptance Alignment

- SubD tools are MCP-exposed with explicit safety annotations.
- `PreviewSubDCage` rejects invalid topology without mutation.
- `CreateSubDCage`, `CreateSubDBox`, and `CreateSubDCushions` are live-only mutation tools and return the live boundary in CLI fallback mode.
- `InspectSubDObjects` is read-only and live-only.
- Product geometry planning can select `SubD` for explicit soft cushion parts while material, texture, highlight, and shadow cues remain non-geometry.
- Debug and Release plugin builds pass.

## Rollback Verification

Rollback removes the SubD DTOs, service, live operator, tools, DI registration, architecture guide ownership bullet, smoke folder, and MCP safety expectations. Product Brep/profile/taper tools remain independent.

## Current Residual Items

- Add a Rhino-hosted live SubD creation smoke with an object-count assertion.
- Add crease control and conversion tools after the first live SubD path is exercised in Rhino.
- Apply `BackTiltDegrees` in the high-level cushion cage generator.

## Conclusion

The SubD plan is executed as a bounded first tool family. It provides topology preview, live-only creation/inspection routes, and agent planning visibility without using SubD as a substitute for misunderstood hard geometry or material/shadow cues.
