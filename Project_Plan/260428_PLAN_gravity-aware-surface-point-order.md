# 260428_PLAN_gravity-aware-surface-point-order

## Background

The current surface point-order rebuild pipeline can rebuild quad surface-like objects, but its automatic reference edge selection is not correct for panels whose front face is not aligned with the world plane assumptions.

The incorrect behavior comes from two coupled implementation choices:

- `LiveBoundaryReferenceCurveAnalyzer` ranks boundary edges with a world-coordinate score based on model `Y` and `Z`.
- `LiveSurfaceLocalCoordinateSystemBuilder` constructs the local coordinate system from the selected reference edge direction plus a boundary-computed normal.

This creates a circular dependency and a wrong frame:

1. the reference edge is selected before a surface-local gravity frame exists;
2. the frame is then built from that already-selected edge;
3. panels in different orientations can receive different or incorrect "lower-left" anchors.

The corrected requirement is surface-local:

- the surface front face is determined per surface or single-face Brep, independent of world plane orientation;
- gravity is always world `(0, 0, -1)`;
- gravity is projected into the surface plane;
- "lower-left" is evaluated in the view from the surface front face, using projected gravity as local down;
- point order starts at that lower-left corner and proceeds clockwise in that front-face view.

Reference sketch: `C:\Users\nxu\OneDrive - Island International Industries\Downloads\image.png`.

## Goals

- Replace the world `Y/Z` reference-edge scoring in `LiveBoundaryReferenceCurveAnalyzer` with surface-local gravity-frame scoring.
- Rebuild `LiveSurfaceLocalCoordinateSystemBuilder` so it constructs a gravity-aware frame per surface:
  - front normal from the actual surface / Brep face orientation;
  - local down from projected world gravity;
  - local right/left from front normal and projected gravity;
  - local origin from the boundary centroid, not the chosen reference edge.
- Preserve the existing public MCP tools:
  - `InspectSurfaceRebuildDescriptor`
  - `PreviewRedefineSurfacePointOrder`
  - `ApplyRedefineSurfacePointOrder`
- Keep mutation behavior live-only and undoable through the existing apply path.
- Keep current object metadata preservation and ObjectId preservation.
- Add smoke coverage proving that rotated and differently oriented panels no longer depend on world `Y/Z` scoring.

## Non-Goals

- Do not add a separate user-facing MCP tool solely for reading local coordinate systems in this wave.
- Do not broaden rebuild support beyond the current executable quad-surface scope.
- Do not change the Rhino live/offline execution model.
- Do not silently guess a lower-left corner when projected gravity is degenerate, such as a horizontal face whose normal is parallel to world gravity.

## Architecture Ownership

This is a capability construction task that modifies an existing Tool-backed workflow.

- **Tools**
  - No new MCP tool is planned.
  - Existing rebuild tools keep their names and contracts unless a contract extension is required for diagnostics.

- **Application**
  - `SurfaceRebuildOrchestrator` may need call-site updates if the local-frame builder interface changes.
  - `SurfaceBoundaryPointOrderer` should remain the pure point ordering implementation. It may need only small adjustments if a dedicated lower-left anchor mode is introduced.

- **Domain**
  - `SurfaceLocalCoordinateSystem` likely needs explicit fields or comments for `XAxis = local right`, `YAxis = local up`, and `Normal = front-face normal`.
  - `SurfaceBoundaryLoop` or `SurfaceRebuildDescriptor` needs to carry the surface front normal and possibly the gravity-frame projection diagnostics.
  - Add warning/error vocabulary for degenerate projected gravity and ambiguous lower-left selection.

- **Infrastructure/Rhino/Live**
  - `LiveBoundaryReferenceCurveAnalyzer` owns Rhino object / face extraction and automatic lower-left/reference anchor selection.
  - `LiveSurfaceLocalCoordinateSystemBuilder` owns gravity-aware local frame construction from pure descriptor data.
  - RhinoCommon calls remain concentrated here.

- **Project_Test**
  - Add a dedicated smoke folder:
    `Project_Test/260428_TEST_gravity-aware-surface-point-order/`
  - Register a unique smoke slug, for example:
    `gravity-aware-surface-point-order-smoke-test`

- **Project_Exet**
  - To be written after implementation and verification:
    `Project_Exet/260428_EXET_gravity-aware-surface-point-order.md`

## Key Design

### 1. Surface Front Normal

`LiveBoundaryReferenceCurveAnalyzer` must extract a stable front normal for each supported target:

- direct `Surface`: evaluate the surface frame/normal near the surface domain center;
- single-face `Brep`: evaluate the `BrepFace` normal so face orientation is respected;
- fallback to boundary normal only if Rhino face/frame evaluation fails, with a warning.

The normal must represent the front face used for the "viewing from the front face" rule. It must not be inferred from world axes.

### 2. Gravity-Aware Local Frame

Let:

- `N` = normalized front-face normal;
- `G` = normalized world gravity `(0, 0, -1)`;
- `D` = projected gravity onto the surface plane:
  `D = Normalize(G - Dot(G, N) * N)`;
- `U` = local up = `-D`;
- `R` = local right = `Normalize(Cross(N, D))`.

The local frame should be:

- `Origin` = boundary centroid;
- `XAxis` = `R` local right;
- `YAxis` = `U` local up;
- `Normal` = `N` front-face normal.

This keeps the existing clockwise angular sort meaningful: in a standard `X = right`, `Y = up` frame, descending polar angle around the centroid gives clockwise order.

Degenerate case:

- if `D` cannot be normalized because `N` is parallel to gravity, automatic lower-left selection is undefined;
- return a clear failure such as `SURFACE_GRAVITY_PROJECTION_DEGENERATE`;
- do not fall back to world `Y/Z`.

### 3. Lower-Left Anchor Selection

Automatic selection should happen after projecting boundary vertices into the gravity-aware local frame.

For each boundary vertex:

- project to local `(x, y)`;
- compute the local bounding box;
- identify the lower-left target as `(minX, minY)`;
- choose the vertex closest to that target, using normalized distances so wide and tall panels are treated consistently;
- if multiple vertices tie within tolerance, mark ambiguity rather than choosing from world coordinates.

The suggested reference curve should be oriented so:

- `StartPoint` is the lower-left vertex;
- `EndPoint` is the next clockwise boundary vertex from that start.

This preserves the existing `ReferenceStart` anchor behavior while making point 1 be the surface-local lower-left corner.

### 4. Candidate Edge Scoring

`SurfaceReferenceCurveSpec.Score` should no longer use world `Y/Z`.

Candidate ranking should be derived from the surface-local gravity frame:

- primary: candidate start is the lower-left vertex;
- secondary: candidate direction matches the clockwise next edge from the lower-left vertex;
- tertiary: longer edge only as a tie-breaker when topology is ambiguous.

For explicit `ReferenceEdgeIndex`, the edge should still be resolved by index, but its start/end should be normalized in the local gravity frame where possible. If the explicit edge conflicts with the lower-left requirement, the response should show that in diagnostics.

### 5. Builder Interface

The current builder signature is:

```csharp
Build(SurfaceBoundaryLoop boundaryLoop, SurfaceReferenceCurveSpec referenceCurve)
```

That is insufficient because the correct frame depends on surface front normal and gravity, not on reference edge direction.

Preferred change:

```csharp
Build(SurfaceBoundaryLoop boundaryLoop, GeometryVectorData frontNormal)
```

or:

```csharp
Build(SurfaceRebuildDescriptor descriptor)
```

The second option is more extensible because the descriptor can carry boundary, normal, tolerance, and future diagnostics. The implementation phase should choose the smaller change after checking call sites, but the builder must not derive `XAxis` from `referenceCurve`.

### 6. Point Orderer Compatibility

The existing `SurfaceBoundaryPointOrderer` can mostly remain unchanged if:

- local frame `XAxis` is right;
- local frame `YAxis` is up;
- `ReferenceStart` points to the lower-left vertex;
- requested direction is `Clockwise`.

If testing shows edge cases where `ReferenceStart` is too indirect, add a new enum value:

```csharp
SurfacePointOrderStartAnchorMode.LowerLeft
```

That mode should resolve directly from projected local coordinates and become the default for this workflow.

### 7. Inspect/Preview Diagnostics

`InspectSurfaceRebuildDescriptor` and `PreviewRedefineSurfacePointOrder` should expose enough data to audit the decision:

- front normal;
- projected gravity/down vector or local frame;
- selected lower-left vertex index;
- selected clockwise order;
- ambiguity or degeneracy warnings.

If adding fields to existing response models is needed, keep it backward compatible by only adding optional properties.

## Involved Files

Primary implementation targets:

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBoundaryReferenceCurveAnalyzer.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceLocalCoordinateSystemBuilder.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceLocalCoordinateSystemBuilder.cs`
- `src/MCP_Rhino.Server/Application/Services/Rebuild/SurfaceRebuildOrchestrator.cs`
- `src/MCP_Rhino.Server/Application/Services/Rebuild/SurfaceBoundaryPointOrderer.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceRebuildDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceBoundaryLoop.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceLocalCoordinateSystem.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceReferenceCurveSpec.cs`

Likely test and smoke targets:

- `Project_Test/260428_TEST_gravity-aware-surface-point-order/DeveloperCommandHandler.GravityAwareSurfacePointOrderSmokeTest.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGravityAwareSurfacePointOrderSmokeCommand.cs`
- existing registration partials in `DeveloperCommandHandler` / plugin command registration, following current project patterns.

Reference files:

- `Project_Test/260423_TEST_surface-point-order-rebuild/DeveloperCommandHandler.SurfacePointOrderRebuildSmokeTest.cs`
- `Project_Plan/260423_PLAN_surface-point-order-rebuild.md`

## Usage After Completion

The user-facing runtime flow should remain the same:

1. filter/select surface-like objects;
2. inspect if needed;
3. preview `PreviewRedefineSurfacePointOrder` with `direction = Clockwise`;
4. apply `ApplyRedefineSurfacePointOrder`.

The difference is internal: automatic point order will be evaluated from each surface front face with gravity-projected local down, not from world `Y/Z`.

## Acceptance Criteria

- No automatic reference edge selection uses world `Y/Z` scoring.
- For a vertical panel whose front normal changes from `+X` to `-X`, the lower-left point changes appropriately in the surface-front view.
- For panels lying on `X`, `Y`, and sloped planes, point 1 is the lower-left corner in the local front-face gravity view.
- Points 1-4 proceed clockwise from that view.
- Existing `PreviewRedefineSurfacePointOrder` and `ApplyRedefineSurfacePointOrder` remain live-only.
- Apply still uses one undo record named `MCP:RedefineSurfacePointOrder`.
- Object IDs and metadata remain preserved.
- Horizontal/gravity-parallel surfaces do not get a silent world-coordinate fallback; they return a clear degeneracy message.
- Existing surface point-order smoke tests still pass or are intentionally updated with documented behavior changes.
- New smoke test slug validates at least:
  - two vertical panels with opposite front normals;
  - one side-facing panel;
  - one sloped panel with non-world-aligned normal;
  - one degenerate horizontal case.

## Test Plan

1. Add focused pure math assertions where possible for:
   - gravity projection;
   - right/up axis construction;
   - lower-left selection from projected points;
   - clockwise order from projected points.

2. Add live Rhino smoke coverage:
   - create temporary quad surfaces/Breps in the active document;
   - set up known orientations and expected front normals;
   - run inspect/preview;
   - assert selected point 1 matches the expected lower-left in local frame;
   - assert ordered point sequence is clockwise;
   - clean up temporary objects through live undo-aware delete.

3. Run existing surface point-order rebuild smoke:
   - ensure old behavior that is still valid remains intact;
   - update only assertions tied to the old world-coordinate scoring.

4. Run repository build/tests required by the implementation changes.

## Risks And Rollback

- **Front normal ambiguity**: direct surfaces and Brep faces can differ in orientation semantics. Mitigation: test both direct Surface and single-face Brep paths and prefer Rhino face-oriented normals for Breps.
- **Gravity projection degeneracy**: horizontal faces cannot produce a lower-left based on gravity. Mitigation: explicit failure with a stable code, no fallback to world scoring.
- **API ripple**: changing the builder interface may touch orchestrator and tests. Mitigation: keep the signature change narrow and avoid changing MCP tool names.
- **Existing workflows**: some previous outputs may change because the old behavior was world-plane based. Mitigation: document this in EXET as intentional behavior correction.
- **Rollback**: revert the files listed in this plan and remove the new test folder/slug. Existing `260423` surface point-order implementation is the rollback baseline.

## Future Extensions

- Add a read-only MCP tool to inspect surface-local gravity frames directly for debugging and QA.
- Support user-specified front-face override when Rhino face orientation is not the intended production front.
- Support non-quad planar boundaries after the quad workflow is stable.
- Add response fields that show local 2D projected coordinates for every ordered point.

