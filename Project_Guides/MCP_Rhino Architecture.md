# MCP_Rhino Architecture Guides

## 目标

本指南用于约束 MCP_Rhino 项目后续新增代码与功能的归属，避免再次退化为单文件、大杂烩结构。

## 目录归属指南

### 1. Program.cs
- 只负责应用启动、Host 构建、调用注册扩展。
- 不放业务逻辑。
- 不放 RhinoCommon 读写细节。
- MCP server 的默认启动形态为 **Rhino Plugin 加载回调**（`.rhp` 宿主）；plugin 在 Rhino 进程内启动 **Named Pipe MCP server**。外部 Client 由独立的 `MCP_Rhino.Bridge` 项目产出的 `.exe` 承担 stdio-to-pipe 桥接。`Program.cs` 保留为可执行入口，但仅服务于开发期 live smoke fallback，不承担 MCP host 角色。

### 2. Server/
- `DependencyInjection.cs`：注册内部服务、仓储、基础设施实现。
- `ToolRegistration.cs`：注册 MCP tool 模块。
- `ResourceRegistration.cs`：注册 MCP resource 模块。Resources 只能暴露 reference-only 内容；不得读取或修改 live Rhino 文档。
- `AgentRegistration.cs`：注册 agent / skill 及未来编排组件。

### 3. Tools/
- 只放直接暴露给 MCP Client 的原子能力。
- Tool 负责参数接收、调用 Application/Service、格式化输出。
- Tool 中禁止堆积大段 Rhino 文件读写逻辑。
- 命名统一使用 `*Tool` 后缀。
- Tool family ownership:
  - `Tools/Analysis`：live document inspection, filtering, resolving, measuring, intersections, metrics, and other read-only document analysis.
  - `Tools/Geometry`：general geometry creation, transform, delete, replace, and object-level geometry mutations.
  - `Tools/Geometry/Edit`：editable curve / surface descriptor, preview, and apply operations.
  - `Tools/Geometry/Rebuild`：surface rebuild and direction correction workflows exposed as atomic MCP tools.
  - `Tools/Geometry/Architecture`：architectural primitives, architectural booleans, openings, and architecture-specific block insertion tools.
  - `Tools/Geometry/CurveOps`：future curve-derived construction, curve segmentation, split, offset, pipe, projection, and sweep/loft/extrude operations.
  - `Tools/Geometry/SubD`：SubD cage preview, bounded SubD creation, soft product / cushion SubD creation, and SubD inspection tools.
  - `Tools/Selection`：future Rhino UI selection reads and selection-state mutations.
  - `Tools/Viewport`：future in-band viewport capture and viewport-only inspection. File-based exports stay under `Tools/File/Export`.
  - `Tools/Reference`：future read-only searchable reference tools only when MCP resources are not reliable across clients.
  - `Tools/Layers`：layer reads, previews, creates, edits, deletes, and purge operations.
  - `Tools/Blocks`：block definition / instance inspection and lifecycle tools.
  - `Tools/File`：document-level metadata and live file operations.
  - `Tools/File/Export`：external file export tools; these are open-world.
  - `Tools/File/Reference`：live external-reference state such as worksession attachments and linked block updates.
  - `Tools/Drawing`：drawing-view setup, drawing export state, styling, and packaged drawing export.
  - `Tools/Editing`：object attributes, object user text, and generic object edit preview/apply tools.
  - `Tools/Modeling`：thin externally callable wrappers for goal-level modeling agents. These tools only adapt MCP requests to registered Agents/Skills, own safety annotations and routing descriptions, and must not duplicate orchestration or RhinoCommon logic.
  - `Tools/Workflow`：repository workflow support tools such as activity logging.
- Add a new Tool subfolder only when there are multiple related tools and a stable capability boundary. One-off tools should join the nearest existing family.
- **执行模式约束**：所有 Tool 只能依赖 Live 适配接口获取当前 Rhino 文档真值；不得提供磁盘文件读取或写入兜底。详见「执行模式指南」章节。
- **MCP 安全注解约束**：每个方法级 `[McpServerTool]` 必须显式声明 `ReadOnly`、`Destructive`、`OpenWorld`。详见「MCP Tool Safety Annotation Guidelines」章节。

### 4. Skills/
- 放置固定流程的复合能力。
- Skill 可以组合多个 service / use case / tool。
- Skill 不直接承担底层文件 API 细节。
- 命名统一使用 `*Skill` 后缀。
- **执行模式约束**：Skill 只能组合 Live 能力；不得引入磁盘文件读取或写入分支。

### 5. Agents/
- 放置目标驱动、可做决策/调度的执行者。
- Agent 优先调用 Skill 或 Application Service。
- Agent 不直接写 RhinoCommon 细节。
- 命名统一使用 `*Agent` 后缀。
- **执行模式约束**：Agent 的 read / preview / mutation 步骤都必须走 Live 适配接口；不得在一次调用里混入磁盘文件真值。

### 6. Application/
- 放置用例、服务、流程编排、接口抽象。
- 这里回答“系统如何完成某个功能”。
- Application 依赖抽象接口，不直接耦合具体基础设施实现。
- 命名建议使用 `*Service`、`*UseCase`、`I*`。

### 7. Domain/
- 放置核心业务模型、值对象、指南、枚举。
- Domain 不依赖 MCP、RhinoCommon、文件系统实现。
- 这里回答“业务概念和指南是什么”。

### 8. Infrastructure/
- 放置 Rhino 适配（`RhinoDoc` / RhinoCommon 在线读写）、文件系统、配置、日志、命令行入口适配等具体实现。
- 所有 `RhinoDoc` / `RhinoCommon` 访问细节集中在这里。
- 这里回答"具体如何和外部技术打交道"。

- **Rhino 适配组织**：`Infrastructure/Rhino/` 承载 RhinoCommon 适配；所有与文档状态有关的适配器以 `Live*` 前缀命名。历史保留的 `Rhino/Live/` 子目录可继续作为分组使用，但项目执行模式只有 Live 一种。
- **`Plugin/` 子目录**：放置 Rhino `.rhp` 宿主入口（`MCP_Rhino.RhinoPlugin.cs`、`McpNamedPipeServer.cs` 等）。在 `OnLoad` 里尝试启动 **Developer Debug Named Pipe MCP server**（固定管道 `\\.\pipe\mcp_rhino`），承载测试 / smoke / 外部调试 MCP 协议帧；Client 端经独立的 `MCP_Rhino.Bridge` 项目提供的 stdio-to-pipe 桥接 exe 连入。多 Rhino 进程并行时，固定 debug 管道只能由一个进程拥有；其它 Rhino 进程不得因此加载失败，仍必须能启动自己的 process-scoped panel-bound 管道。
- **Plugin build-mode contract**：`Debug` and `Release` both build `MCP_Rhino.Server.rhp`, but they intentionally expose different Rhino UI surfaces. `Debug` defines `MCP_RHINO_BRIDGE_PIPE_ONLY`, loads only when needed, and preserves the old bridge-pipe-only plugin shape: it starts only the Developer Debug Control Path (`\\.\pipe\mcp_rhino`) and must not launch Companion, Rhino-hosted chat panel, panel-bound pipes, or routed document endpoints. Packaged `Release` returns `PlugInLoadTime.AtStartup`: it keeps the debug pipe, enables `_Mcpchat` / Companion / panel-bound pipes, and publishes one independent route endpoint for every saved open document in the Rhino process. Startup route failures are diagnostic and must not prevent Rhino from loading. The MCP tool registration and protocol surface must stay shared across both configurations.
- **MCP update validation rule**：Any construction work that changes the MCP server, plugin host, bridge compatibility, tool registration, panel/session routing, or runtime prompt injection must compile the Debug plugin as well as the Release plugin. The validation record must include `dotnet build .\MCP_Rhino.sln -c Debug` and `dotnet build .\MCP_Rhino.sln -c Release`, or a narrower Debug/Release project build with a stated reason. If the tool surface changes, run the MCP tool safety smoke in both configurations.
- **`Plugin/Panel/` 子目录**：放置 Rhino 内 fallback chat panel、per-document panel dispatcher、panel chat session glue。默认 `_Mcpchat` 优先启动独立 Companion；只有 Companion 不可用时才使用 Rhino-hosted fallback panel。
- **`Plugin/Companion/` 子目录**：放置从 Rhino plugin 启动独立 Companion 进程的薄适配（launch spec、process launcher、session handle 等）。这里只负责 Rhino 侧启动与生命周期衔接，不承载 LLM session 业务。
- `CLI/` 放置面向开发者 / 终端的命令行适配实现（例如 `DeveloperCommandHandler`），作为外部入口到 Application / Agent 层的薄适配层；`Program.cs` 只负责解析并委托给这里。

### 9. Contracts/
- 放置 Request / Response / Agent 消息 DTO。
- Contracts 只负责数据交换结构，不承载复杂业务指南。
- 命名统一使用 `*Request`、`*Response`、`*Message`。

### 10. Resources/
- 放置 MCP resources 使用的 reference-only 内容与 provider。
- Resource 不属于 Tool / Skill / Agent，不执行 Rhino 操作。
- Resource 不得读取、检查、修改、导出 live Rhino 文档，也不得包装 mutation preview。
- Resource 可用于 RhinoCommon / RhinoScript reference、tool help、static modeling policy、generated but non-executing documentation。
- 命名统一使用 `*Resource` 后缀，注册入口统一通过 `Server/ResourceRegistration.cs`。
- 如果某项能力需要 live document truth、Undo、selection、viewport state, or filesystem output, it belongs under `Tools/`, not `Resources/`.
- If MCP resource support is not consistently available in a target client, add a read-only fallback under `Tools/Reference` and keep the resource as the preferred reference surface.

### 11. Prompts/
- 放置 Agent / Skill 使用的提示词模板。
- 长 prompt 不要硬编码在 C# 类中。
- 可使用 `.md`、`.txt`、`.yaml` 等文本文件组织。
- `Prompts/Runtime/McpRhinoRuntimePolicyBundle.md` 是 chat panel / Companion 注入给 LLM 的 runtime-only policy bundle；它只约束运行时 Rhino / 外部文件任务，不承载能力构建规则。
- 该 runtime policy bundle 只允许由 chat panel / Companion prompt 路径加载；仓库工作区 / test route 仍使用 `AGENTS.md`、`Runtime_Workflow/`、`Project_Guides/`，不得把 panel runtime policy 当作仓库级规则注入。

## 新功能归属判断表

- 单一动作、直接暴露给 MCP → `Tools/`
- 固定工作流、组合多个能力 → `Skills/`
- 目标驱动、需要选择步骤或调度 → `Agents/`
- 功能流程编排、服务组织 → `Application/`
- 业务模型、值对象、指南 → `Domain/`
- Rhino 适配（`RhinoDoc` 在线）/ IO / Logging / Config 实现 → `Infrastructure/`
- 请求/响应/消息结构 → `Contracts/`
- 静态 / reference-only MCP 内容 → `Resources/`
- LLM 指令模板 → `Prompts/`

## MCP Surface Governance

- `Server/ToolRegistration.cs` remains the single MCP tool registration entry point and should keep assembly scanning via `WithToolsFromAssembly(...)`.
- `Server/ResourceRegistration.cs` is the single MCP resource registration entry point and should keep assembly scanning via `WithResourcesFromAssembly(...)` when resources are enabled.
- Do not introduce a central hand-maintained command dictionary or runtime tool catalog. C# `[Description]` attributes on tool/skill wrapper methods are the routing metadata source.
- Generated inventories are validation output only. They may be printed by smoke tests or written as release-note artifacts, but they must not become a second runtime registry.
- Every MCP tool method must have a non-empty `[Description]` describing the operation, expected inputs, and boundaries clearly enough for model routing.
- MCP tool method names must stay unique across the server surface. If two operations need the same natural verb, make the method names more specific.
- Do not expose equivalent live-only aliases such as both `Foo` and `FooInLive` after the capability has migrated to Live Only. Keep one canonical MCP method and remove the duplicate wrapper from the tool surface.
- Do not keep thin single-criterion wrapper tools when a canonical structured tool covers the same criteria with equal or better routing metadata, unless the wrapper owns a stable domain boundary that materially reduces user error.
- Preview/apply pairs are required when an operation can delete or replace existing Rhino objects, expand beyond explicitly confirmed object ids, produce uncertain result counts, or depend on ambiguous matching.
- Creation-only tools may be apply-only when they only add new Rhino objects and do not delete, replace, purge, overwrite external files, or mutate selection outside their explicit request.
- Tool/resource surface changes must keep the inventory smoke passing and must keep the MCP tool safety annotation smoke passing in Debug and Release builds.

## MCP Tool Safety Annotation Guidelines

Every future method-level `[McpServerTool]` must use explicit named safety annotations. Do not rely on `ModelContextProtocol` defaults.

- Read / Filter / Get / Find / Inspect / Measure / Intersect / Resolve / Preview tools must be annotated as `ReadOnly = true, Destructive = false, OpenWorld = false`.
- Mutation tools that only change the current live Rhino document must be annotated as `ReadOnly = false, OpenWorld = false`, with `Destructive` set to the actual behavior. Create / transform / edit / set operations are normally `Destructive = false`; delete / replace / purge operations are normally `Destructive = true`.
- Export, logging, external-reference, or any tool that writes to caller-provided filesystem paths or external state must set `OpenWorld = true`. If it can overwrite or remove external output, set `Destructive = true`.
- Never mark a mutation or export tool as read-only just to bypass Codex or another MCP client's approval behavior. If a destructive tool needs approval UX, solve that in the panel/client flow.
- When adding, renaming, or removing an MCP tool, update `Project_Test/260506_TEST_mcp-tool-safety-annotations/DeveloperCommandHandler.McpToolSafetyAnnotationsSmokeTest.cs` and run `mcp-tool-safety-annotations-smoke-test`. The smoke must fail if any method-level `[McpServerTool]` is missing explicit `ReadOnly`, `Destructive`, or `OpenWorld` metadata.
- MCP resources have no equivalent mutation-safety annotation in the current SDK surface. Therefore resources in this repository are restricted to reference-only content by architecture rule.

## 执行模式指南（Live Only）

Rhino 侧的能力只跑一种模式：经 RhinoCommon 操作运行中的 `RhinoDoc`。本仓库不再支持磁盘 `.3dm` 业务读写。

### 通用契约

- 所有能力必须由 Rhino Plugin 宿主加载，或经未来的 panel-bound MCP server 驱动。
- 全局 pipe 路径解析为 `RhinoDoc.ActiveDoc`。目标文档必须存在、已 Save 到磁盘（`doc.Path` 非空）、且 `doc.Path` 与请求中的 `FilePath` 匹配，否则分别返回 `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`。
- Panel-bound 路径解析为 `RhinoDoc.FromRuntimeSerialNumber(boundSerial)`。目标文档必须存在、已 Save 到磁盘；请求 `FilePath` 在 bound mode 下被忽略并重写为绑定文档当前 `Path`。文档不存在 / 未保存分别返回 `DOCUMENT_CLOSED` / `ACTIVE_DOC_UNSAVED`；正常不返回 `FILE_NOT_ACTIVE`。
- RhinoCommon 的 `RhinoDoc` / `Rhino.Geometry` API 限定在 Rhino UI 主线程。所有访问必须经 `ILiveRhinoDocumentAccessor` 封送到主线程同步执行；主线程长时间阻塞（> 10s）时返回 `RHINO_MAIN_THREAD_BUSY`。
- 不允许在 live 失败后改读磁盘快照；调用方必须面对当前 Rhino 会话状态。

### 读 / Preview-of-read

- Read / Filter / Get / Find / Inspect，以及 preview-of-read 类能力都走 Live 路径。
- 只读路径不得开启 Undo record，也不得修改任何文档状态。

### 写 / Mutation / Preview-of-mutation

- Create / Transform / Replace / Delete / EditControlPoints / ApplyObjectEdits / Apply/DeleteObjectUserText / Set/DeleteDocumentUserStrings、preview-of-mutation，以及未来所有改动文档状态或依赖在线文档一致性的新能力都走 Live 路径。
- mutation 使用 RhinoCommon API 修改文档，实时刷新视口，并进入 Rhino Undo 栈。
- 每次 Apply 调用用 `doc.BeginUndoRecord(...)` / `EndUndoRecord(...)` 包裹，保证"一次写入 Tool 调用 = 一次 Undo 条目"。无实际变更时 Rhino 会丢弃空 Undo record。
- 修改 Geometry / Attributes 优先使用 RhinoCommon 原生 API（`doc.Objects.Replace` / `doc.Objects.Transform`），保留 attributes 保真度并合并 Undo record。
- Preview-of-mutation 只读 `RhinoDoc`、不写、不开 Undo record，但必须看见与随后 Apply 相同的 live 文档状态。

### CLI 进程入口

- `Program.cs` + `DeveloperCommandHandler` 的 CLI 模式只承担工具注册 smoke 与 live 调用 fallback。
- CLI 模式下 `ILiveRhinoDocumentAccessor` 解析为 `NullLiveRhinoDocumentAccessor`，所有 live tool 返回 `LIVE_RHINO_REQUIRED`；该模式用于验证 DI、tool 签名与 partial smoke 注册，不承担业务功能。

### 去归档化

- 因 Rhino Undo 已覆盖"误操作可回退"的核心诉求，`IFileMutationSafeguard` / Archive snapshot / preflight warning 机制不再保留，相关 Tool、Skill、Service、接口一并下线。
- Save 时机回归 Rhino 既有流程（用户手动 Save 或 Rhino 自带 AutoSave）。

### Developer Debug Control Path（Global Pipe）

`\\.\pipe\mcp_rhino` 是开发者调试控制路径，目标文档解析为 `RhinoDoc.ActiveDoc`。

- 触发场景：外部 MCP client 或 smoke 经 `MCP_Rhino.Bridge.exe` 默认参数连接 `\\.\pipe\mcp_rhino`。
- 用途：快速验证新 tool / skill / agent 的 live Rhino 行为、回归既有能力、调试外部 MCP client 配置。
- 约束：目标文档必须是当前 `RhinoDoc.ActiveDoc`，且请求 `FilePath` 必须与 `ActiveDoc.Path` 匹配；否则沿用 `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`。
- 这条路径是正式保留的开发入口，但不是最终多文档 / 多 Rhino 进程用户体验路径；外部 agent 的多文档、多进程控制必须走 Router path，`_Mcpchat` / Companion 继续走 panel-bound per-doc server。
- `mcp_rhino` 是单 owner debug pipe：同一台机器上只能有一个 Rhino 进程拥有它。第二个 Rhino 进程如果遇到 `All pipe instances are busy`，必须只禁用本进程的 debug pipe 并继续提供 panel-bound 管道，不能进入无限错误循环，也不能阻止插件加载。
- `bin/Debug/net8.0/MCP_Rhino.Server.rhp` is the preferred plugin for bridge-only external MCP client testing. It must keep this debug pipe path working and must reject chat panel / panel-bound startup.
- `bin/Release/net8.0/MCP_Rhino.Server.rhp` must remain compatible with the same bridge path while also supporting `_Mcpchat` and panel-bound execution.
- 新能力的 capability smoke 可以优先走该路径或专属 Rhino smoke command 验证业务能力；panel smoke 只验证 panel / bound accessor / CC / per-doc pipe 生命周期。

### Panel-bound execution mode（Per-Document）

Live Only 是默认模式，绑定到 `RhinoDoc.ActiveDoc`。本子节描述 panel 启用的 per-doc 绑定变体。

- 触发场景：Rhino 内由 chat panel / companion 启动的 per-doc MCP server 实例（`\\.\pipe\mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`）。
- 实现：`Infrastructure/Rhino/Live/BoundLiveRhinoDocumentAccessor` 实现 `ILiveRhinoDocumentAccessor`，所有 `RhinoDoc` 解析都来自 `RhinoDoc.FromRuntimeSerialNumber(boundSerial)`，不走 `ActiveDoc`。
- Pipe 命名必须同时包含 Rhino 进程 id 与 `RuntimeSerialNumber`。`RuntimeSerialNumber` 只保证在单个 Rhino 进程内稳定，不保证跨 Rhino 进程唯一；禁止恢复为 `mcp_rhino_<RuntimeSerialNumber>` 这类跨进程可碰撞命名。
- Panel / companion 生成的临时 MCP config 目录也必须按 pipe name 隔离，不能只按 `RuntimeSerialNumber` 隔离。
- Panel / companion 启动 Claude / Codex / bridge 等 LLM 子进程时，进程工作目录必须使用按 pipe name 隔离的临时目录；禁止把 `.3dm` 所在目录作为 CLI working directory。绑定文档路径只能作为 prompt / MCP tool 参数传递，不能作为本地文件工具、project workspace、session persistence 或 fallback 读取的入口。
- 约束：
  - 绑定 doc 必须存在（`FromRuntimeSerialNumber` 非 null）→ 否则返回 `DOCUMENT_CLOSED`。
  - 绑定 doc 必须已落盘（`doc.Path` 非空）→ 否则沿用 `ACTIVE_DOC_UNSAVED`。
  - 请求 `FilePath` 字段在 bound mode 下被忽略并自动重写为绑定 doc 的当前 `Path`；正常不返回 `FILE_NOT_ACTIVE`。
  - 主线程封送、Undo record 包裹、超时（`> 10s` → `RHINO_MAIN_THREAD_BUSY`）等约束沿用 Live Only §通用契约。
- 全局管道（`\\.\pipe\mcp_rhino`）作为 Developer Debug Control Path 保留 `ActiveDoc`-following 严格语义，不受本子节约束。
- 新增错误码 `DOCUMENT_CLOSED`：bound doc 在 tool call 飞行期间被关闭。客户端处理建议：停止后续调用；面板侧此时已经在 dispatcher 触发 panel 销毁。
- 并发：per-doc panel 不开启并行写入。所有 mutation 仍经 `RhinoApp.InvokeOnUiThread` 序列化在 UI 主线程，跨 panel 串行执行。

### External Multi-Document Router Path

- Packaged Release Rhino plug-in startup creates a current-user route endpoint per saved open
  document. Pipe names use `mcp_rhino_route_<ProcessId>_<RuntimeSerialNumber>` and are distinct from
  both the fixed debug pipe and panel-bound pipes.
- `MCP_Rhino.Transport` owns only the BCL-based route protocol, path normalization, endpoint
  descriptor/attestation records, and the atomic current-user discovery registry under
  `%LOCALAPPDATA%\MCP_Rhino\Routing\v1`. It contains no Rhino or MCP business surface.
- `MCP_Rhino.Router` is a RhinoCommon-free stdio MCP gateway. Every launching agent/MCP session owns
  its own Router process; there is no shared Router daemon. A Router discovers all live descriptors,
  establishes independent backend MCP sessions, verifies the private endpoint attestation, and
  exposes the canonical Rhino tool/resource surface plus `rhino_router_list_documents`,
  `rhino_router_select_document`, and `rhino_router_get_selected_document`.
- Selection is scoped to one Router process. Multiple agents may therefore select different open
  Rhino documents concurrently without occupying one another. An explicit selected session plus a
  conflicting `filePath` is rejected; duplicate paths require selection by opaque session id.
- Route endpoints accept bounded concurrent current-user connections. They are bound to
  `RhinoDoc.FromRuntimeSerialNumber(...)`; they do not follow foreground activation. Mutation calls
  are never automatically replayed after a disconnect.
- Unsaved documents may be listed as unavailable, but become routable on first Save. Save As keeps
  the document session/pipe and atomically republishes the verified path. Closing marks the
  descriptor unavailable before one bounded aggregate endpoint drain.
- Release installation uses Rhino 8's current-user package layout
  `%APPDATA%\McNeel\Rhinoceros\packages\8.0\MCP_Rhino\<version>` and installs external executables at
  `%LOCALAPPDATA%\MCP_Rhino\bin`. Client entries must use the absolute installed Router path and must
  never point to repository `bin` output. Installer changes to client configuration require explicit
  opt-in and recorded ownership.

## 命名指南

- 避免使用 `Helper`、`Manager`、`Util` 这类宽泛命名。
- 优先使用明确职责命名：
  - `GetRhinoFileSummaryTool`
  - `RhinoGeometryService`
  - `CreateRandomSpheresRequest`
  - `RhinoInspectionAgent`
  - `LayerAuditSkill`

## 演进指南

- 主 Server 项目（`MCP_Rhino.Server`，产出 `.rhp`）保持单项目分层的 DDD 目录结构；当业务复杂度显著上升时再考虑拆 `Application.Core` / `Infrastructure` 等子项目。
- **例外：`MCP_Rhino.Bridge` 为独立 csproj**，承载 stdio-to-named-pipe 桥接能力（详见 §8 Plugin/ 子目录）。它只依赖 BCL 的 `System.IO.Pipes`，不引 RhinoCommon，不承担业务逻辑；不适合放进 `Infrastructure/` 下，因为它是 MCP Client 直接 spawn 的最小 exe，对部署可移动性有独立诉求。后续若有其他类似"对外 thin shim"项目，可建立 `src/` 下的兄弟 csproj，但禁止反向依赖主 Server 项目的业务层。
- **例外：`MCP_Rhino.Companion` 为独立 WPF / WebView2 csproj**，承载 `_Mcpchat` 默认打开的 standalone chat UI。它由 Rhino plugin 启动，绑定到单个已保存 Rhino 文档，使用 process-scoped panel pipe 和 `MCP_Rhino.Bridge` 接入 MCP；它不引 RhinoCommon，不直接读写 `.3dm`，不替代 Server 内的 Tool / Skill / Agent 层。
- **例外：`MCP_Rhino.Transport` 为独立 BCL-only csproj**，只承载 Router / Release plugin 共享的 discovery、descriptor、attestation 和 route pipe contract。它不得依赖 RhinoCommon 或 MCP SDK。
- **例外：`MCP_Rhino.Router` 为独立 stdio MCP csproj**，承载每个外部 agent session 的多文档 discovery、selection 和 backend proxy。它可以依赖 Transport 和固定版本 MCP SDK，但不得依赖 Server 或 RhinoCommon，也不得承载 Rhino business logic。
- **`Packaging/MCP_Rhino/`** owns Release staging, current-user install/repair/uninstall, owned hashes,
  rollback state, and opt-in client configuration templates. Deployment code must not contain Rhino
  document routing or MCP business logic.
- 任何新增功能都必须先判断归属，再决定目录位置。
- 如果某个 Tool 开始承担复杂流程，应考虑将流程下沉到 `Application/` 或升级为 `Skill`。
- 如果某个 Skill 开始出现目标判断与动态策略，应考虑升级为 `Agent`。

## 计划 / 执行 / 测试产物沉淀

每次能力演进对应的 Plan、EXET、TEST 三份产物的命名与内容规范，见同目录下的 `MCP_Rhino Plan Log.md`。
