# Grasshopper stone surface script

## Background

The user explicitly requested a standalone Grasshopper C# script on 2026-10-09,
following the completed MCP stone surface bake. It must load from a file path
and run independently of an LLM. This is authorized capability construction.

## Goals

- Five item inputs: FinBaseZ, FinHeight, FlatBaseZ, FlatHeight, Bake.
- Signed world-Z base offsets; separate positive surface heights; inches as the
  numerical unit, converted to Rhino model units. Suggested values: 0,5,0,5,false.
- Search local top-level STN-MET-GA- assemblies by prefix; continue excluding
  _EL and _V and discover nested STN-MET-1/2 stones with full transforms.
- Preserve WF_<stone> names, left-end base points, and parent Assembly_ID.
- Both fin instance/member surfaces use
  01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Fin; flat instances/members use
  01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Back. Geometry names match WF names.

## Ownership and delivery

Deliver a single generated Script-Mode .cs at Scripts/Grasshopper/StoneSurfaceBake.cs.
Rhino implementation source belongs in a standalone Infrastructure/Rhino/Live
folder, separate from the MCP server; a small script entry binds GH variables.
No server tool, runtime transport, plug-in install, or registry change is needed.
This explicitly requested user-run script is not an agent-side fallback transport.

## Design

Bake=false reads the active Rhino document and returns preview surfaces and a
report without changing layers, definitions, attributes, or model objects.
Bake=true synchronizes matching outputs in one Rhino Undo record. Use stable
source assembly-instance plus nested member-path identifiers to update owned
instances, never accumulating duplicates on recompute. Adopt legacy bake output
only by exact WF definition, Assembly_ID and placement, rejecting ambiguity.
Validate the whole scope and shared-definition ownership before any mutation.
Update only the canonical single-surface WF definitions from this workflow;
reject linked, arbitrary, nested, or externally used conflicting definitions.
Do not delete sources or stale unrelated output. When a source disappears report
unmatched managed outputs rather than removing them silently.

Normalize definitions to XZ rectangles; place the source edge by the full nested
transform and shift its world Z by the appropriate family offset. Keep extrusion
world +Z independent of source scale/rotation. Update definition height while
retaining names. Required layer hierarchy is created only during Bake=true.

## Files

- src/GrasshopperStoneBake/Infrastructure/Rhino/Live/StoneSurfaceBake.cs
- Scripts/Grasshopper/StoneSurfaceBake.entry.csx, Build-StoneSurfaceScript.ps1,
  generated StoneSurfaceBake.cs and README.md
- Project_Test/261009_TEST_grasshopper-stone-bake/ standalone compilation/math tests
- Matching EXET record

## Usage

Rhino 8 C# component, Script-Mode external script input with Input is Path. Add
the five named inputs and documented outputs; point script to the generated .cs.
Use Bake=false while adjusting preview, then true to synchronize Rhino blocks.

## Acceptance

Compile the entire external script with a Roslyn script compilation and the
installed RhinoCommon API. Test both family settings, signed offsets, rotated /
mirrored / scaled placement, deterministic occurrence keys and adoption matching,
input validation, shared-definition conflicts, and no-op comparison logic.
Static/native compilation must not be reported as a live Grasshopper solve. If
the installed MCP graph-authoring API cannot load script source, document the
manual connection step rather than bypassing the supported runtime route.

## Risks and rollback

Changing a shared definition affects all its references; preflight must ensure
all references belong to matched eligible outputs. Bake=false is read-only;
successful changes are one Undo. A failed mutation is rolled back through that
record when possible, with an explicit error if rollback fails. Never save the
Rhino model or modify its source assemblies.

## Future extensions

Optional assembly-prefix / layer inputs, explicit target-document input, and
compiled Grasshopper packaging can be added separately if requested.

## 修订记录（2026-10-09）

### Automatic component setup

The user requested automatic component naming, input/output names, type hints,
and access selections instead of manual port configuration. Add a one-time C#
bootstrap for a fresh Rhino 8 C# component. It must use the installed public
Grasshopper/RhinoCode APIs, configure the five inputs and four outputs with safe
defaults, and load the external StoneSurfaceBake.cs file. Schedule changes outside
the component's active solve. Refuse to reconfigure a component with existing
wires rather than discard user connections. Setup must not bake Rhino geometry.
Compile the bootstrap against the installed assemblies and document any remaining
live-host validation limitation. No MCP surface, plug-in, or registry changes.

## 修订记录（2026-10-10）

The user clarified that linking an external file must also cover initial setup;
copy/pasting a bootstrap is not the desired workflow. Replace the embedded-only
setup route with a shared Script-Mode entry that can load from the special script
input, configure its own ports safely, and retain the external link. Prefer a
single file for setup and subsequent baking if the installed script environment
exposes the executing component. Read inputs and write outputs through that
environment rather than compile-time variables that do not exist before setup.
Keep all existing bake logic, naming, layers, defaults, and safe false initial
Bake state. Compile both fresh/default and configured input contexts and document
the exact file-link steps. No host/UI/registry automation is authorized or needed.

## 修订记录（2026-10-10）：assembly blocks with direct stone surfaces

The user explicitly changed the output structure after confirming that shared
stone definitions cannot hold occurrence-specific Assembly_ID values. Bake one
assembly block per source assembly instance, with direct stone surfaces inside
the assembly definition (no individual stone blocks). Use WF_<assembly name>
for assembly definitions; surface names remain WF_<stone name>. Each surface's
object attributes carry STN_ID=<source stone name> and Assembly_ID=<source assembly
name>. Preserve the five inputs, inch conversion, world-Z offsets/heights, layers,
source assemblies, and the project-copy-only delivery instruction.

Share a baked assembly definition where all member surfaces agree in assembly
coordinates. For source transforms requiring different local surfaces to preserve
world +Z, use an instance-specific definition suffix. Track baked assembly instances
separately from legacy individual stone outputs. Preflight all matches and ownership;
create/update assembly output before removing only exactly matched prior WF stone
instances. Leave unrelated/unmatched blocks and unused legacy definitions alone.
Repeat baking must update rather than duplicate output and repair missing surface
attributes even with unchanged numeric inputs. Test actual delivered assembly
planning/identity helpers, full compilation, and existing Undo guards. Native host
bake/migration/Undo still require an explicit live acceptance run.
