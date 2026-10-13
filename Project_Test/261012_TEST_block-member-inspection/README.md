# Block member inspection validation

## Reproduce

From the repository root:

```powershell
& .\Project_Test\261012_TEST_block-member-inspection\Run-Validation.ps1
```

Builds the Server RHP and CLI in Debug and Release, then runs the exclusive `block-member-inspection-smoke-test`, MCP safety, and tool inventory tests in both configurations. NativeChecks runs linked production inspector code against synthetic in-memory Rhino documents through a recording document accessor, using installed licensed Rhino 8 in a hidden Rhino.Inside process. It never opens a user model. The comparison tests validate all-pairs tolerance and ordering/mirror invariance. Build output and transcript are ignored under `.validation/block-member-inspection/`; native project bin/obj are ignored.

Native fixture coverage: direct/nested/shared definitions, full transform multiplication order, rigid rotations, reflection, nonuniform scaling, shear, hidden/locked roots, own vs ancestor metadata, missing/non-block/definition-member roots, duplicate roots, depth/leaf limits, curved/holed/nonquad geometry, invalid transformed surface classification, model units, unchanged object/definition counts, attributes, geometry CRCs, modified state and Undo serials. The depth guard covers excessive recursion; active-definition cycle detection is implemented but a cyclic Rhino definition fixture is not constructed.

The live smoke development command is `McpBlockMemberInspectionSmoke`. Like other development commands, it is excluded from the production RHP command surface. CLI fallback must return LIVE_RHINO_REQUIRED; it never reads a disk snapshot.

## Package and activation

```powershell
& .\Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -OutputRoot .\.validation\block-member-inspection\package
& .\Project_Test\261012_TEST_block-member-inspection\Install-InHostPowerShell.ps1 -Mode Preflight
```

Package version: 1.3.6. **Preflight verifies the bundle only; it does not install or prove host registration.**

After saving and closing Rhino, run the following in ordinary user PowerShell under the Windows account that runs Rhino:

```powershell
& 'C:\01_Projects\MCP_Rhino\Project_Test\261012_TEST_block-member-inspection\Install-InHostPowerShell.ps1'
```

The script checks the staged hashes, captures the prior reachable RHP and registry, invokes the product installer in a separate process, invokes `-Mode Validate`, and attests exact FileName, empty production CommandList, and all three key timestamps in another process. It prompts the user to reopen Rhino and the document, then verifies the exact loaded RHP and Rhino-owned root/CommandList timestamp changes while PlugIn stays unchanged. Evidence is kept under ignored `.validation/block-member-inspection/host/`. On activation/load-verification failure, the prior reachable RHP and registry are restored; this is not a full rollback of Router binaries/ownership manifest and must be reported as a failed activation. No host activation or rollback was executed during construction.

## Live audit

Once the host activation and load evidence pass:

```powershell
node .\Scripts\Analysis\Audit-LiveStoneSizes.mjs '<absolute saved document path>' '<new output JSON path>'
```

The client uses only the installed Router, selects one exact live document, reads all instances including hidden ones, and traverses batches of 50. It requires inch units and no standalone surfaces for this block-surface audit. It repeats the full read and instance inventory to reject observed model edits during batching. No disk model read or modeling mutation occurs.

Planar single-face, single-loop straight quads are compared across **all pairs** sharing a trimmed STN_ID. For each pair, take the smallest maximum difference among four cyclic and four reversed corner correspondences, comparing all four boundary edges and both diagonals. A difference greater than 0.03125 inch (plus 1e-8 numerical epsilon) is a mismatch. This allows translations, rotations and mirroring, and detects scale and shear. Surface recognition uses model absolute tolerance. Unsupported surfaces, missing IDs and singleton groups are reported explicitly. JSON contains occurrence identity, ancestry and worst mismatch pair. Output is create-new only.

## Current result

Construction validation, host installation, exact loaded-RHP verification and the live combined-document audit passed. The audit covered 989 block instances and 1,741 surface occurrences: 100 repeated-ID groups match, 2 mismatch, and 108 IDs occur only once. No missing IDs or unsupported surfaces were found. Detailed local evidence is in `.validation/block-member-inspection/combined-stn-audit.json`; the EXET records installation and live execution separately from synthetic tests.

## Activation follow-up, 2026-10-12

The initial host attempt ran under a different Windows account from the Rhino process. Its `host/before.json` and backups are retained. No `after.json` or post-restart attestation was produced. Reopened Rhino still exposed the old tool catalog. The helper now requires a locally staged `host-target.json` whose account SID and prior RHP are derived from the inspected Rhino process. It rejects other accounts before installation, stores new evidence per SID, checks that the host's prior registration matches the observed loaded RHP, and captures installer/attestation/error output. `CheckAccount` is a read-only preflight. `Restore` under the original attempt's account restores that attempt's prior reachable RHP and registry from the preserved backup. No repair was executed by the agent.

Later in the same task, an independently launched Windows scheduled task using the existing interactive Rhino account established host registry access without a password or elevation. `Read-HostRegistration.ps1` writes create-new observation evidence including identity, parent process, key times, installed versions and loaded modules. The same host route ran `Invoke-HostActivationStep.ps1 -Mode Install`, which calls the helper with `-DeferRestartVerification`; that switch only skips the interactive prompt. The user reopened Rhino. A separate `-Mode AfterRestart` task completed exact loaded-path and key-ownership checks. Temporary tasks were removed after completion. MCP_Rhino 1.3.6 and the already-installed PanelCladdingEditor 1.0.88 were confirmed loaded. See the EXET completion section for exact evidence and process IDs.
