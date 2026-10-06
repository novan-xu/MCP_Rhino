# PCCreate unit dimensions PLAN

## Background

The user explicitly requested that PCCreate generate CW_2.00_UNIT_DIMENSION,
CW_2.01_UNIT_WIDTH, and CW_2.02_UNIT_HEIGHT alongside its existing H/V grid data.
This is authorization for the narrow construction fix and its PLAN -> EXET -> TEST
record. The user also asked about the sync commands, repeating PCSyncSrf; explain
PCSyncSrf and the related PCSyncCrv from current source without changing them.

## Goals

- Write all three canonical dimension attributes for every successfully planned panel.
- Derive dimensions from the same panel-local extents used to generate its H/V grid.
- Refresh old dimension values, including noncanonical key casing, in the existing
  atomic attribute mutation.
- Keep grid topology, cell initialization, unrelated metadata, and failure behavior.

## Architecture ownership

PanelCladdingCreatePlanningService owns the attribute plan. Reuse dimension keys
and invariant five-decimal formatting from PanelCladdingKeyService. The existing
live adapter owns geometry projection and attribute commit/rollback. No MCP,
Router, Rhino host, command registration, or transport change is required.

## Key design

Format width = XMaximum - XMinimum and height = YMaximum - YMinimum after the
existing extent/guide validation. Persist dimension as <width>x<height>. Delete
prior spellings of these three keys before applying canonical writes. Use the
existing live commit path, preserving its mutation scope and rollback behavior.

## Files

- src/PanelCladdingEditor/Application/Services/PanelCladding/PanelCladdingCreatePlanningService.cs
- Project_Test/260818_TEST_panel-cladding-create-command/Program.cs
- Packaging/PanelCladdingEditor/README.md and package-manifest.json
- Matching 261006 PLAN, EXET, and TEST artifacts for this fix.

## Usage

Run PCCreate, select panel Breps, then select applicable H/V guide curves. The
panels receive dimension attributes with their existing grid attributes. PCCreate
still resets generated grid/cell state; this is not a dimensions-only migration.

## Acceptance criteria

- Missing dimensions are created; stale values/casing are replaced.
- Multiple differently sized panels use their own local extents and retain H/V values.
- Decimal formatting is invariant under a comma-decimal culture.
- Existing create, curve-topology, and sparse-topology regressions pass.
- Standalone plug-in Debug and Release builds pass. Narrow builds are appropriate
  because the MCP server, Router, and host are unchanged.
- Verify compiled RHP assembly GUIDs before packaging, stage version 1.0.80, and
  verify bundle file hashes. Production activation is outside this fix's scope;
  preserve the current installed RHP and registration.

## Risks and rollback

Dimension values follow the current panel-local frame, not a world-axis bounding
box. Native Rhino command execution remains a separate live validation step.
Revert this task's source/test/documentation changes to roll back code; staging
does not alter production registration or require deployment rollback.

## Future extensions

None required. A dimensions-only refresh would need its own user-requested scope.
