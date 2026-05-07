# 260423_PLAN_geometry-edit-surface-composite

## 背景

`260423_PLAN_geometry-edit-molecular-architecture`（总纲）定义了 8 个主干分子。至此：

- 子 PLAN A 合入后提供分子 1 / 3 / 6 / 8（描述器 + frame 采样 + metadata + Brep 降级），支持曲线 / 曲面 / untrimmed single-face Brep 的描述器读取
- 子 PLAN B 合入后提供分子 5 曲线分支 + 分子 7（`DirectOverride` 曲线编辑 + Mutation 边界）
- 子 PLAN C 合入后提供分子 2 / 4（派生点 + 策略路由），仅覆盖曲线

本 PLAN 是子 PLAN D，把曲线 lane 的能力对称扩到**曲面**：

- 扩展分子 5 的曲面分支（`NurbsSurface` 重建 + IsValid 尾校验）
- 扩展分子 2 的曲面分支（2D grid 派生点求值 + 曲面 Selector）
- 扩展分子 4 的曲面分支（Uniform Scale / Translate on All → ExactTransform；其余 → reconstruction）
- 新增 2 个复合 Tool：`PreviewEditSurfaceGeometry` / `ApplyEditSurfaceGeometry`
- 新增 `SurfacePointSelectorKind` 专属枚举与 `ISurfaceEditOrchestrator` 编排 Service
- 自然复用子 PLAN A 的分子 8（untrimmed single-face Brep 降级）与 descriptor 两档体量

被替代的旧 PLAN：`Project_Archive/260423_PLAN_geometry-edit-surface-lane.md`。核心差异是：旧 PLAN 把曲面 descriptor 扩展、曲面重建、曲面 derived op、曲面 resolver 分支统一放在一份计划；本 PLAN 基于子 PLAN A/B/C 计划提供的分子架构，只承担曲面重建 + 曲面派生 + 曲面策略分支（描述器扩展由子 PLAN A 先闭环）。

## 目标

- 新增 2 个 MCP Tool：`PreviewEditSurfaceGeometry` / `ApplyEditSurfaceGeometry`
- 对象矩阵：
  - `PlaneSurface`
  - `NurbsSurface`（open / closed，任意 degree U/V；含 periodic 方向）
  - untrimmed single-face Brep（经分子 8 降级为 `UnderlyingSurface` 处理，Apply 后通过 `BrepDowngradeResult.Rebuild` 回包为 Brep，保持对象类型稳定）
- 曲面编辑语义：
  - `DirectOverride`：调用方给完整 U×V 控制点网格（`Flat` 或 `Grid` layout）
  - `DerivedOperation`：沿用 `DerivedPointOperationKind` 三种（`ScaleAboutCentroid` / `TranslateByVector` / `OffsetAlongNormal`，曲面语义见 §4）
  - Selector：新增专属 `SurfacePointSelectorKind = {All, Indices, UvRange, EdgeOnly}`；**不复用** Wave 2 曲线 `PointSelectorKind`
- Strategy resolver 曲面分支规则与曲线对齐（见 §3）
- Apply 单次 Tool 调用 = 单条 Undo record（`MCP:EditSurfaceGeometry`）
- 沿用子 PLAN A 的 metadata 白名单 + 分子 6
- 沿用子 PLAN B 的 `IGeometryMutationService`（Undo name 参数化传入）

**明确不纳入：**

- Trimmed `BrepFace` 重建（trim 信息保留策略另立 PLAN）
- Polysurface / SubD / Mesh / Extrusion 重建
- Surface 级别的 `MatchSrf` / `Blend` / `Loft` / `Sweep`
- 曲面上"按几何条件选点"（例如"靠近某平面的控制点"）
- Plan 4 专属分子（留给子 PLAN E）
- 修改既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 公共签名

## 架构归属

本 PLAN 实现的分子扩展：**5（曲面分支）/ 2（曲面分支）/ 4（曲面分支）**。复用子 PLAN A 的 1 / 3 / 6 / 8；复用子 PLAN B 的 7 与既有 `IGeometryMutationService`。

- **Tools/Geometry/Edit/**（新增）
  - `PreviewEditSurfaceGeometryTool.cs`
  - `ApplyEditSurfaceGeometryTool.cs`

- **Application/Interfaces/**（新增 / 修改）
  - 新增：`ISurfaceEditOrchestrator.cs`
  - 修改：`IGeometryReconstructor.cs`（新增曲面方法，不改曲线方法签名）
  - 修改：`IDerivedPointOperationEvaluator.cs`（新增曲面方法）
  - 修改：`IGeometryEditStrategyResolver.cs`（新增曲面分派方法）
  - 修改：`IGeometryTransformExecutionBridge.cs`（新增曲面 ExactTransform 执行方法）
  - 修改：`IGeometryEditValidator.cs`（新增曲面校验方法）

- **Application/Services/Edit/**（新增）
  - `SurfaceEditOrchestrator.cs`（与 `CurveEditOrchestrator` 并列）

- **Infrastructure/Rhino/Live/**（修改）
  - `LiveGeometryReconstructor.cs`（扩曲面分支 + periodic/closed seam 对齐）
  - `LiveDerivedPointOperationEvaluator.cs`（扩曲面 2D grid 分支）
  - `LiveGeometryEditStrategyResolver.cs`（扩曲面分支）
  - `LiveGeometryTransformExecutionBridge.cs`（扩曲面 ExactTransform 执行桥）
  - `LiveGeometryEditValidator.cs`（扩曲面校验）
  - 新增：`Infrastructure/Plugin/McpGeometryEditSurfaceCompositeSmokeCommand.cs`

- **Domain/Enums/**（新增 / 修改）
  - 新增：`SurfacePointSelectorKind.cs`：`All` / `Indices`（row-major 扁平）/ `UvRange` / `EdgeOnly`
  - 新增：`SurfacePointLayoutKind.cs`：`Flat` / `Grid`
  - 修改：`EditableGeometryKind.cs`（`Surface` 取值，本期真正被用到；子 PLAN A 已预留定义）

- **Domain/Models/**（新增）
  - `SurfaceEditSpec.cs`
  - `SurfacePointSelectorSpec.cs`
  - `SurfaceEditDerivedOperationParameters.cs`：与曲线 `DerivedPointOperationParameters` 语义一致的参数（ScaleX/Y/Z / Vector / Distance），但拆为独立模型避免曲线/曲面字段混用
  - `SurfaceControlPointGridSnapshot.cs`：`CountU` / `CountV` / `SampleCorners: Point3d[]`（仅 4 个角点；完整网格由 descriptor Full 获取，不重复下发）
  - `EditableSurfacePointGrid.cs`：请求侧输入载体（`Layout` + `Points` 扁平 / 二维）

- **Contracts/Requests/**（新增）
  - `PreviewEditSurfaceGeometryRequest.cs`：`FilePath` / `ObjectId` / `EditSpec: SurfaceEditSpec` / 可选 `ExpectedStrategy`
  - `ApplyEditSurfaceGeometryRequest.cs`：同上

- **Contracts/Responses/**（修改）
  - `GeometryEditPreviewResponse.cs`：扩可选 `SurfaceControlPointGridSnapshot`（曲面请求时填充）
  - `GeometryEditApplyResponse.cs`：同上

- **Server/**
  - `DependencyInjection.cs`（修改）：注册 `ISurfaceEditOrchestrator`

- **Skills / Agents**：本 PLAN 不新增。

## 关键设计

### §1 `ISurfaceEditOrchestrator` 职责

与 `CurveEditOrchestrator` 对称，区别仅在曲面分支方法。Preview / Apply 流程：

1. `IGeometryEditValidator.ValidateRequest(request)`
2. `IEditableGeometryDescriptorService.Read(filePath, objectId, Summary)` —— **先用 Summary 拿结构与 `ControlPointCentroidWorld`**
3. 若 descriptor.Kind != Surface → `EDITABLE_KIND_UNSUPPORTED`
4. 如果 descriptor 带 `UNDERLYING_SURFACE_FALLBACK` warning，说明对象是 untrimmed single-face Brep，后续 Apply 需通过分子 8 `BrepDowngradeResult.Rebuild` 回包
5. 若 spec.Operation.Kind == DirectOverride，升级到 Full descriptor 做点数对齐校验；若 DerivedOperation 可路由 ExactTransform（`All + TranslateByVector` 或 `All + uniform ScaleAboutCentroid`），Summary 的结构与 `ControlPointCentroidWorld` 已足够，不升级 Full；其余需要 reconstruction 的 DerivedOperation 再升级到 Full 生成新控制点网格
6. `IGeometryEditValidator.ValidateSurfaceSpecAgainstDescriptor(spec, descriptor)`
7. `IGeometryEditStrategyResolver.ResolveSurface(spec, descriptor)` → `{ Strategy, Trace }`
8. 按 Strategy 分派：
   - **`ExactTransform`**：
      - 构造 `GeometryTransformSpec`（`Translate` 或 `UniformScale` with `Origin = descriptor.ControlPointCentroidWorld`）
      - 委托 `IGeometryTransformExecutionBridge`，由其调用子 PLAN C 新增的 `GeometryModificationSkill` internal 协作方法（与曲线 ExactTransform 底座相同）
     - Undo record 名 `MCP:EditSurfaceGeometry`
   - **`ReconstructFromControlPoints`**：
     - DirectOverride：spec 的点网格 → `IGeometryReconstructor.ReconstructSurface(descriptor, grid)`
     - DerivedOperation：`IDerivedPointOperationEvaluator.EvaluateSurface(descriptor, spec)` → 新 U×V 网格 → `IGeometryReconstructor.ReconstructSurface(...)`
     - Preview 仅返回 `SurfaceControlPointGridSnapshot`（4 角点 + CountU/V）
     - Apply 经 `IGeometryMutationService.ReplaceWithMetadata`
     - Brep 降级路径：`LiveGeometryReconstructor` 返回 `Surface`；orchestrator 检测 descriptor 原始对象是 Brep 时，调用分子 8 的 `Rebuild(newSurface)` 包回 Brep 后再交给 mutation

### §2 曲面 DirectOverride 点网格契约

- 调用方必须给与 descriptor 一致的 `(CountU, CountV)` 网格
- `Layout` discriminator：
  - `Flat`：`Points: Point3d[]`，长度 `CountU * CountV`，row-major（`index = u * CountV + v`）
  - `Grid`：`Rows: Point3d[][]`，外层 `CountU`，内层每行 `CountV`
- `Flat` 输入中的 `Point.Index` 必须等于 row-major 位置；`Grid` 输入不接受独立 Index 字段，由数组位置决定索引
- 硬错误：
  - `EDIT_POINT_COUNT_MISMATCH`：点数与 `CountU * CountV` 不一致
  - `EDIT_POINT_LAYOUT_MISMATCH`：`Layout = Grid` 但传扁平 / 反之
  - `EDIT_POINT_INDEX_MISMATCH`：`Flat` 点 Index 不连续、重复或与 row-major 位置不一致
  - `EDIT_POINT_INVALID`：含 `NaN` / `Inf`
- 不允许调用方改 U/V degree / knots / closure / periodicity（请求模型不暴露这些字段）
- **closed / periodic seam 处理**：若 descriptor 报 `IsClosedU / IsClosedV / IsPeriodicU / IsPeriodicV` 而调用方给的"逻辑起始列"与"逻辑结束列"不一致 → warning `SURFACE_CLOSURE_SEAM_INCONSISTENT`；服务端按 descriptor 元信息做 seam 对齐但不强行纠正；Preview 返回服务端最终采用的网格摘要

### §3 曲面 StrategyResolver 规则（穷举）

优先级从高到低：

1. `Operation.Kind == DirectOverride` → `ReconstructFromControlPoints`
2. `Operation.Kind == DerivedOperation` + `Selector.Kind == All`：
   - `TranslateByVector` → `ExactTransform`
   - `ScaleAboutCentroid` 且 `ScaleX == ScaleY == ScaleZ` → `ExactTransform`
   - `ScaleAboutCentroid` 且三轴不等 → `ReconstructFromControlPoints` + warning `STRATEGY_FORCED_RECONSTRUCTION`
   - `OffsetAlongNormal` → `ReconstructFromControlPoints` + warning `STRATEGY_FORCED_RECONSTRUCTION`
3. `Operation.Kind == DerivedOperation` + `Selector.Kind ∈ {Indices, UvRange, EdgeOnly}` → `ReconstructFromControlPoints` + warning `STRATEGY_FORCED_RECONSTRUCTION`
4. `descriptor.SupportKind == Unsupported` → 硬错误 `EDITABLE_KIND_UNSUPPORTED`

实现 1:1 落到 `LiveGeometryEditStrategyResolver.ResolveSurface`，并在 smoke 穷举。

### §4 曲面 DerivedOperation 语义

- **`ScaleAboutCentroid`**：当 `Selector=All` 且 uniform scale 被路由到 ExactTransform 时，centroid 使用 Summary descriptor 中的 `ControlPointCentroidWorld`；当进入 reconstruction 路径时，centroid = 被选控制点算术平均，缩放作用在被选点上，其余原位
- **`TranslateByVector`**：被选点加向量
- **`OffsetAlongNormal`**：
  - 对每个被选 `(u_i, v_j)` 控制点，**使用 Greville abscissa `(u, v)`** 调用 `IGeometryFrameSampler.Sample(objectId, parameterSpec=AtControlPointGrevilles)` 取 frame normal
  - 注意：曲面版本使用**曲面法向**（`Surface.NormalAt(u, v)`），而非曲线的 `PerpendicularFrameAt` Y 轴
  - `PlaneSurface` 法向退化为平面法线，全网格一致（不触发 warning）
  - `NurbsSurface` 某点法向退化 → 该点不偏移，warning `OFFSET_NORMAL_DEGENERATE` 携带 `(U, V)` 列表
- 响应通过 `DerivedOperationApplied.ResolvedUvSamples` 显式回传所用 `(u, v)` 值，保证结果可复现；若现有 `DerivedOperationApplied` 不适合承载曲面字段，本 PLAN 同步扩展该模型或新增曲面专用派生结果模型

### §5 `SurfacePointSelectorKind` 解析

- `All` → 全网格点
- `Indices` → 扁平索引列表（row-major）；越界 / 空 / 重复 / 负数 → `POINT_SELECTOR_INVALID`
- `UvRange { UStart, UEnd, VStart, VEnd }` → 闭区间矩形区域；0 ≤ UStart ≤ UEnd < CountU；V 同理
- `EdgeOnly` → 四条边上的控制点（U=0 / U=CountU-1 / V=0 / V=CountV-1 的并集）；对 closed / periodic 方向退化处理（该轴上的"边"实际是 seam 重合，不重复计入）

与曲线 `PointSelectorKind` **不共用枚举**，防止曲线 / 曲面语义混淆（总纲 §1 分子 2 明确此约束）。

### §6 untrimmed single-face Brep 端到端路径

Apply 路径下 orchestrator 的处理：

1. descriptor 带 `UNDERLYING_SURFACE_FALLBACK` warning → orchestrator 记住"原对象是 Brep"
2. 走 reconstruction 分支（或 ExactTransform）得到新 Surface（reconstruction）或直接落盘（ExactTransform 底座自带）
3. 若 reconstruction 路径：
   - `IBrepSurfaceDowngrader.Evaluate(originalRhinoObject).Rebuild(newSurface)` → 新 `Brep`
   - `IGeometryMutationService.ReplaceWithMetadata(objectId, newBrep, snapshot, "MCP:EditSurfaceGeometry")`
4. 若 ExactTransform 路径：由底座（`GeometryModificationSkill`）自然保持对象类型（既有实现已正确处理 Brep transform）
5. Apply 后对象**仍是 `Brep`**（不从 Brep 变成裸 Surface），Undo 原子性不变

### §7 Preview 响应字段

`GeometryEditPreviewResponse`（扩展自子 PLAN B/C）：

- `Strategy` / `DescriptorSummary` / `Warnings`（既有）
- `ReconstructedCurveSummary: null`（曲面请求下）
- `SurfaceControlPointGridSnapshot: { CountU, CountV, SampleCorners: Point3d[] }`（4 角点，保证响应 token 可控）
- DerivedOperation 时：`ResolvedPointIndices` / `DerivedOperationApplied` / `StrategyResolutionTrace`

### §8 错误与 warning 码

新增 / 曲面启用硬错误：

- `EDIT_POINT_LAYOUT_MISMATCH`
- `EDIT_POINT_INDEX_MISMATCH`（曲面 `Flat` layout 复用该 code 表达 row-major Index 契约失败）

新增 warning：

- `SURFACE_CLOSURE_SEAM_INCONSISTENT`

继承（子 PLAN A/B/C）：

- 硬错误：`LIVE_RHINO_REQUIRED` / `OBJECT_NOT_FOUND` / `EDITABLE_KIND_UNSUPPORTED` / `EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE` / `EDIT_POINT_COUNT_MISMATCH` / `EDIT_POINT_INVALID` / `RECONSTRUCTION_INVALID` / `APPLY_FAILED_ROLLED_BACK` / `STRATEGY_EXPECTATION_MISMATCH` / `POINT_SELECTOR_INVALID` / `SCALE_FACTOR_INVALID` / `TRANSLATE_VECTOR_ZERO`
- warning：`UNDERLYING_SURFACE_FALLBACK` / `DESCRIPTOR_FULL_LARGE` / `FRAME_DEGENERATE` / `METADATA_FIELDS_DROPPED` / `STRATEGY_FORCED_RECONSTRUCTION` / `OFFSET_NORMAL_DEGENERATE` / `PARAMETERIZATION_CHANGED` / `UNSUPPORTED_GEOMETRY_FALLBACK` / `RECONSTRUCTION_SEMANTICS_CHANGED`

`OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME` 在曲面分支不会触发（曲面不存在"退化为线"的场景）。

### §9 Live Smoke CLI slug 独占

- slug：`geometry-edit-surface-composite-smoke-test`
- 注册入口：`Project_Test/260423_TEST_geometry-edit-surface-composite/DeveloperCommandHandler.GeometryEditSurfaceCompositeSmokeTest.cs`
- Rhino 命令：`_McpGeometryEditSurfaceCompositeSmoke`

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_geometry-edit-surface-composite.md`
- `Project_Test/260423_TEST_geometry-edit-surface-composite/`
- `Project_Test/260423_TEST_geometry-edit-surface-composite/DeveloperCommandHandler.GeometryEditSurfaceCompositeSmokeTest.cs`
- `Project_Test/260423_TEST_geometry-edit-surface-composite/surface/`

**新增（Tools）**

- `src/MCP_Rhino.Server/Tools/Geometry/Edit/PreviewEditSurfaceGeometryTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/ApplyEditSurfaceGeometryTool.cs`

**新增（Application）**

- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceEditOrchestrator.cs`
- `src/MCP_Rhino.Server/Application/Services/Edit/SurfaceEditOrchestrator.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryEditSurfaceCompositeSmokeCommand.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointSelectorKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointLayoutKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceEditSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfacePointSelectorSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceEditDerivedOperationParameters.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceControlPointGridSnapshot.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditableSurfacePointGrid.cs`

**新增（Contracts）**

- `src/MCP_Rhino.Server/Contracts/Requests/PreviewEditSurfaceGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyEditSurfaceGeometryRequest.cs`

**修改**

- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryReconstructor.cs`（新增曲面方法）
- `src/MCP_Rhino.Server/Application/Interfaces/IDerivedPointOperationEvaluator.cs`（新增曲面方法）
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryEditStrategyResolver.cs`（新增曲面分派）
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryTransformExecutionBridge.cs`（新增曲面 ExactTransform 执行方法）
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryEditValidator.cs`（新增曲面校验）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryReconstructor.cs`（扩曲面分支 + seam 对齐）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveDerivedPointOperationEvaluator.cs`（扩曲面分支）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryEditStrategyResolver.cs`（扩曲面分支）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryTransformExecutionBridge.cs`（扩曲面 ExactTransform 执行桥）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryEditValidator.cs`（扩曲面校验）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditPreviewResponse.cs`（扩 `SurfaceControlPointGridSnapshot`）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditApplyResponse.cs`（扩 `SurfaceControlPointGridSnapshot`）
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`（注册 `ISurfaceEditOrchestrator`）

**复用（不改）**

- 子 PLAN A 的 `IEditableGeometryDescriptorService` / `IGeometryFrameSampler` / `IGeometryMetadataOperator` / `IBrepSurfaceDowngrader`
- 子 PLAN B 的 `IGeometryMutationService`
- 子 PLAN C 的 `ICurveEditOrchestrator`（不共用；但复用同一 resolver / evaluator / reconstructor / transform execution bridge 实例）
- 既有 `GeometryModificationSkill`（作为 ExactTransform 底座；复用子 PLAN C 新增的 internal transform 协作方法，对曲面 transform 的既有能力复用）

## 使用方式

### 场景 A — 曲面 DirectOverride（`Flat` layout）

```
PreviewEditSurfaceGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DirectOverride" },
    Grid:{ Layout:"Flat", Points:[... CountU*CountV 个点 ...] } })
→ { Strategy:"ReconstructFromControlPoints",
    SurfaceControlPointGridSnapshot:{ CountU:10, CountV:8, SampleCorners:[...] },
    Warnings:[] }

ApplyEditSurfaceGeometry(...)
→ { Strategy:"ReconstructFromControlPoints",
    UndoRecordName:"MCP:EditSurfaceGeometry",
    ObjectId:"<same-guid>", MetadataDropped:[], Warnings:[] }
```

### 场景 B — 曲面 "围绕中心 uniform scale 1.1 倍"（ExactTransform）

```
PreviewEditSurfaceGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"ScaleAboutCentroid",
                DerivedParameters:{ ScaleX:1.1, ScaleY:1.1, ScaleZ:1.1 } },
    PointSelector:{ Kind:"All" } })
→ { Strategy:"ExactTransform",
    DerivedOperationApplied:{ Kind:"ScaleAboutCentroid", CentroidWorld:{...},
                              ScaleX:1.1, ScaleY:1.1, ScaleZ:1.1 },
    Warnings:[] }
```

### 场景 C — 曲面 "把中间一小块控制点沿曲面法向抬 50"

```
PreviewEditSurfaceGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"OffsetAlongNormal",
                DerivedParameters:{ Distance:50 } },
    PointSelector:{ Kind:"UvRange", UStart:3, UEnd:5, VStart:2, VEnd:4 } })
→ { Strategy:"ReconstructFromControlPoints",
    ResolvedPointIndices:[...被选扁平索引...],
    Warnings:["STRATEGY_FORCED_RECONSTRUCTION"] }
```

### 场景 D — 选中 untrimmed single-face Brep

```
GetEditableGeometryDescriptor(filePath, objectId)
→ { Kind:"Surface", ..., Warnings:["UNDERLYING_SURFACE_FALLBACK"] }

PreviewEditSurfaceGeometry(filePath, objectId,
  editSpec={ Operation:{Kind:"DerivedOperation", DerivedKind:"TranslateByVector",
                        DerivedParameters:{ Vector:{X:0,Y:0,Z:10} } },
             PointSelector:{ Kind:"All" } })
→ { Strategy:"ExactTransform", ... }

ApplyEditSurfaceGeometry(...)
→ 对象类型仍为 Brep（Faces.Count==1），UndoRecordName:"MCP:EditSurfaceGeometry"
```

### 场景 E — 对 polysurface / trimmed BrepFace / mesh

```
GetEditableGeometryDescriptor → EDITABLE_KIND_UNSUPPORTED，Message 含具体原因
PreviewEditSurfaceGeometry → 同样 EDITABLE_KIND_UNSUPPORTED
```

## 验收标准

### 构建

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过

### 非 Rhino Plugin 环境

- `dotnet run --project src/MCP_Rhino.Server -- geometry-edit-surface-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm` 返回 `LIVE_RHINO_REQUIRED`

### Rhino Plugin 环境

在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm`，运行 `_McpGeometryEditSurfaceCompositeSmoke`：

- 对至少一个 `PlaneSurface`、一个 `NurbsSurface`、一个 untrimmed single-face Brep 各跑：
  - Full descriptor（子 PLAN A smoke 应已验证读取稳定）
  - DirectOverride（`Flat` + `Grid` 两种 layout 至少各一次） → Preview → Apply
  - DerivedOperation / `All` / `Translate` → Preview → Apply（预期 `ExactTransform`）
  - DerivedOperation / `UvRange` / `OffsetAlongNormal` → Preview → Apply（预期 `ReconstructFromControlPoints`）
  - DerivedOperation / `EdgeOnly` / `TranslateByVector` → Preview → Apply（预期 `ReconstructFromControlPoints`）
- 每分支：
  - `Success=true`；Preview 前后 `doc.Objects.Count` / `doc.Strings.Count` 不变；Preview 不新增 Undo History
  - Apply 阶段恰好新增 1 条 `MCP:EditSurfaceGeometry`
  - Ctrl+Z 一次完整回滚（几何 + metadata）
  - Apply 后图层 / 颜色 / 名称 / user strings 与 Apply 前一致
  - untrimmed single-face Brep 降级路径：Apply 后对象仍是 `Brep`（`RhinoObject.Geometry is Brep`）
- 边界用例：
  - polysurface / trimmed Brep / mesh / subd → `EDITABLE_KIND_UNSUPPORTED`
  - DirectOverride 点数与 `CountU*CountV` 不一致 → `EDIT_POINT_COUNT_MISMATCH`
  - `Layout=Grid` 但传扁平数组 / 反之 → `EDIT_POINT_LAYOUT_MISMATCH`
  - `ScaleAboutCentroid` `ScaleX=0` → `SCALE_FACTOR_INVALID`
  - `OffsetAlongNormal` 某点法向退化 → 该点不偏移 + warning `OFFSET_NORMAL_DEGENERATE`
  - 重建后 Surface `IsValid=false` → Preview warning `RECONSTRUCTION_INVALID`；Apply 硬错误
  - Closed / periodic seam 不一致 → warning `SURFACE_CLOSURE_SEAM_INCONSISTENT`

### LLM 实战 dry-run

- "读一下这个曲面结构"（期望默认 Summary，LLM 不必要求 Full）
- "把这个曲面往上拉一点，整体"（Translate / All → ExactTransform）
- "把曲面中间一块沿法向下凹 20"（OffsetAlongNormal / UvRange → reconstruction）
- "围绕自己中心放大 1.1 倍"（Uniform Scale / All → ExactTransform）
- "我选中的是一个 polysurface，你来编辑它"（应返回 `EDITABLE_KIND_UNSUPPORTED`，不假装处理）

记录：

- LLM 对 Summary vs Full 的使用偏好
- LLM 是否正确放弃对 polysurface / trimmed Brep / mesh 的编辑尝试

若 LLM 无差别一直要 Full，EXET 须标记并评估是否收紧默认值（不强制本期改设计）。

### 回归

- 子 PLAN A / B / C 的三个 smoke 命令在本期合入后继续通过
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` 等 Tool 行为无变化

## 风险与回退方案

### 风险

- **Descriptor Full 体量失控**：高密度 `NurbsSurface`（50×50 以上）的 Full descriptor 体量大；若客户端默认总请求 Full，context window 压力显著
- **Single-face Brep 降级误判**：OuterLoop 与 UnderlyingSurface 边界的容差比较依赖模型容差，不同文档容差差异可能让"实际被 trim 但几乎对齐"的 Brep 被误识别为 untrimmed；反之亦然。该风险在子 PLAN A 已声明，本 PLAN 仅使用结果
- **Closed / periodic seam 对齐**：服务端按元信息对 seam 做统一处理，但 RhinoCommon 的 periodic surface 重建在极端输入下可能产生不符预期的参数化
- **LLM 误把 polysurface 当 surface 处理**：首次尝试失败后 LLM 是否正确放弃、是否进一步 `Brep.Faces` 拆分，是未知数
- **Metadata 白名单不足以覆盖曲面**：沿用曲线白名单可能遗漏曲面独有字段；通过 `METADATA_FIELDS_DROPPED` 可见但需实战验证

### 缓解

- 默认 Summary；Full 需显式请求；Full 且 `CountU * CountV > 10000` 附 warning `DESCRIPTOR_FULL_LARGE`（子 PLAN A 实现）
- 降级判定容差来源（`doc.ModelAbsoluteTolerance`）在 EXET 测试记录中明确列出
- Seam / periodicity 重建在 smoke 覆盖 closed 与 periodic 两类 NurbsSurface
- `EDITABLE_KIND_UNSUPPORTED` 携带具体原因字符串（由分子 8 提供）
- Metadata 白名单差异 warning 在 smoke 中主动抽样验证

### 回退方案

- 若曲面 Tool 整体不稳定：单独下线 2 个曲面 Tool 的 `[McpServerTool]` 标注（保留 Service 代码），不影响曲线能力与子 PLAN A 能力
- 若 resolver 曲面分支误路由：临时把曲面分支回退为"全部走 reconstruction"（ExactTransform 仅曲线），待后续补丁
- 若 single-face Brep 降级存在系统性问题：临时关闭降级（分子 8 的所有 `true` 结果降级为"返回 false + 原因"），让调用方自行 `Brep.Faces[0].ToNurbsSurface()` 再调 Tool
- 本 PLAN 未改曲线能力；独立 `git revert` 可行

## 后续扩展方向

- 子 PLAN E：Plan 4 使用本 PLAN 的 descriptor（含 Brep 降级）+ metadata + mutation 分子，但**不**使用本 PLAN 的重建器（Plan 4 出口是 4PointSurface / BoundarySurface，属 Plan 4 专属分子）
- 独立 PLAN 评估 trimmed `BrepFace` 重建（trim 信息 replay 是整块独立复杂度）
- 独立 PLAN 评估 SubD / Mesh 的 descriptor-first 形态
- `Extrusion` 可考虑内部自动 `ToBrep()` 再复用本 lane，或单独立项
- 若 `Full` 体量成为常态瓶颈，评估引入分页（page by U-strip）或二进制压缩；跨子 PLAN A/B/C/D，需单独 PLAN
- 若 resolver 后续加入"绕任意轴 Rotate / 任意平面 Mirror"等 derived 操作且能对应原生 `Transform`，再扩 ExactTransform 分支
