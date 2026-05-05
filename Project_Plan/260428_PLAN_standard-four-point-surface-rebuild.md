# Standard Four Point Surface Rebuild

## 背景

Gravity-aware point order rebuild 已能把四点 surface/panel 统一为：front-face local coordinates、lower-left 作为点 1、顺时针点序。实际 Rhino `Dir` 方向仍需在重建后做标准化：先 flip normal，再 swap UV。用户确认这是 rebuild 后多数场景需要的默认方向状态。

## 目标

- 将 `FlipNormal + SwapUV` 设为 surface direction tweak 能力的默认 operation list。
- 新增一个 MCP 可见的标准四点 surface rebuild 工作流入口。
- 标准工作流固定执行：
  1. surface-local gravity frame reference
  2. lower-left point as point 1
  3. clockwise point order
  4. rebuild four-point surface
  5. post-rebuild `FlipNormal`
  6. post-rebuild `SwapUV`
- 工具描述明确声明：LLM 遇到 “rebuild 4 point surfaces / panels” 时应优先使用该标准工具。

## 架构归属

- `Domain/Rules`：沉淀标准 post-rebuild direction operation list。
- `Skills/Modeling`：新增固定流程 Skill，组合 surface point-order rebuild 与 surface direction tweak。
- `Tools/Geometry/Rebuild`：新增 MCP 可见 Apply tool，调用该 Skill。
- `Contracts/Responses`：新增标准 rebuild workflow response。
- `Project_Test/260428_TEST_standard-four-point-surface-rebuild`：新增 CLI fallback / live smoke。

## 关键设计

- `SurfaceDirectionTweakDefaults` 返回标准 operation list：`FlipNormal` then `SwapUV`。
- 低层 `PreviewTweakSurfaceDirections` / `ApplyTweakSurfaceDirections` 的 `operations` 参数允许为空；为空时自动使用标准 default。
- 标准 rebuild Tool 不接受自由方向参数，避免 LLM 在普通 “rebuild” 任务中漏掉或改错 post direction。
- 标准 rebuild Skill 先调用 `ISurfaceRebuildOrchestrator.Apply`，再对成功重建的对象调用 `ISurfaceDirectionTweakOrchestrator.Apply`。
- 本轮复用现有两个 Undo record；后续如果需要绝对原子性，可把 direction tweak 下沉到 rebuild replacement geometry 上，合并为一个 Undo record。

## 涉及文件

- `src/MCP_Rhino.Server/Domain/Rules/SurfaceDirectionTweakDefaults.cs`
- `src/MCP_Rhino.Server/Application/Services/Rebuild/SurfaceDirectionTweakOrchestrator.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/PreviewTweakSurfaceDirectionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/ApplyTweakSurfaceDirectionsTool.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/StandardFourPointSurfaceRebuildSkill.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/ApplyStandardFourPointSurfaceRebuildTool.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/StandardFourPointSurfaceRebuildResponse.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpStandardFourPointSurfaceRebuildSmokeCommand.cs`
- `Project_Test/260428_TEST_standard-four-point-surface-rebuild/`

## 使用方式

- 默认四点重建：
  - `ApplyStandardFourPointSurfaceRebuild(filePath, confirmedObjectIds)`
- 低层方向 tweak：
  - `ApplyTweakSurfaceDirections(filePath, confirmedObjectIds)` 使用默认 `FlipNormal + SwapUV`
  - 显式传入 operations 时仍按显式列表执行

## 验收标准

- Direction tweak tool 空 operations 时返回/执行 `FlipNormal + SwapUV`。
- 标准 rebuild tool 对四点 surface 使用 clockwise/lower-left/local coordinates 后自动执行 post direction tweak。
- CLI fallback smoke 返回 `LIVE_RHINO_REQUIRED`，不静默离线执行。
- Server / Bridge / Solution build 通过。
- `dotnet test MCP_Rhino.sln --no-build` 通过。
- Live smoke 可在 Rhino reload 后运行 `_McpStandardFourPointSurfaceRebuildSmoke`。

## 风险与回退方案

- 当前标准 workflow 使用两个 Undo records，用户可连续 Undo 回退；如果需要单一 Undo record，后续可把方向 tweak 合并进 rebuild geometry preparation。
- 如果某对象 rebuild 成功但 direction tweak skipped，会在 response 中明确标出该对象的 direction phase skip。
- 回退代码时删除新增 standard workflow files，并还原 direction default normalization。

## 后续扩展方向

- 将标准 workflow 合并为单 Undo record。
- 增加 preview-of-standard workflow，直接预览 rebuild geometry 加 post direction 后的最终 snapshot。
- 增加按 layer/filter 选择四点 surface 的 Skill wrapper。
