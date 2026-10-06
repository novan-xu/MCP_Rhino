# Panel cladding object names PLAN

## Background and authorization

The user requests short CID-based Rhino names for source panels and every produced
dependency: CID_BKT_S1_06_14 becomes S1_06_14. This explicitly authorizes the change.

## Goals and design

One shared Application formatter removes the leading CID_BKT_ prefix (case
insensitive), retaining the complete remainder including corner and dependency
suffixes. For other CID_ identifiers, remove only CID_; preserve arbitrary custom
identifiers. Trim surrounding whitespace. Never change CID metadata for naming.

Normalize panel names wherever existing panel identity normalization runs: create,
editor save, match, spawn, sync, and update. Use the current stored CID when no
unit-type CID rewrite applies, without inventing missing identifiers. Missing CID
leaves an existing name alone. Dependency spawn, update/rebuild, and sync all use
the same formatter and full dependency CID, not just the extrusion code.

Sync must detect name-only changes and commit them even when geometry and other
metadata match. Keep panel name changes distinct from cladding layout changes.
Preserve existing selection, ownership, skipped-panel, Undo, and rollback rules.

## Architecture and files

Application owns string formatting and change detection; Domain carries current
names/change flags in sync snapshots/plans. Infrastructure reads/writes Rhino
ObjectAttributes.Name using existing transactions. Touch the CID service, live CID,
spawn/update/sync adapters, sync models/planner/orchestrator, focused tests, package
documentation/version, and matching TEST/EXET artifacts. No MCP/UI changes.

## Usage and acceptance

Run PCUpdate on existing configured panels to refresh panel and dependency names;
new outputs use short names automatically. Examples include S1_06_14,
S1_06_14-P, S1_06_14-P-0A, and S1_06_14-P-INT_B1.
- Panel, surface, and curve names preserve leading zeros and complete suffixes.
- Naming does not change full CIDs/PIDs, layer/material, or topology semantics.
- Name-only sync changes enter the existing commit path; repeated sync is stable.
- Missing CID preserves names; unit-type CID correction precedes panel naming.
- Focused Debug/Release tests, product builds, GUID and package-hash gates pass.
- Stage 1.0.86 while Rhino is open; do not replace its loaded RHP.

## Risks, rollback, and future work

Managed object names are intentionally overwritten from CID. Existing names are
included in original attribute snapshots/Undo; no bulk live-document rename is
performed during development. Source changes can be reverted independently.
Other project-specific prefix conventions can be added if requested. Installation
and native Rhino acceptance follow the existing repository activation gates.
