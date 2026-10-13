# PCpid planar measurement correction PLAN

## Background and authorization

The user reported a PCpid nonplanarity rejection while continuing the authorized
PCpid implementation on 2026-10-07. Read-only routed inspection of that exact live
panel found a planar degree-1, 2x2 surface and rectangular 90 by 180 boundary at
document tolerance 0.00001. The installed 1.0.92 assembly and post-start registry
lifecycle were independently verified. The failing message comes from testing
synthetic bounding-box corners after Rhino has accepted the actual face plane.

## Goals

Remove the contradictory box-based rejection without relaxing face planarity,
changing model tolerance, or flattening Rhino geometry. Preserve numbering,
dimensions, whole-layer context, selected-only writes, point order and Undo.

## Architecture and key design

A small Infrastructure measurement service owns Rhino face validation, oriented
plane selection and detached boundary measurement. Use polyline boundary vertices
for accurate local extents when available; use the detached boundary curve's plane
bounding box for curved boundaries. Construct four measurement corners on the
Rhino-validated face plane. A bounding box's thickness is not a surface planarity
test. Pure planning still validates its geometry contract defensively.

## Files and usage

Integrate the measurement service into LivePanelCladdingPidService; document the
Domain snapshot contract. Add focused managed/native regression coverage in the
matching TEST directory and exact Server test-glob exclusion. PCpid prompts stay
unchanged. Package as 1.0.93 and record EXET results.

## Acceptance

Test the reported boundary, all existing tutorial identities and dimensions,
rotation/translation/front reversal, trimmed and curved planar boundaries and
rejection of truly warped geometry. Compile native adapter tests; distinguish
executable managed tests from native startup limitations. Run Debug/Release plugin
builds and existing PID, setup, scope and point-order regressions. Verify assembly
GUID, package hashes and independent installation attestation when Rhino is closed.

## Risks and rollback

Use the plane normal for measurements, oriented to the original face front. All
projection is into measurement data only; mutation transactions remain unchanged.
Native host startup has previously failed before assertions; report that limit
instead of claiming live command verification. Preserve prior reachable RHP and
installer rollback copy. Do not activate while Rhino is running.

## Future extensions

Nonplanar facade numbering needs a separate explicit geometry contract.
