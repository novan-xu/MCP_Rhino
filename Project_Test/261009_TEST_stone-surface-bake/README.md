# Stone surface bake validation

The user authorized this capability on 2026-10-09. The requested output is 5 inches
in world +Z with unchanged WF naming, source edge placement, and Assembly_ID.

## Passed

- `dotnet build .\MCP_Rhino.sln -c Debug --nologo -m:1`
- `dotnet build .\MCP_Rhino.sln -c Release --nologo -m:1`
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Project_Test/261009_TEST_stone-surface-bake/Run-Validation.ps1`
- Both CLI configurations: surface contract/placement smoke, original stone bake
  smoke, MCP safety (171 tools), tool overlap cleanup, and Router smoke.
- Package 1.3.3 build, full bundle hash verification, and compiled RHP assembly
  metadata: GUID `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`; Runtime DLL has no GUID.

Placement cases cover exact endpoints and 5-unit world height under translation,
rotation, mirror, nonuniform scale, tilt, and shear. Zero/projective/world-Z-only
edges are rejected. Service tests reject invalid modes and nonpositive/nonfinite
heights before live access; Curve stays the default. CLI tests cover inch units
and rejection of undefined/custom units.

## Host-dependent coverage still pending

Rhino's native geometry library cannot initialize inside the ordinary CLI host.
The dedicated `stone-surface-bake-smoke-test` / `McpStoneSurfaceBakeSmoke` live
branch additionally covers millimeter/foot conversion, actual NURBS construction,
and equivalent/wrong-height/curve-definition compatibility. Production packaging
does not expose developer smoke commands. Do not report these native cases as
executed until live evidence exists.

For the requested document, `Verify-LiveBake.py` runs through the installed Router
only. With `--document <open saved path> --evidence <ignored local folder>`, it
previews surfaces. Add `--apply` to bake once, preserve raw mutation evidence,
verify every output's attributes/matrix/bounding box, compare all source definitions
and parent instances, and require a clean idempotent second preview. It never
opens a .3dm from disk. A prior `apply-raw.json` prevents an accidental repeat.

## Installation

`Install-InHostPowerShell.ps1 -Mode Preflight` verifies staged package hashes only.
Default Install must run in ordinary user PowerShell with Rhino closed. The
ignored `.validation/stone-surface-host/target.json` binds it to the previously
verified Rhino Windows account and prior RHP. It runs the product installer and
Validate, launches an independent process for registration attestation, and starts
a hidden, read-only observer to record exact RHP/timestamps after Rhino restarts.

Logs are in `.validation/stone-surface-*.log`; private live/project/host evidence
is kept in ignored local locations, not in this tracked test folder.

## Live result — 2026-10-09

Host Install/Validate and independent post-restart attestation passed for 1.3.3.
The Router verification script completed preview, apply, and all checks: 12
surface definitions, 126 instances, 61 unchanged source assemblies, exact 5-inch
world +Z height, correct names/Assembly_ID, and no duplicates on repeat preview.
Actual native NURBS creation and compatibility checks passed during that live
workflow. Non-inch conversion cases in the standalone live smoke and an Undo
replay remain unexecuted. Evidence: `.validation/stone-surface-live/verification.json`.
