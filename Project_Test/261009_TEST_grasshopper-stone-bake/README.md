# Grasshopper stone bake tests

This is a standalone script artifact, not an MCP server/tool change. The test
project does not modify Rhino, open model files, or need a loaded MCP plug-in.
Its test entry uses `.csx` so the MCP server's broad `Project_Test/**/*.cs` include
does not accidentally compile this unrelated standalone executable.

Run from the repository root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Scripts/Grasshopper/Build-StoneSurfaceScript.ps1
dotnet run --project Project_Test/261009_TEST_grasshopper-stone-bake/GrasshopperStoneBake.Tests.csproj -c Release -- Scripts/Grasshopper/StoneSurfaceBake.cs
```

Repeat with `-c Debug` for the other build configuration. Rhino 8 must be installed
at the reference paths in the csproj. The test uses the installed Roslyn 4.6
scripting compiler and actual RhinoCommon assembly to compile and emit the entire
delivery script with fresh x/y ports as well as configured object and double/bool
input bindings. All contexts contain the installed `ProxyScriptEnv` type for
Rhino's injected `ghenv`; the script does not need named stone globals to compile.

`LinkedScriptChecks.csx` invokes the installed public `Grasshopper1Script.SetOutput`
against a recording `IGH_DataAccess` proxy. Missing and null implicit output values
must not overwrite direct data-access output; a non-null positive control must
write. This managed API test passed without a Rhino document or GH canvas.

The same command also compiles/emits the legacy optional `SetupStoneSurfaceBake.cs` using the installed
Grasshopper and RhinoCode public API assemblies (Rhino 8.35.26251.13001). The
offline harness binds the SDK `GH_ScriptInstance` name to Rhino 8's concrete
`Grasshopper1ScriptInstance`; otherwise bare Roslyn resolves the old abstract
Grasshopper base. This is an API compilation check, not an execution of Rhino's
SDK-mode transformation or its editor. Both Debug and Release passed.

`ApiProbe/` inspects installed assembly metadata only. It neither creates a
Grasshopper canvas nor reads or changes a live Rhino document. Its output
confirmed the public port/type-hint/persistent-data/source APIs and the typed
file-backed script API. Logs are under ignored `.validation/grasshopper-stone-*`.

Covered behavior:

- Separate fin/flat settings, positive/negative base offsets, positive heights,
  rejected zero/nonfinite/negative height values.
- GA- prefix discovery, _EL/_V/legacy assembly exclusions and stone families.
- 45 translation/rotation/mirror/scale/tilt × offset × height combinations,
  preserving both exact edge endpoints and world +Z heights.
- Stable assembly-instance/nested-path keys and distinct repeated occurrences.
- Legacy adoption only when name, Assembly_ID, tracking absence, and placement match.
- Shared-definition ownership: nested and unmatched references reject updates.
- Exact layer paths, transform comparisons, and idempotent no-op detection.

Native Rhino document writes, layer creation, definition replacement, geometry
construction, Undo/rollback, and an actual Grasshopper solve remain host-level
acceptance checks. No claim is made that these tests executed that integration.
The earlier MCP surface implementation has live evidence, but it is not a live
test of this standalone artifact.

## Manual host acceptance

Use a disposable test document with nested flat/fin blocks and two GA- instances,
including a rotated or mirrored one. Connect the delivery script as documented
in `Scripts/Grasshopper/README.md`. With Bake=false verify no Rhino objects/layers
change. With Bake=true check a single surface per WF block, geometry and instance
names, both layer paths, Assembly_ID, signed base shifts, and independent heights.
Recompute unchanged and confirm no duplicates; change heights/offsets and confirm
replacement rather than accumulation. Verify one Undo restores the previous state.
Do not perform destructive fixture checks in the user's production model.

For automatic setup, enable the special script input on a fresh Rhino 8 C#
component, select Input is Path, and connect a File Path for StoneSurfaceBake.cs.
Do not paste or edit component code. Confirm its name, five numerical/
Boolean inputs, Item access, default values 0/5/0/5/false, four named outputs, and
the unchanged script-input path and source wire. Save/reopen the GH document and
verify the link still resolves. Change the external script, refresh it, and verify
the updated code is used without re-pasting or resetting the five input values.
Verify an ordinary wired default port is refused without disconnecting it,
and a missing source file reports a clear error before port changes. These live
setup/persistence/failure checks remain pending; they were not run by the agent.

## Project-copy Undo regression — 2026-10-10

The user confirms live preview works and requested repairing the linked V: copy
directly. `Program.csx` accepts an optional second argument for the legacy setup
script path and `--check-undo` to exercise the new scope in the actual supplied
script assembly. `UndoScopeChecks.csx` never runs the script entry point or calls
Rhino document methods. Eight managed scenarios verify owned/borrowed Undo,
disabled/failed recording, close failure, and safe rollback behavior. The repair
is currently in the user's project copy only, as requested; its path, hash, backup
and passing Debug/Release commands are in the matching EXET repair entry.

Native acceptance still needs a live Bake=true retry. Test Undo with Bake=false
to avoid an automatic rebake; a host-owned record may include other changes from
the same solve/command. Managed tests do not establish native bake success.

## Assembly block / direct surface revision — 2026-10-10

The user now requires assembly blocks with individual stones directly represented
as surfaces. Use the linked project copy described in EXET, not the older repository
delivery. Add `--check-assembly-surfaces` to the project-copy test command above.
`AssemblySurfaceChecks.csx` invokes identity and geometry-sharing helpers from the
actual emitted delivery file. It verifies exact member IDs, shared stones with
different parent codes, missing/wrong identity detection, stable assembly keys,
and 45 transform/offset/height cases. Compilation and managed tests pass in Debug
and Release. No native Rhino document writes are executed by these tests.

Updated native acceptance: Bake a disposable fixture with the same stone in two
different GA assemblies. Inspect each WF_<assembly> definition: all members must
be direct surfaces, named WF_<stone>, with the correct scalar STN_ID/Assembly_ID.
Check Fin/Back layers, signed offsets and positive world-Z heights. Missing or
wrong STN_ID/Assembly_ID on an owned member must be repaired on the next bake.
Recompute unchanged: no duplicate blocks and no writes. Change heights: surfaces
update inside the same assembly output. Start with exactly matched legacy stone
outputs: migration should replace their instances after adding assembly output;
unmatched outputs and unused definitions remain. Undo with Bake=false should
restore the previous state. These native acceptance checks remain unexecuted.

## Visible STN-MET source scope — 2026-10-10

The project script accepts STN-MET-prefixed assembly names, retaining _EL/_V
exclusions. The actual emitted-script tests now cover prefix cases, object and
ancestor/view visibility gates, and isolated definition names for visible scopes.
Both Debug and Release pass with the same command and flags as above.

Native acceptance additionally needs Hide/Show, parent-layer visibility, nested
block visibility, and model/detail layer overrides. Visible locked inputs should
still work. Hide one of two source instances sharing a baked assembly definition,
change a height, and verify only the visible source's output changes (using a
separate definition when necessary). Recompute unchanged and confirm no duplicate
output, including when an output layer is off. Camera cropping and occlusion are
not part of the visibility-state rule. These native checks have not been run.
