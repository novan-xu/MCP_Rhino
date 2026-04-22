# 260422_PLAN_geometry-advanced-edit-tools

## 背景

`260420_PLAN_geometry-create-modify-tools.md` 已经完成了 Rhino 几何**最基础**的写入能力：
Create（Point/Line/Arc/Surface）、Transform（Translate/Rotate/UniformScale）、Replace、Delete、
EditControlPoints。但 Rhino 的"建模主流操作"远远不止于此 —— 当前 LLM 无法做：

- **进阶变换**：Mirror / NonUniformScale(1D/2D) / Array(Rectangular/Polar/AlongCurve) / Orient（按两组参考几何对位）。
- **倒角圆角**：曲线 Fillet / Chamfer，Brep 边 FilletEdge / ChamferEdge，曲面 BlendSrf，曲线 BlendCrv。
- **偏移**：曲线 Offset、曲面 Offset、Mesh Offset。
- **切割组合**：Trim、Split、Join、Explode、Untrim、Merge（Brep 拼接）。
- **延伸 / 重建**：Extend、Rebuild、ChangeDegree、MatchSrf。
- **变形**：Sweep1 / Sweep2、Bend、Twist、Taper、Flow（沿基准线变形）。

这些能力一旦缺失，LLM 永远只能做"骨架级建模"，无法进入"细化建模"阶段。RhinoCommon v8
已经把所需 API 全部暴露在 `Rhino.Geometry.Curve.*` / `Rhino.Geometry.Brep.*` /
`Rhino.Geometry.NurbsSurface.*` / `Rhino.Geometry.Morphs.*` 命名空间下；Rhino3dm 则只暴露其中
**几何级**部分（Brep boolean / Sweep / SpaceMorph 在 Rhino3dm v8 上不完整）。

按 `MCP_Rhino Architecture.md` 中"mutation 必须走 Live"的硬约束，本批工具**全部 live-only**，
全部跑在 Rhino 进程内的 `RhinoDoc.ActiveDoc` 上，全部一次 Tool 调用 = 一次 Undo record。

## 目标

按四个子域拆分本期范围；每个子域独立 Tool 集合，避免单 Tool 承载过多语义。

- **Transform 进阶（4 + 4 = 8）**：Mirror / NonUniformScale / Array / Orient（各 1 个 Apply +
  1 个 Preview）。
- **Fillet/Chamfer/Blend（5 + 5 = 10）**：FilletCurves / ChamferCurves / FilletBrepEdges /
  BlendCurves / BlendSurfaces（各 1 Apply + 1 Preview）。
- **Offset（3 + 3 = 6）**：OffsetCurves / OffsetSurface / OffsetMesh。
- **Trim/Split/Join/Explode/Untrim/Merge（6 + 6 = 12）**：每个动作 Apply + Preview。
- **Extend/Rebuild/ChangeDegree/MatchSurface（4 + 4 = 8）**。
- **Sweep/Loft/Revolve/Extrude（4 + 4 = 8）**：补完"由曲线生成曲面 / 实体"的工业流。
- **Morph（5 + 5 = 10）**：BendMorph / TwistMorph / TaperMorph / FlowAlongCurve /
  FlowAlongSurface（各 1 Apply + 1 Preview）。

> 单期实现规模偏大，PLAN 文档先一次性把 schema 与归属定下；EXET 阶段按子域分多次落地，
> 每个子域单独进 smoke + Undo 验证。Subdomain 顺序建议：Transform → Fillet/Blend → Offset → Sweep/Loft → Trim/Split/Join → Morph → Extend/Rebuild/MatchSurface。

每条 Apply 都遵循既有 mutation 模板：
- 走 `_documentAccessor.ExecuteWithUndo(filePath, "MCP: <ToolName>", doc => ...)`；
- per-entry try/catch，单条失败不阻断同 Request 其他条目；
- 成功项汇总后 `doc.Views.Redraw()`；
- 复用 `ObjectEditWarning` warning code，不新增 warning code。

每条 Preview 都走 `_documentAccessor.Execute(...)` 只读路径，**不开 Undo record、不修改文档**，
返回"将影响的 ObjectId 列表 + 将生成的几何概要 + warning"。

## 架构归属

- **Tools/Geometry/Transform/**（新增子目录）—— 8 个 Tool。
- **Tools/Geometry/Fillet/**（新增子目录）—— 10 个 Tool。
- **Tools/Geometry/Offset/**（新增子目录）—— 6 个 Tool。
- **Tools/Geometry/Boolean/**（新增子目录，"切割组合"语义）—— 12 个 Tool（Trim/Split/Join/Explode/Untrim/Merge）。
- **Tools/Geometry/Refit/**（新增子目录，"延伸/重建/匹配"语义）—— 8 个 Tool。
- **Tools/Geometry/Loft/**（新增子目录，"由曲线生成曲面"）—— 8 个 Tool。
- **Tools/Geometry/Morph/**（新增子目录）—— 10 个 Tool。

> 子目录按"动作语义"切分而非按"Apply / Preview"切分，方便 Tool 文档聚合。Apply 与 Preview
> Tool 同目录、同 prefix。例如 Tools/Geometry/Transform/MirrorObjectsTool.cs 与
> Tools/Geometry/Transform/PreviewMirrorObjectsTool.cs。

- **Application/Services/** —— 新增 7 个 Service，按子目录一一对应：
  - `RhinoGeometryAdvancedTransformService`
  - `RhinoGeometryFilletBlendService`
  - `RhinoGeometryOffsetService`
  - `RhinoGeometryBooleanEditService`
  - `RhinoGeometryRefitService`
  - `RhinoGeometryLoftService`
  - `RhinoGeometryMorphService`
  - 每个 Service 暴露成对的 `<Action>(...)` 与 `Preview<Action>(...)`，签名风格与
    `RhinoGeometryModificationService.Apply / Preview` 对齐。

- **Application/Interfaces/** —— 仅新增 live 侧接口（offline 不参与本批）：
  - `ILiveAdvancedTransformer`
  - `ILiveFilletBlendOperator`
  - `ILiveOffsetOperator`
  - `ILiveBooleanEditOperator`
  - `ILiveRefitOperator`
  - `ILiveLoftOperator`
  - `ILiveMorphOperator`
  - 每个接口的方法粒度与对应 Service 的 per-entry 操作一致；接口不接触 `RhinoDoc`，由 Service
    解析 ObjectId → `RhinoObject` / `GeometryBase` 后传入。

- **Infrastructure/Rhino/Live/** —— 新增 7 份实现文件，命名 `LiveRhino<Subdomain>Operator.cs`，
  集中调用以下 RhinoCommon API：
  - 进阶 Transform：`Transform.Mirror(plane)`、`Transform.Scale(plane, sx, sy, sz)`、
    `Transform.Translation(...)`（Array Rectangular），`Transform.Rotation(...)`（Array Polar）、
    `Transform.PlaneToPlane(srcPlane, tgtPlane)`（Orient）；曲线 Array Along Curve：
    `Curve.DivideByCount` + per-station `Transform.PlaneToPlane`。
  - Fillet / Chamfer / Blend：`Curve.CreateFilletCurves`、`Curve.CreateChamferCurves`（若
    RhinoCommon 暴露相应 API；否则用 `BlendCurve.CreateBlend` + 显式裁剪兜底）、
    `Brep.CreateFilletEdges`、`BlendSurface.CreateBlendSurface`、`Curve.CreateBlendCurve`。
  - Offset：`Curve.Offset(plane, distance, tol, cornerStyle)`、`Surface.Offset(distance, tol)`、
    `Mesh.Offset(distance, solidify)`。
  - Trim/Split/Join/Explode/Untrim/Merge：`Curve.Trim`、`Brep.Trim`、`Brep.Split`、
    `Curve.Split`、`Curve.JoinCurves`、`Brep.JoinBreps`、`Curve.DuplicateSegments`（Explode）、
    `Brep.MergeCoplanarFaces`、`BrepFace.Untrim`。
  - Refit：`Curve.Extend(...)`、`Surface.Extend(...)`、`Curve.Rebuild(pointCount, degree, preserveTangents)`、
    `NurbsCurve.IncreaseDegree`、`NurbsSurface.Rebuild(...)`、`Brep.MatchSurface`（若不可用则用
    `Surface.MatchSurface`）。
  - Loft / Sweep / Revolve / Extrude：`Brep.CreateFromLoft(...)`、`Brep.CreateFromSweep(...)`、
    `Brep.CreateFromRevSurface(...)`、`Surface.CreateExtrusion(...)`、`Extrusion.Create(...)`、
    `SweepOneRail` / `SweepTwoRail` 类（按 RhinoCommon 暴露形态）。
  - Morph：`SpaceMorph.Morph(geom)`、`BendSpaceMorph` / `TwistSpaceMorph` / `TaperSpaceMorph` /
    `FlowSpaceMorph`、`SporphSpaceMorph`（若 RhinoCommon 命名变化则取等价类型）。

- **Domain/Enums/** —— 新增：
  - `MirrorPlaneKind`（XY / XZ / YZ / Custom）。
  - `ScalePlaneKind`（World / Custom）。
  - `ArrayKind`（Rectangular / Polar / AlongCurve）。
  - `OrientMode`（PlaneToPlane / TwoPoints + Rotation / TwoCurvesAtParam）。
  - `CornerStyle`（None / Sharp / Round / Smooth / Chamfer）（与 `CurveOffsetCornerStyle` 一一对应）。
  - `BlendContinuity`（G0 / G1 / G2 / G3 / G4）。
  - `LoftType`（Normal / Loose / Tight / Straight / Uniform / Developable）。
  - `SweepRailKind`（OneRail / TwoRail）。
  - `MorphKind`（Bend / Twist / Taper / FlowAlongCurve / FlowAlongSurface / Sporph）。

- **Domain/Models/** —— 新增 Spec 类，集中描述每种动作的"纯数据规格"：
  - `MirrorSpec`、`NonUniformScaleSpec`、`ArraySpec`、`OrientSpec`。
  - `FilletCurveSpec`、`ChamferCurveSpec`、`FilletEdgeSpec`、`BlendCurveSpec`、`BlendSurfaceSpec`。
  - `OffsetCurveSpec`、`OffsetSurfaceSpec`、`OffsetMeshSpec`。
  - `TrimSpec`、`SplitSpec`、`JoinSpec`、`ExplodeSpec`、`UntrimSpec`、`MergeSpec`。
  - `ExtendSpec`、`RebuildCurveSpec`、`RebuildSurfaceSpec`、`ChangeDegreeSpec`、`MatchSurfaceSpec`。
  - `LoftSpec`、`SweepSpec`、`RevolveSpec`、`ExtrudeSpec`。
  - `BendMorphSpec`、`TwistMorphSpec`、`TaperMorphSpec`、`FlowAlongCurveSpec`、`FlowAlongSurfaceSpec`。
  - 每个 Spec 字段均为标量 / 数组（坐标、参数、ObjectId、enum），不持有 RhinoCommon 类型。

- **Contracts/Requests/** —— 每个动作 1 个 entry DTO + 1 个 Request；Apply 与 Preview Request
  字段一致，命名 `<Action>EntryRequest` / `<Action>Request` / `Preview<Action>Request`。
  - 凡有"目标对象集合"语义的（Mirror / NonUniformScale / Array / Trim / Split / Morph），与
    `TransformObjectsRequest` 对齐，支持 `confirmedObjectIds` 与筛查字段两种选择源；筛查字段通过
    `ObjectSelectionSkill` 解析。
  - 凡 entry 自带 `ObjectId` 的（Fillet / Offset / Replace 类、Loft 列表、Sweep 路径 + 截面），
    沿用"每条 entry 必须显式给 ObjectId"模式，不复用筛查。

- **Contracts/Responses/** —— 复用现有 `GeometryModificationResponse` /
  `GeometryModificationPreviewResponse`，仅在 Service 层填充对应 `OperationCount` 与
  `ObjectResults`；不为每个子域新建 Response 类，避免 MCP Client 的解析逻辑膨胀。
  - 例外：`Loft / Sweep / Revolve / Extrude` 会**新建对象**而非"修改既有对象"，需要返回
    `CreatedObjectIds`。复用现有 `GeometryCreationResponse`，由 Service 选择性返回
    `GeometryCreationResponse` 或 `GeometryModificationResponse`：Loft 系动作返回 `GeometryCreationResponse`，
    其他动作返回 `GeometryModificationResponse`。

- **Skills / Agents** —— 一期不新增 Skill：每条动作粒度自洽，没有"必须打包多个动作"的复合
  流程；后续若浮现"按曲线生成曲面 → 倒边 → Offset 加厚"这类复合工序，再抽
  `BodyShellingSkill` / `ProfileExtrusionSkill`。

## 关键设计

1. **Live-only mutation，严格对齐既有模板**
   - 所有 Apply Service 走 `_documentAccessor.ExecuteWithUndo(...)`，描述串 `"MCP: <ToolName>"`；
     批量项 per-entry try/catch；成功后 `doc.Views.Redraw()`；返回
     `OperationResponse<(bool Mutated, T Result)>`。
   - 所有 Preview Service 走 `_documentAccessor.Execute(...)`，**只读 RhinoDoc**，
     返回字段与 Apply Response 形状一致但不落盘、不开 Undo。

2. **新建对象 vs 替换对象的 Response 分裂**
   - **替换语义**（Mirror、NonUniformScale、Orient、Trim / Split / Join / Explode / Untrim /
     Merge、Extend / Rebuild / ChangeDegree / MatchSurface、Morph）：复用 `GeometryModificationResponse`，
     `ObjectResults` 携带 `OldObjectId / NewObjectId / ResultMessage`。
   - **新建语义**（Array、Fillet、Chamfer、Blend、Offset、Loft / Sweep / Revolve / Extrude）：
     复用 `GeometryCreationResponse`，逐条 entry 返回 `CreatedObjects[]`；旧对象保留（与 Rhino 默认
     "_FilletEdge" 行为不同 —— Rhino 默认替换 Brep；本期为了"可解释 + 可回退"采取**保留原 + 生成新**），
     由 Tool 层在 `Common.DeleteOriginal=true` 时显式调用 `RhinoGeometryModificationService.Delete`
     完成清除；默认 `false`。

3. **Selection source（按动作类别）**
   - "对象集合 → 全部应用同一动作"（Mirror / NonUniformScale / Array / Morph）：与
     `TransformObjects` 对齐，支持 `confirmedObjectIds` + 筛查字段；同时提供时 `confirmedObjectIds`
     优先 + warning。
   - "每条 entry 自带 ObjectId"（Fillet / Offset / Trim / Split / Join / Loft / Sweep / Revolve /
     Extrude / Refit）：每条 entry 必须显式给所有相关 ObjectId（曲线 Fillet 是 `LeftCurveId / RightCurveId`，
     Brep FilletEdge 是 `BrepId + EdgeIndex[]`，Loft 是 `ProfileCurveIds[]`，Sweep 是 `RailIds[] + ProfileCurveIds[]`，
     Revolve 是 `ProfileCurveId + AxisStart/AxisEnd`，Extrude 是 `ProfileCurveId + DirectionVector + Distance`）。
   - 任何 entry 中 ObjectId 解析失败 → per-entry 失败，不阻断同 Request 其他 entry。

4. **公共参数 normalization**
   - `Tolerance` 缺省取 `doc.ModelAbsoluteTolerance`，`AngleTolerance` 缺省取 `doc.ModelAngleToleranceRadians`，
     由 Service 在调用 RhinoCommon 前注入；`Tolerance < 1e-9` 视为非法 → 硬错误。
   - `ScaleFactor < 1e-12` / 任意 `ScaleFactor` ≈ 0 → 硬错误（避免退化几何）。
   - Array 数量上限：Rectangular / Polar 单 entry 总 station 数 ≤ 5000，超出硬错误；AlongCurve
     按弧长分布，等距数量上限同样 5000。

5. **Mirror plane / Scale plane 显式枚举**
   - `MirrorPlaneKind`：`XY / XZ / YZ / Custom`；`Custom` 时需要 `PlaneOrigin{XYZ} + PlaneNormal{XYZ}`。
   - `ScalePlaneKind`：`World / Custom`；`Custom` 时同样接受 plane 三参数。
   - 内部统一构造 `Plane(origin, normal)` → 调 `Transform.Mirror(plane)` / `Transform.Scale(plane, sx, sy, sz)`。

6. **Fillet / Chamfer / Blend 的"先 Preview 看交点 → 再 Apply"**
   - Preview 返回每对输入曲线的"延伸 / 修剪交点"以及生成弧 / blend 段的端点，让 LLM 在 Apply 前
     看是否符合预期。
   - Apply 返回新生成的 `CreatedObjects[]`；原曲线默认保留（见关键设计 #2）。

7. **Offset 的 Plane 选择**
   - 曲线 Offset 必须绑定平面：`OffsetPlaneKind`（CPlane / WorldXY / WorldXZ / WorldYZ / FromCurveBoundingBox /
     Custom）；曲线非平面 / 平面与曲线退化 → 硬错误。
   - 曲面 Offset 默认 normal 方向，距离正负决定方向。
   - Mesh Offset 默认 `solidify=true`，生成闭合 shell；提供 `solidify` 字段允许覆盖。

8. **Trim / Split 的 cutter / cuttee 拆分**
   - `TrimSpec`：`SubjectId + CutterIds[] + KeepSide(Inside / Outside / KeepBoth)`；KeepBoth 时
     转成 Split 语义。
   - Split：返回所有 split 段的新 ObjectId。
   - Trim：默认仅保留 KeepSide；要保留多段则用 Split。

9. **Join / Explode 的可逆性**
   - Join 把多个 Curve / Brep 合并为单条；返回新 ObjectId 列表（多段无法 join 的部分单列）。
   - Explode 把单个 Curve（PolyCurve / Polyline）/ Brep（多 face）拆为多个；返回新 ObjectId 列表。
   - 两者天然互逆，但 Apply 不串联两者；客户端需要时分两步调用。

10. **Extend / Rebuild 的连续性策略**
    - Extend：`Mode(Line / Arc / Smooth)`；Smooth 模式按当前末端切矢延伸。
    - Rebuild Curve：`PointCount + Degree + PreserveTangents(bool)`；`PointCount < Degree + 1` → 硬错误。
    - Rebuild Surface：`UCount + VCount + UDegree + VDegree`；同样硬错误约束。
    - ChangeDegree：仅升阶（degree-elevation），不降阶；输入新 degree ≤ 当前 degree → 硬错误。
    - MatchSurface：`SourceBrepId + SourceEdgeIndex + TargetBrepId + TargetEdgeIndex + Continuity(G0|G1|G2)`。

11. **Loft / Sweep / Revolve / Extrude 的几何源**
    - Loft：`ProfileCurveIds[]`（顺序敏感） + `LoftType` + `Closed?`；`ProfileCurves.Count < 2` → 硬错误。
    - Sweep：`RailIds[]`（1 或 2 条） + `ProfileCurveIds[]` + `Closed?`；按 `SweepRailKind` 分派
      `SweepOneRail` / `SweepTwoRail`。
    - Revolve：`ProfileCurveId + AxisStart{XYZ} + AxisEnd{XYZ} + StartAngleRadians + EndAngleRadians`；
      角度跨度 ≤ 0 → 硬错误。
    - Extrude：`ProfileCurveId + DirectionX/Y/Z + Distance + Cap?(bool)`；profile 闭合且 `Cap=true` 时返回 closed Brep。
    - 全部使用 RhinoCommon 同步 API，不开 background worker；超时由 `RHINO_MAIN_THREAD_BUSY` 兜底。

12. **Morph 的"基准几何"输入**
    - `BendMorph`：`StartPoint{XYZ} + EndPoint{XYZ} + AngleRadians + Preserve(StraightSection|SmoothBend)`。
    - `TwistMorph`：`AxisStart{XYZ} + AxisEnd{XYZ} + AngleRadians`。
    - `TaperMorph`：`AxisStart{XYZ} + AxisEnd{XYZ} + StartRadius + EndRadius`。
    - `FlowAlongCurve`：`SourceCurveId + TargetCurveId + Stretch?(bool)`。
    - `FlowAlongSurface`：`SourceSurfaceId + TargetSurfaceId + ReverseUVN?`。
    - 每条 entry 的 `ObjectIds` 由 entry 自带（避免单 Tool 同时跑多个 morph 类型时筛查源混淆）。

13. **Undo / 撤销策略**
    - 单次 Tool 调用 = 单个 Undo record，描述形如 `"MCP: MirrorObjects (12 ok / 0 fail)"`。
    - 部分失败仍写入 Undo，确保用户 `Ctrl+Z` 能一次还原已成功的部分；纯失败（无任何成功）通过
      `CancelUndoRecord` 关闭，避免空 Undo 条目。

14. **Stale / Live 状态**
    - 全部 Apply / Preview 走 live；不需要 stale 检测；不依赖 offline 路径。
    - Tool / Service 不直接处理 `LIVE_RHINO_REQUIRED` / `NO_ACTIVE_DOCUMENT` /
      `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE` / `RHINO_MAIN_THREAD_BUSY`，
      `ILiveRhinoDocumentAccessor` 自动返回。

15. **EXET 阶段子域分批**
    - 7 个子域按 `Transform → Fillet/Blend → Offset → Sweep/Loft → Boolean → Refit → Morph` 顺序落地，
      每个子域单独 build + smoke + Undo 验证 + EXET 文档；任一子域失败不阻塞其他子域 Plan。
    - PLAN 文档保持单份（本文件），EXET 文档允许按子域分多份，命名 `260422_EXET_geometry-advanced-edit-tools-<subdomain>.md`。

## 涉及文件

> 数量较多，按目录列出；具体文件名按"`<Action>Tool.cs` + `Preview<Action>Tool.cs`"成对出现。

**新增（Tools）** —— 共 62 个文件：
- `src/MCP_Rhino.Server/Tools/Geometry/Transform/{Mirror,NonUniformScale,Array,Orient}{,Preview}ObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Fillet/{FilletCurves,ChamferCurves,FilletBrepEdges,BlendCurves,BlendSurfaces}{,Preview}Tool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Offset/{OffsetCurves,OffsetSurface,OffsetMesh}{,Preview}Tool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Boolean/{Trim,Split,Join,Explode,Untrim,Merge}{,Preview}ObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Refit/{Extend,RebuildCurve,RebuildSurface,ChangeDegree,MatchSurface}{,Preview}Tool.cs`
  - 注：本子域共 5 个动作 = 10 个 Tool；与目标章节"4 + 4 = 8"略有出入，目标处把 RebuildCurve / RebuildSurface 视作同一 Rebuild 动作的两种形态；实际落盘为 5 个 Apply + 5 个 Preview，便于 schema 解耦。
- `src/MCP_Rhino.Server/Tools/Geometry/Loft/{Loft,Sweep,Revolve,Extrude}{,Preview}Tool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Morph/{BendMorph,TwistMorph,TaperMorph,FlowAlongCurve,FlowAlongSurface}{,Preview}Tool.cs`

**新增（Application）** —— 14 个文件：
- 7 个 Service：`src/MCP_Rhino.Server/Application/Services/RhinoGeometry{AdvancedTransform,FilletBlend,Offset,BooleanEdit,Refit,Loft,Morph}Service.cs`
- 7 个接口：`src/MCP_Rhino.Server/Application/Interfaces/ILive{AdvancedTransformer,FilletBlendOperator,OffsetOperator,BooleanEditOperator,RefitOperator,LoftOperator,MorphOperator}.cs`

**新增（Infrastructure）** —— 7 个文件：
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhino{AdvancedTransformer,FilletBlendOperator,OffsetOperator,BooleanEditOperator,RefitOperator,LoftOperator,MorphOperator}.cs`

**新增（Domain）** —— Enum 9 个 + Spec 30+ 个，按子域分组放入：
- `src/MCP_Rhino.Server/Domain/Enums/{MirrorPlaneKind,ScalePlaneKind,ArrayKind,OrientMode,CornerStyle,BlendContinuity,LoftType,SweepRailKind,MorphKind}.cs`
- `src/MCP_Rhino.Server/Domain/Models/{<SpecName>}.cs`（详见架构归属章节列表）

**新增（Contracts）** —— 每动作 1 个 Entry + Apply Request + Preview Request；总数与 Tool 数一致：
- `src/MCP_Rhino.Server/Contracts/Requests/{<Action>EntryRequest,<Action>Request,Preview<Action>Request}.cs`

**新增（Test）** —— 7 个子文件夹：
- `Project_Test/260422_TEST_geometry-advanced-edit-{transform,fillet,offset,boolean,refit,loft,morph}/`，
  每个子文件夹包含对应子域的 `*SmokeTest.cs` + 必要的 `.3dm` 工作副本。

**修改**：
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs` —— 注册 7 个新 interface 实现 + 7 个 Service。
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs` + `.Parsing.cs` ——
  追加 7 个子域对应 smoke 子命令（如 `geometry-transform-smoke-test` / `geometry-fillet-smoke-test` 等）。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs` —— 接入新 smoke 分支。

**复用（不改）**：
- `ILiveRhinoDocumentAccessor`（`Execute` / `ExecuteWithUndo`）。
- `IGeometryValidator` / `ILiveGeometryValidator`（按需扩展校验，不替换）。
- `GeometryModificationResponse` / `GeometryModificationPreviewResponse` / `GeometryCreationResponse`。
- `ObjectSelectionSkill`（筛查字段解析）。

## 使用方式

MCP Tool 调用示例：

- `MirrorObjects(filePath, mirror={MirrorPlaneKind:"XZ"}, confirmedObjectIds=[...])`
- `ArrayObjects(filePath, array={ArrayKind:"Polar", CenterX/Y/Z, AxisX/Y/Z, Count:8, AngleStepRadians:0.7854}, confirmedObjectIds=[...])`
- `OrientObjects(filePath, orient={Mode:"PlaneToPlane", SourcePlane:{...}, TargetPlane:{...}}, confirmedObjectIds=[...])`
- `FilletCurves(filePath, entries=[{LeftCurveId, RightCurveId, Radius:5}, ...])`
- `FilletBrepEdges(filePath, entries=[{BrepId, EdgeIndices:[0,2,4], Radius:3}, ...])`
- `BlendSurfaces(filePath, entries=[{LeftBrepId, LeftEdgeIndex:0, RightBrepId, RightEdgeIndex:1, Continuity:"G2", Bulge:1.0}, ...])`
- `OffsetCurves(filePath, entries=[{CurveId, Distance:10, OffsetPlaneKind:"WorldXY", CornerStyle:"Round"}, ...])`
- `OffsetSurface(filePath, entries=[{SurfaceId, Distance:5, Solid:true}, ...])`
- `TrimObjects(filePath, entries=[{SubjectId, CutterIds:[...], KeepSide:"Outside"}, ...])`
- `JoinCurves(filePath, entries=[{CurveIds:[...]}, ...])`
- `ExplodeObjects(filePath, entries=[{ObjectId}, ...])`
- `Loft(filePath, entries=[{ProfileCurveIds:[...], LoftType:"Normal", Closed:false}, ...])`
- `Sweep(filePath, entries=[{RailIds:[...], ProfileCurveIds:[...], SweepRailKind:"OneRail", Closed:false}, ...])`
- `Revolve(filePath, entries=[{ProfileCurveId, AxisStart:{...}, AxisEnd:{...}, StartAngleRadians:0, EndAngleRadians:6.2832}, ...])`
- `Extrude(filePath, entries=[{ProfileCurveId, DirectionX/Y/Z, Distance:100, Cap:true}, ...])`
- `BendMorph(filePath, morph={StartPoint:{...}, EndPoint:{...}, AngleRadians:1.5708}, confirmedObjectIds=[...])`
- `FlowAlongCurve(filePath, entries=[{ObjectIds:[...], SourceCurveId, TargetCurveId, Stretch:false}, ...])`

每个 Apply 都有同名 `Preview<Action>(...)` 等价工具，参数一致。

典型流程：
- "把 ColumnGrid 层所有点按 X 阵列 5 列、Y 阵列 3 行、间距 6000 / 4500，再倒角" →
  `ArrayObjects(... ArrayKind:Rectangular ...)` → `FindLayerCandidates` → `FilletBrepEdges`。
- "Loft 三条曲线生成屋面 → MatchSurface 把屋面与已有侧墙 G2 对齐 → OffsetSurface 加厚 200" →
  `Loft` → `MatchSurface` → `OffsetSurface(... Solid:true ...)`。
- "用 RoofCurves 沿 RoofRail Sweep 生成屋面 Brep → 给屋面加 50mm Offset 厚度 → 把
  屋面整体沿 Z 轴翻转" → `Sweep` → `OffsetSurface` → `MirrorObjects(... XY plane ...)`。

## 验收标准

构建 / smoke：
- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 无 Warning 通过。
- 各子域 smoke：`dotnet run --project src/MCP_Rhino.Server -- geometry-<subdomain>-smoke-test test-files/MCP_rhino_test.3dm`
  - CLI fallback 路径下：所有 Apply / Preview Tool 返回 `LIVE_RHINO_REQUIRED`；工作副本对象数无变化。
- Live 手工 smoke（Rhino 内 `_McpDevSmoke <subdomain>`）：
  - Mirror / NonUniformScale / Array / Orient：源对象数量变化符合预期；Edit → Undo 一步还原。
  - Fillet / Blend：生成的圆角 / blend 弧端点距离原曲线端点 ≤ `Tolerance`。
  - Offset：生成偏移曲线与原曲线垂直距离误差 ≤ `Tolerance`。
  - Trim / Split：被裁 / 切断对象数量与 cutter 关系一致；Undo 一步还原。
  - Loft / Sweep / Revolve / Extrude：生成 Brep `IsValid == true`；体积 / 面积大于 0。
  - Morph：原对象 BBox 中心点经过 Morph 后落在期望位置（误差 ≤ 0.1）。

边界用例：
- 空 entries → 整 Request 失败，消息 `"At least one entry is required."`。
- 单 entry ObjectId 解析失败 → 该 entry 失败 + 同 Request 其他 entry 不受影响。
- Mirror plane 法向 ≈ 0 → 该 entry 失败。
- Array 总 station > 5000 → 整 Request 失败。
- Loft profiles < 2 / Sweep rails 数量非 1 或 2 → 整 Request 失败。
- 倒角半径 ≤ 0、Offset 距离 ≈ 0 → 该 entry 失败。
- Apply 阶段 RhinoCommon API 返回 `null` / `false`：该 entry 失败，Message 含 RhinoCommon 反馈
  （如 `"Brep.CreateFilletEdges returned null"`）。

## 风险与回退方案

风险：
- **API 形态在 Rhino 8 与未来版本之间的差异**：`Brep.CreateFilletEdges` / `Brep.CreateFromSweep` 等
  API 在不同 Rhino 版本签名 / 行为略有差异。缓解：所有调用集中在 Infrastructure/Live 单层，
  签名漂移只影响一份文件；CI 锁定 Rhino 8 SDK 版本。
- **响应规模**：Loft / Sweep / Trim 可能一次产生大量子对象 / 子曲线。缓解：单 Apply 总输出对象数
  > 5000 触发硬错误。
- **Undo 大批量回退性能**：单次 Apply 处理 1000+ entry 时，Rhino Undo record 大小可能拖慢
  `Ctrl+Z` 体验。缓解：Tool 文档明确建议"批次大小不超过 200 entry"；超过 1000 → warning。
- **Brep 健壮性**：`Brep.CreateFromLoft` 在某些拓扑下生成无效 Brep。缓解：Apply 前不做修复；
  Apply 后调 `Brep.IsValid` 检查，如果 invalid → 该 entry 失败 + Message 含失败原因 + 调用
  `doc.Objects.Delete` 回滚已 Add 的 invalid Brep；该 entry 不进入成功列。
- **Morph 性能**：高密度 Brep 经过 SpaceMorph 计算可能 > 10s 触发主线程超时。缓解：Tool 文档建议
  morph 前先 Reduce 复杂度；超时由 `RHINO_MAIN_THREAD_BUSY` 自然兜底。

回退方案：
- 单 Tool 失败：失败 Tool 通过 `ToolRegistration` 黑名单单独下线，其他 Tool 不受影响。
- 单子域回退：每子域文件落在独立子目录 + 独立 Service，回退仅需 `git revert` 对应 commit，不
  影响其他子域。
- 全部回退：所有新文件均落在新增目录，不修改既有 Service / Tool / Skill 签名；`git revert`
  整批 commit 即可还原。

## 后续扩展方向

- **Brep 布尔**（Union / Difference / Intersection）：本期不纳入"Boolean"子域，留作下一波扩展；
  与 Trim / Split 共享解析与目标对象逻辑，复用 `RhinoGeometryBooleanEditService` 即可承接。
- **Polyline / Polysurface 专项**：批量平滑、批量分段、按公差合并共线段。
- **Hatch / Annotation / Block** 专项：当前完全不涉及，留作"非几何"扩展期。
- **Skills / Agents**：浮现复合工序（"按曲线生成壳体并自动加厚"、"按导轨阵列对象并对齐"）后，抽
  `BodyShellingSkill` / `RailArraySkill`；Agent 层预留 `ParametricModelingAgent` 用于多步策略。
- **NURBS 直接编辑**：在 `EditControlPoints` 之上扩展 KnotInsert / KnotRemove / DegreeReduce。
- **几何修复**：`Brep.Repair` / `Mesh.Heal` / 自交检测与裁剪，独立 `RhinoGeometryRepairService`。
- **批量大小自适应分片**：当 entry 数超过阈值时由 Service 自动拆分多次 Undo record，把单步 Undo
  大小控制在用户可接受范围。

