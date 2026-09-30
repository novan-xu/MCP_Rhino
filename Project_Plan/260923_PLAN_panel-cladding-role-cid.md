# Panel cladding parent/child CID suffixes

## Background

The user requested a PC cladding editor behavior change: panel user text `parent=1`
adds `-P` to its CID and `child=1` adds `-C`. Material cell and extrusion curve
identifiers inherit that suffix before their own component code.

## Goals

For `PID_BKT_W1_03_01`, produce `CID_BKT_W1_03_01-P`,
`CID_BKT_W1_03_01-P-0A`, and `CID_BKT_W1_03_01-P-INT_B1` for a parent;
use `-C` for a child. Preserve the PID and ordinary panel behavior.

## Architecture ownership

- Application: shared, Rhino-independent CID naming policy; save/create/spawn/sync planning.
- Infrastructure/Rhino: persist panel metadata in existing mutation/rollback boundaries.
- Project_Test: focused executable regression tests, plus affected existing smokes.

## Key design

- Read role keys case-insensitively, trim values, and activate only the value `1`.
- If both flags are set, parent takes precedence, producing one suffix.
- Derive marked panel CIDs from PID so repeated execution and role changes cannot stack suffixes.
- Keep ordinary custom curve CIDs intact. Remove a stale role suffix only when the stored
  CID exactly matches the canonical PID-derived parent or child CID and neither flag is active.
- Apply naming to editor saves, panel create/match, spawn, synchronization, and PCUpdate.
- Normalize source panel CIDs inside the same mutation boundary as output changes.
- Keep existing managed-layer ownership and duplicate/ambiguity protections. Do not let
  an update of one role delete the other role's already suffixed dependencies.

## Files involved

`src/PanelCladdingEditor/Application/Services/PanelCladding/`, save service,
live panel repository and live create/match/spawn/sync/update adapters, and
`Project_Test/260923_TEST_panel-cladding-role-cid/`.

## Usage

Set `parent=1` or `child=1` on a source panel. Save or run the existing PC commands.
Use PCUpdate to regenerate existing managed dependencies with the new identifiers.

## Acceptance

Exact example outputs for both roles; no duplicated suffix on repeated execution;
PID and other metadata preserved; ordinary CIDs unchanged; sync retains role suffixes;
Debug/Release editor builds and focused/affected smoke tests pass.

## Risks and rollback

Changing identifiers can cause PCUpdate to recreate older unsuffixed outputs through
its existing reconciliation. Retain managed-scope limits, rollback, and Undo behavior.
No live document editing or installation is requested. Revert this change to restore
previous naming; no production registration or RHP activation is part of this work.

## Future extension

Any broader change to duplicate PID ownership or migration between panel roles needs
its own explicit contract; existing ambiguity checks remain in place.
