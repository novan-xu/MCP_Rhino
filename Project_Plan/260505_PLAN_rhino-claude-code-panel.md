# 260505_PLAN_rhino-claude-code-panel

## 背景

到目前为止，MCP_Rhino 的原有控制链路是：用户在 Claude Desktop / Claude Code / Cursor 等外部
MCP Client 里发对话 → `MCP_Rhino.Bridge.exe` 经 `\\.\pipe\mcp_rhino` 进入 Rhino 内的 MCP
server → 工具落地到 `RhinoDoc.ActiveDoc`。整条链路在 `260422_PLAN_mcp-client-integration`
里已经闭环。

但这条链路有两条 UX 痛点：

1. **LLM 客户端在 Rhino 之外**。用户必须切到另一个窗口（IDE 或桌面 App）才能发指令；Rhino
   只承担"被驱动"的角色。对建模过程中"边看边想边说"的工作流不友好。
2. **会话不绑定文档**。`\\.\pipe\mcp_rhino` 是进程级单实例（`maxNumberOfServerInstances=1`），
   且工具默认跟随 `RhinoDoc.ActiveDoc`。多文档并行工作时，给 File A 发的指令很容易因为
   `ActiveDoc` 切到 File B 而打到错文档（被 `FILE_NOT_ACTIVE` 兜住但体验割裂）。

但这条原有链路仍然很有价值：它是开发者最快的 live 调试入口。后续明确把它保留为
**Developer Debug Control Path**：`MCP_Rhino.Bridge.exe` 不传 `--pipe` 时继续连接
`\\.\pipe\mcp_rhino`，server 继续解析 `RhinoDoc.ActiveDoc`。它用于功能开发、能力 smoke、
回归和外部 MCP client 调试；不作为最终多文档用户体验的安全绑定路径。

参照 Cline / Claudian 这类"chat panel 嵌入宿主应用"的形态，本次能力把 LLM 对话面板搬进
Rhino 自己——在 Rhino 内开 docking panel，每打开一个 .3dm 就生成一个**独占绑定到该文档**
的 chat panel + MCP server 实例。Grasshopper 的"definition 跟随它被打开时的文档"是直接的
心智模型类比。

LLM 本身的认证、模型选择、provider 路由全部委托给已经成熟的 **Claude Code CLI**
（v2.1.119+，本机 `C:\Users\Novan\.local\bin\claude`），不在我们的插件里重写一遍。这条
决策有三个理由：

- 用户已有 Claude Max 订阅 + claude.ai OAuth；CC 进程会自动继承，无需我们处理 API key。
- CC 已经支持多 provider（Anthropic / Bedrock / Vertex / 经 `ANTHROPIC_BASE_URL` 接 OpenAI
  兼容代理），后续 provider 演进自动惠及面板。
- 我们的插件不需要承担"对话 / 工具调度 / 上下文管理 / 成本统计"的重复实现，把核心精力
  放在"per-doc 绑定 + UI 可视化"这两个真正属于本仓库的部分。

启动这条 PLAN 前已经完成 30 分钟 CC 嵌入 spike，确认下列契约可用（见 §关键设计 §3-§5）：

- `claude --print --output-format=stream-json --verbose --input-format=stream-json` 是
  embedding-mode 的官方入口，输出是 line-delimited JSON 事件流。
- `--mcp-config <path> --strict-mcp-config` 提供 per-instance MCP 配置隔离；不会污染
  `~/.claude.json` / 项目根 `.mcp.json` / 用户级别 user scope。
- `init` 事件实时携带 `mcp_servers: [{name, status: "connected"|"failed"}]`，面板可立即
  渲染连接状态。
- 用户身份继承自 CC 已有的 OAuth 状态，插件零凭据管理。
- `--permission-mode bypassPermissions` + `--disallowedTools "..."` 让 MCP 工具调用免提示
  通过，CC 的内置 Bash / Edit / Read / Write 在面板里完全禁用。

Spike 也暴露了一个限制：交互式 slash 命令（`/model`、`/login`）在 `--print` 模式不可用。
影响：

- `/login` 不影响——用户在 Rhino 之外跑一次 `claude auth login` 即可，符合"force the user
  to use CC for authentication"的初衷。
- `/model` 影响模型切换 UX；workaround 是面板加一个**最小**的模型下拉框，把选中的模型
  以 `--model <id>` 形式传给下一次 CC spawn。模型解析仍然是 CC 的职责，面板只是在 spawn
  阶段帮用户选一次。

补充 spike（2026-05-05）：用 metadata-only 方式读取 Rhino 8 的
`C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll`，确认 `Rhino.UI.Panels` 暴露：

- `PanelType.PerDoc`
- `IPanel.PanelShown(uint documentSerialNumber, ...)` /
  `PanelHidden(...)` / `PanelClosing(...)`
- `Panels.GetPanel<T>(uint documentSerialNumber)` /
  `GetPanels<T>(RhinoDoc doc)` / `ClosePanel(Type panelType, RhinoDoc doc)`

这说明 RhinoCommon API 形状支持 per-document panel 实例；但 metadata spike 不能替代真实
Rhino UI 运行验证。因此本 plan 的 EXET 第一门槛仍然是 Rhino 内空白 `PanelType.PerDoc`
panel spike：双文档打开时必须能拿到两个按 `RuntimeSerialNumber` 区分的 panel 实例，否则
先把方案改为"单 system panel + per-doc tabs/session list"，再继续接 CC 与 MCP。

**前置依赖**：本 plan 假定项目已经收敛为 active-doc-only。具体地，依赖
`260505_PLAN_active-doc-only-architecture` 落地后的两条不变量：

1. `WithToolsFromAssembly` 反射注册的工具集合中**不再含任何 offline 读写工具**。这意味着
   bound MCP host 可以直接复用全量 tool surface；不需要在 host 层加 tool-name 白名单 / 黑名单
   过滤层来阻止 LLM 通过 offline 工具读其他 `.3dm` 文件——因为根本没有这种工具。
2. 架构指南 §执行模式指南 已经收敛为单一 Live 模式；本 plan §10 在该指南之后追加 panel-bound
   子节，与重写后的 Live 路径并列描述，不需要回头处理 offline 段落。

如果 active-doc-only 推迟落地，本 plan 的 §1（per-doc lifecycle）必须额外包含一个 tool 白名单
过滤层的设计，并在每次新增工具时维护该白名单。两个 plan 的执行顺序不可颠倒。

## 目标

1. **新增"per-doc Claude Code chat panel"能力**：在 Rhino 内每打开一个 .3dm 自动注册一个
   chat panel；用户在面板里输入自然语言指令，由 panel 内嵌的 Claude Code 子进程驱动 MCP
   工具操作**该 .3dm 对应的** `RhinoDoc`，结果实时刷新到 Rhino 视口。
2. **每个文档独占一个 MCP server 实例**：在现有 `\\.\pipe\mcp_rhino` 全局管道之外，新增
   `\\.\pipe\mcp_rhino_<docRuntimeSerial>` 命名规则的 per-doc 管道服务器。每个 per-doc
   server 在 DI 层强绑定到该 `RhinoDoc.RuntimeSerialNumber`；bound mode 下调用方传入的
   `FilePath` 被忽略并重写为绑定文档当前 `Path`，避免 `_SaveAs` 或 LLM 猜错路径导致误拒。
3. **明确保留 Developer Debug Control Path**：`\\.\pipe\mcp_rhino` 全局管道继续存在，
   沿用 `ActiveDoc`-following 语义；`MCP_Rhino.Bridge.exe` 不传 `--pipe` 时继续连接该管道。
   Claude Desktop / Cursor / Cline 等现有外部客户端配置无需改动，但本 plan 把这条路径标注为
   developer/debug 用途，用于功能测试、能力 smoke 和回归验证。
4. **零凭据管理 / 零模型 SDK**：插件不引 Anthropic / OpenAI / Microsoft.Extensions.AI 等
   provider SDK，不存储任何 API key。一切走 Claude Code 子进程。
5. **架构指南同步更新**：在 `Project_Guides/MCP_Rhino Architecture.md` §执行模式指南中
   新增"Panel-bound execution mode"子节，把 per-doc 绑定的语义、错误码、生命周期写进
   架构文档。

**非目标（v1 不做）**：

- 对话历史持久化（关闭面板即丢失；后续可加 sidecar `<file>.mcp_chat.json`）
- `--continue` / `--resume` 跨会话恢复
- 同一 .3dm 内多 panel / 多 CC 实例（v1 严格 1 doc ↔ 1 panel ↔ 1 CC）
- 自定义 system prompt UI / skill 管理 UI / 插件 UI（用户可在 CC 自身的 settings 里改）
- 富 markdown 渲染（v1 用最简洁的 text + tool-card 渲染）
- Mac Rhino 适配（v1 仅 Windows；Eto.Forms 选型已留 Mac 余地，但不在 v1 验收范围）
- 离线 / CLI fallback 模式下的 panel（panel 是 live-only 能力）

## 架构归属

新增能力跨多个层，按 `Project_Guides/MCP_Rhino Architecture.md` 归属如下：

- **`Infrastructure/Plugin/Panel/`**（新增子目录）—— Rhino 宿主侧的 docking panel UI
  与生命周期：
  - Eto.Forms 控件（chat 视图、输入框、模型下拉框）
  - `Rhino.UI.Panels.RegisterPanel(..., PanelType.PerDoc)` 注册 glue
  - `Rhino.UI.IPanel` 回调（`PanelShown` / `PanelHidden` / `PanelClosing`）中的
    `documentSerialNumber` 绑定检查
  - 文档级生命周期 dispatcher（hook `RhinoDoc.NewDocument` / `EndOpenDocument` / `CloseDocument`）
  - `PanelChatSessionService`：接收 panel 用户输入 → 写入 CC stdin；读取 CC stdout
    stream-json → 翻译为 panel 可消费的 strongly-typed 事件。它读写 UI / 子进程，不进入
    Application 层。
- **`Infrastructure/Plugin/`** —— 现有目录扩展，复用既有 `McpNamedPipeServer`：
  - **不新增** `BoundMcpNamedPipeServer` 类。`McpNamedPipeServer` 现行签名
    `McpNamedPipeServer(string pipeName, Func<Stream, Stream, IHost> hostFactory)` 已经
    支持任意 pipe 名 + 自定义 host factory。Per-doc 绑定通过新增一个 `BoundHostFactory`
    （静态方法或 lambda 工厂）实现：传入 `RuntimeSerialNumber`，返回一个 `Func<Stream, Stream, IHost>`
    它在 DI 注册阶段把 `ILiveRhinoDocumentAccessor` 替换为 `BoundLiveRhinoDocumentAccessor(boundSerial)`，
    其余服务沿用现有注册。
  - 落点：`Infrastructure/Plugin/BoundHostFactory.cs`（小文件，~50 LOC）。
- **`Infrastructure/ClaudeCode/`**（新增子目录）—— Claude Code CLI 子进程封装：
  - 进程生命周期、stream-json 解析、stream-json 写入、版本探测、临时 mcp-config 生成。
- **`Infrastructure/Rhino/Live/`** —— 新增绑定模式的 document accessor：
  - `BoundLiveRhinoDocumentAccessor.cs`：实现 `ILiveRhinoDocumentAccessor`，但 `ActiveDoc`
    检查替换为"`RhinoDoc.FromRuntimeSerialNumber(boundSerial)` 必须存在 + 忽略入参
    `filePath` 并使用 bound doc 当前 `Path`"。
- **`Application/Interfaces/`** —— 抽象：
  - `ILiveRhinoDocumentAccessorFactory.cs`：根据 `RuntimeSerialNumber` 产出 bound accessor。
- **`Infrastructure/ClaudeCode/` / `Infrastructure/Plugin/Panel/` 局部接口**：
  - `IClaudeCodeProcess.cs`：CC 子进程抽象，便于测试；放在 ClaudeCode 基础设施目录。
  - `IPanelChatSession.cs`：panel UI 与 orchestrator 之间的契约；放在 Panel 基础设施目录。
- **`Domain/Models/`** —— chat 领域对象：
  - `ChatMessage.cs`、`ChatToolCall.cs`、`ChatThinkingBlock.cs`、`ChatSessionInitInfo.cs`
- **`Domain/Enums/`**：
  - `ChatRole.cs`（User / Assistant / System）
  - `ChatToolCallStatus.cs`（Pending / Running / Success / Failed）
  - `ChatTerminalReason.cs`（Completed / Cancelled / Errored / RateLimited / AuthFailed）
- **`Server/DependencyInjection.cs`** —— 注册新接口实现 + factory；保留现有注册不变。
- **`Tools/` / `Skills/` / `Agents/`** —— **不动**。本期不增删任何 MCP 工具，只是给现有
  工具集新增一种"被绑定的" host 形态。
- **`MCP_Rhino.Bridge`**（独立 csproj）—— 新增 `--pipe <name>` 命令行参数；默认值保留
  `mcp_rhino`，作为 Developer Debug Control Path 的入口；显式传 `--pipe mcp_rhino_<serial>`
  时用于 panel-bound per-doc server。
- **`Project_Guides/MCP_Rhino Architecture.md`** —— 更新 §通用契约 中 panel-bound
  路径的 `FilePath` 语义，并在 §执行模式指南新增 "Developer Debug Control Path" 与
  "Panel-bound execution mode" 子节，把 debug path 与 per-doc 绑定的契约纳入架构指南。

**Live Smoke CLI 入口**（按 `Project_Guides/MCP_Rhino Plan Log.md` §Live Smoke CLI 入口约定
落地）：

- slug：`rhino-claude-code-panel-smoke-test`
- 注册文件（partial）：`Project_Test/260505_TEST_rhino-claude-code-panel/DeveloperCommandHandler.RhinoClaudeCodePanelSmokeTest.cs`
- Rhino live smoke 命令：`McpRhinoClaudeCodePanelSmokeCommand` → `_McpRhinoClaudeCodePanelSmoke`

## 关键设计

### 1. Per-doc panel & MCP server 生命周期

`McpRhinoPlugin.OnLoad` 现行只启动一条全局 named-pipe server。本期在它之后：

1. 在默认 ALC 注册 `RhinoChatPanel` 为 `PanelType.PerDoc`。
2. 追加 `PerDocumentPanelDispatcher`。
3. dispatcher 启动时先扫描 Rhino 当前已经打开且 `doc.Path` 非空的文档并 spin up，避免
   插件加载晚于文档打开时漏建 panel。
4. dispatcher 再订阅 `RhinoDoc.NewDocument` / `EndOpenDocument` / `CloseDocument` /
   `EndSaveDocument`。

事件行为：

- `RhinoDoc.NewDocument`（`File > New` 或 `_New`）→ 文档已创建但未保存（`doc.Path == null`）。
  此时**不**立即起 panel：未保存文档无法满足现有"`doc.Path` 非空"硬约束（见
  `Project_Guides/MCP_Rhino Architecture.md` §执行模式指南）。Panel 等到 `EndSaveDocument`
  把它转为有 path 的状态后再 spin up。
- `RhinoDoc.EndOpenDocument`（`File > Open` 或 `_Open`）→ 文档已加载且有 path。立刻 spin
  up：
  1. 创建 / 打开 per-doc Eto.Forms panel。实现必须使用 `PanelType.PerDoc` 注册，并通过
     `Panels.GetPanel<T>(doc)` / `GetPanels<T>(doc)` 验证拿到的是该 `RuntimeSerialNumber`
     对应的实例。`OpenPanel(Type)` 在 Rhino 内是否按当前 doc materialize per-doc 实例，
     由 EXET 第一门槛的空白 panel spike 验证；若失败，立即切换为"单 system panel +
     per-doc tabs/session list"设计。
  2. 启动一个新的 `McpNamedPipeServer` 实例（pipe 名 `mcp_rhino_<doc.RuntimeSerialNumber>`，
     hostFactory 由 `BoundHostFactory.For(serial)` 提供）；构造与启动通过反射调用 isolated
     ALC 内的 `ServerBootstrap.StartBoundPipeServer(pipeName, runtimeSerial)`。
  3. 生成临时 mcp-config JSON 文件到 `%TEMP%\MCP_Rhino\<doc.RuntimeSerialNumber>\.mcp.json`，
     内容指向 `MCP_Rhino.Bridge.exe --pipe mcp_rhino_<serial>`。
  4. Spawn Claude Code 子进程，参数：
     ```
     claude --print
            --output-format=stream-json
            --input-format=stream-json
            --verbose
            --mcp-config <%TEMP%\MCP_Rhino\<serial>\.mcp.json>
            --strict-mcp-config
            --permission-mode bypassPermissions
            --disallowedTools "Bash,Edit,Read,Write,Grep,Glob,WebFetch,WebSearch,TodoWrite,Task,NotebookEdit"
            [--model <selected-model>]   # 仅在用户从下拉框选了非默认模型时
     ```
     `cwd` 设为 `Path.GetDirectoryName(doc.Path)`，让 CC 的 auto-memory / project state 自动
     按 .3dm 所在目录隔离。
- `RhinoDoc.CloseDocument` → 拆除：
  1. 关闭 panel（`Rhino.UI.Panels.ClosePanel`）+ 释放 Eto 控件。
  2. 向 CC stdin 写一条 EOT 或直接 `Process.Kill()`（spike 中确认 CC 对 SIGTERM 等价的
     Windows kill 响应清晰，会 emit 一条最终 `result` 事件再退出；不会留僵尸子进程）。
  3. 通过反射调 `ServerBootstrap.StopBoundPipeServer(pipeName)`，停止该 doc 对应的
     `McpNamedPipeServer` accept loop；释放 pipe 句柄。
  4. 删除 `%TEMP%\MCP_Rhino\<serial>\` 临时目录。

**`RhinoDoc.RuntimeSerialNumber` 作为绑定主键**而非 `doc.Path`：path 在 `_SaveAs` 时会变，
但 `RuntimeSerialNumber` 在该文档对象的整个生命周期内稳定。`_SaveAs` 后 dispatcher 监听
`EndSaveDocument` 更新 panel header / 当前路径状态（不重启 panel / CC，pipe 名也不变）。
临时 mcp-config 只引用 Bridge 路径与 pipe 名，不受 doc path 影响。

**Developer Debug Control Path（全局 `\\.\pipe\mcp_rhino`）**：

- 现有全局 `McpNamedPipeServer` 在 plugin 加载时仍然按原样启动，沿用
  `ActiveDoc`-following 语义。
- `MCP_Rhino.Bridge.exe` 不传 `--pipe` 时继续连接 `mcp_rhino`，即进入 debug path。
- 这条路径明确用于开发者能力验证：新 tool / skill / agent 进入 server tool surface 后，可以
  先通过现有外部 MCP client 或 smoke 直接验证 live Rhino 行为，不需要先改 panel UI / CC
  编排。
- 调用方必须知道当前 `RhinoDoc.ActiveDoc` 是目标文档，并继续传正确 `FilePath`；否则按全局
  live 语义返回 `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`。
- `260422_PLAN_mcp-client-integration` 的 README / samples / Bridge 默认行为保持兼容，但文档措辞
  应标注为 developer/debug 控制路径，不再描述为面向最终多文档用户体验的主路径。

### 2. Bound document accessor + filePath 重写

现有 `LiveRhinoDocumentAccessor` 在每次 `Execute(...)` / `ExecuteWithUndo(...)` 入口检查：

- `RhinoDoc.ActiveDoc` 是否非空 → 否则 `NO_ACTIVE_DOCUMENT`
- `ActiveDoc.Path` 是否非空 → 否则 `ACTIVE_DOC_UNSAVED`
- `ActiveDoc.Path == request.FilePath` → 否则 `FILE_NOT_ACTIVE`

新增 `BoundLiveRhinoDocumentAccessor`（实现同一接口 `ILiveRhinoDocumentAccessor`），
入口检查改为：

- `RhinoDoc.FromRuntimeSerialNumber(_boundSerial)` 是否非空 → 否则新增 `DOCUMENT_CLOSED`
- `boundDoc.Path` 是否非空 → 否则沿用 `ACTIVE_DOC_UNSAVED`（但语义是"被绑定的 doc 未保存"）
- `filePath` 入参不参与匹配；accessor 每次从 `boundDoc.Path` 读取当前真值作为 effective
  path。bound mode 正常不返回 `FILE_NOT_ACTIVE`。

**FilePath 强制重写（解决 SaveAs / LLM 错猜路径问题）**：

panel 里 LLM 看不到当前文档绝对路径（CC 子进程的 cwd 在 spawn 时就定死了；`_SaveAs` 改路径
后 CC 不感知；LLM 也可能凭直觉拼一个错的 `filePath`）。如果 bound accessor 严格按"路径
匹配"判 `FILE_NOT_ACTIVE`，每次 LLM 拼错 path 都会失败，UX 灾难。

为此，bound MCP host 不依赖 ModelContextProtocol SDK 的 middleware 能力，而是在
`BoundLiveRhinoDocumentAccessor` 内实现 **filePath 重写**：

- 中间件位置：`ILiveRhinoDocumentAccessor.Execute(...)` /
  `ExecuteWithUndo(...)` 的 bound 实现内部。所有 live service / tool 都已经通过该 accessor
  进入 RhinoDoc，因此该位置比 MCP SDK interceptor 更稳定。
- 实现方式：bound accessor 忽略调用方传入的 `filePath` 参数，统一替换为绑定 doc 的当前
  `Path`。
- 重写逻辑：每次进入 accessor，从 `RhinoDoc.FromRuntimeSerialNumber(_boundSerial)` 读最新
  `Path`，作为后续 RhinoCommon 调用的真值；调用方传的 `filePath` 字符串被丢弃。
- LLM 视角：tool schema 里仍有 `filePath` 字段（避免改 Tool 签名），但传任意字符串都行，
  bound mode 下都被改写为绑定 doc 的当前 path。**不会**返回 `FILE_NOT_ACTIVE`（除非
  `RhinoDoc.FromRuntimeSerialNumber` 找不到 doc，那是 `DOCUMENT_CLOSED`）。
- Developer Debug Control Path 不动：外部 client（Claude Desktop / Cursor）经全局 pipe 调用时
  仍然必须传正确 path 才能匹配 `ActiveDoc.Path`，沿用现有严格语义。

副作用 / 一致性：

- `_SaveAs` 改路径后**自动生效**：下一次 tool 调用时 `boundDoc.Path` 已经是新值，accessor
  无缝切换。CC 子进程不需要 restart。
- 临时 mcp-config 的内容（指向 Bridge.exe + pipe 名）不受 path 影响；不需要在 SaveAs 时
  重写。
- LLM 在 prompt 里看到的"当前文档路径"信息（如果 system prompt 注入）始终通过 panel 实时
  注入；中间件兜底保证即便 LLM 引用过期 path 也不会出错。

错误码沿用最大化，避免 Tool 层调整：

| 错误码 | 既有语义（全局 pipe） | 新增语义（per-doc panel） |
| --- | --- | --- |
| `NO_ACTIVE_DOCUMENT` | `ActiveDoc == null` | 不出现（per-doc 走 DOCUMENT_CLOSED） |
| `ACTIVE_DOC_UNSAVED` | `ActiveDoc.Path == null` | bound doc 未保存（v1 避免方式：仅在
  `EndSaveDocument` 后 spin up panel） |
| `FILE_NOT_ACTIVE` | request path ≠ ActiveDoc.Path | 正常不出现；bound mode 忽略 request path |
| `DOCUMENT_CLOSED`（新） | 不出现 | bound doc 已被关闭但 tool call 还在飞行（race） |
| `RHINO_MAIN_THREAD_BUSY` | 既有 | 既有，不变 |

注入方式：`BoundHostFactory.For(serial)` 返回一个 `Func<Stream, Stream, IHost>`，构造时
把 `ILiveRhinoDocumentAccessor` 注册替换为：

```csharp
services.AddSingleton<ILiveRhinoDocumentAccessor>(sp =>
    new BoundLiveRhinoDocumentAccessor(boundRuntimeSerialNumber));
```

其余服务（`LiveRhinoGeometryBuilder` 等）保持现有注册不变；它们通过构造函数依赖
`ILiveRhinoDocumentAccessor`，自动获得绑定版本。

工厂层抽象：新建 `ILiveRhinoDocumentAccessorFactory`，方法 `For(uint runtimeSerial)`
返回绑定 accessor；`BoundHostFactory` 内部通过它构造 isolated ALC 内的 DI 容器。全局 pipe
继续直接 resolve 默认 `LiveRhinoDocumentAccessor`，不经工厂。

`TryGetActiveDocumentState(...)` 在 bound accessor 中同样忽略入参路径，返回绑定 doc 的
`Modified` 状态；若 doc 已关闭或未保存，返回 `false`。

### 3. Claude Code 子进程封装

新增 `Infrastructure/ClaudeCode/`：

- **`ClaudeCodeAvailability.cs`**：探测 `claude` 是否在 PATH，运行 `claude --version` 解析
  版本号；要求 ≥ 2.1.119（embedded stream-json 在该版本验证通过）。结果缓存，进程内只
  探测一次。检测失败 → panel 立刻显示"Install Claude Code: https://docs.claude.com/en/docs/claude-code/setup"
  并禁用输入框。

- **`ClaudeCodeProcess.cs`**：`Process.Start` 包装：
  - `RedirectStandardInput=true` / `RedirectStandardOutput=true` / `RedirectStandardError=true` /
    `UseShellExecute=false` / `CreateNoWindow=true`。
  - stdout 由 `StreamJsonReader` 行解析；stderr 一律 raw-log 到 `RhinoApp.WriteLine` 前缀
    `[mcp_rhino panel <serial>] CC stderr: `（出错排查用）。
  - `Kill()` / `Dispose()`：先尝试 stdin EOT + 200ms grace，超时则 `Process.Kill(entireProcessTree:true)`。
  - 进程意外退出（非 panel 触发）→ raise `Exited` 事件，panel 显示"Claude Code exited
    unexpectedly. Click here to restart."

- **`StreamJsonReader.cs`**：异步读取 stdout 行，反序列化为
  `ClaudeCodeStreamEvent` 联合类型（`InitEvent` / `RateLimitEvent` / `AssistantEvent` /
  `ResultEvent` / `UnknownEvent`）。每个 event 强类型暴露关键字段（session_id, model,
  mcp_servers, content blocks, total_cost_usd, terminal_reason）。

- **`StreamJsonWriter.cs`**：把用户输入序列化为 stream-json `user` 消息写入 stdin。格式
  与 Claude Agent SDK 一致：
  ```json
  {"type":"user","message":{"role":"user","content":[{"type":"text","text":"..."}]}}
  ```
  追加 `\n` flush。Spike 中未做完整双向多轮联调；EXET 阶段会基于 Claude Agent SDK 的
  Python / TypeScript 文档 mirror 出 C# 写入实现，并以一次"开 panel → 发两轮消息 → 收两
  轮 result"作为最小验证。

- **`McpConfigBuilder.cs`**：生成临时 `.mcp.json`：
  ```json
  {
    "mcpServers": {
      "rhino": {
        "command": "<absolute-path-to>/MCP_Rhino.Bridge.exe",
        "args": ["--pipe", "mcp_rhino_<serial>"]
      }
    }
  }
  ```
  路径用 forward-slash 形式（spike 已确认 backslash 在 JSON 字符串里需要双重转义；
  forward-slash 在 Windows `Process.Start` 中也合法，避免转义陷阱）。

### 4. Stream-json 事件 → panel UI 映射

CC stdout 输出的事件序列（spike 中已抓取实样）：

| CC event type | Panel 行为 |
| --- | --- |
| `system/init` | 渲染 panel header：model 名、mcp_servers 状态徽章（连接图标 connected/failed）、session_id、cwd。`mcp_servers[0].status == "failed"` → header 红色提示"Rhino MCP not connected"，禁用输入。 |
| `rate_limit_event` | 静默；只在 `status != "allowed"` 时弹一条 toast。 |
| `assistant` content `thinking` | 渲染为可折叠"💭 Thinking" 块（默认折叠）。 |
| `assistant` content `text` | 流式追加到 chat bubble；`stop_reason == null` 表示尚未结束本条消息。 |
| `assistant` content `tool_use` | 创建 ToolCallCard：tool name、入参摘要、status=Running。 |
| `assistant` content `tool_result` | 找到匹配 tool_use_id 的 ToolCallCard，更新 status=Success/Failed + 结果摘要。 |
| `result` | 关闭"Sending..."状态；显示本轮成本（`total_cost_usd`）、turn 数、`terminal_reason`；解锁输入框等待下一轮。 |
| stderr 行 | 不进 chat 流；只在 panel 底部"Diagnostics" 抽屉里列出。 |

**Panel UI 三件套（Eto.Forms）**：

- **Transcript view**：垂直滚动列表，按 message / tool-call / thinking 分块渲染。message
  用 `RichTextArea` 简单纯文本；tool-call 用自定义 `Drawable`（标题、状态图标、参数摘要、
  耗时、可展开看详细 JSON）。
- **Input box**：`TextArea` 多行输入；`Ctrl+Enter` 发送；`Send` / `Stop` 按钮。Stop 直接
  写一个 cancel 信号（v1 简化：`Process.Kill` + 重启 CC 子进程，下一轮新会话）。
- **Header bar**：model 下拉框（见 §6）、mcp 状态徽章、session_id 缩写、`Open CC settings`
  链接（外部启动 `claude` 交互终端到一个临时目录，便于用户跑 `/login` / `/model` 等）。

### 5. MCP config 注入

每次 spawn CC 都生成一份独立 `.mcp.json`：

- 路径：`%TEMP%\MCP_Rhino\<doc.RuntimeSerialNumber>\.mcp.json`，避免多 doc 共用同一份文件。
- 内容指向**已知存在**的 Bridge.exe 路径（plugin 启动时探测 `MCP_Rhino.Bridge.exe`：先看
  与 plugin assembly 同目录，然后看 `%LOCALAPPDATA%\McNeel\Rhinoceros\8.0\Plug-ins\MCP_Rhino\Bridge\`，
  最后看 PATH）。任一找到即用；都没找到 → panel 显示"Bridge not found, panel disabled"。
- `--strict-mcp-config` 保证 CC **不**读取用户级 `~/.claude.json` 或项目级 `.mcp.json`，
  避免与既有外部 client 配置混淆。

### 6. 模型选择（dropdown）

由于 spike 确认 `/model` 在 stream-json 模式不可用，本期方案：

- Panel header 一个 `DropDown`：
  - `Default (CC settings)` — 不传 `--model` 参数，CC 用 `~/.claude/settings.json` 的默认值。
  - `claude-haiku-4-5` / `claude-sonnet-4-6` / `claude-opus-4-7` — 直接传 `--model <id>`。
  - `Custom...` — 弹一个 input box 让用户输入任意 model id（包括 Bedrock / Vertex / OpenAI 兼容代理需要的字符串）。
- 选择持久化到 panel 实例本地（每个 doc 一份），关闭 doc 即丢失（v1 不进 settings）。
- **切换模型意味着新会话**：panel 弹一个确认"Switching model will start a new conversation.
  Continue?"；用户确认后 kill 当前 CC，按新 `--model` re-spawn。

更复杂的 provider 配置（Bedrock region / Vertex project id / `ANTHROPIC_BASE_URL`
环境变量）由用户在系统级设置或 `~/.claude/settings.json` 里配；面板不接管。

### 7. Bridge `--pipe <name>` 参数

`MCP_Rhino.Bridge/Program.cs` 现行硬编码 `mcp_rhino` 管道名（推断自
`260422_PLAN_mcp-client-integration` §背景）。本期最小修改：

- 新增 `--pipe <name>` 命令行参数；默认值 `mcp_rhino`。
- 解析后用作 `NamedPipeClientStream` 的目标管道。
- 不接受其他参数；其他 flag 一律 reject + 打印 usage 到 stderr。
- Bridge 不感知 panel；它只是按管道名连过去。

语义分层：

- 不传 `--pipe` → 连接 `mcp_rhino`，进入 **Developer Debug Control Path**，目标文档为
  `RhinoDoc.ActiveDoc`。
- 传 `--pipe mcp_rhino_<serial>` → 连接 per-doc panel-bound server，目标文档为绑定的
  `RuntimeSerialNumber`。

外部客户端配置（Claude Desktop / Cursor / `.mcp.json` samples）**不需改**，因为不传
`--pipe` 时 Bridge 行为与今天完全一致；但文档中应把该默认路径命名为 debug/dev control path。

### 8. 权限 / 工具表面隔离

CC 子进程的工具集严格收敛：

- 通过 `--disallowedTools "Bash,Edit,Read,Write,Grep,Glob,WebFetch,WebSearch,TodoWrite,Task,NotebookEdit"`
  禁掉 CC 的所有内置工具。
- 唯一可用的工具来自 mcp-config 注入的 `rhino` server（即我们的 MCP）。
- `--permission-mode bypassPermissions` 让 MCP 工具调用免提示；权限策略由 Rhino 侧的工具
  实现（黑名单 / 写入路径强制 Live / Undo 包裹等）保证。
- 用户输入的内容仍然受 CC 的 prompt-injection 防护（CC 自带）。

后果：面板里的 Claude **只能**用我们暴露的 Rhino MCP 工具。它不会在面板里去读用户磁盘
文件、跑 shell、上网搜东西。这与"chat panel 是 Rhino 的助理，不是 IDE 助手"的产品定位
吻合。

### 9. 成本与 UX 注意

- 每个 fresh CC session 启动时会 cache-create system prompt + memory + skills，约 20k
  tokens（spike 实测 0.027 USD on Haiku）。Per-doc panel 每次开新文档都付一次。Max
  订阅用户感知不到；按量计费用户应当知情。
- 长会话内存占用：CC 子进程不主动 compact context；超长会话需要用户在 panel header 点
  `Clear conversation` 重启子进程。v1 提供 button，无自动 compact。
- Rhino 主线程依然是"被工具串行写入"的瓶颈：两个 panel 同时发指令 → 两边的 MCP 调用都
  走 `RhinoApp.InvokeOnUiThread`，串行执行。Panel 在等待时显示 spinner 但实际是排队。
  用户感知"两份对话各跑各的，但视口刷新有先后"——与 Grasshopper 多 .gh 同时运行时的
  体验一致。

### 10. 架构指南（`MCP_Rhino Architecture.md`）变更

`260505_PLAN_active-doc-only-architecture` 落地后，§执行模式指南 已经被改写为"Live Only"
单一模式（包含 §通用契约 / §读 / §写 / §CLI 进程入口 / §去归档化 五个子节）。本期先更新
§通用契约 中已有的 panel-bound 路径 bullet，避免它继续要求 request `FilePath` 与绑定文档
路径匹配；随后在该重写后的指南末尾追加两个子节：Developer Debug Control Path 与
Panel-bound execution mode。

§通用契约 中的 panel-bound bullet 改为：

```markdown
- Panel-bound 路径解析为 `RhinoDoc.FromRuntimeSerialNumber(boundSerial)`。目标文档必须存在、
  已 Save 到磁盘；请求 `FilePath` 在 bound mode 下被忽略并重写为绑定文档当前 `Path`。
  文档不存在 / 未保存分别返回 `DOCUMENT_CLOSED` / `ACTIVE_DOC_UNSAVED`；正常不返回
  `FILE_NOT_ACTIVE`。
```

随后在指南末尾追加：

```markdown
### Developer Debug Control Path（Global Pipe）

`\\.\pipe\mcp_rhino` 是开发者调试控制路径，目标文档解析为 `RhinoDoc.ActiveDoc`。

- 触发场景：外部 MCP client 或 smoke 经 `MCP_Rhino.Bridge.exe` 默认参数连接
  `\\.\pipe\mcp_rhino`。
- 用途：快速验证新 tool / skill / agent 的 live Rhino 行为、回归既有能力、调试外部 MCP client
  配置。
- 约束：目标文档必须是当前 `RhinoDoc.ActiveDoc`，且请求 `FilePath` 必须与
  `ActiveDoc.Path` 匹配；否则沿用 `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` /
  `FILE_NOT_ACTIVE`。
- 这条路径是正式保留的开发入口，但不是最终多文档用户体验路径；多文档用户体验应走
  panel-bound per-doc server。
- 新能力的 capability smoke 可以优先走该路径或专属 Rhino smoke command 验证业务能力；
  panel smoke 只验证 panel / bound accessor / CC / per-doc pipe 生命周期。

### Panel-bound execution mode（Per-Document）

`260505_PLAN_active-doc-only-architecture` 定义的 Live Only 是默认模式，绑定到
`RhinoDoc.ActiveDoc`。本子节描述 panel 启用的 per-doc 绑定变体。

- 触发场景：Rhino 内由 chat panel 启动的 per-doc MCP server 实例
  （`\\.\pipe\mcp_rhino_<RuntimeSerialNumber>`）。
- 实现：`Infrastructure/Rhino/Live/BoundLiveRhinoDocumentAccessor` 实现
  `ILiveRhinoDocumentAccessor`，所有 `RhinoDoc` 解析都来自
  `RhinoDoc.FromRuntimeSerialNumber(boundSerial)`，**不**走 `ActiveDoc`。
- 约束（与 §通用契约 的全局 pipe 路径并列）：
  - 绑定 doc 必须存在（`FromRuntimeSerialNumber` 非 null）→ 否则返回 `DOCUMENT_CLOSED`。
  - 绑定 doc 必须已落盘（`doc.Path` 非空）→ 否则沿用 `ACTIVE_DOC_UNSAVED`。
  - 请求 `FilePath` 字段在 bound mode 下被忽略并自动重写为绑定 doc 的当前 `Path`
    （详见本期 PLAN §关键设计 §2 的 bound accessor 重写）；不返回 `FILE_NOT_ACTIVE`。
  - 主线程封送、Undo record 包裹、超时（`> 10s` → `RHINO_MAIN_THREAD_BUSY`）等约束沿用
    Live Only §通用契约。
- 全局管道（`\\.\pipe\mcp_rhino`）作为 Developer Debug Control Path 保留
  `ActiveDoc`-following 严格语义，不受本子节约束。
- 新增错误码 `DOCUMENT_CLOSED`：bound doc 在 tool call 飞行期间被关闭。客户端处理建议：
  停止后续调用；面板侧此时已经在 dispatcher 触发 panel 销毁。
- 并发：per-doc panel 不开启并行写入。所有 mutation 仍经 `RhinoApp.InvokeOnUiThread`
  序列化在 UI 主线程，跨 panel 串行执行。
```

本 plan 的预执行修订记录见文末；正式 Execute 完成后的实际偏差记录写入对应 EXET。

### 11. ALC topology

现行 `MCP_Rhino.RhinoPlugin.cs` 在 Rhino 默认 ALC 加载，`OnLoad` 内构造
`PluginLoadContext`（自定义隔离 ALC），把 `MCP_Rhino.Server.dll` 重新加载进去。当前 isolated
ALC 承载 MCP host、tools、services、RhinoCommon adapter；默认 ALC 只持有 `McpRhinoPlugin`
入口。两者通过反射在 `ServerBootstrap` 上调原始类型方法（参数全部是 primitives）通信。

本期新增类必须显式归属到正确 ALC，否则会撞上类型 identity / Eto / `System.Text.Json`
版本冲突。归属规则：

| 组件 | ALC | 理由 |
| --- | --- | --- |
| Eto.Forms 面板 UI（`RhinoChatPanel`、`ChatTranscriptView`、`ChatInputBox`、`ModelSelector`、`ToolCallCard`） | **默认 ALC** | Eto 与 `Rhino.UI` 已被 Rhino 加载到默认 ALC；从 isolated ALC 注册的 `Type` 在 Rhino panel 注册表里 identity 不一致，`Panels.RegisterPanel(..., PanelType.PerDoc)` 会拒识。 |
| `Rhino.UI.Panels.RegisterPanel(..., PanelType.PerDoc)` glue | **默认 ALC** | 同上。 |
| `PerDocumentPanelDispatcher`（订阅 `RhinoDoc.NewDocument` / `EndOpenDocument` / `CloseDocument` / `EndSaveDocument`） | **默认 ALC** | RhinoDoc 事件源自 Rhino UI 主线程默认 ALC；从 isolated ALC 订阅会产生 marshaling 复杂度。 |
| `ClaudeCodeProcess`、`StreamJsonReader`、`StreamJsonWriter`、`McpConfigBuilder`、`ClaudeCodeAvailability` | **默认 ALC** | 仅用 BCL（`System.Diagnostics.Process`、`System.Text.Json`）；与 Rhino 加载的版本兼容。 |
| `PanelChatSessionService`（CC 流事件 → 面板事件翻译） | **默认 ALC** | 读 CC stdout、写面板控件，全部默认 ALC 内闭环；**不**直接调 MCP host，也不进入 Application DI。 |
| `BoundLiveRhinoDocumentAccessor` | **隔离 ALC** | 实现 `ILiveRhinoDocumentAccessor`，由 isolated ALC 内的 service / tool 通过 DI 消费。 |
| `BoundHostFactory`（构造 bound `IHost` 给 `McpNamedPipeServer`） | **隔离 ALC** | 构造的 DI graph 全部是 isolated ALC 类型。 |
| 既有 `McpNamedPipeServer` 实例（按 per-doc pipe 名复用） | **隔离 ALC** | 不动。 |

**跨 ALC 桥接**：仅追加两个方法到 `ServerBootstrap`（位于 isolated ALC，已通过反射被默认
ALC 调用），参数为 primitives：

```csharp
// 默认 ALC 通过反射调用：
void StartBoundPipeServer(string pipeName, uint runtimeSerialNumber);
void StopBoundPipeServer(string pipeName);
```

调用模式与现行 `Start(pipeName)` 反射调用一致：

```csharp
bootstrapType.GetMethod("StartBoundPipeServer")!.Invoke(_serverHandle, new object[] { pipeName, runtimeSerialNumber });
```

**chat orchestrator 不直接调 MCP host**——这是 ALC 设计的关键简化点。orchestrator
（默认 ALC）做的事是：

1. 通过反射告诉 isolated ALC：起一个绑定到 `runtimeSerialNumber` 的 pipe server。
2. spawn `claude.exe` 子进程（独立 OS 进程，与 ALC 无关）。
3. 把 mcp-config 指向 `MCP_Rhino.Bridge.exe --pipe mcp_rhino_<serial>`，CC 经 Bridge 连
   per-doc pipe，实际 tool 调用全程在 isolated ALC 内闭环。
4. 读 CC stdout 渲染面板。

唯一跨 ALC 调用是步骤 1 与对称的 stop。**没有任何共享 interface 程序集**；不需要
`MCP_Rhino.Contracts` 之类的"中立"程序集。

**`PluginLoadContext` resolver 的影响**：现行 `PluginLoadContext.cs` 把 `MCP_Rhino.Server.dll`
独占加载进 isolated ALC；其他 assembly（`Eto.dll` / `Rhino.UI.dll` / `RhinoCommon.dll`）
回退到默认 ALC 解析。本期不改 resolver 行为：默认 ALC 引用的 `MCP_Rhino.Server.dll` 类型
（panel UI、orchestrator、CC 子进程）也会被加载到默认 ALC，但这是**第二份**加载（与 isolated
ALC 的副本 Type identity 不同）。两份副本在不同 ALC 各自服务自己的对象图，互不干扰。
代码组织上 `Infrastructure/Plugin/Panel/`、`Infrastructure/ClaudeCode/` 这些目录的类只在
默认 ALC 被实例化（plugin 的 `OnLoad` 默认 ALC 路径触发）；isolated ALC 加载这些类只是
内存浪费、不会被实例化，可接受。

未来若想做更彻底的物理拆分（panel 与 dispatcher 抽离到独立 `MCP_Rhino.Plugin.dll`，避免
isolated ALC 加载无用的 Eto 类），是 §后续扩展方向 内容；本期不做。

## 涉及文件

**新增（Infrastructure/Plugin/Panel/）**：
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/RhinoChatPanel.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/RhinoChatPanelHost.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/ChatTranscriptView.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/ChatInputBox.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/ModelSelector.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/ToolCallCard.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/PerDocumentPanelDispatcher.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/PanelChatSessionService.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/IPanelChatSession.cs`

**新增（Infrastructure/Plugin/）**：
- `src/MCP_Rhino.Server/Infrastructure/Plugin/BoundHostFactory.cs`（构造 bound `IHost`
  并交给现有 `McpNamedPipeServer`；不新建 pipe server 类）

**新增（Infrastructure/ClaudeCode/）**：
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/IClaudeCodeProcess.cs`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/ClaudeCodeAvailability.cs`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/ClaudeCodeProcess.cs`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/StreamJsonReader.cs`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/StreamJsonWriter.cs`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/McpConfigBuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/ClaudeCode/ClaudeCodeStreamEvent.cs`

**新增（Infrastructure/Rhino/Live/）**：
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/BoundLiveRhinoDocumentAccessor.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessorFactory.cs`

**新增（Application/Interfaces/）**：
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveRhinoDocumentAccessorFactory.cs`

**新增（Domain/Models/）**：
- `src/MCP_Rhino.Server/Domain/Models/ChatMessage.cs`
- `src/MCP_Rhino.Server/Domain/Models/ChatToolCall.cs`
- `src/MCP_Rhino.Server/Domain/Models/ChatThinkingBlock.cs`
- `src/MCP_Rhino.Server/Domain/Models/ChatSessionInitInfo.cs`

**新增（Domain/Enums/）**：
- `src/MCP_Rhino.Server/Domain/Enums/ChatRole.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ChatToolCallStatus.cs`
- `src/MCP_Rhino.Server/Domain/Enums/ChatTerminalReason.cs`

**新增（Test）**：
- `Project_Test/260505_TEST_rhino-claude-code-panel/`
  - `DeveloperCommandHandler.RhinoClaudeCodePanelSmokeTest.cs`（partial 注册；遵循
    `DeveloperCommandHandler.cs:42-49` 的"`partial void Register<X>Handlers()` 声明 +
    `RegisterExtensionHandlers()` 内显式调用"既有模式：本期 partial 文件实现
    `RegisterRhinoClaudeCodePanelHandlers()` 并把 slug 写入 `_extensionHandlers`，主文件
    `DeveloperCommandHandler.cs` 同步追加一行 `partial void RegisterRhinoClaudeCodePanelHandlers();`
    声明 + 一行 `RegisterRhinoClaudeCodePanelHandlers();` 调用。
  - `RhinoClaudeCodePanelSmokeTest.cs`（CLI fallback + live smoke 主体）
  - `samples/test_mcp_config.json`（最小 mcp-config 样例）
  - `README.md`（手动验收清单 + 排错表）

**修改**：
- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`：
  - `OnLoad` 末尾追加：注册 chat panel 类型 + 启动 `PerDocumentPanelDispatcher`。
  - `OnShutdown` 头部追加：停止 dispatcher（关掉所有 active panel + CC 子进程 + per-doc
    pipe server）。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs`：**完全不动**。已支持
  任意 `pipeName + hostFactory` 参数，本期 per-doc 复用其现有签名。
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs`：抽出
  公共 base 类或工具方法供 `BoundLiveRhinoDocumentAccessor` 复用（避免重复 RhinoCommon
  marshaling + Undo 包裹逻辑）。
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`：注册 `ILiveRhinoDocumentAccessorFactory`
  → `LiveRhinoDocumentAccessorFactory`（在 isolated ALC 范围内）。注意
  `PanelChatSessionService` / `PerDocumentPanelDispatcher` / `ClaudeCodeProcess` 等是默认 ALC
  里的类，**不**进 isolated ALC 的 DI；它们由默认 ALC 的 plugin 入口直接 `new` 出来。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/ServerBootstrap.cs`：追加两个跨 ALC 桥接方法
  `StartBoundPipeServer(string pipeName, uint runtimeSerialNumber)` 与
  `StopBoundPipeServer(string pipeName)`（详见 §关键设计 §11）；维护 isolated ALC 内的
  per-doc `McpNamedPipeServer` 实例字典。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`：
  - `OnLoad` 末尾追加：注册 chat panel 类型 + 启动 `PerDocumentPanelDispatcher`（默认 ALC
    路径直接 `new`，不走 isolated ALC 的反射）。
  - dispatcher 在收到 `EndOpenDocument` 等事件时通过 `bootstrapType.GetMethod("StartBoundPipeServer")
    !.Invoke(_serverHandle, ...)` 调入 isolated ALC 起 per-doc pipe；`CloseDocument` 对称
    调 `StopBoundPipeServer`。
  - `OnShutdown` 头部追加：停止 dispatcher（关掉所有 active panel + CC 子进程 + per-doc
    pipe server）；释放默认 ALC 持有的所有 panel 状态。
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`：追加一行
  `partial void RegisterRhinoClaudeCodePanelHandlers();` 声明（与 `DeveloperCommandHandler.cs:42-49`
  既有声明并列），并在 `RegisterExtensionHandlers()` 实现内（`DeveloperCommandHandler.cs:51-60`）
  追加一行 `RegisterRhinoClaudeCodePanelHandlers();` 调用。**partial 实现本身**位于
  `Project_Test/260505_TEST_rhino-claude-code-panel/DeveloperCommandHandler.RhinoClaudeCodePanelSmokeTest.cs`。
- `src/MCP_Rhino.Bridge/Program.cs`：新增 `--pipe <name>` 参数解析；默认 `mcp_rhino`。
  当前文件硬编码 `const string PipeName = "mcp_rhino";`（`Program.cs:3`），改为从
  `args` 解析。
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`：新增 Eto / Rhino UI 引用：
  `C:\Program Files\Rhino 8\System\Eto.dll` 与
  `C:\Program Files\Rhino 8\System\netcore\Rhino.UI.dll`，按 `RhinoCommon` 现行 HintPath
  模式标记 `<Private>false</Private>`。
- `Project_Guides/MCP_Rhino Architecture.md`：在 `260505_PLAN_active-doc-only-architecture`
  改写后的 §执行模式指南中，更新 §通用契约 的 panel-bound bullet，并在末尾追加
  "Developer Debug Control Path" 与 "Panel-bound execution mode" 两个子节（内容见
  §关键设计 §10）。

**复用（不改）**：
- 所有 `Tools/**` 类（无任何 MCP 工具签名变更；新工具继续按现有约定进入两条 host）。
- 所有 `Skills/**`、`Agents/**`、`Application/UseCases/**`、`Domain/**` 现有内容。
- `Server/ToolRegistration.cs`（`WithToolsFromAssembly` 反射发现继续生效，bound host 也用它）。
- `Project_Test/` 既有目录与既有 smoke。
- Runtime fixtures（`Runtime_Test/MCP_rhino_test.3dm` 等）。

## 使用方式

### 终端用户视角

**前置（一次性）**：

1. 安装 Claude Code（≥ 2.1.119）：`npm install -g @anthropic-ai/claude-code`，确认
   `claude --version` 成功。
2. 在任一终端跑 `claude auth login` 完成登录（OAuth 走浏览器；Max 订阅或 API key 都行）。
3. 按 `README.md` 步骤构建 + 注册 `MCP_Rhino.Server.rhp` 插件，并构建出 `MCP_Rhino.Bridge.exe`。

**日常用法**：

1. 打开任一 .3dm（必须是已保存的），Rhino 自动弹出"Claude Code Chat" panel（首次手动 dock）。
2. Panel header 显示：当前 model、MCP 连接状态徽章、`Clear` / `Open CC settings` 按钮。
3. 在输入框打字、`Ctrl+Enter` 发送；面板里实时流式渲染：
   - 思考块（默认折叠）
   - 文本回复
   - 工具调用卡片（参数 → 状态 → 结果）
   - 末尾本轮成本与时长
4. Rhino 视口随工具调用实时刷新；任何时候 `Ctrl+Z` 撤销最近一次工具写入。
5. 同时打开第二个 .3dm → 第二个 panel 自动出现，独立会话；同样自动挂到第二个文档。
6. 关闭 .3dm → 对应 panel 与对话连同 CC 子进程一起释放。

**典型对话**：

> 当前文档已经绑定到这个 panel，无需再告诉你 filePath。
> 在 `Wall::Concrete` 图层上加 5 条从 (0,0,0) 出发、长度 3 的随机直线；然后告诉我这个图层
> 当前的对象总数。

CC 会自然地调 `CreateLines` + `FilterObjectsByLayer` 工具，工具调用卡片在 panel 里逐个
点亮；Rhino 视口同步出现新几何。

### 开发者 / smoke 视角

**Developer Debug Control Path（Rhino 内 live，走原 Bridge + global server）**：

默认 Bridge 路径保留为开发入口：

```powershell
src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe
```

或在外部 MCP client 的 `.mcp.json` 中继续使用不带 `--pipe` 的 Bridge 配置。此时请求进入
`\\.\pipe\mcp_rhino`，目标文档为 `RhinoDoc.ActiveDoc`。开发者可用它快速验证新 tool /
skill / agent 的 live 行为：只要 Rhino 内 active doc 是目标文档，且请求 `FilePath` 与
`ActiveDoc.Path` 匹配，就能按原方式控制 Rhino 文件。该路径不验证 panel，也不验证 per-doc
绑定；它验证业务能力本身。

**Capability smoke（Rhino 内程序化，推荐给新增能力）**：

每个新增能力仍然可以按 `Project_Guides/MCP_Rhino Plan Log.md` 注册独立 CLI slug + 独立
Rhino command，例如 `_McpGeometryAnalysisSmoke`。这类 smoke 的目标是快速验证 DI、tool
签名、live service 行为和 Undo / document state，不要求同步更新 panel UI 或 CC 编排。Panel
只消费 server tool surface；业务能力先通过 capability smoke 站稳，再进入 panel 回归。

**Panel CLI fallback smoke（无 Rhino）**：

```powershell
dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-panel-smoke-test
```

CLI 模式下没有 `RhinoDoc` 可绑定，绝大部分逻辑无法运行；smoke 仅断言"无 Rhino 时该
命令优雅返回"：

- `[OK] CLI 模式不实例化 `BoundLiveRhinoDocumentAccessor`，避免在无 Rhino host 进程内触碰
  `RhinoDoc.FromRuntimeSerialNumber`；需要 live doc 的路径统一返回 `LIVE_RHINO_REQUIRED`。
- `[OK] McpConfigBuilder` 生成的临时 JSON 可被 `System.Text.Json` 解析并 round-trip。
- `[OK] ClaudeCodeAvailability` 探测 `claude` 在 PATH 并打印版本；不强制要求（CI 没装 CC 也通过）。
- `[OK] MCP_Rhino.Bridge.exe --pipe other_name --help` 退出码 0、stderr 包含 usage（验证
  参数解析未破坏 default 行为）。

**Panel live smoke（Rhino 内程序化）**：

`_McpRhinoClaudeCodePanelSmoke` 命令做以下断言（不走真实 CC，避免依赖网络与 token 成本）：

0. **空白 per-doc panel proof**：注册一个最小 `PanelType.PerDoc` panel 类型，打开两个已保存
   doc，验证 `Panels.GetPanels<T>(doc1)` / `GetPanels<T>(doc2)` 分别返回独立实例，且
   `IPanel.PanelShown/PanelClosing` 收到不同 `documentSerialNumber`。此步失败则停止执行，
   EXET 记录失败并把 plan 改为"单 system panel + per-doc tabs/session list"。
1. 当前已加载 `Runtime_Test/MCP_rhino_test.3dm` 且已 Save → factory 产出 BoundAccessor，
   `Execute(...)` 在 `boundDoc.Path == ActiveDoc.Path` 时正常 round-trip。
2. 注入一个故意错误的 `filePath` 字符串到 tool 请求 → 经 bound accessor 改写为
   `boundDoc.Path`，调用照常成功（验证中间件 §关键设计 §2 的"重写而非拒绝"语义）。
3. 通过反射调 `ServerBootstrap.StartBoundPipeServer("mcp_rhino_smoke_<random>", boundSerial)`，
   再用 `NamedPipeClientStream` 连接，跑一次 MCP `initialize` + `tools/list`，断言返回的
   工具数等于全局 server 的工具数（即没有静默漏注册）。
4. 关闭被绑定的 doc（程序化），再次任何 tool call → 返回 `DOCUMENT_CLOSED`。
5. 通过反射调 `StopBoundPipeServer`；全过程结束后 pipe 句柄释放、临时目录清理。

**Panel live smoke（人工 + 真实 CC，README 文档化）**：

1. 开 .3dm（已 Save），观察 panel 出现 + header 状态徽章为 connected。
2. 在 panel 里发送"列出当前文档的图层"，验证：
   - 看到 `tool_use` 卡片
   - 卡片内显示工具名 = `GetLayersInLive`
   - 卡片状态从 Running → Success
   - 文本回复列出图层名
3. 同时开第二个 .3dm，验证第二个 panel 独立出现；在第二个 panel 里发指令，**第一个**
   .3dm 的视口不应受影响。
4. 关闭第二个 .3dm，验证第二个 panel + CC 子进程被清理（任务管理器不留 `claude.exe`
   悬挂进程）。
5. 在 panel 里点 model 下拉框切换到 `claude-opus-4-7`，确认弹"new conversation" 提示，
   确认后 chat 历史清空、header 更新 model。

## 验收标准

### 构建与静态

- `dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo` 退出码 0、
  零 warning（`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 已生效）。
- `dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo` 退出码 0、
  零 warning。
- 产物：
  - `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp` 存在。
  - `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe` 存在。
  - 调 `MCP_Rhino.Bridge.exe --pipe foo --help` 退出码 0；stderr 包含可见 usage 文本。
- `Project_Test/260505_TEST_rhino-claude-code-panel/samples/test_mcp_config.json` 可被
  `System.Text.Json.JsonDocument.Parse(...)` 零异常解析。
- `Project_Guides/MCP_Rhino Architecture.md`（前置 plan 已改写为 Live Only）更新了
  §通用契约 的 panel-bound bullet：bound mode 忽略 request `FilePath`、重写为绑定文档当前
  `Path`，且正常不返回 `FILE_NOT_ACTIVE`；末尾追加了 "Developer Debug Control Path" 与
  "Panel-bound execution mode" 两个子节。除该 bullet 外，前置 plan 改写后的 §通用契约 /
  §读 / §写 / §CLI 进程入口 / §去归档化 不做回头改写。

### Developer Debug Control Path smoke

- Rhino plugin 加载后，`\\.\pipe\mcp_rhino` 全局 server 仍然存在。
- 不带 `--pipe` 的 `MCP_Rhino.Bridge.exe` 可以连接该全局 server。
- 在 Rhino 中打开并激活一个已保存文档后，经全局 pipe 调用 `tools/list` 能看到完整 tool
  surface；调用一个只读 live tool（如 `GetLayersInLive`）成功，且返回数据来自
  `RhinoDoc.ActiveDoc`。
- 将 Rhino active doc 切到另一个已保存文档后，同一全局 pipe 调用按新的 `ActiveDoc`
  解析；传旧 `FilePath` 时返回 `FILE_NOT_ACTIVE`。
- 该 smoke 明确记录为 developer/debug 验证，不作为 per-doc 用户体验验收。

### Capability smoke convention

- 新能力仍应注册自己的唯一 CLI slug 与 Rhino command，用于快速验证该能力的 live 行为。
- Capability smoke 不要求改 panel UI；除非新能力需要 panel 专属展示，否则 panel 只通过
  `tools/list` 自动获得新的 MCP tool surface。
- EXET 阶段必须记录本能力是否新增或复用了 capability smoke，以及它验证的是业务能力还是
  panel lifecycle。

### Panel CLI fallback smoke

`dotnet run --project src\MCP_Rhino.Server -- rhino-claude-code-panel-smoke-test`：
- 退出码 0。
- 输出包含上文 §使用方式 列出的全部 `[OK]` 行。
- 输出不得包含 `DOCUMENT_CLOSED` 作为 CLI 成功条件；`DOCUMENT_CLOSED` 只在 Rhino-hosted
  live smoke 中验证。
- 工作目录无残留临时文件 / 临时管道句柄。

### Panel live smoke（Rhino 内程序化）

`_McpRhinoClaudeCodePanelSmoke`：
- 命令行打印每一步 `[OK]` / `[FAIL]`；最终 1 行总结成功 / 失败计数。
- 空白 `PanelType.PerDoc` proof 通过：两个已保存 doc 对应两个独立 panel 实例，回调中的
  `documentSerialNumber` 与各自 `RuntimeSerialNumber` 一致。
- 正确绑定的 BoundAccessor 调用与全局 accessor 在数据上一致（同一个 doc，同一组对象数）。
- 故意传错的 `filePath` 经中间件改写为绑定 doc 的 `Path`，调用照常成功；不返回
  `FILE_NOT_ACTIVE`。
- 关闭 bound doc 后调用返回 `DOCUMENT_CLOSED`。
- 命令结束后 `\\.\pipe\mcp_rhino_smoke_*` 句柄已释放（`Get-ChildItem -Path \\.\pipe\\` 看不到）。

### Panel live smoke（人工，真实 CC）

由 `Project_Test/260505_TEST_rhino-claude-code-panel/README.md` 提供清单；EXET 阶段记录
每一项是否通过：

1. 双文档独立 panel + 独立会话。
2. 工具调用卡片 Running → Success 转移可见。
3. 工具实际写入文档（视口刷新 + Undo 栈 +1）。
4. 切 model 重启会话；旧 chat 清空、header 更新。
5. 关 doc 清理子进程（任务管理器无 zombie `claude.exe`）。
6. CC 探测失败（人为 `claude.exe` rename）→ panel 显示安装提示并禁用输入。
7. Bridge.exe 探测失败（人为 rename）→ panel 显示"Bridge not found"。

### 边界用例

- 打开未保存的 New 文档 → panel **不**出现；用户 `_Save` 后才 spin up（监听 `EndSaveDocument`）。
- 打开同一 .3dm 两次（Rhino 多窗口）→ 走两个 RuntimeSerialNumber，两个 panel 独立存在；
  这是合法行为，不报错。
- `_SaveAs` 改路径 → 现有 panel 不重启、CC 不重启、临时 mcp-config 不需要重写（它只引用
  pipe 名）。下一次 tool 调用时 bound accessor 直接读 `boundDoc.Path` 拿到新 path（每次
  现取，不缓存），调用方传的 `filePath` 字符串被忽略。LLM 即便用
  旧 path 拼调用也不会出错。
- Rhino 关闭整个进程 → `OnShutdown` 串行清理所有 panel + 所有 CC 子进程 + 所有 pipe，5 秒
  超时强 kill。
- Bridge.exe 启动失败（pipe 不存在 / 权限 / 拼写错） → CC `init` 事件 `mcp_servers[0].status="failed"`，
  panel 红色提示 + 禁用输入。

### 副作用 / Undo

- 全局 pipe `\\.\pipe\mcp_rhino` 行为零变化，并被明确命名为 Developer Debug Control Path；
  既有外部客户端 smoke（`_McpGeometryAnalysisSmoke` 等）应当全部通过且行为不变（回归）。
- Debug path 仍然跟随 `RhinoDoc.ActiveDoc`，因此只适合开发者有意识地控制 active doc 的场景；
  最终多文档用户体验必须走 panel-bound per-doc server。
- Per-doc panel 写入仍走 `BeginUndoRecord` / `EndUndoRecord`，与现有写入工具一致；"一次
  tool call = 一条 Undo 条目"成立。
- Panel 自身的 UI 操作（开关 panel、切 model、点击 Clear）**不**进 Rhino Undo 栈。

## 风险与回退方案

### 风险

1. **Per-doc panel runtime 行为仍需 Rhino 内验证**：metadata spike 已确认 RhinoCommon
   暴露 `PanelType.PerDoc`、`IPanel` document callbacks、`GetPanel/GetPanels/ClosePanel`
   的 doc overload，但没有真实创建 Eto 控件、dock panel 或多文档实例。EXET 第一门槛必须
   先搭一个默认 ALC 内的空白 `PanelType.PerDoc` panel，验证双文档独立实例；失败时停止，
   改为"单 system panel + per-doc tabs/session list"设计。
2. **默认 ALC 的 Eto / Rhino.UI / System.Text.Json 兼容性**：panel UI、CC stream-json
   解析、子进程封装都在默认 ALC。实现不得依赖 isolated ALC 内的 MCP SDK / STJ 10 类型，
   也不得使用 Rhino 默认 `System.Text.Json` 不支持的新 API。缓解：空白 panel proof 先验证
   Eto/Rhino.UI 注册；stream-json parser 使用 Rhino 默认 STJ 可用的基础 API，并在 CLI smoke
   中解析样本 JSON。
3. **Stream-json 双向多轮协议**：spike 只验证了"单条 prompt → 完整 response"。多轮会话
   要求 stdin 持续写入 + stdout 持续读取。CC 内部应当支持，但 v1 实现里需要测：
   - 第二条 user 消息发出后能否复用同一 session（不应因 `--print` 而退出）。
   - tool_use / tool_result 的 stream-json 形态。
   - 中途 stdin EOF 时 CC 是否优雅终止还是 hang。
   缓解：EXET 第一步搭"echo bot"（panel 不连真 MCP，只跟 CC 打招呼），先跑通双向消息流，
   再接 MCP。
4. **`--input-format stream-json` 写入格式细微差异**：CC 内部用与 Claude Agent SDK 一致
   的 wire format，但 SDK 文档主要在 TS / Python；C# 实现需要 mirror。
   缓解：参考 `claude` 自带 `--debug` 模式抓真实输入格式样本（CC 自己也是这个 wire
   format 的消费者）；echo bot 阶段完成对照。
5. **CC 子进程在 Rhino 进程退出时的 zombie 风险**：Windows 不像 Linux 有 process group，
   父进程崩溃不一定立即收割子进程。
   缓解：注册 Job Object（Windows）把 CC 进程绑定到 Rhino，让 Rhino 退出时 OS 自动 kill 子
   进程；同时 OnShutdown 主动 Kill。
6. **per-doc pipe 句柄泄漏**：`NamedPipeServerStream` 释放有微妙的时序。
   缓解：smoke 里专门验"close doc → pipe 句柄消失"；EXET 阶段排查 dispose 顺序。
7. **CC 版本漂移**：未来 CC 升级可能改 stream-json 字段或行为。
   缓解：`ClaudeCodeAvailability` 显式 require ≥ 2.1.119，遇到不识别字段降级为
   `UnknownEvent` 在 Diagnostics 抽屉打印，不 crash 主流程。
8. **大 transcript 内存增长**：长时间不 Clear 的会话，transcript view 列表可能很长。
   缓解：v1 不做虚拟化 ListView；超过 500 条消息时 panel 提示 Clear。后续可加
   virtualization。
9. **`--max-budget-usd` 默认未设**：API key 用户可能跑出大额账单。
   缓解：Settings UI v1 不做；README 写明"如非 Max 订阅，请在 ~/.claude/settings.json
   设 maxBudgetUsd"；后续可加 panel 内置 budget 输入。
10. **前置 plan 落地次序错配**：本 plan 假定 `260505_PLAN_active-doc-only-architecture`
   已经合并；若顺序颠倒，bound MCP host 会暴露 offline 工具表面，破坏 per-doc 绑定语义。
   缓解：两个 plan 的 Execute 必须严格按顺序进行；本 plan 的 EXET 第一步检查仓库内
   `File3dm.Read` 调用数应当为 0，否则 abort 并要求先合并前置 plan。
11. **CC 输出格式包含 thinking 但 Bedrock / Vertex 可能不支持**：换 provider 后
    `assistant.content` 可能没有 thinking 块。
    缓解：panel 渲染时 `thinking` 块缺失只是少一个折叠区，不影响主流程。
12. **Developer Debug Control Path 被误当成最终用户路径**：全局 pipe 仍跟随
    `RhinoDoc.ActiveDoc`，多文档场景下没有 per-doc 绑定体验。缓解：Architecture / README /
    samples 明确标注它是 developer/debug control path；panel-bound 才是最终用户路径。

### 回退方案

- **整能力回退**：移除 `OnLoad` 末尾对 `PerDocumentPanelDispatcher.Start()` 的调用 →
  panel 不再生成；全局 pipe 与所有现有工具行为完全保留。CC 进程不会被启动；mcp-config
  临时文件不会生成；BoundAccessor / BoundPipeServer 代码可保留但未被使用。
- **单文件回退**：每个新增类都落在新目录或新文件，没有改既有 Tool / Skill / Agent
  签名。`git revert` 整批 commit 即可；既有外部客户端体验零回归。
- **架构指南回退**：删除 §"Developer Debug Control Path" 与
  §"Panel-bound execution mode" 子节；如需完整回退，恢复 §通用契约 中 panel-bound
  bullet 对 request `FilePath` 匹配的旧表述。
- **Bridge `--pipe` 参数回退**：删除参数解析回到硬编码 `mcp_rhino`；外部客户端配置依旧
  正常（它们本来不传 `--pipe`）。

## 后续扩展方向

- **对话历史持久化**：把 transcript + session_id 写到 `<doc-dir>/<doc-name>.mcp_chat.jsonl`
  或 doc user-string，重启 Rhino / 重开 .3dm 时 `--resume <session_id>` 续上。
- **Compact / Clear 策略**：长会话自动调用 `/compact`（CC 自带）或 panel 主动 kill +
  `--continue` re-spawn 接续上下文。
- **多 panel per doc**：支持同一 .3dm 多个 chat panel（不同任务并行讨论）；需要 pipe 名
  额外加 panel-id 区分。
- **Sidecar / 外部 CC 终端入口**：panel header 加"Open in Terminal"按钮，启动 `wt.exe` /
  `cmd.exe` 跑同一 mcp-config，方便用户用全功能 CC（含 `/model`、`/login`、`/skill`）
  接入同一 doc 的 MCP server。
- **Panel 自定义 system prompt**：允许在 panel UI 里写入 custom system prompt（注入到
  `--append-system-prompt`），把"我是建筑师 / 我是产品设计师"等 persona 沉淀到 Rhino
  本地配置。
- **Tool 调用分组与重放**：把一轮 user turn 内的 tool call 序列存档，未来可"replay
  这一轮"（重新执行而不重新问 LLM），用于稳定流程的批量化。
- **Cost / token 仪表盘**：把 `result.total_cost_usd` 与 `usage` 累加渲染到 Rhino
  status bar 或 panel header；按 doc 维度统计每张图纸的 LLM 成本。
- **Mac Rhino 适配**：Eto.Forms 已经跨平台；剩下的是 Bridge.exe 改 `dotnet` cross-publish
  + 命名管道在 Mac/Linux 用 Unix Domain Socket 替代。独立 plan。
- **Skill 联动**：把 `Skills/**` 下的复合能力暴露给 panel 一个 quick-action 区
  （`Filter & confirm`、`Layer audit` 等按钮），不是聊天而是一键触发。
- **远程 Rhino**：把 per-doc bound MCP host 的 transport 层抽象成 `IBoundMcpTransport`，
  未来可以接
  WebSocket / gRPC，让 panel 跑在远端 Rhino 上（结合 RhinoCommon 8 的 Compute 服务化）。

## 修订记录（2026-05-05）

- 根据 metadata-only Rhino panel API spike，补充 `PanelType.PerDoc`、`IPanel` per-doc callback、
  `Panels.GetPanel/GetPanels/ClosePanel` document overload 结论，并把 Rhino 内空白 per-doc
  panel proof 设为 EXET 第一门槛。
- 修正 `Plan Log` smoke 注册规则后，同步本 plan 的 partial hook 接入方式：测试 partial
  实现 `RegisterRhinoClaudeCodePanelHandlers()`，主 `DeveloperCommandHandler.cs` 只保留唯一
  `RegisterExtensionHandlers()` 聚合实现。
- 将 `PanelChatSessionService` 与 `IPanelChatSession` 归入默认 ALC 的
  `Infrastructure/Plugin/Panel/`，将 `IClaudeCodeProcess` 归入 `Infrastructure/ClaudeCode/`，
  避免 UI / 子进程编排进入 Application 层。
- 将 bound mode 的 `FilePath` 处理收敛为 `BoundLiveRhinoDocumentAccessor` 内部重写：忽略
  调用方入参并使用绑定 doc 当前 `Path`，CLI fallback 不再把 `DOCUMENT_CLOSED` 当作成功条件。
- 修正 `_SaveAs` 文案：只刷新 panel header / 当前路径状态；临时 mcp-config 只引用 Bridge
  与 pipe 名，不因 doc path 变化而重写。
- 根据调试工作流需求，明确保留 `\\.\pipe\mcp_rhino` + 默认 Bridge 为
  Developer Debug Control Path：用于新能力快速 live 验证、capability smoke 和外部 MCP client
  调试；panel-bound path 只承担最终 per-doc 用户体验与 panel 生命周期验证。
- 自审发现 Architecture §通用契约 中旧的 panel-bound `FilePath` 匹配语义会与
  `BoundLiveRhinoDocumentAccessor` 的入参忽略 / 路径重写策略冲突；修订本 plan，要求
  Execute 阶段同步更新该 bullet，而不是只在指南末尾追加新子节。
