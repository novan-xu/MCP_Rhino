# PCMatch Parent-Cell Fidelity Execution

## Corresponding Plan

- Plan: `Project_Plan/260819_PLAN_pcmatch-parent-fidelity.md`
- Execution date: 2026-08-19

## Related Artifacts

- Focused test: `Project_Test/260819_TEST_pcmatch-parent-fidelity/`
- Existing regressions:
  - `Project_Test/260805_TEST_panel-cladding-match/`
  - `Project_Test/260818_TEST_panel-cladding-match-parent-cells/`
  - `Project_Test/260818_TEST_panel-cladding-match-topology-masks/`
- Package: `Packaging/PanelCladdingEditor/artifacts/PanelCladdingEditor-1.0.43/`
- Installed plug-in: `%LOCALAPPDATA%\PanelCladdingEditor\plugin\1.0.43\PanelCladdingEditor.rhp`
- Commit / PR: not created.

## Execution Result / Actual Scope

- `PCMatch` still validates the complete source parent graph through
  `PanelCladdingRegionService.Resolve`.
- After validation, cell writes now use each parsed source material/reference token rather than the
  region service's canonicalized equivalent graph.
- Added a shared pure user-text plan application function. The live Rhino adapter uses this same
  transformation before `ModifyAttributes`, while the focused console regression can verify it
  without requiring Rhino native state.
- Unrelated target metadata remains preserved; planned target configuration keys are deleted and
  rewritten case-insensitively as before.
- Package version advanced from 1.0.42 to 1.0.43.

## Deviations From Plan

- The first test attempted to construct Rhino `ObjectAttributes` in a console host and correctly
  failed because `rhcommon_c` is available only inside Rhino. Instead of recording a misleading
  skip, the delete/write transformation was moved into a pure application function called by the
  live adapter and tested directly.
- Rhino was running during the first deployment attempt, so 1.0.43 was staged. After Rhino closed,
  the staged installer activated and validated the package.

## Problems Found and Fixed During Construction

- The previous regression fixture used an already-canonical graph, for which canonicalization and
  exact copying produce identical output. A reverse-parent fixture (`0A=1A, 1A=MPL-001`) now proves
  graph fidelity independently of material-region equivalence.

## Test Record

- Focused Debug test exit 0:
  - exact reported `0A/0B/1A/1B` values passed;
  - non-canonical parent direction passed;
  - shared live-adapter transformation passed and preserved unrelated metadata.
- Existing Debug PCMatch, parent-cell, cycle, topology-mask, and target-offset regressions passed.
- `dotnet build MCP_Rhino.sln -c Debug -m:1`: exit 0, zero warnings/errors.
- `dotnet build MCP_Rhino.sln -c Release -m:1`: exit 0, zero warnings/errors.
- Focused Release plus existing parent-cell and topology-mask tests: exit 0.
- Package build 1.0.43: exit 0.
- Release assembly identity: non-empty manifest-matched plug-in id
  `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`; repository plug-in ids remain distinct.
- Packaged RHP SHA-256:
  `2417E02994945C142695F4F2768FC3BD581D17F5A14A8B4983080F5F87DC93A4`.

## Acceptance Alignment

- Reported four values copied exactly: passed.
- Valid non-canonical parent graph preserved: passed.
- Invalid cycle rejection retained: passed.
- Target H/V offsets untouched: passed.
- Segment/merge-mask transfer retained: passed.
- Debug/Release/package/identity validation: passed.

## Rollback Verification

- PCMatch remains one Rhino Undo record.
- The installer retained 1.0.42 in its rollback area and registered only the 1.0.43 RHP for startup.

## Current Remaining Item

- Restart Rhino before retesting PCMatch so the process loads 1.0.43.

## Conclusion

PCMatch now treats the source parent-cell graph as configuration truth instead of replacing it with
an equivalent canonical graph. The corrected build is tested, packaged, identity-validated,
installed, and registry-validated as version 1.0.43.
