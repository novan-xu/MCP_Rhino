# PCpid panel setup PLAN

## Background and authorization

The user explicitly requested implementation of `PCpid` on 2026-10-07. This
authorizes construction and the required PLAN -> EXET -> TEST chain. Existing
commands consume PID/CID but do not infer panel addresses from geometry.

## Goals

Prompt, in order, for a three-letter project code, target panels, a north-facing
panel, and a first-floor panel. Assign unique `PID_<PROJECT>_<ELEVATION>_<LEVEL>_<BAY>`
and the corresponding CID, plus canonical elevation and level user text.

## Architecture ownership

- Domain: Rhino-independent geometric snapshots, requests, assignments/results.
- Application: deterministic direction/plane/row/column planning and identity checks.
- Infrastructure/Rhino: live geometry extraction, document collision/dependency
  checks, attribute commit and rollback within one Rhino undo record.
- UI: thin `PCpid` command and its four prompts.
- Packaging: expected command list, release version, usage documentation.
- TEST: standalone production-code smoke tests and anonymized tutorial evidence.

## Key design

Use world Z for height. The north reference's oriented outward face normal defines
project north; clockwise quarter turns define east, south, west. Preserve actual
face orientation, including recessed facades: a building-centroid sign heuristic
would misclassify the tutorial's N2 facade. Require planar vertical single-face
panels and outward normals; reject ambiguous diagonal directions.

Group same-facing coplanar panels within document linear/angular tolerances.
Order elevation planes by their left edge in the exterior elevation view, then
bottom edge, with geometric tie breakers. For each elevation, group aligned left
edges into bays starting at 01. Across all targets, group aligned bottom edges into
levels; the first-floor reference is 01, observed rows above increment, below use
00, -01, etc. Use bounded clusters rather than chaining near-equal coordinates.
Do not infer missing floors/bays from spacing or read temporary IDs for numbering.

Write CW_1.01_PID, CW_1.02_CID, CW_1.03_ELEVATION, CW_1.04_LEVEL and the existing
CID-derived object-name convention. Bay is encoded in PID/CID; no new CW key is
invented. Reuse CW_1.06_UNIT_TYPE role suffix rules, preserving all other metadata.
PCpid explicitly recalculates CID even if it previously held a custom value.

Both reference objects must belong to the target selection. Validate every panel
before writes. Reject duplicate computed addresses and conflicts with unselected
source objects, including hidden/locked objects. Reject renumbering that would
orphan existing generated dependencies; PCpid is the initial setup step. Changes
stay limited to explicitly selected panels and are undone together; rollback any
partial attribute commit. A repeat with identical input should make no writes.

## Files involved

New PanelCladdingPid models, planning service, live service, command, and interface;
Packaging/PanelCladdingEditor manifest, installer, README; corresponding TEST
folder; existing command-registration regression expectations.

## Usage

Save the Rhino document, run PCpid, enter e.g. BKT, select the complete panel set,
choose a selected north panel and a selected level-01 panel. Run PCCreate and the
remaining setup commands afterward. Include all panels being numbered in one run
for consistent numbering; unselected objects do not contribute inferred grid lines.

## Acceptance criteria

- Tutorial panels 1/2 => N1, bay 01, levels 01/02; 3/4 => N2, bay 01, levels 01/02.
- Tutorial 5/6, 8/7, 9/10 => E1, bays 01/02/03, lower/upper levels 01/02.
- All 20 tutorial snapshots produce unique IDs, independent of selection order.
- Rotation, tolerances, unequal panel sizes, stepped planes, reference validation,
  corner CIDs, collisions, dependency guards, idempotence and failure rollback tested.
- Standalone Debug/Release builds and affected regressions pass; compiled RHP GUID
  matches the manifest and remains distinct from MCP_Rhino. Staged package includes PCpid.

## Risks and rollback

Irregular/staggered rows and inward normals require geometry preparation; four
prompts cannot identify architectural floors absent from the selected geometry.
The tutorial will be inspected read-only through the live Router. Do not modify
the user's document just to verify implementation. Native prompt/Undo acceptance
is recorded separately if unavailable. Stage only unless independent host registry
attestation is established; preserve the current installed RHP. Source rollback
removes this feature and restores the previous packaging metadata.

## Future extensions

Explicit floor datums, manual plane/bay overrides, and dependency-aware renumbering
can be separate requests when needed.
