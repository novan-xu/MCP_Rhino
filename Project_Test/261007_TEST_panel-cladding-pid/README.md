# PCpid validation — 2026-10-07

## Reproduce

```powershell
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj -c Debug
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj -c Release
dotnet build MCP_Rhino.sln -c Debug --nologo
dotnet build MCP_Rhino.sln -c Release --nologo
```

Both planning runs and both solution builds passed (exit 0); solution builds had
zero warnings/errors. The precise standalone-test exclusion in the Server project
was verified by both solution builds. This feature adds no MCP tool or Router change.

Release regressions also passed for:

- `260818_TEST_panel-cladding-pc-commands/PanelCladdingPcCommandsSmoke.csproj`
- `260923_TEST_panel-cladding-role-cid/PanelCladdingRoleCidSmoke.csproj`
- `261006_TEST_panel-cladding-object-names/PanelCladdingObjectNamesSmoke.csproj`

The corresponding `.log` files contain their assertion output. `git diff --check`
also passed. Test code uses production planning and identity-preflight services.

## Tutorial evidence and coverage

The saved tutorial was inspected through the live Router with FilterObjects,
GetGeometryFramesByFilter and InspectSurfaceRebuildDescriptor. All 20 panels were
planar vertical quad Breps with outward normals and two aligned bottom-edge rows.
The communication attribute was actually named `temp ID`; production code does
not read it. There were no document mutations or file reads used as a live fallback.

`tutorial-fixture.json` contains only translated geometry, synthetic object GUIDs,
communication labels, and synthetic user text. Private document paths, layers,
object names, lots and project metadata are omitted. Normals and floating-point
alignment residuals are preserved. This is an anonymized snapshot regression,
not a claim that the new RHP executed in the live tutorial.

With panel 1 as north and first floor, all supplied examples pass:

| Tutorial panel | Elevation | Level | Bay |
| --- | --- | --- | --- |
| 1 | N1 | 01 | 01 |
| 2 | N1 | 02 | 01 |
| 3 | N2 | 01 | 01 |
| 4 | N2 | 02 | 01 |
| 5 / 6 | E1 | 01 / 02 | 01 |
| 8 / 7 | E1 | 01 / 02 | 02 |
| 9 / 10 | E1 | 01 / 02 | 03 |

All 20 have distinct PIDs. Other coverage: 40 selection permutations, four building
rotations, large translations/unit scaling, unchanged source metadata, repeated
planning, all unit-role CID suffixes, old/custom CIDs, reference changes, invalid
inputs, duplicate addresses, nonfinite/nonplanar/degenerate/diagonal geometry,
unequal panel sizes, plane bottom ties, bay restarts, bounded tolerance clusters,
external PID/CID collisions and dependency protection.

## Native acceptance limitation

An optional native harness is included and compiles. Invocation:

```powershell
$env:PATH = 'C:\Program Files\Rhino 8\System;' + $env:PATH
dotnet run --project Project_Test/261007_TEST_panel-cladding-pid/PanelCladdingPidSmoke.csproj -c Debug -- --native
```

Attempted 2026-10-07: pure assertions passed, then RhinoCore startup failed with
`COMException 0x80004005` in `StartupInProcess`; process exit 1. No synthetic or user
document was created by that attempt. Native geometry extraction, attribute writes,
repeat/no-op behavior and ambient/single-step Undo assertions were not reached.
`NativeSmoke.cs` confines its future synthetic `.3dm` to ignored `Runtime_Test/local/pcpid/`.

Pending installed acceptance: invoke all four prompts; cancel each prompt and
confirm no attribute writes; select all tutorial panels with 1 as both references;
inspect PID/CID/elevation/level/name; rerun and confirm zero changes; Undo once and
confirm original attributes on every changed panel. Native partial-write failure
rollback remains unexercised. Normal extraction explicitly accounts for
[Brep face orientation](https://developer.rhino3d.com/guides/cpp/determining-normal-direction-of-brep-face/).

## Package validation

Version 1.0.89 is staged under
`%LOCALAPPDATA%/PanelCladdingEditor/staged/1.0.89-pcpid-261007/PanelCladdingEditor-1.0.89`.
Package publish/rebuild passed. Direct assembly metadata checks passed before
packaging and on the packaged RHP: ID `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35` matches
the class/manifest and differs from MCP_Rhino. All 27 bundle-manifest hashes match.
Packaged RHP SHA-256:
`28db95de7cf98f253def1f770997c7dcd37c05480ff9037a143690a031384321`.

The existing registration regression passed using only its temporary product
directory and isolated HKCU test key: Install + Validate, shorthand rejection,
Repair + Validate, missing-command rejection, Repair + Validate. Exact expected
commands include PCpid. The test removes its own temporary directory/key.
This is isolated installer validation, not production registry attestation.

Production installation/activation was not attempted; no prior RHP or production
registry key was moved or changed. Follow AGENTS.md's independent host-registration
attestation and post-start load verification gates before activation is reported.

## Installation follow-up — 2026-10-07

The user requested installation. With zero Rhino processes, all 27 bundle hashes
and the compiled assembly GUID were rechecked. A hidden same-user host PowerShell
process created a nonce; an independent StdRegProv read under HKEY_USERS/current
SID confirmed persistence before production changes. The nonce was removed.

The existing host activation script installed 1.0.89 and ran mandatory Validate.
After it exited, a different host process ran Validate and captured the registry.
The installed RHP path/hash, exact 12-command set including PCpid, and advancement
of all three registry timestamps passed. The Windows registry provider separately
confirmed FileName and the command set. The 1.0.88 rollback RHP matches its original
hash. See [activation-summary.json](activation-summary.json).

Production installation/registration is now passed. Rhino remains closed, so
loaded-RHP/post-start timestamp checks and native command acceptance remain pending.
No user document was modified. Prior registry backup and raw host snapshots are
retained as ignored local evidence in this TEST folder.
