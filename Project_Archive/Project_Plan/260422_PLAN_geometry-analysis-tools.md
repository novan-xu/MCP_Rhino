# 260422_PLAN_geometry-analysis-tools

## 背景

MCP_Rhino 当前已有 Layer / 几何创建 / 几何修改 / UserText / DocumentUserString / 筛查
（Filter）等能力，但**几何度量与分析**这一大类仍然缺失：LLM 既无法回答"这条曲线有多
长 / 这个面有多大面积"这类基础问题，也无法完成"两个 Brep 的求交线在哪 / 某点到曲面的
最近点是多少 / 这条边连续性是 G1 还是 G2"这类分析判断。下游 Agent 做尺寸校验、设计审
计、装配冲突检查、工程量统计时，仍然依赖用户回到 Rhino 手工执行命令并回填数值，链路
被截断。

本期把 geometry analysis 明确收敛为 **纯 Live、纯只读** 能力：

- **只做 RhinoCommon + `RhinoDoc.ActiveDoc` 路径**，不再为本期引入任何 offline Tool / offline calculator。
- 所有能力统一通过 `*InLive` Tool 暴露，读取当前 Rhino 会话中的真实文档状态。
- 不写文档、不开 Undo record、不生成工作副本，测试统一直接使用 `Runtime_Test/MCP_rhino_test.3dm`。

## 目标

- 首批暴露 10 类几何分析能力给 MCP Client：
  - **度量**：长度 / 面积 / 周长 / 体积 / 是否闭合。
  - **测距与测角**：点、曲线、曲面之间距离；三点 / 双向量 / 双曲线切向夹角。
  - **框架与方向**：曲线 start/end/tangent、曲面 normal/frame。
  - **曲率与连续性**：曲线 / 曲面曲率采样、G0/G1/G2 连续性检查。
  - **几何求解**：求交、最近点、截面 / 等高线、mass properties。
- 10 类能力全部只提供 `*InLive` 入口，客户端不再有 offline 回退分支。
- 输入支持 ObjectId 列表；部分 live Tool 额外支持临时几何 Spec。
- 返回按 ObjectId 或 entry 对齐，不在 Tool 层做聚合求和。
- 全链路保持只读：不修改对象、不改 layer / user text / document user strings、不产生 Undo 条目。

## 架构归属

- **Tools/Analysis/**：新增 10 个 Live Tool。
  - `GetObjectMetricsInLiveTool`
  - `MeasureDistancesInLiveTool`
  - `MeasureAnglesInLiveTool`
  - `GetGeometryFramesInLiveTool`
  - `GetCurvatureSamplesInLiveTool`
  - `CheckContinuityInLiveTool`
  - `GetMassPropertiesInLiveTool`
  - `IntersectObjectsInLiveTool`
  - `GetClosestPointsInLiveTool`
  - `GetContourCurvesInLiveTool`
- **Application/Services/Analysis/**：新增 3 个 Service。
  - `RhinoGeometryMetricsService`
  - `RhinoGeometryCurvatureService`
  - `RhinoGeometryIntersectionService`
- **Application/Interfaces/**：新增 3 个 Live calculator 接口。
  - `ILiveGeometryMetricsCalculator`
  - `ILiveGeometryCurvatureCalculator`
  - `ILiveGeometryIntersectionCalculator`
- **Infrastructure/Rhino/Live/**：新增 3 个 RhinoCommon 实现。
  - `LiveRhinoGeometryMetricsCalculator.cs`
  - `LiveRhinoGeometryCurvatureCalculator.cs`
  - `LiveRhinoGeometryIntersectionCalculator.cs`
- **Domain/Models/**：新增分析结果模型。
  - `GeometryMetricsResult`
  - `GeometryFrameResult`
  - `GeometryCurvatureSample`
  - `GeometryContinuityResult`
  - `GeometryMassResult`
  - `GeometryIntersectionResult`
  - `GeometryClosestPointResult`
  - `GeometryContourResult`
- **Domain/Enums/**：新增分析枚举。
  - `GeometryFrameKind`
  - `GeometryContinuityKind`
  - `GeometryMassKind`
  - `GeometryIntersectionKind`
  - `GeometryClosestPointTargetKind`
- **Contracts/{Requests,Responses}/**：新增 10 类 Request / Response 与若干 entry DTO。
- **Project_Test/260422_TEST_geometry-analysis-tools/**：新增本期唯一测试文件夹。
  - `DeveloperCommandHandler.GeometryAnalysisSmokeTest.cs` 作为唯一 partial 扩展文件，负责注册 `geometry-analysis-smoke-test`。

## 关键设计

1. **10 类能力全部 Live-only**
   - 所有 Tool 统一走 `ILiveRhinoDocumentAccessor`，要求目标文档就是当前 `RhinoDoc.ActiveDoc`。
   - 不提供 `GetGeometryFrames` / `GetCurvatureSamples` 的 offline 变体；本期 scope 内完全删除 offline 分支。
   - `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE` 等错误直接向上返回，客户端不再做 geometry-analysis 的 offline fallback。

2. **Service 负责 ObjectId 解析，calculator 只负责计算**
   - Service 在 `doc.Objects` 中把 ObjectId 解析为 `GeometryBase`、`ObjectId`、`GeometryTypeName`。
   - Calculator 接口只接受几何对象与参数，返回 Domain DTO；不接触 `RhinoDoc`、不关心选择 / 文档匹配。
   - ObjectId 找不到时按 entry 失败处理，不阻断同一 Request 中其他 entry。

3. **临时几何 Spec 仅在支持该输入形态的 Live Tool 中可用**
   - `MeasureDistancesInLive` / `MeasureAnglesInLive` / `GetClosestPointsInLive` / `IntersectObjectsInLive` 可接临时几何 Spec。
   - 复用现有 `ILiveGeometryBuilder` 在内存中构建临时几何，不写盘、不入文档。
   - `GetGeometryFramesInLive` / `GetCurvatureSamplesInLive` / `CheckContinuityInLive` 仍只接受 ObjectId。

4. **曲率采样统一四种模式，但全部在 RhinoCommon 路径执行**
   - `EvenByCount`
   - `EvenByLength`
   - `AtParameters`
   - `AtUVList`
   - 单 entry 一次只允许一种 Mode；超过 1000 样点给 warning，超过 10000 样点给硬错误。

5. **连续性检查统一映射到 G0 / G1 / G2**
   - 默认阈值：
     - G0：端点距离 <= `1e-6`
     - G1：切向夹角 <= `1°`（约 `0.01745 rad`）
     - G2：曲率差比值 <= `5%`
   - 返回值同时带 `IsContinuous` 与具体度量值，方便客户端复核。
   - 一期只支持 `曲线/曲线` 与 `曲面/曲面（按指定边）` 两种组合。

6. **Mass properties 与交并分析统一放在 `RhinoGeometryIntersectionService`**
   - `GetMassPropertiesInLive` 对开放曲线不做降级兼容：`Kind=Area` / `Kind=Auto` 直接 per-entry 失败，并提示改调 `GetObjectMetricsInLive` 查长度。
   - `IntersectObjectsInLive` 的 `Tolerance` 缺省取 `doc.ModelAbsoluteTolerance`。
   - `SurfaceSurface` / `BrepBrep` 的 curves 与 overlaps 分开返回，不做合并。

7. **响应规模显式限流**
   - 单 Request 的 ObjectId 总数 > 5000：硬错误。
   - 单 Request 产出的 intersection / contour curves 总数 > 10000：硬错误。
   - 错误信息明确引导按 layer 或按 ObjectId 分批调用。

8. **全链路只读，不引入 mutation 风险**
   - 禁止调用 `doc.Objects.Add/Delete/Replace`、`doc.Strings.SetString` 等任何写入 API。
   - 不开 Undo record，不改 layer / user text / document user strings。
   - smoke 通过对象数、图层数、文档字符串数、对象 user text key 数与 Undo History 共同证明只读语义。

## 涉及文件

**新增（Tools）**：
- `src/MCP_Rhino.Server/Tools/Analysis/GetObjectMetricsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/MeasureDistancesInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/MeasureAnglesInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetGeometryFramesInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetCurvatureSamplesInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/CheckContinuityInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetMassPropertiesInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/IntersectObjectsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetClosestPointsInLiveTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetContourCurvesInLiveTool.cs`

**新增（Application）**：
- `src/MCP_Rhino.Server/Application/Services/Analysis/RhinoGeometryMetricsService.cs`
- `src/MCP_Rhino.Server/Application/Services/Analysis/RhinoGeometryCurvatureService.cs`
- `src/MCP_Rhino.Server/Application/Services/Analysis/RhinoGeometryIntersectionService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeometryMetricsCalculator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeometryCurvatureCalculator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeometryIntersectionCalculator.cs`

**新增（Infrastructure）**：
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryMetricsCalculator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryCurvatureCalculator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryIntersectionCalculator.cs`

**新增（Domain）**：
- `src/MCP_Rhino.Server/Domain/Models/GeometryMetricsResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryFrameResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryCurvatureSample.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryContinuityResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryMassResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryIntersectionResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryClosestPointResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryContourResult.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryFrameKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryContinuityKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryMassKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryIntersectionKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryClosestPointTargetKind.cs`

**新增（Contracts）**：
- 10 个 `*Request`
- 多个 entry DTO
- 10 个 `*Response`
- 路径统一落在 `src/MCP_Rhino.Server/Contracts/{Requests,Responses}/`

**新增（Test）**：
- `Project_Test/260422_TEST_geometry-analysis-tools/`
- `Project_Test/260422_TEST_geometry-analysis-tools/DeveloperCommandHandler.GeometryAnalysisSmokeTest.cs`

**修改**：
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryAnalysisSmokeCommand.cs`

**复用（不改）**：
- `ILiveRhinoDocumentAccessor`
- `ILiveGeometryBuilder`
- `ObjectEditWarning`

## 使用方式

MCP Tool 调用示例：

- `GetObjectMetricsInLive(filePath, objectIds)`
- `MeasureDistancesInLive(filePath, entries=[...])`
- `MeasureAnglesInLive(filePath, entries=[...])`
- `GetGeometryFramesInLive(filePath, entries=[...])`
- `GetCurvatureSamplesInLive(filePath, entries=[...])`
- `CheckContinuityInLive(filePath, entries=[...])`
- `GetMassPropertiesInLive(filePath, objectIds, kind:"Auto")`
- `IntersectObjectsInLive(filePath, entries=[...])`
- `GetClosestPointsInLive(filePath, entries=[...])`
- `GetContourCurvesInLive(filePath, entries=[...])`

调用约定：

- geometry-analysis 本期统一只调 `*InLive`。
- 调用前提是 Rhino Plugin 已加载、目标文件是当前已保存的 ActiveDoc、且 `doc.Path` 与 `filePath` 一致。
- 不存在 geometry-analysis 的 offline fallback；拿不到 live 文档时直接失败。

典型流程：

- "对当前 `Wall::Concrete` 层所有曲面统计总面积" → `FilterObjectsInLive(...)` → `GetMassPropertiesInLive(...)` → 客户端求和。
- "审计某条曲线与曲面在端点 G2 是否光滑" → `CheckContinuityInLive(filePath, entries=[...])`。

## 验收标准

构建 / smoke：

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过。
- `dotnet run --project src/MCP_Rhino.Server -- geometry-analysis-smoke-test Runtime_Test/MCP_rhino_test.3dm`
  在非 Rhino Plugin 环境下返回明确的 live-only 提示或 `LIVE_RHINO_REQUIRED`，不再假装提供 CLI fallback。
- 在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm` 后，运行 **`_McpGeometryAnalysisSmoke`**：
  - `McpGeometryAnalysisSmokeCommand` 直接调用 `"geometry-analysis-smoke-test"`。
  - 10 个 `*InLive` Tool 返回 `Success=true`。
  - `doc.Objects.Count`、`doc.Layers.ActiveCount`、`doc.Strings.Count`、object user text key count 前后无变化。
  - Rhino Undo History 无新条目。

边界用例：

- ObjectId 不存在：单 entry `Success=false`，其他 entry 不受影响。
- 临时几何 Spec 非法（NaN / 退化）：单 entry `Success=false`，其他 entry 不受影响。
- 曲率采样 1001 样点：成功 + warning；10001 样点：硬错误。
- `GetMassPropertiesInLive` 对开放曲线传 `Kind=Auto` 或 `Kind=Area`：单 entry `Success=false`，消息引导改调 `GetObjectMetricsInLive`。
- 求交无交点：成功，`Curves[]` 为空。
- ObjectId 数量超过 5000：硬错误。

## 风险与回退方案

风险：

- **大几何性能**：高密度 Brep 的 mass / intersection / contour 可能触发主线程忙。缓解方式是限制响应规模并引导分批调用。
- **响应体积**：曲率采样、求交结果可能撑大单条 MCP message。缓解方式是样点与结果数上限。
- **RhinoCommon 主线程约束**：所有 live 读都依赖 `ILiveRhinoDocumentAccessor` 正确封送到 UI 线程。

回退方案：

- 单 Tool 不稳定时，可单独移除对应 Tool 的 MCP 导出属性或回退该 Tool 文件，不影响其他分析 Tool。
- 整批回退时，可整体 `git revert` 本期提交；本期不改既有分析外接口签名，回退面可控。

## 后续扩展方向

- 后续若确实出现稳定、必要的离线分析诉求，再单独起新 plan 评估是否重引 offline 版本。
- 引入"批量统计 + 写回 user text"的复合 Skill。
- 引入 Mesh 拓扑健康度检查、Brep / Surface 反向工程类分析。
- 将 `GetCurvatureSamplesInLive` 进一步升级为曲率热图 / 极值摘要输出。

## 修订记录（2026-04-22）

- 本版按最新范围收口为 **live-only**，删除 geometry-analysis 相关 offline Tool / offline calculator / offline smoke 设计。
- 测试命名口径固定为：
  - `Project_Plan/260422_PLAN_geometry-analysis-tools.md`
  - `Project_Exet/260422_EXET_geometry-analysis-tools.md`
  - `Project_Test/260422_TEST_geometry-analysis-tools/`
- 测试输入统一直接使用 `Runtime_Test/MCP_rhino_test.3dm`，不再引入工作副本约定。
- Rhino live smoke 入口改为本期独立命令 `_McpGeometryAnalysisSmoke`，不再复用共享 `_McpDevSmoke`。

