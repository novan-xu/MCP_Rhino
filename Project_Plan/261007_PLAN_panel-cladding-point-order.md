# Panel command surface point order PLAN

## Background and authorization

The user requested automatic surface point-order adjustment in PCpid and PCUpdate
on 2026-10-07 and directed us to the existing MCP_Rhino skill requirements.
Reviewed SurfacePointOrderRebuildSkill, StandardFourPointSurfaceRebuildSkill,
the gravity-aware point-order PLAN/EXET, and current reconstruction/flip/direction code.

## Goals and design

Follow the current standard skill: gravity projected onto each face, lower-left
anchor viewed from its oriented front, clockwise corner order, four-point rebuild,
Brep front/back flip, then SwapUV. The current skill uses an explicit front/back
flip followed by SwapUV; older archived descriptions of FlipNormal are superseded.
Preserve front direction, footprint, object ID, attributes and geometry user data.
Repeated execution must not toggle normals or reorder already standardized panels.

Use actual straight quad boundaries. Report unsupported curved, nonquad, holed,
horizontal/gravity-degenerate or ambiguous panels without flattening or filling
their geometry. Other existing command operations may continue with reported skips.
PCpid retains full-layer numbering context and changes only selected surfaces.
PCUpdate standardizes processable selected sources before generating dependencies;
existing duplicate-CID skips remain excluded from geometry changes.

## Architecture ownership and files

Pure corner-order math belongs to PanelCladdingEditor Application; Rhino boundary
extraction, reconstruction and staged geometry changes belong to Infrastructure.
Use a standalone adapter matching the MCP skill without importing MCP_Rhino into
the independent plug-in. Domain results carry reordered counts/IDs and skip reasons;
command UI reports them. Integrate with existing PID metadata and Update dependency
transactions so each command retains one Undo record and restores source geometry
on failure. Add a focused TEST project and exact Server compilation exclusion.
Update packaging documentation/version and the matching EXET.

## Usage

Existing PCpid and PCUpdate prompts remain sufficient. Geometry ordering is automatic.

## Acceptance

Verify lower-left/clockwise ordering for reversed, permuted, side-facing, rotated
and sloped quads; ambiguity/degeneracy handling; native UV/normal parity with the
standard skill; shape/metadata/object-ID preservation; repeat no-op; selected-only
writes and single Undo including failure restoration. Run existing PID/setup/scope
and Update regressions, Debug/Release builds, assembly identity and package hashes.
Attempt native assertions in an independent hidden host to distinguish the earlier
agent-context RhinoCore startup failure from actual implementation failures.

## Risks and rollback

Never infer corners from bounding boxes or flatten curved surfaces. Rebuild may
change UV parameterization, so PCUpdate must generate after standardization. Keep
stored geometry metadata and the source front direction. Any native acceptance
limitation must remain explicit. Restore snapshots/Undo for geometry changes;
retain the previous installed RHP when staging or installing the new version.

## Future extensions

Nonquad/curved surface point-order conventions need their own explicit contract.
