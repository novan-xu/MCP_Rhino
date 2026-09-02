# Panel Cladding Match Topology Masks Plan

## Background

`PCMatch` currently copies the configured cladding cells, cladding type, and signature while
preserving each target panel's own H/V divider offsets. Panel extrusion edits are now persisted as
two opaque panel-level values, `CW_2.05_SEGMENT_MASK` and `CW_2.06_MERGE_MASK`, but the match
planner does not transfer them.

The requested scope is deliberately narrower than copying the complete extrusion definition:
targets keep their existing H/V offset keys and values. Only the segment-presence and curve-join
masks follow the source.

## Goals

- Make `PCMatch` copy the source panel's canonical segment mask and merge mask to every compatible
  target.
- Remove stale target mask keys before writing the source masks.
- Preserve every target H/V offset key and value exactly as before.
- Preserve the existing logical-grid compatibility check so the mask dimensions always match the
  target lattice.
- Canonicalize legacy sources without stored masks as an all-segments-present, no-merges pair.
- Keep PID, CID, wall type, release, geometry, and unrelated target data untouched.

## Architecture Ownership

- `PanelCladdingKeyService` remains the owner of mask decoding and encoding.
- `PanelCladdingMatchPlanningService` owns source configuration projection, target compatibility,
  cleanup selection, and planned attribute writes.
- `LivePanelCladdingMatchService` continues to apply the completed plan in one Rhino Undo record;
  no live-adapter change is expected.

## Files Involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `Project_Test/260805_TEST_panel-cladding-match/Program.cs`
- `Project_Test/260805_TEST_panel-cladding-match/README.md`
- `Project_Test/260818_TEST_panel-cladding-match-topology-masks/`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Exet/260818_EXET_panel-cladding-match-topology-masks.md`

## Transfer Contract

1. Parse the source with its own existing H/V offsets.
2. Re-encode the decoded source topology into canonical segment and merge payloads.
3. Keep the source's offset keys out of the planned writes.
4. Require each target's parsed cell labels to match the source cell labels, which also proves that
   the target has the same horizontal-track and vertical-track counts.
5. Delete any target segment/merge mask keys and write the canonical source pair.
6. Leave all target offset keys absent from both the delete set and write set.

## Acceptance Criteria

- A source with deleted extrusion segments and merged runs produces plans containing both topology
  keys with decoded topology equal to the source.
- Existing target topology masks are replaced.
- Target H/V offsets are neither deleted nor written.
- A target with different valid offset distances but the same grid dimensions remains compatible.
- A target with different grid dimensions still fails before mutation.
- A legacy source with no mask keys writes canonical default masks.
- Existing `PCMatch` regression tests, focused Debug/Release tests, solution builds, package build,
  and compiled RHP assembly-identity checks pass.

## Risks and Rollback

- Risk: copying an opaque mask to a different lattice would make it invalid. Mitigation: retain the
  existing exact logical-cell-label compatibility check before generating a target plan.
- Risk: treating mask-only target data as a configured cladding assignment would unnecessarily
  block matching. Mitigation: keep target eligibility based on cladding/type/signature data and
  treat topology keys only as replaceable transfer data.
- Rollback consists of reverting the planner/test/package changes. Rhino Undo continues to cover
  the live multi-target attribute mutation.
