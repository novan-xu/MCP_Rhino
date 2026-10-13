# 261010_PLAN_stone-bake-lintels

## Background

The user requests lintel stone support in the existing linked bake script, using the back-face baseline highlighted in the supplied top-view image and independent baseline offset/surface height controls. The image labels the assembly `STN-MET-GA-3010S-020`; extend nested stone detection to the corresponding `STN-MET-3` family.

## Goals

Add lintels alongside fins and flats while preserving missing-only assembly baking, current visible-source scope, direct surface identities, source preservation and Undo behavior. Use the same back-edge selection convention as existing families: the uppermost local-Y horizontal stone-outline edge, left-to-right, with world-Z offset and height in inches.

## Architecture ownership

Standalone linked C# Grasshopper script at `V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/01_grasshopper/05_STN/StoneSurfaceBake.cs`. Do not regenerate from the older repository adapter. No MCP/plugin/Router change or live model bake is needed.

## Key design

- Recognize `STN-MET-3...` as lintel stones, retaining case-insensitive matching and _EL/_V exclusions.
- Add `LintelBaseZ=0` and `LintelHeight=5` (inches), independent of fin/flat values. Validate all offsets as finite and heights as positive and above model tolerance after conversion.
- Add a `Lintels` preview list and dedicated `Surfaces - STN detailed::Lintel` layer. Retain direct member STN_ID and parent Assembly_ID metadata.
- Append new inputs after existing Bake and append the preview output after Report so existing port positions remain stable. Detect the prior configured five-input/four-output component and schedule an additive, GH-Undo-recorded upgrade. Keep all existing parameter objects, data, wires and the file link. Fresh components still configure automatically; unknown custom layouts fail without rewiring.
- Existing bake identities continue to skip the whole source assembly. Do not update prior output definitions or add lintels into already-baked assemblies.
- Extract the established back-edge selection into a managed helper for representative lintel, ambiguous-edge and world-placement regressions without native model changes.
- Stage in ignored `.validation/stone-bake-lintels/`, compile/test, verify source hash, create a clearly suffixed sibling backup, update the linked file, and validate the exact delivered bytes.

## Files

Linked script; `Project_Test/261010_TEST_stone-bake-lintels/`; existing standalone harness registration; matching PLAN/EXET artifacts. No maintained production source inside TEST.

## Usage

Reload/recompute the same file-linked component. Old configured components gain two inputs and one output without disconnecting existing controls. Bake=false previews missing assemblies; Bake=true adds them. LintelBaseZ is a signed world-Z baseline offset; LintelHeight is a positive world-Z surface height, both in inches.

## Acceptance

Debug/Release compilation and emit with fresh, previous configured, and lintel configured bindings. Managed tests cover family recognition/exclusions, defaults and independent settings/validation, back edge and corner placement, layer routing, new output wiring and non-destructive port upgrade. Rerun missing-only, visibility, geometry and Undo regressions. Distinguish compile/helper/structural evidence from unexecuted live GH/model validation.

## Risks and rollback

Screenshot-based naming uses the existing numeric-family convention; the actual live lintel definition geometry is not inspected by this construction. Back means the established local +Y outline edge, matching the image orientation. Missing-only skips an entire previously baked assembly, including any newly supported lintels inside it. Restore the verified sibling script backup to roll back. No .3dm or .gh is written by this construction.

## Future work

Any explicit rebake/update mode or alternative back-edge orientation requires a separate request.
