# Cladding surface release inheritance

## Background

The user explicitly requested that release numbers also apply to cladding surfaces.
PCSpawnSrf and PCUpdate already inherit the source panel's release through surface
spawn plans. PCSyncSrf does not read, compare, or refresh that metadata.

## Goals

Keep `CW_1.05_RELEASE` synchronized from each panel to its managed cladding surfaces,
including release-only changes. Retain the existing curve inheritance work.

## Architecture ownership

- Domain: surface sync snapshot and plan carry current and desired release text.
- Application: surface sync planning detects release drift; existing orchestration
  includes changed surfaces in the commit request.
- Infrastructure/Rhino: read live surface attributes and apply release changes in
  the existing sync Undo/rollback transaction.
- Project_Test: standalone application regression and affected existing suites.

## Key design

Use the panel's canonical release key case-insensitively and trim surrounding
whitespace, preserving leading zeros and arbitrary text. Compare against the raw
surface value so non-normalized values are repaired. On sync, remove old key case
variants and write the canonical current value; omit it when the panel has no value.
Release-only changes must not regenerate panel types or modify geometry. Curve-only
sync remains scoped to curves. Spawn/update retain their required-release contract.

## Files involved

`PanelCladdingModels.cs`, `PanelCladdingSurfaceSyncPlanningService.cs`,
`LivePanelCladdingSurfaceSyncRepository.cs`, the server's exact standalone test
exclusion, and `Project_Test/260923_TEST_panel-cladding-surface-release/`.

## Usage

PCSpawnSrf copies the panel release onto new surfaces. PCUpdate regenerates surface
metadata from the panel. PCSyncSrf refreshes release metadata on existing surfaces.

## Acceptance criteria

New and updated surface plans inherit release text for all regions. Surface sync
repairs missing/stale/whitespace values, leaves equal values alone, and clears old
release metadata when the source is empty. The application commits release-only
changes, does not rewrite panel types for them, and does not leak between panels or
from curve-only scope. Focused and affected regression suites and Debug/Release
builds pass.

## Risks and rollback

Keep existing ownership, PID/CID, geometry, and transaction boundaries. Revert only
this follow-up's fields and plumbing to restore the prior behavior, preserving the
pre-existing working tree. Live Rhino verification is separate from automated
application tests; this change does not activate an installed plug-in.

## Future extensions

Additional inherited metadata should have explicit field-level requirements.
