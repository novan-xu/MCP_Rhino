# Nested stone outline support

## Background
The STN_srf offline batch completed 17 of 18 drawings. Row 6 contains a flat stone whose outline is inside another block definition. The linked Grasshopper script only inspects direct curves. On 2026-10-10 the user explicitly authorized extending the script and finishing row 6.

## Goal
Read nested outline curves in stone-definition coordinates and bake row 6 using the workbook offsets/heights. Preserve missing-only behavior and all original DWGs, workbook data, source geometry, and 17 completed outputs.

## Architecture ownership
This is a change to the user-owned linked StoneSurfaceBake.cs Grasshopper script. Existing Router offline Rhino script tools execute it in a headless document. No MCP surface, host, transport, registry, installation, or package change is required.

## Design
- Recursively collect direct and nested curves, composing instance transforms into the root stone definition before selecting the horizontal back edge.
- Duplicate transformed curves; never transform source objects or explode definitions.
- Preserve highest-Y/longest-edge selection and ambiguity rejection.
- Bound traversal and reject cycles, missing definitions, and invalid transforms before bake mutations.
- Keep existing skip-before-traversal behavior for previously baked assemblies.

## Files
- Authoritative linked script: the user's 05_STN/StoneSurfaceBake.cs.
- Ignored .validation/stone-bake-nested-outlines staging snapshots.
- Project_Test/261010_TEST_stone-bake-nested-outlines/ native regression scripts and test record.
- Matching EXET report after validation.

## Usage
Existing Grasshopper inputs and RunHeadless entry point remain unchanged. Resume only the failed row 6 job through existing offline preview/apply tools, then relocate its verified new copy to processed using the short date/time suffix.

## Acceptance
- Full linked script and existing regressions pass Debug and Release.
- Native tests cover nested translation, rotation, reflection/scaling, multiple levels, repeated definitions, ambiguity, bounds/cycle guards, and unchanged source geometry.
- Actual row 6 preview/apply succeeds, saved surfaces match workbook elevations/heights, and repeated bake adds nothing.
- All 18 output files reopen successfully; source DWGs/workbook retain their original hashes, and previous outputs retain theirs.

## Risks and rollback
Nested hardware may expose additional edges; retain deterministic selection and fail on ambiguity. Stage and test before deployment, retain a create-only backup of the prior script, and hash-check the source before replacement. Offline writer publishes a new copy only. No existing completed output is replaced.

## Future extensions
Other outline orientations or non-curve boundaries require separately scoped requests.
