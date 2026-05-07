# 260423_PLAN_geometry-edit-surface-lane

## 背景

`260423_PLAN_geometry-edit-descriptor-lane`（Wave 1）与
`260423_PLAN_geometry-edit-derived-operations`（Wave 2）已经把 descriptor-first 编辑能力在
**曲线**上打通：
- Wave 1：`GetEditableGeometryDescriptor` / `PreviewEditCurveGeometry` / `ApplyEditCurveGeometry`
  三个 Tool + Curve DirectOverride + metadata replay。
- Wave 2：DerivedPointOperation + PointSelectors + StrategyResolver 真实路由。

曲面是 MCP_Rhino 进阶编辑里跨不过去的另一半：Rhino 工程建模里大部分"细化调整"都落在曲面上
（拉抬控制点、围绕中心缩放、沿法向偏移局部点等），而曲面又带来曲线上没有的结构性复杂度：
- 控制点是二维网格，不是一维列表。
- 存在 U/V 两个方向各自的 degree、knots、closure、periodicity。
- 部分曲面以 `BrepFace` 形式嵌在 `Brep` 内部；本期仍不支持 trimmed `BrepFace`，但需要明确
  判别逻辑。
- descriptor 体量随 `U×V` 爆炸，必须引入 summary / full 双模式控 token。

本期是三份拆分中的 **Wave 3**：在 Wave 1/2 的骨架与策略路由之上，把能力线扩到曲面，并引入
descriptor 的两档体量模式。Wave 3 完成后，整个 descriptor-first lane 在曲线 / 曲面上对称可用，
后续再按需抽 `GeometryReconstructionSkill` 或扩到 polysurface / subd / mesh。

## 目标

- 新增面向曲面的 descriptor-first 编辑 Tool：
  - `PreviewEditSurfaceGeometryTool`
  - `ApplyEditSurfaceGeometryTool`
  - `GetEditableGeometryDescriptorTool`（Wave 1 既有）本期扩展为同时支持曲面对象。
- 首期曲面对象矩阵严格收敛：
  - `PlaneSurface`
  - `NurbsSurface`
  - 以 `RhinoObject.Geometry` 形式存在且 `Brep.Faces.Count == 1 && Brep.Faces[0].IsSurface &&
    Brep.Faces[0].OuterLoop.To3dCurve() 与 UnderlyingSurface 边界重合` 的 "untrimmed single-face
    Brep"，在内部会被降级为其 `UnderlyingSurface` 处理。
  - 明确**不支持**：
    - 任何 trimmed `BrepFace`
    - `Brep.Faces.Count > 1`（polysurface）
    - `Mesh` / `SubD`
    - `Extrusion`（虽然可隐式转 Surface，但本期不纳入以避免语义混淆；调用方可先
      `Extrusion.ToBrep()` 再编辑）
- descriptor 引入两档体量模式：
  - `DescriptorDetail.Summary`：只给"结构信息"（U/V degree、U/V count、IsClosed[U/V]、
    IsPeriodic[U/V]、bbox、metadata summary、SupportKind、warning hint），不给点列。
  - `DescriptorDetail.Full`：在 `Summary` 基础上追加完整 U/V 控制点网格。
  - 默认曲线 `Full`、曲面 `Summary`。曲面请求 `Full` 需显式声明。
- 首期曲面编辑语义：
  - 沿用 Wave 1 的 `DirectOverride`：调用方给出完整 U×V 新控制点网格。
  - 沿用 Wave 2 的 `DerivedPointOperation`：`ScaleAboutCentroid` / `TranslateByVector` /
    `OffsetAlongNormal`（曲面版本的"法向"使用 `Surface.NormalAt(u,v)`，对 `PlaneSurface` 即平
    面法线）。
  - 沿用 Wave 2 的 `PointSelectors`，但语义从"索引 / 范围"扩展到 **2D grid**：
    - `All`
    - `Indices`（扁平索引；row-major 对齐 descriptor）
    - `UvRange`（`UStart..UEnd × VStart..VEnd` 矩形区域）
    - `EdgeOnly`（四条边上的控制点；对 closed / periodic 方向退化处理）
- 首期曲面 StrategyResolver 规则：
  - DirectOverride → `ReconstructFromControlPoints`。
  - DerivedOperation + `All` + uniform `ScaleAboutCentroid` → `ExactTransform`
    （委托曲线版本已打通的底座）。
  - DerivedOperation + `All` + `TranslateByVector` → `ExactTransform`。
  - DerivedOperation + 非均匀 `ScaleAboutCentroid` / `OffsetAlongNormal` / 局部作用 →
    `ReconstructFromControlPoints`。
  - 不支持对象 → resolver 直接返回 `Unsupported`，Tool 返回 `EDITABLE_KIND_UNSUPPORTED`。
- Apply 必须保留图层、颜色、名称、可安全复制的 object attributes、object user strings（与
  Wave 1 完全一致的 metadata 白名单）。

**本期明确不纳入：**

- Trimmed `BrepFace` 重建（trim 信息保留策略足够复杂，需要独立 PLAN）。
- Polysurface / SubD / Mesh / Extrusion 重建。
- Surface 级别的 `MatchSrf` / `Blend` / `Loft` / `Sweep`（属于整体重构命令，不走 descriptor
  路径）。
- 曲面上的"按几何条件选点"（例如"靠近某平面的控制点"）；仍由调用方自己筛 Indices。
- 新增 Skills / Agents。

## 架构归属

- **Tools/Geometry/Edit/**：新增 2 个 Tool。
  - `PreviewEditSurfaceGeometryTool`
  - `ApplyEditSurfaceGeometryTool`
  - `GetEditableGeometryDescriptorTool`（Wave 1 既有）不新建文件，在 Service 层扩曲面分支；
    Tool 签名追加可选参数 `detail: DescriptorDetail = Summary|Full`。

- **Application/Services/Edit/**：扩展既有 Service，不新建 Service。
  - `RhinoEditableGeometryDescriptorService`：扩曲面 descriptor 读取 + 两档体量支持。
  - `RhinoGeometryReconstructionService`：扩曲面 preview / apply 主流程。
  - `RhinoGeometryEditStrategyService`（Wave 2 既有）：扩曲面分支规则；曲线与曲面复用同一 resolver
    实例，分支内部按 descriptor kind 分派。

- **Application/Interfaces/**：
  - `ILiveEditableGeometryReader`（Wave 1 既有）：方法签名追加 `detail` 参数，曲线默认 `Full`
    保留 Wave 1 行为。
  - `ILiveGeometryReconstructor`（Wave 1 既有）：新增曲面重建方法，不改曲线方法签名。
  - 其余 Wave 1/2 接口不动。

- **Infrastructure/Rhino/Live/**：
  - `LiveRhinoEditableGeometryReader.cs`（Wave 1 既有）：扩曲面读取与 summary / full 分支。
  - `LiveRhinoGeometryReconstructor.cs`（Wave 1/2 既有）：扩曲面重建、扩派生点操作对 2D 网
    格的预处理。
  - `LiveRhinoGeometryMetadataOperator.cs`（Wave 1 既有）：不改；沿用同一白名单。
  - `GeometryEditStrategyResolver.cs`（Wave 2 既有）：扩曲面分支规则。
  - `GeometryEditValidator.cs`（Wave 1 既有）：扩曲面输入校验。

- **Domain/Enums/**：
  - `EditableGeometryKind`：启用 `Surface` 枚举位（Wave 1 已保留）。
  - **新增**：
    - `DescriptorDetail`（`Summary` / `Full`）。
    - `SurfacePointSelectorKind`（`All` / `Indices` / `UvRange` / `EdgeOnly`）。
    - 说明：为避免曲线 / 曲面 PointSelector 语义混淆，本期**不**复用 Wave 2 的
      `PointSelectorKind`；曲面走专属枚举与专属 `SurfacePointSelectorSpec`。

- **Domain/Models/**：
  - `EditableGeometryDescriptor`（Wave 1 既有）：扩可选字段 `SurfaceStructure`（degree U/V、
    count U/V、closure U/V、periodicity U/V）。
  - **新增**：
    - `SurfaceEditSpec`
    - `SurfacePointSelectorSpec`
    - `SurfaceControlPointGridSnapshot`（用于 Preview 响应中的"已落地点网格摘要"）
    - `SurfaceEditDerivedOperationSpec`（复用 Wave 2 `DerivedPointOperationKind` 枚举值，但参
      数结构专属曲面：`OffsetAlongNormal` 为每 (u,v) 采样曲面法向，而非曲线 frame 法向）

- **Contracts/Requests/**：新增。
  - `PreviewEditSurfaceGeometryRequest`
  - `ApplyEditSurfaceGeometryRequest`
  - `GetEditableGeometryDescriptorRequest`（Wave 1 既有）扩字段：`Detail: DescriptorDetail?`
    （曲线默认 Full、曲面默认 Summary）。

- **Contracts/Responses/**：
  - `EditableGeometryDescriptorResponse`（Wave 1 既有）：扩可选字段 `SurfaceStructure` /
    `SurfaceControlPoints`（后者仅 `Full` 模式返回）。
  - `GeometryEditPreviewResponse` / `GeometryEditApplyResponse`（Wave 1/2 既有）：
    - 扩可选字段 `SurfaceControlPointGridSnapshot`（仅曲面请求回传）。
    - 其余字段语义不变。

- **Server/**：
  - `DependencyInjection.cs`：本期无新 Service 注册（所有扩展都在既有 Service 内部），保持注册
    列表稳定。
  - `ToolRegistration.cs`：依赖反射自动发现曲面 Tool，不手改。

- **Infrastructure/Plugin/**：
  - `McpGeometryEditSurfaceSmokeCommand.cs`：Rhino 端命令 `_McpGeometryEditSurfaceSmoke`，
    调用本期独占 slug。

- **Skills / Agents**：本期不新增；Wave 3 完成后再单独评估是否抽
  `GeometryReconstructionSkill`。

## 关键设计

1. **Descriptor 两档体量模式**
   - `Summary`：
     - 固定小体量字段：曲面类型、U/V degree、U/V count、IsClosedU/V、IsPeriodicU/V、
       bbox（min/max 世界坐标）、metadata summary、SupportKind、warning hint。
     - 不含点列；响应体量与 U×V 无关，可作为 LLM 规划决策默认入口。
   - `Full`：
     - 在 Summary 基础上追加完整 U×V 控制点网格。
     - 点以 row-major 扁平索引输出：`index = u * countV + v`，descriptor 明确写出这条规则。
   - 默认值：
     - 曲线请求默认 `Full`（保留 Wave 1 行为，不回归）。
     - 曲面请求默认 `Summary`。
   - Token 预算：`Summary` 目标 < 500 tokens；`Full` 在 100×100 控制点极端情况下允许 > 30k
     tokens，响应层附 warning `DESCRIPTOR_FULL_LARGE` 提示客户端按需分段。

2. **曲面 DirectOverride 点网格契约**
   - 调用方必须给出与 descriptor 完全一致的 `(countU, countV)` 的点网格，扁平数组或二维数组
     两种等价表示**择一**（request JSON 用 discriminator `Layout: "Flat" | "Grid"`）。
   - 点数不一致、`Layout` 与实际数据不一致、点坐标 NaN / Inf → 硬错误
     `EDIT_POINT_COUNT_MISMATCH` / `EDIT_POINT_LAYOUT_MISMATCH` / `EDIT_POINT_INVALID`。
   - 不允许调用方改 U/V degree / U/V knots / closure / periodicity；上述字段只读。
   - closed / periodic 方向若调用方给的"逻辑起始列"与"逻辑结束列"不一致 →
     warning `SURFACE_CLOSURE_SEAM_INCONSISTENT`，服务端按 descriptor 的 closed/periodic 元
     信息做 seam 对齐，但不强行纠正；Preview 展示服务端最终采用的点网格。

3. **曲面 DerivedOperation**
   - `ScaleAboutCentroid`：
     - centroid 取被选点网格的控制点算术平均（与 Wave 2 一致，无加权）。
     - 缩放作用在 **被选中** 的控制点上；未被选中的保持原位。
   - `TranslateByVector`：
     - 对被选中的控制点加向量。
   - `OffsetAlongNormal`：
     - 对每个被选控制点 `(u_i, v_j)`，在 `Surface.NormalAt(u_i, v_j)` 处取曲面法向；
       `new = old + normal * distance`。
     - `PlaneSurface` 的法向退化为平面法线，全网格一致；属正常情况不触发 warning。
     - `NurbsSurface` 某点法向退化（长度近零） → 该点不偏移，warning
       `OFFSET_NORMAL_DEGENERATE` 携带 `(U, V)` 列表。
   - 曲面法向的参数取值：直接取控制点对应节点的 `Greville abscissa` (u, v)，不自行重新采样，
     以便 `PlaneSurface` / 规则 `NurbsSurface` 上结果可复现；该约定在响应中以
     `ResolvedUvSamples` 明写。

4. **曲面 StrategyResolver 规则**
   - 与 Wave 2 曲线规则对齐，按优先级从高到低：
     1. `Operation.Kind = "DirectOverride"` → `ReconstructFromControlPoints`。
     2. `Operation.Kind = "DerivedOperation"` + `Selector.Kind = "All"`：
        - `ScaleAboutCentroid` 且 ScaleX = ScaleY = ScaleZ → `ExactTransform`。
        - `TranslateByVector` → `ExactTransform`。
        - 非均匀 `ScaleAboutCentroid` / `OffsetAlongNormal` → `ReconstructFromControlPoints`。
     3. `Operation.Kind = "DerivedOperation"` + `Selector.Kind ≠ "All"` →
        `ReconstructFromControlPoints`。
     4. 对象判为 `Unsupported` → resolver 直接报 `Unsupported`，Tool 返回
        `EDITABLE_KIND_UNSUPPORTED`。
   - ExactTransform 路径委托给 Wave 2 已打通的 `RhinoGeometryEditStrategyService` 内部组合
     层（构造 `TransformObjectsRequest` → 委托 `GeometryModificationSkill` 内部方法）；曲面
     不需要第二条独立路径。
   - 与 Wave 2 一致，`ExpectedStrategy` 不匹配 → 硬错误 `STRATEGY_EXPECTATION_MISMATCH`。

5. **Untrimmed single-face Brep 降级判定**
   - 目的：允许用户直接选中一个"看起来像 surface 但对象是 Brep"的对象（这是 Rhino 实际场景
     的常态）。
   - 判定条件（全部满足才降级）：
     - `RhinoObject.Geometry is Brep brep`
     - `brep.Faces.Count == 1`
     - `brep.Faces[0].IsSurface`
     - `brep.Faces[0].UnderlyingSurface()` 非空
     - `brep.Faces[0].OuterLoop.To3dCurve()` 的包围区域与 `UnderlyingSurface` 的参数域边界
       在模型容差内一致（即"没有被 trim"）
   - 一旦降级：
     - descriptor 以 `UnderlyingSurface` 形态返回；同时响应带
       `UNDERLYING_SURFACE_FALLBACK` warning，告知调用方本期对该对象按 surface 处理。
     - Apply 时用新 surface 重新 `Brep.CreateFromSurface` 后 `doc.Objects.Replace`，保持对象
       仍是 Brep（避免类型变化引发 downstream 问题）；重建 Brep 后再 metadata replay。
   - 任一条件不满足 → `EDITABLE_KIND_UNSUPPORTED`，绝不静默处理。

6. **Metadata 白名单不动**
   - 沿用 Wave 1 的 metadata 白名单；曲面 Apply 与曲线 Apply 走同一个
     `LiveRhinoGeometryMetadataOperator`，不为曲面复制新一套白名单。
   - 白名单差异仍以 `METADATA_FIELDS_DROPPED` 具名返回。

7. **Preview 只读 / Apply 单 Undo（不放宽）**
   - 曲面 Preview 在主线程读取真值 + 内存重建 + 可选法向采样；不写文档、不开 Undo。
   - 曲面 Apply 单次 Tool 调用 = 单次 Undo record（`MCP:EditSurfaceGeometry`）。曲线 Apply 的
     `MCP:EditCurveGeometry` 不变；曲线 / 曲面用**不同** Undo 描述，但 Undo 原子性行为一致。
   - 降级到 Brep 的 Apply 需保证"新 Brep + metadata replay"在同一 Undo record 内，用户一次
     Ctrl+Z 全回滚。

8. **Warning 语义扩充**
   - 复用 Wave 1/2 warning code，新增曲面相关：
     - `DESCRIPTOR_FULL_LARGE`（`Full` 模式下 U×V > 10000 时）
     - `SURFACE_CLOSURE_SEAM_INCONSISTENT`
     - `UNDERLYING_SURFACE_FALLBACK`
   - 继续禁止含义模糊的总括 warning。

9. **Live Smoke CLI slug 独占**
   - slug：`geometry-edit-surface-smoke-test`（全仓唯一）。
   - 注册入口：`Project_Test/260423_TEST_geometry-edit-surface-lane/DeveloperCommandHandler.GeometryEditSurfaceSmokeTest.cs`。
   - Rhino 命令：`_McpGeometryEditSurfaceSmoke`，由本期新增
     `McpGeometryEditSurfaceSmokeCommand` 注册。

10. **对前两期的兼容性**
    - Wave 1 的曲线 Tool 行为完全不变；Wave 1 集成测试无需修改即应通过。
    - Wave 2 的曲线 DerivedOperation / StrategyResolver 分支行为完全不变；Wave 2 集成测试无需
      修改即应通过。
    - `GetEditableGeometryDescriptorRequest` 新增字段 `Detail`：缺省时按 Wave 1 行为（曲线
      Full）返回，不破坏已有客户端调用。

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_geometry-edit-surface-lane.md`
- `Project_Test/260423_TEST_geometry-edit-surface-lane/`
- `Project_Test/260423_TEST_geometry-edit-surface-lane/DeveloperCommandHandler.GeometryEditSurfaceSmokeTest.cs`
- `Project_Test/260423_TEST_geometry-edit-surface-lane/surface/`

**新增（Tools）**

- `src/MCP_Rhino.Server/Tools/Geometry/Edit/PreviewEditSurfaceGeometryTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Edit/ApplyEditSurfaceGeometryTool.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/DescriptorDetail.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointSelectorKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceEditSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfacePointSelectorSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceControlPointGridSnapshot.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceEditDerivedOperationSpec.cs`

**新增（Contracts）**

- `src/MCP_Rhino.Server/Contracts/Requests/PreviewEditSurfaceGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyEditSurfaceGeometryRequest.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryEditSurfaceSmokeCommand.cs`

**修改**

- `src/MCP_Rhino.Server/Tools/Geometry/Edit/GetEditableGeometryDescriptorTool.cs`
  （新增可选 `detail` 参数）
- `src/MCP_Rhino.Server/Contracts/Requests/GetEditableGeometryDescriptorRequest.cs`
  （新增 `Detail` 字段）
- `src/MCP_Rhino.Server/Contracts/Responses/EditableGeometryDescriptorResponse.cs`
  （新增 `SurfaceStructure` / `SurfaceControlPoints` 可选字段）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditPreviewResponse.cs`
  （新增 `SurfaceControlPointGridSnapshot` 可选字段）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryEditApplyResponse.cs`
  （新增 `SurfaceControlPointGridSnapshot` 可选字段）
- `src/MCP_Rhino.Server/Domain/Enums/EditableGeometryKind.cs`
  （启用 `Surface` 取值）
- `src/MCP_Rhino.Server/Domain/Models/EditableGeometryDescriptor.cs`
  （扩 `SurfaceStructure` 字段）
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveEditableGeometryReader.cs`
  （扩 `detail` 参数）
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeometryReconstructor.cs`
  （新增曲面重建方法）
- `src/MCP_Rhino.Server/Application/Services/Edit/RhinoEditableGeometryDescriptorService.cs`
- `src/MCP_Rhino.Server/Application/Services/Edit/RhinoGeometryReconstructionService.cs`
- `src/MCP_Rhino.Server/Application/Services/Edit/RhinoGeometryEditStrategyService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoEditableGeometryReader.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryReconstructor.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/GeometryEditStrategyResolver.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/GeometryEditValidator.cs`

**复用（不改签名）**

- Wave 1 的 `PreviewEditCurveGeometryTool` / `ApplyEditCurveGeometryTool`
- `LiveRhinoGeometryMetadataOperator`
- `GeometryModificationSkill`（仍作为 ExactTransform 底座）
- `ILiveRhinoDocumentAccessor`

## 使用方式

场景 A — 读取曲面 Summary descriptor（默认）：

```
GetEditableGeometryDescriptor(filePath, objectId)
→ {Kind:"Surface", SurfaceKind:"NurbsSurface",
   SurfaceStructure:{ DegreeU:3, DegreeV:3, CountU:10, CountV:8,
                      IsClosedU:false, IsClosedV:false,
                      IsPeriodicU:false, IsPeriodicV:false,
                      Bbox:{Min:..., Max:...} },
   MetadataSummary:{...},
   SupportKind:"Supported",
   Warnings:[]}
```

场景 B — 读取曲面 Full descriptor（显式请求完整控制点网格）：

```
GetEditableGeometryDescriptor(filePath, objectId, detail="Full")
→ {...Summary 字段..., SurfaceControlPoints:[...CountU*CountV 个点...],
   Warnings:[]}
   # U×V > 10000 时追加 "DESCRIPTOR_FULL_LARGE"
```

场景 C — 曲面 DirectOverride（用户给完整新网格）：

```
PreviewEditSurfaceGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DirectOverride", Layout:"Flat" },
    Points:[... CountU*CountV 个点 ...] })
→ {Strategy:"ReconstructFromControlPoints",
   SurfaceControlPointGridSnapshot:{ CountU:10, CountV:8, SampleCorners:[...] },
   Warnings:[]}
ApplyEditSurfaceGeometry(...) → UndoRecordName:"MCP:EditSurfaceGeometry"
```

场景 D — 曲面 "围绕中心 uniform scale 1.1 倍"（resolver 选 `ExactTransform`）：

```
PreviewEditSurfaceGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"ScaleAboutCentroid",
                DerivedParameters:{ ScaleX:1.1, ScaleY:1.1, ScaleZ:1.1 } },
    PointSelector:{ Kind:"All" } })
→ {Strategy:"ExactTransform",
   DerivedOperationApplied:{ Kind:"ScaleAboutCentroid", CentroidWorld:{...},
                             ScaleX:1.1, ScaleY:1.1, ScaleZ:1.1 }}
```

场景 E — 曲面 "把中间一小块控制点沿曲面法向抬 50"（resolver 选 `ReconstructFromControlPoints`）：

```
PreviewEditSurfaceGeometry(filePath, objectId,
  editSpec={
    Operation:{ Kind:"DerivedOperation", DerivedKind:"OffsetAlongNormal",
                DerivedParameters:{ Distance:50 } },
    PointSelector:{ Kind:"UvRange",
                    UStart:3, UEnd:5, VStart:2, VEnd:4 } })
→ {Strategy:"ReconstructFromControlPoints",
   ResolvedPointIndices:[...被选的扁平索引...]}
```

场景 F — 选中一个 untrimmed single-face Brep（降级）：

```
GetEditableGeometryDescriptor(filePath, objectId)
→ {Kind:"Surface", SurfaceKind:"NurbsSurface",
   SurfaceStructure:{...}, SupportKind:"Supported",
   Warnings:["UNDERLYING_SURFACE_FALLBACK"]}
```

## 验收标准

构建 / smoke：

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过。
- `dotnet run --project src/MCP_Rhino.Server -- geometry-edit-surface-smoke-test Runtime_Test/MCP_rhino_test.3dm`
  在非 Rhino Plugin 环境下返回明确的 `LIVE_RHINO_REQUIRED`。
- 在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm` 后，运行 `_McpGeometryEditSurfaceSmoke`：
  - 对至少一个 `PlaneSurface`、一个 `NurbsSurface`、一个 untrimmed single-face Brep 各跑一轮
    完整链路：
    - Summary descriptor → Full descriptor → Preview(DirectOverride) → Apply(DirectOverride)
    - Preview(DerivedOperation / `All` / Translate) → Apply（预期 `ExactTransform`）
    - Preview(DerivedOperation / `UvRange` / OffsetAlongNormal) → Apply（预期
      `ReconstructFromControlPoints`）
  - 每个分支 `Success=true`；Preview 阶段 `doc.Objects.Count` / `doc.Strings.Count` 前后无变化；
    Preview 阶段 Rhino Undo History 无新条目；Apply 阶段恰好新增 1 条
    `MCP:EditSurfaceGeometry`。
  - 用户在 Rhino 中 Ctrl+Z 一次即可同时撤销几何替换与 metadata replay。
  - 图层、颜色、名称、user strings 在 Apply 后与 snapshot 完全一致。
  - untrimmed single-face Brep 降级路径：Apply 后对象仍是 Brep（`RhinoObject.Geometry is Brep`），
    不因重建而变成裸 Surface。
- Wave 1 `_McpGeometryEditDescriptorSmoke` 与 Wave 2 `_McpGeometryEditDerivedSmoke` 在本期合
  入后继续通过，无回归。

LLM 实战 dry-run：

- 在 MCP Client 侧以自然语言发起代表性任务：
  - "读一下这个曲面结构"（期望默认 Summary，LLM 不必要求 Full）
  - "把这个曲面往上拉一点，整体"（TranslateByVector / All → ExactTransform）
  - "把曲面中间一块沿法向下凹 20"（OffsetAlongNormal / UvRange → reconstruction）
  - "把这个曲面围绕自己中心放大 1.1 倍"（uniform ScaleAboutCentroid / All → ExactTransform）
  - "我选中的是一个 polysurface，你来编辑它"（应返回 `EDITABLE_KIND_UNSUPPORTED`，不假装处理）
- 记录：
  - LLM 对 `Summary` vs `Full` 的使用偏好；若 LLM 无差别一直要 Full，EXET 须标记并评估是否
    收紧默认值。
  - LLM 是否正确放弃对 polysurface / trimmed Brep / mesh 的编辑尝试。

边界用例：

- 对象是 polysurface / trimmed Brep / mesh / subd → `EDITABLE_KIND_UNSUPPORTED`。
- Brep 符合 single-face 但 OuterLoop 不匹配 UnderlyingSurface 边界（实际被 trim） →
  `EDITABLE_KIND_UNSUPPORTED`，不静默降级。
- DirectOverride 点数与 `CountU*CountV` 不一致 → `EDIT_POINT_COUNT_MISMATCH`。
- DirectOverride `Layout="Grid"` 但传扁平数组或反之 → `EDIT_POINT_LAYOUT_MISMATCH`。
- `ScaleAboutCentroid` ScaleX = 0 → `SCALE_FACTOR_INVALID`。
- `OffsetAlongNormal` 某点曲面法向退化 → 该点不偏移 + warning `OFFSET_NORMAL_DEGENERATE`。
- 重建后 Surface `IsValid=false` → Preview 返回 `RECONSTRUCTION_INVALID`，Apply 拒绝落盘。
- Closed/periodic 方向 seam 不一致 → warning `SURFACE_CLOSURE_SEAM_INCONSISTENT`。
- `Full` 模式 U×V > 10000 → warning `DESCRIPTOR_FULL_LARGE`（仍返回数据，不硬失败）。

## 风险与回退方案

风险：

- **Descriptor Full 体量失控**：高密度 `NurbsSurface`（50×50 以上）的 Full descriptor 体量大，
  token 成本显著；若客户端默认总请求 Full，会迅速吞掉 context window。
- **Single-face Brep 降级判定误判**：OuterLoop 与 UnderlyingSurface 边界比较依赖模型容差，
  不同文档容差差异可能让"实际被 trim 但几乎对齐"的 Brep 被误识别为 untrimmed；反之亦然。
- **Closed / periodic seam 对齐**：服务端按元信息对 seam 做统一处理，但 RhinoCommon 的
  periodic surface 重建在极端输入下可能产生不符预期的参数化。
- **LLM 误把 polysurface 当 surface 处理**：首次尝试失败后 LLM 是否正确放弃、是否进一步
  `Brep.Faces` 拆分并按 face-by-face 改、都是未知数。
- **Metadata 白名单不足以覆盖曲面**：沿用曲线白名单有可能遗漏曲面独有的有效字段（例如某些
  display attribute），通过 `METADATA_FIELDS_DROPPED` 可见但仍需实战验证。

缓解：

- 默认 `Summary`，`Full` 必须显式请求；`Full` 且 U×V > 10000 直接 warning。
- Single-face 降级判定加上模型容差明确回退条件，容差在 EXET 测试记录里显式列出。
- Seam 与 periodicity 重建在 smoke 覆盖 closed 与 periodic 两类 NurbsSurface。
- `EDITABLE_KIND_UNSUPPORTED` 携带具体原因字符串（"polysurface has N faces" /
  "brep face is trimmed" 等），便于 LLM 规划下一步。
- Metadata 白名单差异 warning 在 smoke 中主动抽样验证，且 EXET 要求标注每个 Surface 对象
  的 dropped fields（若存在）。

回退方案：

- 若曲面 Tool 整体不稳定：
  - 可单独下线 2 个曲面 Tool 的 `[McpServerTool]` 标注（保留 Service 代码），不影响 Wave 1/2
    曲线能力。
- 若 Strategy resolver 曲面分支误路由：
  - 可临时回退曲面分支为"全部走 reconstruction"（ExactTransform 仅在曲线上启用），等待后续
    补丁重新启用。
- 若 single-face Brep 降级存在系统性问题：
  - 可临时禁用降级，全部走 `EDITABLE_KIND_UNSUPPORTED`，让调用方自行
    `Brep.Faces[0].ToNurbsSurface()` 再调编辑；不影响裸 Surface 能力。
- 整体 `git revert` 仍可作为兜底；本期不改 Wave 1/2 的 Tool 接口与公共 Service 签名，回退
  面可控。

## 后续扩展方向

- 收敛评估：
  - 若三 Wave 完成后"descriptor-first"整体适配 LLM 的程度达到预期，再抽
    `GeometryReconstructionSkill`，承载"读 descriptor → 推点 → 预览 → 应用"的固定多轮流程。
  - 评估是否把 `Agents/` 层的"编辑规划 Agent" 纳入，读 descriptor 后自动给出多轮 preview 候选。
- 对象矩阵纵向扩展：
  - 单独 PLAN 评估 **trimmed `BrepFace`** 重建（trim 信息 replay 是一整块独立复杂度）。
  - 单独 PLAN 评估 `SubD` / `Mesh` 的 descriptor-first 形态；二者与 NURBS 差异显著，不宜混
    在本 lane。
  - `Extrusion` 可考虑内部自动 `ToBrep()` 再复用本 lane，或单独立项。
- Strategy resolver 扩展：
  - 若后续加入"绕任意轴 Rotate / 任意平面 Mirror"等 derived 操作且能对应 RhinoCommon 原生
    `Transform`，再扩 ExactTransform 分支。
- Descriptor 性能扩展：
  - 若 `Full` 体量成为常态瓶颈，评估引入分页 (page by U-strip) 或二进制压缩；该改动跨 Wave
    1/2/3，需单独 PLAN。
