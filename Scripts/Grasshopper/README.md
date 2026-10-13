# Stone Surface Bake — Rhino 8 Grasshopper

Link **StoneSurfaceBake.cs** directly to a Rhino 8 **C# Script** component. The
same external file configures the component on its first run and performs later
preview/bake runs. No code needs to be pasted into the editor. Rhino/Grasshopper are sufficient;
no LLM, MCP plug-in, extra DLL, Python runner, or plug-in installation is required.

## Link the external file

1. Add a fresh Rhino 8 **C# Script** component. Leave the default `x`, `y`, `out`,
   and `a` ports. Leave those ordinary ports unconnected.
2. Hold **Shift** and right-click the component. Enable **Script Input Parameter
   ("script")**.
3. Right-click the new `script` input and enable **Input is Path**.
4. Connect a **File Path** parameter pointing to
   `C:\01_Projects\MCP_Rhino\Scripts\Grasshopper\StoneSurfaceBake.cs`
   to `script`. A Panel containing that path also works. Select C# as the script
   language if prompted; the file declares its language too.
5. The component changes to **Stone Surface Bake** and configures the table below,
   including numeric/Boolean type hints, Item access, descriptions, persistent
   defaults, and outputs `Fins`, `Flats`, `BakedIds`, `Report`. The existing file
   connection stays in place. Bake starts **false**.

You can now connect four number sliders and a Boolean Toggle; there is no need
to name ports or choose type hints/access manually. The default numbers are
already stored on the inputs, so sliders are optional. Use one value per input.
Save the Grasshopper definition to retain the link and settings. Reopening it
does not require another paste or setup. For another definition, copy the configured
component together with its File Path parameter, or repeat the file-link steps.
If the script moves, update the File Path value. Edit the external file for code
updates; recompute/Discard Cache if the host has not refreshed the edited file.

The file references RhinoCode assemblies in the normal Rhino 8 installation folder;
its API compilation was checked against installed Rhino 8.35. Setup preserves the
special input object, its path setting, and its source wires. It only replaces
the fresh component's unused default ports, outside the active solve. An already
configured component keeps its input values and connections. Partially failed
setup disables the component and reports the error. Bake never turns on during
setup; the next solution previews the active Rhino document.

**SetupStoneSurfaceBake.cs is now a legacy optional bootstrap and is not needed.**
Use **StoneSurfaceBake.cs** for the external link.

McNeel documents the
[shared-script input](https://developer.rhino3d.com/guides/scripting/scripting-gh-csharp/#shared-scripts)
and the public
[component configuration API](https://discourse.mcneel.com/t/programmatically-creating-new-c-python-script-components/199692/15).
This delivery file uses Script-Mode, which supports shared scripts.

## Inputs

| Input | Default | Type / access | Meaning |
|---|---:|---|---|
| `FinBaseZ` | 0 | double / Item | Signed world-Z offset from the original fin edge, in inches |
| `FinHeight` | 5 | double / Item | Positive fin surface height in world +Z, in inches |
| `FlatBaseZ` | 0 | double / Item | Signed world-Z offset from the original flat-stone edge, in inches |
| `FlatHeight` | 5 | double / Item | Positive flat-stone surface height in world +Z, in inches |
| `Bake` | false | bool / Item | False: preview only. True: create/update matching Rhino blocks. |

For example, `FinBaseZ=-2` and `FinHeight=7` starts each fin 2 inches below its
original curve and extends it 7 inches upward. Flat settings are independent.
All values are converted from inches to the active Rhino document's units.

## Use

Open/activate the intended Rhino document. Set Bake to false, adjust the four
numbers, and inspect Fins/Flats plus the document name and counts in Report.
Then set Bake to true. The Boolean input performs the block bake; Grasshopper's
ordinary right-click Bake on preview surfaces does not reproduce block attributes.

The component searches all local top-level assembly instances whose names begin
`STN-MET-GA-`, excluding `_EL` and `_V`. It traverses nested definitions to find
flat `STN-MET-1...` and fin `STN-MET-2...` blocks. Normal, hidden, and locked source
assemblies are included. It selects the highest definition-local horizontal edge
and the longest among equally high edges, matching the established workflow.
Geometrically different tied edges produce an error instead of an arbitrary pick.

Each output is a standalone block named `WF_<source stone name>`. Its base point
is the selected edge's left endpoint after placement and the requested Z offset.
There is one surface per definition and one instance per nested stone occurrence.
The block instance has `Assembly_ID` equal to the exact source assembly name.
**Both the block instance and its member surface have Name equal to the WF block name.**

Both the instance and its surface use these layers:

- Fins: `01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Fin`
- Flats: `01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Back`

Missing layer hierarchy is created only with Bake=true. Existing layer visibility
is respected. Locked/reference target layers or output blocks must be resolved
before baking; the script does not unlock them.

## Recompute and repeat bakes

When Bake stays true, later recomputes synchronize the current settings. Matching
outputs are updated, and an unchanged solution performs no document writes.
Set Bake=false while adjusting previews if you want to defer those updates.

Two tracking attributes, `WF_Bake_Source` and `WF_Bake_Tool`, identify each source
assembly instance/nested path. This lets offset changes update the same occurrence
without stacking copies. A moved output may receive a new Rhino object ID.

The earlier 5-inch surface bake can be adopted by exact block name, Assembly_ID,
and original/current placement. Ambiguous duplicates or incompatible WF contents
are reported. Existing curve-only WF definitions are not converted by this script.
Changing a shared definition is blocked if it would affect an unmatched or nested
instance. Outputs whose sources disappeared are retained and reported; this script
does not silently delete unrelated or unmatched blocks.

Successful changes use one Rhino Undo record. Source assemblies are preserved,
and the script never saves or opens .3dm files from disk.

## Source and validation

`StoneSurfaceBake.cs` is the delivery file; it can be copied on its own.
Repository maintenance edits belong in `StoneSurfaceBake.entry.csx`,
`src/GrasshopperStoneBake/Infrastructure/Grasshopper/Live/LiveStoneSurfaceComponent.cs`,
and `src/GrasshopperStoneBake/Infrastructure/Rhino/Live/StoneSurfaceBake.cs`, then
run `Build-StoneSurfaceScript.ps1` to regenerate the delivery file.

The complete generated script was compiled/emitted against the installed
RhinoCommon/Grasshopper APIs with fresh default ports and configured typed/untyped
ports. A managed test of Rhino's real Script-Mode output method verifies that
unset output globals preserve the direct data-access results used by this script.
Tests also cover 45 placement/offset/height combinations, validation, identities,
legacy adoption, shared-definition ownership, and no-op comparisons. These checks
do not execute a live Grasshopper solution.
Actual automatic setup, save/reopen of the link, Grasshopper preview, and native
bake of this standalone script have not yet been run in the live host.
