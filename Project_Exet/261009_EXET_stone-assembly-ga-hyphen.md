# GA- assembly naming execution

## Corresponding plan

[Plan](../Project_Plan/261009_PLAN_stone-assembly-ga-hyphen.md), executed 2026-10-09.

## Related artifacts

- [Test folder](../Project_Test/261009_TEST_stone-assembly-ga-hyphen/README.md).
- Restored original capability artifacts dated 260901.
- Release bundle: `Packaging/MCP_Rhino/artifacts/MCP_Rhino-1.3.2/`.
- Branch: `codex/stone-assembly-ga-hyphen-20261009`. No commit or PR created.

## Execution result / actual scope

Recovered the installed stone bake capability from Git snapshot `c4f1a80`, with
targeted registration/coverage changes from `f31020a`. The stash was not applied
or consumed. Changed the assembly prefix to `STN-MET-GA-`, updated descriptions,
validation messages, script help, and regression expectations. Legacy GA names
are no longer included. Flat/vertical naming, curve selection, WF_ definition
creation, base point, transforms, and exact parent Assembly_ID behavior remain.

Explicit object enumeration includes hidden and locked local assembly instances
and existing output instances, preventing omitted assemblies and duplicate output
when visibility/lock state changes. Recovered the installed startup idle
reconciliation fix to avoid downgrading document discovery on replacement.

Built and verified 1.3.2. Production remains on 1.3.1; the user's live document
has not been mutated.

## Deviations from plan

The package installer also needed compatibility corrections before safe staging:
accept Rhino's hydrated `MCP_Rhino.Server` product name; require installer-owned
PlugIn FileName while allowing Rhino to remove the bootstrap root FileName; keep
an exact empty CommandList for this checkout's command-free production RHP.
Added an ordinary-host installation wrapper with separate-process attestation.

## Problems found and fixed

- Current checkout lacked the installed capability source after earlier sync.
- Retired prefix rejected every current GA- assembly name.
- Default object enumeration could omit hidden/locked instances.
- The installer rejected its own Rhino-hydrated registration or treated a
  Rhino-owned root FileName as permanent authority.

## Test record

Passed:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo -m:1
dotnet build .\MCP_Rhino.sln -c Release --nologo -m:1
& .\Project_Test\261009_TEST_stone-assembly-ga-hyphen\Run-Validation.ps1
& .\Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -Version 1.3.2
& .\Project_Test\261009_TEST_stone-assembly-ga-hyphen\Verify-StagedInstaller.ps1 -BundleRoot <staged-1.3.2-bundle>
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Project_Test\261009_TEST_stone-assembly-ga-hyphen\Install-InHostPowerShell.ps1 -Mode Preflight
```

Both solution builds: exit 0, zero warnings/errors. Debug/Release capability,
safety, inventory, and Router smokes: passed. Safety verified 171 tools; inventory
verified 16 Blocks tools. Direct compiled metadata probe verified the RHP's
nonempty expected GUID and absence of an identity on the runtime DLL. Installer
test fixture passed installation/validation/repair and negative registration cases.

## Acceptance alignment

Code, naming/service coverage, build, tool-safety, package identity, and isolated
installer checks passed. Production activation and live bake acceptance are pending.
Test fixture installation output must not be interpreted as a production install.

## Rollback verification

The current production installation and all live source assemblies are unchanged.
The installer retains its existing rollback mechanism for a later ordinary-host
upgrade. Production rollback and live Rhino Undo were not exercised this turn.

## Current remaining items

1. User saves/closes Rhino, runs the prepared host installer, and reopens Rhino.
2. Verify exact loaded RHP and post-load registration timestamp contract.
3. Select the live target, preview and validate curves, apply bake, and verify
   attributes and zero additional proposed placements on a second preview.

## Conclusion

GA- support is implemented, tested, and packaged. The requested bake remains
pending host activation/restart; no claim of completed live baking is made.
# Follow-up completion — 2026-10-09

The user subsequently requested 5-inch world +Z surfaces in place of curves and
authorized that extension. The final task completed using MCP_Rhino 1.3.3:
12 WF definitions and 126 surface block instances from 61 GA- assemblies,
with exact parent Assembly_ID and source geometry preserved. Independent host
install/restart attestation and live verification passed. See
[surface bake execution](261009_EXET_stone-surface-bake.md) for the final evidence.
The original curve-only bake was never applied.
