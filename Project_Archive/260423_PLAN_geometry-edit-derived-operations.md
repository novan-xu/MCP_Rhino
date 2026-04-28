# 260423_PLAN_geometry-edit-derived-operations

## 背景

`260423_PLAN_geometry-edit-descriptor-lane`（Wave 1）已建立 descriptor-first 骨架的最小闭环：
`GetEditableGeometryDescriptor` / `PreviewEditCurveGeometry` / `ApplyEditCurveGeometry` 三个
Tool，仅支持曲线、仅支持 `DirectOverride`、Strategy 固定为 `ReconstructFromControlPoints`。

Wave 1 的最大未解问题是：**大部分几何修改其实都能写成"按规则推点"，而不是由调用方逐个显式
给新坐标**。例如"把这条曲线围绕中心缩放 1.2 倍"、"把这几个控制点沿法向抬高 50mm"、"把选中
的控制点整体沿向量平移"——LLM 更自然的表达是"给规则"，而不是"给所有新点"。同时 Wave 1 的
`IGeometryEditStrategyResolver` 是占位实现，没有真正在 `ExactTransform` 与 reconstruction 之
间选路，导致"全局仿射变换"这类明显应该走既有 `TransformObjects` 的请求，如果由 LLM 误选到
descriptor lane，只能按 reconstruction 路径执行，精度与语义都不理想。

本期是三份拆分中的 **Wave 2**：在 Wave 1 骨架之上新增"派生点操作（DerivedPointOperation）"
与"局部点作用（PointSelectors）"，并把 `IGeometryEditStrategyResolver` 从占位实现升级为真实
路由器，让"能用 exact transform 表达的请求"自动走既有 `TransformObjects` 语义的执行底座，
"必须按规则推点再重建的请求"继续走 Wave 1 的 reconstruction 底座。Wave 3 再扩到曲面。

## 目标

- 新增派生点操作 `DerivedPointOperationKind`：
  - `ScaleAboutCentroid`（以 descriptor 点集 centroid 为中心按比例缩放，可选 X/Y/Z 分量）
  - `TranslateByVector`（沿给定向量平移被选中的点）
  - `OffsetAlongNormal`（仅 NurbsCurve，沿每个控制点的"切线法向"偏移，方向由曲线 frame 决定；
    若某点法向退化则该点回落为不偏移并在 warning 中具名）
  - 不新增其他派生操作；Fillet / Offset / Boolean 等整体曲线变换仍不纳入。
- 新增 `PointSelectors` 支持"局部点作用"：
  - `All`（作用于 descriptor 中全部点）
  - `Indices`（按点索引列表选中）
  - `Range`（按连续索引区间选中）
  - `EndpointsOnly`（仅首尾端点）
  - 不支持"按几何条件"选点（例如"靠近某平面"），若需此能力，由调用方先自己筛 Indices。
- 激活 `IGeometryEditStrategyResolver` 真实路由：
  - 输入：`CurveEditSpec` + descriptor + 候选策略集合。
  - 输出：`GeometryEditStrategyKind` ∈ `{ExactTransform, ReconstructFromPoints,
    ReconstructFromControlPoints}`。
  - 默认偏好顺序：`ExactTransform` > `ReconstructFromControlPoints`；其中 `ReconstructFromPoints`
    作为 PolylineCurve 在 DirectOverride 场景下的细分策略，沿用 Wave 1 行为。
- 明确"ExactTransform lane 在本 Tool 内如何表达"：
  - 本 Tool **内部**调用既有 `TransformObjects` 的底层 Service（即 `GeometryModificationSkill.Apply`
    所封装的变换执行），**不**重写一份变换实现。
  - Strategy resolver 判定为 `ExactTransform` 时：
    - `Preview` 构造等价的 `GeometryTransformSpec`，调用既有 preview 底座，返回
      `GeometryEditPreviewResponse`（Strategy 字段 = `ExactTransform`）。
    - `Apply` 同样构造等价 spec，调用既有 apply 底座；Undo record 命名仍为
      `MCP:EditCurveGeometry`，保持与 Wave 1 一致。
  - 不因路由到 ExactTransform 就二次引入一条"直接走 `TransformObjects` Tool"的 hint 响应；
    所有决策都在服务端完成，客户端只看到 Strategy 字段的自述。
- 扩展响应字段：
  - `GeometryEditPreviewResponse` 与 `GeometryEditApplyResponse` 明确回传：
    - 最终选中 `Strategy`
    - `PointSelectors` 解析后的点索引列表
    - 每个派生操作的**已落地参数**（例如 `ScaleAboutCentroid` 实际采用的 centroid 世界坐标）
    - `Warnings`

**本期明确不纳入：**

- Surface / Brep / Mesh / Subd（整体留 Wave 3）
- 依然不做 Fillet / Chamfer / Blend / Offset / Trim / Split / Join / Loft / Sweep / Morph
- 不新增 Skill / Agent；等三 Wave 完毕再决定是否抽 `GeometryReconstructionSkill`

## 架构归属

- **Tools/Geometry/Edit/**：本期不新增 Tool，继续复用 Wave 1 的
  `PreviewEditCurveGeometryTool` / `ApplyEditCurveGeometryTool`；
  入参 `CurveEditSpec` 新增枚举值，保持 Tool 签名不变。

- **Application/Services/Edit/**：新增 1 个 Service。
  - `RhinoGeometryEditStrategyService`：封装"派生点操作 → 选策略 → 委托底座（reconstruction 或
    exact transform）"的主流程；`RhinoGeometryReconstructionService` 继续处理 reconstruction
    底座，ExactTransform 底座由下列既有服务承担，不在本期重复实现。

- **Application/Services/**（既有，不改签名，仅被 StrategyService 组合）：
  - `GeometryModificationSkill`（既有）作为 ExactTransform 路径的底座；本期只增加一个适配方法
    把 `CurveEditSpec` + DerivedPointOperation 转成 `GeometryTransformSpec`，再委托既有 Skill。
  - 适配逻辑放在 `RhinoGeometryEditStrategyService`，不回写 `GeometryModificationSkill` 内部。

- **Application/Interfaces/**：
  - `IGeometryEditStrategyResolver`（Wave 1 占位接口，现由 `GeometryEditStrategyResolver` 真实
    实现覆盖；接口签名原则上不变，必要时只扩字段不删字段，保持 Wave 1 调用点兼容）。

- **Infrastructure/Rhino/Live/**：
  - `LiveRhinoGeometryReconstructor.cs`（Wave 1 既有）：新增"派生点操作预处理"—— 在重建前
    先按 `DerivedPointOperationSpec` + `PointSelectors` 算出实际新点列表，再走原重建路径。
  - `GeometryEditStrategyResolver.cs`（Wave 1 占位实现）：升级为真实路由器，具体规则见
    §关键设计 §3。

- **Domain/Enums/**：
  - `EditablePointRole`：启用 `DerivedPoint` 枚举位（Wave 1 已保留）。
  - `GeometryEditStrategyKind`：启用 `ExactTransform` 枚举位（Wave 1 已保留）。
  - **新增**：
    - `DerivedPointOperationKind`（`ScaleAboutCentroid` / `TranslateByVector` /
      `OffsetAlongNormal`）。
    - `PointSelectorKind`（`All` / `Indices` / `Range` / `EndpointsOnly`）。

- **Domain/Models/**：
  - `CurveEditSpec`（Wave 1 既有）：新增可选字段 `Operation.DerivedKind`、
    `Operation.DerivedParameters`、`PointSelector`；`Operation.Kind` 新增 `DerivedOperation`
    取值。保留 `DirectOverride` 取值向后兼容。
  - **新增**：
    - `DerivedPointOperationSpec`
    - `PointSelectorSpec`
    - `StrategyResolutionTrace`（可选字段：承载 resolver 决策依据，仅 preview 响应中出现，
      便于调试）

- **Contracts/Requests/**：
  - `PreviewEditCurveGeometryRequest` / `ApplyEditCurveGeometryRequest`（Wave 1 既有）：
    request body 不加必填字段，只有 `EditSpec` 内部语义扩展；JSON 层面使用 discriminator
    `EditSpec.Operation.Kind = "DirectOverride" | "DerivedOperation"`。

- **Contracts/Responses/**：
  - `GeometryEditPreviewResponse` / `GeometryEditApplyResponse`（Wave 1 既有）：
    - 原 `Strategy` 字段（Wave 1 只有 `ReconstructFromControlPoints` 取值）现允许 `ExactTransform`
      / `ReconstructFromPoints`。
    - 新增可选 `ResolvedPointIndices: int[]`。
    - 新增可选 `DerivedOperationApplied: DerivedPointOperationSpec`（回传实际落地参数）。
    - 新增可选 `StrategyResolutionTrace`（仅 Preview）。

- **Server/**：
  - `DependencyInjection.cs`：注册 `RhinoGeometryEditStrategyService` 与 resolver 的真实实现
    替换；Wave 1 的 Service 注册不动。

- **Infrastructure/Plugin/**：
  - `McpGeometryEditDerivedSmokeCommand.cs`：Rhino 端命令 `_McpGeometryEditDerivedSmoke`，
    调用本期独占 slug。

- **Skills / Agents**：本期不新增。

## 关键设计

1. **派生点操作 = "先算点，再交给 Wave 1 重建底座"**
   - `ScaleAboutCentroid`：
     - 输入 `ScaleX / ScaleY / ScaleZ`（均 > 0，至少一个 ≠ 1）。
     - 计算被选点集 centroid（对 Wave 1 descriptor 返回的控制点做算术平均；不做按弧长加权）。
     - 每个被选点：`new = centroid + (old - centroid) * scale`。未被选中的点保持原位。
   - `TranslateByVector`：
     - 输入 `Vector = (dx, dy, dz)`，至少一个分量 ≠ 0。
     - 每个被选点 `new = old + vector`。
   - `OffsetAlongNormal`：
     - 仅 NurbsCurve 与 PolylineCurve；LineCurve 返回硬错误
       `OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME`。
     - 在每个被选控制点的曲线参数上求 `Curve.PerpendicularFrameAt(t)`；取 frame 的 Y 轴作为
       "局部法向"。
     - `new = old + normal * distance`；`distance` 可正可负。
     - 若某点 frame 失败（参数处曲率退化等），该点不偏移，warning `OFFSET_NORMAL_DEGENERATE`
       携带 `Index` 列表。

2. **PointSelectors 的可组合性与优先级**
   - `PointSelectorKind.All` 与其他 Kind 互斥；请求中只允许出现一种 Kind。
   - `Indices` 与 `Range` 二选一；不允许同请求混用。
   - `EndpointsOnly`：
     - 对 `LineCurve` 等价于 `Indices:[0,1]`。
     - 对 `PolylineCurve` / `NurbsCurve` 等价于 `Indices:[0, LastIndex]`。
     - 对 closed 曲线仍使用 Wave 1 descriptor 返回的 `LastIndex`；不做 closed-aware 合并。
   - 索引越界 / 重复 / 负数 → 硬错误 `POINT_SELECTOR_INVALID`。

3. **Strategy resolver 真实路由规则（可枚举、可复核）**
   - 优先级从高到低：
     1. **若 `Operation.Kind = "DirectOverride"`** → 强制 `ReconstructFromControlPoints`
        （PolylineCurve 时退化为 `ReconstructFromPoints`）。Strategy resolver 不再过问 exact
        transform，因为显式给点的语义天然是点位覆盖。
     2. **若 `Operation.Kind = "DerivedOperation"` 且 `PointSelector.Kind = "All"`**：
        - `ScaleAboutCentroid` 且 `ScaleX = ScaleY = ScaleZ` → `ExactTransform`（等价 uniform scale
          about centroid；由 StrategyService 构造 `GeometryTransformSpec.UniformScale` +
          `Origin = centroid`）。
        - `TranslateByVector` → `ExactTransform`（等价 `GeometryTransformSpec.Translate`）。
        - `ScaleAboutCentroid` 且三轴比例不全相等 → `ReconstructFromControlPoints`
          （RhinoCommon 原生 Transform 无法表达非各向同性 scale about centroid，且本期不
           引入自定义变换矩阵 Tool，不把 ExactTransform 扩到非 uniform scale）。
        - `OffsetAlongNormal` → `ReconstructFromControlPoints`（天然不是仿射变换）。
     3. **若 `Operation.Kind = "DerivedOperation"` 且 `PointSelector.Kind ≠ "All"`**：
        无论派生类型 → `ReconstructFromControlPoints`。局部作用本身就不是整体仿射，绝不路由到
        ExactTransform。
   - 上述规则必须 1:1 落到 `GeometryEditStrategyResolver`，并在单元/集成测试里穷举。
   - 当请求体带 `ExpectedStrategy` 且与 resolver 结果不一致 → 硬错误
     `STRATEGY_EXPECTATION_MISMATCH`，响应中回传 resolver 的实际选择。

4. **ExactTransform 执行底座复用，不再实现第二份**
   - StrategyService 判定 `ExactTransform` 后，构造 `TransformObjectsRequest` 并委托
     `GeometryModificationSkill.Apply(...)` 或其对应的 preview 路径。
   - 构造规则：
     - `FilePath` = Request.FilePath
     - `ConfirmedObjectIds = [Request.ObjectId]`
     - `Transform.Kind` = 对应仿射类型（Translate / UniformScale）
     - `Transform.Origin` = 对应锚点（centroid / world origin）
   - 底座返回 `GeometryModificationResponse`，StrategyService 再把它映射成
     `GeometryEditPreviewResponse` / `GeometryEditApplyResponse`：
     - `Strategy = "ExactTransform"`
     - `Warnings` 从底座 response 的 warning 列表合并而来
     - `UndoRecordName = "MCP:EditCurveGeometry"`（统一由本期 Tool 归属，避免用户看到
       `MCP:TransformObjects` 的描述）
   - 该路径**不得**直接调用 `GeometryModificationSkill` 面向 MCP 的 Tool 入口；只调
     Application/Skill 层已有的内部方法，避免绕过 DI 与统一错误处理。

5. **响应自述：决策必须对外可见**
   - `Strategy` 字段必填，值必为 resolver 的最终选择。
   - `DerivedOperationApplied` 必填，内容为 **已落地参数**（例如 `ScaleAboutCentroid` 的
     `CentroidWorld`、`OffsetAlongNormal` 的每点 `AppliedDistance`）。
   - Preview 响应可携带 `StrategyResolutionTrace`，列出 "规则 N → 匹配 / 不匹配" 顺序，便于
     调试；Apply 响应不带 trace，控制 token 体量。

6. **Warning 语义扩充**
   - 复用 Wave 1 warning code，新增：
     - `STRATEGY_FORCED_RECONSTRUCTION`（resolver 本可 ExactTransform，但因局部作用或非各向同
        性 scale 降级到 reconstruction）
     - `OFFSET_NORMAL_DEGENERATE`
   - 禁止新增"含义模糊的总括 warning"，每条都必须可以被客户端程序化解读。

7. **Preview 只读 / Apply 单 Undo**（沿用 Wave 1，不放宽）
   - 无论 ExactTransform 还是 reconstruction，Preview 都不写文档、不开 Undo。
   - Apply 对应单条 Undo record；ExactTransform 路径由既有底座的 `ExecuteWithUndo` 提供，
     本期不重新设计 Undo 边界。

8. **Live Smoke CLI slug 独占**
   - slug：`geometry-edit-derived-smoke-test`（全仓唯一）。
   - 注册入口：`Project_Test/260423_TEST_geometry-edit-derived-operations/DeveloperCommandHandler.GeometryEditDerivedSmokeTest.cs`。
   - Rhino 命令：`_McpGeometryEditDerivedSmoke`，由本期新增的
     `McpGeometryEditDerivedSmokeCommand` 注册。

9. **与 Wave 1 的兼容性**
   - 既有 `DirectOverride` 语义不改；Wave 1 写的集成测试应全部继续通过。
   - Wave 1 EXET 若尚未闭环，本期不开工；Wave 2 严格依赖 Wave 1 EXET 通过。

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_geometry-edit-derived-operations.md`
- `Project_Test/260423_TEST_geometry-edit-derived-operations/`
- `Project_Test/260423_TEST_geometry-edit-derived-operations/DeveloperCommandHandler.GeometryEditDerivedSmokeTest.cs`
- `Project_Test/260423_TEST_geometry-edit-derived-operations/curve/`

**新增（Application）**

- `src/MCP_Rhino.Server/Application/Services/Edit/RhinoGeometryEditStrategyService.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/DerivedPointOperationKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/PointSelectorKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/DerivedPointOperationSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/PointSelectorSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/StrategyResolutionTrace.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryEditDerivedSmokeCommand.cs`

**修改**

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryReconstructor.cs`
  （增加派生点预处理）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/GeometryEditStrategyResolver.cs`
  （占位实现 → 真实路由）
- `src/MCP_Rhino.Server/Domain/Models/CurveEditSpec.cs`
  （扩字段 `DerivedKind` / `DerivedParameters` / `PointSelector`）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditPreviewResponse.cs`
  （扩字段 `ResolvedPointIndices` / `DerivedOperationApplied` / `StrategyResolutionTrace`）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditApplyResponse.cs`
  （扩字段 `ResolvedPointIndices` / `DerivedOperationApplied`）
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
  （注册 StrategyService；resolver 真实实现绑定）

**复用（不改签名）**

- `PreviewEditCurveGeometryTool` / `ApplyEditCurveGeometryTool`
- `GetEditableGeometryDescriptorTool`
- `GeometryModificationSkill`（作为 ExactTransform 底座被组合；不改公共签名）
- `ILiveRhinoDocumentAccessor`

## 使用方式

场景 A — "围绕中心等比缩放一条 NurbsCurve 1.2 倍"（resolver 会选 `ExactTransform`）：

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"ScaleAboutCentroid",
                DerivedParameters:{ ScaleX:1.2, ScaleY:1.2, ScaleZ:1.2 } },
    PointSelector:{ Kind:"All" } })
→ {Strategy:"ExactTransform",
   DerivedOperationApplied:{ Kind:"ScaleAboutCentroid", CentroidWorld:{X,Y,Z},
                             ScaleX:1.2, ScaleY:1.2, ScaleZ:1.2 },
   Warnings:[]}
ApplyEditCurveGeometry(...) → UndoRecordName:"MCP:EditCurveGeometry"
```

场景 B — "围绕中心非各向同性缩放一条 NurbsCurve"（resolver 会选 `ReconstructFromControlPoints`）：

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"ScaleAboutCentroid",
                DerivedParameters:{ ScaleX:1.2, ScaleY:1.0, ScaleZ:1.0 } },
    PointSelector:{ Kind:"All" } })
→ {Strategy:"ReconstructFromControlPoints",
   Warnings:["STRATEGY_FORCED_RECONSTRUCTION", "PARAMETERIZATION_CHANGED"]}
```

场景 C — "只把一条曲线的中间三个控制点沿 +Z 平移 10"（resolver 会选
`ReconstructFromControlPoints`，因为是局部作用）：

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"TranslateByVector",
                DerivedParameters:{ Vector:{X:0, Y:0, Z:10} } },
    PointSelector:{ Kind:"Indices", Indices:[3,4,5] } })
→ {Strategy:"ReconstructFromControlPoints",
   ResolvedPointIndices:[3,4,5]}
```

场景 D — "把一条 NurbsCurve 的末端点沿法向下移 5"：

```
PreviewEditCurveGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"OffsetAlongNormal",
                DerivedParameters:{ Distance:-5 } },
    PointSelector:{ Kind:"EndpointsOnly" } })
→ {Strategy:"ReconstructFromControlPoints",
   DerivedOperationApplied:{ Kind:"OffsetAlongNormal",
     PerPointDistances:[{Index:0, Distance:-5}, {Index:LastIndex, Distance:-5}] }}
```

## 验收标准

构建 / smoke：

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过。
- `dotnet run --project src/MCP_Rhino.Server -- geometry-edit-derived-smoke-test Runtime_Test/MCP_rhino_test.3dm`
  在非 Rhino Plugin 环境下返回明确的 `LIVE_RHINO_REQUIRED`。
- 在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm` 后，运行 `_McpGeometryEditDerivedSmoke`：
  - 覆盖所有 resolver 分支：
    - Translate / All → `ExactTransform`
    - Uniform ScaleAboutCentroid / All → `ExactTransform`
    - Non-uniform ScaleAboutCentroid / All → `ReconstructFromControlPoints`
    - Translate / Indices → `ReconstructFromControlPoints`
    - OffsetAlongNormal / EndpointsOnly → `ReconstructFromControlPoints`
  - 每个分支 `Success=true`；Preview 阶段 `doc.Objects.Count` / `doc.Strings.Count` 前后无变化；
    Preview 阶段 Rhino Undo History 无新条目；Apply 阶段恰好新增 1 条 `MCP:EditCurveGeometry`。
  - 用户在 Rhino 中 Ctrl+Z 一次即可复原几何与所有 metadata。
- Wave 1 的 smoke（`_McpGeometryEditDescriptorSmoke`）在本期合入后继续通过，无回归。

LLM 实战 dry-run：

- 在 MCP Client 侧以自然语言发起代表性任务：
  - "把这条曲线等比放大 1.2 倍"（应路由到 ExactTransform）
  - "把这条曲线的中间几个控制点往上抬一点"（应路由到 reconstruction）
  - "整体沿 X+10 平移"（应路由到 ExactTransform）
- 记录：
  - LLM 是否稳定选择 `DerivedOperation` 而非自己把规则展开成 `DirectOverride`。
  - Strategy 路由是否与语义预期一致。
- 若 LLM 倾向于展开为 `DirectOverride`（降低了 exact transform 复用率），EXET 的"与计划的偏差"
  章节必须记录，作为 Wave 3 立项前置评估材料。

边界用例：

- 多种 Operation.Kind 组合非法组合（如 `PointSelector.Kind="Indices"` 但 `Indices` 为空） →
  硬错误 `POINT_SELECTOR_INVALID`。
- `ExpectedStrategy = "ExactTransform"` 但实际 resolver 选 reconstruction → 硬错误
  `STRATEGY_EXPECTATION_MISMATCH`。
- `ScaleAboutCentroid` 输入 `ScaleX = 0` → 硬错误 `SCALE_FACTOR_INVALID`。
- `TranslateByVector` 输入零向量 → 硬错误 `TRANSLATE_VECTOR_ZERO`。
- `OffsetAlongNormal` 对 `LineCurve` → 硬错误 `OFFSET_ALONG_NORMAL_REQUIRES_CURVE_WITH_FRAME`。
- 派生操作导致新 Curve `IsValid=false` → Preview 返回 `RECONSTRUCTION_INVALID`，Apply 拒绝落盘。
- ExactTransform 路径下 metadata 未经 Wave 1 replay 路径（因为底座是 `GeometryModificationSkill`），
  验证 Apply 前后图层 / 颜色 / 名称 / user strings 仍不变。

## 风险与回退方案

风险：

- **StrategyResolver 误路由**：规则覆盖不全或边界条件遗漏，可能把"本应 exact transform"的请求
  路由到 reconstruction（精度下降）、或反之（语义不符）。
- **ExactTransform 底座组合层**：本期在 `RhinoGeometryEditStrategyService` 里把 `CurveEditSpec`
  转成 `TransformObjectsRequest`；若既有 `GeometryModificationSkill` 的内部方法在后续版本被
  重构，组合层需要同步改。
- **Warning 噪声**：派生操作中可能触发多个 warning（例如
  `STRATEGY_FORCED_RECONSTRUCTION` + `PARAMETERIZATION_CHANGED` 同时出现），影响响应可读性。
- **LLM 选路偏差**：LLM 可能仍然倾向使用 `DirectOverride`，导致 DerivedOperation 低利用率。

缓解：

- Resolver 规则 1:1 枚举 + 穷举 smoke 覆盖，EXET 要求附 resolver 分支测试矩阵。
- 组合层调用既有 Skill 的**内部**方法，不触碰 `GeometryModificationSkill` 公共签名；如需新
  增组合接口，另起小 PLAN。
- Warning 按 code 稳定对外，客户端可按 code 过滤。
- LLM dry-run 纳入验收。

回退方案：

- 若 Strategy resolver 不稳定：
  - 可将 resolver 行为临时回退为"全部走 reconstruction"（等价 Wave 1 行为），不影响既有
    Tools 返回结构；ExactTransform 仅作为可选路径由后续补丁重新启用。
- 若派生操作某一类（例如 `OffsetAlongNormal`）存在系统性问题：
  - 可在 `DerivedPointOperationKind` 枚举层把该值标记为 `Reserved`，Tool 层一律拒绝并提示
    `DERIVED_OPERATION_DISABLED`，不影响其他派生操作。
- 整体 `git revert` 仍是可行兜底；本期不改 Wave 1 的 Tool 接口与接口签名，回退面可控。

## 后续扩展方向

- Wave 3 `260423_PLAN_geometry-edit-surface-lane`：
  - 扩到 `PlaneSurface` / `NurbsSurface` / 单面可安全提取的 surface。
  - descriptor 引入 summary / full 双模式以控 token。
  - Strategy resolver 在 surface 上新增决策规则（例如 uniform scale about centroid 仍可走
    ExactTransform；非均匀缩放 / 局部控制点抬升走 reconstruction）。
- Wave 3 完成后评估：
  - 是否抽 `GeometryReconstructionSkill` 承载"读 descriptor → 推点 → 预览 → 应用"固定流程。
  - 是否把 `OffsetAlongNormal` 扩到曲面法向（差异较大，需要另立 PLAN 评估 frame 计算策略）。
- 后续若 DerivedPointOperation 出现稳定第 4 类（例如 `RotateAbout`），再评估是否纳入；本期
  拒绝在未收集使用数据前扩派生操作集合。
