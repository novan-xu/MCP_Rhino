# 261010_EXET_stone-bake-lintels

## Corresponding plan

[261010_PLAN_stone-bake-lintels](../Project_Plan/261010_PLAN_stone-bake-lintels.md). Execution date: 2026-10-10, following the user's explicit lintel-support request and annotated top-view example.

## Related artifacts

[TEST folder and usage](../Project_Test/261010_TEST_stone-bake-lintels/README.md), `LintelChecks.csx`, and `Run-Validation.ps1`. The existing standalone `261009_TEST_grasshopper-stone-bake` project adds the optional lintel checks and two new compilation bindings. No commit or PR requested or created.

## Result and delivered scope

Updated the project-linked `V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/01_grasshopper/05_STN/StoneSurfaceBake.cs`.

`STN-MET-3...` is now the lintel family. Its back-face baseline uses the existing uppermost local-Y horizontal edge selection, consistent with the highlighted top-view edge and existing fin/flat convention. Edge selection was extracted into a managed helper without changing fin/flat selection semantics. The baseline is transformed to the source placement, then receives a signed world-Z offset and independent positive world-Z height.

Added `LintelBaseZ=0`, `LintelHeight=5`, in inches, and a separate `Lintels` preview output. Settings, model-unit conversion, validation, report counts, preview disposal and direct member layer routing now handle all three families. Lintel output uses the `Surfaces - STN detailed::Lintel` child layer and retains STN_ID/Assembly_ID attributes.

Prior configured components schedule an additive GH-Undo-recorded upgrade that appends the new controls/output. Existing parameter objects, indices, data, wires and the script link are retained. Fresh setup creates the expanded layout. Unknown or partial/custom layouts are rejected without replacing existing ports; upgrade failure after partial changes disables the component and reports the GH Undo recovery action.

Missing-only behavior is retained: an existing source assembly output is skipped before geometry traversal, and existing shared definitions/instances remain unchanged. No live .3dm/.gh, MCP capability, Router, plugin or registry operation was performed by this construction.

## Deviations from plan

None. Staged production snapshots stayed under ignored `.validation/stone-bake-lintels/`, outside TEST compilation globs. The older repository delivery was not regenerated or changed.

## Problems found and resolved

Simply adding the new expected port names would make a previously configured component fail the fresh-component check. An explicit previous-layout upgrade now appends only the lintel ports and retains existing wiring. Family selection previously used a fin/flat boolean; it now routes settings, previews and layers explicitly by family so lintels cannot accidentally inherit flat settings.

## Test record

Both Debug and Release passed for staging and again for the exact deployed file, exit code 0. Commands and scope are recorded in [TEST README](../Project_Test/261010_TEST_stone-bake-lintels/README.md). The whole script compiles/emits against installed Rhino 8 and Grasshopper libraries with five binding configurations. New actual emitted-script checks cover lintel naming/exclusions, settings/defaults/validation, layer/preview routing, baseline edge cases, 45 placement combinations and component-layout detection. Syntax checks verify scheduled additive upgrade and GH Undo order. Existing missing-only, visibility, geometry, identity, null-output and Undo regressions remain passing.

The linked source was hash-checked before backup and immediately before replacement. A create-new sibling backup was flushed and verified, and the deployed hash matched the tested staging revision.

Before SHA256: `03D7692DF207A8742EA3766B97527AC03D9029490D167EF0C986C20355C04ACF`.
Delivered SHA256: `2B250DA9DA620C38357876F020AD1B2B4047FD8B3968E9C2C926C2F86108A5E3`.

No server/Router/plugin changes were made, so standalone Debug/Release validation was appropriate; solution/RHP rebuilding was unnecessary. No live solve, port upgrade, native geometry bake, object count or native Undo is claimed as executed.

## Acceptance alignment

Lintel detection, independent controls, baseline construction, output routing, old-layout migration design, existing output preservation and compile/helper regressions are implemented and verified. The provided screenshot supplies orientation/naming context; actual live lintel block geometry has not been inspected. Native Grasshopper/Rhino acceptance remains unexecuted.

## Rollback verification

The sibling `StoneSurfaceBake__before_lintels_20261010.cs` has the exact before-change hash above. Code can be restored from that file. If the component has subsequently upgraded, its GH Undo event must also be restored (or a fresh old-layout component used). Rollback itself was not run; no live component was changed during construction.

## Remaining items

The live script will expose the appended ports when loaded/recomputed. An existing baked assembly is intentionally skipped as a whole; adding lintels does not revise its contents. Back-edge selection assumes the established local +Y outline convention. Native preview/bake validation on actual model geometry is not part of the reported test evidence.

## Conclusion

The requested lintel extension is delivered to the selected linked script with a verified backup. Debug and Release checks pass, including all prior missing-only safeguards. No live bake was performed.

## GitHub publication - 2026-10-13

Published with [PR #11](https://github.com/novan-xu/MCP_Rhino/pull/11), source commit `cae9c71`.
The branch integrates `origin/master` at `f6dc9d0`. Publication validation is recorded in
`Project_Exet/261012_EXET_live-custom-scripts.md`. Earlier acceptance boundaries in this record remain applicable.
