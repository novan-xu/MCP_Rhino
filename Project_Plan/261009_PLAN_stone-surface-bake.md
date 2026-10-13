# Stone surface bake

## Background

On 2026-10-09 the user changed the requested WF stone output from curves to
5-inch-tall surfaces extending in world +Z, then explicitly authorized extending
the existing bake capability. The selected live document uses inches. Its latest
preview found 126 stone occurrences in 61 GA- assemblies and 12 output definitions;
no WF output has been created.

## Goals

- Add an optional world +Z surface output to the existing stone bake Skill.
- Retain GA- scope and exclusions, selected edge, left-end base point, WF names,
  one shared definition per stone name, and exact parent Assembly_ID on instances.
- Keep curve mode backward compatible, preserve source assemblies, and keep
  preview read-only, apply undoable, and repeat runs idempotent.

## Architecture ownership

The existing Tools/Blocks wrappers expose typed output mode and height. Contracts
carry these inputs and preview evidence. The Skill and Application service route
and validate them. Infrastructure/Rhino/Live owns unit conversion, surface geometry,
placement, compatibility checks, and live document writes.

## Key design

Use Curve (default) and WorldZSurface modes, with explicit height in inches for
surface mode. Convert to document units, rejecting unspecified/custom units.
Normalize each surface definition to an XZ rectangle starting at the origin.
For each occurrence, transform both selected source endpoints through the full
nested transform. Build a placement whose X axis spans that exact edge, origin
is its transformed left endpoint, and Z axis is unit world +Z. This keeps the
physical height constant even under mirrored, tilted, or scaled parents while
sharing the original WF name. Reject degenerate edges parallel to world Z.

Check existing definitions against the requested geometry and height; never
overwrite a conflicting curve/surface definition or change source blocks.
Return bounded placement evidence (world endpoints and height) for verification.

## Files involved

- StoneWireframeBake request/response, Skill, service/interface, and two wrappers.
- LiveStoneWireframeBakeOperator and a focused surface geometry adapter.
- CLI runner, surface regression smoke registration and TEST artifacts.
- MCP package manifest and a host-side installation/attestation script.

## Usage

Preview/apply through the installed Router with outputMode WorldZSurface and
surfaceHeightInches 5, targeting the explicit document session. Use the matching
surface-mode preview again to prove no duplicate output remains to create.

## Acceptance criteria

- Debug and Release solution builds and tool safety smoke pass.
- Tests cover options, unit conversion, rotated/mirrored/nonuniform placements,
  exact endpoints, world +Z height, singular edges, and curve-mode compatibility.
- Package assembly GUID matches the manifest and plugin declaration.
- Independent host install/Validate and post-restart load attestation precede
  production use of the new version.
- Live verification confirms 12 single-surface definitions, 126 correctly placed
  and attributed instances, 5-inch world height, and unchanged source assemblies.

## Risks and rollback

World direction must not inherit parent Z scale or rotation. Shared definitions
must reject incompatible existing WF contents. Keep the prior RHP reachable;
stage only until ordinary host PowerShell establishes persistent registration.
Do not close Rhino with unsaved work. The live bake is one Rhino Undo entry.

## Future extensions

Other extrusion directions and conversion of existing conflicting WF definitions
are outside this request.
