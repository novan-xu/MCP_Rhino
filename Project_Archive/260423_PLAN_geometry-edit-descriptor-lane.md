# 260423_PLAN_geometry-edit-descriptor-lane

## 背景

`260420_PLAN_geometry-create-modify-tools.md` 已经覆盖了基础几何创建与基础修改（Translate /
Rotate / UniformScale / Replace / Delete / EditControlPoints）。既有 Tool 沿用"命令式映射"思路：
用户讲一个动词，系统调用对应 RhinoCommon API 写回。

但对大量几何修改任务，LLM 更自然的规划路径是"先理解几何的可编辑结构，再推导修改后的点位，再
重建"。先前的 `260422_PLAN_geometry-advanced-edit-tools.md` 试图一次性落地这条 descriptor-first
修改能力线以及 Fillet / Offset / Trim / Split / Morph 等进阶建模命令的框架，规模过大，风险集中，
已被本轮三份 Wave 拆分替代。

本期是三份拆分中的 **Wave 1**：只建立 descriptor-first reconstruction 的最小骨架，服务于曲线，
且只支持"显式点位编辑"（DirectOverride）。目的是**用最小落地面，快速验证"descriptor-first"这条
工作流是否真的适合 LLM 规划**，而不是一次性把"显式点编辑 + 派生点操作 + 曲面重建"全部堆进来。

Wave 2 引入派生点操作与 Strategy 路由；Wave 3 扩到曲面。三份计划按依赖顺序串联，任一 Wave 的
EXET 失败不会让后续 Wave 强行推进。

## 目标

- 建立 descriptor-first 的单一执行模型：
  1. 读取目标对象的可编辑 descriptor（类型、控制点/锚点、degree、closure、属性摘要）。
  2. 基于 descriptor 直接给新坐标，Preview 返回重建摘要与 warning。
  3. Apply 单次 Tool 调用 = 单次 Undo record 完成重建 + 属性回放。
- 首期对象矩阵严格收敛到曲线：
  - `LineCurve`
  - `PolylineCurve`
  - `NurbsCurve`（open / closed，任意 degree）
- 首期编辑语义严格收敛到 `DirectOverride`：
  - 调用方给完整新控制点（或端点）列表，系统负责用新点重建曲线。
  - 不支持派生点操作（ScaleAboutCentroid / TranslateByVector / OffsetAlongNormal，留给 Wave 2）。
  - 不支持部分点编辑（`PointSelectors` 留给 Wave 2）。
- 执行策略本期**固定**：
  - `GeometryEditStrategyKind = ReconstructFromControlPoints`。
  - 本期**不引入** `IGeometryEditStrategyResolver` 的真实路由逻辑，只提供占位实现把所有请求报
    回 `ReconstructFromControlPoints`。
  - 既有 `TransformObjects` 作为 exact transform lane 继续独立存在，不在本期接入选路。
- 响应必须：
  - 显式回传 descriptor 摘要与最终采用的策略。
  - 显式回传几何保真 warning（允许出现 `PARAMETERIZATION_CHANGED` / `UNSUPPORTED_GEOMETRY_FALLBACK`
    等明确 code，而非沉默修改）。
  - Preview 全链路只读，不开 Undo record、不写对象。
  - Apply 保留图层、颜色、名称、可安全复制的 object attributes、object user strings。

**本期明确不纳入：**

- 派生点操作（DerivedPointOperation）与 `PointSelectors`
- Strategy 路由的真实逻辑（ExactTransform ↔ Reconstruction 切换）
- Surface / Brep 单面 / polysurface / mesh / subd
- Fillet / Chamfer / Blend / Offset / Trim / Split / Join / Loft / Sweep / Morph
- Skills / Agents 层新抽象

## 架构归属

- **Tools/Geometry/Edit/**：首次建立 `Edit` 子目录，承载 descriptor-first 系列。
  - `GetEditableGeometryDescriptorTool`
  - `PreviewEditCurveGeometryTool`
  - `ApplyEditCurveGeometryTool`

- **Application/Services/Edit/**：本期新增 2 个 Service。
  - `RhinoEditableGeometryDescriptorService`：负责读取 live 几何的 descriptor。
  - `RhinoGeometryReconstructionService`：负责 preview / apply 曲线重建主流程。
  - 说明：Strategy resolver / metadata operator 作为 Service 依赖以接口形态注入，具体实现见
    Infrastructure；本期不单独抽 `RhinoGeometryEditStrategyService`（留给 Wave 2）。

- **Application/Interfaces/**：新增接口。
  - `ILiveEditableGeometryReader`
  - `ILiveGeometryReconstructor`
  - `ILiveGeometryMetadataOperator`
  - `IGeometryEditStrategyResolver`（本期仅占位，实现始终返回 `ReconstructFromControlPoints`）
  - `IGeometryEditValidator`

- **Infrastructure/Rhino/Live/**：新增 Live 实现。
  - `LiveRhinoEditableGeometryReader.cs`
  - `LiveRhinoGeometryReconstructor.cs`
  - `LiveRhinoGeometryMetadataOperator.cs`
  - `GeometryEditStrategyResolver.cs`（占位实现）
  - `GeometryEditValidator.cs`
  - 所有 RhinoCommon 细节集中在这一层：读取控制点 / degree / knots / closure、读取并复制
    `RhinoObject.Attributes` 与 user strings、构建新的 `Curve`、调用 `doc.Objects.Replace(...)`。

- **Domain/Enums/**：新增（本期用到的那一部分）。
  - `EditableGeometryKind`：本期值域仅 `Curve`；`Surface` 保留枚举位留给 Wave 3。
  - `EditablePointRole`：`ControlPoint` / `Anchor`；`DerivedPoint` 保留枚举位留给 Wave 2。
  - `GeometryEditStrategyKind`：`ReconstructFromControlPoints` / `ReconstructFromPoints`；
    `ExactTransform` 保留枚举位留给 Wave 2。
  - `GeometryReconstructionSupportKind`：`Supported` / `PreviewOnly` / `Unsupported`。

- **Domain/Models/**：新增（本期用到的那一部分）。
  - `EditableGeometryDescriptor`
  - `EditablePointDescriptor`
  - `GeometryMetadataSnapshot`
  - `CurveEditSpec`（本期只承载 `DirectOverride` 语义）
  - `ReconstructedGeometrySummary`
  - 要求：全部是纯数据模型，不持有 RhinoCommon 类型。

- **Contracts/Requests/**：新增。
  - `GetEditableGeometryDescriptorRequest`
  - `PreviewEditCurveGeometryRequest`
  - `ApplyEditCurveGeometryRequest`
  - Curve Request 统一包含：`FilePath`、`ObjectId`、`EditSpec`；`ExpectedStrategy` 字段本期保留
    但仅接受 `ReconstructFromControlPoints`，传入其他值 → 硬错误。

- **Contracts/Responses/**：新增。
  - `EditableGeometryDescriptorResponse`
  - `GeometryEditPreviewResponse`
  - `GeometryEditApplyResponse`

- **Server/**：
  - `DependencyInjection.cs`：注册本期新接口与 Service。
  - `ToolRegistration.cs`：依赖既有 `WithToolsFromAssembly` 反射发现，不需手改。

- **Infrastructure/Plugin/**：
  - `McpGeometryEditDescriptorSmokeCommand.cs`：Rhino 端命令 `_McpGeometryEditDescriptorSmoke`，
    内部调用本期专属 smoke slug。

- **Skills / Agents**：本期不新增。

## 关键设计

1. **骨架验证优先，不把三类未知数堆在一期里**
   - 本期只验证"descriptor-first + 曲线 + 显式点位重建"是否可用。
   - Strategy 路由、局部点作用、曲面重建这三个大块都留给后续 Wave。
   - Strategy resolver 以占位实现落库，保证接口形态在 Wave 2 不变；Wave 2 只替换实现逻辑而
     不破坏外部契约。

2. **Descriptor-first workflow**
   - 所有 reconstruction 型编辑默认推荐两步：
     1. `GetEditableGeometryDescriptor`
     2. `PreviewEditCurveGeometry` / `ApplyEditCurveGeometry`
   - Descriptor 至少包含：
     - `EditableGeometryKind`（本期 = `Curve`）
     - 具体曲线子类型名（`LineCurve` / `PolylineCurve` / `NurbsCurve`）
     - degree（NurbsCurve）、isClosed、isPeriodic
     - 控制点列表（含点角色、索引、世界坐标）
     - 属性摘要（LayerIndex / Color / Name / user string keys 数量）
     - `GeometryReconstructionSupportKind`
     - 潜在保真 warning hint（例如 `PARAMETERIZATION_MAY_CHANGE`）
   - Descriptor 本期统一返回"完整点列"，不提供 summary 模式；若响应体积成为瓶颈，留给 Wave 3
     统一引入 summary / full 双模式（因为曲面才是 token 大头）。

3. **DirectOverride 语义必须明确契约**
   - 调用方传入新点列表时必须给**完整**控制点序列，点数必须与 descriptor 中点数一致。
   - 点数不一致 → 硬错误 `EDIT_POINT_COUNT_MISMATCH`。
   - 点坐标 NaN / Inf → 硬错误 `EDIT_POINT_INVALID`。
   - 对 `NurbsCurve` 不允许调用方改 degree / knots / closure；上述字段只读。
   - 对 `PolylineCurve`，调用方给多少点就生成多少段，点首尾相同视为闭合（保留 descriptor 的
     `isClosed` 提示；若 descriptor 报 closed 但新点首尾不同 → warning `POLYLINE_CLOSURE_CHANGED`）。

4. **Metadata snapshot / replay 是一等公民**
   - 重建流程固定 4 步：
     1. `ILiveGeometryMetadataOperator.Snapshot(rhinoObject)` 返回 `GeometryMetadataSnapshot`。
     2. `ILiveGeometryReconstructor.Build(editSpec, descriptor)` 返回新的 `Curve`。
     3. `doc.Objects.Replace(objectId, newCurve)` 执行替换。
     4. `ILiveGeometryMetadataOperator.Replay(objectId, snapshot)` 回放属性。
   - 白名单复制字段（本期确定范围，不盲目整对象克隆）：
     - `LayerIndex`
     - `ObjectColor` / `ColorSource`
     - `PlotColor` / `PlotColorSource`
     - `PlotWeight` / `PlotWeightSource`
     - `LinetypeIndex` / `LinetypeSource`
     - `Name`
     - `Visible`
     - 所有 user strings（通过 `RhinoObject.Attributes.GetUserStrings()` 读、逐条 `SetUserString` 写）
   - 明确**不复制**（本期）：
     - `RenderMaterial` / `MaterialSource`
     - `GroupList`
     - `DisplayMode`
     - `Space`
     - 任何 block instance 相关字段
   - 上述白名单差异若造成属性丢失，Preview 与 Apply 均追加 warning `METADATA_FIELDS_DROPPED` +
     具体字段列表，避免静默丢字段。

5. **Preview 只读，Apply 单 Undo**
   - Preview 通过 `ILiveRhinoDocumentAccessor.Execute(...)` 在主线程读取当前真值，内存重建几何，
     返回摘要；不落盘、不写对象、不开 Undo record。
   - Apply 通过 `ILiveRhinoDocumentAccessor.ExecuteWithUndo(...)`，单次 Tool 调用对应单次
     Undo record（包含 `doc.Objects.Replace` + metadata replay，用户 Ctrl+Z 一次整条复原）。
   - Apply 成功后调用 `doc.Views.Redraw()`。

6. **Validator 负责两类硬校验**
   - 输入层：`IGeometryEditValidator.ValidateRequest(...)` 做点数、坐标有效性、ObjectId 非空、
     `ExpectedStrategy` 合法性等。
   - 输出层：`IGeometryEditValidator.ValidateReconstruction(...)` 对新 `Curve` 调用 RhinoCommon
     `IsValid` / `IsValidWithLog`，invalid 时 Preview 报 `RECONSTRUCTION_INVALID`，Apply 拒绝落盘。

7. **明确区分"点保真"与"几何语义保真"**
   - 本 lane 保证"新点位就是 descriptor 回馈的点位"，但不保证原 `NurbsCurve` 的参数化、内部
     knots、或者控制点权重在重建后完全相同。
   - 因此响应允许出现：
     - `PARAMETERIZATION_CHANGED`
     - `UNSUPPORTED_GEOMETRY_FALLBACK`（descriptor 判为 `PreviewOnly` 但仍被 Apply）
     - `RECONSTRUCTION_SEMANTICS_CHANGED`
   - 比起假装"和原 geometry 完全等价"，这更诚实，也更适合下游 LLM 决策。

8. **Live Smoke CLI slug 独占**
   - slug：`geometry-edit-descriptor-smoke-test`（全仓唯一）。
   - 注册入口：`Project_Test/260423_TEST_geometry-edit-descriptor-lane/DeveloperCommandHandler.GeometryEditDescriptorSmokeTest.cs`。
   - Rhino 命令：`_McpGeometryEditDescriptorSmoke`，由本期新增的
     `McpGeometryEditDescriptorSmokeCommand` 注册，内部直接调用上述 slug，不复用 `_McpDevSmoke`。

9. **与既有基础修改能力的关系**
   - 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 不删除、
     不重命名、不回退。
   - 本期新增能力与既有 Tools 并行暴露；是否使用 descriptor lane 由 MCP Client 侧 LLM 决策。
   - 若后续 Wave 2 引入 Strategy 路由并决定由 Apply Tool 内部代理 exact transform，再在 Wave 2
     显式修订本份 Plan。

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_geometry-edit-descriptor-lane.md`
- `Project_Test/260423_TEST_geometry-edit-descriptor-lane/`
- `Project_Test/260423_TEST_geometry-edit-descriptor-lane/DeveloperCommandHandler.GeometryEditDescriptorSmokeTest.cs`
- `Project_Test/260423_TEST_geometry-edit-descriptor-lane/curve/`（曲线 fixture 与 expected 数据）

**新增（Tools）**

- `src/MCP_Rhino.Server/Tools/Geometry/Edit/GetEditableGeometryDescriptorTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/PreviewEditCurveGeometryTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/ApplyEditCurveGeometryTool.cs`

**新增（Application）**

- `src/MCP_Rhino.Server/Application/Services/Edit/RhinoEditableGeometryDescriptorService.cs`
- `src/MCP_Rhino.Server/Application/Services/Edit/RhinoGeometryReconstructionService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveEditableGeometryReader.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeometryReconstructor.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeometryMetadataOperator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryEditStrategyResolver.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryEditValidator.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoEditableGeometryReader.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryReconstructor.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryMetadataOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/GeometryEditStrategyResolver.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/GeometryEditValidator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryEditDescriptorSmokeCommand.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/EditableGeometryKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/EditablePointRole.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryEditStrategyKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryReconstructionSupportKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditableGeometryDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditablePointDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryMetadataSnapshot.cs`
- `src/MCP_Rhino.Server/Domain/Models/CurveEditSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReconstructedGeometrySummary.cs`

**新增（Contracts）**

- `src/MCP_Rhino.Server/Contracts/Requests/GetEditableGeometryDescriptorRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewEditCurveGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyEditCurveGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/EditableGeometryDescriptorResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditPreviewResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditApplyResponse.cs`

**修改**

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`

**复用（不改）**

- `ILiveRhinoDocumentAccessor`
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` Tool

**删除**

- `Project_Plan/260422_PLAN_geometry-advanced-edit-tools.md`（被本 Wave 三份计划替代，原 Plan
  未进入 Execute；git 历史保留）

## 使用方式

推荐流程（本期）：

1. `GetEditableGeometryDescriptor(filePath, objectId)`
2. 客户端 / LLM 根据 descriptor 计算新的控制点列表。
3. `PreviewEditCurveGeometry(filePath, objectId, editSpec={Operation:{Kind:"DirectOverride"}, Points:[...]})`
4. 确认 preview 无硬错误后执行 `ApplyEditCurveGeometry(...)`。

示例 A — 读取 NurbsCurve 可编辑结构：

```
GetEditableGeometryDescriptor(filePath, objectId)
→ {Kind:"Curve", CurveKind:"NurbsCurve", Degree:3, IsClosed:false,
   Points:[{Index:0, Role:"ControlPoint", X:..., Y:..., Z:...}, ...],
   MetadataSummary:{LayerIndex:2, Name:"beam-01", UserStringCount:3},
   SupportKind:"Supported"}
```

示例 B — 直接修改 NurbsCurve 控制点：

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={Operation:{Kind:"DirectOverride"}, Points:[...新点列...]})
→ {Strategy:"ReconstructFromControlPoints",
   DescriptorSummary:{...}, ReconstructedSummary:{...},
   Warnings:["PARAMETERIZATION_CHANGED"]}

ApplyEditCurveGeometry(filePath, objectId,
  editSpec={Operation:{Kind:"DirectOverride"}, Points:[...新点列...]})
→ {Strategy:"ReconstructFromControlPoints",
   MetadataDropped:[], UndoRecordName:"MCP:EditCurveGeometry"}
```

示例 C — 修改 PolylineCurve 节点（闭合性变化）：

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={Operation:{Kind:"DirectOverride"}, Points:[p0, p1, p2]})
→ Warnings:["POLYLINE_CLOSURE_CHANGED"] （descriptor 原为 closed，新点首尾不同）
```

## 验收标准

构建 / smoke：

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过。
- `dotnet run --project src/MCP_Rhino.Server -- geometry-edit-descriptor-smoke-test Runtime_Test/MCP_rhino_test.3dm`
  在非 Rhino Plugin 环境下，所有 live-only Tool 返回明确的 `LIVE_RHINO_REQUIRED`，不假装
  提供 CLI fallback。
- 在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm` 后，运行 `_McpGeometryEditDescriptorSmoke`：
  - 3 个 Tool 全部 `Success=true`。
  - Preview 阶段 `doc.Objects.Count` / `doc.Strings.Count` 前后无变化。
  - Preview 阶段 Rhino Undo History 无新条目。
  - Apply 阶段 Rhino Undo History **恰好**新增 1 条 `MCP:EditCurveGeometry`。
  - 用户在 Rhino 中 Ctrl+Z 一次即可同时撤销几何替换与 metadata replay（几何、图层、颜色、
    名称、user strings 全部回到 Apply 前状态）。
  - 对一条 `NurbsCurve`、一条 `PolylineCurve`、一条 `LineCurve` 各至少跑一轮 Apply，图层、
    颜色、名称、user strings 在 Apply 后与 snapshot 完全一致。

LLM 实战 dry-run（本 Wave 最重要的验收项）：

- 在 MCP Client 侧以自然语言发起两类任务并记录效果：
  - "把这条曲线的这个控制点往 X+50 的位置移"
  - "读一下这条线的可编辑结构，然后把它重新画直"
- 记录：
  - descriptor 单次返回的 token 近似体量。
  - LLM 是否能在 2 轮以内完成"读 descriptor → 构造新点 → Preview → Apply"闭环。
  - LLM 是否把本应走 `TransformObjects` 的请求误导到 descriptor lane。
- 若 dry-run 暴露 descriptor token 过大或 LLM 误路径率显著，EXET 需在"与计划的偏差"章节
  明确说明，并作为 Wave 2 立项前置条件。

边界用例：

- `ObjectId` 不存在 → 硬错误 `OBJECT_NOT_FOUND`。
- 目标对象不是支持的曲线类型（例如 Brep / Mesh） → 硬错误 `EDITABLE_KIND_UNSUPPORTED`。
- `EditSpec.Operation.Kind` 非 `DirectOverride` → 硬错误 `EDIT_OPERATION_UNSUPPORTED_IN_WAVE1`。
- `Points` 长度与 descriptor 中点数不一致 → 硬错误 `EDIT_POINT_COUNT_MISMATCH`。
- `Points` 中出现 NaN / Inf → 硬错误 `EDIT_POINT_INVALID`。
- 新 Curve `IsValid=false` → Preview 返回 `RECONSTRUCTION_INVALID`，Apply 拒绝落盘。
- PolylineCurve descriptor 报 closed 但新点首尾不同 → Preview 带 warning，Apply 仍放行。
- metadata 回放某字段失败 → Apply 整条失败，doc 回滚到 Replace 之前（借助 Undo record 边界）。

## 风险与回退方案

风险：

- **重建不等于原几何语义不变**：即便点位正确，也可能改变参数化、内部 knots、权重。
- **Metadata 白名单仍有盲点**：列表以外的字段被静默丢弃的风险由 `METADATA_FIELDS_DROPPED`
  warning 暴露，但白名单本身是否完备只能靠 Rhino 实战验证。
- **Descriptor token 体量**：高阶 NurbsCurve（>200 控制点）的 descriptor 可能撑大单条 MCP
  message；本期不引入 summary 模式以控范围，极端情况仅以 warning 暴露。
- **LLM 把 exact transform 任务也走到 reconstruction**：本期没有 Strategy 路由，误用只会降低
  精度不会破坏数据，但会影响"骨架是否好用"的验收判断。

缓解：

- 响应强制回传 `Strategy` 与 warning 列表；EXET 阶段必须抓 warning 日志。
- 白名单失配的字段以 warning 具名返回，不做"隐式丢字段"。
- LLM 实战 dry-run 纳入验收标准，尽量早暴露误用模式。

回退方案：

- 若 descriptor lane 首轮验证不理想：
  - 可单独下线 Tools 层 3 个新 Tool 的 `[McpServerTool]` 标注，保留 Service 代码以便 Wave 2
    复用；不影响既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` 使用。
  - 极端情况下整体 `git revert` 本期提交；本期不改既有 Tool 接口与既有 Service，回退面可控。
- 若仅 metadata replay 有系统性缺陷：
  - 可临时将 Apply Tool 标为内部隐藏、保留 Preview；不影响 descriptor 读取能力。

## 后续扩展方向

- Wave 2 `260423_PLAN_geometry-edit-derived-operations`：
  - 引入 `DerivedPointOperationKind`（ScaleAboutCentroid / TranslateByVector / OffsetAlongNormal）。
  - 引入 `PointSelectors` 支持局部点作用。
  - 激活 `IGeometryEditStrategyResolver` 真实路由：在 `ExactTransform` 与
    `ReconstructFromControlPoints` 间选路。
  - 明确"ExactTransform lane 在本 Tool 内部如何表达"（内部代理还是返回 hint）。
- Wave 3 `260423_PLAN_geometry-edit-surface-lane`：
  - 新增 `PreviewEditSurfaceGeometryTool` / `ApplyEditSurfaceGeometryTool`。
  - 支持 `PlaneSurface` / `NurbsSurface` / 可安全提取的单面。
  - 引入 descriptor summary / full 双模式，针对曲面控制点网格的 token 成本优化。
- Wave 3 之后若出现固定"读 descriptor → 推点 → 预览 → 应用"流程，再抽 `GeometryReconstructionSkill`。
