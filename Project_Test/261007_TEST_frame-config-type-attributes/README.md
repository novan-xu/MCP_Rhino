# Frame config/type attributes TEST

Run from the repository root:

```powershell
& ./Project_Test/261007_TEST_frame-config-type-attributes/Verify-Regression.ps1
```

The runner executes 14 existing smoke suites in Debug and Release, then builds the
standalone editor in each configuration. It writes 28 smoke logs and two build logs
in this folder. No Rhino production document or workbook is used.

Verified 2026-10-07: final runner exit 0; all 28 smoke runs passed and both builds
completed with zero warnings/errors. `git diff --check` passed. The reused UI
suite's two detached-control PNG renders were copied here; its historical tracked
PNG files were restored to their pre-task contents. The footer render was inspected.

## Persistence contract

| Object User Text key | Value |
| --- | --- |
| `CW_1.08_FRAME_CONFIG` | `{"v":1,"delete":"<encoded mask>","merge":"<encoded mask>","hide":"<encoded mask>"}` |
| `CW_1.09_FRAME_TYPE` | Existing assignment JSON: `v`, `f`, `s`, `d`, `x`; schema and calculation values unchanged |

Config includes all three masks even for a default grid. Frame type is absent when
unassigned. Old typology is never regenerated. Separate 2.10/2.11/2.12 masks and
2.09 assignments are accepted only as legacy input and removed by extrusion writes.

## Coverage

- Codec migration: complete/legacy round trips, lower-case keys, blank PCpid
  placeholders, new-key precedence, malformed/version/duplicate-field rejection,
  conflicting case variants, grid mismatch and save-time old-key removal.
- PCMatchCrv: old/new sources, complete assignment replacement, empty-source clearing,
  hidden/merged/perimeter assignments, quantities, parent links and modifiers;
  generated curves retain target dimensions and profile formulas.
- Save/reload and scoped save: config/type persistence, cladding-only isolation,
  topology/assignment retention during edits, new UI summary and retired-field removal.
- PCCreate/PCClear: reset or remove new and legacy frame attributes without losing
  unrelated panel identity data.
- PCCrvTemplate: combined config retains deleted/hidden state with changed merge
  runs; configured and no-op panel behavior remains covered.
- Layout reconciliation, surface sync, structural-grid sync, surface matching and
  PCUpdate retain their existing regression coverage.

The assignment/migration tests live in
`Project_Test/260820_TEST_panel-extrusion-assignment/Program.cs`; the matching tests
live in `Project_Test/260819_TEST_panel-cladding-curve-topology/Program.cs`.
Other suites are listed explicitly in the runner. Older sparse-mask codec checks
remain as legacy compatibility tests; production writes now use combined config.

Live Rhino selection, commit/Undo and production installation are outside this
verification. Tests construct local WPF objects and render detached controls;
they do not automate Windows or interact with the user's Rhino session.

## Installation verification

Version 1.0.96 was installed at the user's subsequent request on 2026-10-07.
`Verify-Activation.ps1` exited 0 after independent Install/Validate and a separate
post-exit Validate host process. The packaged and registered RHP hash, exact command
inventory, three advancing key timestamps, independent registry-provider agreement
and prior-version rollback hash all passed. See `activation-summary.json` and
`package-summary.json`. Rhino remained closed; loaded-RHP verification is pending
the next launch. No additional registry probe was created.
