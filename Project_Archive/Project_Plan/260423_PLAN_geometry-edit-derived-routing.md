# 260423_PLAN_geometry-edit-derived-routing

## 背景

`260423_PLAN_geometry-edit-molecular-architecture`（总纲）定义了 8 个主干分子。子 PLAN A 落地了分子 1 / 3 / 6 / 8（描述器、frame 采样、metadata、Brep 降级）；子 PLAN B 落地了分子 5 曲线分支 + 分子 7，支持 `DirectOverride` 编辑。

本 PLAN 是子 PLAN C，引入两条在总纲中合并后的能力：

- 分子 2：`IDerivedPointOperationEvaluator`（含 Selector 解析）—— 派生点规则求值器
- 分子 4：`IGeometryEditStrategyResolver`（纯策略路由）+ `IGeometryTransformExecutionBridge`（ExactTransform 执行桥）—— 策略决策与执行桥分离

落地后曲线 Preview/Apply Tool 支持 `DerivedOperation`（`ScaleAboutCentroid` / `TranslateByVector` / `OffsetAlongNormal`）+ `PointSelector`（`All` / `Indices` / `Range` / `EndpointsOnly`），并在 `{ExactTransform, ReconstructFromControlPoints, ReconstructFromPoints}` 三类策略之间真实路由。**仍只覆盖曲线**；曲面扩展由子 PLAN D 承担。

被替代的旧 PLAN：`Project_Archive/260423_PLAN_geometry-edit-derived-operations.md`。本 PLAN 与旧 PLAN 的核心差异是：旧 PLAN 假设 Wave 1 已存在 `IGeometryEditStrategyResolver` 占位接口，本期把它"激活";本 PLAN 则是**从零引入**该接口，因为子 PLAN B 并未预留占位，只固定了 `GeometryEditStrategyKind` 枚举。

## 目标

- 落地分子 2：`IDerivedPointOperationEvaluator` + Live/Domain 实现（曲线分支）
- 落地分子 4：`IGeometryEditStrategyResolver` + Live/Domain 实现（纯决策），并新增 `IGeometryTransformExecutionBridge` 作为 ExactTransform 执行桥，委托本 PLAN 明确新增的 `GeometryModificationSkill` internal 协作方法
- 扩展曲线 edit 能力：
  - `CurveEditSpec` 新增 `DerivedOperation` 分支与 `PointSelector` 字段
  - `GeometryEditOperationKind` 启用 `DerivedOperation` 枚举位
  - `GeometryEditStrategyKind` 启用 `ExactTransform` 枚举位
  - 响应扩展：`ResolvedPointIndices` / `DerivedOperationApplied` / `StrategyResolutionTrace`（仅 Preview）
- 引入 `ICurveEditOrchestrator`（Application Service），集中 Tool 的两分支逻辑（reconstruction 路径 + ExactTransform 路径）
- 复用子 PLAN A 的 `IGeometryFrameSampler`（`OffsetAlongNormal` 需要 frame）
- 保证 Apply 仍为单条 Undo record；两条路径下均如此

**明确不纳入：**

- 任何曲面能力（留给子 PLAN D）
- Plan 4 专属分子（留给子 PLAN E）
- 新增 Skill / Agent
- 修改 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` Tool 的既有公共签名

## 架构归属

本 PLAN 实现的分子：**2（曲线分支） / 4**。复用子 PLAN A 的分子 1 / 3 / 6 / 8；复用子 PLAN B 的分子 5（曲线分支）/ 7。

- **Tools/Geometry/Edit/**（修改，不新增文件）
  - `PreviewEditCurveGeometryTool.cs`：内部委托 `ICurveEditOrchestrator`（替换子 PLAN B 的直调 Service 链路）
  - `ApplyEditCurveGeometryTool.cs`：同上
  - **Tool 公共签名不变**；仅 request/response 字段扩展

- **Application/Interfaces/**（新增）
  - `IDerivedPointOperationEvaluator.cs`
  - `IGeometryEditStrategyResolver.cs`
  - `IGeometryTransformExecutionBridge.cs`
  - `ICurveEditOrchestrator.cs`

- **Application/Services/Edit/**（新增）
  - `CurveEditOrchestrator.cs`（纯 orchestration，无 Rhino 直调；通过注入接口协作）

- **Infrastructure/Rhino/Live/**（新增）
  - `LiveDerivedPointOperationEvaluator.cs`（曲线分支；`OffsetAlongNormal` 注入 `IGeometryFrameSampler`）
  - `LiveGeometryEditStrategyResolver.cs`（纯策略决策）
  - `LiveGeometryTransformExecutionBridge.cs`（ExactTransform 执行桥）

- **Domain/Enums/**（修改）
  - `GeometryEditStrategyKind.cs`：启用 `ExactTransform` 取值
  - `GeometryEditOperationKind.cs`：启用 `DerivedOperation` 取值
  - **新增**：
    - `DerivedPointOperationKind.cs`：`ScaleAboutCentroid` / `TranslateByVector` / `OffsetAlongNormal`
    - `PointSelectorKind.cs`：`All` / `Indices` / `Range` / `EndpointsOnly`

- **Domain/Models/**
  - `CurveEditSpec.cs`（修改）：
    - `Operation.DerivedKind: DerivedPointOperationKind?`
    - `Operation.DerivedParameters: DerivedPointOperationParameters?`（discriminator 结构）
    - `PointSelector: PointSelectorSpec?`
  - **新增**：
    - `DerivedPointOperationParameters.cs`：discriminator 载体（`ScaleParameters` / `TranslateParameters` / `OffsetParameters`）
    - `PointSelectorSpec.cs`：`Kind` + `Indices: int[]?` / `Range: {Start, End}?`
    - `StrategyResolutionTrace.cs`：`Steps: { RuleName, Matched, Reason }[]`
    - `DerivedOperationApplied.cs`：已落地参数回传（曲线专用字段，如 `CentroidWorld` / `PerPointOffsets`）

- **Contracts/Requests/**（修改）
  - `PreviewEditCurveGeometryRequest.cs` / `ApplyEditCurveGeometryRequest.cs`：request body 不加必填字段，只扩 `EditSpec` 内部；JSON 层面使用 discriminator `EditSpec.Operation.Kind = "DirectOverride" | "DerivedOperation"`
  - 可选字段：`ExpectedStrategy: GeometryEditStrategyKind?`（调用方声明期望的 Strategy，用于选路校验）

- **Contracts/Responses/**（修改）
  - `GeometryEditPreviewResponse.cs`：
    - 扩可选 `ResolvedPointIndices: int[]`
    - 扩可选 `DerivedOperationApplied`
    - 扩可选 `StrategyResolutionTrace`
  - `GeometryEditApplyResponse.cs`：
    - 扩可选 `ResolvedPointIndices`
    - 扩可选 `DerivedOperationApplied`
    - 不带 `StrategyResolutionTrace`（控制 token 体量）

- **Infrastructure/Plugin/**（新增）
  - `McpGeometryEditDerivedRoutingSmokeCommand.cs`：Rhino 端命令 `_McpGeometryEditDerivedRoutingSmoke`

- **Server/**
  - `DependencyInjection.cs`：注册 `IDerivedPointOperationEvaluator` / `IGeometryEditStrategyResolver` / `IGeometryTransformExecutionBridge` / `ICurveEditOrchestrator`

- **复用（不改）**
  - 子 PLAN A 的全部接口与 Live 实现
  - 子 PLAN B 的 `IGeometryReconstructor` / `IGeometryMutationService` / `IGeometryEditValidator`
  - 既有 `GeometryModificationSkill`（作为 ExactTransform 底座；本 PLAN 新增 internal 协作方法，非 MCP Tool 入口）

- **Skills / Agents**：本 PLAN 不新增。

## 关键设计

### §1 `ICurveEditOrchestrator` 的职责

`ICurveEditOrchestrator.Preview / Apply(request)` 是 Tool 的唯一委托入口。内部逻辑：

1. `IGeometryEditValidator.ValidateRequest(request)`
2. `IEditableGeometryDescriptorService.Read(filePath, objectId, Full)` → descriptor
3. `IGeometryEditValidator.ValidateSpecAgainstDescriptor(spec, descriptor)`
   - 对 `DirectOverride` 走子 PLAN B 的点数校验
   - 对 `DerivedOperation` 走新的 Selector + DerivedParameters 校验（见 §4）
4. `IGeometryEditStrategyResolver.Resolve(spec, descriptor)` → `{ Strategy, Trace }`
5. 若 `request.ExpectedStrategy != null && ExpectedStrategy != Strategy` → 硬错误 `STRATEGY_EXPECTATION_MISMATCH`
6. 按 Strategy 分派：
   - **`ExactTransform`**：
     - `IGeometryTransformExecutionBridge.ExecuteExactTransform(spec, descriptor)` → 构造 `GeometryTransformSpec` + 委托本 PLAN 新增的 `GeometryModificationSkill` internal 方法（Preview / Apply 分别走对应方法）
     - Undo record 名统一为 `MCP:EditCurveGeometry`（即便底座内部命名不同）
     - 响应 `Strategy = ExactTransform`，`ReconstructedCurveSummary` 为 `null`
   - **`ReconstructFromControlPoints` / `ReconstructFromPoints`**：
     - 若 Operation.Kind = DirectOverride：直接用 spec.Points 喂入 `IGeometryReconstructor`
     - 若 Operation.Kind = DerivedOperation：
       - `IDerivedPointOperationEvaluator.Evaluate(descriptor, spec)` → `{ ResolvedIndices, NewPoints, DerivedApplied, Warnings }`
       - 把 NewPoints 喂入 `IGeometryReconstructor`（复用子 PLAN B 的重建路径）
     - Preview 仅返回 summary；Apply 走 `IGeometryMutationService.ReplaceWithMetadata`

### §2 `IGeometryEditStrategyResolver` 真实路由规则（曲线，穷举）

优先级从高到低：

1. **`Operation.Kind == DirectOverride`**：
   - `CurveKind == PolylineCurve` → `ReconstructFromPoints`
   - 其他 → `ReconstructFromControlPoints`
2. **`Operation.Kind == DerivedOperation` 且 `PointSelector.Kind == All`**：
   - `DerivedKind == TranslateByVector` → `ExactTransform`
   - `DerivedKind == ScaleAboutCentroid` 且 `ScaleX == ScaleY == ScaleZ` → `ExactTransform`
   - `DerivedKind == ScaleAboutCentroid` 且三轴比例不全相等 → `ReconstructFromControlPoints`（warning `STRATEGY_FORCED_RECONSTRUCTION`）
   - `DerivedKind == OffsetAlongNormal` → `ReconstructFromControlPoints`（warning `STRATEGY_FORCED_RECONSTRUCTION`）
3. **`Operation.Kind == DerivedOperation` 且 `PointSelector.Kind != All`**：
   - 任意 `DerivedKind` → `ReconstructFromControlPoints`（warning `STRATEGY_FORCED_RECONSTRUCTION`）
4. 其余 → `Unsupported`（本期不会发生；保留以防未来扩枚举）

规则 1:1 落到 `LiveGeometryEditStrategyResolver`，并在 smoke 测试里穷举所有分支。

### §3 ExactTransform 执行桥

`LiveGeometryEditStrategyResolver` 只负责返回 `Strategy` 与 `StrategyResolutionTrace`，不执行 mutation、不调用 Skill。`LiveGeometryTransformExecutionBridge.ExecuteExactTransform` 的契约：

- 输入：spec + descriptor
- 构造 `GeometryTransformSpec`：
  - `TranslateByVector` → `Transform.Kind = Translate`；`Vector = spec.DerivedParameters.Vector`
  - `ScaleAboutCentroid` uniform → `Transform.Kind = UniformScale`；`Origin = descriptor.ControlPointCentroidWorld`（子 PLAN A descriptor 的 Summary / Full 两档均提供）；`Factor = spec.DerivedParameters.ScaleX`
- 通过 DI 注入 `GeometryModificationSkill` 并调用本 PLAN 新增的 **internal 协作方法**（非 MCP Tool 入口），执行 Preview / Apply
- 收敛结果到 `GeometryEditPreviewResponse` / `GeometryEditApplyResponse`：
  - `Strategy = ExactTransform`
  - `Warnings` 从底座 response 合并
  - `UndoRecordName = "MCP:EditCurveGeometry"`（统一由本 lane 归属）
- 不允许直接调用 `GeometryModificationSkill` 的 MCP Tool 入口（绕过 DI 与统一错误处理）

**`GeometryModificationSkill` internal 协作方法盘点结果**：

- 当前 `GeometryModificationSkill` 没有 `internal` / `internal protected` 协作方法；已有 `Preview(PreviewTransformObjectsRequest)` 与 `Apply(TransformObjectsRequest)` 是 public Skill 方法，且 Apply 底层 Undo 描述固定为 `MCP: TransformObjects`。
- 因此本 PLAN 选择"待新增"路径，随本 PLAN 明确新增以下 internal 协作方法；它们不加 `[McpServerTool]`，不改变现有 public Skill 方法签名：
  - `internal OperationResponse<GeometryModificationPreviewResponse> PreviewTransformForGeometryEdit(PreviewTransformObjectsRequest request)`：复用现有 selection 解析与 `RhinoGeometryModificationService.Preview(...)` 路径；不写文档、不开 Undo record。
  - `internal OperationResponse<GeometryModificationResponse> ApplyTransformForGeometryEdit(TransformObjectsRequest request, string undoDescription)`：复用现有 selection 解析与 transform apply 路径，但把 Undo 描述参数化；子 PLAN C 传入 `MCP:EditCurveGeometry`，子 PLAN D 传入 `MCP:EditSurfaceGeometry`。
- 为支撑 `ApplyTransformForGeometryEdit(...)`，`RhinoGeometryModificationService` 的 transform apply 内部方法需接受 `undoDescription` 参数；既有 public `Apply(TransformObjectsRequest, RhinoObjectFilterResult)` 继续传 `MCP: TransformObjects`，保证 `TransformObjects` Tool 行为不变。
- EXET 阶段禁止再新增上述清单以外的 `GeometryModificationSkill` 协作方法；如执行桥还缺能力，必须回到 PLAN 阶段补契约。

本 PLAN 不改 `GeometryModificationSkill` 的 MCP 公共签名（即 `[McpServerTool]` 标注的方法）。

### §4 分子 2 派生点规则求值器（曲线）

`IDerivedPointOperationEvaluator.Evaluate(descriptor, spec)` 输出：

- `ResolvedIndices: int[]`：已解析 selector 后的扁平索引列表
- `NewPoints: Point3d[]`：新点列（未被选点保持原位）
- `DerivedApplied: DerivedOperationApplied`：已落地参数
- `Warnings: string[]`

**Selector 解析规则**：

- `All` → `Indices = [0..PointCount)`
- `Indices` → 原样（校验无越界 / 负数 / 重复）
- `Range { Start, End }` → `Indices = [Start..End]`（闭区间；`Start <= End`；`0 <= Start, End < PointCount`）
- `EndpointsOnly`：
  - `LineCurve` → `[0, 1]`
  - `PolylineCurve` / `NurbsCurve` → `[0, LastIndex]`（对 closed 曲线仍按 descriptor 返回的 `LastIndex`，不做 closed-aware 合并）

校验失败 → 硬错误 `POINT_SELECTOR_INVALID`。

**派生点规则**：

- **`ScaleAboutCentroid`**：
  - 输入：`ScaleX / ScaleY / ScaleZ`（均 > 0，至少一个 ≠ 1；否则 `SCALE_FACTOR_INVALID`）
  - centroid = 被选点集的算术平均（不做弧长加权）
  - 每个被选点：`new = centroid + (old - centroid) * scale`
  - `DerivedApplied.CentroidWorld` 回传
- **`TranslateByVector`**：
  - 输入：`Vector = (dx, dy, dz)`（至少一个分量 ≠ 0；否则 `TRANSLATE_VECTOR_ZERO`）
  - 每个被选点 `new = old + vector`
  - `DerivedApplied.Vector` 回传
- **`OffsetAlongNormal`**：
  - 仅 `NurbsCurve` / `PolylineCurve`；`LineCurve` → `OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME`
  - 对每个被选控制点，调用 `IGeometryFrameSampler.Sample(objectId, parameterSpec=AtControlPointGrevilles)` 获取 frame；采样结果顺序必须与 descriptor 控制点顺序一致，再按 `ResolvedIndices` 取对应 frame；取 frame 的 Y 轴作为"局部法向"
  - `new = old + normal * distance`；`distance` 可正可负
  - 若某点 frame 退化 → 该点不偏移；warning `OFFSET_NORMAL_DEGENERATE` 携带 `Index` 列表
  - `DerivedApplied.PerPointOffsets` 回传每点实际 offset 向量

实现约束：`IDerivedPointOperationEvaluator` 的核心计算应为纯函数（除 frame 采样由注入的 `IGeometryFrameSampler` 完成）。

### §5 响应字段演进

子 PLAN B 定义的响应字段全部保留。本期追加：

- `GeometryEditPreviewResponse`：
  - `ResolvedPointIndices: int[]?`（仅 DerivedOperation 时填充）
  - `DerivedOperationApplied: DerivedOperationApplied?`
  - `StrategyResolutionTrace: StrategyResolutionTrace?`
- `GeometryEditApplyResponse`：
  - `ResolvedPointIndices: int[]?`
  - `DerivedOperationApplied: DerivedOperationApplied?`
  - 不带 Trace

ExactTransform 路径下：

- `ReconstructedCurveSummary = null`
- `ResolvedPointIndices` 仍回传（即 `All`）
- `DerivedOperationApplied` 回传实际落地的 transform 参数（centroid / vector）

### §6 新增 warning 码

- `STRATEGY_FORCED_RECONSTRUCTION`：resolver 本可 ExactTransform，但因局部作用或非各向同性 scale 降级
- `OFFSET_NORMAL_DEGENERATE`：携带退化点 `Index` 列表

新增硬错误码：

- `STRATEGY_EXPECTATION_MISMATCH`
- `POINT_SELECTOR_INVALID`
- `SCALE_FACTOR_INVALID`
- `TRANSLATE_VECTOR_ZERO`
- `OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME`

（子 PLAN B 的错误码继承使用）

### §7 Preview 只读 / Apply 单 Undo

- ExactTransform 与 Reconstruction 两条路径下，Preview 都不写文档、不开 Undo record
- Apply 对应单条 Undo record，`UndoRecordName = "MCP:EditCurveGeometry"`
- ExactTransform 路径由底座的 `ExecuteWithUndo` 提供 Undo record，本 PLAN 不重新设计 Undo 边界
- 两条路径的 Apply 响应在 `UndoRecordName` 字段上一致（用户看不出底座差异）

### §8 与子 PLAN B 的兼容性

- `DirectOverride` 语义完全不变；子 PLAN B 写的集成测试应继续通过
- 子 PLAN B 的 Tool 签名不变；仅 `CurveEditSpec` 字段扩展
- 子 PLAN B 的 `EDIT_OPERATION_UNSUPPORTED_IN_CURRENT_WAVE` 硬错误码在本期**不再触发**（因为 `DerivedOperation` 现在被支持）；错误码本身保留在枚举中以备未来收紧

### §9 Live Smoke CLI slug 独占

- slug：`geometry-edit-derived-routing-smoke-test`
- 注册入口：`Project_Test/260423_TEST_geometry-edit-derived-routing/DeveloperCommandHandler.GeometryEditDerivedRoutingSmokeTest.cs`
- Rhino 命令：`_McpGeometryEditDerivedRoutingSmoke`

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_geometry-edit-derived-routing.md`
- `Project_Test/260423_TEST_geometry-edit-derived-routing/`
- `Project_Test/260423_TEST_geometry-edit-derived-routing/DeveloperCommandHandler.GeometryEditDerivedRoutingSmokeTest.cs`
- `Project_Test/260423_TEST_geometry-edit-derived-routing/curve/`

**新增（Application）**

- `src/MCP_Rhino.Server/Application/Interfaces/IDerivedPointOperationEvaluator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryEditStrategyResolver.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryTransformExecutionBridge.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ICurveEditOrchestrator.cs`
- `src/MCP_Rhino.Server/Application/Services/Edit/CurveEditOrchestrator.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveDerivedPointOperationEvaluator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryEditStrategyResolver.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryTransformExecutionBridge.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryEditDerivedRoutingSmokeCommand.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/DerivedPointOperationKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/PointSelectorKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/DerivedPointOperationParameters.cs`
- `src/MCP_Rhino.Server/Domain/Models/PointSelectorSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/StrategyResolutionTrace.cs`
- `src/MCP_Rhino.Server/Domain/Models/DerivedOperationApplied.cs`

**修改**

- `src/MCP_Rhino.Server/Domain/Enums/GeometryEditStrategyKind.cs`（启用 `ExactTransform`）
- `src/MCP_Rhino.Server/Domain/Enums/GeometryEditOperationKind.cs`（启用 `DerivedOperation`）
- `src/MCP_Rhino.Server/Domain/Models/CurveEditSpec.cs`（扩字段）
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/PreviewEditCurveGeometryTool.cs`（改为委托 `ICurveEditOrchestrator`）
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/ApplyEditCurveGeometryTool.cs`（同上）
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewEditCurveGeometryRequest.cs`（`ExpectedStrategy` 可选字段）
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyEditCurveGeometryRequest.cs`（同上）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditPreviewResponse.cs`（扩字段）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditApplyResponse.cs`（扩字段）
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryEditValidator.cs`（扩 DerivedOperation 校验方法）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryEditValidator.cs`（扩实现）
- `src/MCP_Rhino.Server/Skills/Modeling/GeometryModificationSkill.cs`：新增 `internal PreviewTransformForGeometryEdit(...)` / `internal ApplyTransformForGeometryEdit(..., undoDescription)`；不改 public Skill 方法签名
- `src/MCP_Rhino.Server/Application/Services/RhinoGeometryModificationService.cs`：将 transform apply 内部 Undo 描述参数化；既有 public `TransformObjects` 路径仍使用 `MCP: TransformObjects`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`（注册新接口）

**复用（不改）**

- 全部子 PLAN A 接口与 Live 实现
- 子 PLAN B 的 `IGeometryReconstructor` / `IGeometryMutationService`
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` Tool（公共签名）

## 使用方式

### 场景 A — "围绕中心等比缩放一条 NurbsCurve 1.2 倍"（resolver 选 `ExactTransform`）

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"ScaleAboutCentroid",
                DerivedParameters:{ ScaleX:1.2, ScaleY:1.2, ScaleZ:1.2 } },
    PointSelector:{ Kind:"All" } })
→ { Strategy:"ExactTransform",
    DerivedOperationApplied:{ Kind:"ScaleAboutCentroid", CentroidWorld:{...},
                              ScaleX:1.2, ScaleY:1.2, ScaleZ:1.2 },
    Warnings:[] }

ApplyEditCurveGeometry(...) → UndoRecordName:"MCP:EditCurveGeometry"
```

### 场景 B — "围绕中心非各向同性缩放"（resolver 选 reconstruction）

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"ScaleAboutCentroid",
                DerivedParameters:{ ScaleX:1.2, ScaleY:1.0, ScaleZ:1.0 } },
    PointSelector:{ Kind:"All" } })
→ { Strategy:"ReconstructFromControlPoints",
    Warnings:["STRATEGY_FORCED_RECONSTRUCTION", "PARAMETERIZATION_CHANGED"] }
```

### 场景 C — "把一条 NurbsCurve 的中间三个控制点沿 +Z 平移 10"

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"TranslateByVector",
                DerivedParameters:{ Vector:{X:0, Y:0, Z:10} } },
    PointSelector:{ Kind:"Indices", Indices:[3,4,5] } })
→ { Strategy:"ReconstructFromControlPoints",
    ResolvedPointIndices:[3,4,5],
    Warnings:["STRATEGY_FORCED_RECONSTRUCTION"] }
```

### 场景 D — "把 NurbsCurve 末端点沿法向下移 5"

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"OffsetAlongNormal",
                DerivedParameters:{ Distance:-5 } },
    PointSelector:{ Kind:"EndpointsOnly" } })
→ { Strategy:"ReconstructFromControlPoints",
    DerivedOperationApplied:{ Kind:"OffsetAlongNormal",
      PerPointOffsets:[{Index:0, Offset:{...}}, {Index:LastIndex, Offset:{...}}] },
    Warnings:["STRATEGY_FORCED_RECONSTRUCTION"] }
```

### 场景 E — `ExpectedStrategy` 不匹配

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={ Operation:{ Kind:"DerivedOperation", DerivedKind:"TranslateByVector",
                         DerivedParameters:{ Vector:{X:10, Y:0, Z:0} } },
             PointSelector:{ Kind:"Indices", Indices:[0] } },
  expectedStrategy="ExactTransform")
→ Success=false, ErrorCode:"STRATEGY_EXPECTATION_MISMATCH",
  Message:"resolver selected ReconstructFromControlPoints"
```

## 验收标准

### 构建

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过

### 非 Rhino Plugin 环境

- `dotnet run --project src/MCP_Rhino.Server -- geometry-edit-derived-routing-smoke-test Runtime_Test/MCP_rhino_test.3dm` 返回 `LIVE_RHINO_REQUIRED`

### Rhino Plugin 环境

在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm`，运行 `_McpGeometryEditDerivedRoutingSmoke`：

- **resolver 分支穷举**（每项至少一轮 Preview + Apply）：
  - `DirectOverride` / NurbsCurve → `ReconstructFromControlPoints`
  - `DirectOverride` / PolylineCurve → `ReconstructFromPoints`
  - `Translate` / `All` → `ExactTransform`
  - `Uniform ScaleAboutCentroid` / `All` → `ExactTransform`
  - `Non-uniform ScaleAboutCentroid` / `All` → `ReconstructFromControlPoints` + warning `STRATEGY_FORCED_RECONSTRUCTION`
  - `Translate` / `Indices` → `ReconstructFromControlPoints` + warning `STRATEGY_FORCED_RECONSTRUCTION`
  - `OffsetAlongNormal` / `EndpointsOnly` → `ReconstructFromControlPoints` + warning `STRATEGY_FORCED_RECONSTRUCTION`
- 每分支：
  - `Success=true`
  - Preview 前后 `doc.Objects.Count` / `doc.Strings.Count` 不变；Preview 不新增 Undo History
  - Apply 阶段恰好新增 1 条 `MCP:EditCurveGeometry`
  - Ctrl+Z 一次完整回滚（几何 + metadata）
- 边界用例：
  - `POINT_SELECTOR_INVALID`：`Indices` 越界 / 为空 / 负数 / 重复
  - `SCALE_FACTOR_INVALID`：`ScaleX = 0`
  - `TRANSLATE_VECTOR_ZERO`：全零向量
  - `OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME`：`LineCurve` + `OffsetAlongNormal`
  - `STRATEGY_EXPECTATION_MISMATCH`：`ExpectedStrategy` 与 resolver 结果不一致
  - `RECONSTRUCTION_INVALID`：派生操作后新 Curve `IsValid=false` → Preview warning；Apply 硬错误
  - ExactTransform 路径下 Apply 前后图层 / 颜色 / 名称 / user strings 不变

### LLM 实战 dry-run

- "把这条曲线等比放大 1.2 倍"（预期 ExactTransform）
- "把这条曲线的中间几个控制点往上抬一点"（预期 reconstruction）
- "整体沿 X+10 平移"（预期 ExactTransform）

记录：

- LLM 是否稳定选择 `DerivedOperation` 而非自己展开成 `DirectOverride`
- Strategy 路由是否与语义预期一致

若 LLM 倾向展开为 `DirectOverride`（降低 ExactTransform 复用率），EXET 的"与计划的偏差"章节必须记录，作为子 PLAN D 立项前置评估材料。

### 回归

- 子 PLAN A 的 `_McpGeometryEditMolecularFoundationSmoke` 通过
- 子 PLAN B 的 `_McpGeometryEditCurveCompositeSmoke` 通过（DirectOverride 行为完全不变）
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` 等 Tool 行为无变化

## 风险与回退方案

### 风险

- **Strategy resolver 误路由**：规则覆盖不全或边界条件遗漏，可能把"本应 exact transform"的请求路由到 reconstruction（精度下降）、或反之（语义不符）
- **ExactTransform 底座组合层脆弱**：本 PLAN 新增的 `GeometryModificationSkill` internal 方法若在后续被重构，执行桥需要同步改；本 PLAN 通过 `IGeometryTransformExecutionBridge` 隔离该耦合，并只依赖 `internal` 方法（而不是 MCP Tool 入口）
- **Warning 噪声**：派生操作中可能触发多个 warning 同时出现（例如 `STRATEGY_FORCED_RECONSTRUCTION` + `PARAMETERIZATION_CHANGED`）
- **LLM 选路偏差**：LLM 可能仍倾向于使用 `DirectOverride` 展开规则，导致 DerivedOperation 低利用率

### 缓解

- Resolver 规则 1:1 枚举 + 穷举 smoke 覆盖；EXET 需附 resolver 分支测试矩阵
- 执行桥只调本 PLAN 明确列出的 `GeometryModificationSkill` internal 方法；若需新增清单外协作入口，先回到 PLAN 阶段补契约
- Warning 按 code 稳定对外，客户端可按 code 过滤
- LLM dry-run 纳入验收

### 回退方案

- 若 Strategy resolver 不稳定：临时回退 resolver 行为为"全部走 reconstruction"（等价子 PLAN B 行为），不影响既有响应结构；ExactTransform 作为可选路径由后续补丁重新启用
- 若派生操作某一类（例如 `OffsetAlongNormal`）存在系统性问题：可在 `DerivedPointOperationKind` 枚举层把该值标记为 `Reserved`，orchestrator 一律拒绝并提示 `DERIVED_OPERATION_DISABLED`，不影响其他派生操作
- 本 PLAN 未改既有 Tool / Service 公共签名，可独立 `git revert`（需同时回退 Tool 文件中 `ICurveEditOrchestrator` 委托改动）

## 后续扩展方向

- 子 PLAN D：曲面 Preview/Apply Tool 需要 resolver 曲面分支（Uniform scale / translate on All → ExactTransform；其余 → reconstruction）；`IDerivedPointOperationEvaluator` 扩曲面 2D grid 分支；`SurfacePointSelectorKind` 在子 PLAN D 另立枚举（不复用曲线 `PointSelectorKind`）
- 子 PLAN E：Plan 4 不引入新的 `DerivedPointOperationKind`；它使用自己的参考边 + LCS + 点序规范化规则构建新点列，再走本 PLAN 的 reconstruction 路径不合适（出口是 4Point / BoundarySurface，不是控制点重建）——由 Plan 4 子 PLAN 自行编排
- 若后续加入"绕任意轴 Rotate / 任意平面 Mirror"等 derived 操作且能对应 RhinoCommon 原生 `Transform`，再扩 ExactTransform 分支
- 若实战表明 `StrategyResolutionTrace` 对 LLM 无实际用途，Preview 响应可以移除该字段减小 token；以小 PLAN 推动
