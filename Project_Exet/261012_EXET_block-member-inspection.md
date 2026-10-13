# Block member inspection execution

## Corresponding plan

[261012_PLAN_block-member-inspection](../Project_Plan/261012_PLAN_block-member-inspection.md). Execution date: 2026-10-12. User explicitly approved building the missing read-only capability after the runtime assessment.

## Related artifacts

[TEST instructions](../Project_Test/261012_TEST_block-member-inspection/README.md), `NativeChecks.csproj` / `NativeChecks.csx`, `DeveloperCommandHandler.BlockMemberInspectionSmokeTest.cs`, `Compare-StoneSizes.test.mjs`, `Run-Validation.ps1`, and `Install-InHostPowerShell.ps1` in that folder. Runtime audit and comparison code lives under `Scripts/Analysis/`. No commit or PR requested or created.

Local build transcript: `.validation/block-member-inspection/validation.log`. Staged package: `.validation/block-member-inspection/package/MCP_Rhino-1.3.6/`. These are ignored build outputs. Initial construction staged the package only; independent host activation and the live audit subsequently completed, as recorded below.

## Execution result / implemented scope

Added `inspect_block_members` under Tools/Blocks with explicit read-only, non-destructive, closed-world annotations. The thin wrapper calls `RhinoBlockMemberInspectionService`; `ILiveBlockMemberInspector` separates orchestration from the live Rhino adapter. Both live and CLI DI register the adapter. Assembly scanning remains the only tool registration source.

The live adapter runs through `ILiveRhinoDocumentAccessor.Execute`, resolves explicit top-level instances including hidden/locked ones, follows nested definitions, composes full transforms, and preserves occurrence ancestry, root/member IDs, definition names, layer, leaf user text and ancestor user text. Root IDs are deduplicated. Invalid targets, leaf/depth/time limits, invalid transforms and cyclic definition traversal fail the whole response, without partial-success data. Empty root lists and invalid limits are rejected before live access.

Geometry is duplicated in memory for measurement. Valid planar, one-face, one-loop, four-straight-edge surfaces return ordered world corners, edges, diagonals, area and perimeter in model units. Other shapes remain explicitly Unsupported. Surface classification is retained when a geometry transform fails, preventing invalid surfaces from disappearing from audit scope. The adapter never writes objects/attributes, selection, viewport, files, or Undo.

The runtime Node client uses only the installed Router stdio connection, explicit session selection and live read tools. It reads batches of 50 roots and repeats the full scope to detect observed edits. Pure comparison checks all occurrence pairs per trimmed STN_ID against 1/32 inch, allowing cyclic/reversed corner correspondence and comparing four edges plus two diagonals. Unsupported/missing-ID/singleton surfaces are separate from confirmed matches. Report publication is create-new JSON. The client requires inches and rejects standalone surfaces so it cannot silently claim incomplete coverage.

Package manifest advanced from the pre-existing working-tree version 1.3.5 to 1.3.6. Existing unrelated changes were preserved. Initial construction built and staged only. The agent-visible installed state reported 1.3.1, while independent host observation later established that Rhino used 1.3.3. Activation subsequently upgraded the host installation to 1.3.6.

## Deviations from plan

- Runtime audit/comparison scripts are under `Scripts/Analysis/`, rather than inside TEST, to keep operational code separate from regression artifacts.
- Full solution builds were unnecessary for an additive Server-only capability; narrowed Debug/Release Server builds were run, as allowed by the guides. Package staging additionally rebuilt the isolated Server runtime, Router and its existing dependencies.
- The actual combined-document audit cannot run yet: final Router discovery returned an empty document list, and no Rhino processes were running. No offline fallback was attempted.
- Production activation is deferred to ordinary user PowerShell because host-persistent registry access has not been independently established in the agent context. The prepared helper is a delivery aid, not evidence that installation has passed.

## Problems found and fixed

- Initial Server build imported generated AssemblyInfo files from an existing standalone test project's obj directory, producing duplicate assembly attributes. Added explicit bin/obj exclusions to the external TEST/archived TEST compile globs. No maintained smoke source was excluded.
- Corrected Rhino API use during compile validation: definition-object ownership is on ObjectAttributes, and Extrusion must precede its Surface base type in geometry conversion.
- Geometry failures initially lacked surface classification; retained it so failed surfaces remain unchecked instead of being counted as nonsurfaces. Added a native regression.

## Test record

`Project_Test/261012_TEST_block-member-inspection/Run-Validation.ps1` final run exited 0:

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c Debug --nologo -m:1 -o .validation/block-member-inspection/plugin-Debug`: passed, zero warnings/errors.
- Equivalent Release plugin build: passed, zero warnings/errors.
- Debug and Release CLI builds with `-p:McpRhinoCliHost=true`: passed, zero warnings/errors.
- `block-member-inspection-smoke-test`: both configurations passed service validation, deduplication and LIVE_RHINO_REQUIRED fallback checks.
- `mcp-tool-safety-annotations-smoke-test`: both configurations passed for 172 tools, with no bare safety annotations.
- `mcp-tool-overlap-cleanup-smoke-test`: both configurations passed; 172 tools, 17 in Blocks.
- `dotnet run --project Project_Test/261012_TEST_block-member-inspection/NativeChecks.csproj -c Release`: passed 22 native assertions in a synthetic headless Rhino document. Eight distinct requested roots yielded 13 leaves after root deduplication. Geometry/attribute checksums, object/definition counts, modified flag and Undo serials were unchanged by reads. A recording accessor confirmed no Undo call. No user .3dm was opened.
- `node Project_Test/261012_TEST_block-member-inspection/Compare-StoneSizes.test.mjs`: passed rigid transform and corner correspondence, exact tolerance boundary, above-tolerance mismatch, all-pairs chaining, unsupported/singleton/missing ID cases.
- Package build and host-helper `-Mode Preflight`: passed, package hashes verified; no activation.
- PE metadata probe directly verified staged RHP assembly GUID `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`, matching source plug-in declaration and manifest. Runtime DLL has no assembly GUID and does not claim the plug-in identity.
- Host-helper PowerShell parser, runtime-client `node --check`, and `git diff --check`: passed.

Live client invocation stopped because the requested file had no routable open document; separate Router discovery confirmed `documents: []`. No comparison JSON for the real model was created. This is not a geometry comparison result.

## Acceptance alignment

Construction, contract, native geometry, safety, inventory, packaging and identity gates passed. Exact live loaded-tool invocation and the requested model's comparison remain pending activation. Cycle guard is implemented; a cyclic native definition fixture was not constructed. Geometry outside planar straight quads is explicitly not supported for shape comparison.

## Rollback verification

No production activation occurred; prior installed paths remain reachable and no production rollback was necessary or exercised. The host helper snapshots the prior plug-in and registry before installation. If activation or post-startup attestation fails, it restores the prior reachable RHP and registration and reports failure. It does not claim a full Router/ownership-manifest rollback; that path has only been syntax checked, not executed against production.

## Remaining work

None for the requested inspection. The two model mismatches are reported findings; correction was not requested. Installation, exact load verification, live comparison, and runtime activity logging completed on 2026-10-12.

## Conclusion

Capability implemented, validated, installed and verified in the user's Rhino session. The live audit found two mismatched STN-ID groups at 1/32 inch, 100 matching repeated-ID groups and 108 singleton groups. No user model geometry was modified.

## Activation follow-up (2026-10-12)

The user reopened Rhino. Router discovery found the requested combined document in process 39988, but the catalog did not contain `inspect_block_members`; the fingerprint matched the old runtime. Process-module inspection showed RHP/runtime 1.3.3. Process-owner inspection established that Rhino ran under the nuofa account. The helper's persisted initial `host/before.json` instead identified the Novan account and its prior 1.2.2 installation. Only pre-install backups existed; no independent after/post-restart attestation was present. Filesystem inspection showed that the Novan attempt had moved its old plug-in to rollback and staged/placed 1.3.6 without completing its ownership manifest. The generic 'operation completed' reported by the user is not installation proof.

This exposed a delivery-helper defect: its first version instructed the user to use the Rhino account but did not enforce that account before activation. Added a mandatory target SID/previous-loaded-RHP check derived from the observed Rhino process, per-account evidence directories, a read-only CheckAccount mode, output/error logs, and rollback-error preservation. Legacy evidence is retained for Restore under its original account. No installed files or registry entries were repaired from the agent context.

PowerShell parsing, CheckAccount under the expected execution account, and staged-package hash Preflight passed after the helper-only repair. Server code/package contents did not change, so plugin builds were not repeated. The Router session was closed to avoid blocking the retry. Installation and actual model comparison are still pending an ordinary-host retry under the Rhino account; the wrong-account attempt also needs its prepared Restore step.

## Independent host activation and live completion (2026-10-12)

The user reported running the prior-account Restore step successfully. That recovery restored the prior reachable RHP and registry; it is not evidence of a complete Router/ownership-manifest rollback for that other account.

On reassessment, the agent process was already signed in under the same account as Rhino. A temporary Windows scheduled task with that existing interactive token and Limited run level launched a read-only host observation. It required no password or elevation. The observer recorded parent `svchost.exe`, the expected SID, a fresh challenge, and the true reachable 1.3.3 registration. This independently established a host process route before any activation. `Read-HostRegistration.ps1` preserves the observation recipe. The task was removed after completion.

Added `DeferRestartVerification` to the delivery helper and `Invoke-HostActivationStep.ps1` to capture noninteractive host step transcripts. Deferral returns after installation/attestation and does not waive post-startup checks. All three helper scripts passed PowerShell parsing, and bundle hash Preflight passed. No production C# or staged binary changed in this follow-up.

A second temporary task ran installation through that verified host route. Installer and separate-process Validate/attestation passed; the task exited 0 and was removed. Evidence is under `.validation/block-member-inspection/`: `host-install-bded5283e39c449487212c80966b49cf.log`, the per-account before/after JSON, registry backup and prior RHP backup. All three registry key times advanced from the prior registration to FILETIME 134363092889157307. The exact installed RHP is version 1.3.6.

The user explicitly retained responsibility for launching Rhino and opened it themselves. No Rhino launch or UI automation was performed. A temporary independent host verification task confirmed Rhino process 18132 loaded the exact 1.3.6 RHP. Root and CommandList times advanced to 134363096827553947, while installer-owned PlugIn remained 134363092889157307. `host-poststart-dc39e2941b624b6bbbfc0dac5ef826c9.log` and `host-products-dc39e2941b624b6bbbfc0dac5ef826c9.json` preserve this evidence. PanelCladdingEditor was already at repository version 1.0.88 and was also loaded from its matching registered RHP. No PanelCladdingEditor installation was necessary. Verification task exited 0 and was removed; no temporary scheduled tasks remain.

The installed Router then selected the user's live combined document, exposed `inspect_block_members`, and completed `Audit-LiveStoneSizes.mjs` with exit 0. Both full reads and instance inventories agreed. Results in ignored `.validation/block-member-inspection/combined-stn-audit.json`:

- 989 placed block instances; 1,741 surface occurrences; 210 STN IDs.
- 100 repeated-ID groups match; 2 repeated-ID groups mismatch; 108 singleton groups.
- No missing STN IDs, unsupported surfaces, or unchecked groups.
- Each failing group contains one surface 24 inches longer than its same-ID peers; both are in the same assembly occurrence. Full IDs, dimensions, occurrence identities and ancestry are in the local report.
- Maximum difference among passing repeated-ID groups: approximately 0.0000006923 inch.
- Runtime activity was appended through `append_activity_log`; no model writes, selection changes or saves were performed.

Construction and requested live inspection are complete. The task's read-only comparison result does not authorize changing stone IDs or geometry.
