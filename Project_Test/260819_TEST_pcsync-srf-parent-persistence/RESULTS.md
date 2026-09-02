# PCSyncSrf Parent Persistence Test Results

Date: 2026-08-19

## Focused smoke

- Debug: passed.
- Release: passed.

Validated:

- a spanning surface reaches the final commit request as `0A=MPL-001`, `1A=0A`;
- every inferred logical cell is required;
- blank cells use the retained-blank sentinel;
- exact postcondition validation passes;
- missing or changed parent values fail validation.

## Regressions

- Structural-grid surface sync: passed; Rhino-native Brep subtest skipped outside a Rhino host.
- Base surface-sync workflow: passed.
- Blank-cell persistence: passed.

## Build and package

- Solution Debug: passed, 0 warnings, 0 errors.
- Solution Release: passed, 0 warnings, 0 errors.
- Package `1.0.54`: built successfully.
- Assembly/manifest identity: passed for both RHP products with distinct GUIDs.
- Packaged RHP SHA-256: `1FC8D787394BE61A3145E4A47E18ECBA5067490E495E13F7754D92734719E4F4`.

## Deployment state

Rhino initially caused `1.0.54` to stage. After Rhino closed, the staged installer activated the package at:

`C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.54\PanelCladdingEditor.rhp`

Registry-only validation passed, the current-user registry points to this RHP, and the installed SHA-256 matches the tested package. Live command validation remains pending Rhino restart.
