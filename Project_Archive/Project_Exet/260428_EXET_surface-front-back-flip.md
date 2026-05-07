# Surface Front Back Flip EXET

## 对应计划

- Plan: `Project_Plan/260428_PLAN_surface-front-back-flip.md`
- 执行日期: 2026-04-28

## 关联产物

- Test: `Project_Test/260428_TEST_surface-front-back-flip/`
- Commit / PR: 当前工作区未提交

## 执行结果 / 实际落地范围

- 新增 live-only front/back face flip 能力：
  - `ApplyFlipSurfaceFrontBack`
  - Undo record: `MCP:FlipSurfaceFrontBack`
- RhinoCommon implementation:
  - `Brep.Flip()` for Breps
  - `Surface.Reverse(0, true)` for bare Surface fallback
- 标准四点 surface rebuild 流程已改为：
  1. `MCP:RedefineSurfacePointOrder`
  2. `MCP:FlipSurfaceFrontBack`
  3. `MCP:TweakSurfaceDirections` with `SwapUV`
- Dir-style default operation set changed from `FlipNormal + SwapUV` to `SwapUV` only.
- Standard rebuild response now reports front/back flip phase separately.

## 与计划的偏差

- 未新增 preview front/back flip tool；本轮只新增 apply，因为用户需求是 rebuild 后强制 flip。
- 当前标准 rebuild 仍然是三个 Undo records，不是单一 Undo record。

## 施工中发现并修复的问题

- 原 `LiveSurfaceDirectionTweakService` 内部 snapshot logic 被提取为 `LiveSurfaceDirectionSnapshotFactory`，供 front/back flip 与 Dir tweak 复用。
- Smoke partial 中 `Dot` helper 与既有 partial 冲突，已重命名为 `FrontBackFlipDot`。

## 测试记录

- `dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- surface-front-back-flip-smoke-test Runtime_Test\MCP_rhino_test.3dm`
  - Exit code: 0
  - Result: CLI fallback confirmed `ApplyFlipSurfaceFrontBack` returns `LIVE_RHINO_REQUIRED`
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- standard-four-point-surface-rebuild-smoke-test Runtime_Test\MCP_rhino_test.3dm`
  - Exit code: 0
  - Result: CLI fallback confirmed standard rebuild remains live-only
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- surface-direction-tweak-smoke-test Runtime_Test\MCP_rhino_test.3dm`
  - Exit code: 0
  - Result: Existing direction tweak CLI fallback remains valid
- `dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet build MCP_Rhino.sln`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet test MCP_Rhino.sln --no-build`
  - Exit code: 0

## 验收判据对齐

- Front/back flip is now a distinct operation from Dir `FlipNormal`.
- Standard rebuild applies front/back flip before Dir.
- Standard Dir step now only defaults to `SwapUV`.
- Mutation remains live-only and uses Rhino Undo.
- Build and fallback smoke checks passed.

## 回退验证

- Runtime edits are covered by Rhino Undo records.
- Code rollback is isolated to the new front/back flip files plus edits to standard rebuild sequence, DI, CLI, and direction defaults.

## 当前遗留项

- Reload Rhino plugin and run `_McpSurfaceFrontBackFlipSmoke`.
- Reload Rhino plugin and run `_McpStandardFourPointSurfaceRebuildSmoke`.
- Rerun `ApplyStandardFourPointSurfaceRebuild` on `MCP_rhino_test.3dm` after reload to apply the corrected sequence.

## 结论

The front/back face flip capability is implemented. The standard four-point rebuild workflow now uses Rhino Flip semantics before the Dir `SwapUV` step.
