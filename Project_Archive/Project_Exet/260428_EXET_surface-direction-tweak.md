# Surface Direction Tweak EXET

## 对应计划

- Plan: `Project_Plan/260428_PLAN_surface-direction-tweak.md`
- 执行日期: 2026-04-28

## 关联产物

- Test: `Project_Test/260428_TEST_surface-direction-tweak/`
- Commit / PR: 当前工作区未提交

## 执行结果 / 实际落地范围

- 新增 live-only surface direction tweak 能力。
- 新增 MCP tools:
  - `PreviewTweakSurfaceDirections`
  - `ApplyTweakSurfaceDirections`
- 新增支持操作:
  - `ReverseU`
  - `ReverseV`
  - `SwapUV`
  - `FlipNormal`
- 支持对象:
  - Rhino `Surface`
  - single-face `Brep`
- Apply 使用 Undo record: `MCP:TweakSurfaceDirections`
- Apply 保留 ObjectId，并通过 metadata snapshot / replay 保留对象属性。

## 与计划的偏差

- 实现中未单独保留 Application-level `PreparedSurfaceDirectionTweak` model，最终将 prepared payload 限定在 live adapter 内部，以减少跨层暴露 RhinoCommon geometry 的范围。
- Live smoke 已注册但未在本轮终端验证，因为需要 Rhino 重新加载新构建的 `.rhp` 后执行 `_McpSurfaceDirectionTweakSmoke`。

## 施工中发现并修复的问题

- RhinoCommon API 签名经构建验证可直接调用:
  - `Surface.Reverse(0, true)`
  - `Surface.Reverse(1, true)`
  - `Surface.Transpose(true)`
  - `BrepFace.OrientationIsReversed`
- `FlipNormal` 对 single-face Brep 使用 face orientation flag；对裸 Surface 使用 U reverse 实现 normal flip。

## 测试记录

- `dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- surface-direction-tweak-smoke-test Runtime_Test\MCP_rhino_test.3dm`
  - Exit code: 0
  - Result: CLI fallback confirmed preview/apply return `LIVE_RHINO_REQUIRED`
- `dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet build MCP_Rhino.sln`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet test MCP_Rhino.sln --no-build`
  - Exit code: 0

## 验收判据对齐

- Live-only enforcement: CLI fallback smoke passed.
- Preview path: implemented without Undo record or document mutation.
- Apply path: implemented with `ExecuteWithUndo`.
- Metadata preservation: implemented through `IGeometryMetadataOperator` snapshot / replay.
- ObjectId preservation: implemented through `doc.Objects.Replace(objectId, replacement, false)`.
- API mapping aligned with Rhino Direction Display / Dir operations.

## 回退验证

- Runtime geometry changes are covered by Rhino Undo record `MCP:TweakSurfaceDirections`.
- Code rollback is isolated to new surface-direction files plus DI / CLI registration additions.

## 当前遗留项

- Run `_McpSurfaceDirectionTweakSmoke` inside Rhino after reloading the rebuilt plugin.
- After reload, use the MCP tools on the rebuilt panels to correct U/V direction as needed.

## 结论

Surface direction tweak capability is implemented and non-live verification passed. Live verification is ready after Rhino plugin reload.
