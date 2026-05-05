# Surface Front Back Flip

## 背景

标准四点 surface rebuild 目前执行 `FlipNormal + SwapUV`。现场复查发现 Dir-style `FlipNormal` 改变了 normal / parameterization，但没有可靠触发 Rhino frontface/backface display 的翻转。用户确认需要在 rebuild 之后、Dir 操作之前执行 Rhino `Flip` 命令语义的 front/back face flip；之后 Dir 操作只执行 UV 方向调整。

## 目标

- 新增 live-only front/back face flip 能力，语义对应 Rhino `Flip` 命令。
- 标准四点 rebuild 流程改为：
  1. local lower-left clockwise rebuild
  2. front/back face flip
  3. Dir-style `SwapUV`
- 从标准 rebuild 默认 Dir 操作中移除 `FlipNormal`。
- 保留低层 Dir `FlipNormal` 显式操作能力，但不再作为标准 rebuild 默认项。

## 架构归属

- `Application/Interfaces`：新增 live front/back flip service 与 orchestrator 抽象。
- `Application/Services/Rebuild`：新增 front/back flip orchestrator。
- `Infrastructure/Rhino/Live`：新增 RhinoCommon live implementation。
- `Contracts/Requests` / `Contracts/Responses`：新增请求响应 DTO。
- `Tools/Geometry/Rebuild`：新增 MCP Tool。
- `Skills/Modeling`：更新标准四点 rebuild Skill，在 direction tweak 前插入 front/back flip。
- `Project_Test/260428_TEST_surface-front-back-flip`：新增 smoke。

## 关键设计

- 对 single-face / multi-face `Brep` 使用 `Brep.Flip()`，对应 Rhino object face orientation flip。
- 对裸 `Surface` 使用 `Surface.Reverse(0, true)` 作为 front/back flip fallback。
- Mutation 通过 live document、Undo record、metadata snapshot / replay 执行。
- 标准 rebuild 的 direction phase 只执行 `SwapUV`。
- Response 分离 rebuild / front-back flip / direction tweak 三个阶段，便于排查。

## 涉及文件

- `src/MCP_Rhino.Server/Application/Interfaces/ILiveSurfaceFrontBackFlipService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceFrontBackFlipOrchestrator.cs`
- `src/MCP_Rhino.Server/Application/Services/Rebuild/SurfaceFrontBackFlipOrchestrator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceFrontBackFlipService.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplySurfaceFrontBackFlipRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SurfaceFrontBackFlipApplyResponse.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/ApplyFlipSurfaceFrontBackTool.cs`
- `src/MCP_Rhino.Server/Domain/Rules/SurfaceDirectionTweakDefaults.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/StandardFourPointSurfaceRebuildSkill.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/StandardFourPointSurfaceRebuildResponse.cs`

## 使用方式

- Direct flip:
  - `ApplyFlipSurfaceFrontBack(filePath, confirmedObjectIds)`
- Standard rebuild:
  - `ApplyStandardFourPointSurfaceRebuild(filePath, confirmedObjectIds)`
  - Now applies rebuild, then front/back flip, then `SwapUV`.

## 验收标准

- CLI fallback rejects front/back flip with `LIVE_RHINO_REQUIRED`.
- Server / Bridge / solution build pass.
- `dotnet test MCP_Rhino.sln --no-build` passes.
- Standard rebuild response reports front/back flip phase and direction phase separately.
- Direction default is `SwapUV` for post-rebuild Dir step.

## 风险与回退方案

- `Brep.Flip()` on multi-face Breps flips all faces; standard four-point panels are expected single-face Breps after rebuild.
- Current workflow still uses separate Undo records per phase.
- Rollback is Rhino Undo or code removal of the additive flip capability plus standard sequence edits.

## 后续扩展方向

- Merge rebuild, front/back flip, and SwapUV into one prepared geometry replacement and one Undo record.
- Add preview for front/back flip if needed.
