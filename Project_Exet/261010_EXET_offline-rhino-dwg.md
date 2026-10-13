# 261010_EXET_offline-rhino-dwg

## Corresponding plan

[PLAN](../Project_Plan/261010_PLAN_offline-rhino-dwg.md). Executed 2026-10-10 America/New_York (2026-10-11 UTC). The user explicitly authorized adding native offline DWG capability after the runtime gap was identified.

## Related artifacts

[TEST](../Project_Test/261010_TEST_offline-rhino-dwg/README.md), native probe/smoke and project-DWG acceptance executables in that folder. No commit or PR created. Existing unrelated worktree edits were retained. Production code lives in `src/MCP_Rhino.Offline/` and the new `src/MCP_Rhino.RhinoWorker/`, with Router registration/staging changes. Runtime routing and architecture guidance were updated.

## Delivered scope

Added `rhino_offline_preview_rhino_script` and `rhino_offline_apply_rhino_script` to the existing Router. Accepts `.dwg`, `.dxf`, `.3dm`, optional trusted synchronous C#, optional JSON parameters and single-class extraction from a linked `.cs`/`.csx` library. An empty script imports/converts. Uses the installed Rhino DWG importer and explicit headless RhinoDoc; no UI automation, extra MCP transport, or live ActiveDoc routing.

The source is held read-only denying write/delete while a temporary snapshot runs in an isolated Rhino.Inside worker. Preview validates and retains exact output bytes, bound to source hash/path for ten minutes. Apply rechecks the source under the same lock and uses the existing atomic create-new/non-replacing copy writer. Output always has `_offline-edit_<UTC timestamp>_<unique id>.3dm`. Failed/canceled/timed-out work publishes nothing. Native C# metadata correctly declares potential destructive external effects; this is full-trust code and is not the restricted JS interpreter.

The authoritative project-linked StoneSurfaceBake script gained only an explicit headless entry point calling its existing evaluator. Previous SHA-256 `2B250DA9DA620C38357876F020AD1B2B4047FD8B3968E9C2C926C2F86108A5E3` was backed up beside it as `StoneSurfaceBake__before_headless_20261010.cs`. Deployed and tested SHA-256 is `9EB14EFD7BF35EE7B9A4B2E1942FCBBFB00E8E4B5AE0ACB4373ED04862988E94`. Existing GH UI behavior, settings, lintel support and missing-only evaluation remain intact.

The requested lintel DWG was imported and baked with baseline Z offset 0 inches and height 4.46645 inches. Delivered sibling copy ends in `_offline-edit_20261011_023354_32470e96dc6147e58d3193a425019201.3dm`; exact private paths are recorded in ignored acceptance evidence. 26 lintel occurrences were baked into 26 assembly instances using six new definitions. All 38,830 imported persistent objects and the original DWG bytes/timestamp were preserved. Saved output has 38,862 objects, 912 definitions and 63 layers, in inches. Independent file readback verified direct Breps, STN_ID/Assembly_ID, and transformed heights. A fresh worker reopening the saved copy added zero objects/definitions.

## Deviations from plan

No functional scope change. Native startup succeeded with the Rhino.Inside resolver initialized before JIT reaches Rhino types. The resolver package is downloaded separately from its old Rhino/Grasshopper dependency graph; worker references the installed RhinoCommon and matching Roslyn 4.6 assemblies. RhinoCommon is not copied into the Router or bundled as a substitute for installed Rhino.

Native previews are conservatively annotated `ReadOnly=false, Destructive=true, OpenWorld=true` because caller-provided C# has full trust, despite final publication being a separate copy-only step. This explicit exception is recorded in the architecture guide.

## Problems found and fixed

1. Rhino `ObjectTable.Count` includes deleted DWG import intermediates (71,585 vs 38,830 persisted). The worker now enumerates top-level objects and definition members, then compares serialized identity sets rather than accepting or silently ignoring a mismatched count.
2. `IdefObjects=true` was an exclusive enumeration filter and returned no imported objects in this headless case. Acceptance now explicitly enumerates definition members and rejects an empty preservation check.
3. Saved-copy duplicate verification initially treated `BakedIds` as new objects. The library intentionally includes existing skipped IDs. The test now compares actual object/definition counts and new preview geometry. The first already-valid output copy was left intact per the no-overwrite rule; the final copy completed the corrected full test.
4. Server's broad external test source glob picked up generated `obj/*.cs` from the new standalone tests. Added an exact exclusion for this TEST folder, consistent with other standalone tests; no runtime smoke registrations were removed.

## Test record

- `Project_Test/261010_TEST_offline-rhino-dwg/Run-Validation.ps1`: exit 0. Debug/Release solution and CLI builds, existing bounded JS tests, Router protocol tests, native Rhino integration, source protection/freshness, failure/timeout, MCP safety and bundle presence/isolation checks passed. Builds reported zero warnings/errors.
- `OfflineRhinoSmoke.csproj` additionally ran with the final ASCII DXF case in Debug and against the packaged Release Router: exit 0. Empty C# script converted DXF to a suffixed 3dm with explicit inches and unchanged source.
- `Project_Test/261010_TEST_stone-bake-lintels/Run-Validation.ps1 -ScriptPath <staged linked-script bytes> -Configuration Debug/Release`: both exit 0, exact deployed hash. Full GH compile/setup compatibility, missing-only matching, Undo guards, lintel selection and 45 transformed offset/height cases passed.
- `DwgBakeAcceptance.csproj -c Release -- Release <original DWG> <deployed linked script> apply`: exit 0 after corrected existing-ID assertion. Native Rhino 8.35.26251.13001, 26 lintels, baseline Z=0, height=4.46645; source preserved; saved copy repeat adds 0. Output SHA-256 `65BB9146A0C094F5CECFF26131E18E2E9C6636D58B7C1755CD269721143ACD29`, 17,412,983 bytes.
- `Verify-PluginAssemblyIdentity.ps1` Debug/Release and packaged explicit RHP paths: exit 0. MCP_Rhino `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a`, PanelCladdingEditor `7c1a4d3b-5e29-4f68-9a72-1d8c6b0f4e35`: nonempty, manifest-matching, distinct compiled assembly GUIDs.
- `Packaging/MCP_Rhino/Build-McpRhinoPackage.ps1`: exit 0; package 1.3.4 staged. All 181 manifest hashes verified. Packaged Router `--validate-install`: exit 0. This is bundle validation, not production installation or host registry attestation.
- `git diff --check`: exit 0.

Logs: ignored `.validation/offline-rhino-dwg/{validation,native-Debug,native-package-Release,dwg-acceptance,package,package-identity,identity-Debug,identity-Release}.log`. Private project evidence: ignored `Runtime_Test/local/offline-rhino-dwg/dwg-bake-result.json`.

## Acceptance alignment

Reusable script-based DWG/DXF/3dm processing, source protection, clear suffix copies, actual native execution, exact user settings, original geometry retention, reopened output verification, and persistent missing-only bake all passed. Source hash remains `2CA3FAA9614B539C76AA59261AC93D693EF21A16D278DEDC1765CA1FA26B1654`. No originals or prior copies were removed or replaced.

## Rollback verification

The linked-script backup hash equals the previous deployed bytes; restoring it would remove only the new headless entry point. Production RHP/Router installation and registry were not changed, so no installed-component rollback was needed. New model copies are independent; original DWG hash/timestamp checks passed through native and final readback.

## Remaining limits

Package 1.3.4 is built/staged, not activated. AGENTS.md requires independent host-persistent registry attestation before installation/retiring prior RHPs; no such attestation was established or claimed. A supported staged Router session executed the actual task. Native scripting requires the installed licensed Rhino 8; JS remains usable without it. DWG proxy/xref/font fidelity remains the native importer's responsibility, and relative external references may require explicit handling because the worker imports a snapshot. No live UI/Grasshopper solve was tested or required.

## Conclusion

Capability construction and the requested offline lintel bake are complete and validated. The requested output is a separate `.3dm`; source and previous outputs remain intact.

## GitHub publication - 2026-10-13

Published with [PR #11](https://github.com/novan-xu/MCP_Rhino/pull/11), source commit `cae9c71`.
The branch integrates `origin/master` at `f6dc9d0`. Publication validation is recorded in
`Project_Exet/261012_EXET_live-custom-scripts.md`. Earlier acceptance boundaries in this record remain applicable.
