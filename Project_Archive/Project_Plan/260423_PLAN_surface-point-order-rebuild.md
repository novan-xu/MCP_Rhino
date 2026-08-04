# 260423_PLAN_surface-point-order-rebuild

## 背景

`260423_PLAN_geometry-edit-molecular-architecture`（总纲）在 §4 标识本 PLAN 为子 PLAN E。原独立栈版本 `Project_Archive/260423_PLAN_surface-point-order-redefinition.md` 在评审中发现与 Wave 1/2/3（现子 PLAN A/B/C/D）的分子严重重叠，此版本本**薄化**重写：

- 只保留 Plan 4 的 4 个专属分子（参考边识别、LCS 构造、点序规范化、边界驱动重建）
- 复用子 PLAN A 的描述器、frame 采样、metadata、Brep 降级（分子 1 / 3 / 6 / 8）
- 复用子 PLAN B 的 Mutation 边界（分子 7）
- **不复用** 子 PLAN B/D 的控制点重建器（分子 5），因为边界驱动重建出口（4PointSurface / BoundarySurface）与控制点重建出口不同
- 硬依赖为子 PLAN A + B；子 PLAN D 不是本 PLAN 的技术前置，仅作为曲面 edit 复合能力的顺序建议
- 引入一个编排 Skill `SurfacePointOrderRebuildSkill` 作为内部固定流程编排层；MCP 可见入口仍以 Tool 为准

样本 GH 文件 `Runtime_Test/REG_Redefine Point Order.gh` 已离线解析，关键管线结构在原 PLAN 中详述（不在此重复）。总体业务意图：

1. 识别曲面底部（或指定）参考边
2. 基于参考边 + 面法向 + centroid 构造局部坐标系
3. 把边界顶点投影到 LCS，按方向 + 起点锚定 + 极角排序生成规范化有序点列
4. 按点数路由：4 点走 `4PointSurface`；平面单外环多边形走 `BoundarySurface`
5. 重建后通过 Mutation 边界替换原对象，保留属性

本 PLAN 不新建描述器、frame 采样、metadata 白名单、Brep 降级、Undo 与属性回放——这些都在子 PLAN A/B 已经落地。

## 目标

- 落地 Plan 4 的 4 个专属分子：
  - `IBoundaryReferenceCurveAnalyzer`（分子 E1）
  - `ISurfaceLocalCoordinateSystemBuilder`（分子 E2，基于分子 3 frame 采样）
  - `ISurfaceBoundaryPointOrderer`（分子 E3）
  - `IBoundaryDrivenSurfaceReconstructor`（分子 E4，两条出口：4Point / BoundarySurface）
- 对外暴露 3 个 MCP Tool：
  - `InspectSurfaceRebuildDescriptor`（Preview 前的只读"排序计划"工具）
  - `PreviewRedefineSurfacePointOrder`
  - `ApplyRedefineSurfacePointOrder`
- 新增 1 个 Skill：`SurfacePointOrderRebuildSkill`（编排三个 Tool 形成固定多轮流程，供 Agent / 内部编排复用；不单独视为 MCP 可见入口）
- 对象矩阵：
  - `PlaneSurface` / `NurbsSurface` / untrimmed single-face Brep（四角面路径）
  - 单外环、无内环、可稳定提取外边界顶点的 planar single-face Brep（多边面路径）
- 保留原对象 Layer / Name / Color / UserStrings 等（复用分子 6 白名单 + 分子 7 Mutation 边界）
- 不依赖 `EleFront` / `LunchBox` / `Pufferfish` 即可运行
- Apply 单次 Tool 调用 = 单条 Undo record（`MCP:RedefineSurfacePointOrder`）；批量 `ConfirmedObjectIds` 在同一调用内共享这条 Undo record，成功对象可由用户一次 Ctrl+Z 全部回滚

**明确不纳入：**

- 带内环的多边面重建（外环 + 内环）——留作后续扩展
- 非平面多边面重建——留作后续扩展
- Mesh / SubD / block instance / polysurface
- 任意 `.gh` 自动转 Skill 的通用框架
- 极端退化拓扑的完整兜底；优先规则清晰、行为稳定、错误可解释
- 修改子 PLAN A/B/C/D 的既有分子接口

## 架构归属

本 PLAN 实现的分子：**Plan 4 专属 E1 / E2 / E3 / E4**。复用子 PLAN A 的 1 / 3 / 6 / 8；复用子 PLAN B 的 7。

- **Tools/Geometry/Rebuild/**（新增子目录，避免与 `Tools/Geometry/Edit/` 混淆）
  - `InspectSurfaceRebuildDescriptorTool.cs`
  - `PreviewRedefineSurfacePointOrderTool.cs`
  - `ApplyRedefineSurfacePointOrderTool.cs`

- **Skills/Modeling/**（新增）
  - `SurfacePointOrderRebuildSkill.cs`

- **Application/Interfaces/**（新增）
  - `IBoundaryReferenceCurveAnalyzer.cs`
  - `ISurfaceLocalCoordinateSystemBuilder.cs`
  - `ISurfaceBoundaryPointOrderer.cs`
  - `IBoundaryDrivenSurfaceReconstructor.cs`
  - `ISurfaceRebuildOrchestrator.cs`（Tool 与 Skill 共用的编排入口）

- **Application/Services/Rebuild/**（新增）
  - `SurfaceRebuildOrchestrator.cs`
  - `SurfaceBoundaryPointOrderer.cs`（分子 E3 纯算法实现；不放入 Live 层）

- **Infrastructure/Rhino/Live/**（新增）
  - `LiveBoundaryReferenceCurveAnalyzer.cs`
  - `LiveSurfaceLocalCoordinateSystemBuilder.cs`
  - `LiveBoundaryDrivenSurfaceReconstructor.cs`（两条出口）
  - `Infrastructure/Plugin/McpSurfacePointOrderRebuildSmokeCommand.cs`

- **Domain/Enums/**（新增）
  - `SurfacePointOrderGuideMode.cs`：`Auto` / `ReferenceCurve` / `ReferenceEdgeIndex`
  - `SurfacePointOrderDirection.cs`：`Clockwise` / `CounterClockwise`
  - `SurfacePointOrderStartAnchorMode.cs`：`ReferenceStart` / `MinAngle` / `ClosestToOrigin`
  - `SurfaceRebuildRouteKind.cs`：`FourPoint` / `BoundarySurface`
  - `SurfaceTopologyKind.cs`：`Quad` / `NGon` / `Unknown`

- **Domain/Models/**（新增）
  - `SurfaceRebuildDescriptor.cs`：Inspect Tool 专用；包含 `CandidateReferenceCurves`、`SuggestedReferenceCurve`、`OuterBoundaryLoop`、`LocalFrame` 建议、`SuggestedDirection`、`SuggestedStartAnchor`、`SuggestedRoute`、`Warnings`
  - `SurfaceReferenceCurveSpec.cs`：`ObjectId?` / `EdgeIndex?` / `Midpoint` / `StartPoint` / `EndPoint` / `Length`
  - `SurfaceBoundaryLoop.cs`：`Vertices3d[]` / `IsClosed` / `IsPlanar` / `HasInnerLoops` / `Topology`
  - `SurfaceLocalCoordinateSystem.cs`：`Origin` / `XAxis` / `YAxis` / `Normal` / `IsDegenerate`
  - `OrderedSurfacePoint.cs`：`OriginalIndex` / `SortKey` / `Position3d` / `Position2dInLcs`
  - `SurfacePointOrderPlan.cs`：`Points: OrderedSurfacePoint[]` / `Direction` / `StartAnchorIndex` / `ReferenceCurve: SurfaceReferenceCurveSpec` / `LocalFrame: SurfaceLocalCoordinateSystem` / `Route: SurfaceRebuildRouteKind` / `Topology: SurfaceTopologyKind`
  - `SurfaceRebuildSpec.cs`：输入参数（`GuideMode` / `Direction` / `ReferenceCurveObjectId?` / `ReferenceEdgeIndex?` / `StartAnchorMode?`）
  - `BoundaryRebuildResult.cs`：重建摘要（新 `GeometryBase` 对象 + `Route` + warning 列表）

- **Contracts/Requests/**（新增）
  - `InspectSurfaceRebuildDescriptorRequest.cs`：`FilePath` / `ConfirmedObjectIds` / 可选 `ReferenceCurveObjectId` / 可选 `ReferenceEdgeIndex`
  - `PreviewRedefineSurfacePointOrderRequest.cs`：`FilePath` / `ConfirmedObjectIds` / `Spec: SurfaceRebuildSpec`
  - `ApplyRedefineSurfacePointOrderRequest.cs`：同上 + 可选 `ReplaceOriginal: bool = true`

- **Contracts/Responses/**（新增）
  - `SurfaceRebuildDescriptorResponse.cs`：`SurfaceRebuildDescriptor[]`（批量）
  - `SurfacePointOrderPreviewResponse.cs`：每对象 `{ ObjectId, Plan: SurfacePointOrderPlan, PreviewSummary, Warnings, Skipped, SkipReason }`
  - `SurfacePointOrderApplyResponse.cs`：每对象 `{ OriginalObjectId, ObjectId, UndoRecordName, MetadataDropped, Warnings, Skipped, SkipReason }`

- **Server/**
  - `DependencyInjection.cs`（修改）：注册 E1–E4 接口 + orchestrator
  - `AgentRegistration.cs`（修改）：注册 `SurfacePointOrderRebuildSkill`

- **复用（不改）**
  - 子 PLAN A：`IEditableGeometryDescriptorService` / `IGeometryFrameSampler` / `IGeometryMetadataOperator` / `IBrepSurfaceDowngrader`
  - 子 PLAN B：`IGeometryMutationService`
  - 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 全部不动

## 关键设计

### §1 Skill 与 Tool 的分工

- **Tool 层**：
  - `InspectSurfaceRebuildDescriptorTool` → 只读；返回 `SurfaceRebuildDescriptor` 列表，LLM 可在不 Preview / Apply 的情况下先评估"哪些对象能自动识别，哪些要显式补参考边"
  - `PreviewRedefineSurfacePointOrderTool` → 只读；返回每对象的 `SurfacePointOrderPlan` 与重建摘要；**不写文档**
  - `ApplyRedefineSurfacePointOrderTool` → 写文档；每次 Tool 调用一条 Undo record，响应仍按对象返回独立结果
- **Skill 层**（`SurfacePointOrderRebuildSkill`）：
  - 把 "Inspect → Preview → Apply" 串成固定流程
  - 对批量对象：先用 Inspect 过滤出"自动可识别"与"需显式补参考边"两类；对前者自动走 Preview/Apply；对后者 Skill 返回 `NEEDS_EXPLICIT_REFERENCE` 让客户端决定
  - Skill 本身不重复实现分子，只编排 orchestrator 调用
- 三个 Tool 是 **MCP 可见入口**；Skill 仅注册到 `AgentRegistration` 供内部编排 / 后续 Agent 复用。若未来需要直接对话式调用，需另补 `[McpServerTool]` wrapper，而不是把 Skill 注册等同为 MCP Tool

### §2 分子 E1：参考边识别器

`IBoundaryReferenceCurveAnalyzer.Analyze(rhinoObject)` 输出：

- `Candidates: SurfaceReferenceCurveSpec[]`：可能的参考边列表，每条带长度 / 中点 / 方向 / 在局部 -Z（或最低标高方向）上的"优越度"评分
- `Suggested: SurfaceReferenceCurveSpec?`：服务端推荐首选；歧义时 `null`
- `OuterBoundaryLoop: SurfaceBoundaryLoop`：排序与多边面重建使用的唯一边界输入；来自对象真实 outer loop，而不是子 PLAN A 的控制点网格 descriptor
- `Ambiguity: { Level: None | Low | High, Reason: string }`
- `Warnings: string[]`

识别优先级（在 orchestrator 中统一执行，不在分子内混入）：

1. 用户显式指定 `ReferenceCurveObjectId` → 直接用
2. 用户显式指定 `ReferenceEdgeIndex` → 直接用
3. 自动识别：
   - 取面的外边界所有边
   - 在面的局部 -Z 方向（用面法向作基准）上"最低"的边
   - 若多条等高：取长度最长者
   - 若仍等高等长：标记歧义

硬错误（由 orchestrator 判定）：

- `REFERENCE_CURVE_AMBIGUOUS`：自动识别歧义且用户未显式指定
- `REFERENCE_CURVE_REQUIRED`：对象不是四边 / 多边曲面，无法识别参考边

### §3 分子 E2：LCS 构造器

`ISurfaceLocalCoordinateSystemBuilder.Build(referenceCurve, rhinoObject)` 输出 `SurfaceLocalCoordinateSystem`：

- `Origin`：参考边中点（或用户选的起点）
- `XAxis`：沿参考边切线方向归一化
- `Normal`：面在 `Origin` 处的法向（通过 `Surface.NormalAt(u, v)`；内部调用分子 3 frame 采样器以复用实现）
- `YAxis`：`Normal × XAxis` 归一化
- `IsDegenerate: bool`：若 XAxis / YAxis / Normal 任一退化（长度 < 容差）

硬错误：`DEGENERATE_LOCAL_FRAME`（由 orchestrator 判定）

### §4 分子 E3：点序规范化器

`ISurfaceBoundaryPointOrderer.Order(boundaryLoop, lcs, referenceCurve, direction, startAnchorMode)` 输出 `SurfacePointOrderPlan`：

- 输入统一来自 `SurfaceBoundaryLoop.Vertices3d`
- 把 `boundaryLoop.Vertices3d`（边界顶点）投影到 LCS XY 平面
- 计算排序 key：
  - 首键：相对 `Origin` 的极角（按 `Direction` 顺 / 逆时针）
  - 次键：沿参考边的投影参数（parameter on reference curve）
  - 末键：与参考边的距离（打破角度接近时的歧义）
- 确定起点：
  - `ReferenceStart` → 最靠近参考边起点的排序点
  - `MinAngle` → 极角最小者
  - `ClosestToOrigin` → 与 LCS Origin 距离最近者
- 循环展开：环形点列旋转到统一起点

硬错误：

- `INSUFFICIENT_ORDERABLE_POINTS`：可用顶点 < 3
- `SELF_INTERSECTING_BOUNDARY`：投影后边界有自交

warning：`POINT_ORDER_UNCHANGED`（排序结果与原始点列一致，本次 Apply 无实际效果）

此分子是**纯算法**（无 RhinoCommon 直调，只用 Point3d / Vector3d 数学），实现放在 `Application/Services/Rebuild/SurfaceBoundaryPointOrderer.cs`，便于单元测试；Live 层只负责把 RhinoObject / Brep / Surface 提取成该算法需要的纯模型。

### §5 分子 E4：边界驱动重建器（两条出口）

`IBoundaryDrivenSurfaceReconstructor.Reconstruct(orderedPoints, route, boundaryLoop)` 输出 `BoundaryRebuildResult`：

- `route == FourPoint`：
  - 点数必须 == 4；否则 `BOUNDARY_SURFACE_FAILED`（内部逻辑错误，不应到此）
  - `NurbsSurface.CreateFromCorners(p0, p1, p2, p3)` → 新 `Surface`
  - `IsValid` 校验，失败 → warning `RECONSTRUCTION_INVALID`（Preview）/ 硬错误（Apply）
- `route == BoundarySurface`：
  - 前置条件：`boundaryLoop.IsPlanar == true`、`boundaryLoop.HasInnerLoops == false`、`orderedPoints.Count > 4`
  - 用 ordered points 构造闭合 `Polyline`（`Points[0] == Points[^1]`）
  - 仅采用 `Brep.CreatePlanarBreps(polyline.ToNurbsCurve(), tol)`；首期不在 EXET 阶段临时决定第二条非平面重建路径
  - 若成功但返回多张面：硬错误 `BOUNDARY_SURFACE_FAILED` + message 描述原因
  - 若成功且单张面：返回 `Brep`
  - 若 `boundaryLoop.IsPlanar == false` → `BOUNDARY_SURFACE_NON_PLANAR_UNSUPPORTED`
  - 若失败 → `BOUNDARY_SURFACE_FAILED`

路由决策（orchestrator 而非 reconstructor）：

- `orderedPoints.Count == 4` 且 `Topology == Quad` → `FourPoint`
- `orderedPoints.Count > 4` 或 `Topology == NGon` → `BoundarySurface`

### §6 Preview / Apply 管线（Orchestrator 视角）

Preview 阶段每对象独立执行，失败不影响批量其他对象；Apply 阶段先逐对象生成可应用结果，再在一次 `ApplyRedefineSurfacePointOrder` 调用的一条 Undo record 内写入所有成功对象。单个对象失败会进入该对象的 `Skipped/SkipReason` 或失败结果，不阻断其它成功对象；同一调用内的成功对象共享一次 Ctrl+Z 回滚。

1. descriptor（分子 1；含分子 8 Brep 降级）
2. 参考边识别（分子 E1 + 用户覆盖）
3. LCS 构造（分子 E2）
4. 抽取 outer loop（由分子 E1 从对象真实边界返回 `SurfaceBoundaryLoop`；明确不使用子 PLAN A 的控制点网格作为排序输入）
5. 点序规范化（分子 E3）
6. 路由判定 → 重建（分子 E4）
7. IsValid 校验
8. **Apply 阶段独有**：
   - metadata snapshot（分子 6）
   - mutation 边界（分子 7）：`ReplaceWithMetadata(objectId, newGeometry, snapshot, "MCP:RedefineSurfacePointOrder")`
   - 若原对象是 untrimmed single-face Brep 且新几何是 Surface → 经分子 8 `Rebuild` 回包为 Brep 再替换（保持对象类型稳定）

Preview 不调用第 8 步，只返回 `SurfacePointOrderPlan` + 重建摘要。

### §7 请求模型（首期只用显式 ObjectId）

- `ConfirmedObjectIds: Guid[]`：显式列出要处理的对象
- 首期**不**支持 `LayerQueries` / `ConfirmedLayerFullPaths` / `ObjectTypes` / `UserAttributeConditions` 筛查入口；后续需要再扩
- `GuideMode`：
  - `Auto`：完全自动识别参考边
  - `ReferenceCurve`：调用方给 `ReferenceCurveObjectId`
  - `ReferenceEdgeIndex`：调用方给 `ReferenceEdgeIndex`
- `Direction`：`Clockwise` / `CounterClockwise`
- `StartAnchorMode`：默认 `ReferenceStart`

### §8 错误与 warning 码

硬错误：

- 继承子 PLAN A/B：`LIVE_RHINO_REQUIRED` / `OBJECT_NOT_FOUND` / `EDITABLE_KIND_UNSUPPORTED` / `APPLY_FAILED_ROLLED_BACK`
- 本 PLAN 新增：
  - `REFERENCE_CURVE_AMBIGUOUS`
  - `REFERENCE_CURVE_REQUIRED`
  - `DEGENERATE_LOCAL_FRAME`
  - `INSUFFICIENT_ORDERABLE_POINTS`
  - `SELF_INTERSECTING_BOUNDARY`
  - `UNSUPPORTED_TOPOLOGY`
  - `BOUNDARY_SURFACE_NON_PLANAR_UNSUPPORTED`
  - `BOUNDARY_SURFACE_FAILED`
  - `RECONSTRUCTION_INVALID`（仅 Apply；Preview 降级 warning）

warning：

- 继承：`UNDERLYING_SURFACE_FALLBACK` / `METADATA_FIELDS_DROPPED`
- 本 PLAN 新增：
  - `POINT_ORDER_UNCHANGED`
  - `REFERENCE_CURVE_AUTO_LOW_CONFIDENCE`（自动识别置信度低；仍执行但建议显式指定）

原 PLAN 的 `ATTRIBUTE_COPY_PARTIAL` 由分子 6 的 `METADATA_FIELDS_DROPPED` 取代，不再单独定义。

### §9 Live Smoke CLI slug 独占

- slug：`surface-point-order-rebuild-smoke-test`
- 注册入口：`Project_Test/260423_TEST_surface-point-order-rebuild/DeveloperCommandHandler.SurfacePointOrderRebuildSmokeTest.cs`
- Rhino 命令：`_McpSurfacePointOrderRebuildSmoke`

### §10 Skill 内部形态

`SurfacePointOrderRebuildSkill` 作为内部编排层提供一个固定流程入口（与既有 Skills 风格一致），但不等同于额外 MCP Tool：

- 输入：`FilePath` + `ObjectIds` + `Direction` + 可选批量参考边覆盖 map
- 内部流：
  1. 对每个 ObjectId 调 `InspectSurfaceRebuildDescriptor`
  2. 拆分为"自动 OK"与"需显式参考边"两类
  3. 对自动 OK 类批量 `Preview`
  4. Preview 全部 success 后批量 `Apply`（一次 Tool 调用共享一条 Undo record）
  5. 对需显式参考边类返回给调用方，附 `NEEDS_EXPLICIT_REFERENCE` 状态码 + 候选参考边列表
- 输出：每对象的最终状态（`Applied` / `NeedsExplicitReference` / `Failed`）+ 汇总

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_surface-point-order-rebuild.md`
- `Project_Test/260423_TEST_surface-point-order-rebuild/`
- `Project_Test/260423_TEST_surface-point-order-rebuild/DeveloperCommandHandler.SurfacePointOrderRebuildSmokeTest.cs`
- `Project_Test/260423_TEST_surface-point-order-rebuild/surface/`（fixture + expected，含四角面、planar single-face Brep 多边面、歧义参考边三类）

**新增（Tools）**

- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/InspectSurfaceRebuildDescriptorTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/PreviewRedefineSurfacePointOrderTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/ApplyRedefineSurfacePointOrderTool.cs`

**新增（Skills）**

- `src/MCP_Rhino.Server/Skills/Modeling/SurfacePointOrderRebuildSkill.cs`

**新增（Application）**

- `src/MCP_Rhino.Server/Application/Interfaces/IBoundaryReferenceCurveAnalyzer.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceLocalCoordinateSystemBuilder.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceBoundaryPointOrderer.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IBoundaryDrivenSurfaceReconstructor.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceRebuildOrchestrator.cs`
- `src/MCP_Rhino.Server/Application/Services/Rebuild/SurfaceRebuildOrchestrator.cs`
- `src/MCP_Rhino.Server/Application/Services/Rebuild/SurfaceBoundaryPointOrderer.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBoundaryReferenceCurveAnalyzer.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceLocalCoordinateSystemBuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBoundaryDrivenSurfaceReconstructor.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpSurfacePointOrderRebuildSmokeCommand.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointOrderGuideMode.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointOrderDirection.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointOrderStartAnchorMode.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfaceRebuildRouteKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfaceTopologyKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceRebuildDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceReferenceCurveSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceBoundaryLoop.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceLocalCoordinateSystem.cs`
- `src/MCP_Rhino.Server/Domain/Models/OrderedSurfacePoint.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfacePointOrderPlan.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceRebuildSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/BoundaryRebuildResult.cs`

**新增（Contracts）**

- `src/MCP_Rhino.Server/Contracts/Requests/InspectSurfaceRebuildDescriptorRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewRedefineSurfacePointOrderRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyRedefineSurfacePointOrderRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SurfaceRebuildDescriptorResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SurfacePointOrderPreviewResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SurfacePointOrderApplyResponse.cs`

**修改**

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`（注册 E1–E4 + orchestrator）
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`（注册 `SurfacePointOrderRebuildSkill`）

**复用（不改）**

- 子 PLAN A 全部接口与 Live 实现
- 子 PLAN B 的 `IGeometryMutationService`
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` Tool

## 使用方式

### MCP Tool

```
InspectSurfaceRebuildDescriptor(filePath, confirmedObjectIds,
  referenceCurveObjectId?, referenceEdgeIndex?)
```

```
PreviewRedefineSurfacePointOrder(filePath, confirmedObjectIds, spec={
  GuideMode, Direction, ReferenceCurveObjectId?, ReferenceEdgeIndex?, StartAnchorMode? })
```

```
ApplyRedefineSurfacePointOrder(filePath, confirmedObjectIds, spec={...},
  replaceOriginal=true)
```

### Skill（编排入口）

```
SurfacePointOrderRebuildSkill.Run(filePath, objectIds, direction, referenceOverrides?)
```

### 典型场景

- **四角面自动识别**：`GuideMode=Auto` → 自动定底边 → LCS → 排序 → 4Point 重建 → Apply
- **多边面自动识别**：仅限 planar single-face Brep 的单外环路径；Apply 走 `BoundarySurface`
- **批量歧义处理**：先 Skill 批量 Inspect → 拆分"OK"与"歧义"两类；对歧义返回 `NEEDS_EXPLICIT_REFERENCE`，客户端补参考边后再 Apply
- **显式覆盖**：直接调 Tool，给 `ReferenceCurveObjectId`

## 验收标准

### 构建

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过

### 非 Rhino Plugin 环境

- `dotnet run --project src/MCP_Rhino.Server -- surface-point-order-rebuild-smoke-test Runtime_Test/MCP_rhino_test.3dm` 返回 `LIVE_RHINO_REQUIRED`

### Rhino Plugin 环境

在 Rhino 中打开 fixture 文件（含至少一个四角 NurbsSurface、一个 planar single-face Brep 多边面、一个歧义参考边的四角面），运行 `_McpSurfacePointOrderRebuildSmoke`：

- **四角面自动识别**：
  - Inspect → `Suggested` 非空，`Ambiguity.Level = None`
  - Preview → `Route = FourPoint`，`Plan.Points.Count = 4`，`Success = true`
  - Apply → `UndoRecordName = "MCP:RedefineSurfacePointOrder"`，`ObjectId` 与输入一致，对象类型不变（若原为 Brep 则仍 Brep）
  - Ctrl+Z 一次完整回滚（几何 + metadata）
- **多边面自动识别**：
  - Preview → `Route = BoundarySurface`，`Plan.Points.Count > 4`，`Success = true`，且 `OuterBoundaryLoop.IsPlanar = true`
  - Apply 同上，Undo 名一致
- **显式参考边覆盖**：
  - Inspect 与 Preview/Apply 在 `GuideMode = ReferenceCurve` 下一致采用显式参考边
- **歧义参考边**：
  - Inspect → `Ambiguity.Level = High`，`Suggested = null`
  - Preview 未传显式参考边 → `Success = false`，`ErrorCode = "REFERENCE_CURVE_AMBIGUOUS"`
  - 传入显式参考边后 Preview 成功
- **Preview 无副作用**：`doc.Objects.Count` / `doc.Strings.Count` 前后不变，Rhino Undo History 无新增
- **批量 Apply Undo 边界**：同一 `ApplyRedefineSurfacePointOrder` 调用处理多个成功对象时，Rhino Undo History 恰好新增 1 条 `MCP:RedefineSurfacePointOrder`；Ctrl+Z 一次回滚该调用内全部成功对象
- **Apply metadata 保留**：Layer / Name / Color / UserStrings 与 Apply 前一致（以 `MetadataDropped` 为准抽样验证）
- **Apply 后对象类型稳定**：若原为 untrimmed single-face Brep，Apply 后仍是 Brep（经分子 8 回包）
- **不支持对象**：对 polysurface / trimmed Brep / mesh / subd 的对象返回 `EDITABLE_KIND_UNSUPPORTED`（由分子 1 + 8 透传）
- **不依赖第三方插件**：smoke 执行过程中不加载 EleFront / LunchBox / Pufferfish；Rhino 启动时未安装上述插件亦可通过

### 回归

- 子 PLAN A / B 的 smoke 命令在本期合入后继续通过（硬依赖）
- 若当前分支已合入子 PLAN C / D，则其 smoke 命令也必须继续通过；C / D 未合入时不阻塞本 PLAN E
- 既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 行为无变化

## 风险与回退方案

### 风险

- **GH 语义不完全显式**：原 GH 样本的分组名和组件能说明流程，但不能百分之百还原细节；本 PLAN 在 §2–§5 用中文规则替代，可能与 GH 原行为有细微偏差（例如 centroid 权重、极角起始方向）
- **参考边自动识别歧义**：高度接近 / 边长接近 / 法向翻转的对象上自动识别不稳定；靠 `REFERENCE_CURVE_AMBIGUOUS` 硬错误暴露，但歧义判定阈值本身需要实战调参
- **多边面边界自交 / 退化**：BoundarySurface 路径对环顺序与闭合质量敏感；首期已明确收敛到 planar single outer loop，多边面若非平面则直接拒绝而不是在 EXET 阶段临时发明重建规则
- **属性保留范围**：用户若用 EleFront 对照，仍可能发现 RenderMaterial / Group 差异；`METADATA_FIELDS_DROPPED` 暴露但需协商范围

### 缓解

- `InspectSurfaceRebuildDescriptorTool` 强制作为 Preview 前置，排序计划可被 LLM / 用户审阅
- Preview 强制输出 `SurfacePointOrderPlan` 而非仅重建几何
- 参考边 override 是一等输入，三 Tool 均接受
- smoke fixture 必须同时含四角、多边、歧义三类对象
- `Brep.CreatePlanarBreps` 失败时清晰返回 `BOUNDARY_SURFACE_FAILED` + 具体原因（容差 / 返回多面 / 自交）；多边面若非平面则单独返回 `BOUNDARY_SURFACE_NON_PLANAR_UNSUPPORTED`

### 回退方案

- 若自动参考边策略不稳定：临时把默认 `GuideMode` 改为 `ReferenceCurve`（强制用户显式指定），不影响 Inspect / Preview / Apply 的其他路径
- 若多边面路径在首轮实现中不稳定：保留 Inspect + Preview，Apply 先只开放 FourPoint 路径（`Route = BoundarySurface` 时 Apply 硬错误 `BOUNDARY_SURFACE_DISABLED`）
- 若 Skill 编排有系统性问题：保留三个 Tool 对外暴露，临时下线 Skill（`AgentRegistration` 中注释该 Skill 注册，不影响 MCP 可见入口）
- 本 PLAN 为新增文件集（DI / AgentRegistration 仅新增注册），可独立 `git revert`

## 后续扩展方向

- 支持带内环的多边面重建（外环 + 内环分别排序，保留环方向约束）
- "保留原修剪边界" vs "重建为无修剪规范面" 两种模式（本期只做后者）
- 批量对象按 user attribute 自动选取参考边规则（例如"所有 `bottom=true` 的对象用底边"），引入 `UserAttributeConditions` 入口
- 沉淀独立 `GH definition analyzer` 能力（把 `.gh` / `.ghx` 解析成"候选 Skill 规划提示"），作为独立 PLAN
- 若后续需要支持非平面多边面：另起增量 PLAN，明确 `CreateEdgeSurface` 或其他重建规则与验收矩阵，不在本期 EXET 阶段临时决定
