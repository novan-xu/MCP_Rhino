# PCMatchSrf Explicit Parent Cells Test Results

Date: 2026-08-19

## Focused smoke

- Debug: PASS
- Release: PASS

Validated:

- reported 2x4 source graph copies all eight cells;
- `1A=0A`, `1B=0B`, `1C=0C`, and `1D=0D` remain parent references;
- an explicit parent survives topology collapse;
- an absent topology-hidden source cell remains omitted;
- target offsets, masks, type/signature, and unrelated metadata remain unchanged.

## Live diagnostic

- Source `PID_BKT_N1_01_07`: eight explicit cell keys present.
- Target `PID_BKT_N1_01_05`: only four owner cell keys present after the reported match.
- Installed plug-in: `1.0.47`.
- Live diagnostic mutations: none.

## Regressions

The logical-cell, logical-cell-cleanup, parent-fidelity, parent-cell, base match, and topology-mask Debug smokes all passed.

## Builds and package

- Solution Debug: PASS, 0 warnings / 0 errors
- Solution Release: PASS, 0 warnings / 0 errors
- Package `1.0.52`: PASS
- Packaged Release RHP identity: PASS
- Packaged RHP SHA-256: `CE25B38ACBFB0A359547D05BAA6D32D35D4349803FFA11DCAB61694B01D4D36F`
- Installation: PASS
- Installed RHP: `C:\Users\nxu\AppData\Local\PanelCladdingEditor\plugin\1.0.52\PanelCladdingEditor.rhp`
- Registry-only validation: PASS
