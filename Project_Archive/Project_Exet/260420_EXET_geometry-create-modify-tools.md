# 260420_EXET_geometry-create-modify-tools 执行总结

## 对应计划

- Plan: `Project_Plan/260420_PLAN_geometry-create-modify-tools.md`
- Execute: `Project_Execute/260420_EXET_geometry-create-modify-tools.md`
- 执行日期: `2026-04-20`

## 执行结果

- 已完成计划中的几何创建能力：`CreatePoints` / `CreateLines` / `CreateArcs` / `CreateSurfaces`
- 已完成计划中的几何修改能力：
  - Apply: `TransformObjects` / `ReplaceGeometry` / `DeleteObjects` / `EditControlPoints`
  - Preview: `PreviewTransformObjects` / `PreviewReplaceGeometry` / `PreviewDeleteObjects` / `PreviewEditControlPoints`
- 已完成 Domain / Contracts / Application / Infrastructure / Skills / Tools 全链路落地
- 已完成 `IEditResultFormatter` 扩展与 `PassThroughEditResultFormatter` 适配
- 已完成 `RhinoObjectFilterService.ResolveByObjectIds(...)`，用于显式 `ObjectId` 目标解析
- 已新增开发者烟雾测试入口：`geometry-smoke-test`

## 本次实际落地范围

### 1. 几何创建

- 新增 primitive 与构造模式枚举：
  - `GeometryPrimitiveKind`
  - `ArcConstructionMode`
  - `SurfaceConstructionMode`
- 新增创建规格与属性规格：
  - `GeometryCreationSpec`
  - `GeometryObjectAttributesSpec`
- 新增创建 request / response：
  - `CreatePointsRequest` / `CreateLinesRequest` / `CreateArcsRequest` / `CreateSurfacesRequest`
  - `PointItemRequest` / `LineItemRequest` / `ArcItemRequest` / `SurfaceItemRequest`
  - `GeometryCreationCommonOptions`
  - `GeometryCreationResponse` / `GeometryCreatedObjectResponse`
- 新增创建链路实现：
  - `GeometryCreationSkill`
  - `RhinoGeometryCreationService`
  - `IGeometryBuilder`
  - `RhinoGeometryBuilder`

### 2. 几何修改

- 新增修改枚举与规格：
  - `GeometryTransformKind`
  - `GeometryTransformSpec`
  - `GeometryReplacementSpec`
  - `ControlPointEditSpec`
  - `ControlPointTargetMode`
- 新增修改 request / response：
  - `TransformObjectsRequest` / `DeleteObjectsRequest`
  - `ReplaceGeometryRequest` / `EditControlPointsRequest`
  - `PreviewTransformObjectsRequest` / `PreviewDeleteObjectsRequest`
  - `PreviewReplaceGeometryRequest` / `PreviewEditControlPointsRequest`
  - `GeometryReplacementEntryRequest`
  - `ControlPointEditEntryRequest`
  - `GeometryModificationResponse`
  - `GeometryModificationPreviewResponse`
- 新增修改链路实现：
  - `GeometryModificationSkill`
  - `RhinoGeometryModificationService`
  - `IGeometryMutator`
  - `IGeometryValidator`
  - `RhinoGeometryMutator`
  - `RhinoGeometryValidator`

### 3. MCP 接入

- 新增 `Tools/Geometry/` 下 12 个 MCP Tool
- `DependencyInjection.cs` 已注册 geometry 相关 interface / service
- `AgentRegistration.cs` 已注册 `GeometryCreationSkill` / `GeometryModificationSkill`
- `DeveloperCommandHandler` 已加入 `geometry-smoke-test` 入口，便于重复执行开发者级验证

## 施工中发现并修复的问题

### 四角曲面初版失败

首次在 `MCP_rhino_test.3dm` 上执行 smoke test 时，`CreateSurfaces` 的四角面构造失败，错误为：

```text
CreateSurfaces failed: 错误：四角曲面构造失败，可能角点退化。
```

定位结果：

- 问题不在测试输入，矩形四角点是合法的
- 问题在 bilinear `NurbsSurface.Create(3, false, 2, 2, 2, 2)` 构造后，初版实现只写入控制点，没有显式补齐 `KnotsU` / `KnotsV` 与 domain
- 导致 validator 与 builder 对一个正常的四角面都可能得到 `IsValid == false`

修复方式：

- 在 `RhinoGeometryBuilder` 的四角面构造中补齐：
  - `KnotsU[0..1]`
  - `KnotsV[0..1]`
  - `SetDomain(0, [0,1])`
  - `SetDomain(1, [0,1])`
- 在 `RhinoGeometryValidator` 中同步使用同样的 bilinear NurbsSurface 校验逻辑

修复后，整轮 smoke test 通过。

## 测试记录

### 构建验证

执行命令：

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --no-restore
```

结果：

- `Build succeeded`

### MCP/Tool 链路烟雾测试

执行命令：

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --no-build -- geometry-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

测试方式：

- 不直接改写原始 `Runtime_Test/MCP_rhino_test.3dm`
- 先复制到 `_validation/geometry-smoke-test/MCP_METtest.geometry-smoke.3dm`
- 再通过 Tool -> Skill -> Service -> Infrastructure 的真实链路执行写入与回读断言

本轮测试使用的模型信息：

- 源文件：`Runtime_Test/MCP_rhino_test.3dm`
- 工作副本：`_validation/geometry-smoke-test/MCP_METtest.geometry-smoke.3dm`
- 命中的有效图层：`01_MET-IEF-Curtain Wall`
- 初始对象数：`1014`
- 最终对象数：`1017`

成功断言的检查点：

- `CreatePoints` 成功
- `CreateLines` 成功
- `CreateArcs` 成功
- `CreateSurfaces` 成功
- `PreviewTransformObjects` 成功，且 preview 不落盘
- `TransformObjects` 成功，且显式 `ObjectId` 优先逻辑可用
- `PreviewReplaceGeometry` 成功，且 preview 不落盘
- `ReplaceGeometry` 成功，且对象 `ObjectId` / user text 保留
- `PreviewEditControlPoints` 成功，且 preview 不落盘
- `EditControlPoints` 成功，且 NurbsSurface 控制点被正确改写
- `PreviewDeleteObjects` 成功，且 preview 不落盘
- `DeleteObjects` 成功，且 user attribute 筛查路径可用

## 当前遗留项

- 还没有补正式的单元测试 / 集成测试项目；目前已有的是开发者 smoke test 命令
- `_validation/geometry-smoke-test/` 下保留了本次测试工作副本与归档文件，便于复查
- `.claude/settings.local.json` 为现有本地改动，未触碰

## 结论

本次计划已经完成代码落地，并已在 `MCP_rhino_test.3dm` 的工作副本上完成一轮真实 MCP Tool 链路烟雾测试。测试期间发现并修复了“四角曲面 bilinear NurbsSurface 未初始化 knot/domain 导致无效”的实现问题；修复后复测通过，当前功能处于可继续进入后续回归与补测试阶段的状态。

## 附加功能检查与修补（2026-04-20，后续一轮）

### 本轮发现的问题

基于执行总结做独立功能复检，共定位 6 项偏差（按本轮复检记录编号）：

1. Smoke test 失败路径吞异常且进程仍以退出码 0 返回；Console 只输出 `ex.Message`，无栈信息，CI 无法通过退出码感知失败。
2. `.gitignore` 只覆盖 `_validation/file-safeguard-build/`，未覆盖 `_validation/geometry-smoke-test/`，7MB+ 工作副本有被 `git add .` 误提交的风险。
3. Smoke test 的 Transform 路径只覆盖 `Translate`，`Rotate` / `UniformScale` 两种 kind 从未被跑过。
4. Smoke test 只覆盖 `ArcConstructionMode.ThreePoint` 与 `SurfaceConstructionMode.FourCorners`；`CenterRadius` / `Plane` 构造模式从未被跑过。
5. 计划校验策略列出的硬错误（类型不兼容 Replace、LineCurve 上执行 EditControlPoints、控制点越界、LayerFullPath 不存在、NaN 坐标、零长度线、缩放因子=0、`ConfirmedObjectIds` 无法解析）全部无测试覆盖，smoke test 只证明 happy path。
6. `TransformObjects` Apply 路径在「`ConfirmedObjectIds` + 筛查条件」混用时是否仍 emit warning 从未被断言（只有 Preview 路径有该断言）。

第 7 项「大量落地文件尚未提交」按用户要求本轮不处理。

### 本轮修补

- **`.gitignore`**：追加 `_validation/geometry-smoke-test/` 一行；修补后 `git status` 验证 `_validation/geometry-smoke-test/` 不再出现在未追踪列表；当前仓库中用于验证产物的 `_validation/file-safeguard-build/` 也已单独被忽略。
- **`DeveloperCommandHandler.GeometrySmokeTest.cs`**：
  - `catch` 改写栈信息到 `Console.Error` 并 `System.Environment.ExitCode = 1`；
  - 参数缺失、源文件不存在两条早退路径也统一走 `Console.Error` + `ExitCode = 1`，保证「未成功完成测试 → 非零退出」语义一致；
  - 使用 `System.Environment` 全限定避免与 `Rhino.DocObjects.Environment` 冲突。
- **Apply 路径 warning 断言**：在既有 `TransformObjects` Apply 调用后加一条 `Require(..Warnings.Count > 0, ..)`，堵住 #6。
- **扩展 happy path 覆盖**：
  - `Rotate` 90° 绕 Z 轴对已有 pointId 执行，断言点位从 `(6,2,3)` 变为 `(-2,6,3)`；
  - `UniformScale` factor=2 绕原点执行，断言点位变为 `(-4,12,6)`；
  - `ArcConstructionMode.CenterRadius` 新建一段圆弧后立刻删除，保持净对象数稳定；
  - `SurfaceConstructionMode.Plane` 新建一张平面曲面后立刻删除，同上。
- **扩展硬错误覆盖**（新增 `RequireFailure<T>` 辅助方法，断言 `response.Success == false`）：
  - `ReplaceGeometry` 用 Surface spec 替换 Point 对象 → 拒绝；
  - `EditControlPoints` 目标为 `LineCurve` 时（`TargetMode = CurveIndex`）→ 拒绝；
  - `EditControlPoints` `UIndex = 999` 越界 → 拒绝；
  - `CreatePoints` `LayerFullPath = "NoSuch::NoLayer"` → 拒绝；
  - `CreatePoints` `X = double.NaN` → 拒绝；
  - `CreateLines` `Start == End` 零长度 → 拒绝；
  - `TransformObjects` `UniformScale` `ScaleFactor = 0` → 拒绝；
  - `TransformObjects` `ConfirmedObjectIds = [Guid.NewGuid()]`（无法解析）→ 拒绝。

### 本轮测试结果

构建：

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo
```

首次失败于 `CS0104 'Environment' is an ambiguous reference`（Rhino.DocObjects 命名空间污染），使用 `System.Environment` 全限定后 `Build succeeded, 0 Warning(s), 0 Error(s)`。

扩展 smoke test：

```powershell
dotnet run --project src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --no-build -- geometry-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

输出包含 24 个 checkpoint（原 12 个 + 扩展 12 个），全部通过；进程退出码 `0`。扩展 checkpoint 列表：

- `TransformObjects (Rotate) ok`
- `TransformObjects (UniformScale) ok`
- `CreateArcs (CenterRadius) ok: <guid>`
- `CreateSurfaces (Plane) ok: <guid>`
- `ReplaceGeometry cross-primitive rejected`
- `EditControlPoints on LineCurve rejected`
- `EditControlPoints out-of-bounds rejected`
- `CreatePoints missing layer rejected`
- `CreatePoints NaN rejected`
- `CreateLines zero-length rejected`
- `TransformObjects zero-scale rejected`
- `TransformObjects unresolvable ObjectId rejected`

退出码回归验证：

```powershell
dotnet run ... -- geometry-smoke-test nonexistent-file.3dm
# → "Smoke test source file was not found: ..." → 退出码 1
```

文件结束状态：

- 源文件：`Runtime_Test/MCP_rhino_test.3dm`（未改动）
- 工作副本：`_validation/geometry-smoke-test/MCP_METtest.geometry-smoke.3dm`（Initial=1014 / Final=1017，净增 3 个对象，与原 smoke test 一致；扩展的 CenterRadius 弧与 Plane 曲面在测试中原地 create+delete，不影响净计数）

### 本轮结论

原 12 个 checkpoint 保持通过；新增 12 个 checkpoint（4 个 happy path 覆盖 + 8 个 hard error 覆盖）全部通过；退出码语义修复后 3 条失败路径（断言异常 / 文件缺失 / 参数缺失）均正确返回非零；`.gitignore` 已阻止当前两类验证产物目录被误提交。本轮修补未触碰计划执行主逻辑，仅在测试与 CLI 行为层面补齐遗漏，可直接进入下一阶段回归或真实 MCP Client 联调。

