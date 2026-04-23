# 背景

当前 MCP_Rhino 已经具备筛查、属性编辑（user text / 图层 / 显示颜色）、文档级 user string 读写、文件修改保护与归档快照等能力，但仍缺少**几何本身**的创建与修改入口。具体缺口：

- 无法在 `.3dm` 文件中新增 **点 / 线 / 圆弧 / 曲面** 等原子几何。
- 无法对既有对象执行 **平移 / 旋转 / 缩放**、**按 GUID 替换几何**、**删除**、**控制点编辑** 等修改。

Rhino3dm v8.17.0 已经暴露所需底层 API：

- `Rhino.Geometry.Point` / `Point3d`
- `Rhino.Geometry.Line` / `LineCurve`
- `Rhino.Geometry.Arc` / `ArcCurve` / `Circle` / `Plane` / `Interval`
- `Rhino.Geometry.NurbsSurface.CreateFromCorners(...)` / `PlaneSurface`
- `Rhino.Geometry.Transform.Translation / Rotation / Scale`
- `File3dm.Objects.Add(geometry, attributes)` / `Delete(guid)`
- `NurbsCurve.Points.SetPoint(...)` / `NurbsSurface.Points.SetPoint(u,v,...)`

用户需求：MCP 能够以单条或批量模式创建与修改上述几何，以便下游 Agent / MCP Client 端到端编排 Rhino 建模流程。

说明：用户最初引用的 RhinoCommon 文档所描述的 API 大部分与 Rhino3dm 共享 `Rhino.Geometry.*` 命名空间；但涉及运行中 Rhino 进程的能力（Loft / Sweep / Revolve / Brep 布尔 / Redraw）需要 RhinoCommon 或 RhinoInside，本次不纳入范围，保持当前 Rhino3dm 文件级架构。

# 目标

- 新增 **几何创建** 工具：Point / Line / Arc / Surface，支持单条与批量。
- 新增 **几何修改** 工具：Transform（Translate / Rotate / UniformScale）、ReplaceGeometry、DeleteObjects、EditControlPoints。
- `TransformObjects` / `DeleteObjects` 支持两种选择源：**显式 `ConfirmedObjectIds`** 或 **现有筛查条件**（layer / objectType / user attribute）。
- `ReplaceGeometry` / `EditControlPoints` 采用 **按 entry 定点目标** 模式；每个 entry 必须显式提供 `ObjectId`，不复用筛查条件。
- 仅对 `TransformObjects` / `DeleteObjects` 适用：当 `ConfirmedObjectIds` 与筛查条件同时提供时，以 `ConfirmedObjectIds` 为准，并在响应 `Warnings` 中写入“筛查条件已被忽略”提示。`ReplaceGeometry` / `EditControlPoints` 因每条 entry 自带 `ObjectId`，不涉及此指南。
- 所有修改工具（`TransformObjects` / `DeleteObjects` / `ReplaceGeometry` / `EditControlPoints`）均提供配套 `Preview*` 工具，返回受影响对象列表与校验 warning，不落盘；Create 系工具不设 Preview。
- `EditControlPoints` 首期仅支持 `NurbsCurve` / `NurbsSurface`，不对 `LineCurve` / `ArcCurve` / `PlaneSurface` 做隐式 NURBS 转换。
- 复用现有 `IFileMutationSafeguard`、归档快照、结果格式化管线，不新增写保护机制。
- 严格遵守 `.clinerules/MCP_Rhino Architecture.md`，不在 Tool / Skill 中堆积 Rhino3dm 细节。

# 架构归属

- **Tools/Geometry/**
  - 面向 MCP Client 的 12 个原子工具：
    - 创建（4）：`CreatePointsTool` / `CreateLinesTool` / `CreateArcsTool` / `CreateSurfacesTool`
    - 修改-Apply（4）：`TransformObjectsTool` / `ReplaceGeometryTool` / `DeleteObjectsTool` / `EditControlPointsTool`
    - 修改-Preview（4）：`PreviewTransformObjectsTool` / `PreviewReplaceGeometryTool` / `PreviewDeleteObjectsTool` / `PreviewEditControlPointsTool`
  - Tool 只负责接参、组装 Request DTO、调用 Skill、返回 `OperationResponse<T>`。
- **Skills/Modeling/**
  - `GeometryCreationSkill`：按 primitive 分派到创建 Service。
  - `GeometryModificationSkill`：
    - `TransformObjects` / `DeleteObjects`：若提供 `ConfirmedObjectIds`，直接按 GUID 解析目标对象；否则把请求中的 layer / objectType / user attribute 字段映射为 `FilterObjectsRequest`，委派给 `ObjectSelectionSkill.Select` 得到目标对象集合。
    - `ReplaceGeometry` / `EditControlPoints`：逐条处理 entry 中显式给出的 `ObjectId`。
    - Preview 变体复用相同目标解析逻辑，但仅走 Service 的 `Preview` 路径，不落盘。
- **Application/Services/**
  - `RhinoGeometryCreationService`：统筹校验、写保护、构建、落盘、结果汇总。
  - `RhinoGeometryModificationService`：统筹目标对象解析、校验、修改、落盘、结果汇总；暴露 `Preview(...)` 与 `Apply(...)` 两个方法，镜像 `RhinoObjectEditingService` 的 preview/apply 分离骨架。
- **Application/Interfaces/**
  - `IGeometryBuilder`：从 Spec 构建 `GeometryBase`。
  - `IGeometryMutator`：在 `File3dm` 上执行 Transform / Replace / Delete / EditControlPoints。
  - `IGeometryValidator`：校验创建 Spec、修改请求与目标对象适配性，输出硬错误与软警告。方法签名：
    - `Validate(GeometryCreationSpec spec, GeometryObjectAttributesSpec attributes)` → `OperationResponse<IReadOnlyList<ObjectEditWarning>>`
    - `Validate(GeometryTransformSpec spec, IReadOnlyList<RhinoObjectInfo> targets)` → `OperationResponse<IReadOnlyList<ObjectEditWarning>>`
    - `Validate(GeometryReplacementSpec spec, RhinoObjectInfo target)` → `OperationResponse<IReadOnlyList<ObjectEditWarning>>`
    - `Validate(ControlPointEditSpec spec, RhinoObjectInfo target)` → `OperationResponse<IReadOnlyList<ObjectEditWarning>>`
  - `IEditResultFormatter` 扩展：新增 `FormatGeometryCreation(GeometryCreationResponse)` / `FormatGeometryModification(GeometryModificationResponse)` / `FormatGeometryModificationPreview(GeometryModificationPreviewResponse)`；实现由既有 `PassThroughEditResultFormatter` 承担。
- **Infrastructure/Rhino/**
  - `RhinoGeometryBuilder` / `RhinoGeometryMutator` / `RhinoGeometryValidator`：所有 `new Point3d` / `new LineCurve` / `NurbsSurface.CreateFromCorners` / `Transform.Translation` / `model.Objects.Add / Delete` 集中在这里。
- **Domain/Enums/**
  - `GeometryPrimitiveKind`：Point / Line / Arc / Surface。
  - `ArcConstructionMode`：ThreePoint / CenterRadius。
  - `SurfaceConstructionMode`：FourCorners / Plane。
  - `GeometryTransformKind`：Translate / Rotate / UniformScale。
  - `ControlPointTargetMode`：CurveIndex / SurfaceUV。
- **Domain/Models/**
  - `GeometryCreationSpec`：纯数据几何创建规格。
  - `GeometryObjectAttributesSpec`：纯数据对象属性规格；承载 `LayerFullPath / Color / UserText / Name`，由 `GeometryCreationCommonOptions` 映射而来，供 validator / service 校验与落盘复用。
  - `GeometryTransformSpec`：纯数据变换规格；字段显式包含：
    - Translate：`VectorX / VectorY / VectorZ`
    - Rotate：`CenterX / CenterY / CenterZ`、`AxisX / AxisY / AxisZ`、`AngleRadians`
    - UniformScale：`CenterX / CenterY / CenterZ`、`ScaleFactor`
  - `GeometryReplacementSpec`：`ObjectId + GeometryCreationSpec`
  - `ControlPointEditSpec`：`ObjectId + TargetMode(CurveIndex | SurfaceUV) + PointIndex? + UIndex? + VIndex? + X/Y/Z + Weight?`
- **Contracts/Requests/**
  - 12 个 `*Request`：
    - 创建：`CreatePointsRequest` / `CreateLinesRequest` / `CreateArcsRequest` / `CreateSurfacesRequest`
    - 修改-Apply：`TransformObjectsRequest` / `ReplaceGeometryRequest` / `DeleteObjectsRequest` / `EditControlPointsRequest`
    - 修改-Preview：`PreviewTransformObjectsRequest` / `PreviewReplaceGeometryRequest` / `PreviewDeleteObjectsRequest` / `PreviewEditControlPointsRequest`（字段与对应 Apply Request 一致）
  - `TransformObjectsRequest`：`filePath + transform + confirmedObjectIds? + layerQueries? + confirmedLayerFullPaths? + objectTypes? + userAttributeConditions? + matchMode? + userAttributeMatchMode?`
  - `DeleteObjectsRequest`：`filePath + confirmedObjectIds? + layerQueries? + confirmedLayerFullPaths? + objectTypes? + userAttributeConditions? + matchMode? + userAttributeMatchMode?`
  - `ReplaceGeometryRequest`：`filePath + entries`
  - `EditControlPointsRequest`：`filePath + entries`
  - 条目 DTO：`PointItemRequest` / `LineItemRequest` / `ArcItemRequest` / `SurfaceItemRequest` / `GeometryReplacementEntryRequest` / `ControlPointEditEntryRequest`。
  - `GeometryReplacementEntryRequest`：`ObjectId + GeometryCreationSpec`
  - `ControlPointEditEntryRequest`：`ObjectId + TargetMode + PointIndex? + UIndex? + VIndex? + X/Y/Z + Weight?`
  - 公共：`GeometryCreationCommonOptions`（LayerFullPath / Color / UserText / Name）。由 `GeometryCreationSkill` 在调用 `RhinoGeometryCreationService` 之前映射为 `GeometryObjectAttributesSpec`；Tool 层仅透传 DTO，Infrastructure 层仅消费 Spec，映射逻辑不下沉也不上浮。
- **Contracts/Responses/**
  - `GeometryCreationResponse`：FilePath / RequestedCount / CreatedCount / CreatedObjects / Warnings。
  - `GeometryModificationResponse`：字段与 `ObjectEditExecutionResponse` 对齐（FilePath / CriteriaSummary / MatchedObjectCount / UpdatedObjectCount / FailedObjectCount / OperationCount / Warnings / ObjectResults），便于 MCP Client 单份解析逻辑同时消费两类响应。
  - `GeometryModificationPreviewResponse`：字段对齐 `ObjectEditPreviewResponse`（FilePath / CriteriaSummary / MatchedObjectCount / PreviewObjectCount / OperationCount / Warnings / ObjectResults），最多预览 20 条与现有 `PreviewLimit` 一致。
- **Server/**
  - `DependencyInjection.cs`：注册 3 个接口 + 2 个 Service。
  - `AgentRegistration.cs`：注册 2 个 Skill。
  - `ToolRegistration.cs`：无需改动（反射自动发现）。

# 关键设计

1. **创建按 primitive 分 Tool，不做万能 Tool**
   - 参照现有 `GetObjectUserStringsTool` / `DeleteObjectUserTextTool` 等原子 Tool 的命名与粒度。
   - 每个 primitive 参数形状差异大，分 Tool 可让 MCP Client 看到更清晰的 schema。

2. **修改不复用 `ObjectEditOperationType` 枚举**
   - 该枚举语义为“属性编辑”，现有 applier 永不改动 `Geometry`。
   - Transform / Replace / Delete / EditControlPoints 若挤进该枚举，会让所有属性编辑请求携带空字段，违反单一职责。
   - 因此单独实现 `IGeometryMutator` + `RhinoGeometryModificationService`。

3. **选择源按操作类型拆分**
   - `TransformObjects` / `DeleteObjects` 支持两种选择源：
     - 显式 `ConfirmedObjectIds: List<Guid>`：精准，推荐用于脚本化操作。
     - 筛查字段（复用 `ObjectSelectionSkill`）：用于“所有墙体层的曲线都平移 (0,0,100)”等语义选择。
   - `ReplaceGeometry` / `EditControlPoints` 不走筛查选择；每个 entry 必须自带 `ObjectId`。
   - **仅对 `TransformObjects` / `DeleteObjects`**：两种选择源同时提供时以 `ConfirmedObjectIds` 为准，并写入 warning；`ReplaceGeometry` / `EditControlPoints` 因每条 entry 自带 `ObjectId`，不触发此指南。
   - 对于显式 `ConfirmedObjectIds` 或 entry 级 `ObjectId`，若存在无法解析的 `ObjectId`，视为硬错误，阻断写入。

4. **修改沿用 delete + re-add**
   - Rhino3dm 的对象修改统一走“复制属性 → 删除原对象 → 按新几何 Add”。
   - `RhinoObjectEditOperationApplier` 已经是此模式，`RhinoGeometryMutator` 以相同契约实现，保证读者心智一致。
   - `EditControlPoints` 虽然 `NurbsCurve.Points.SetPoint` / `NurbsSurface.Points.SetPoint` 是 in-place 操作，但仍遵循 delete+re-add：`原对象 Duplicate → 在副本上 SetPoint → Delete 原 → Add 副本`，不直接修改 `model.Objects` 中的现存对象。

4a. **选择源适配**
   - `GeometryModificationSkill` 负责把 `TransformObjectsRequest` / `DeleteObjectsRequest` 中的 layer / objectType / userAttribute 字段组装成 `FilterObjectsRequest`，委派给 `ObjectSelectionSkill.Select` 得到目标对象集合；避免两处维护筛查解析逻辑。
   - `TransformObjectsRequest` / `DeleteObjectsRequest` 因此增加 `confirmedLayerFullPaths?` 与 `userAttributeMatchMode?` 字段（与 `FilterObjectsRequest` 对齐），以覆盖图层歧义确认的往返流程。

4b. **Replace 类型兼容策略**
   - `ReplaceGeometry` 的兼容指南基于现有 `RhinoObjectType` 大类，而不是新 Spec 的具体 primitive 名：
     - `RhinoObjectType.Point`：仅允许替换为 `GeometryPrimitiveKind.Point`
     - `RhinoObjectType.Curve`：允许替换为 `GeometryPrimitiveKind.Line` 或 `GeometryPrimitiveKind.Arc`
     - `RhinoObjectType.Surface`：仅允许替换为 `GeometryPrimitiveKind.Surface`
   - `RhinoObjectType.Brep` / `Mesh` / `Annotation` / `BlockInstance` 等首期不支持 `ReplaceGeometry`；命中即硬错误。
   - 若需要跨大类变更类型，应先 `DeleteObjects` 再执行对应 `Create*` 工具。

4c. **Formatter 对接**
   - 扩展 `IEditResultFormatter`：`FormatGeometryCreation(GeometryCreationResponse)` / `FormatGeometryModification(GeometryModificationResponse)` / `FormatGeometryModificationPreview(GeometryModificationPreviewResponse)`。
   - 由既有 `PassThroughEditResultFormatter` 实现，沿用其 StringBuilder + markdown 风格；无需新增 formatter 类。

5. **写保护与归档复用**
   - `RhinoGeometryCreationService` 与 `RhinoGeometryModificationService.Apply` 复用 `RhinoObjectEditingService.Apply` 的主结构：
     - `Exists` → `Read` → `Validator.Validate` → `FileMutationSafeguard.BeforeOverwrite` → 构建/修改 → `Repository.Write` → `FileMutationSafeguard.AfterOverwrite`。
   - `RhinoGeometryModificationService.Preview` 仅复用前半段：`Exists` → `Read` → `Validator.Validate` → 结果汇总；不触发 `FileMutationSafeguard`，不落盘。
   - 不新增写保护、不绕过归档快照。

6. **Transform 参数显式化**（字段名与 `GeometryTransformSpec`、`GeometryTransformKind` 一一对应）
   - Translate：`VectorX / VectorY / VectorZ`，三维向量，不接受额外参数。
   - Rotate：`CenterX / CenterY / CenterZ` + `AxisX / AxisY / AxisZ` + `AngleRadians`；角度单位统一为 `radians`。
   - UniformScale：`CenterX / CenterY / CenterZ` + `ScaleFactor`；首期仅支持均匀缩放，不支持非均匀缩放。

7. **Rhino3dm API 映射（只在 Infrastructure 层出现）**
   - Point：`new Point(new Point3d(x,y,z))`
   - Line：`new LineCurve(new Line(start, end))`
   - Arc 三点：`new ArcCurve(new Arc(p1, p2, p3))`
   - Arc 圆心半径：`new ArcCurve(new Arc(new Circle(new Plane(center, normal), radius), new Interval(startRad, endRad)))`
   - Surface 四角：`NurbsSurface.CreateFromCorners(c0, c1, c2, c3)`
   - Surface 平面：`new PlaneSurface(new Plane(origin, normal), new Interval(0, u), new Interval(0, v))`
   - Transform：`geometry.Duplicate(); geometry.Transform(Transform.Translation / Rotation / Scale(...))`
     - `GeometryTransformKind.UniformScale` 映射到 `Transform.Scale(Plane, scaleFactor)`，x/y/z 使用同一 `scaleFactor` 表达各向同性缩放；枚举名与 API 方法名刻意分离（枚举承担域语义，API 承担调用形式）。
   - 落盘：`model.Objects.Add(geometry, attributes)`
   - 删除：`model.Objects.Delete(guid)`

8. **校验策略**
   - 硬错误（阻断写入）：`filePath` 不存在、NaN / Inf 坐标、零长度线、三点共线 Arc (`Arc.IsValid == false`)、半径 ≤ 0、法向量为零、退化曲面、缩放因子 ≈ 0、`LayerFullPath` 不存在、显式 `ObjectId` 无法解析、控制点索引越界、`EditControlPoints` 目标对象不是 `NurbsCurve` / `NurbsSurface`、`ReplaceGeometry` 目标对象类型不在支持矩阵内，或与新 `GeometryPrimitiveKind` 不兼容。
   - 软警告（写入响应 Warnings）：bbox 对角线 > 1e12（疑似单位不匹配）、批量条目 > 10000、`TransformObjects` / `DeleteObjects` 同时提供 `ConfirmedObjectIds` 与筛查条件时筛查条件被忽略。

9. **响应结构对齐现有编辑管线**
   - `GeometryCreationResponse` / `GeometryModificationResponse` 尽量复用 `ObjectEditWarning` 与同风格字段，保证 MCP Client 只维护一份响应解析逻辑。

# 涉及文件

- `Project_Plan/260420_PLAN_geometry-create-modify-tools.md`
- `src/MCP_Rhino.Server/Tools/Geometry/CreatePointsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateLinesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateArcsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/CreateSurfacesTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/TransformObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/ReplaceGeometryTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/DeleteObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/EditControlPointsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/PreviewTransformObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/PreviewReplaceGeometryTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/PreviewDeleteObjectsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/PreviewEditControlPointsTool.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/GeometryCreationSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/GeometryModificationSkill.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoGeometryCreationService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoGeometryModificationService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryBuilder.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryMutator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryValidator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoGeometryBuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoGeometryMutator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoGeometryValidator.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryPrimitiveKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ArcConstructionMode.cs`
- `src/MCP_Rhino.Server/Domain/Enums/SurfaceConstructionMode.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryTransformKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ControlPointTargetMode.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryCreationSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryObjectAttributesSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryTransformSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryReplacementSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/ControlPointEditSpec.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/GeometryCreationCommonOptions.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PointItemRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/LineItemRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ArcItemRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/SurfaceItemRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreatePointsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateLinesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateArcsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateSurfacesRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/TransformObjectsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ReplaceGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/DeleteObjectsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/EditControlPointsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewTransformObjectsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewReplaceGeometryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewDeleteObjectsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewEditControlPointsRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/GeometryReplacementEntryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ControlPointEditEntryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryCreationResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryModificationResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryModificationPreviewResponse.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IEditResultFormatter.cs`（扩展三个几何相关方法，非新增文件）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/PassThroughEditResultFormatter.cs`（新增三个方法实现，非新增文件）
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`

# 使用方式

## MCP Tool

创建：

- `CreatePoints(filePath, items, common)`
- `CreateLines(filePath, items, common)`
- `CreateArcs(filePath, items, common)`
- `CreateSurfaces(filePath, items, common)`

修改（Apply）：

- `TransformObjects(filePath, transform, confirmedObjectIds?, layerQueries?, confirmedLayerFullPaths?, objectTypes?, userAttributeConditions?, matchMode?, userAttributeMatchMode?)`
- `DeleteObjects(filePath, confirmedObjectIds?, layerQueries?, confirmedLayerFullPaths?, objectTypes?, userAttributeConditions?, matchMode?, userAttributeMatchMode?)`
- `ReplaceGeometry(filePath, entries)` 其中 `entry = { ObjectId, Geometry }`
- `EditControlPoints(filePath, entries)` 其中 `entry = { ObjectId, TargetMode, PointIndex? | UIndex?/VIndex?, X, Y, Z, Weight? }`

修改（Preview，参数与 Apply 版一致，不落盘）：

- `PreviewTransformObjects(...)` / `PreviewDeleteObjects(...)` / `PreviewReplaceGeometry(...)` / `PreviewEditControlPoints(...)`

## 典型调用场景

- **单条创建**：`CreatePoints(file, [{X:0,Y:0,Z:0}], {LayerFullPath:"Base::Grid"})`
- **批量创建**：一次性创建 100 条直线作为轴网，附带统一图层与颜色。
- **按 GUID 精准平移**：`TransformObjects(file, {Kind:"Translate", VectorX:0, VectorY:0, VectorZ:3000}, confirmedObjectIds:[...])`
- **按 GUID 旋转**：`TransformObjects(file, {Kind:"Rotate", CenterX:0, CenterY:0, CenterZ:0, AxisX:0, AxisY:0, AxisZ:1, AngleRadians:1.57079632679}, confirmedObjectIds:[...])`
- **按筛查删除**：清空 `"Temp::Scratch"` 图层所有 Curve：`DeleteObjects(file, layerQueries:["Temp::Scratch"], objectTypes:["Curve"])`
- **先预览再删除**：`PreviewDeleteObjects(file, layerQueries:["Temp::Scratch"])` 确认数量 / warning 后再调 `DeleteObjects`。
- **替换几何**：`ReplaceGeometry(file, [{ObjectId:..., Geometry:{Primitive:Arc, Mode:ThreePoint, ...}}])`
- **控制点编辑**：`EditControlPoints(file, [{ObjectId:..., TargetMode:CurveIndex, PointIndex:2, X:10, Y:20, Z:30}])`
- **复合流程（Create → Transform → EditControlPoints）**：
  1. `CreateLines(file, [...], {LayerFullPath:"Base::Grid"})` 建立轴网，记录返回的 ObjectIds。
  2. `TransformObjects(file, {Kind:"Translate", VectorX:0, VectorY:0, VectorZ:3000}, confirmedObjectIds:[...])` 把轴网整体上移。
  3. 对其中某条曲线转为 NurbsCurve 后（由上游工具或后续能力完成），`EditControlPoints(file, [{ObjectId:..., TargetMode:CurveIndex, PointIndex:1, X:..., Y:..., Z:...}])` 微调局部形态。

# 后续扩展方向

- 引入 RhinoCommon/RhinoInside 适配层，扩展 Loft / Sweep / Revolve / Extrude / Brep 布尔等高级曲面/实体能力。
- 增加曲线 Join / Trim / Split、Offset 等派生工具。
- 增加 Mesh 原子创建与编辑工具。
- 当 `LayerFullPath` 不存在时支持自动创建图层（需与筛查侧约定命名规范）。
- 为本批工具补充单元测试（validator 边界）与集成测试（round-trip `.3dm` 断言）。
- 当"设计意图驱动"流程浮现（如"按轴网生成柱子"）时，升级为 `Agents/Modeling/` 下的 Agent，调用现有 Skill。
- `Create*` 系工具是否需要补 Preview，在实际用量与 Client 反馈观察后再评估（首期不做，避免 API 表面膨胀）。
- `ReplaceGeometry` 跨 primitive kind 替换的用例若浮现，可考虑引入「强制替换 + warning」模式作为显式开关。
