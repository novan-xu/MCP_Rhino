# 260423_PLAN_geometry-edit-molecular-architecture

## 背景

04-23 已规划四份 geometry edit 相关 PLAN：

- `260423_PLAN_geometry-edit-descriptor-lane`（Wave 1，曲线 + DirectOverride 最小骨架）
- `260423_PLAN_geometry-edit-derived-operations`（Wave 2，曲线派生操作 + 真实策略路由）
- `260423_PLAN_geometry-edit-surface-lane`（Wave 3，扩到曲面 + descriptor 两档体量）
- `260423_PLAN_surface-point-order-redefinition`（Plan 4，曲面边界点序重建）

逐份审视后发现：

- Wave 1/2/3 在契约层高度耦合（共享 `EditableGeometryDescriptor`、`CurveEditSpec`、`GeometryEditStrategyKind`、Tool 家族、metadata replay 白名单），"独立 lane" 主要是风险/验证门槛边界而非能力边界；Wave 2 单独成 PLAN 偏薄。
- Plan 4 被设计成与 Wave 3 平行的独立栈，但其 5 个功能块中至少"属性保留"可完全复用 Wave 1、"frame 采样"可部分复用 Wave 3；真正独有的是参考边识别、LCS 构造、点序规范化、边界驱动重建。
- 各 lane 里大量"跨多个 RhinoCommon API 的合成操作"埋在 Service 或 Live 适配器内部，没有对外暴露；未来 Skill、Agent 或其它 Tool 想复用，无处可取。

因此重组思路：**按"分子工具"粒度重组能力线**。分子定义为：

- 跨多个 RhinoCommon API 才能完成
- 不包含完整业务决策
- 可被多条 Skill / Tool 路径复用

先固定分子目录与对外暴露策略，再决定：

- 哪些分子直接对外暴露为 MCP Tool；
- 哪些分子作为内部 Service 供 Skill 与其它分子调用；
- 哪些上层工作流以 Preview/Apply 复合 Tool 或 Skill 的形式编排分子。

本 PLAN 是**总纲**，只定义分子目录 + 暴露策略 + 子 PLAN 拆分方案；**不直接产出代码**，落地由后续子 PLAN 承担。

## 目标

- 固定 8 个主干分子的职责边界与对外暴露策略；
- 给出子 PLAN 拆分方案，替代 Wave 1/2/3 与 Plan 4 的 lane 划分；
- 明确"哪些既有分子可被 Plan 4 的专属分子复用"，避免重复造轮子；
- 保证与既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 兼容（不删、不改签名）。

**明确非目标：**

- 不决定子 PLAN 的执行顺序（由子 PLAN 评审决定）；
- 不定义 Plan 4 专属分子的详细签名（由 Plan 4 子 PLAN 承担）；
- 不产出代码、不改动现有 `src/` 下任何文件。

## 架构归属

本 PLAN 在总体上采用以下分层。具体文件清单由各子 PLAN 负责。

- **Application/Interfaces/**：每个分子（除纯 Domain 部分）对应一个接口
- **Infrastructure/Rhino/Live/**：每个接口对应一个 `Live*` 实现
- **Domain/**：纯数据模型与纯逻辑（如派生点规则求值的无 Rhino 部分、策略路由的决策逻辑）沿用 Wave 1/2/3 已规划的 `EditableGeometryDescriptor` / `CurveEditSpec` / `SurfaceEditSpec` / 各 Kind 枚举，按分子视角微调字段归属，不新增平行模型家族
- **Tools/Geometry/Edit/**：
  - 分子级暴露 Tool：`GetEditableGeometryDescriptorTool`、既有 `GetGeometryFramesInLiveTool`（扩展承载 frame 采样语义，不新增平行 frame MCP Tool）
  - 复合级暴露 Tool：`PreviewEditCurveGeometryTool` / `ApplyEditCurveGeometryTool` / `PreviewEditSurfaceGeometryTool` / `ApplyEditSurfaceGeometryTool`
  - 既有 `ReplaceGeometryTool` 继续承担 Mutation 边界分子角色
- **Skills/**：本总纲不新增 Skill。Plan 4 若需要 Skill 编排，由 Plan 4 子 PLAN 决定

## 关键设计

### §1 8 个主干分子目录

#### 分子 1：几何可编辑描述器

- **输入**：`FilePath` + `ObjectId` + 可选 `DescriptorDetail = Summary | Full`
- **输出**：
  - 身份（GUID + 子类名如 `NurbsCurve` / `NurbsSurface` / `PlaneSurface`）
  - 结构字段：曲线 `Degree` / `IsClosed` / `IsPeriodic`；曲面 `DegreeU/V` / `CountU/V` / `IsClosedU/V` / `IsPeriodicU/V` / `Bbox`
  - 控制点数组（仅 `Full` 档；曲面 row-major 扁平 `index = u*countV + v`）
  - `ControlPointCentroidWorld`：控制点世界坐标算术中心；`Summary` / `Full` 两档均返回，用作 ExactTransform 的稳定原点，避免曲面请求为取 centroid 被迫升级到 Full
  - metadata summary（LayerIndex / Name / Color / UserString 数量）
  - `SupportKind = Supported | PreviewOnly | Unsupported`
  - warning hint 列表（`PARAMETERIZATION_MAY_CHANGE` / `UNDERLYING_SURFACE_FALLBACK` 等）
- **涉及 API**：`RhinoDoc.Objects.FindId`、`Curve.Points` / `NurbsSurface.Points`、`BrepFace.UnderlyingSurface`（与分子 8 协作）、`Attributes.GetUserStrings`
- **范围**：曲线 + 曲面（含 single-face Brep 降级）
- **对外暴露：是** → MCP Tool `GetEditableGeometryDescriptor`

#### 分子 2：派生点规则求值器（含 Selector 解析）

- **输入**：descriptor 原点集 + Selector spec（`All` / `Indices` / `Range` / `EndpointsOnly` / `UvRange` / `EdgeOnly`） + DerivedSpec（`ScaleAboutCentroid` / `TranslateByVector` / `OffsetAlongNormal`）
- **输出**：
  - 已解析扁平索引列表
  - 新点列（被选点变，未选点原位保留）
  - 已落地参数（centroid 世界坐标、每点实际偏移向量、退化列表等）
  - warning（`POINT_SELECTOR_INVALID` / `OFFSET_NORMAL_DEGENERATE` 等）
- **实现**：纯函数；`OffsetAlongNormal` 通过注入分子 3 取 frame
- **范围**：曲线 + 曲面分支（曲线一维索引；曲面 row-major 扁平索引）
- **对外暴露：否** → Application Service `IDerivedPointOperationEvaluator`

#### 分子 3：曲线/曲面 frame 采样器

- **输入**：目标几何（Curve 或 Surface） + 参数列表（`t[]` 或 `(u,v)[]`）
- **输出**：每点 `{Origin, XAxis, YAxis, ZAxis/Normal}` + 退化标记
- **涉及 API**：`Curve.PerpendicularFrameAt(t)`、`Surface.FrameAt(u,v)` / `Surface.NormalAt(u,v)`、NurbsSurface Greville abscissa 计算
- **范围**：曲线 + 曲面
- **对外暴露：不新增平行 MCP Tool** → 内部为 `IGeometryFrameSampler` 服务；对外由既有 `GetGeometryFramesInLive` 扩展承载，避免第二套 frame Tool
- **复用场景**：`OffsetAlongNormal` 内部调用；Plan 4 LCS builder 的基础之一；构造辅助线 / 放样参考

#### 分子 4：策略路由器 + ExactTransform 执行桥

- **输入**：EditSpec（Operation.Kind + DerivedKind + Selector.Kind + 数值参数） + descriptor kind
- **输出**：
  - `GeometryEditStrategyKind ∈ {ExactTransform, ReconstructFromControlPoints, ReconstructFromPoints, Unsupported}`
  - `StrategyResolutionTrace`（规则命中序列）
- **执行桥**：当路由为 `ExactTransform` 时，由独立 `IGeometryTransformExecutionBridge` 产出等价 `GeometryTransformSpec`，并委托 `GeometryModificationSkill` 的 `internal` 协作方法执行（由子 PLAN C 盘点并在必要时新增）；Undo 描述统一为 `MCP:EditCurveGeometry` / `MCP:EditSurfaceGeometry`
- **internal 协作方法约束**：所需 `internal` 方法清单必须在子 PLAN C（及后续扩曲面分支的子 PLAN D）草稿评审前完成盘点并写入对应 PLAN 的"涉及文件"章节；禁止 EXET 阶段临时给 `GeometryModificationSkill` 添加新的 `internal` 方法。本总纲对"既有 Tool/Skill 不删不改公开签名"的承诺不覆盖 `internal` 协作方法的新增；`internal` 方法新增视作执行桥契约的一部分，由子 PLAN 自行声明。
- **实现**：`IGeometryEditStrategyResolver` 只做纯决策；执行桥单独注册，避免 resolver 直接承担 mutation / preview 执行语义
- **范围**：曲线 + 曲面共享 resolver 实例，内部按 descriptor kind 分派
- **对外暴露：否** → Application Service `IGeometryEditStrategyResolver` + `IGeometryTransformExecutionBridge`

#### 分子 5：控制点重建器（含 IsValid 尾校验）

- **输入**：
  - descriptor（提供身份 + 摘要 + `IsClosed` / `IsPeriodic` 等可对外暴露的结构信息）
  - 原始几何引用（`filePath` + `ObjectId`，用于 reconstructor 实现内部经 `ILiveRhinoDocumentAccessor` 在主线程重读 knots / weights / rational / parameter range 等不暴露在 descriptor 的结构信息）
  - 新点列（曲线一维 / 曲面 U×V 网格）
- **设计要点**：
  - knots / weights / rational / parameter range 故意不进 descriptor，以控制 Full 档 token 体量（曲面尤甚）；这些信息只在重建瞬间需要，由 reconstructor 实现内部即时读取
  - reconstructor 自身不写文档、不做 mutation；文档读取通过注入的 `ILiveRhinoDocumentAccessor` 完成
- **输出**：
  - 新 `Curve` 或 `Surface` 内存对象（未落盘）
  - warning（`PARAMETERIZATION_CHANGED` / `UNSUPPORTED_GEOMETRY_FALLBACK` / `RECONSTRUCTION_INVALID` / `RECONSTRUCTION_SEMANTICS_CHANGED`）
- **尾步骤**：强制调用 `IsValid` / `IsValidWithLog`，失败则 Preview 返回 `RECONSTRUCTION_INVALID`，Apply 拒绝落盘
- **涉及 API**：`NurbsCurve.Create` / `Curve.CreateControlPointCurve`、`NurbsSurface.Create`、`IsValid` 系列
- **范围**：
  - 曲线：LineCurve / PolylineCurve / NurbsCurve
  - 曲面：PlaneSurface / NurbsSurface / untrimmed single-face Brep（由分子 8 降级为 Underlying，再由分子 8 回包为 Brep）
- **对外暴露：否** → Application Service `IGeometryReconstructor`
- **不覆盖** Plan 4 的 4PointSurface / BoundarySurface 两条出口——那是 Plan 4 专属的"边界驱动重建器"分子，由 Plan 4 子 PLAN 定义

#### 分子 6：Metadata snapshot / replay

- **snapshot**：源 `RhinoObject` → 白名单字段 + user strings 快照
- **replay**：目标 `ObjectId` + 快照 → 写回 + 返回 `MetadataDropped` 字段列表
- **白名单（固定）**：`LayerIndex` / `ObjectColor + Source` / `PlotColor + Source` / `PlotWeight + Source` / `LinetypeIndex + Source` / `Name` / `Visible` / 全部 user strings
- **明确不复制**：RenderMaterial / MaterialSource / GroupList / DisplayMode / Space / block instance 相关字段
- **涉及 API**：`RhinoObject.Attributes` 读写、`GetUserStrings` / `SetUserString`、`doc.Objects.ModifyAttributes`
- **范围**：完全跨几何种类通用；Plan 4 属性保留直接复用
- **对外暴露：否** → Application Service `IGeometryMetadataOperator`

#### 分子 7：Mutation 边界（Geometry Replace + 单 Undo）

- **输入**：`ObjectId` + 新 `GeometryBase` + 可选 metadata 快照
- **输出**：`UndoRecordName` + 新 `ObjectId` + 前后对象数校验
- **契约**：单次 Undo record 内完成 `doc.Objects.Replace` + metadata replay；Apply 成功后 `doc.Views.Redraw()`
- **涉及 API**：`doc.BeginUndoRecord` / `doc.Objects.Replace` / `doc.EndUndoRecord` / `doc.Views.Redraw`
- **范围**：曲线 + 曲面 + Plan 4 边界重建通用
- **对外暴露：是（既有）** → MCP Tool `ReplaceGeometry`（继续沿用，不改签名）

#### 分子 8：untrimmed single-face Brep 探测/降级器

- **输入**：`RhinoObject`
- **输出**：
  - `IsUntrimmedSingleFaceBrep: bool`
  - 若是：`UnderlyingSurface` 引用 + 回包函数 `(newSurface) => Brep`（Apply 后仍保持 Brep 类型）
  - 若否：具体原因字符串（`"polysurface has N faces"` / `"brep face is trimmed"` / `"OuterLoop not aligned within tolerance"`）
- **判定条件**（全部满足才降级）：
  - `Geometry is Brep`
  - `brep.Faces.Count == 1`
  - `brep.Faces[0].IsSurface`
  - `brep.Faces[0].UnderlyingSurface()` 非空
  - `brep.Faces[0].OuterLoop.To3dCurve()` 包围区域与 `UnderlyingSurface` 边界在 `ModelAbsoluteTolerance` 内一致
- **涉及 API**：`Brep.Faces`、`BrepFace.IsSurface`、`UnderlyingSurface`、`OuterLoop.To3dCurve`、容差比较
- **范围**：曲面专用
- **对外暴露：否** → Application Service `IBrepSurfaceDowngrader`；判定结果通过分子 1 descriptor 的 `UNDERLYING_SURFACE_FALLBACK` warning 对外暴露

### §2 对外暴露策略总览

| 分子 | 对外 MCP Tool | 内部 Application Service |
|---|---|---|
| 1 可编辑描述器 | `GetEditableGeometryDescriptor` | `IEditableGeometryDescriptorService` |
| 2 派生点求值器 | — | `IDerivedPointOperationEvaluator` |
| 3 frame 采样器 | `GetGeometryFramesInLive`（扩展） | `IGeometryFrameSampler` |
| 4 策略路由器 | — | `IGeometryEditStrategyResolver` + `IGeometryTransformExecutionBridge` |
| 5 控制点重建器 | — | `IGeometryReconstructor` |
| 6 metadata 操作 | — | `IGeometryMetadataOperator` |
| 7 Mutation 边界 | `ReplaceGeometry`（既有） | `IGeometryMutationService` |
| 8 Brep 降级器 | — | `IBrepSurfaceDowngrader` |

**分子级对外 Tool 共 3 个**：`GetEditableGeometryDescriptor`、`GetGeometryFramesInLive`（既有扩展）、`ReplaceGeometry`（既有）。

**复合级对外 Tool 共 4 个**（由 §3 编排，子 PLAN B / D 落地）：`PreviewEditCurveGeometry` / `ApplyEditCurveGeometry` / `PreviewEditSurfaceGeometry` / `ApplyEditSurfaceGeometry`。

**Plan 4 专属对外 Tool 共 3 个**（子 PLAN E）：`InspectSurfaceRebuildDescriptor` / `PreviewRedefineSurfacePointOrder` / `ApplyRedefineSurfacePointOrder`。

合计 geometry edit 相关对外 Tool 约 10 个（含既有的 `GetGeometryFramesInLive` / `ReplaceGeometry`）。LLM/Agent 选路提示需相应扩，重点说明：

- "整体仿射变换"（translate / uniform scale on All）优先 `TransformObjects`，本 lane 仅在调用方已经在做控制点 / 派生点级别编辑时才进入
- 曲面 default `Summary`、曲线 default `Full` 的 token 政策需写入 Tool 描述

**暴露原则**：

- 当分子本身就是"LLM 会单独使用的信息源"（描述器、frame）→ 暴露
- 当分子仅是 Preview/Apply 管线的中间环节（派生求值、路由、重建、metadata replay、Brep 降级）→ 不单独暴露；其结果通过 Preview 响应字段或 warning 间接暴露
- 如后续 LLM 实战出现"干跑推点"或"干跑选路"的稳定需求，再追加 Tool 暴露分子 2 / 分子 4，属增量优化

### §3 复合 Tool 的分子编排

| 复合 Tool | 编排的分子（按调用顺序） |
|---|---|
| `PreviewEditCurveGeometry` | 1 读 → 2 求值（曲线分支） → 4 路由 → 5 曲线重建 |
| `ApplyEditCurveGeometry` | 同上 → 6 snapshot → 7 替换 + 单 Undo（内含 metadata replay） |
| `PreviewEditSurfaceGeometry` | 1 读（经 8 Brep 判定/降级） → 2 求值（曲面分支） → 4 路由 → 5 曲面重建 |
| `ApplyEditSurfaceGeometry` | 同上 → 6 snapshot → 7 替换 + 单 Undo |

Plan 4 的复合 Tool 或 Skill 由其专属子 PLAN 定义，将复用分子 3（frame 采样，作为 LCS builder 底座之一）、分子 6（属性保留）与分子 7（Mutation 边界），其余为 Plan 4 专属分子。

### §4 子 PLAN 拆分方案

在本总纲之下，建议拆出以下子 PLAN，替代现有四份 lane PLAN：

#### 子 PLAN A：分子基础层

包含分子 **1、3、6、8**。产出：

- `IEditableGeometryDescriptorService` + Live 实现（曲线 + 曲面 + Brep 降级）
- `IGeometryFrameSampler` + Live 实现（不新增并列 MCP Tool；复用既有 `GetGeometryFramesInLive` 入口）
- `IGeometryMetadataOperator` + Live 实现（白名单已固定）
- `IBrepSurfaceDowngrader` + Live 实现
- MCP Tool：`GetEditableGeometryDescriptor`、`GetGeometryFramesInLive`（扩展）

验收：上述两个 Tool 能对曲线 + 曲面 + untrimmed single-face Brep 稳定返回 descriptor 与 frame；metadata snapshot/replay 作为内部 Service 具备独立单元测试。无 edit/mutation 能力。

#### 子 PLAN B：曲线 edit 复合

包含分子 **5（曲线分支） + 7（Apply 集成）**；首期仅 `DirectOverride`。产出：

- `IGeometryReconstructor` + Live 实现（曲线分支）
- `PreviewEditCurveGeometryTool` / `ApplyEditCurveGeometryTool`

依赖：子 PLAN A 合入。

#### 子 PLAN C：派生点操作 + 策略路由

包含分子 **2、4**。产出：

- `IDerivedPointOperationEvaluator` + Live/Domain 实现
- `IGeometryEditStrategyResolver` + Live/Domain 实现（纯决策）
- `IGeometryTransformExecutionBridge` + Live 实现（ExactTransform 执行桥）
- 扩展曲线 edit 复合 Tool 的 `EditSpec` 支持 DerivedOperation + PointSelectors
- 响应字段扩展：`Strategy` / `ResolvedPointIndices` / `DerivedOperationApplied` / `StrategyResolutionTrace`

依赖：子 PLAN B 合入。

#### 子 PLAN D：曲面 edit 复合

包含分子 **5（曲面分支扩展）**，以及分子 **2 / 4 的曲面分支扩展**；descriptor 两档体量（Summary / Full）由子 PLAN A 先闭环，本期直接复用并补足曲面 edit 编排。产出：

- `PreviewEditSurfaceGeometryTool` / `ApplyEditSurfaceGeometryTool`
- 复用 `IEditableGeometryDescriptorService` 的 Summary / Full descriptor 与 `ControlPointCentroidWorld`
- 扩 `IGeometryReconstructor` 支持曲面分支
- 扩 `IDerivedPointOperationEvaluator` 支持曲面 2D grid + `SurfacePointSelectorKind`
- 扩 `IGeometryEditStrategyResolver` / `IGeometryTransformExecutionBridge` 支持曲面 ExactTransform 分支

依赖：子 PLAN A、B、C 合入。

#### 子 PLAN E：Plan 4 专属分子 + 边界重建 Skill

包含 Plan 4 的 4 个专属分子：

- 参考边识别器
- LCS builder（基于分子 3）
- 点序规范化器
- 边界驱动重建器（4PointSurface + BoundarySurface 两条出口）

以及编排这些分子的 Skill `SurfacePointOrderRebuildSkill`，复用分子 6（属性保留）与分子 7（Mutation 边界）。

依赖：子 PLAN A、B 合入；子 PLAN D 非硬依赖，仅作为曲面 edit 能力完成后的顺序建议。Plan 4 子 PLAN 不得绕开分子 6/7 自建属性保留或替换路径。

### §5 与原 Wave 1/2/3 / Plan 4 的对应

| 原 PLAN | 被替代为 |
|---|---|
| `Project_Archive/260423_PLAN_geometry-edit-descriptor-lane.md` | 子 PLAN A（分子 1/3/6/8） + 子 PLAN B（曲线） |
| `Project_Archive/260423_PLAN_geometry-edit-derived-operations.md` | 子 PLAN C |
| `Project_Archive/260423_PLAN_geometry-edit-surface-lane.md` | 子 PLAN D |
| `Project_Archive/260423_PLAN_surface-point-order-redefinition.md` | 子 PLAN E（薄化至 Plan 4 独有部分） |

旧 PLAN 文件已移至 `Project_Archive/`，不删除以便回溯；子 PLAN 在各自"背景"章节显式声明替代关系。

## 涉及文件

**本 PLAN**

- `Project_Plan/260423_PLAN_geometry-edit-molecular-architecture.md`（本文件）

**后续子 PLAN（由本总纲落地后撰写）**

- `Project_Plan/260423_PLAN_geometry-edit-molecular-foundation.md`（子 PLAN A）
- `Project_Plan/260423_PLAN_geometry-edit-curve-composite.md`（子 PLAN B）
- `Project_Plan/260423_PLAN_geometry-edit-derived-routing.md`（子 PLAN C）
- `Project_Plan/260423_PLAN_geometry-edit-surface-composite.md`（子 PLAN D）
- `Project_Plan/260423_PLAN_surface-point-order-rebuild.md`（子 PLAN E；替换旧版 `redefinition` 文件，命名上与归档版区分）

**已归档的旧 PLAN**（由本总纲声明替代关系；文件保留以便回溯）

- `Project_Archive/260423_PLAN_geometry-edit-descriptor-lane.md`
- `Project_Archive/260423_PLAN_geometry-edit-derived-operations.md`
- `Project_Archive/260423_PLAN_geometry-edit-surface-lane.md`
- `Project_Archive/260423_PLAN_surface-point-order-redefinition.md`（旧独立栈版本；子 PLAN E 将重新在 `Project_Plan/` 下撰写）

## 使用方式

本 PLAN 是架构总纲文档，不直接产出代码。使用方式：

1. 用户评审 8 个分子的职责边界、暴露策略、子 PLAN 拆分方案
2. 确认后按子 PLAN 依赖顺序依次 PLAN → EXET → TEST；默认推进顺序为 A → B → C → D → E，若需优先落地 Plan 4，可在 A、B 闭环后单独评审 E
3. 每个子 PLAN 必须在"背景"章节引用本总纲作为架构依据
4. 子 PLAN 不得新增本 PLAN 未列出的"分子"；如确需扩展，先修本总纲
5. 扩字段（例如 descriptor 新增一个 warning code、EditSpec 新增一个参数）可在子 PLAN 内直接改；**新增分子必须回修总纲**

## 验收标准

- 本 PLAN 获用户确认；
- 5 份子 PLAN（A/B/C/D/E）草稿按本总纲中的分子划分依次撰写；
- 每个子 PLAN 的"架构归属"章节显式列出其实现的分子编号；
- 子 PLAN A/B/C/D 合入后，既有 `TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 无回归；
- 子 PLAN E 合入后，不依赖 EleFront / LunchBox / Pufferfish 即可运行 Plan 4 能力。

## 风险与回退方案

### 风险

- **分子粒度仍需调整**：实际 EXET 中可能发现某个分子边界不合理（例如分子 2 吞下 Selector 是否合适），需要在子 PLAN C 评审时复核
- **总纲 PLAN 冻结代价**：若子 PLAN 实施期间发现需要新增分子，必须回修本总纲，流程开销比直接在子 PLAN 中扩字段大
- **字段演进纪律**：descriptor / EditSpec / Response 字段会在 B/C/D 中持续扩展，需要明确"扩字段在子 PLAN 内直接改，新增分子必须回修总纲"的边界
- **既有 Wave 1/2/3 PLAN 处置**：三份旧 PLAN 未进入 EXET，以"由子 PLAN 替代、git 历史保留"方式处置；但需在子 PLAN 首部明确引用关系，避免文档漂移

### 缓解

- 本总纲明确列出"粒度可能调整"的候选（分子 2 的 Selector 吸收、分子 4 的决策 / 执行桥边界），便于后续子 PLAN 评审重点关注
- 每次分子变更必须修本总纲；不允许在子 PLAN 中偷偷扩分子
- 子 PLAN A 验收完毕后再启动 B；B 完毕后再启动 C，不允许跨序并行，以免分子契约不稳定

### 回退方案

- 若本总纲在评审阶段被推翻，回到原 Wave 1/2/3 + Plan 4 的 lane 划分，不影响任何已合入代码（因为本 PLAN 不直接改代码）
- 若子 PLAN A 合入后发现分子设计不适用，可在 B/C/D 启动前修订本总纲；已合入的基础层分子 Service 若仍可复用，保留；若需重构，通过新的子 PLAN 推进
- 本总纲本身不产代码，回退成本 = 删除本 PLAN 文件

## 后续扩展方向

- 若 LLM 实战出现"干跑推点"或"干跑选路"的稳定需求，追加 Tool 暴露分子 2 / 分子 4（低成本增量）
- 若出现新的重建出口（例如 Plan 4 之外的"多段曲线融合"），以"新增分子 + 新复合 Tool"方式扩展，不破坏既有 8 分子
- 若后续需要跨能力的 Skill 编排（批量 descriptor + 批量 preview + 批量 apply），以 Skill 层编排分子即可，不需在分子层增新
- Mesh / SubD / 非 NURBS 对象的 descriptor-first 支持需要新增独立分子（因为结构完全不同），建议另立总纲 PLAN
- 若 `TransformObjects` / `EditControlPoints` 等既有"命令式"Tool 在分子化后与分子 4 的 ExactTransform 执行桥产生功能重叠，评估是否由分子 4 的 resolver / execution bridge 体系统一代理；该评估属独立 PLAN 范围，不在本总纲内
