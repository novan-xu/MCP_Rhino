# 260421_EXET_layer-management-tools

## 对应计划

- Plan: [Project_Plan/260421_PLAN_layer-management-tools.md](/C:/01_Projects/MCP_Rhino/Project_Plan/260421_PLAN_layer-management-tools.md)
- 执行日期: 2026-04-21

## 关联产物

- 测试产物目录: [Project_Test/260421_TEST_layer-management-tools](/C:/01_Projects/MCP_Rhino/Project_Test/260421_TEST_layer-management-tools)
- 本轮未产出 commit / PR 链接

## 执行结果 / 实际落地范围

- Domain:
  - 新增 [RhinoLayerDetail.cs](/C:/01_Projects/MCP_Rhino/src/MCP_Rhino.Server/Domain/Models/RhinoLayerDetail.cs)，承载图层离线读取详情。
- Contracts:
  - 新增 `Get/Create/Modify/Delete/Purge/Preview*` 相关 request DTO。
  - 新增 `LayerReadResponse`、`LayerMutationResponse`、`LayerModificationPreviewResponse`、`LayerDeletionPreviewResponse` 及配套 result / impact DTO。
- Application:
  - 新增 [RhinoLayerManagementService.cs](/C:/01_Projects/MCP_Rhino/src/MCP_Rhino.Server/Application/Services/RhinoLayerManagementService.cs)。
  - 已实现离线 `Get`、live `Create/Modify/Delete/Purge`、live preview `PreviewModify/PreviewDelete/PreviewPurge`。
  - mutation 全部走 `ILiveRhinoDocumentAccessor.ExecuteWithUndo(...)`，preview 全部走 `Execute(...)`。
- Tools:
  - 新增 8 个 `Tools/Layers/*Tool.cs`，对应计划中的 layer management MCP tools。
- Infrastructure:
  - 在 [DependencyInjection.cs](/C:/01_Projects/MCP_Rhino/src/MCP_Rhino.Server/Server/DependencyInjection.cs) 注册 `RhinoLayerManagementService`。
  - 在 [DeveloperCommandHandler.cs](/C:/01_Projects/MCP_Rhino/src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs) 接入 `layer-management-smoke-test`。
  - 更新 [McpDevSmokeCommand.cs](/C:/01_Projects/MCP_Rhino/src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs)，Rhino 内 `_McpDevSmoke` 现转发到 `layer-management-smoke-test`。
- Test:
  - 新增 [DeveloperCommandHandler.LayerManagementSmokeTest.cs](/C:/01_Projects/MCP_Rhino/Project_Test/260421_TEST_layer-management-tools/DeveloperCommandHandler.LayerManagementSmokeTest.cs)。
  - CLI fallback 已验证；live smoke 已接线，但本轮环境未实际进入 Rhino 手工执行。

## 与计划的偏差

- Preview impact DTO 额外加入了 `Success` 与 `Message` 字段。
  - 原因: 计划中的 preview DTO 只有 impact 数据，没有承载单项失败信息的位置，无法表达“某条 FullPath 未命中/参数非法，但其它项仍可返回”的结果。
- `PlotColor` / `PlotWeight` 的“显式值 vs 继承值”判断采用了近似映射。
  - `PlotColor` 通过与 display color 比较推断。
  - `PlotWeight` 把 `0.0` 视为 `<default>` / `null`。
  - 原因: 当前一版实现未继续深挖 RhinoCommon 更底层的“是否显式设置”标志位；先保证 API 可用与编译/烟测闭环。
- `CreateLayers` 对“auto-created parent + explicit create 同路径属性冲突”未单独做专门 upfront rule。
  - 当前实现通过按层级深度先创建父层来规避大多数冲突路径。

## 施工中发现并修复的问题

- RhinoCommon `LayerTable.Find(Guid, bool)` 已过时，改为 `Find(Guid, bool, -1)`。
- Rhino3dm `File3dm.Materials` 已过时，改为 `AllMaterials`。
- smoke test 文件内 `Rhino.DocObjects.Layer` 与当前命名空间 `MCP_Rhino.Server.Infrastructure.Rhino` 产生名称遮蔽，已改为 `global::Rhino.DocObjects.Layer`。
- layer service 首次编译时暴露了一个 nullable 路径归一化调用，已补空值保护。

## 测试记录

- 构建命令:
  - `dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo`
  - 结果: PASS
  - 输出摘要: `Build succeeded. 0 Warning(s) 0 Error(s)`
- 构建命令:
  - `dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj --nologo`
  - 结果: PASS
  - 输出摘要: `Build succeeded. 0 Warning(s) 0 Error(s)`
- CLI fallback smoke:
  - `dotnet run --project src\MCP_Rhino.Server -- layer-management-smoke-test test-files\MCP_METtest.3dm`
  - 结果: PASS
  - 关键输出:
    - `Initial layers: 30`
    - `Final layers: 30`
    - `Initial objects: 1014`
    - `Final objects: 1014`
    - `GetLayers ok`
    - `Create/Modify/Delete/Purge/Preview* rejected in CLI fallback`
    - `Working copy remained unchanged`
  - 工作副本:
    - [MCP_METtest.layer-management-tools.3dm](/C:/01_Projects/MCP_Rhino/_validation/layer-management-tools/MCP_METtest.layer-management-tools.3dm)

## 验收判据对齐

- `GetLayers` 提供离线读取结果: PASS
  - 证据: CLI smoke 中 `GetLayers ok`，并校验 `TotalCount == initialLayerCount`。
- live mutation / preview 在 CLI fallback 返回 `LIVE_RHINO_REQUIRED`: PASS
  - 证据: smoke 逐项断言 `CreateLayers`、`ModifyLayers`、`DeleteLayers`、`PurgeLayers`、`PreviewModifyLayers`、`PreviewDeleteLayers`、`PreviewPurgeLayers` 的失败消息。
- CLI fallback 不改动工作副本: PASS
  - 证据: layer/object 计数前后分别保持 `30` / `1014`。
- `MCP_Rhino.Server` 与 `MCP_Rhino.Bridge` 可构建: PASS
  - 证据: 两个 `dotnet build` 均零 warning / 零 error。
- Rhino 内 live smoke 与 Undo 验证: N/A
  - 证据: 本轮环境未进入 Rhino 交互宿主；已完成 `_McpDevSmoke -> layer-management-smoke-test` 接线。

## 回退验证

- N/A
- 本轮主要新增文件与 DI/命令接线；若需回退，可直接移除新增 layer-management 相关文件并撤销 DI / CLI / plugin 接线。

## 当前遗留项

- 未在真实 Rhino live host 中手工跑 `_McpDevSmoke`，因此尚未验证:
  - `DeleteLayers` 的对象上浮行为在 live 文档中的实际结果。
  - `PurgeLayers` 的级联删除 message 与对象数是否完全符合预期。
  - Undo 栈中每个 tool 调用是否都呈现为独立、可回退的一条记录。
- `PlotColor` / `PlotWeight` 的“继承态”判定仍是近似实现，后续如果需要严格区分显式设置与继承状态，需要继续补 RhinoCommon 侧探针或更底层 API 解析。
  - 具体 follow-up:另起一个 layer-source-probe(复用 `_McpLayerBehaviorProbe` 同款结构），测 `Rhino.DocObjects.Layer.PlotColorSource` / `LinetypeSource` / `RenderMaterialSource` 等 `Source` 枚举属性是否存在、取值含义是否能区分 `ByLayer` / `ByParent` / `ByObject`；若可区分，把 service 里 `InferPlotColor` / `InferPlotWeight` 的启发式替换成基于 Source 枚举的精确判断。
- `LayerMutationResultResponse.Message` 中“(auto-created parent layers: [X], [Y])”这段是字符串拼接，没有结构化字段暴露。未来若下游 LLM 工具链要对 auto-created 父层做后续编辑(比如补颜色/锁定)，建议在 `LayerMutationResultResponse` 加 `AutoCreatedParentFullPaths: IReadOnlyList<string>`，Message 保留人读摘要。本期不阻塞 MCP 客户端用例。
- 当前未补 developer-facing 单命令 CLI（例如直接 `get-layers` / `create-layers`），只补了 smoke command 与 MCP tools。

## 结论

- 当前代码已达到“可构建 + CLI fallback 可验证 + MCP tool / service / smoke 接线完整”的状态。
- 进入下一阶段前，建议在真实 Rhino 8 中对 `_McpDevSmoke` 与 Undo 行为补一轮手工 smoke，重点覆盖 delete/purge 的 live 语义与 plot/material/linetype 字段的读写闭环。
