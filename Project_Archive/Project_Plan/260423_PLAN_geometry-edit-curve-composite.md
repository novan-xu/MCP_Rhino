# 260423_PLAN_geometry-edit-curve-composite

## 背景

`260423_PLAN_geometry-edit-molecular-architecture`（总纲）将原 Wave 1/2/3 与 Plan 4 重组为 8 个主干分子。本 PLAN 依赖子 PLAN A（`260423_PLAN_geometry-edit-molecular-foundation`）先闭环；A 合入后提供分子 1 / 3 / 6 / 8（描述器、frame 采样、metadata、Brep 降级）。

本 PLAN 是子 PLAN B，承担**曲线**方向的 edit 复合能力，首期只支持 `DirectOverride`（调用方给完整新控制点）。落地内容：

- 分子 5 的**曲线分支**：`IGeometryReconstructor`（含 IsValid 尾校验）
- 分子 7：`IGeometryMutationService`（`doc.Objects.Replace` + metadata replay + 单 Undo 的统一边界）
- 对外暴露 2 个复合 Tool：`PreviewEditCurveGeometry`、`ApplyEditCurveGeometry`

Strategy resolver（分子 4）与派生点操作（分子 2）留给子 PLAN C；本期在 Domain 层定义 `GeometryEditStrategyKind` 枚举，但 Strategy 取值固定为 `ReconstructFromControlPoints`（`PolylineCurve` 路径取 `ReconstructFromPoints`）。

被替代的旧 PLAN 相关部分：`Project_Archive/260423_PLAN_geometry-edit-descriptor-lane.md`（其中 Preview/Apply 曲线 Tool 与 metadata replay 链路）。

## 目标

- 落地分子 5 曲线分支 + 分子 7，并把 `GetEditableGeometryDescriptor`（子 PLAN A）与 metadata snapshot/replay（子 PLAN A）串起来，形成完整曲线 edit 闭环
- 对外暴露：
  - `PreviewEditCurveGeometry`（纯读 + 重建 + IsValid，不写文档）
  - `ApplyEditCurveGeometry`（同上 + Replace + replay + 单 Undo）
- 首期编辑语义：**仅 `DirectOverride`**。调用方必须给**完整**新控制点序列，点数与 descriptor 点数一致
- 对象矩阵：`LineCurve` / `PolylineCurve` / `NurbsCurve`（open / closed，任意 degree）
- `Strategy` 字段必回传，取值为 `ReconstructFromControlPoints` 或 `ReconstructFromPoints`（PolylineCurve 路径）
- Apply 单次 Tool 调用 = 单条 Undo record（`MCP:EditCurveGeometry`），含 Replace + metadata replay
- 保留图层、颜色、名称、白名单 object attributes、object user strings（白名单由子 PLAN A 固定）

**明确不纳入：**

- 派生点操作 / PointSelectors / Strategy 真实路由（留给子 PLAN C）
- 任何曲面能力（留给子 PLAN D）
- Plan 4 专属分子（留给子 PLAN E）
- 修改 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 既有能力

## 架构归属

本 PLAN 实现的分子：**5（曲线分支） / 7**。复用子 PLAN A 的分子 1 / 6 / 8。

- **Tools/Geometry/Edit/**（新增）
  - `PreviewEditCurveGeometryTool.cs`
  - `ApplyEditCurveGeometryTool.cs`

- **Application/Interfaces/**（新增）
  - `IGeometryReconstructor.cs`（分子 5；首期仅曲线方法）
  - `IGeometryMutationService.cs`（分子 7）
  - `IGeometryEditValidator.cs`（请求入参校验；为分子 2/4 预留接口但首期只做 DirectOverride 校验）

- **Infrastructure/Rhino/Live/**（新增）
  - `LiveGeometryReconstructor.cs`（曲线分支；曲面分支由子 PLAN D 扩）
  - `LiveGeometryMutationService.cs`
  - `LiveGeometryEditValidator.cs`

- **Domain/Enums/**（新增）
  - `GeometryEditStrategyKind.cs`：`ReconstructFromControlPoints` / `ReconstructFromPoints`；为子 PLAN C 预留 `ExactTransform` 枚举位
  - `GeometryEditOperationKind.cs`：`DirectOverride`；为子 PLAN C 预留 `DerivedOperation` 枚举位

- **Domain/Models/**（新增）
  - `CurveEditSpec.cs`：`Operation: { Kind: GeometryEditOperationKind }` + `Points: EditablePointInput[]`（DirectOverride 专用字段）
  - `EditablePointInput.cs`：`Index` / `X` / `Y` / `Z`（请求侧输入点）
  - `ReconstructedCurveSummary.cs`：`CurveKind` / `Degree` / `IsClosed` / `IsValid` / `Bbox` / `PointCount`

- **Contracts/Requests/**（新增）
  - `PreviewEditCurveGeometryRequest.cs`：`FilePath` / `ObjectId` / `EditSpec: CurveEditSpec`
  - `ApplyEditCurveGeometryRequest.cs`：同上

- **Contracts/Responses/**（新增）
  - `GeometryEditPreviewResponse.cs`：`Strategy` / `DescriptorSummary` / `ReconstructedCurveSummary` / `Warnings`
  - `GeometryEditApplyResponse.cs`：`Strategy` / `UndoRecordName` / `ObjectId` / `MetadataDropped` / `Warnings`

- **Infrastructure/Plugin/**（新增）
  - `McpGeometryEditCurveCompositeSmokeCommand.cs`：Rhino 端命令 `_McpGeometryEditCurveCompositeSmoke`

- **Server/**
  - `DependencyInjection.cs`（修改）：注册 `IGeometryReconstructor` / `IGeometryMutationService` / `IGeometryEditValidator`

- **Skills / Agents**：本 PLAN 不新增。

## 关键设计

### §1 Tool 内部调用链

**`PreviewEditCurveGeometryTool.Invoke`**：

1. `IGeometryEditValidator.ValidateRequest(request)` 做入参硬校验（见 §3）
2. `IEditableGeometryDescriptorService.Read(filePath, objectId, Full)`（子 PLAN A 分子 1）→ 拿到 descriptor
3. 若 descriptor.Kind != Curve → 硬错误 `EDITABLE_KIND_UNSUPPORTED`
4. `IGeometryEditValidator.ValidatePointCount(spec, descriptor)` → 点数校验
5. `IGeometryReconstructor.ReconstructCurve(filePath, objectId, descriptor, spec.Points)` → 新 `Curve` + IsValid + warning 列表
   - reconstructor 实现内部经 `ILiveRhinoDocumentAccessor` 在主线程读取原始 `Curve` 的 knots / weights / rational / parameter range 等不暴露在 descriptor 的结构信息；descriptor 仅承担身份与摘要校验
   - reconstructor 不写文档、不调用 mutation API
6. 若 IsValid=false → 响应 `Success=true`, `Warnings` 包含 `RECONSTRUCTION_INVALID`，**不拒绝**（Preview 允许展示失败原因）
7. 组装 `GeometryEditPreviewResponse`

**`ApplyEditCurveGeometryTool.Invoke`**：

1. 复用 Preview 的步骤 1–5
2. 若 IsValid=false → `Success=false`, `ErrorCode="RECONSTRUCTION_INVALID"`（Apply 拒绝落盘）
3. `IGeometryMetadataOperator.Snapshot(rhinoObject)` → `GeometryMetadataSnapshot`
4. `IGeometryMutationService.ReplaceWithMetadata(objectId, newCurve, snapshot, undoName="MCP:EditCurveGeometry")`
   - 内部：`doc.BeginUndoRecord(undoName)` → `doc.Objects.Replace(objectId, newCurve)` → `IGeometryMetadataOperator.Replay(objectId, snapshot)` → `doc.EndUndoRecord` → `doc.Views.Redraw()`
   - 返回：`objectId`（沿用输入对象 GUID）+ `MetadataDropped` 字段列表
5. 组装 `GeometryEditApplyResponse`

### §2 Strategy 字段的本期取值

固定逻辑：

- 对 `PolylineCurve` → `Strategy = ReconstructFromPoints`
- 对其他曲线 → `Strategy = ReconstructFromControlPoints`

Strategy 在本期由 `IGeometryReconstructor` 的实现内部选择并回传；不引入 resolver 接口。子 PLAN C 会把这一选择迁移到 `IGeometryEditStrategyResolver`，但对外响应契约不变。

`GeometryEditStrategyKind.ExactTransform` 枚举位已保留但本期不会被返回；若调用方传入 `ExpectedStrategy=ExactTransform`（字段在子 PLAN C 引入），本期以未知字段对待（忽略）——**本期请求模型不包含 `ExpectedStrategy` 字段**，避免把 Wave 2 的设计提前引入。

### §3 DirectOverride 硬校验

`IGeometryEditValidator` 在本期只做以下校验：

- `ObjectId` 存在 → 否则 `OBJECT_NOT_FOUND`
- Descriptor `Kind == Curve` → 否则 `EDITABLE_KIND_UNSUPPORTED`
- `Operation.Kind == DirectOverride` → 否则 `EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE`（为子 PLAN C 的 DerivedOperation 留路径）
- `Points.Count == descriptor.Points.Count` → 否则 `EDIT_POINT_COUNT_MISMATCH`
- `Points[i].Index == i` 对所有点成立，且 Index 覆盖 `0..PointCount-1`、无重复 → 否则 `EDIT_POINT_INDEX_MISMATCH`
- 所有点坐标无 `NaN` / `Inf` → 否则 `EDIT_POINT_INVALID`
- `NurbsCurve` 不允许调用方改 degree / knots / closure：请求模型本身不暴露这些字段，违反前置条件自然不会发生

### §4 重建语义细节

- **`LineCurve`**：`Points.Count == 2`；若调用方给 2 点外的数量 → 前置点数校验已拒绝；重建直接用两端点构造 `LineCurve`
- **`PolylineCurve`**：沿用 descriptor `IsClosed` 提示；若 descriptor 报 closed 但新点首尾不同 → warning `POLYLINE_CLOSURE_CHANGED`（不拒绝）
- **`NurbsCurve`**：保留原 degree / knots / IsClosed / IsPeriodic；reconstructor 接口签名为 `ReconstructCurve(filePath, objectId, descriptor, points)`，实现内部经注入的 `ILiveRhinoDocumentAccessor` 在主线程重新读取原始 `NurbsCurve` 的 knots / weights / rational 等必要结构信息（这些信息不在 descriptor 暴露，避免膨胀 token），再以新控制点重建；可能出现 warning `PARAMETERIZATION_CHANGED`（参数化轻微变化）或 `RECONSTRUCTION_SEMANTICS_CHANGED`
- 所有情况在重建完成后强制调用 `IsValid` / `IsValidWithLog`；结果汇入 `ReconstructedCurveSummary.IsValid`

### §5 Mutation 边界契约

`LiveGeometryMutationService.ReplaceWithMetadata` 必须满足：

- **单条 Undo record**：Replace + Replay 在同一 `BeginUndoRecord` / `EndUndoRecord` 之间
- **原子回退**：用户在 Rhino 中 Ctrl+Z 一次 → 几何、图层、颜色、名称、user strings 全部回到 Apply 前状态
- **沿用原 `ObjectId`**：本 PLAN 沿用仓库当前 `doc.Objects.Replace(...)` 语义；Apply 响应回传输入 `objectId`，不引入 delete+add 式新 GUID 契约
- **失败回滚**：若 Replay 中途抛异常，整条 Undo record 回滚；Apply 响应返回 `APPLY_FAILED_ROLLED_BACK`
- **单线程**：通过 `ILiveRhinoDocumentAccessor.ExecuteWithUndo(...)` 保证主线程调度

### §6 Preview 只读

Preview 路径：

- 通过 `ILiveRhinoDocumentAccessor.Execute(...)` 而不是 `ExecuteWithUndo`
- 不调用 `doc.Objects.Replace` / `ModifyAttributes` / 任何写 API
- 不创建临时对象
- 响应返回 `ReconstructedCurveSummary` 摘要即可，不回传完整点列（保持响应轻量；如需完整点列客户端可先 `GetEditableGeometryDescriptor` Full 再模拟替换）

### §7 错误与 warning 码

硬错误：

- `LIVE_RHINO_REQUIRED`：非 Rhino Plugin 环境
- `OBJECT_NOT_FOUND`
- `EDITABLE_KIND_UNSUPPORTED`
- `EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE`：本期仅 `DirectOverride`
- `EDIT_POINT_COUNT_MISMATCH`
- `EDIT_POINT_INDEX_MISMATCH`
- `EDIT_POINT_INVALID`
- `RECONSTRUCTION_INVALID`（仅 Apply；Preview 降级为 warning）
- `APPLY_FAILED_ROLLED_BACK`

warning：

- `PARAMETERIZATION_CHANGED`
- `UNSUPPORTED_GEOMETRY_FALLBACK`
- `RECONSTRUCTION_SEMANTICS_CHANGED`
- `POLYLINE_CLOSURE_CHANGED`
- `METADATA_FIELDS_DROPPED`（继承自子 PLAN A）

### §8 Live Smoke CLI slug 独占

- slug：`geometry-edit-curve-composite-smoke-test`（全仓唯一）
- 注册入口：`Project_Test/260423_TEST_geometry-edit-curve-composite/DeveloperCommandHandler.GeometryEditCurveCompositeSmokeTest.cs`
- Rhino 命令：`_McpGeometryEditCurveCompositeSmoke`

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_geometry-edit-curve-composite.md`
- `Project_Test/260423_TEST_geometry-edit-curve-composite/`
- `Project_Test/260423_TEST_geometry-edit-curve-composite/DeveloperCommandHandler.GeometryEditCurveCompositeSmokeTest.cs`
- `Project_Test/260423_TEST_geometry-edit-curve-composite/curve/`（fixture + expected）

**新增（Tools）**

- `src/MCP_Rhino.Server/Tools/Geometry/Edit/PreviewEditCurveGeometryTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/ApplyEditCurveGeometryTool.cs`

**新增（Application）**

- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryReconstructor.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryMutationService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryEditValidator.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryReconstructor.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryMutationService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryEditValidator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryEditCurveCompositeSmokeCommand.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/GeometryEditStrategyKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryEditOperationKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/CurveEditSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditablePointInput.cs`
- `src/MCP_Rhino.Server/Domain/Models/ReconstructedCurveSummary.cs`

**新增（Contracts）**

- `src/MCP_Rhino.Server/Contracts/Requests/PreviewEditCurveGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyEditCurveGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditPreviewResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditApplyResponse.cs`

**修改**

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`

**复用（不改）**

- `ILiveRhinoDocumentAccessor`
- `IEditableGeometryDescriptorService`（子 PLAN A）
- `IGeometryMetadataOperator`（子 PLAN A）
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 全部 Tool

## 使用方式

### 推荐流程

1. `GetEditableGeometryDescriptor(filePath, objectId)`（子 PLAN A）
2. 客户端 / LLM 根据 descriptor 计算新控制点列表
3. `PreviewEditCurveGeometry(filePath, objectId, editSpec)`
4. 确认 Preview 无硬错误后 `ApplyEditCurveGeometry(...)`

### 场景 A — 修改 NurbsCurve 控制点

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={ Operation:{ Kind:"DirectOverride" },
             Points:[{Index:0,X:...,Y:...,Z:...}, ...N 个新点] })
→ { Strategy:"ReconstructFromControlPoints",
    DescriptorSummary:{ CurveKind:"NurbsCurve", Degree:3, PointCount:N },
    ReconstructedCurveSummary:{ CurveKind:"NurbsCurve", Degree:3, IsValid:true, Bbox:..., PointCount:N },
    Warnings:["PARAMETERIZATION_CHANGED"] }

ApplyEditCurveGeometry(...)
→ { Strategy:"ReconstructFromControlPoints",
    UndoRecordName:"MCP:EditCurveGeometry",
    ObjectId:"<same-guid>",
    MetadataDropped:[],
    Warnings:[] }
```

### 场景 B — 修改 PolylineCurve 顶点（闭合性变化）

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={ Operation:{ Kind:"DirectOverride" },
             Points:[p0, p1, p2] })
→ Warnings:["POLYLINE_CLOSURE_CHANGED"]
```

### 场景 C — LineCurve 两端点替换

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={ Operation:{ Kind:"DirectOverride" },
             Points:[pStart, pEnd] })
→ { Strategy:"ReconstructFromControlPoints", Warnings:[] }
```

### 场景 D — 点数不匹配

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={ Operation:{ Kind:"DirectOverride" },
             Points:[p0] })   // descriptor 说有 5 个点
→ Success=false, ErrorCode:"EDIT_POINT_COUNT_MISMATCH"
```

## 验收标准

### 构建

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过

### 非 Rhino Plugin 环境

- `dotnet run --project src/MCP_Rhino.Server -- geometry-edit-curve-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm` 返回 `LIVE_RHINO_REQUIRED`

### Rhino Plugin 环境

在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm`，运行 `_McpGeometryEditCurveCompositeSmoke`：

- 对一条 `NurbsCurve`、一条 `PolylineCurve`、一条 `LineCurve` 各跑一轮完整链路（Preview → Apply）：
  - `Success=true`
  - Preview 阶段 `doc.Objects.Count` / `doc.Strings.Count` 前后无变化
  - Preview 阶段 Rhino Undo History 无新条目
  - Apply 阶段恰好新增 1 条 `MCP:EditCurveGeometry`
  - Apply 后对象 Layer / Name / Color / user strings 与 Apply 前一致
  - 用户 Ctrl+Z 一次即可整条回滚（几何 + metadata）
  - Apply 响应回传原 `ObjectId`（与输入一致）
- `PolylineCurve` descriptor 报 closed 但新点首尾不同 → Preview `Success=true` 带 `POLYLINE_CLOSURE_CHANGED` warning，Apply 放行
- 边界用例：
  - `ObjectId` 不存在 → `OBJECT_NOT_FOUND`
  - 目标是 `Brep` / `Mesh` → `EDITABLE_KIND_UNSUPPORTED`
  - `Operation.Kind == "DerivedOperation"` → `EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE`
  - `Points.Count != descriptor.PointCount` → `EDIT_POINT_COUNT_MISMATCH`
  - `Points` 的 `Index` 不连续、重复或与数组顺序不一致 → `EDIT_POINT_INDEX_MISMATCH`
  - `Points` 含 `NaN` / `Inf` → `EDIT_POINT_INVALID`
  - 新 Curve `IsValid=false` → Preview warning `RECONSTRUCTION_INVALID`；Apply `Success=false, ErrorCode="RECONSTRUCTION_INVALID"`

### LLM 实战 dry-run

在 MCP Client 侧以自然语言发起：

- "把这条曲线的这个控制点往 X+50 的位置移"
- "读一下这条线的可编辑结构，然后把它重新画直"

记录：

- descriptor 单次返回的 token 近似体量
- LLM 是否在 2 轮以内完成"读 descriptor → 构造新点 → Preview → Apply"闭环
- LLM 是否把本应走 `TransformObjects` 的请求误导到本 PLAN 的 Tool

若 dry-run 暴露 descriptor token 过大或 LLM 误路径率显著，EXET 的"与计划的偏差"章节必须记录，作为子 PLAN C 立项前置条件。

### 回归

- 子 PLAN A 的 `_McpGeometryEditMolecularFoundationSmoke` 在本 PLAN 合入后继续通过
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 行为无变化

## 风险与回退方案

### 风险

- **重建不等于原几何语义不变**：点位正确但参数化 / knots / 权重可能变化；以 warning 对外暴露
- **Metadata replay 边界情况**：子 PLAN A 白名单无法覆盖所有字段；盲区以 `METADATA_FIELDS_DROPPED` 暴露，仍需实战验证
- **LLM 选路偏差**：曲线上"整体仿射变换"请求可能被 LLM 误导到本 Tool 而非 `TransformObjects`；本期无 resolver，只能以降精度完成
- **Mutation service 与既有 `ReplaceGeometryTool` 路径并存**：首期不重构既有 Tool，两条替换路径并存，EXET 需确认 Undo record 描述与既有 Tool 无冲突

### 缓解

- 响应强制回传 `Strategy` + warning 列表；EXET 阶段必须抓 warning 日志
- 白名单差异的字段以 warning 具名返回，不"隐式丢字段"
- LLM dry-run 纳入验收；若误路径严重，子 PLAN C 的 resolver 设计优先考虑"对 `DirectOverride` 且点布局等同整体仿射的情况提示切换 `TransformObjects`"
- Mutation service 的 Undo record 命名与既有 Tool 区分（本 PLAN 统一用 `MCP:EditCurveGeometry`）

### 回退方案

- 若 Preview/Apply Tool 不稳定：单独下线对应 Tool 的 `[McpServerTool]` 标注，保留 Service 代码供子 PLAN C/D 复用；不影响子 PLAN A 与既有 Tool
- 若 `IGeometryMutationService` 存在系统性缺陷：临时回退到直接在 Apply Tool 内调用 `doc.Objects.Replace` + metadata replay 两步式（不走新 Service 接口），作为 hotfix
- 本 PLAN 为新增文件集（DI 文件仅新增注册），可独立 `git revert`

## 后续扩展方向

- 子 PLAN C：在本期 Tool 之上扩展 `CurveEditSpec` 支持 `DerivedOperation` + `PointSelector`；激活 `IGeometryEditStrategyResolver`；Strategy 响应可取 `ExactTransform`
- 子 PLAN D：曲面 Preview/Apply Tool 复用本期的 `IGeometryMutationService`（Undo record 改名 `MCP:EditSurfaceGeometry`）与 `IGeometryReconstructor`（扩曲面分支）
- 后续评估是否把既有 `ReplaceGeometryTool` 重构为委托本期 `IGeometryMutationService`；非本 PLAN 范围
- 若 `AppliedPointCount / TotalPointCount` 等精细反馈在实战中被 LLM 稳定使用，再扩 `ReconstructedCurveSummary` 字段
