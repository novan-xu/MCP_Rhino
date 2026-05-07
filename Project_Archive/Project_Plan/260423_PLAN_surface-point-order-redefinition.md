# 背景

用户的目标不是在 MCP_Rhino 内直接运行 Grasshopper，而是：

1. 先从既有 `.gh` 文件中识别出真实业务管线；
2. 再把这条管线翻译成 MCP_Rhino 内可维护、可测试、可 preview/apply 的 `Skill` 规划。

本次样本为 `Runtime_Test/REG_Redefine Point Order.gh`。该文件是 Grasshopper 二进制定义，已通过 `GH_IO` 成功离线解析。当前可确认的关键结构如下：

- `DefinitionObjects.ObjectCount = 177`
- 主流程分组：
  - `Select Bottom Curve for LCS Origin`
  - `Create Local Coordinate System`
  - `Sort Points with Guide Circle`
  - `Create New Surfaces with Correct Point Order`
  - `Bake New Surfaces w Existing Attributes I`
  - `Multi-Sided Srfs`
- 第三方依赖信号：
  - `LunchBox`
  - `EleFront`
  - `Pufferfish`
  - `treesloth01`
- 关键构件信号：
  - 参考识别：`Deconstruct Brep`、`Point On Curve`、`Surface Closest Point`、`Area`
  - 局部坐标系：`Construct Plane`、`Rotate`、`Unit Z`、`Evaluate Surface`
  - 排序：`Curve Closest Point`、`Circle`、`Sort List`
  - 四角面重建：`4Point Surface`
  - 多边面重建：`PolyLine`、`Boundary Surfaces`
  - 属性回写：`Get Rhino Attributes`、`Get User Attributes`、`Object Bake`

从这些分组与组件组合可以稳定抽出一条统一 pipeline：

- 参考边识别
- 局部坐标系建立
- 点序重排
- 按拓扑类型分流重建
- 保留原对象属性

其中，四角面和多边面的差异只发生在“重建出口”，而不是前面的排序逻辑。也就是说，这个 GH 文件已经给出了一个清晰的设计方向：

- **排序规则应统一**
- **四角面与多边面只在最终几何构造方式上分流**

因此，MCP_Rhino 里应构建的不是“4Point Surface Tool”，而是一个更高阶的固定流程 `Skill`：

- 先分析目标面
- 再建立参考与排序基准
- 再生成规范化有序点列
- 最后根据点数与拓扑选择四角面或多边面重建策略

# 目标

- 新增一个以 GH 样本 pipeline 为蓝本的固定流程能力，用于**重新定义曲面边界点序并重建几何**。
- 该能力同时覆盖：
  - 四角面
  - 多边面
- 两类对象共享同一套前置分析与排序规则：
  - 参考边识别
  - 局部坐标系建立
  - 点列规范化
  - 排序方向控制
  - 起点锚定规则
- 四角面与多边面的唯一区别在最终重建出口：
  - 四角面：优先走四点重建路径
  - 多边面：走“有序点列 -> 闭合边界 -> Boundary Surface”路径
- Preview 必须完整暴露排序依据，避免“黑箱式重建”
- Apply 必须保留原对象主要属性，并遵守现有 Live mutation / Undo 规则

明确非目标：

- 不在 server 内直接运行 `Grasshopper.dll`
- 不把 `EleFront` / `LunchBox` / `Pufferfish` 作为服务端运行时硬依赖
- 不做“任意 `.gh` 自动转 Skill”的通用框架
- 不在本期处理 Mesh / SubD / BlockInstance / 非面对象的重排序重建
- 不在本期覆盖所有极端退化拓扑；优先保证规则清晰、行为稳定、错误可解释

# 架构归属

- **Tools/Geometry/**
  - `InspectSurfaceRebuildDescriptorTool`
  - `PreviewRedefineSurfacePointOrderTool`
  - `ApplyRedefineSurfacePointOrderTool`
- **Skills/Modeling/**
  - `SurfacePointOrderRebuildSkill`
- **Application/Services/**
  - `RhinoSurfacePointOrderService`
  - `SurfaceOrderingRuleService`
  - `SurfaceRebuildRoutingService`
- **Application/Interfaces/**
  - `ISurfaceDescriptorAnalyzer`
  - `ISurfaceOrderingRuleEvaluator`
  - `ISurfaceRebuildPlanner`
  - `ISurfaceRebuilder`
  - `ISurfacePointOrderValidator`
- **Domain/Models**
  - `SurfaceRebuildDescriptor`
  - `SurfaceBoundaryLoopDescriptor`
  - `SurfaceReferenceCurveSpec`
  - `OrderedSurfacePoint`
  - `SurfacePointOrderPlan`
  - `SurfaceRebuildSpec`
  - `SurfaceAttributeCarryOverSpec`
- **Domain/Enums**
  - `SurfacePointOrderGuideMode`
  - `SurfacePointOrderDirection`
  - `SurfaceTopologyKind`
  - `SurfaceRebuildRouteKind`
- **Contracts/Requests**
  - `InspectSurfaceRebuildDescriptorRequest`
  - `PreviewRedefineSurfacePointOrderRequest`
  - `ApplyRedefineSurfacePointOrderRequest`
- **Contracts/Responses**
  - `SurfaceRebuildDescriptorResponse`
  - `SurfacePointOrderPreviewResponse`
  - `SurfacePointOrderApplyResponse`
- **Infrastructure/Rhino/Live/**
  - `LiveSurfaceDescriptorAnalyzer`
  - `LiveSurfaceRebuilder`
  - `LiveSurfaceAttributeReader`
  - `LiveSurfaceAttributeWriter`
- **Server/**
  - `DependencyInjection.cs` 注册 analyzer / evaluator / planner / rebuilder / service
  - `AgentRegistration.cs` 注册 `SurfacePointOrderRebuildSkill`

结论：这是一个**固定流程 Skill + 3 个 MCP Tool 包装**的问题，不需要新增顶层 Agent。

# 关键设计

## 1. 技术路线：翻译 GH 语义，不托管 GH 运行时

GH 样本已经足够说明业务流程，但不适合作为 MCP server 内部执行引擎。原因很直接：

- 运行时依赖 Rhino + Grasshopper + 第三方插件版本，部署脆弱
- 不容易并入现有 `Preview -> Apply -> Undo` 语义
- 业务规则无法在 C# 域模型中沉淀为稳定接口与可测试逻辑

因此本期的正确方向是：

- **把 GH 文件视为工作流蓝图**
- **把工作流蓝图翻译成 MCP_Rhino 的 Skill**

## 2. 从 GH 样本抽出的统一 pipeline

按 GH 分组，可以稳定归纳出以下执行链：

1. 目标对象解析
2. 候选边界/顶点/参考边抽取
3. 底部参考边识别
4. 局部坐标系构造
5. 生成导向曲线 / 参考圆 / 参数化排序基准
6. 计算有序点列
7. 依据点数与拓扑选择重建出口
8. 保留属性并回写

该链路中，步骤 1-6 对四角面和多边面完全通用。

## 3. 统一排序规则：先决定参考，再决定方向，再决定起点

本期最重要的不是“怎么造面”，而是“如何稳定地把点列排序成预期顺序”。建议把 GH 暴露出来的规则抽象成以下三层。

### 3.1 参考边识别

默认优先级：

1. 用户显式指定 `ReferenceCurveObjectId`
2. 用户显式指定 `ReferenceEdgeIndex`
3. 自动识别“底部参考边”

自动识别“底部参考边”时，优先使用：

- 边界候选中在局部 `-Z` 或最低标高方向最稳定的边
- 若多个候选并列，则取长度更长、与主方向更一致者
- 若仍歧义，则返回 `REFERENCE_CURVE_AMBIGUOUS`

这对应 GH 中的 `Select Bottom Curve for LCS Origin`。

### 3.2 局部坐标系构造

由以下信号共同构造局部平面：

- 参考边中点或起点作为原点候选
- 面的法向或 `Surface Closest Point` / `Evaluate Surface` 返回的局部 frame
- `Area` / centroid 作为辅助定向参考

构造结果必须产出：

- `Origin`
- `XAxis`
- `YAxis`
- `Normal`

如果局部 frame 不稳定或退化，返回 `DEGENERATE_LOCAL_FRAME`。

这对应 GH 中的 `Create Local Coordinate System`。

### 3.3 点列排序

在局部平面中对候选点统一做 2D 归一化处理，然后生成排序 key。

建议排序逻辑如下：

1. 先确定排序方向
   - `Clockwise`
   - `CounterClockwise`
2. 再确定起点锚定
   - 默认锚定在参考边起点最近的排序点
   - 若参考边起点不可稳定映射，则锚定到参考边中点投影角度最小点
3. 再做循环展开
   - 将环形点列旋转到统一起点
   - 输出规范化顺序

排序 key 的推荐组成：

- 首键：相对局部原点或导向圆的极角
- 次键：参考曲线参数或沿参考边的投影参数
- 末键：与参考边的距离，用于打破角度相近时的歧义

这对应 GH 中的 `Sort Points with Guide Circle`。

## 4. 统一规则下的两条重建出口

### 4.1 四角面出口

若最终规范化有序点列满足四角面要求：

- 点数 = 4
- 无重复角点
- 边界无自交
- 四边拓扑可稳定解释

则走四角面重建出口：

- 直接使用四个角点按**规范化顺序**构造曲面
- 该顺序由统一排序规则给出，不再单独发明四角面特例规则

这对应 GH 中的 `Create New Surfaces with Correct Point Order -> 4Point Surface`。

### 4.2 多边面出口

若最终有序点列点数 > 4，或拓扑明确属于多边面，则走多边面出口：

- 按同一规范化顺序生成闭合 polyline / boundary curve
- 用闭合边界做 `BoundarySurface`
- 如有内环，按外环/内环分别排序并保留环方向约束

这里的关键约束是：

- **多边面不是另一套排序规则**
- **只是把同一份有序点列交给另一种几何构造器**

这对应 GH 中的 `Multi-Sided Srfs -> PolyLine -> Boundary Surfaces`。

## 5. 四角面规则必须向多边面继承，而不是相反

用户明确要求“尽量套用从 GH 文件里分析出的四角面构建规则”。这件事在设计上应这样落实：

- 先把四角面中已经清晰的规则抽象成**通用边界排序规则**
- 再让多边面复用这套通用规则

而不是：

- 先设计一个抽象多边形算法
- 再让四角面去适配

原因是 GH 样本中最清晰、最强约束的部分恰恰是四角面路径。多边面路径更像是“在同一排序规则上，换一个重建器”。

因此本期的规则中心应当是：

- 参考边驱动
- 局部坐标系驱动
- 起点锚定
- 环向一致

这四条既适用于 4 点，也适用于 n 点。

## 6. 预览必须输出“排序计划”，不能只输出结果几何

建议增加一个只读 descriptor tool：

- `InspectSurfaceRebuildDescriptor`

用于先返回：

- 对象拓扑类型
- 候选边界环数量
- 候选点列表
- 自动识别到的参考边
- 局部坐标系
- 推荐起点
- 推荐方向
- 建议重建出口

然后 Preview 再在此基础上返回：

- 原始点顺序
- 规范化点顺序
- 四角面或多边面分流结果
- 预期重建曲线/曲面摘要
- warning / skip reason

如果 Preview 不能把“为什么排成这个顺序”讲清楚，就不应允许 Apply。

## 7. 属性保留不依赖 EleFront，而依赖现有 Rhino 属性链

GH 文件里 `Bake New Surfaces w Existing Attributes I` 说明用户在意的不只是几何，还包括原对象属性延续。MCP_Rhino 里应原生支持：

- Layer
- Name
- Display Color
- User Strings

首期不强求完全复制 EleFront 的所有 attribute 行为，但必须明确保留：

- 几何替换前后的对象识别信息
- 最关键的可见属性
- 用户附加 metadata

若部分属性无法保真，返回 `ATTRIBUTE_COPY_PARTIAL`。

## 8. 请求模型建议

### 8.1 Inspect

- `FilePath`
- `ConfirmedObjectIds`
- 可选 `ReferenceCurveObjectId`
- 可选 `ReferenceEdgeIndex`

### 8.2 Preview / Apply

- `FilePath`
- `ConfirmedObjectIds`
- `GuideMode`
- `Direction`
- 可选 `ReferenceCurveObjectId`
- 可选 `ReferenceEdgeIndex`
- 可选 `StartAnchorMode`
- 可选 `ReplaceOriginal`

如后续需要支持筛查入口，再复用：

- `LayerQueries`
- `ConfirmedLayerFullPaths`
- `ObjectTypes`
- `UserAttributeConditions`

但首期 smoke 和主交互建议优先使用显式对象 Id，避免额外歧义。

## 9. 错误与 warning 设计

建议至少定义这些稳定 code：

- `REFERENCE_CURVE_AMBIGUOUS`
- `REFERENCE_CURVE_REQUIRED`
- `DEGENERATE_LOCAL_FRAME`
- `INSUFFICIENT_ORDERABLE_POINTS`
- `POINT_ORDER_UNCHANGED`
- `SELF_INTERSECTING_BOUNDARY`
- `UNSUPPORTED_TOPOLOGY`
- `BOUNDARY_SURFACE_FAILED`
- `ATTRIBUTE_COPY_PARTIAL`

对于 Preview，不要只返回“失败”，而要尽量返回：

- 当前识别到哪一步
- 为什么无法继续
- 用户需要补什么显式输入

# 涉及文件

- `Project_Plan/260423_PLAN_surface-point-order-redefinition.md`
- `src/MCP_Rhino.Server/Tools/Geometry/InspectSurfaceRebuildDescriptorTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/PreviewRedefineSurfacePointOrderTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/ApplyRedefineSurfacePointOrderTool.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/SurfacePointOrderRebuildSkill.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoSurfacePointOrderService.cs`
- `src/MCP_Rhino.Server/Application/Services/SurfaceOrderingRuleService.cs`
- `src/MCP_Rhino.Server/Application/Services/SurfaceRebuildRoutingService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceDescriptorAnalyzer.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceOrderingRuleEvaluator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceRebuildPlanner.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceRebuilder.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfacePointOrderValidator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceDescriptorAnalyzer.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceRebuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceAttributeReader.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceAttributeWriter.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceRebuildDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceBoundaryLoopDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceReferenceCurveSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/OrderedSurfacePoint.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfacePointOrderPlan.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceRebuildSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceAttributeCarryOverSpec.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointOrderGuideMode.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfacePointOrderDirection.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfaceTopologyKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfaceRebuildRouteKind.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/InspectSurfaceRebuildDescriptorRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewRedefineSurfacePointOrderRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ApplyRedefineSurfacePointOrderRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SurfaceRebuildDescriptorResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SurfacePointOrderPreviewResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/SurfacePointOrderApplyResponse.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `Project_Test/260423_TEST_surface-point-order-redefinition/`
- `Project_Exet/260423_EXET_surface-point-order-redefinition.md`

# 使用方式

## MCP Tool

检查 descriptor：

- `InspectSurfaceRebuildDescriptor(filePath, confirmedObjectIds, referenceCurveObjectId?, referenceEdgeIndex?)`

预览：

- `PreviewRedefineSurfacePointOrder(filePath, confirmedObjectIds, guideMode, direction, referenceCurveObjectId?, referenceEdgeIndex?, startAnchorMode?)`

应用：

- `ApplyRedefineSurfacePointOrder(filePath, confirmedObjectIds, guideMode, direction, referenceCurveObjectId?, referenceEdgeIndex?, startAnchorMode?, replaceOriginal=true)`

## 典型场景

- 对四角面：
  - 自动识别底部参考边
  - 建立局部坐标系
  - 输出四角点规范化顺序
  - 预览后按四点曲面重建

- 对多边面：
  - 使用同一参考边与同一方向规则
  - 生成有序环点列
  - 构造闭合 polyline
  - 预览后按 boundary surface 重建

- 对批量对象：
  - 先用 descriptor 看哪些对象能稳定自动识别
  - 对歧义对象显式补参考边
  - 再统一 preview/apply

# 验收标准

- `PLAN` 获用户确认后，方可进入 Execute 阶段
- 实现阶段 `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过
- Live smoke 至少覆盖以下分支：
  - 四角面自动参考边 -> Preview -> Apply
  - 多边面自动参考边 -> Preview -> Apply
  - 显式参考边覆盖自动参考边
  - 歧义参考边返回稳定错误
  - Preview 前后对象数不变、Undo 无新增
  - Apply 后新增 1 条 Undo 记录
  - Apply 后对象保留 Layer / Name / Color / UserStrings
- 四角面与多边面在同一输入规则下表现一致：
  - 相同方向参数
  - 相同起点锚定规则
  - 相同 reference override 机制
- 不要求安装 `EleFront` / `LunchBox` / `Pufferfish` 即可运行最终能力

# 风险与回退方案

## 风险

- **GH 语义并非完全显式**：分组名和组件组合能说明流程，但不能百分之百还原每一个细节
- **参考边自动识别存在歧义**：特别是在高度接近、边长接近或法向翻转的对象上
- **多边面比四角面更容易出现边界自交与退化**
- **Boundary surface 路径对环顺序和闭合质量更敏感**
- **属性保留范围若定义不清，用户会拿 EleFront 结果作对照**

## 缓解

- 增加 `InspectSurfaceRebuildDescriptor`
- Preview 强制输出排序计划而不是只输出重建结果
- 将“参考边 override”设计成一等输入
- 统一排序规则，减少四角面/多边面双轨漂移
- smoke 测试中同时准备四角面与多边面样本

## 回退方案

- 若自动参考边策略不稳定，可临时收紧为必须显式指定参考边
- 若多边面路径在首轮实现中不稳定，可临时保留 descriptor + preview，Apply 先只开放四角面
- 本能力为新增文件集，可独立 `git revert`

# 后续扩展方向

- 支持带内环的多边面重建
- 支持“保留原修剪边界”与“重建为无修剪规范面”两种模式
- 支持批量对象按 user attribute 自动选取参考边规则
- 沉淀独立 `GH definition analyzer` 能力，用于把 `.gh/.ghx` 解析成“候选 Skill 规划提示”，但这应是另一项独立能力，不与本次 surface point order skill 混做一个交付
