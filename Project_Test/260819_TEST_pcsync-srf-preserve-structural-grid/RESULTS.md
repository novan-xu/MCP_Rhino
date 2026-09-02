# PCSyncSrf Preserve Structural Grid Test Results

Date: 2026-08-19

## Focused smoke

- Debug: passed.
- Release: passed.

Validated:

- stored `V0=22.5` survives surface sync with no inferred vertical boundary;
- new inferred surface boundaries are unioned without duplicates;
- curve scope remains authoritative for offset removal;
- the preserved grid reconstructs `1A=0A`;
- live surface-sync grid construction uses effective scope-aware offsets.

## Regressions

- PCSyncSrf parent persistence: passed.
- Structural-grid surface sync: passed; Rhino-native Brep subtest skipped outside a Rhino host.
- Base surface-sync workflow: passed.
- Extrusion/curve sync: passed.

## Build and package

- Solution Debug: passed, 0 warnings, 0 errors.
- Solution Release: passed, 0 warnings, 0 errors.
- Package `1.0.55`: built successfully.
- Assembly/manifest identity: passed for both RHP products with distinct GUIDs.
- Packaged RHP SHA-256: `1FD89973102971DEF470749C44319645C3E398A23D7E2D5C98AE97C8DE9FE61A`.

## Deployment state

After Rhino closed, staged package `1.0.55` was installed at:

`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.55\PanelCladdingEditor.rhp`

Registry-only validation passed, the current-user registry points to this RHP, and the installed SHA-256 matches the tested package. Live command validation remains pending the next Rhino launch.
