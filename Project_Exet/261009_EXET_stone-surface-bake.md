# Stone surface bake execution

## Corresponding plan

[Plan](../Project_Plan/261009_PLAN_stone-surface-bake.md). Executed 2026-10-09 after
explicit user authorization. Requested direction: world +Z, physical height 5 inches.

## Related artifacts

[Tests and host installation script](../Project_Test/261009_TEST_stone-surface-bake/README.md).
Work is uncommitted on `codex/stone-assembly-ga-hyphen-20261009`; no PR was requested.

## Implemented scope

- Extended the existing preview/apply bake tools, Skill, service, and live adapter
  with Curve (default) / WorldZSurface and an inch-based height option.
- Surface definitions are canonical XZ rectangles. Per-occurrence matrices map
  the bottom edge to both exact transformed source endpoints and keep Z unit world
  +Z, supporting rotated, mirrored, scaled, tilted, and sheared parent transforms.
- Retained left-end base points, WF names, exact parent Assembly_ID, source
  preservation, scope exclusions, one-Undo apply, and equivalent-output skipping.
- Added geometry/height conflict checks and bounded placement/instance evidence.
- Staged MCP_Rhino 1.3.3; production activation and live bake are pending.

## Deviations from plan

Native NURBS and non-inch Rhino unit conversion checks require the Rhino host;
the ordinary CLI can test placement arithmetic and inch-unit/validation paths.
The dedicated live smoke branch contains native checks but has not been run.

## Issues found and corrected

An invalid-unit test initially exceeded the enum's byte range; corrected to an
undefined in-range value. Transform.IsAffine invokes Rhino native code, so the
placement adapter checks the equivalent homogeneous affine matrix row directly.
The package builder required an explicit OutputRoot in this PowerShell context.

## Test record

- Debug and Release solution builds: exit 0, zero warnings/errors.
- Debug and Release CLI builds/smokes: exit 0. Surface placement/options tests,
  original stone bake regression, MCP safety (171 tools), overlap and Router tests pass.
- Build-McpRhinoPackage.ps1 with explicit artifact root: exit 0, package 1.3.3.
- Host script Preflight: all staged hashes pass; no installation performed.
- Compiled RHP metadata directly inspected: expected assembly GUID present;
  isolated Runtime DLL carries none.
- `git diff --check` passed for tracked changes.
- No Rhino model mutation has occurred. Last existing-runtime preview counted
  126 stones, 61 assemblies, and 12 new WF definitions in the inch-unit document.

## Acceptance alignment

Code, managed placement validation, tool safety, packaging, and identity checks
pass. Independent host install/Validate, post-restart load attestation, native
surface verification, and the final live bake remain pending.

## Rollback verification

The previous installed RHP remains loaded and was not moved by this construction
turn. The staged host script requires the correct user identity and prior reachable
RHP before installer activation. Live apply uses the existing one-Undo wrapper;
Undo has not yet been exercised for surfaces.

## Outstanding work

Save/close Rhino, install 1.3.3 from ordinary host PowerShell, reopen the document,
check host attestation, then preview/apply and verify all outputs via Router.
Save timing remains with the user/Rhino.

## Conclusion

Surface capability is built, tested within the CLI's limits, and staged. The
user's requested surfaces are not yet baked; completion depends on the host update.

## Live completion — 2026-10-09

The user installed and reopened Rhino. Independent host process 59792 attested
the 1.3.3 registration, exact empty CommandList, and advancement of all three key
timestamps. Independent observer 56088 then recorded Rhino process 42352 loading
the exact 1.3.3 RHP. Root/CommandList advanced on restart while PlugIn stayed at
the install timestamp. The agent independently confirmed the same loaded module.
Evidence is in ignored `.validation/stone-surface-host/`.

`Verify-LiveBake.py` previewed and then applied through the installed Router in
the explicitly selected, reopened document. Both calls exited 0. The live bake
created 12 WF surface definitions and 126 instances from 61 assemblies. Native
NURBS construction and compatibility checks ran successfully in the live host.
The document uses inches; every surface extends exactly 5 inches in world +Z.

Verification read all 126 output instances and checked WF definition/name,
Assembly_ID, all four transformed corners, and actual world bounding boxes. It
compared every prior source definition and all 61 parent instances unchanged.
The second surface preview found all 126 existing placements, zero failures,
and zero definitions or instances to create. Evidence, including raw mutation
response, is in ignored `.validation/stone-surface-live/`.

The requested live task is complete. No .3dm disk write or automatic save was
performed. The separate native smoke's non-inch conversion cases and an actual
Undo replay were not run; these are not claimed as passed.
