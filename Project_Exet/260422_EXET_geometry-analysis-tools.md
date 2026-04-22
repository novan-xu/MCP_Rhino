# 260422_EXET_geometry-analysis-tools

## 对应计划

- 计划文档：`Project_Plan/260422_PLAN_geometry-analysis-tools.md`
- 执行日期：2026-04-22

## 关联产物

- 测试目录：`Project_Test/260422_TEST_geometry-analysis-tools/`
- CLI smoke 入口：`geometry-analysis-smoke-test`
- Rhino live smoke 命令：`_McpGeometryAnalysisSmoke`
- commit / PR：N/A（当前直接在工作区执行）

## 执行结果 / 实际落地范围

本次已落地 10 个 live-only 几何分析 Tool，并接通 `Contracts -> Application -> Infrastructure/Rhino/Live -> Tools` 全链路：

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

同时新增：

- `RhinoGeometryMetricsService`
- `RhinoGeometryCurvatureService`
- `RhinoGeometryIntersectionService`
- `ILiveGeometryMetricsCalculator`
- `ILiveGeometryCurvatureCalculator`
- `ILiveGeometryIntersectionCalculator`
- 3 个 RhinoCommon live calculator 实现
- 本期 smoke partial：
  `Project_Test/260422_TEST_geometry-analysis-tools/DeveloperCommandHandler.GeometryAnalysisSmokeTest.cs`
- 本期独立 Rhino 命令：
  `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryAnalysisSmokeCommand.cs`

## 与计划的偏差

1. 为保持 Tool / Service / calculator 间职责清晰，额外新增了辅助类型：
   - `Application/Models/ResolvedGeometryReference.cs`
   - `Domain/Models/GeometryDistanceResult.cs`
   - `Domain/Models/GeometryAngleResult.cs`
   - `Domain/Models/GeometryAnalysisValueData.cs`

2. `SurfaceSurface` 与 surface contour 的 RhinoCommon 实现统一先转成 `Brep` 再走 `BrepBrep` / `Brep.CreateContourCurves(...)`。
   - 原因：当前 RhinoCommon API 下更稳定，分支更少。

3. 原先实现里沿用了共享 Rhino 命令 `_McpDevSmoke`。
   - 已按最新要求改为本期独立 Rhino 命令 `_McpGeometryAnalysisSmoke`
   - 共享入口 `McpDevSmokeCommand.cs` 已删除

## 施工中发现并修复的问题

1. `Surface.CreateContourCurves(...)` 在当前 RhinoCommon 目标环境中不可用，已改为 `surface.ToBrep()` 后调用 `Brep.CreateContourCurves(...)`。

2. geometry-analysis 同时需要支持：
   - 纯 `ObjectId` 输入
   - 部分 Tool 的临时几何 Spec 输入
   已通过 `ResolvedGeometryReference` + `LiveGeometryAnalysisHelpers.ResolveReference(...)` 统一收口，避免 10 个 Tool 各自复制解析逻辑。

3. 曲率采样限流已前置到 Service：
   - `> 1000`：写入 `HIGH_SAMPLE_COUNT`
   - `> 10000`：直接失败

4. Rhino live smoke 命令策略已调整：
   - 不再使用共享 `_McpDevSmoke`
   - 每期能力需拥有独立 Rhino 命令名，便于多个功能组并行测试

## 测试记录

### 构建

命令：

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo
```

结果：

- 退出码：`0`
- 关键输出：`Build succeeded.`

### CLI fallback smoke

命令：

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-analysis-smoke-test test-files/MCP_rhino_test.3dm
```

结果：

- 退出码：`0`
- 使用 fixture：`test-files/MCP_rhino_test.3dm`
- 检查点全部通过：
  - `GetObjectMetricsInLive rejected in CLI fallback`
  - `MeasureDistancesInLive rejected in CLI fallback`
  - `MeasureAnglesInLive rejected in CLI fallback`
  - `GetGeometryFramesInLive rejected in CLI fallback`
  - `GetCurvatureSamplesInLive rejected in CLI fallback`
  - `CheckContinuityInLive rejected in CLI fallback`
  - `GetMassPropertiesInLive rejected in CLI fallback`
  - `IntersectObjectsInLive rejected in CLI fallback`
  - `GetClosestPointsInLive rejected in CLI fallback`
  - `GetContourCurvesInLive rejected in CLI fallback`

### Rhino live smoke

已接入但本次未执行：

```text
在 Rhino 中打开并保存 test-files/MCP_rhino_test.3dm
运行命令：_McpGeometryAnalysisSmoke
```

未执行原因：

- 当前执行环境是普通 shell，可编译并运行 CLI，但不持有可交互 Rhino Plugin live session。

## 验收判断对齐

- `dotnet build ...`：已通过
- `geometry-analysis-smoke-test` 在非 plugin 环境下返回 live-only 行为：已通过
- 10 个 Tool 已全部接入 `Tools/Analysis/`：已完成
- 依赖注入与 `DeveloperCommandHandler` smoke 入口：已完成
- 独立 Rhino live smoke 命令已提供：已完成
- Rhino 内 live smoke：代码已接入，待在 Rhino 会话中执行复核

## 回退验证

- 本期改动主要集中在新增文件和少量 DI / smoke 入口修改。
- 若需回退，可优先撤销：
  - `src/MCP_Rhino.Server/Tools/Analysis/*`
  - `Application/Services/Analysis/*`
  - 3 个 live calculator
  - `DependencyInjection.cs`
  - `DeveloperCommandHandler.cs`
  - `McpGeometryAnalysisSmokeCommand.cs`
  - `Project_Test/260422_TEST_geometry-analysis-tools/`
- 由于本期不改既有 mutation Tool 行为，回退面可控。

## 当前遗留项

1. 仍需在真实 Rhino Plugin 会话中执行 `_McpGeometryAnalysisSmoke`，确认：
   - 10 个 Tool 的 live happy path 全部通过
   - 对象数 / 图层数 / 文档字符串数 / user text key 数不变
   - Undo serial 不变化

2. `CurveBrep` / `SurfaceSurface` / `Contour` 的 happy path 目前主要依赖实现逻辑与编译通过，尚未在 Rhino 会话中做最终运行态复核。

## 结论

本期 `geometry-analysis-tools` 已按 **live-only、read-only** 方向完成主实现，构建通过，CLI fallback smoke 通过，测试与 EXET 产物已归档。剩余工作是进入 Rhino live 会话执行 `_McpGeometryAnalysisSmoke` 做最终运行态确认。
