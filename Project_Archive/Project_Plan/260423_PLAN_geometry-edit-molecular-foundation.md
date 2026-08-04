# 260423_PLAN_geometry-edit-molecular-foundation

## 背景

`260423_PLAN_geometry-edit-molecular-architecture`（总纲）已把原 Wave 1/2/3 与 Plan 4 的 lane 划分重组为 8 个主干分子 + 复合 Tool 层 + 子 PLAN。本 PLAN 是总纲下的 **子 PLAN A**，承担其中四个**读路径 / 属性快照 / 判定**基础分子；其中 metadata replay 是内部写入 helper，只能在 Apply 流程或 smoke 的受控 Undo record 内调用：

- 分子 1：几何可编辑描述器
- 分子 3：曲线/曲面 frame 采样器
- 分子 6：Metadata snapshot / replay
- 分子 8：untrimmed single-face Brep 探测/降级器

本 PLAN 只建立这四个分子及其暴露，不引入几何 edit / mutation 能力（reconstruction / replace 留给子 PLAN B/D）。**例外**：分子 6 的 metadata replay 是 Apply 流程内部 attribute 写入 helper —— 本 PLAN 不暴露其 Tool 入口，但 smoke 命令为单测 replay 行为会临时创建 Rhino 对象 + `ModifyAttributes` + `SetUserString`（详见 §6），属于受控的 attribute 写路径，不等价于几何 mutation；该路径仅出现在 smoke 命令内，常规 Tool 调用流程纯读。曲线 / 曲面编辑复合 Tool（Preview/Apply）由子 PLAN B（曲线）与子 PLAN D（曲面）承担，均依赖本 PLAN 提供的分子作为底层服务。

被替代的旧 PLAN 相关部分：`Project_Archive/260423_PLAN_geometry-edit-descriptor-lane.md`（其中"descriptor 读取 + metadata 白名单"部分），以及 `Project_Archive/260423_PLAN_geometry-edit-surface-lane.md`（其中 descriptor 曲面扩展与 single-face Brep 降级判定部分）。两份旧 PLAN 未进入 EXET，本 PLAN 以分子视角重新组织相关内容，不回溯老文件。

## 目标

- 落地分子 1 / 3 / 6 / 8 的 Application Interface + Infrastructure/Rhino/Live 实现，并注册到 DI
- 对外暴露 1 个新增分子级 MCP Tool + 复用 1 个既有 MCP Tool：`GetEditableGeometryDescriptor`、`GetGeometryFramesInLive`（扩展承载 frame 采样；不新增并列 `SampleGeometryFrame`）
- 对象矩阵：
  - 曲线：`LineCurve` / `PolylineCurve` / `NurbsCurve`（open / closed，任意 degree）
  - 曲面：`PlaneSurface` / `NurbsSurface`
  - untrimmed single-face Brep → 由分子 8 降级为 `UnderlyingSurface` 后，沿用曲面路径
- Descriptor 支持两档体量：`Summary` / `Full`
  - 曲线默认 `Full`
  - 曲面默认 `Summary`，`Full` 需显式声明
  - 两档均返回 `ControlPointCentroidWorld`，供子 PLAN C/D 的 ExactTransform 路径直接使用，避免为了 centroid 拉取完整点列
- Metadata snapshot/replay 白名单在本 PLAN 固定成文，后续子 PLAN 直接复用；snapshot 属读路径，replay 属内部写入 helper
- Live smoke 覆盖曲线 + 曲面 + untrimmed single-face Brep 全部读路径；metadata replay 验证不依赖 mutation Tool，使用"临时副本对象 + 单条 Undo record"策略单测（详见 §关键设计 §6）

**明确不纳入：**

- 任何几何重建、替换、删除（留给子 PLAN B/D）
- 派生点操作 / Selector / 策略路由（留给子 PLAN C）
- Plan 4 专属分子（留给子 PLAN E）
- Mesh / SubD / Extrusion / polysurface / trimmed BrepFace

## 架构归属

本 PLAN 实现的分子：**1 / 3 / 6 / 8**。

- **Tools/**（新增 / 修改）
  - `GetEditableGeometryDescriptorTool.cs`（分子 1 对外）
  - `GetGeometryFramesInLiveTool.cs`（既有；本 PLAN 扩展其参数协议以承载分子 3）

- **Application/Interfaces/**（新增）
  - `IEditableGeometryDescriptorService.cs`（分子 1）
  - `IGeometryFrameSampler.cs`（分子 3）
  - `IGeometryMetadataOperator.cs`（分子 6）
  - `IBrepSurfaceDowngrader.cs`（分子 8）

- **Infrastructure/Rhino/Live/**（新增）
  - `LiveEditableGeometryDescriptorService.cs`
  - `LiveGeometryFrameSampler.cs`
  - `LiveGeometryMetadataOperator.cs`
  - `LiveBrepSurfaceDowngrader.cs`

- **Domain/Enums/**（新增）
  - `EditableGeometryKind.cs`：`Curve` / `Surface`
  - `EditablePointRole.cs`：`ControlPoint` / `Anchor`；为子 PLAN C 预留 `DerivedPoint` 枚举位
  - `DescriptorDetail.cs`：`Summary` / `Full`
  - `GeometryReconstructionSupportKind.cs`：`Supported` / `PreviewOnly` / `Unsupported`

- **Domain/Models/**（新增）
  - `EditableGeometryDescriptor.cs`：统一的曲线 + 曲面 descriptor，含 `ControlPointCentroidWorld`
  - `EditableCurveStructure.cs`：`CurveKind` / `Degree` / `IsClosed` / `IsPeriodic`
  - `EditableSurfaceStructure.cs`：`SurfaceKind` / `DegreeU/V` / `CountU/V` / `IsClosedU/V` / `IsPeriodicU/V` / `Bbox`
  - `EditablePointDescriptor.cs`：`Index` / `Role` / `X` / `Y` / `Z`（曲面用 row-major 扁平 Index = u*countV + v）
  - `GeometryMetadataSnapshot.cs`：分子 6 快照载体
  - `GeometryMetadataSummary.cs`：descriptor 里的摘要字段（LayerIndex / Name / ColorSource / UserStringCount）
  - `BrepDowngradeResult.cs`：分子 8 输出载体（见 §关键设计 §5）
  - `FrameSample.cs`：`{Index, Parameter, Origin, XAxis, YAxis, ZAxis, IsDegenerate}`

- **Contracts/Requests/**（新增 / 修改）
  - `GetEditableGeometryDescriptorRequest.cs`：`FilePath` / `ObjectId` / 可选 `Detail`
  - `GetGeometryFramesInLiveRequest.cs` / `GeometryFrameEntryRequest.cs`（修改）：在既有 live frame inspection 契约上扩展 `ParameterSpec` / entry 形态（见 §关键设计 §3）

- **Contracts/Responses/**（新增 / 修改）
  - `EditableGeometryDescriptorResponse.cs`
  - `GetGeometryFramesInLiveResponse.cs`（既有扩展）

- **Server/**
  - `DependencyInjection.cs`：注册 4 个接口 + Live 实现

- **Infrastructure/Plugin/**（新增）
  - `McpGeometryEditMolecularFoundationSmokeCommand.cs`：Rhino 端命令 `_McpGeometryEditMolecularFoundationSmoke`

- **Skills / Agents**：本 PLAN 不新增。

## 关键设计

### §1 Descriptor 是 curve / surface 的统一入口

- `EditableGeometryDescriptor` 是一个包含曲线与曲面两条结构分支的并集模型：
  - `Kind: EditableGeometryKind`（`Curve` / `Surface`）
  - `CurveStructure: EditableCurveStructure?`（当 `Kind=Curve` 时填充）
  - `SurfaceStructure: EditableSurfaceStructure?`（当 `Kind=Surface` 时填充）
  - `Points: IReadOnlyList<EditablePointDescriptor>?`（仅 `Full` 档）
  - `ControlPointCentroidWorld: Point3d`（`Summary` / `Full` 均填充；曲线按控制点列表算术平均，曲面按 U×V 控制点网格算术平均）
  - `MetadataSummary: GeometryMetadataSummary`
  - `SupportKind: GeometryReconstructionSupportKind`
  - `Warnings: IReadOnlyList<string>`
- 调用方通过 `Kind` 分派，不需要判断具体 RhinoCommon 类型。
- 本 PLAN 不引入 "派生点" 相关字段；为 `EditablePointRole` 预留 `DerivedPoint` 枚举位但不使用。

### §2 DescriptorDetail 默认值与 token 预算

- 曲线请求默认 `Full`（沿用 Wave 1 行为，保持未来子 PLAN B 入参最小变化）
- 曲面请求默认 `Summary`（沿用 Wave 3 立场，避免 U×V 网格默认爆炸）
- `Full` 档响应体量无上限；当曲面 `CountU*CountV > 10000` 时追加 warning `DESCRIPTOR_FULL_LARGE`
- `Summary` 档下曲面响应无点列，但仍包含 `ControlPointCentroidWorld`；体量与 U×V 基本无关

### §3 Frame 采样器的参数协议（复用既有 `GetGeometryFramesInLive`）

本 PLAN 不新增平行 `SampleGeometryFrame` MCP Tool。分子 3 的公开入口复用既有 `GetGeometryFramesInLive(filePath, entries)`，并扩展 `GeometryFrameEntryRequest` / `GetGeometryFramesInLiveResponse` 以容纳批量 frame 采样语义。

`GeometryFrameEntryRequest` 对分子 3 新增一组面向采样的 entry 形态，其核心 `ParameterSpec` 是一个 discriminator 字段，取值：

- `CurveExplicit`：曲线专用，携带 `Parameters: double[]`（曲线参数 `t`）
- `SurfaceExplicit`：曲面专用，携带 `Parameters: (double U, double V)[]`
- `AtControlPointGrevilles`：便利模式，不带参数；服务端对曲线用每个控制点的 Greville abscissa 作为 `t`；对曲面用 `(U,V)` Greville grid
- `AtParameterFractions`：便利模式，携带 `Fractions: double[] ∈ [0,1]`；曲线按归一化参数域映射；曲面按 `(U,V)` 分数对映射（两轴共用同一数组）。此模式不支持"两轴分别给分数"，如需更复杂采样，使用 `SurfaceExplicit`

输出沿用 `GetGeometryFramesInLiveResponse` 批量结果容器；frame sample 的顺序与输入参数顺序一一对应；退化点仍出现在列表中，`IsDegenerate=true` + `Origin` / `Normal` 可能为零向量。

### §4 分子 6 metadata 白名单（固定成文）

**必须复制（同一 Apply record 内 replay）**：

- `LayerIndex`
- `ObjectColor` + `ColorSource`
- `PlotColor` + `PlotColorSource`
- `PlotWeight` + `PlotWeightSource`
- `LinetypeIndex` + `LinetypeSource`
- `Name`
- `Visible`
- 全部 user strings（通过 `Attributes.GetUserStrings()` 读；逐条 `SetUserString` 写）

**明确不复制（本期）**：

- `RenderMaterial` / `MaterialSource`
- `GroupList`
- `DisplayMode`
- `Space`
- 任何 block instance 相关字段

差异字段在 replay 时以 `METADATA_FIELDS_DROPPED` warning 具名返回，不静默丢字段。

### §5 分子 8 Brep 降级判定

`LiveBrepSurfaceDowngrader.Evaluate(rhinoObject)` 返回 `BrepDowngradeResult`：

- `IsUntrimmedSingleFaceBrep: bool`
- 当 `true` 时：
  - `UnderlyingSurface: Surface`（非空）
  - `Rebuild: Func<Surface, Brep>`（给定新 Surface，用 `Brep.CreateFromSurface` 回包为 Brep；保持对象类型稳定，避免 Apply 后 RhinoObject 从 Brep 变成裸 Surface）
- 当 `false` 时：
  - `Reason: string`，具体值如 `"not a brep"` / `"polysurface has N faces"` / `"brep face is trimmed"` / `"outer loop not aligned within tolerance"`
  - `UnderlyingSurface` / `Rebuild` 均为 null

**判定条件（全部满足才降级）**：

1. `rhinoObject.Geometry is Brep brep`
2. `brep.Faces.Count == 1`
3. `brep.Faces[0].IsSurface`
4. `brep.Faces[0].UnderlyingSurface() != null`
5. `brep.Faces[0].OuterLoop.To3dCurve()` 的包围区域与 `UnderlyingSurface` 参数域边界在 `doc.ModelAbsoluteTolerance` 内一致

Descriptor 服务调用降级器时，若返回 `IsUntrimmedSingleFaceBrep=true`，descriptor 附 warning `UNDERLYING_SURFACE_FALLBACK` 并按曲面路径返回 `SurfaceStructure`；若 `false`，descriptor `SupportKind=Unsupported` 并把 `Reason` 作为 warning 附上。

### §6 Metadata 单测策略

分子 6 不对外暴露 Tool；snapshot 是读操作，replay 会写对象 attributes / user strings，因此本 PLAN 只在 smoke 的受控 Undo record 中验证 replay 写入效果，不把 replay 暴露为常规 Tool。解决方案：

- 在 smoke 命令中显式创建一个临时测试对象（例如 `doc.Objects.AddPoint(...)`），手动改图层 / Name / UserStrings，snapshot 一次
- 再创建一个新对象，调用 replay 写入 snapshot
- 校验两个对象的白名单字段一致，然后在同一条 Undo record 结束前删除两个临时对象
- smoke 不主动调用 Undo；执行结束后对象数应回到执行前。Rhino Undo History 允许新增 1 条 `MCP:GeometryEditMolecularFoundationMetadataSmoke` 临时记录，用户 Ctrl+Z 后仍应回到 smoke 前状态，不暴露临时对象
- 该路径仅在 smoke 命令中执行，不作为常规 Tool 暴露；实现必须走主线程封送与单条 Undo record，避免绕开仓库的 live mutation 约束

### §7 Tool 调用路径与 Live 依赖

- `GetEditableGeometryDescriptorTool` → `IEditableGeometryDescriptorService.Read(filePath, objectId, detail)` → 经 `ILiveRhinoDocumentAccessor.Execute(...)` 在主线程读取 → 调用 `LiveBrepSurfaceDowngrader`（如对象是 Brep） → 组装 descriptor
- `GetGeometryFramesInLiveTool`（扩展后）→ `IGeometryFrameSampler.Sample(filePath, objectId, parameterSpec)` → `ILiveRhinoDocumentAccessor.Execute(...)` → 按 parameterSpec 分支采样
- 两个 Tool 均为 live-first 读：非 Rhino Plugin 环境下返回 `LIVE_RHINO_REQUIRED`，不假装 CLI fallback
- 两个 Tool 均不写文档、不开 Undo record

### §8 Live Smoke CLI slug 独占

- slug：`geometry-edit-molecular-foundation-smoke-test`（全仓唯一）
- 注册入口：`Project_Test/260423_TEST_geometry-edit-molecular-foundation/DeveloperCommandHandler.GeometryEditMolecularFoundationSmokeTest.cs`
- Rhino 命令：`_McpGeometryEditMolecularFoundationSmoke`

### §9 错误与 warning 码

硬错误（Tool 响应 `Success=false`）：

- `LIVE_RHINO_REQUIRED`：非 Rhino Plugin 环境
- `OBJECT_NOT_FOUND`：`ObjectId` 不存在
- `EDITABLE_KIND_UNSUPPORTED`：对象不属于本期支持矩阵（polysurface / mesh / subd / trimmed BrepFace / 其它）
- `FRAME_PARAMETER_OUT_OF_RANGE`：采样参数越界
- `FRAME_PARAMETER_SPEC_MISMATCH`：`CurveExplicit` 用在曲面 / 反之

warning（响应 `Success=true`，以 code 具名附带）：

- `UNDERLYING_SURFACE_FALLBACK`（分子 8 判定降级）
- `DESCRIPTOR_FULL_LARGE`（曲面 Full 且 U×V > 10000）
- `FRAME_DEGENERATE`（frame 采样器某点退化，`IsDegenerate=true`）
- `METADATA_FIELDS_DROPPED`（metadata snapshot/replay 跨白名单差异；仅 smoke 命令中可见）

## 涉及文件

**新增（Plan / Test）**

- `Project_Plan/260423_PLAN_geometry-edit-molecular-foundation.md`
- `Project_Test/260423_TEST_geometry-edit-molecular-foundation/`
- `Project_Test/260423_TEST_geometry-edit-molecular-foundation/DeveloperCommandHandler.GeometryEditMolecularFoundationSmokeTest.cs`

**新增（Tools）**

- `src/MCP_Rhino.Server/Tools/Geometry/Edit/GetEditableGeometryDescriptorTool.cs`
- `src/MCP_Rhino.Server/Tools/Analysis/GetGeometryFramesInLiveTool.cs`

**新增（Application）**

- `src/MCP_Rhino.Server/Application/Interfaces/IEditableGeometryDescriptorService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryFrameSampler.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryMetadataOperator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IBrepSurfaceDowngrader.cs`

**新增（Infrastructure）**

- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveEditableGeometryDescriptorService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryFrameSampler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveGeometryMetadataOperator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveBrepSurfaceDowngrader.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpGeometryEditMolecularFoundationSmokeCommand.cs`

**新增（Domain）**

- `src/MCP_Rhino.Server/Domain/Enums/EditableGeometryKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/EditablePointRole.cs`
- `src/MCP_Rhino.Server/Domain/Enums/DescriptorDetail.cs`
- `src/MCP_Rhino.Server/Domain/Enums/GeometryReconstructionSupportKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditableGeometryDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditableCurveStructure.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditableSurfaceStructure.cs`
- `src/MCP_Rhino.Server/Domain/Models/EditablePointDescriptor.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryMetadataSnapshot.cs`
- `src/MCP_Rhino.Server/Domain/Models/GeometryMetadataSummary.cs`
- `src/MCP_Rhino.Server/Domain/Models/BrepDowngradeResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/FrameSample.cs`

**新增（Contracts）**

- `src/MCP_Rhino.Server/Contracts/Requests/GetEditableGeometryDescriptorRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/GetGeometryFramesInLiveRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/GeometryFrameEntryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/EditableGeometryDescriptorResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/GetGeometryFramesInLiveResponse.cs`

**修改**

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`（注册 4 个接口 + Live 实现）

**复用（不改）**

- `ILiveRhinoDocumentAccessor`

## 使用方式

### 场景 A：读取 NurbsCurve 的完整 descriptor（默认 Full）

```
GetEditableGeometryDescriptor(filePath, objectId)
→ { Kind:"Curve",
    CurveStructure:{ CurveKind:"NurbsCurve", Degree:3, IsClosed:false, IsPeriodic:false },
    Points:[ {Index:0, Role:"ControlPoint", X:..., Y:..., Z:...}, ... ],
    MetadataSummary:{ LayerIndex:2, Name:"beam-01", UserStringCount:3, ... },
    SupportKind:"Supported",
    Warnings:[] }
```

### 场景 B：读取 NurbsSurface 的 Summary descriptor（默认）

```
GetEditableGeometryDescriptor(filePath, objectId)
→ { Kind:"Surface",
    SurfaceStructure:{ SurfaceKind:"NurbsSurface", DegreeU:3, DegreeV:3,
                       CountU:10, CountV:8, IsClosedU:false, IsClosedV:false,
                       IsPeriodicU:false, IsPeriodicV:false,
                       Bbox:{Min:..., Max:...} },
    MetadataSummary:{...},
    SupportKind:"Supported",
    Warnings:[] }
```

### 场景 C：读取 untrimmed single-face Brep（降级为曲面）

```
GetEditableGeometryDescriptor(filePath, objectId)
→ { Kind:"Surface",
    SurfaceStructure:{ SurfaceKind:"NurbsSurface", ... },
    MetadataSummary:{...},
    SupportKind:"Supported",
    Warnings:["UNDERLYING_SURFACE_FALLBACK"] }
```

### 场景 D：读取 polysurface / trimmed BrepFace / mesh

```
GetEditableGeometryDescriptor(filePath, objectId)
→ Success=false, ErrorCode:"EDITABLE_KIND_UNSUPPORTED",
  Message:"brep face is trimmed" | "polysurface has 7 faces" | ...
```

### 场景 E：采样曲线 frame（显式参数）

```
GetGeometryFramesInLive(filePath,
  entries:[{ EntryId:"curve-0", ObjectId:objectId, ParameterSpec:{ Kind:"CurveExplicit", Parameters:[0.0, 0.5, 1.0] } }])
→ { Results:[ { EntryId:"curve-0", Samples:[ {Index:0, Parameter:0.0, Origin:..., XAxis:..., YAxis:..., ZAxis:...,
               IsDegenerate:false}, ... ] } ] }
```

### 场景 F：采样曲面 frame（Greville 网格）

```
GetGeometryFramesInLive(filePath,
  entries:[{ EntryId:"srf-greville", ObjectId:objectId, ParameterSpec:{ Kind:"AtControlPointGrevilles" } }])
→ { Results:[ { EntryId:"srf-greville", Samples:[...每个控制点对应一个 frame...] } ] }
```

## 验收标准

### 构建

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 通过

### 非 Rhino Plugin 环境（CLI）

- `dotnet run --project src/MCP_Rhino.Server -- geometry-edit-molecular-foundation-smoke-test Runtime_Test/MCP_rhino_test.3dm` 返回明确的 `LIVE_RHINO_REQUIRED`，不假装 CLI fallback

### Rhino Plugin 环境

在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm`，运行 `_McpGeometryEditMolecularFoundationSmoke`：

- **曲线 descriptor**：对一条 `NurbsCurve`、一条 `PolylineCurve`、一条 `LineCurve` 各跑一轮 Full descriptor：
  - `Success=true`；`Kind="Curve"`；`Points.Count == descriptor.ControlPointCount`
  - `ControlPointCentroidWorld` 与控制点算术平均一致（容差内）
  - `MetadataSummary` 的 LayerIndex / Name / UserStringCount 与对象实际属性一致
- **曲面 descriptor**：
  - 对一个 `PlaneSurface`：Summary `Success=true`；Full `Success=true` 且 `Points.Count == 4`
  - 对一个 `NurbsSurface`：Summary `Success=true`；Full `Success=true` 且 `Points.Count == CountU*CountV`
  - Summary / Full 两档的 `ControlPointCentroidWorld` 一致（容差内）
  - 对一个 untrimmed single-face Brep：Summary `Success=true` 且 `Warnings` 含 `UNDERLYING_SURFACE_FALLBACK`
  - 对一个 polysurface / trimmed BrepFace / mesh：`Success=false` 且 `ErrorCode="EDITABLE_KIND_UNSUPPORTED"`，`Message` 含降级判定具体原因
- **Frame 采样**：
  - 曲线显式参数：3 点采样成功，顺序与输入一致
  - 曲线 `AtParameterFractions`：`[0.0, 0.5, 1.0]` 采样成功
  - 曲面 `AtControlPointGrevilles`：采样数 == `CountU*CountV`
  - 曲面显式 `(U,V)`：3 点成功
  - 越界参数：`Success=false` + `FRAME_PARAMETER_OUT_OF_RANGE`
- **Metadata snapshot/replay（仅 smoke 命令内）**：
  - 临时对象 A 的白名单字段经 snapshot → replay 到临时对象 B，B 读回与 A 一致
  - 两个临时对象在 smoke 结束前删除；执行前后 `doc.Objects.Count` 不变
- **无副作用**：smoke 执行前后 `doc.Objects.Count` / `doc.Strings.Count` 不变；Rhino Undo History 除 metadata smoke 的一条临时记录外无其他新增，且用户 Ctrl+Z 后仍不留下临时对象

### 回归

- 既有 Tool（`TransformObjects` / `EditControlPoints` / `ReplaceGeometry` / `DeleteObjects` 等）无任何签名或行为变化

## 风险与回退方案

### 风险

- **Brep 降级容差误判**：`OuterLoop` 与 `UnderlyingSurface` 边界的容差比较可能在"几乎对齐但仍是 trim"的 Brep 上误识别；反之容差偏紧会漏识别
- **Descriptor 字段演进**：子 PLAN B/C/D 会持续扩展 descriptor 字段；本 PLAN 需避免把字段定义得过死，但又不能预留过多空位
- **Frame 采样器参数模型**：`AtParameterFractions` 对曲面取"两轴共享分数"是故意简化，若后续需要独立 U / V 分数，再扩 `ParameterSpec`
- **Metadata replay 白名单盲区**：本 PLAN 固定的白名单无法覆盖所有 Rhino 属性；盲区通过 `METADATA_FIELDS_DROPPED` 暴露，实战中若出现关键字段被误丢，另起小 PLAN 扩白名单

### 缓解

- Brep 降级判定的容差来源（`doc.ModelAbsoluteTolerance`）写进 response 的 warning 描述，便于排查
- descriptor 字段使用 optional / nullable，预留曲线或曲面独有字段扩展位，但不预留"暂未使用的命名字段"
- `ParameterSpec` 是 discriminator，后续可加新 Kind 而不破坏旧字段
- 白名单差异持续具名 warning；EXET 阶段主动抽样检查

### 回退方案

- 若分子 1 或 3 Tool 不稳定：单独下线对应 Tool 的 `[McpServerTool]` 标注，保留 Service 代码供子 PLAN B/D 复用
- 若分子 8 降级判定存在系统性问题：临时收紧为"不降级"，返回 `EDITABLE_KIND_UNSUPPORTED`，让调用方自行 `Brep.Faces[0].ToNurbsSurface()` 后再调 Tool
- 本 PLAN 为新增文件集（不改动既有代码签名），可独立 `git revert`

## 后续扩展方向

- 子 PLAN B：在本 PLAN 分子之上新增曲线 Preview/Apply Tool（编排分子 5 曲线分支 + 分子 6/7）
- 子 PLAN C：新增分子 2（派生点求值器）+ 分子 4（策略路由器），扩展曲线 Preview/Apply
- 子 PLAN D：复用分子 1 / 8 的曲面 descriptor 与 Brep 降级结果，扩展分子 5 / 2 / 4 的曲面分支，新增曲面 Preview/Apply Tool
- 子 PLAN E：Plan 4 专属分子，复用分子 3（frame 采样）+ 分子 6（metadata）+ 分子 7（mutation 边界）
- 本 PLAN 的 frame 采样分子在 Plan 4 LCS builder 中复用；若 LCS builder 对 frame 语义有额外约束（例如必须归一化 XAxis 到某参考方向），由子 PLAN E 扩字段不扩分子，也不新增第二套 public frame API
