# Grasshopper stone surface script execution

## Corresponding plan

[Plan](../Project_Plan/261009_PLAN_grasshopper-stone-bake.md), implemented 2026-10-09
under the user's explicit request for an independently runnable C# script.

## Related artifacts

- [Delivery script](../Scripts/Grasshopper/StoneSurfaceBake.cs)
- [Setup and usage](../Scripts/Grasshopper/README.md)
- [Tests](../Project_Test/261009_TEST_grasshopper-stone-bake/README.md)
- Canonical Rhino implementation: `src/GrasshopperStoneBake/Infrastructure/Rhino/Live/StoneSurfaceBake.cs`
- No commit/PR or installation requested or performed.

## Delivered scope

A self-contained Rhino 8 shared Script-Mode C# file. It exposes five inputs for
separate fin/flat base Z offsets and surface heights plus Bake. Numerical values
are inches, with suggested defaults 0,5,0,5,false. Discovery uses GA- prefix and
excludes _EL/_V. The established upper-edge/left-end selection and world +Z
placement are preserved. Both surface objects and block instances use the WF
name and the user-specified Fin/Back layers. Assembly_ID stays on each instance.

Bake=false is read-only preview. Bake=true creates/updates outputs with source
path tracking, legacy matching for the earlier surface output, scoped shared
definition validation, and one Undo. Unchanged recomputes do not write. Unmatched
outputs are not deleted. The script runs without an LLM, MCP, or new RHP install.

## Deviations

No Grasshopper document/component was injected into the live session. Existing
MCP graph authoring does not expose the required variable script ports/source
binding. The requested file is delivered with the direct native Grasshopper
file-path connection steps; no alternate runtime transport or UI automation was
introduced. Because this does not change MCP server/plugin/tool surfaces, only
the standalone artifact requires compilation, not another server release.

## Issues resolved

Shared file scripts require Script-Mode rather than SDK-mode Script_Instance.
The generated delivery file therefore combines a small script entry with the
standalone Rhino adapter. Both typed and untyped input bindings are compiled to
catch accidental type-dependent expressions. Output wrappers are re-resolved
after definition replacement so the script does not rely on stale RhinoObjects.

## Test record

Full generated-script Roslyn compilation and emit passed against installed
RhinoCommon. Arithmetic/policy tests passed for separate inputs, 45 world placement
cases, prefix/exclusions, source keys, legacy matching, shared-definition reference
ownership, layer paths, and no-op detection. See the TEST artifact for exact
commands and the distinction between managed checks and native host checks.
Logs are ignored `.validation/grasshopper-stone-bake-*.log`.

## Acceptance and rollback

The requested .cs artifact and documented five-input contract are delivered.
No live model, loaded plugin, or registry was modified by this construction.
The script contains one-Undo bake and failed-operation rollback; those native
paths have not been executed during this task. Live Grasshopper acceptance is
documented for a disposable fixture, not falsely reported as passed.

## Outstanding items

Connect the script in Grasshopper using the supplied instructions, then perform
native preview/bake acceptance. No new MCP capability or Rhino restart is required.

## Conclusion

Standalone file deliverable is implemented and compile/policy tested. The
Grasshopper-native solve/bake remains untested in this session.

## Automatic setup revision — 2026-10-09

The user requested that a script configure the C# component's names and input
selections automatically. Added `Scripts/Grasshopper/SetupStoneSurfaceBake.cs`:
paste once into a fresh Rhino 8 C# component, then save/close. It schedules setup
outside the current solve, records a GH undo event, creates the five typed Item
inputs with persistent defaults (0,5,0,5,false), creates the four named outputs,
renames the component, and supplies the original external script through a typed
file-backed `ScriptType`. It refuses wired/non-default components and checks the
source file before mutation. Partial setup failure disables the component. The
Rhino bake adapter and delivery script are unchanged.

The linked script remains Script-Mode; only the one-time bootstrap uses SDK mode.
This avoids requiring manual input/output/type-hint/access choices or an Input
is Path menu selection. README now leads with automatic setup; the earlier manual
route remains available. No additional MCP or installation work was performed.

Validation: full bootstrap compilation and emit against installed Rhino 8.35
Grasshopper/RhinoCode assemblies, plus the existing generated-script and 45-case
geometry/policy tests, passed with the standalone test project in Debug and
Release (exit 0). The harness supplies Rhino 8's concrete SDK base binding for
offline Roslyn; it does not exercise Rhino's script transformation. The first
compile exposed the bare Roslyn/legacy GH_ScriptInstance mismatch, which was
corrected in this harness binding. API metadata also confirms BaseScriptParam is
the actual special input type installed by UsingScriptInputParam.

Automatic setup in the live Grasshopper host, linked-file save/reopen, and native
preview/bake remain unexecuted. They are explicit TEST acceptance steps, not
reported passes. No live model, Grasshopper document, registry, or loaded plugin
was changed. The user-run setup artifact is complete and compile-tested.

## Direct external link revision — 2026-10-10

The user clarified that initial setup must also be externally loaded, without
copy/pasting code. `StoneSurfaceBake.cs` is now one generated shared Script-Mode
file that performs initial port configuration and subsequent preview/bake runs.
Connect it to the special script input with Input is Path; keep the new C#
component's default x/y and out/a ports initially. The old embedded bootstrap is
retained as a legacy optional artifact, but the README no longer requires it.

Added the GH adapter at
`src/GrasshopperStoneBake/Infrastructure/Grasshopper/Live/LiveStoneSurfaceComponent.cs`.
The adapter uses Rhino's injected `ghenv` and its public data-access manager.
This avoids compile-time references to stone input/output globals before setup.
It schedules first-run configuration outside the solve, retains the exact special
script input and its source wires/path setting, and adds the names, type hints,
Item access and persistent defaults. It does not call SetSource or swap script
values. Later solves preserve the five input values and connections. More than
one setting per ordinary input is rejected before any document-wide bake.

The generated delivery script, entry, builder, user instructions and TEST artifact
were updated. The canonical Rhino stone bake adapter is unchanged; there are no
server, transport, registry, installation or live-document changes.

Validation commands (both exit 0):

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/Grasshopper/Build-StoneSurfaceScript.ps1
dotnet run --project Project_Test/261009_TEST_grasshopper-stone-bake/GrasshopperStoneBake.Tests.csproj -c Debug -- Scripts/Grasshopper/StoneSurfaceBake.cs
dotnet run --project Project_Test/261009_TEST_grasshopper-stone-bake/GrasshopperStoneBake.Tests.csproj -c Release -- Scripts/Grasshopper/StoneSurfaceBake.cs
```

The new direct-link delivery compiles/emits against the installed APIs with fresh
x/y-only input bindings and with both typed/untyped configured stone bindings.
`LinkedScriptChecks.csx` executes the installed public Script-Mode output method
against an IGH_DataAccess recording proxy: unset/null implicit outputs leave the
direct results intact, and a non-null positive control writes. The existing 45
placement cases and remaining bake policy tests also pass. Logs are ignored
`.validation/grasshopper-stone-linked-debug.log` and `-release.log`.

Metadata inspection confirms Rhino 8.35 injects ghenv as ProxyScriptEnv with the
actual IGH_DataAccess instance. No private API is invoked by the delivery file.
The revised file has no SDK-mode class or SDK transformation dependency. Full
live initial setup, preview/bake, external-edit refresh and GH save/reopen remain
pending host acceptance, explicitly described in TEST. The compile/managed checks
must not be presented as a completed native Grasshopper test.

## Linked project copy Undo repair — 2026-10-10

The user reports that preview now works, but enabling Bake produces the old
"Bake needs Rhino Undo enabled and no running Rhino command" error. They explicitly
requested editing only their linked project copy at
`V:\01 Project Folders\P00012 Met Tang\03-Design-Eng\03-BIM\05-Wireframe\01_grasshopper\05_STN\StoneSurfaceBake.cs`.
The production change was applied directly to that file; the repository delivery
and canonical adapter were not regenerated or changed for this repair. Future
maintenance must reconcile this project-copy repair before replacing that file.

The previous code assumed BeginUndoRecord returning zero always meant failure.
McNeel's RhinoCommon source documents that an existing record also returns zero:
https://github.com/mcneel/rhinocommon/blob/master/dotnet/rhino/rhinosdkdoc.cs
The new BakeUndoScope accepts active recording, otherwise starts its own record,
and closes/automatically rolls back only its own successfully closed record.
Disabled recording and failure to obtain any active record still reject before
writes. A failed bake using a borrowed record reports partial changes and manual
Undo instructions rather than undoing the host's record. The success report no
longer promises a separate record when Rhino/Grasshopper owns it. The precise
native host state causing the user's original error has not been inspected.

Before-edit SHA-256: `0B1482726036ED8FF99FE7B38757D7578AB2B516ABB25D0D1DB617CF7CA819F7`.
After-edit SHA-256: `1575F1CE44D4DFF38565F41C93824F9EBA6EF76512526DC42C1E0E319E55F612`.
Backup: `.validation/StoneSurfaceBake-before-undo-fix-feac365b64bd47fcac374e1a06a904c7.cs`.
Diff review confirms only Undo scope, error handling, and success wording changed.

Validation against the actual V: file passed in Debug and Release, both exit 0:

```powershell
$scriptPath = 'V:\01 Project Folders\P00012 Met Tang\03-Design-Eng\03-BIM\05-Wireframe\01_grasshopper\05_STN\StoneSurfaceBake.cs'
dotnet run --project Project_Test/261009_TEST_grasshopper-stone-bake/GrasshopperStoneBake.Tests.csproj -c Debug -- $scriptPath Scripts/Grasshopper/SetupStoneSurfaceBake.cs --check-undo
dotnet run --project Project_Test/261009_TEST_grasshopper-stone-bake/GrasshopperStoneBake.Tests.csproj -c Release -- $scriptPath Scripts/Grasshopper/SetupStoneSurfaceBake.cs --check-undo
```

Added `UndoScopeChecks.csx`, which invokes the actual emitted delivery class with
recording delegates, without executing the script entry point or touching Rhino.
Eight scenarios cover owned/borrowed records, disabled recording, failed start,
record becoming active, no-write failure, failed close, and failed rollback.
Full script compilation with fresh/configured ports and existing managed geometry/
policy tests pass. Live bake and native Undo remain pending host retry. Preview
works per the user's report; no native document mutation was run by the agent.

## Direct stone surfaces inside assembly blocks — 2026-10-10

The user requested STN_ID and Assembly_ID on the surfaces themselves. Historical
verification data showed that WF_STN-MET-2010S-020 occurs under five different GA
codes; shared individual-stone definitions cannot represent one occurrence-specific
Assembly_ID. After clarification the user explicitly chose assembly blocks with
direct stone surfaces. The PLAN revision records this change in output structure.

Only the linked V: project script was changed as the production deliverable (same
path as the Undo repair above). Repository delivery/canonical implementation remain
the earlier version and must not be copied over the project file. A before-edit
backup is `.validation/StoneSurfaceBake-before-member-ids-02960f9721834ec7af3aa2cf59dfb54b.cs`.
Final project-file SHA-256 is
`5298125B9B9D715A0F49DBDA2CA3A752935EB2B16329B117DCF38D7AD823CCE8`.

Output structure:

- One baked assembly block instance per eligible source assembly instance, with
  WF_<assembly name> definitions and no nested individual stone block instances.
- Direct surfaces retain WF_<stone name> object names; object attributes contain
  STN_ID=<stone name> and Assembly_ID=<parent GA name>, plus workflow ownership and
  stable member-path tracking. For the requested example the two values are
  STN-MET-2010S-020 and STN-MET-GA-2010C-026.
- Fin/Back layers, inch inputs, signed base offsets, and world +Z heights remain.
  World surfaces are converted into assembly coordinates. Definitions are shared
  only when their members match in world placement; incompatible tilted/scaled
  occurrences receive a source-instance GUID suffix to preserve geometry exactly.
- BakedIds now returns assembly instance IDs. The component message reads
  "Assembly surfaces | inches" after a successful solve.

Preflight checks definition ownership, transforms, matching instances, locked/
reference layers, and external references before mutation. Existing owned surface
STN_ID/Assembly_ID values participate in no-op detection, so missing/wrong values
trigger repair without changing numeric settings. Modified definitions are read
back to confirm direct members and exact attributes. Unrelated user text is copied.

Migration creates/updates new assembly output before removing exactly matched old
individual WF stone instances. Tracked legacy outputs require the v1 owner/source
key, definition name, and Assembly_ID. Untracked original outputs require exact
name/Assembly_ID and original/current placement, with ambiguity rejected. Unmatched
objects and unused legacy block definitions are retained. Source assemblies are
unchanged. The existing Undo ownership/rollback behavior is retained.

Validation commands against the actual project file, Debug and Release exit 0:

```powershell
$scriptPath = 'V:\01 Project Folders\P00012 Met Tang\03-Design-Eng\03-BIM\05-Wireframe\01_grasshopper\05_STN\StoneSurfaceBake.cs'
dotnet run --project Project_Test/261009_TEST_grasshopper-stone-bake/GrasshopperStoneBake.Tests.csproj -c Debug -- $scriptPath Scripts/Grasshopper/SetupStoneSurfaceBake.cs --check-undo --check-assembly-surfaces
dotnet run --project Project_Test/261009_TEST_grasshopper-stone-bake/GrasshopperStoneBake.Tests.csproj -c Release -- $scriptPath Scripts/Grasshopper/SetupStoneSurfaceBake.cs --check-undo --check-assembly-surfaces
```

`AssemblySurfaceChecks.csx` invokes helpers from the emitted project script (not
the older repository adapter). It checks the exact example IDs, one stone shared
by two different assemblies, missing/wrong identity detection and subsequent no-op,
occurrence keys, and 45 world-Z/definition-sharing combinations including mirrored
and tilted transforms. Full linked-file compilation with fresh/configured ports,
eight Undo ownership scenarios, installed output API checks, and earlier geometry/
policy tests pass. Only the new helpers and compilation cover the revised assembly
logic; native document migration, geometry construction, readback, repeat bake, and
Undo are still pending live host acceptance. No live Rhino objects were modified
by the agent and no model was saved.

## Visible STN-MET source scope — 2026-10-10

The user changed assembly discovery from STN-MET-GA- to the literal STN-MET
prefix and restricted reading to currently visible blocks. Updated the linked
V: project file directly. Case-insensitive prefix matching now accepts old/new
and other STN-MET names; _EL/_V exclusions remain. The component message and empty
scope report describe the new rule. The STN-MET-1/2 child-stone families remain.

Only visible source assembly instances enter traversal; hidden nested block
members are skipped before inspecting their referenced definitions. Visibility
checks object hidden/visible state, the active viewport's model/page space,
global/model/per-viewport layer visibility, and the full parent-layer chain.
Locked but visible source blocks remain eligible. This is visibility-state
filtering, not camera-frustum, occlusion, or screen-pixel filtering.

Output identity metadata is still indexed across hidden/visible outputs to
prevent duplicate baking when an output layer is off. Excluded source geometry
is not traversed. When a definition update would change output shared with
excluded source occurrences, the selected visible scope uses a deterministic
__scope_ definition name instead. Existing reference ownership checks remain the
final gate. Hidden/unmatched source outputs are retained without geometry updates.

Backup: `.validation/StoneSurfaceBake-before-visible-scope-847c8df770c04c5aa7d45dcd821356dd.cs`.
Project-file SHA-256 after this repair:
`66E276A7EAA8F32115495D2F2E879A02FF122460E0EB54E94FF849680220874A`.

The same actual-project-file Debug and Release test commands from the previous
entry passed, both exit 0. `AssemblySurfaceChecks.csx` now also verifies the new
prefix, retained exclusions, object/ancestor/view visibility gates, refusal to
modify a definition shared with an unselected reference, and stable/distinct
scope names. Full compile and previous attribute, geometry, and Undo checks pass.
Native visibility evaluation and scoped rebaking remain host acceptance checks;
no live Rhino mutation or UI automation was performed.

## GitHub publication - 2026-10-13

Published with [PR #11](https://github.com/novan-xu/MCP_Rhino/pull/11), source commit `cae9c71`.
The branch integrates `origin/master` at `f6dc9d0`. Publication validation is recorded in
`Project_Exet/261012_EXET_live-custom-scripts.md`. Earlier acceptance boundaries in this record remain applicable.
