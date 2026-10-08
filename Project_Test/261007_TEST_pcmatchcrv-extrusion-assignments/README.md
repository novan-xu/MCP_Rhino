# PCMatchCrv extrusion assignment TEST

Run from the repository root:

```powershell
& ./Project_Test/261007_TEST_pcmatchcrv-extrusion-assignments/Verify-Regression.ps1
```

Verified on 2026-10-07: all four smoke suites passed in Debug and Release;
standalone editor builds passed in both configurations with zero warnings/errors.
The runner exited 0. Logs are saved here as `<configuration>-<suite>.log`.

The existing curve-topology smoke now exercises complete PCMatchCrv assignments:

- A 2H/1V source with assigned perimeter frames, a merged horizontal run, a
  vertical segment and a hidden segment matches two differently sized targets.
- Additive 1D/0D profiles retain quantities, fixed/spacing formulas, parent links,
  category/source metadata and positive/negative length modifiers.
- Existing target assignments are replaced together with masks, even when an old
  assigned segment becomes missing. Stale signatures are removed.
- Target offsets, dimensions, material cells, PID and unrelated metadata survive;
  frame typology uses each target's geometry and system code.
- Generated extrusion plans have target-sized curves and the expected formulas
  `(LL+4)*2`, `3`, `(LL+4)/24`, `(LL-2)*2` and `LL/24`; hidden/missing atoms stay suppressed.
- Source data remains unchanged, repeated matches are idempotent, legacy v1
  assignments normalize successfully, and empty source assignments remove target
  payload/typology keys, including lower-case variants.
- Malformed source/target assignments, inconsistent assignments on a merged run
  and mismatched grid counts reject without a partial match plan.

Reused suites:

- `260819_TEST_panel-cladding-curve-topology`: new matching assertions and existing PCCreate behavior.
- `260819_TEST_panel-cladding-hide-mask`: hide-mask behavior and sparse transfer.
- `260805_TEST_panel-cladding-match`: independent PCMatchSrf scope and command identity.
- `260820_TEST_panel-extrusion-assignment`: assignment serialization, formulas and frame typology.

No live document, project workbook or installed plug-in was changed. Rhino command
selection/Undo and display in a live PCEditor session remain untested. No UI automation
was used. The optional real-PDF/workbook extraction branch of the assignment smoke
was not invoked for this change.
