# Panel cladding curve release inheritance

## Background

The user requested that baked curves inherit the source panel's release number.
Material surfaces already copy `CW_1.05_RELEASE`; extrusion curves currently omit it.
This is an explicitly requested construction follow-up to the role CID change.

## Goals

Copy `CW_1.05_RELEASE` onto all baked frame, intermediate, and merged curves. Carry
the current panel value through PCSpawnCrv, PCUpdate, and curve synchronization.

## Architecture ownership

- Application: extrusion metadata generation and sync change detection.
- Domain: existing curve snapshot/plan DTOs carry current/desired release strings.
- Infrastructure/Rhino: read live curve release text and apply planned metadata.
- Packaging and Project_Test: regression verification and the next product package.

## Key design

Treat the release number as text, preserving leading zeros and trimming surrounding
whitespace, consistent with surface release inheritance. Read the canonical metadata
key case-insensitively. Curves remain usable for panels without a release: omit the
key when baking, and remove stale curve release text during sync if the panel has
no release. Do not invent a value or change panel metadata.

Use the existing shared curve plan so PCUpdate's fresh attribute writes also inherit
release automatically. Include release-only drift in sync's MetadataChanged flag and
write the canonical key within its current Undo/rollback transaction.

## Files involved

Extrusion/spawn/sync planning services, live spawn/sync adapters,
`Domain/PanelCladdingModels.cs`, the exact standalone test exclusion in the server
project, package manifest/README, and `Project_Test/260923_TEST_panel-cladding-curve-release/`.

## Usage

Set the release number on a source panel. Bake with PCSpawnCrv. Use PCUpdate or
PCSyncCrv to refresh release metadata on existing managed curves.

## Acceptance

All curve kinds inherit the exact normalized release string; missing/stale curve
values are repaired; unchanged values are stable; missing panel values create no
fabricated metadata. Parent/child CIDs remain intact. Debug/Release builds and
focused plus affected existing regressions pass.

## Risks and rollback

Release metadata must participate in scoped curve updates without changing surface
or panel behavior. Use existing managed ownership and Undo boundaries. Revert the
scoped changes to restore earlier output; preserve the prior product for installer
rollback. Activation requires Rhino closed and independently verified host registry
access; the already established host route may be reused after fresh checks.

## Future extension

Additional inherited metadata should follow explicit field-level requirements.
