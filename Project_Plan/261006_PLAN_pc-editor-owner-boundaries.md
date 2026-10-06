# PC editor owner boundaries PLAN

## Background

The user explicitly requested correcting dashed cladding boundaries in the
attached irregular-grid example. The current renderer compares a cell only with
its direct parent and infers adjacency from representative row/column numbers.
Deleted dividers create spanning cells whose representative labels do not encode
their full extent; indirect references also share ownership without a direct link.

## Goals

Render each existing boundary inside one resolved cladding region as dashed,
independent of label numbering, direct versus indirect references, and material
code equality. Keep boundaries between distinct owners solid and deleted
boundaries absent.

## Architecture ownership

Reuse Application logical-cell expansion and region resolution, the same ownership
rules used by save/spawn. UI owns mapping physical cell edges to drawing positions
and the existing material-backed dashed style. Persisted topology and assignments
are not changed by rendering.

## Key design

Expand the current logical groups onto physical cells, resolve cladding regions,
then inspect each neighboring physical pair once. Draw dashes only between
different logical groups within the same resolved region. Use actual grid-edge
coordinates, never representative-label adjacency or material-code comparisons.
Keep deleted logical interiors and panel perimeter untouched. Invalid ownership
must not produce invented region boundaries.

## Files

- Application/Services/PanelCladding/PanelCladdingLogicalCellService.cs
- UI/PanelCladdingGridCanvas.cs
- Existing cell-topology and visual-polish test projects
- Packaging documentation/version and matching TEST/EXET artifacts

## Usage

Open PCEditor in cladding view. Boundaries update from the current parent graph
and topology, including unsaved assignments.

## Acceptance criteria

Reproduce the supplied 3-column/4-row topology: 0D/2D, 0C/2C, 0B/1A,
0B/2A, and 1A/2A are dashed; 0A/0B and separate same-material owners remain
solid. Test indirect references, alternate owners/numbering, nonrectangular
logical groups, and absent deleted boundaries. Inspect rendered WPF output and
run Debug/Release regressions/builds. Stage version 1.0.84 after assembly identity
and bundle hash verification.

## Risks and rollback

Avoid drawing across another owner at T-junctions or restoring deleted dividers.
Preserve line weight, dash/gap style, selection, and extrusion-view behavior.
Rendering is read-only; source rollback can remove this follow-up independently.
Build/stage only under the repository's host-registry-attestation restriction.

## Future extensions

Native Rhino acceptance with the user's saved panel and expanded irregular-layout
render fixtures as additional cases arise.
