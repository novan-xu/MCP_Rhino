# MCP_Rhino Architecture Rules

## 目标

本规则用于约束 MCP_Rhino 项目后续新增代码与功能的归属，避免再次退化为单文件、大杂烩结构。

## 目录归属规则

### 1. Program.cs
- 只负责应用启动、Host 构建、调用注册扩展。
- 不放业务逻辑。
- 不放 Rhino3dm / RhinoCommon 读写细节。
- MCP server 的默认启动形态为 **Rhino Plugin 加载回调**（`.rhp` 宿主）；plugin 在 Rhino 进程内启动 **Named Pipe MCP server**。外部 Client 由独立的 `MCP_Rhino.Bridge` 项目产出的 `.exe` 承担 stdio-to-pipe 桥接。`Program.cs` 保留为可执行入口，但仅服务于只读 CLI 工具与开发期 offline smoke 流程，不承担 MCP host 角色。

### 2. Server/
- `DependencyInjection.cs`：注册内部服务、仓储、基础设施实现。
- `ToolRegistration.cs`：注册 MCP tool 模块。
- `AgentRegistration.cs`：注册 agent / skill 及未来编排组件。

### 3. Tools/
- 只放直接暴露给 MCP Client 的原子能力。
- Tool 负责参数接收、调用 Application/Service、格式化输出。
- Tool 中禁止堆积大段 Rhino 文件读写逻辑。
- 命名统一使用 `*Tool` 后缀。
- **执行模式约束**：mutation 类 Tool 与 preview-of-mutation 类 Tool 只能依赖 Live 适配接口（见 §Infrastructure `Rhino/Live/`）；read / preview-of-read 类 Tool 以 Offline 适配接口（`Rhino/Offline/`）为主数据源。若需生成 stale warning，可额外只读查询 Live 状态，但不得把 Rhino 可用性作为成功返回的前置条件。详见「执行模式规则」章节。

### 4. Skills/
- 放置固定流程的复合能力。
- Skill 可以组合多个 service / use case / tool。
- Skill 不直接承担底层文件 API 细节。
- 命名统一使用 `*Skill` 后缀。
- **执行模式约束**：mutation 流程与 preview-of-mutation 流程的 Skill 只能依赖 Live 适配接口；read / preview-of-read 流程以 Offline 适配接口为主，若需 stale warning 可做 best-effort 的 Live 状态查询，但不得因此失去无 Rhino 可运行性。

### 5. Agents/
- 放置目标驱动、可做决策/调度的执行者。
- Agent 优先调用 Skill 或 Application Service。
- Agent 不直接写 Rhino3dm / RhinoCommon 细节。
- 命名统一使用 `*Agent` 后缀。
- **执行模式约束**：当 Agent 触发 mutation 或 preview-of-mutation 步骤时，必须走 Live 适配接口；read / preview-of-read 步骤以 Offline 适配接口为主，允许附加 best-effort 的 Live 状态查询用于告警，但不得阻断 read 结果。

### 6. Application/
- 放置用例、服务、流程编排、接口抽象。
- 这里回答“系统如何完成某个功能”。
- Application 依赖抽象接口，不直接耦合具体基础设施实现。
- 命名建议使用 `*Service`、`*UseCase`、`I*`。

### 7. Domain/
- 放置核心业务模型、值对象、规则、枚举。
- Domain 不依赖 MCP、Rhino3dm、文件系统实现。
- 这里回答“业务概念和规则是什么”。

### 8. Infrastructure/
- 放置 Rhino 适配（`File3dm` 默认仅离线读，`RhinoDoc` 在线读写）、文件系统、配置、日志、命令行入口适配等具体实现。
- 所有 `File3dm.Read`、例外场景下显式启用的 `File3dm.Write`，以及 `RhinoDoc` / `RhinoCommon` 等细节集中在这里。
- 这里回答"具体如何和外部技术打交道"。

- **Rhino 适配子目录按执行模式拆分**：
  - `Rhino/Offline/`：承载 `File3dm` 离线读实现，服务 read / filter / get / inspect / preview-of-read 类流程；必要时可协同查询 Live 状态生成 stale warning，但磁盘读取仍是唯一数据源。
  - `Rhino/Live/`：承载 `RhinoDoc` / RhinoCommon 在线实现，服务所有 mutation 与 preview-of-mutation 流程；所有 `RhinoDoc.ActiveDoc` / `Rhino.RhinoApp` / `BeginUndoRecord` 等调用集中在这里。
- **`Plugin/` 子目录**：放置 Rhino `.rhp` 宿主入口（`MCP_Rhino.RhinoPlugin.cs`、`McpNamedPipeServer.cs` 等）。在 `OnLoad` 里启动 **Named Pipe MCP server**（管道 `\\.\pipe\mcp_rhino`），承载 MCP 协议帧；Client 端经独立的 `MCP_Rhino.Bridge` 项目提供的 stdio-to-pipe 桥接 exe 连入。MCP server 的默认部署形态即由此承载。
- `CLI/` 放置面向开发者 / 终端的命令行适配实现（例如 `DeveloperCommandHandler`），作为外部入口到 Application / Agent 层的薄适配层；`Program.cs` 只负责解析并委托给这里。

### 9. Contracts/
- 放置 Request / Response / Agent 消息 DTO。
- Contracts 只负责数据交换结构，不承载复杂业务规则。
- 命名统一使用 `*Request`、`*Response`、`*Message`。

### 10. Prompts/
- 放置 Agent / Skill 使用的提示词模板。
- 长 prompt 不要硬编码在 C# 类中。
- 可使用 `.md`、`.txt`、`.yaml` 等文本文件组织。

## 新功能归属判断表

- 单一动作、直接暴露给 MCP → `Tools/`
- 固定工作流、组合多个能力 → `Skills/`
- 目标驱动、需要选择步骤或调度 → `Agents/`
- 功能流程编排、服务组织 → `Application/`
- 业务模型、值对象、规则 → `Domain/`
- Rhino 适配（`File3dm` 离线 / `RhinoDoc` 在线）/ IO / Logging / Config 实现 → `Infrastructure/`
- 请求/响应/消息结构 → `Contracts/`
- LLM 指令模板 → `Prompts/`

## 执行模式规则（在线 vs 离线）

Rhino 侧的能力按"是否修改文档状态"划分执行模式，互相不混用。

### 只读路径（Offline 允许）

- 覆盖动作：Read / Filter / Get / Find / Inspect，以及 preview-of-read 类能力。
- 允许实现：经 `Infrastructure/Rhino/Offline/` 直接读 `.3dm` 文件（`Rhino.FileIO.File3dm`），无需运行中的 Rhino 实例。
- 适用场景：对未打开的文件做审计、筛查、属性导出、元数据抓取。
- 约束：
  - 离线路径**不得**调用任何写入 API（`model.Objects.Add` / `Delete` / `Replace` / `SetUserString` 等）。
  - 若检测到目标文件正以 ActiveDoc 形态在 Rhino 中打开且存在未保存改动，离线读实现应在响应中写入 `OFFLINE_READ_STALE` 软警告；不阻断调用。该检测属于 best-effort 行为，可通过只读的 Live 状态查询完成；CLI 模式或无 Rhino 可用时静默跳过，不得让 read 调用失败。

### 在线路径（Live 强制）

- 覆盖动作：Create / Transform / Replace / Delete / EditControlPoints / ApplyObjectEdits / Apply/DeleteObjectUserText / Set/DeleteDocumentUserStrings、preview-of-mutation，以及未来所有改动文档状态或依赖在线文档一致性的新能力。
- 强制实现：经 `Infrastructure/Rhino/Live/` 路由到运行中的 Rhino 实例（`RhinoDoc.ActiveDoc`），通过 RhinoCommon 执行。
- 理由：
  - 离线 `File3dm.Write` 落盘即生效，绕过 Rhino Undo 栈与用户的 Save 动作，**不可撤销**；团队已确认此行为不可接受。
  - 在线 `RhinoDoc` 写入自然进入 Undo 栈，用户在 Rhino 中 `Ctrl+Z` 即可撤销；viewport 实时刷新；多 MCP 工具共享同一份文档状态，不会出现"离线改完但 Rhino 里仍是旧版"的撕裂。
- 约束：
  - 写入工具与 preview-of-mutation 工具必须由 Rhino Plugin 宿主加载；目标文档必须为 `RhinoDoc.ActiveDoc`、已 Save 到磁盘（`doc.Path` 非空）、且 `doc.Path` 与请求中的 `FilePath` 匹配，否则分别返回 `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`。
  - **主线程封送**：RhinoCommon 的 `RhinoDoc` / `Rhino.Geometry` API 限定在 Rhino UI 主线程。MCP stdio host 请求运行在后台线程，因此所有 `RhinoDoc` 读写必须经 `RhinoApp.InvokeOnUiThread` 封送到主线程同步执行。封送实现集中在 `ILiveRhinoDocumentAccessor` 内部，业务层（Service / Skill / Tool）对线程模型无感。主线程长时间阻塞（> 10s）时返回 `RHINO_MAIN_THREAD_BUSY`。
  - 每次 Apply 调用用 `doc.BeginUndoRecord(...)` / `EndUndoRecord(...)` 包裹，保证"一次写入 Tool 调用 = 一次 Undo 条目"；无实际变更或提前失败时改用 `CancelUndoRecord` 关闭，避免 Undo 栈出现空条目。
  - 修改 Geometry / Attributes 优先使用 RhinoCommon 原生 API（`doc.Objects.Replace` / `doc.Objects.Transform`），保留 attributes 保真度并合并 Undo record；不沿用 Rhino3dm 的 delete+re-add 模式。

### Preview 归属

- Preview 本身不改文档，但在"Preview 某个 mutation"的语义下，Preview 看到的文档必须与随后 Apply 作用的文档一致。
- 因此 Preview-of-mutation 也走 Live 路径：只读 `RhinoDoc`、不写、不开 Undo record；不允许走 Offline `File3dm` 回退，避免 Preview 与 Apply 对齐到不同的文档状态。
- Preview-of-read（例如 `PreviewObjectUserTextWrites` 这类"在 Apply 前看一眼会写成什么"的工具）维持离线读语义。

### 去归档化

- 因 Rhino Undo 已覆盖"误操作可回退"的核心诉求，`IFileMutationSafeguard` / Archive snapshot / preflight warning 机制不再保留，相关 Tool、Skill、Service、接口一并下线。
- Save 时机回归 Rhino 既有流程（用户手动 Save 或 Rhino 自带 AutoSave）。

### 例外处理

- 若后续出现"必须在无 Rhino 环境中做 mutation"的合理诉求（例如批处理服务器），需以显式开关（如 `--force-offline`）形式重新引入，并在本章节补充约束；该路径必须与默认 live mutation 实现严格隔离，默认禁用。

## 命名规则

- 避免使用 `Helper`、`Manager`、`Util` 这类宽泛命名。
- 优先使用明确职责命名：
  - `GetRhinoFileSummaryTool`
  - `RhinoGeometryService`
  - `CreateRandomSpheresRequest`
  - `RhinoInspectionAgent`
  - `LayerAuditSkill`

## 演进规则

- 主 Server 项目（`MCP_Rhino.Server`，产出 `.rhp`）保持单项目分层的 DDD 目录结构；当业务复杂度显著上升时再考虑拆 `Application.Core` / `Infrastructure` 等子项目。
- **例外：`MCP_Rhino.Bridge` 为独立 csproj**，承载 stdio-to-named-pipe 桥接能力（详见 §8 Plugin/ 子目录）。它只依赖 BCL 的 `System.IO.Pipes`，不引 RhinoCommon / Rhino3dm，不承担业务逻辑；不适合放进 `Infrastructure/` 下，因为它是 MCP Client 直接 spawn 的最小 exe，对部署可移动性有独立诉求。后续若有其他类似"对外 thin shim"项目，可建立 `src/` 下的兄弟 csproj，但禁止反向依赖主 Server 项目的业务层。
- 任何新增功能都必须先判断归属，再决定目录位置。
- 如果某个 Tool 开始承担复杂流程，应考虑将流程下沉到 `Application/` 或升级为 `Skill`。
- 如果某个 Skill 开始出现目标判断与动态策略，应考虑升级为 `Agent`。

## 计划 / 执行 / 测试产物沉淀

每次能力演进对应的 Plan、EXET、TEST 三份产物的命名与内容规范，见同目录下的 `MCP_Rhino Plan Log.md`。
