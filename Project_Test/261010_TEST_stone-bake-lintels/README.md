# Lintel stone bake validation

Delivery: `V:/01 Project Folders/P00012 Met Tang/03-Design-Eng/03-BIM/05-Wireframe/01_grasshopper/05_STN/StoneSurfaceBake.cs`.

## Usage

Reload/recompute the existing linked C# component with Bake=false. The prior five-input/four-output layout is upgraded by appending two inputs after Bake and one output after Report. Existing parameter objects, input data, wires, indices and file link are retained. Fresh x/y components still configure automatically. The script does not rebuild an unknown custom port layout.

| Port | Meaning | Default |
| --- | --- | --- |
| LintelBaseZ | Signed world-Z offset from the lintel back-edge baseline, inches | 0 |
| LintelHeight | Positive surface height in world +Z, inches | 5 |
| Lintels | Preview surfaces for lintels in missing assembly outputs | Empty list |

Lintels are nested `STN-MET-3...` stones, matching the supplied `STN-MET-GA-3010S-020` assembly example. `_EL`/`_V` remain excluded. The back edge is the uppermost local-Y horizontal outline edge, with the longest coincident edge selected and ambiguous ties rejected. This retains the existing fin/flat edge convention. The baseline is transformed into the assembly before applying world-Z offset/height.

Lintel members bake on `01_MET-IEF-Curtain Wall::Surfaces - STN detailed::Lintel`, with STN_ID and Assembly_ID as before. Bake=true adds missing assembly outputs only. Already-baked source assemblies remain skipped as a whole, even if they contain newly supported lintels. Existing shared definitions are never modified.

## Reproduce

Requires installed Rhino 8/Grasshopper libraries and .NET 8 SDK. Run from the repository root:

```powershell
& './Project_Test/261010_TEST_stone-bake-lintels/Run-Validation.ps1' -Configuration Debug
& './Project_Test/261010_TEST_stone-bake-lintels/Run-Validation.ps1' -Configuration Release
```

`-ScriptPath` accepts a staging file. The wrapper uses the existing standalone harness with all prior regression flags and `--check-lintels`. It does not execute the script entry point, modify a model, open UI or write a GH document.

## Results — 2026-10-10

Both configurations exited 0 for the staged and actual deployed file. Local ignored logs are `validation-Debug.log` and `validation-Release.log`.

- Full-script compilation/emit with five binding configurations: fresh x/y, previous typed/untyped, new lintel typed/untyped.
- Actual emitted-script lintel family recognition/exclusions, independent offset/height/defaults, nonfinite/nonpositive rejection, layer and preview routing.
- Actual back-edge helper: representative rectangular lintel outline, reversed segments, duplicate edges, order independence, empty/non-horizontal/ambiguous rejection; 45 transformed baseline/offset/height cases.
- Actual layout classification: old/new/fresh/custom/partial states, with idempotent new-layout detection. Syntax contract: scheduled upgrade, GH Undo before appending, no removal/replacement of existing parameters or wires, and new preview exposure.
- Prior missing-only, visible-source, geometry sharing, member-identity, null-output and eight Undo ownership/recovery regressions pass. Historical baseline comparison-helper tests do not imply that the current script updates existing output.

The live port upgrade, actual model lintel geometry, native Rhino bake and live Undo were **not** executed. Managed/helper and syntax evidence is not native integration evidence.

Delivered SHA256: `2B250DA9DA620C38357876F020AD1B2B4047FD8B3968E9C2C926C2F86108A5E3`.
Before-change SHA256: `03D7692DF207A8742EA3766B97527AC03D9029490D167EF0C986C20355C04ACF`.

## Rollback

`StoneSurfaceBake__before_lintels_20261010.cs` beside the linked file is an exact, hash-verified original backup, created without overwriting any prior backup. Restore it to the linked path to revert the code. If the live component has already added ports, also undo that GH port upgrade or use a fresh component for the old script; the old script does not recognize the extended layout. No live upgrade or rollback was performed by this construction.

Ignored `.validation/stone-bake-lintels/` retains local before/after `.csx` snapshots. The older repository delivery is intentionally unchanged and must not regenerate over this newer linked file.
