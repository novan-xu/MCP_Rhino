# PCUpdate CID selection PLAN

## Background

The user explicitly requested replacing PCUpdate's duplicate-PID rejection with
CID normalization and partial duplicate-CID handling. Parent and child panels
can share a PID while owning separate role-suffixed CIDs.

## Goals

Normalize selected parent/child CIDs before checking collisions. Update all
selected panels with unique CIDs; skip every member of each duplicate-CID group
and leave those panels selected. Support an all-duplicate batch without geometry
changes. Preserve dependencies of skipped panels.

## Architecture ownership

Domain owns source/selection/result data. Application owns case-insensitive CID
grouping and dependency ownership decisions. Infrastructure owns normalization,
Rhino geometry reconciliation, selection, and one coherent Undo scope. The command
only reports processed/skipped counts and duplicate identities.

## Key design

Validate source Breps/PIDs, then normalize role CIDs inside the update Undo scope
before grouping. Duplicate comparison trims values and ignores case. Read
dependencies using all selected sources so a skipped sibling's objects cannot
enter another panel's reconciliation. Ambiguous legacy unsuffixed dependencies
shared by multiple selected sources remain untouched and are reported. Existing
single-source legacy migration remains supported. Do not enumerate selected
source panels as dependencies. Preserve ambient-command Undo ownership and the
mode-aware replacement/deletion paths.

## Files

- Domain/PanelCladdingUpdateModels.cs and a focused Application selection service
- LivePanelCladdingUpdateService.cs and PanelCladdingUpdateCommand.cs
- Existing update-command and command-undo smoke projects
- Packaging/PanelCladdingEditor documentation/version and matching TEST/EXET

## Usage

Select panel Breps and run PCUpdate. Inspect any panels left selected and the
reported duplicate CIDs; resolve their identity conflict before updating them.

## Acceptance criteria

- Shared PID parent/child panels receive distinct canonical CIDs and both proceed.
- Duplicate CIDs across any selected PIDs skip all conflicting panels, while
  unique panels proceed; case/whitespace differences do not hide collisions.
- An all-duplicate batch succeeds with no dependency mutation.
- Skipped/opposite-role dependencies and ambiguous legacy dependencies survive.
- Normalization and geometry changes share one Undo scope; locked/hidden handling
  remains intact. Debug/Release builds and focused regression smokes pass.
- Stage version 1.0.83 after assembly identity and bundle hash checks.

## Risks and rollback

Legacy unsuffixed ownership cannot be inferred safely when a PID is shared; retain
and report those objects. Dependency-plan validation remains active. Service-owned
failures roll back via Undo; command-owned records remain Rhino-owned. Build/stage
only because independent host registry attestation is unavailable; preserve the
installed RHP. Source changes can be reverted independently of earlier fixes.

## Future extensions

Native Rhino acceptance of mixed selections and a separate explicit migration
workflow for ambiguous historical dependencies.
