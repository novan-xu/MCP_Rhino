# Grasshopper Authoring Tools EXET

## 对应计划

- Plan: [`Project_Plan/260805_PLAN_grasshopper-authoring-tools.md`](../Project_Plan/260805_PLAN_grasshopper-authoring-tools.md)
- Execute date: 2026-08-05

## 关联产物

- Test folder: [`Project_Test/260805_TEST_grasshopper-authoring-tools/`](../Project_Test/260805_TEST_grasshopper-authoring-tools/)
- Commit / PR: none created in this execution

## 执行结果 / 实际落地范围

- Added the ten canonical GH1 MCP tools under `Tools/Grasshopper`: definition list/start,
  component search/describe, graph read, graph preview/apply, solve, and clear preview/apply.
- Added engine-neutral typed requests, responses, graph specifications, diagnostics, revision values,
  limits, and a reserved `Gh2` discriminator.
- Added `RhinoGrasshopperAuthoringService` validation for bounds, duplicate client keys, slider
  ranges, target requirements, preview-token binding, expiry, and graph-spec fingerprinting.
- Added a lazy GH1 adapter using the installed Rhino 8 `Grasshopper.dll` and `GH_IO.dll`. The first
  GH request loads the Grasshopper plug-in through Rhino's plug-in API; normal MCP_Rhino startup and
  CLI tool scanning do not require GH to be loaded.
- Added deterministic definition sessions. A session resolves only while the same `GH_Document`
  remains in `Instances.DocumentServer` and its `RhinoDocument.RuntimeSerialNumber` matches the
  Router-selected Rhino document.
- Added installed component catalog search and exact GUID resolution. Hidden and obsolete proxies
  are excluded; installed Python/C#/script-code components remain discoverable and placeable and
  are explicitly classified.
- Added graph projection with nodes, wires, diagnostics, bounded volatile-data samples, truncation,
  and a structural/persistent revision fingerprint.
- Added batch placement of components, number sliders, and wires, suppression of intermediate
  solutions, one optional final solve, rollback cleanup, and one combined native GH undo record.
- Added stale-safe destructive clearing through the native GH undo server.
- Added DI, isolated load-context sharing for `Grasshopper`/`GH_IO`, tool safety inventory updates,
  the unique CLI slug `grasshopper-authoring-tools-smoke-test`, and Rhino command
  `_McpGrasshopperAuthoringToolsSmoke`.
- Updated the Architecture Guide and root README with target selection, open-world code-component
  policy, and the Grasshopper-native undo exception.

## 与计划的偏差

- The installed GH1 API exposes `GH_Document.EnableSolutions` as a process-global static switch,
  not a per-definition property. Batch mutation therefore disables it only inside the UI-thread
  critical section and restores it in `finally`; definition targeting remains explicit.
- The Phase 0 probe was implemented as a standalone reflection project under the TEST folder. It
  proved the required public API surface without creating a second production adapter assembly.
- The pre-existing overlap smoke had a stale hardcoded count of 156. Actual discovery is now 169;
  the new `Grasshopper` family accounts for exactly 10 tools, and the smoke expectation/output were
  updated to the real inventory.
- The live Rhino/Router smoke was not launched automatically. Repository policy forbids Windows UI
  automation without explicit authorization, and no anonymous saved Rhino fixture/live GH session
  was supplied. A manual Rhino smoke command is included for that validation.

## 施工中发现并修复的问题

- Initial reflection probing failed on `System.Windows.Forms` and `Eto` signatures. The probe was
  changed to a Windows desktop target, added Rhino shared-assembly resolution, and made individual
  signature inspection tolerant; it then completed with exit code 0.
- Direct GH references risked eager resolution before Rhino loaded Grasshopper. GH-dependent cores
  were moved behind no-inline lazy boundaries so `Rhino.PlugIns.PlugIn.LoadPlugIn` completes before
  the GH implementation methods are JIT-compiled.
- The first undo implementation pushed separate wire/object records and merged them. It was replaced
  by one explicit `GH_UndoRecord` containing add-object actions followed by wire actions, giving
  correct undo/redo ordering and a single record.
- The initial revision fingerprint covered topology only. It was extended with names, nicknames,
  pivots, lock state, slider domains/values/precision, parameter metadata, and source identities so
  relevant edits invalidate previews.
- CLI-host and RHP outputs share configuration folders, which can leave a stale `.deps.json` when
  switching output shapes. Final CLI smokes used clean builds, followed by final solution RHP builds.

## 测试记录

### API probe

Command:

```powershell
dotnet run --project Project_Test\260805_TEST_grasshopper-authoring-tools\ApiProbe\GrasshopperApiProbe.csproj -c Release
```

Result: exit code 0. Verified installed versions and APIs:

- RhinoCommon `8.33.26188.13001`
- Grasshopper `8.33.26188.13001`
- GH_IO `8.33.26188.13001`
- GrasshopperPlugin.rhp `8.33.26188.13001`, id
  `B45A29B1-4343-4035-989E-044E8580D9CF`
- `Instances.DocumentServer`, `GH_Document.DocumentID`, `GH_Document.RhinoDocument`
- component proxies / `CreateInstance`, graph objects/parameters/wires, volatile data
- `GH_Document.NewSolution`, native undo record/action creation, and undo server push/remove

### CLI contract and inventory smokes

Executed in both Debug and Release CLI-host builds:

```powershell
MCP_Rhino.Server.exe grasshopper-authoring-tools-smoke-test
MCP_Rhino.Server.exe mcp-tool-safety-annotations-smoke-test
MCP_Rhino.Server.exe mcp-tool-overlap-cleanup-smoke-test
```

Results in both configurations:

- `[OK] Grasshopper authoring tool surface and contract validation passed.`
- `[OK] MCP safety annotations verified for 169 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`
- `[OK] MCP tool overlap cleanup smoke verified canonical tools and removed duplicate wrappers.`
- `[OK] MCP tool count=169; removed duplicate wrappers=11.`

The Release surface-governance smoke also reported `Tool family Grasshopper: 10` and total tool
inventory 169.

### Required solution builds

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

Final results: both exit code 0, 0 warnings, 0 errors. Debug produced
`src/MCP_Rhino.Server/bin/Debug/net8.0/MCP_Rhino.Server.rhp`; Release produced the matching Release
RHP.

### Packaging and plug-in identity checks

- Debug output duplicate `Grasshopper.dll` / `GH_IO.dll` / `RhinoCommon.dll` / `Rhino.UI.dll` /
  `Eto.dll`: 0
- Release output duplicate count: 0
- `Project_Test/260805_TEST_rhino-plugin-assembly-identity/Verify-PluginAssemblyIdentity.ps1`:
  exit code 0; MCP_Rhino assembly plug-in id verified as
  `7a3fc2f0-24a8-4b79-be58-5a08cfb0d10a` and distinct from PanelCladdingEditor.

### Live smoke

Not executed in this session. The owned command `_McpGrasshopperAuthoringToolsSmoke` performs a
non-destructive live start/binding/catalog read smoke against a saved active Rhino document. Full
graph apply/undo/clear behavior remains a manual live acceptance item.

## 验收判据对齐

- **Build and tool surface:** met for Debug/Release builds, unique methods/descriptions, explicit
  annotations, inventory, CLI registration, assembly sharing, and no packaged duplicate GH runtime.
- **Contracts and validation:** met by the owned smoke for typed JSON, no raw script-source field,
  duplicate keys, slider validation, engine gating, preview fingerprints, stale-token rejection,
  and allowed/classified script-code component placement.
- **Deterministic target architecture:** implemented using opaque sessions plus the direct
  `GH_Document.RhinoDocument.RuntimeSerialNumber` binding; active canvas is not read for targeting.
- **Live GH1 behavior:** implementation and API surface compile, but the plan's interactive live
  multi-definition, undo, solve, rollback, stale-clear, and Router-disconnect scenarios are not yet
  empirically signed off.
- **Documentation/artifacts:** PLAN, TEST, Architecture, README, and this EXET are present with
  matching capability/date names; no `.3dm`, `.gh`, or `.ghx` fixture was added.

## 回退验证

- The implementation is isolated under `Tools/Grasshopper`, Grasshopper contracts/domain/service,
  and `Infrastructure/Rhino/Live/Grasshopper`, plus narrow DI/load-context/CLI/project references.
- Removing those files and registrations restores the prior tool surface; no Rhino document or GH
  file schema migration exists.
- Failed apply paths remove connected sources and newly added objects and remove any pushed native
  undo record; this path is structurally covered but still requires live fault-injection validation.

## 当前遗留项

- Run `_McpGrasshopperAuthoringToolsSmoke` in Rhino 8 against an anonymous saved `.3dm`.
- Complete the plan's live two-definition targeting, batch apply, one-step GH undo, solve diagnostics,
  rollback fault injection, stale clear, non-GH regression, and Router disconnect checks.
- GH2 remains intentionally unavailable until a separate Rhino 9 capability is approved.
- Raw script-source creation/editing remains outside this capability; installed script/code
  components themselves are supported.

## 结论

The GH1 authoring capability is implemented and passes the repository's static, contract, inventory,
assembly, identity, and Debug/Release build gates. It is ready for manual live Rhino acceptance; that
remaining interactive validation is explicitly not claimed as complete here.
