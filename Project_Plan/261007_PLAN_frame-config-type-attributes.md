# Frame config/type attributes PLAN

## Background

The user explicitly requests retiring CW_1.5D_FRAME TYPOLOGY and
CW_2.09_FRAME_ASSIGNMENTS, storing the combined delete/merge/hide layout as
CW_1.08_FRAME_CONFIG and the existing assignment values as CW_1.09_FRAME_TYPE.

## Goal

Use two canonical frame attributes throughout the standalone editor and curve
commands, without losing existing panel layouts or assigned profile definitions.

## Architecture ownership

Application key/assignment services own codecs and legacy reads; save/create/match/
template/sync planners own persistence. Rhino adapters only apply planned values.
The editor displays configuration and assignment status without generating the
retired frame-typology identifier. No MCP surface or transport change is involved.

## Key design

- CW_1.08_FRAME_CONFIG is versioned JSON with delete, merge and hide mask payloads,
  preserving the existing validated binary-mask formats and grid dimensions.
- CW_1.09_FRAME_TYPE retains the current assignment JSON schema and values.
- Canonical keys are read case-insensitively. Nonblank canonical data takes
  precedence; malformed canonical data fails rather than silently falling back.
  Blank setup placeholders permit legacy fallback. Conflicting case variants fail.
- Legacy separate mask keys and CW_2.09_FRAME_ASSIGNMENTS remain read-only inputs
  for existing documents. Extrusion save/match/create/template/sync writers use
  canonical keys and clean retired keys in their owned scope. No bulk document
  migration is performed. Cladding-only saves preserve extrusion settings.
- Stop writing the retired typology identifier or showing it as an active UI field.
- Keep target offsets/dimensions and PCMatchSrf's cladding-only scope unchanged.
  PCMatchCrv copies config and assignments by equal grid indices.
- Default full-grid configuration has a valid combined payload; unassigned frame
  type remains absent. Generated curves retain their existing profile/formula keys.

## Files

Key/assignment/save and curve planners; live template/surface-sync adapters as
needed; editor controller/window/models; existing affected smoke suites and new
matching TEST folder, package README and EXET report.

## Usage

Existing panels load through legacy compatibility. Saving extrusions/both or
matching curve settings writes the new attribute pair. Spawn/Update read both old
and new panels. Installation is separate from this implementation request.

## Acceptance criteria

Canonical/legacy round trips preserve masks and all profile definitions, quantities,
parents and modifiers; canonical conflicts/malformed values fail; obsolete keys
are not newly written. Save scope, curve match, create, template, clear, surface
sync and generation remain functional. Default/empty assignments and blank PCpid
placeholders are covered. Relevant smokes and standalone Debug/Release builds pass.

## Risks and rollback

Old installed builds cannot understand the combined configuration. Do not install
or mutate production documents in this task. Keep legacy read compatibility and
existing Rhino transaction/Undo boundaries. Rollback requires restoring prior
attributes via Undo for documents edited with the new build.

## Future extensions

Any requested bulk migration or externally visible short configuration-code scheme
is a separate change; this task persists restorable configuration data.
