# Panel Cladding Match Parent Cells Plan

## Background

`PCMatch` transfers source cladding cell values to compatible target panels. A cell value can carry
one of two distinct meanings:

- a material code, such as `MPL-001`; or
- a parent-cell reference, such as `0A` or `0B`.

The match planner currently iterates the parsed cells, calls the general cladding-value normalizer,
stores the result in a variable named `material`, and writes it without validating the source's
parent-reference graph. The reported failure is a 2x2 source where `0A` and `0B` own `MPL-001`,
`1A` references `0A`, and `1B` references `0B`; the matched target receives four direct material
assignments instead of retaining the two child references.

## Goals

- Preserve parent-cell references in every `PCMatch` target write.
- Copy the canonical source relationship for the example exactly as
  `0A=MPL-001`, `0B=MPL-001`, `1A=0A`, `1B=0B`.
- Keep direct material assignments as material codes.
- Validate missing targets, blank referenced cells, cycles, disconnected parent regions, and other
  invalid reference graphs before planning any target mutation.
- Retain the existing mask-only topology transfer and target-owned H/V offset behavior.
- Preserve the all-targets-or-no-targets planning/commit contract and Rhino Undo behavior.

## Architecture Ownership

- `PanelCladdingRegionService` remains the application owner of parent-reference resolution,
  validation, and canonical cell-value projection.
- `PanelCladdingMatchPlanningService` will use that service when building the source configuration
  and will transfer its canonical cell values rather than treating every value as a material.
- `LivePanelCladdingMatchService` remains a thin Rhino adapter applying the completed plan verbatim
  inside one Undo record.

## Key Design

1. Parse the source panel's raw canonical cell keys and topology as today.
2. Resolve the complete source cell set through `PanelCladdingRegionService` with populated cells
   required.
3. Use `NormalizedCellValues` for the match writes. In that representation, each region's canonical
   owner retains the material code while every child stores the canonical owner cell label.
4. Fail source eligibility when the reference graph is invalid instead of silently materializing or
   partially copying it.
5. Continue copying the segment and merge masks, but no H/V offset keys.

## Files Involved

- `src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingMatchPlanningService.cs`
- `Project_Test/260805_TEST_panel-cladding-match/Program.cs`
- `Project_Test/260805_TEST_panel-cladding-match/README.md`
- `Project_Test/260818_TEST_panel-cladding-match-parent-cells/`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `Packaging/PanelCladdingEditor/package-manifest.json`
- `Project_Exet/260818_EXET_panel-cladding-match-parent-cells.md`

## Usage

1. Configure and save a source panel with direct material owners and parent-cell children.
2. Run `_PCMatch`, select compatible unconfigured targets, then select the source.
3. Open a target in `_PCEditor`; its child cells retain the source parent arrows/references rather
   than becoming independent material assignments.

## Acceptance Criteria

- The reported 2x2 example produces planned writes containing `MPL-001`, `MPL-001`, `0A`, and `0B`
  at the four corresponding cell keys.
- Applying the plan to a target dictionary and reparsing it preserves those raw values.
- Resolving the matched target produces two parent regions, each with two cells and material
  `MPL-001`.
- A source reference cycle fails before any target plan is returned.
- Existing segment/merge mask transfer remains intact.
- Target H/V offsets remain absent from both planned deletes and writes.
- Focused and existing Debug/Release regressions, solution builds, package build, hashes, and
  compiled RHP identity checks pass.

## Risks and Rollback

- Risk: canonicalization can collapse multi-hop reference chains to their canonical owner. This is
  already the editor Save contract and preserves the actual parent region while removing unstable
  intermediate chains.
- Risk: a previously accepted invalid source graph may now fail. This is intentional fail-closed
  behavior; the editor must save a valid source before matching.
- Rollback consists of reverting the planner/test/package changes. Existing panel attributes remain
  compatible, and Rhino Undo continues to cover each live match operation.

## Future Extensions

- Add an optional diagnostic summary to the Rhino command line reporting the number of copied
  material-owner cells and parent-reference cells.
- Add live fixture coverage if a stable anonymous Rhino test document becomes available.
