# Block member inspection

## Background

The user approved construction on 2026-10-12 after a live audit found that stone surfaces and STN_ID attributes in combined.3dm are inside assembly definitions. Existing block summaries cannot enumerate or measure those members. Requested comparison tolerance: 1/32 inch.

## Goal

Add one read-only live MCP tool that enumerates placed block leaves, preserving member identity, attributes and occurrence ancestry, and reports transformed surface geometry for size comparison. Then use the capability for the requested audit when the updated runtime is loaded.

## Architecture ownership

- Tools/Blocks: thin InspectBlockMembers MCP wrapper.
- Application: input validation, live inspector interface and service.
- Contracts: request/response DTOs without Rhino dependencies.
- Infrastructure/Rhino/Live: recursive definition traversal and geometry measurement through ILiveRhinoDocumentAccessor.
- Server: DI registration; existing assembly-scanned tool registration remains authoritative.

## Key design

Explicit top-level instance IDs; hidden/locked instances remain inspectable. Traverse nested definitions with composed occurrence transforms, cycle/depth/leaf limits and fail the whole request on traversal failure (no silently truncated success). Return leaf user text separately from root user text. Never explode, add objects, save, change selection, or open Undo.

Return model units, model tolerance, full transformed metrics and ordered corner coordinates for valid planar straight-edge single-face quads. Curved, holed, degenerate, or other unsupported surface shapes are explicitly classified and cannot be claimed as matching from bounding boxes. Quad edge/diagonal comparisons use cyclic/reversed corner correspondence and the user's linear tolerance, independent of position/orientation; all pairs within each ID must pass, avoiding reference-only tolerance chaining.

## Files

New block-member Contracts, Application interface/service, live inspector and geometry reader, Tools/Blocks wrapper, DI and safety-test updates. Matching TEST folder includes service/safety/live-only smoke, native synthetic geometry/recursive occurrence tests, and a reusable audit client. Add an exclusive block-member-inspection-smoke-test CLI hook and development Rhino smoke command.

## Usage

Select a document through the Router, list/filter top-level block instances, call inspect_block_members with explicit IDs in bounded batches, and compare leaves grouped by their own STN_ID. Report unsupported/missing IDs and every out-of-tolerance group without changing the model.

## Acceptance

Debug and Release Server plugin builds; both configurations pass tool safety and inventory smokes. Native fixtures verify nested transform composition, rotation/mirror invariance, scale/shear detection, trimmed/nonquad exclusion, duplicate roots, invalid targets, limits, unit reporting and no document/Undo mutation. Staged RHP assembly GUID must match the product manifest and plug-in declaration.

## Risks and rollback

The checkout already contains unrelated uncommitted construction; preserve it. The production plug-in is loaded in existing Rhino processes and cannot be replaced in memory. Build/stage first. Do not activate or retire any installed RHP unless independent host-persistent registry access is established. If activation needs user action, provide a concrete staged package and instructions; do not claim the live audit passed before it runs. Rollback consists of removing this additive capability or using the prior installed package, which stays reachable.

## Future extensions

General curved-surface congruence and a reusable grouped geometry audit skill can be separate work if needed. No offline fallback or new transport is introduced.
