# 背景

当前 MCP_Rhino 已经具备从筛查、属性编辑、几何创建到几何修改的完整 MCP 工具矩阵，但所有 mutation 类操作（`Create* / Transform / Replace / Delete / EditControlPoints / ApplyObjectEdits / Apply/DeleteObjectUserText / Set/DeleteDocumentUserStrings`）都走同一个离线落盘骨架：

```
Exists → File3dm.Read → Validate → FileMutationSafeguard.BeforeOverwrite
       → Mutate(File3dm) → File3dm.Write → AfterOverwrite
```

团队讨论后明确：**离线 `File3dm.Write` 不可接受**。落盘瞬间即生效，绕过 Rhino Undo 栈与用户的 Save 动作，**无法撤销**；viewport 也不会同步刷新，用户一旦在 Rhino 里开着同一份文件就会与磁盘版本撕裂。现有的 `IFileMutationSafeguard` + 归档快照仅是事后补救，不是真正的原子回滚。

对 29 个 MCP Tool 盘点：

- **17 个与 mutation 语义直接相关的 Tool**：
  - **13 个写入 Tool**（改造为 live）：`CreatePointsTool` / `CreateLinesTool` / `CreateArcsTool` / `CreateSurfacesTool` / `TransformObjectsTool` / `ReplaceGeometryTool` / `DeleteObjectsTool` / `EditControlPointsTool` / `ApplyObjectEditsTool` / `ApplyObjectUserTextWritesTool` / `DeleteObjectUserTextTool` / `SetDocumentUserStringsTool` / `DeleteDocumentUserStringsTool`
  - **3 个归档/预检 Tool**（直接下线）：`CreateArchiveSnapshotTool` / `CleanupArchiveTool` / `InspectFileMutationReadinessTool`
  - **1 个 preview-of-mutation Tool**：`PreviewObjectEditsTool`（改造为 live）
- **12 个 read / preview Tool**：
  - **8 个 offline 保留**：`FilterObjectsTool` / `FilterObjectsByTypeTool` / `FilterObjectsByUserAttributesTool` / `FilterObjectsByLayerTool` / `FindLayerCandidatesTool` / `GetDocumentUserStringsTool` / `GetObjectUserStringsTool` / `PreviewObjectUserTextWritesTool`
  - **4 个 preview-of-mutation Tool**（改造为 live）：`PreviewTransformObjectsTool` / `PreviewDeleteObjectsTool` / `PreviewReplaceGeometryTool` / `PreviewEditControlPointsTool`

当前架构中有 4 个 Application 接口在签名层暴露 `File3dm`，构成最大的重构阻力：

- `IRhinoDocumentRepository.Read(filePath) → File3dm` / `Write(File3dm, filePath)`
- `IGeometryMutator.Transform(File3dm, ...)` / `Replace(File3dm, ...)` / `Delete(File3dm, ...)` / `EditControlPoints(File3dm, ...)`
- `IObjectEditOperationApplier.Apply(File3dm, RhinoObjectInfo, operations)`
- `IObjectEditValidator.Validate(File3dm, operations, matchedObjects)`

另有 3 个接口**不含** `File3dm`，可原样复用：`IGeometryBuilder`、`IGeometryValidator`（spec 分支）、`IEditResultFormatter`。

Live Rhino 能力在当前代码库是空白地——无任何 `RhinoDoc.ActiveDoc` / `Rhino.RhinoApp` / plugin hook 引用。260420_PLAN_geometry-create-modify-tools.md 明确将 RhinoCommon / RhinoInside 范围排除（*"本次不纳入范围，保持当前 Rhino3dm 文件级架构"*）；本次计划正式解除该限制。

# 目标

- **强制 live-only 写路径**：13 个写入 Tool 全部通过 `RhinoDoc.ActiveDoc` 执行，不再经 `File3dm.Read/Write` 落盘；每次 Apply 纳入 Rhino Undo 栈，用户在 Rhino 中 `Ctrl+Z` 即可撤销。
- **强制 live-only mutation preview**：`PreviewObjectEditsTool` 与 4 个几何 `Preview*` Tool 改为只读 `RhinoDoc`，不写、不建 Undo record；以确保 Preview 与随后的 Apply 对齐到同一份文档状态。
- **保留 offline read / preview-of-read**：8 个纯 read / preview-of-read Tool 继续读 `File3dm`，保留未打开文件的审计与查询能力。
- **保持 MCP API 表面不变**：对仍保留对外暴露的写入 Tool 与 preview-of-mutation Tool，方法名、参数、响应字段一个不删、一个不改；行为变化仅在：需要 Rhino 运行中、`FilePath` 需匹配 ActiveDoc、Save 回归 Rhino 用户流程。
- **引入 Rhino Plugin 宿主 + Bridge 传输层**：MCP server 默认以 `.rhp` 形态在 Rhino 进程内启动；plugin 在 `OnLoad` 中拉起 Named Pipe server，外部 MCP Client 通过独立的 `MCP_Rhino.Bridge.exe` 做 stdio-to-pipe 桥接。`Program.cs` 保留为开发期 CLI 入口（只读 / smoke），不承担 MCP host 角色。
- **去归档化**：`IFileMutationSafeguard`、`IArchiveSnapshotService`、`IArchiveRetentionService`、`IFileOpenStateInspector` 以及配套 Tool / Response warning 全链路移除；回滚交还 Rhino Undo。
- **严格遵守** `.clinerules/MCP_Rhino Architecture.md` 新增的「执行模式指南」章节：写入与 preview-of-mutation 只进 `Infrastructure/Rhino/Live/`，offline 读与 preview-of-read 只进 `Infrastructure/Rhino/Offline/`。
- **非目标（本次不做）**：RhinoCommon-only 的高级建模能力（Loft / Sweep / Revolve / Brep 布尔等）、Grasshopper 集成、无头 Rhino CI 集成测试；这些列入后续扩展。

# 架构归属

- **Infrastructure/Plugin/**（新增目录）
  - `MCP_Rhino.RhinoPlugin.cs`：继承 `Rhino.PlugIns.PlugIn`（RhinoCommon），`OnLoad` 中构建 Host、注册依赖、启动 Named Pipe MCP server；`OnShutdown` 中关闭管道并释放。
  - `McpNamedPipeServer.cs`：用 `System.IO.Pipes.NamedPipeServerStream` 暴露管道 `\\.\pipe\mcp_rhino`，承载 MCP JSON-RPC 协议帧；与 MCP SDK 的 stream transport 对接（若 SDK 已提供），否则落一层薄封装。
  - 携带 `plugin.manifest`（Rhino 插件元数据，GUID / Name / Version / Description）。
- **MCP_Rhino.Bridge/**（新增顶层目录 / 独立 csproj）
  - `Program.cs`：stdio-to-pipe 桥接入口——启动后连 `\\.\pipe\mcp_rhino`，把 stdin 原样 proxy 到管道、把管道响应 proxy 回 stdout；连接失败时向 stderr 写友好提示后退出。
  - `MCP_Rhino.Bridge.csproj`：独立产出 `MCP_Rhino.Bridge.exe`；MCP Client 配置为 spawn 此可执行文件。
- **Infrastructure/Rhino/Live/**（新增目录）
  - `LiveRhinoDocumentAccessor`：实现 `ILiveRhinoDocumentAccessor`；封装 `RhinoDoc.ActiveDoc` 访问、路径匹配、Undo record 生命周期；所有 `Rhino.RhinoApp` 门面调用集中在这里。
  - `LiveRhinoGeometryMutator`：实现新签名 `IGeometryMutator`，用 `doc.Objects.Add / Replace / Delete / Transform` 执行变更。
  - `LiveRhinoObjectEditOperationApplier`：实现新签名 `IObjectEditOperationApplier`，在 `RhinoObject.Attributes` 上修改图层 / 颜色 / Name / UserText。
  - `LiveRhinoObjectUserTextWriter` / `LiveRhinoDocumentUserStringWriter`：在 `doc.Objects[id].Attributes.SetUserString` / `doc.Strings.SetString` 上做批量写 / 删。
  - `LiveRhinoGeometryValidator`：同时实现既有 `IGeometryValidator`（所有 spec 分支无 doc 依赖，Live / Offline 共用）与新增 `ILiveGeometryValidator`（承载需要 `RhinoDoc` 上下文的校验，例如 `RhinoGeometryModificationService.ValidateControlPointIndex(File3dm, ControlPointEditSpec)` 在新模型下迁入 `ValidateAgainstDocument(RhinoDoc, ControlPointEditSpec)`）。
- **Infrastructure/Rhino/Offline/**（既有实现搬迁，内容不变）
  - `RhinoDocumentRepository`（read-only 面）、`RhinoObjectFilterService`、`RhinoLayerCandidateService`、`RhinoObjectAttributesReader`、`RhinoDocumentUserStringReader`、`RhinoObjectUserTextReader`、`PassThroughEditResultFormatter`、`RhinoGeometryBuilder`、`RhinoGeometryValidator`（纯 spec 分支）等。
  - 搬迁时仅调整 namespace / 文件位置，保留原有公开 API。
- **Infrastructure/Rhino/**（根目录）
  - 仅保留跨模式共享的工具类（例如 `RhinoObjectTypeNames`、`RhinoGeometryHelpers`），不再直接放具体 Repository / Mutator 实现。
- **Application/Interfaces/**（重塑）
  - `IRhinoDocumentRepository`：收窄为 read-only `Read(filePath) → File3dm` + `Exists(filePath)`；`Write` 方法删除。
  - `IGeometryMutator`：方法签名去掉 `File3dm model` 参数；改为 `Transform(RhinoObjectInfo target, GeometryTransformSpec spec)` / `Replace(...)` / `Delete(...)` / `EditControlPoints(...)`，由 Live 实现在 `ILiveRhinoDocumentAccessor` 内部拿到 `RhinoDoc`。
  - `IObjectEditOperationApplier`：同上，去 `File3dm`。
  - `IObjectEditValidator`：拆分为两个接口——
    - `IObjectEditSpecValidator`：纯数据校验（无 doc 上下文），Offline / Live 共用。
    - `ILiveObjectEditValidator`：需要 `RhinoDoc` 的校验（对象存在性、类型兼容、控制点越界），仅 Live 使用。
  - `IGeometryValidator`：保留不动（既有 4 个 overload 全部以 spec + `RhinoObjectInfo` 为输入，无 `File3dm` 依赖），Offline / Live 共用。
  - 新增 `ILiveGeometryValidator`：承载需要 `RhinoDoc` 上下文的几何校验（当前为 `ValidateAgainstDocument(RhinoDoc, ControlPointEditSpec)`；未来可按需扩展）。命名与 `ILiveObjectEditValidator` 对称。
  - 新增 `ILiveRhinoDocumentAccessor`（主线程封送 + 文档访问统一入口，详见关键设计 #10、#12）：
    - `Execute<T>(string filePath, Func<RhinoDoc, T> work)` → 封送到主线程、解析 ActiveDoc 并校验路径匹配、执行 `work`、返回值 / 错误。所有 live 读写走此入口。
    - `ExecuteWithUndo<T>(string filePath, string undoDescription, Func<RhinoDoc, (bool mutated, T result)> work)` → 在 `Execute` 基础上自动包 `BeginUndoRecord` / `EndUndoRecord` / `CancelUndoRecord`，写入 Tool 使用。
    - `TryGetActiveDocumentState(string filePath, out bool hasUnsavedChanges) → bool` → 供 offline read Tool 以 best-effort 方式判断是否需要写 `OFFLINE_READ_STALE` 警告；返回 `true` 表示路径匹配 ActiveDoc，`hasUnsavedChanges` 决定是否告警；不做路径外的副作用。
    - CLI 模式下注册的空实现（`NullLiveRhinoDocumentAccessor`）对 `Execute` 系方法直接返回 `LIVE_RHINO_REQUIRED`，对 `TryGetActiveDocumentState` 返回 `false` 且 `hasUnsavedChanges=false`。
  - 移除 `IFileMutationSafeguard`、`IArchiveSnapshotService`、`IArchiveRetentionService`、`IFileOpenStateInspector`。
- **Application/Services/**（改造）
  - `RhinoGeometryCreationService` / `RhinoGeometryModificationService` / `RhinoObjectEditingService` / `RhinoObjectUserTextService` / `RhinoDocumentUserStringService`：
    - 去掉 `_repository.Read/Write`、`_fileMutationSafeguard.BeforeOverwrite/AfterOverwrite` 调用链。
    - 通过 `ILiveRhinoDocumentAccessor.ExecuteWithUndo<T>(filePath, undoDescription, work)` 在主线程封送内同步执行变更、绑定 Undo record；work 回调返回 `(bool mutated, T result)`，`mutated=false` 时内部改走 `CancelUndoRecord`。
    - Preview-of-mutation 路径：改用 `ILiveRhinoDocumentAccessor.Execute<T>(filePath, work)`（不带 Undo record），在主线程封送内只读 `RhinoDoc`。
    - 构造函数依赖同步改动。
  - 删除 `RhinoFileMutationSafeguard`、`RhinoArchiveSnapshotService`、`RhinoArchiveRetentionService`、`RhinoFileOpenStateInspector`。
- **Tools/**（删除 3 个 + 改造 18 个）
  - 删除：`Tools/File/CreateArchiveSnapshotTool.cs` / `Tools/File/CleanupArchiveTool.cs` / `Tools/File/InspectFileMutationReadinessTool.cs`。
  - 保留签名、改造实现（通过对应 Skill / Service 间接换到 live 路径）：
    - `Tools/Geometry/CreatePointsTool` / `CreateLinesTool` / `CreateArcsTool` / `CreateSurfacesTool`
    - `Tools/Geometry/TransformObjectsTool` / `ReplaceGeometryTool` / `DeleteObjectsTool` / `EditControlPointsTool`
    - `Tools/Geometry/PreviewTransformObjectsTool` / `PreviewReplaceGeometryTool` / `PreviewDeleteObjectsTool` / `PreviewEditControlPointsTool`（Preview 也改走 live）
    - `Tools/Editing/ApplyObjectEditsTool` / `ApplyObjectUserTextWritesTool` / `DeleteObjectUserTextTool` / `PreviewObjectEditsTool`
    - `Tools/File/SetDocumentUserStringsTool` / `DeleteDocumentUserStringsTool`
- **Skills/**
  - `Skills/Modeling/GeometryCreationSkill` / `GeometryModificationSkill`：构造函数依赖从 offline Service 切到 live Service（实现类变，接口名不变）。
  - `Skills/Editing/ObjectEditApplySkill` / `ObjectEditPreviewSkill`：构造函数依赖从 offline Service 切到 live Service（实现类变，接口名不变）。
  - `Skills/Editing/ObjectSelectionSkill` **显式拆分为两个 Skill**：
    - `OfflineObjectSelectionSkill`（保留原文件并改名）：承载 5 个 Filter* / `FindLayerCandidates` Tool 的筛查解析，依赖 offline `IRhinoDocumentRepository.Read → File3dm`。
    - `LiveObjectSelectionSkill`（新增）：承载 `PreviewObjectEdits` / Apply 侧的目标解析，依赖 `ILiveRhinoDocumentAccessor → RhinoDoc`。
    - 两者共享同一份 `FilterObjectsRequest` 契约与筛查指南（层级 / 类型 / UserAttribute），仅数据源不同；公用校验逻辑抽取到 `Application/UseCases/ObjectFilterEvaluator`（纯函数）。
    - 依赖 `ObjectSelectionSkill` 的下游调用方按执行模式一对一切换：`RhinoGeometryModificationService.ResolveSelection` / `RhinoObjectEditingService` 注入 Live 版；`FilterObjectsTool` 链路注入 Offline 版。
  - `Skills/File/`：移除 `ArchiveRetentionSkill` / `ArchiveSnapshotSkill` / `FileMutationPreflightSkill` / `FileOpenStateCheckSkill`。
- **Contracts/**
  - `Requests/`：所有 live-only Request（写入 Tool + preview-of-mutation Tool）的 `FilePath` 字段语义更新为"目标文档标识；需匹配 ActiveDoc.Path"；字段本身保留，不加不减。Offline read Request 的 `FilePath` 语义不变。
  - `Responses/`：
    - 从 `GeometryModificationResponse` / `ObjectEditExecutionResponse` / `DocumentUserStringWriteResponse` 等 DTO 的 `Warnings` 语义中移除 `FILE_MUTATION_PREFLIGHT` / `FILE_OPEN_STATE` 等已下线 warning code；常量定义同步清理。
    - 在承载 offline read / preview-of-read 的 Response DTO 中新增 `OFFLINE_READ_STALE` warning code；本次覆盖当前全部 8 个 offline read Tool（`Filter*` / `FindLayerCandidates` / `GetDocumentUserStrings` / `GetObjectUserStrings` / `PreviewObjectUserTextWrites`）对应的响应类型；未来若新增 offline read Tool，需同步补此 warning code。
    - 删除 `FileMutationPreflightResponse`、`ArchiveSnapshotResponse`、`ArchiveCleanupResponse`、`FileOpenStateInspectionResponse` 及对应 Contracts 文件。
  - `AgentMessages/`：无影响。
- **Domain/**
  - `Domain/Enums/FileOpenState.cs`、`Domain/Models/FileMutationContext.cs` 等为归档机制专用的模型一并删除；其余保留。
- **Program.cs / 入口划分**（详见关键设计 #13）：
  - `MCP_Rhino.RhinoPlugin.OnLoad`（plugin 形态，MCP Client 实际使用的入口）：构建 Host、启动 `McpNamedPipeServer`。
  - `Program.Main`（CLI 形态，开发期 offline smoke）：仅注册 offline-only 依赖，可通过 `dotnet run --project src/MCP_Rhino.Server -- ...` 用于 read / preview-of-read smoke 与 `DeveloperCommandHandler` 命令；注册的 `ILiveRhinoDocumentAccessor` 为 `NullLiveRhinoDocumentAccessor`，调用 live-only Tool 时返回 `"LIVE_RHINO_REQUIRED"`。
  - `MCP_Rhino.Bridge/Program.cs`（独立 exe）：stdio ↔ Named Pipe 桥，MCP Client spawn 的实际进程。
- **Server/DependencyInjection.cs**
  - 按执行模式分组注册：`AddOfflineRhinoAdapters()` / `AddLiveRhinoAdapters(IServiceCollection)` / `AddMcpServer()`；Plugin 入口同时调用所有分组，CLI 入口仅调 offline + MCP。
  - 注册 `ILiveRhinoDocumentAccessor` → `LiveRhinoDocumentAccessor`。
- **Infrastructure/CLI/DeveloperCommandHandler**
  - 拆分调用入口：
    - **Offline 分支**（原有）：由 `Program.Main` 调用，依赖 offline DI；用于 read / preview-of-read 回归，无需 Rhino。
    - **Live 分支**（新增）：由 plugin 注册的 Rhino Command `_McpDevSmoke` 触发，共用 plugin 的 live DI（包括真正的 `LiveRhinoDocumentAccessor`），在 Rhino 进程内跑 13 个写入 Tool 与 5 个 preview-of-mutation Tool 的 smoke；**不**经过 `Program.Main`，也**不**经过 Bridge / MCP 协议层，避免把 smoke 和生产路径的故障耦合到一起。
    - 两分支共享同一套 `DeveloperCommandHandler` 核心实现（命令解析、参数绑定、输出格式化），区别只在注入的依赖集合；通过构造函数参数接收 `IServiceProvider` 或具体接口，而不是在 handler 内部自行判断执行模式。
  - 新增文件：`src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs`（继承 `Rhino.Commands.Command`，在 plugin `OnLoad` 时随 plugin 自动注册）。
- **csproj**
   - `MCP_Rhino.Server.csproj`：新增对 `RhinoCommon` NuGet 的 PackageReference（版本与安装的 Rhino 对齐，例如 Rhino 8 → `RhinoCommon 8.x`）；交付物要求为 `.rhp`（通过 `<TargetExt>.rhp</TargetExt>` 或 `<RhinoPlugin>true</RhinoPlugin>` 属性，具体取决于 Rhino 模板）。`Program.cs` 保留为开发期 offline CLI 入口，使用方式以 `dotnet run --project src/MCP_Rhino.Server -- ...` 为准；一期不额外发布独立的 Server CLI exe。
  - `MCP_Rhino.Bridge.csproj`（**新增独立项目**）：`net7.0` / `net8.0` console app，产出 `MCP_Rhino.Bridge.exe`；仅引用 BCL 的 `System.IO.Pipes`，不引 RhinoCommon。这是 MCP Client 直接 spawn 的进程。
  - 后续若需要进一步解耦，可再引入 `MCP_Rhino.Server.Core.csproj` 承载 Application / Domain / Contracts，给 Plugin 与潜在的独立 host 共享。一期保留单一 Server csproj + 独立 Bridge csproj 的两项目结构。

# 关键设计

1. **Plugin 作为唯一的 live 宿主**
   - 所有写入路径与 preview-of-mutation 路径**强制要求** Rhino 进程已启动并加载 `.rhp`；否则 `ILiveRhinoDocumentAccessor` 的 CLI 兜底实现（`NullLiveRhinoDocumentAccessor`）直接让 `Execute` / `ExecuteWithUndo` 返回硬错误 `"LIVE_RHINO_REQUIRED"`。
   - 这比 RhinoInside 方案更稳定（避免 resolver / licensing 边界问题）、比 IPC 方案更简单（无需跨进程协议）；代价是 mutation 工具链需要 Rhino 在跑。
   - Rhino 版本锁定为 **8.x**（与当前 Rhino3dm 8.17.0 同步）；其他版本一期不测试、不承诺。

2. **目标文档解析：ActiveDoc 优先 + 路径匹配**
   - 所有 live-only Tool 接到的 `FilePath` 参数解析步骤：
     1. 取 `RhinoDoc.ActiveDoc`；若为 `null` → 返回 `NO_ACTIVE_DOCUMENT`。
     2. 若 `ActiveDoc.Path` 为空（新建未保存文档）→ 返回硬错误 `ACTIVE_DOC_UNSAVED`，提示用户先 `Save As` 到目标路径再调用；一期不支持对未命名文档做 live mutation，避免 `FilePath` 语义无法校验导致误操作。
     3. 标准化比较 `ActiveDoc.Path` 与请求的 `FilePath`（大小写不敏感 + 相对 / 绝对路径归一化）；不匹配 → 返回 `FILE_NOT_ACTIVE`，提示用户先在 Rhino 中打开对应文件。
     4. 匹配成功 → 后续操作绑定 `ActiveDoc`。
   - 不支持多文档并发 mutation（一期）；非 ActiveDoc 的其他打开文档被忽略，未来通过 `documentId` 字段扩展。
   - 若后续放开对未保存文档的支持，应引入显式契约（例如允许 `FilePath` 为空字符串代表 ActiveDoc；或在 Request 增加 `allowUnsavedDocument: bool` 标志），并同步更新校验指南。

3. **Undo 边界 = 一次写入 Tool 调用**
   - 每个写入 Tool（13 个 `Create* / Transform / Replace / Delete / EditControlPoints / ApplyObjectEdits / Apply/DeleteObjectUserText / Set/DeleteDocumentUserStrings`）：`recordId = doc.BeginUndoRecord($"MCP: {ToolName}")` → 执行变更 → `doc.EndUndoRecord(recordId)`。
   - 失败或无实际变更时改用 `doc.CancelUndoRecord(recordId)`（RhinoCommon API，直接挂在 `RhinoDoc` 上）替代 `EndUndoRecord`，避免在 Rhino Undo 栈塞入空条目；判定指南：Validate 通过但 `mutator.*` 全部返回"未改动"，或 Apply 过程抛异常回退。
   - 失败时仍必须显式 Close/Cancel（try-finally），避免 Undo 栈半开；异常场景下写入响应 `Warnings`，不调用额外的 rollback（Rhino 自身的 Undo 足够覆盖成功部分）。
   - Create 类 Tool（同一调用产生多个对象）合并为一个 Undo 条目；Preview-of-mutation 工具完全不触发 `BeginUndoRecord`。

4. **接口签名去 File3dm**
   - `IGeometryMutator` / `IObjectEditOperationApplier` 方法参数里不再出现 `File3dm`；由实现方内部通过 `ILiveRhinoDocumentAccessor` 拿上下文。
   - `IObjectEditValidator` 拆分为 spec 校验（可复用）与 live 校验；Offline 路径不再调 live 校验，Live 路径两者都调。
   - `IRhinoDocumentRepository` 保留但收窄为 read-only；offline 读工具仍然依赖它。

5. **Preview 走 live**
   - 四个 `Preview*` 几何工具 + `PreviewObjectEdits` 从 offline 迁移到 live：只读 `RhinoDoc.Objects`、不写、不开 Undo record。
   - `PreviewObjectUserTextWrites` 保持离线读语义（"会写成什么"属于纯数据预览，不需要对齐 live doc 状态）。
   - 这是对 260420_PLAN_geometry-create-modify-tools.md 中 Preview 语义的收紧：当时 Preview 允许读 File3dm 做 validation；新模型下 Preview 必须跟 Apply 看同一份 `RhinoDoc`。

6. **去归档化全量下线**
   - 删除：`IFileMutationSafeguard`、`RhinoFileMutationSafeguard`、`IArchiveSnapshotService`、`RhinoArchiveSnapshotService`、`IArchiveRetentionService`、`RhinoArchiveRetentionService`、`IFileOpenStateInspector`、`RhinoFileOpenStateInspector`。
   - 删除工具：`CreateArchiveSnapshotTool`、`CleanupArchiveTool`、`InspectFileMutationReadinessTool`。
   - 删除 Skill：`ArchiveSnapshotSkill`、`ArchiveRetentionSkill`、`FileMutationPreflightSkill`、`FileOpenStateCheckSkill`。
   - 删除 Contracts：归档相关 Request / Response DTO 及 warning code。
   - 归档目录 `test-files/archive/` 的既有快照文件保留（不自动清理）；用户可手工管理。

7. **校验策略更新**
   - 在既有校验集合基础上新增硬错误：
     - `NO_ACTIVE_DOCUMENT`：Rhino 未打开或无 ActiveDoc。
     - `ACTIVE_DOC_UNSAVED`：ActiveDoc 存在但 `Path` 为空（未保存新建文档）。
     - `FILE_NOT_ACTIVE`：`FilePath` 与 `ActiveDoc.Path` 不匹配。
     - `LIVE_RHINO_REQUIRED`：写入 Tool 或 preview-of-mutation Tool 在 CLI 模式下被调用。
     - `RHINO_MAIN_THREAD_BUSY`：主线程封送超过 10s 未排到（Rhino 有阻塞的模态命令 / 对话框）。
   - 新增软警告：
     - `OFFLINE_READ_STALE`：offline read Tool 目标文件正在 Rhino 中打开且 ActiveDoc 有未保存改动，磁盘内容可能过期。
   - 移除的软警告：`FILE_MUTATION_PREFLIGHT`、`FILE_OPEN_STATE`。
   - 其余硬 / 软错误集合（bbox 异常、NaN、退化几何等）原样保留。

8. **部署形态与 csproj 影响**
   - 两个 csproj：`MCP_Rhino.Server`（`.rhp` + offline CLI）+ `MCP_Rhino.Bridge`（stdio-to-pipe 桥 `.exe`）。
   - `Program.Main` 入口仅做 offline-only 启动；遇 live-only Tool 调用即返回 `LIVE_RHINO_REQUIRED`，**不尝试** 自动切换到 live（避免在 CLI 环境中偷偷启动 Rhino）。
   - `OnLoad` 调用链：`RhinoPlugin.OnLoad` → `DependencyInjection.AddOfflineRhinoAdapters + AddLiveRhinoAdapters + AddMcpServer` → `McpNamedPipeServer.StartAsync()`；Client 侧通过 `MCP_Rhino.Bridge.exe` 把 stdio 桥到管道（详见关键设计 #13）。

9. **测试策略**
   - **Offline read 回归**：继续依赖 `test-files/*.3dm` fixture；`tests/MCP_Rhino.UnitTests/` 首次落盘（当前只有 README），把 filter / get / inspect 类 Service 的 unit test 补上。
   - **Live write smoke**：`Project_Test/260420_TEST_online-mutation-refactor/` 新增一份 smoke 脚本，步骤：启动 Rhino 8 → 加载 plugin → 打开 `test-files/MCP_rhino_test.3dm` → 在 Rhino 命令行运行 `_McpDevSmoke` 依次触发 13 个写入 Tool 与 5 个 preview-of-mutation Tool → 校验 Preview/Apply 对齐 → 校验 Undo 能回退 → 校验关闭文档不触发落盘 → 校验空改动不产生 Undo 条目 → 校验未保存文档返回 `ACTIVE_DOC_UNSAVED`。MCP 正路（Bridge → Pipe → plugin）另行用真实 MCP Client 走冒烟，不走 `_McpDevSmoke`，确保两条路径都覆盖。
   - **无头 CI**：一期不做；依赖 Rhino.Inside + xUnit 集成的成本暂不支付，列为后续扩展。

10. **主线程封送（RhinoCommon threading）**
    - RhinoCommon 的 `RhinoDoc` / `Rhino.Geometry` 调用限定在 Rhino 主 UI 线程；但 plugin 内的 MCP 请求处理跑在 .NET 后台线程池，若直接在后台线程调用 `doc.Objects.Add` 等 API，轻则收到 `InvalidOperationException`，重则破坏 Rhino 内部状态。
    - 所有对 `RhinoDoc` 的读写（包括 preview-of-mutation 的只读查询）必须经 `Rhino.RhinoApp.InvokeOnUiThread(Action)` 封送到主线程同步执行，并等待返回值。封送边界统一集中在 `ILiveRhinoDocumentAccessor` 实现内部，业务层（Service / Skill / Tool）对线程模型无感。
    - `ILiveRhinoDocumentAccessor` 暴露 `Execute<T>(string filePath, Func<RhinoDoc, T> work)` / `ExecuteWithUndo<T>(string filePath, string undoDescription, Func<RhinoDoc, (bool mutated, T result)> work)` 作为唯一访问通路（等价于"取 doc + 路径校验 + 封送 + 执行 + 返回"的一揽子操作）；`BeginUndoRecord` / `EndUndoRecord` / `CancelUndoRecord` 也在封送内部成对完成，避免主线程切换撕裂 Undo record。
    - 超时保护：若主线程被 Rhino 自身命令（例如打开的模态对话框）占用导致封送排队 > 10s，返回硬错误 `RHINO_MAIN_THREAD_BUSY`，提示用户关闭 Rhino 中阻塞的对话框 / 命令后重试。
    - 封送实现测试点：在 `Project_Test` smoke 脚本中纳入"模态对话框打开期间调用 MCP mutation → 应收到超时错误而非挂起"的用例。

11. **Replace / Transform 策略：Live 路径采用 RhinoCommon 原生 API**
    - 260420_PLAN_geometry-create-modify-tools.md 对 Rhino3dm 环境规定了"复制属性 → 删除原对象 → 按新几何 Add"的 delete+re-add 契约（受限于 `File3dm.Objects` 没有原生 Replace 语义）。
    - Live 路径改走 RhinoCommon 原生：
      - ReplaceGeometry → `doc.Objects.Replace(Guid objectId, GeometryBase newGeometry)`（自动保留图层 / 颜色 / UserText 等 attributes，同属一条 Undo record）。
      - Transform → `doc.Objects.Transform(ObjRef, Transform xform, bool deleteOriginal=true)`，或对每个 `RhinoObject` 走 `Replace` + 几何 Duplicate+Transform 的组合，具体以能合入单条 Undo record 为准。
      - EditControlPoints → 先 `Duplicate` 出 `NurbsCurve` / `NurbsSurface`，在副本上 `Points.SetPoint`，再 `doc.Objects.Replace(objectId, modified)`；不再手动 Delete+Add。
    - 理由：原生 Replace 对 attributes 的保真度高于手写的 Duplicate-attributes 流程；同时能让单次 mutation 落入单条 Undo record，用户 `Ctrl+Z` 一步回退。
    - `IGeometryMutator` / `LiveRhinoGeometryMutator` 的实现按此契约编写；Offline 若未来恢复（作为 `--force-offline` 分支）再另行沿用 delete+re-add 骨架，Live 与 Offline 实现不共享变更代码。

12. **Offline 读与 Live 打开文档的一致性**
    - 风险：用户在 Rhino 打开 `tower.3dm` 并做了未保存改动；此时另一 MCP Client 发起 offline `FilterObjects(filePath="tower.3dm", ...)`，会读到磁盘上的旧版本，返回与用户 Rhino 中所见不符的对象清单。
    - 策略：offline read Tool 在执行前可向 `ILiveRhinoDocumentAccessor` 做一次 best-effort 查询：`TryGetActiveDocumentState(filePath, out hasUnsavedChanges)`。
      - 返回 `true` 且 `hasUnsavedChanges == true` → 在响应 `Warnings` 写入 `OFFLINE_READ_STALE`，提示"当前文件在 Rhino 中已打开且有未保存改动，离线读取结果可能过期"；继续返回磁盘结果。
      - 返回 `true` 且 `hasUnsavedChanges == false` → 不警告（磁盘 = 内存）。
      - 返回 `false` → 表示无 ActiveDoc / 路径不匹配 / 当前无 Live 宿主可查询；不警告。
    - 该查询是 best-effort 的告警增强，不是 offline read 成功返回的前提。CLI fallback（无 Rhino 进程）时 `NullLiveRhinoDocumentAccessor` 直接返回 `false`，等价于无警告。
    - 契约归属：`IRhinoDocumentRepository` 只读磁盘，不承担 stale 检测；stale warning 的产生由 **offline read Service 的实现契约** 负责（在调 Repository 前后插入 `TryGetActiveDocumentState` 查询并装配 `Warnings`），并在 **Response DTO 的 warning code 常量** 中登记 `OFFLINE_READ_STALE`。本次在全部 8 个 offline read Tool 对应的 Service 与 Response 中同步落地；Repository 侧不改注释。

13. **传输层：Named Pipe + stdio-to-pipe Bridge CLI**
    - 问题：MCP 官方 stdio transport 的启动模型是 **Client 派生 server 子进程并接管其 stdin/stdout**。本次的 MCP server 跑在 Rhino 进程内（plugin），Client 既无法派生 Rhino，也不能抢占 Rhino 已占用的 stdio。
    - 决策：采用 **命名管道 + 桥接 CLI** 拆成两段传输：
      1. **Pipe server（in-plugin）**：Rhino plugin 在 `OnLoad` 里起一个 Windows Named Pipe server（管道名一期固定为 `mcp_rhino`，即 `\\.\pipe\mcp_rhino`），承担真正的 MCP server 侧逻辑。MCP 协议帧直接在 pipe 上承载（JSON-RPC over 字节流，换行分帧，与 stdio 等价）。
      2. **Bridge CLI（new process）**：新增一个轻量 `MCP_Rhino.Bridge.exe`，职责仅是：启动后连接 `\\.\pipe\mcp_rhino` → 把自己的 stdin 内容原样写入管道 → 把管道响应原样写回 stdout。失败条件：pipe 不存在（Rhino 没跑 / 插件没加载）→ stderr 输出友好提示后退出非零。
      3. **Client 侧**：MCP Client（Claude Desktop / Cline 等）像连普通 stdio server 一样配置 `command: "MCP_Rhino.Bridge.exe"`；Client 只跟 bridge 打交道，bridge 把消息透传给 plugin。
    - Bridge 是 **live-only** 的；它要求 Rhino 已启动并加载插件。offline CLI 操作仍走 `Program.Main` → `DeveloperCommandHandler`，不经过 bridge、不要求 Rhino 在跑。
    - 管道名 vs 多实例：一期锁定单一固定名（`mcp_rhino`）。若同机多开 Rhino，后启的插件 `OnLoad` 发现管道已占用时拒绝启动并写日志，要求用户先关闭前一个。多实例路由列入后续扩展（按 Rhino 进程 pid / doc 路径派发管道名）。
    - 生命周期：plugin `OnShutdown` 关闭 pipe server 并释放管道；bridge 检测到管道断开即退出；Client 收到 stdio EOF 触发重连。
    - 安全：Named Pipe 默认为当前 Windows user 的 ACL，只允许同一用户访问；本机多用户场景不纳入首期范围。
    - 依赖：Pipe server 使用 `System.IO.Pipes.NamedPipeServerStream`（BCL 自带，不引新包）；MCP JSON-RPC 协议帧由既有 MCP SDK 复用（若 SDK 提供 stream-based transport 则直接套用，否则在 `McpNamedPipeServer` 内手写一层薄封装）。
    - 不做事项（一期）：网络可见的 TCP / HTTP / SSE transport；跨主机访问；pipe 上的加密（依赖 Windows ACL）。

# 涉及文件

新增：

- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs`（Rhino Command `_McpDevSmoke`，触发 `DeveloperCommandHandler` 的 live 分支）
- `src/MCP_Rhino.Server/Infrastructure/Plugin/plugin.manifest`
- `src/MCP_Rhino.Bridge/Program.cs`
- `src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/NullLiveRhinoDocumentAccessor.cs`（CLI fallback 的空实现；按"实现 `ILiveRhinoDocumentAccessor` 接口"归类于 Rhino 适配根目录，与其他跨模式共享类并列，不放 `Offline/` 以免误导）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryMutator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoObjectEditOperationApplier.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoObjectUserTextWriter.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentUserStringWriter.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoGeometryValidator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveRhinoDocumentAccessor.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IObjectEditSpecValidator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveObjectEditValidator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveGeometryValidator.cs`
- `src/MCP_Rhino.Server/Application/UseCases/ObjectFilterEvaluator.cs`
- `src/MCP_Rhino.Server/Skills/Editing/LiveObjectSelectionSkill.cs`
- `Project_Test/260420_TEST_online-mutation-refactor/LiveMutationSmokeTest.md`

修改：

- `.clinerules/MCP_Rhino Architecture.md`（本计划的姊妹改动，单独提交）
- `src/MCP_Rhino.Server/Program.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Server/ToolRegistration.cs`
- `src/MCP_Rhino.Server/Server/AgentRegistration.cs`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Application/Interfaces/IRhinoDocumentRepository.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IGeometryMutator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IObjectEditOperationApplier.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IObjectEditValidator.cs`（拆分后删除或收窄）
- `src/MCP_Rhino.Server/Application/Services/RhinoGeometryCreationService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoGeometryModificationService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectEditingService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectUserTextService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoDocumentUserStringService.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/GeometryCreationSkill.cs`
- `src/MCP_Rhino.Server/Skills/Modeling/GeometryModificationSkill.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectEditApplySkill.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectEditPreviewSkill.cs`
- `src/MCP_Rhino.Server/Skills/Editing/ObjectSelectionSkill.cs` → 重命名为 `OfflineObjectSelectionSkill.cs`；Filter* / FindLayerCandidates 链路切到此类
- `src/MCP_Rhino.Server/Tools/Geometry/*.cs`（12 个；签名不变，依赖链下的 Service 切 live）
- `src/MCP_Rhino.Server/Tools/Editing/ApplyObjectEditsTool.cs` / `ApplyObjectUserTextWritesTool.cs` / `DeleteObjectUserTextTool.cs` / `PreviewObjectEditsTool.cs`
- `src/MCP_Rhino.Server/Tools/File/SetDocumentUserStringsTool.cs` / `DeleteDocumentUserStringsTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/PreviewObjectUserTextWritesTool.cs`（保持 offline 读，仅确认依赖未误切）
- `src/MCP_Rhino.Server/Contracts/Requests/`（mutation Request 中 `FilePath` 字段注释语义更新）
- `src/MCP_Rhino.Server/Contracts/Responses/GeometryModificationResponse.cs` / `ObjectEditExecutionResponse.cs` / `DocumentUserStringWriteResponse.cs`（移除归档相关 warning code 常量）
- `src/MCP_Rhino.Server/Contracts/Responses/`（为首批 offline read / preview-of-read Response DTO 增加 `OFFLINE_READ_STALE` warning code）
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler*.cs`（新增 live 分支；offline 分支对 live-only 命令返回 `LIVE_RHINO_REQUIRED`）

搬迁（文件移动，内容不变 / 仅调整 namespace）：

- `src/MCP_Rhino.Server/Infrastructure/Rhino/*Repository.cs` / `*Reader.cs` / `*FilterService.cs` / `*CandidateService.cs` → `src/MCP_Rhino.Server/Infrastructure/Rhino/Offline/`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoGeometryBuilder.cs` → `Offline/`（spec-only builder 同时被 live 复用，通过 interface 共享）
- `src/MCP_Rhino.Server/Infrastructure/Rhino/PassThroughEditResultFormatter.cs` → `Offline/`（Live / Offline 共享）

删除：

- `src/MCP_Rhino.Server/Application/Interfaces/IFileMutationSafeguard.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IArchiveSnapshotService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IArchiveRetentionService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IFileOpenStateInspector.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoFileMutationSafeguard.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoArchiveSnapshotService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoArchiveRetentionService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoFileOpenStateInspector.cs`
- `src/MCP_Rhino.Server/Tools/File/CreateArchiveSnapshotTool.cs`
- `src/MCP_Rhino.Server/Tools/File/CleanupArchiveTool.cs`
- `src/MCP_Rhino.Server/Tools/File/InspectFileMutationReadinessTool.cs`
- `src/MCP_Rhino.Server/Skills/File/ArchiveSnapshotSkill.cs`
- `src/MCP_Rhino.Server/Skills/File/ArchiveRetentionSkill.cs`
- `src/MCP_Rhino.Server/Skills/File/FileMutationPreflightSkill.cs`
- `src/MCP_Rhino.Server/Skills/File/FileOpenStateCheckSkill.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/FileMutationPreflightResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ArchiveSnapshotResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ArchiveCleanupResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/FileOpenStateInspectionResponse.cs`
- `src/MCP_Rhino.Server/Domain/Enums/FileOpenState.cs`
- `src/MCP_Rhino.Server/Domain/Models/FileMutationContext.cs`

（上述文件若实际路径 / 名称与当前仓库存在差异，以仓库实际为准；ACT 阶段以 `Grep` 对接口名、类名的引用点为准核对。）

# 使用方式

## 部署

1. 安装 Rhino 8（与 `RhinoCommon` NuGet 版本对齐）。
2. 执行 `dotnet build` 同时构建：
   - `src/MCP_Rhino.Server/` → 输出 `MCP_Rhino.Server.rhp`（Rhino plugin）
   - `src/MCP_Rhino.Bridge/` → 输出 `MCP_Rhino.Bridge.exe`（stdio-to-pipe 桥接 CLI）
3. 在 Rhino 中运行 `_PlugInManager`，载入 `.rhp`；加载成功后 plugin 启动 Named Pipe server（管道 `\\.\pipe\mcp_rhino`）。
4. MCP Client（Claude Desktop / Cline 等）配置为 spawn `MCP_Rhino.Bridge.exe`；bridge 把 Client 的 stdio 流桥接到 plugin 的命名管道。Client 的配置示例：
   ```json
   {
     "mcpServers": {
       "rhino": { "command": "C:/path/to/MCP_Rhino.Bridge.exe" }
     }
   }
   ```
5. 若用户启动 Client 时 Rhino 尚未开启 / 插件未加载，bridge 连管道失败并向 Client 返回 stderr 错误；用户打开 Rhino + 加载插件后重启 Client 即可。

## MCP Tool 行为

工具名与参数**一字不改**，用法与 260420_PLAN_geometry-create-modify-tools.md 列出的完全一致：

- 创建：`CreatePoints / CreateLines / CreateArcs / CreateSurfaces`
- 修改-Apply：`TransformObjects / ReplaceGeometry / DeleteObjects / EditControlPoints`
- 修改-Preview：`PreviewTransformObjects / PreviewReplaceGeometry / PreviewDeleteObjects / PreviewEditControlPoints`
- 对象编辑：`ApplyObjectEdits / ApplyObjectUserTextWrites / DeleteObjectUserText / PreviewObjectEdits / PreviewObjectUserTextWrites / GetObjectUserStrings`
- 文档元数据：`SetDocumentUserStrings / DeleteDocumentUserStrings / GetDocumentUserStrings`
- 筛查：`FilterObjects / FilterObjectsByType / FilterObjectsByUserAttributes / FilterObjectsByLayer / FindLayerCandidates`

调用前提新增：**所有 live-only Tool 的目标文件必须已在 Rhino 中打开并为 ActiveDoc，且已保存到磁盘获得路径**；未满足条件时分别返回硬错误 `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`。若 Rhino 主线程被模态对话框 / 命令阻塞 > 10s，返回 `RHINO_MAIN_THREAD_BUSY`。纯 read 与 `PreviewObjectUserTextWrites` 不受以上限制；offline read Tool 命中"目标文件正在 Rhino 中打开且有未保存改动"时，响应 `Warnings` 携带 `OFFLINE_READ_STALE`。

## 撤销与保存

- Apply 工具调用成功后：Rhino 的 Edit → Undo（`Ctrl+Z`）可撤销，撤销粒度 = 一次工具调用。
- 文件落盘由用户手动触发（`File → Save` / `Ctrl+S`）；MCP server 不代替用户保存，也不再做归档快照。

## CLI Fallback

`DeveloperCommandHandler`（`Program.Main` 入口）支持 offline 命令（filter / get / inspect / `PreviewObjectUserTextWrites`）用于无 Rhino 环境下的 smoke；建议通过 `dotnet run --project src/MCP_Rhino.Server -- ...` 调用；live-only 命令返回 `LIVE_RHINO_REQUIRED`。

## 典型流程

1. 用户在 Rhino 中打开 `D:/projects/tower.3dm`，加载 MCP plugin。
2. MCP Client 发送 `PreviewTransformObjects(filePath="D:/projects/tower.3dm", transform=Translate(0,0,3000), layerQueries=["Floor::Level 3"])`。
3. Plugin 解析 → 匹配 ActiveDoc → 只读 `RhinoDoc` 返回受影响对象清单。
4. Client 根据预览确认 → 调 `TransformObjects(...)`。
5. Plugin 打开 Undo record → 在 `RhinoDoc` 上逐个 Transform → Close Undo record → 返回结果。
6. 用户在 Rhino viewport 实时看到变化；不满意 `Ctrl+Z` 撤销；满意 `Ctrl+S` 保存。

# 后续扩展方向

- **高级建模能力**：引入 RhinoCommon 独占 API（Loft / Sweep / Revolve / Extrude / Brep 布尔 / Fillet / Offset），作为独立的 live-only 能力批次。
- **Grasshopper 集成**：`ILiveRhinoDocumentAccessor` 扩展为 `ILiveRhinoHostAccessor`，支持触发 GH 定义 / 读取 GH 参数。
- **Viewport 可视化 Preview**：借 `Rhino.Display.DisplayConduit` 在 Preview 时绘制虚影高亮，真正的图形级预览，替换当前文本预览。
- **多文档路由**：Request 增加 `documentId?` 字段，允许在多个打开文档间定位；`ILiveRhinoDocumentAccessor` 暴露 `GetDocumentById`。
- **无头 Rhino CI**：以 `Rhino.Inside.Runtime` 在 CI 机器上启动 headless Rhino，用 xUnit 跑 live mutation 集成测试，纳入 PR 检查。
- **显式离线 mutation 开关**：若后续出现"必须在无 Rhino 环境下批量改 `.3dm`"的合理诉求（如服务器批处理），引入 `--force-offline` 显式开关，独立走旧 File3dm 骨架，默认禁用并在日志中告警。
- **Save 策略可配置**：支持可选"工具调用后自动 Save"模式（默认关），用于自动化流水线。
- **接口进一步分层**：若 live 实现种类增多，可把 `ILiveRhinoDocumentAccessor` 下沉为 `Domain/` 内的抽象 + `Infrastructure/Rhino/Live/` 的实现，Application 层只持有 Domain 抽象。
