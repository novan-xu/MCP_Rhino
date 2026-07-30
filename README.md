# MCP_Rhino

让 LLM（Claude / 其它兼容 MCP 的大模型）通过 [Model Context Protocol](https://modelcontextprotocol.io/) 直接操作**正在运行的 Rhino 8** 里的当前文档——读取几何、筛图层、建/改/删对象、做几何分析——所有写入动作都自动进入 Rhino 的 Undo 栈，按 `Ctrl+Z` 即可回退。

## 项目形态

- [src/MCP_Rhino.Server/](src/MCP_Rhino.Server/) —— 主工程，构建产出 `MCP_Rhino.Server.rhp`（Rhino 插件）。`Debug` 产物保留旧的 bridge-pipe-only 插件形态，只启动固定 debug/test Named Pipe（`\\.\pipe\mcp_rhino`）；`Release` 产物是 chat-capable 插件，同样保留 debug pipe，并为 `_Mcpchat` 启动 process-scoped panel pipes（`\\.\pipe\mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`）。
- [src/MCP_Rhino.Bridge/](src/MCP_Rhino.Bridge/) —— 独立 `.exe`，承担 **stdio ↔ Named Pipe** 桥接，给 MCP Client 直接 spawn。
- [src/MCP_Rhino.Companion/](src/MCP_Rhino.Companion/) —— 独立 WPF / WebView2 chat UI，`_Mcpchat` 默认启动它，并绑定到当前已保存 Rhino 文档的 per-doc pipe。
- [src/MCP_Rhino.Transport/](src/MCP_Rhino.Transport/) —— Rhino 与 Router 共享的 BCL-only route discovery / attestation contract。
- [src/MCP_Rhino.Router/](src/MCP_Rhino.Router/) —— 每个外部 MCP session 自动启动的 stdio gateway；一个 session 可选择并控制多个已打开 Rhino 文档。

能力矩阵（当前已落地的 MCP Tool 分类）：几何创建 / 几何修改 / 对象编辑 / 对象与文档级 UserString / 图层管理 / 对象筛查 / 几何分析。详见 [src/MCP_Rhino.Server/Tools/](src/MCP_Rhino.Server/Tools/) 下各子目录。

## 快速开始（Windows 11 + Rhino 8）

### 前置
- .NET 8 SDK
- Rhino 8（默认安装路径 `C:\Program Files\Rhino 8\`）
- 任一 MCP Client：Claude Desktop / Claude Code / Cursor / VS Code MCP 扩展 / Cline / Continue 等

### Step 1 —— 构建产物

正常安装建议构建一个 Release bundle：

```powershell
Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1
```

然后从输出 bundle 运行 `Installer\Install-McpRhino.ps1`。它将 Release `.rhp` 安装到 Rhino
8 当前用户 package 目录，并把 Router / Bridge / Companion 安装到稳定的
`%LOCALAPPDATA%\MCP_Rhino\bin`。如需修改某个 JSON MCP client 配置，必须显式传入
`-ConfigureClient -ClientConfigPath <path>`；否则 installer 只输出可复制的配置 snippet。

下面的直接 project build 用于开发：

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release
dotnet build src\MCP_Rhino.Router\MCP_Rhino.Router.csproj -c Release
```

构建产出：

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`
- `src\MCP_Rhino.Companion\bin\Release\net8.0-windows\MCP_Rhino.Companion.exe`
- `src\MCP_Rhino.Router\bin\Release\net8.0\MCP_Rhino.Router.exe`

需要旧的 bridge-pipe-only Rhino 插件时，也构建 Debug：

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug
```

Debug 输出 `src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.rhp`，只用于 `MCP_Rhino.Bridge.exe` + `\\.\pipe\mcp_rhino` 的外部 MCP client / test route；它不会打开 `_Mcpchat`、Companion 或 panel-bound pipes。Release 输出仍用于 chat panel / Companion。

> Rhino 如果装在非默认路径，需要先改 [src/MCP_Rhino.Server/MCP_Rhino.Server.csproj](src/MCP_Rhino.Server/MCP_Rhino.Server.csproj) 里 `RhinoCommon` 的 `HintPath`。

### Step 2 —— 启动 Rhino

安装后的 Release `.rhp` 会在每个 Rhino 进程启动时自动加载，一次加载即可跟踪该进程内
所有已打开文档；不需要运行 `_LoadPlugin`。直接 project build 的开发产物仍可拖入 Rhino
视口，或运行 `_PlugInManager` → `Install...`。成功标志是命令行出现：

```text
MCP_Rhino plugin loaded. Developer debug pipe requested: \\.\pipe\mcp_rhino
```

### Step 3 —— 打开并保存一个 .3dm

Rhino 里 `_Open` 目标文件并 `_Save` 过（必须已落盘，否则所有工具会返回 `ACTIVE_DOC_UNSAVED`）。仓库里有一个 fixture 可用：[Runtime_Test/MCP_rhino_test.3dm](Runtime_Test/MCP_rhino_test.3dm)。

### Step 4 —— 接入 MCP Client

**任一支持 stdio 的 MCP Client** —— 正常安装使用稳定 Router 路径：

```json
{
  "mcpServers": {
    "mcp-rhino": {
      "type": "stdio",
      "command": "C:\\Users\\<user>\\AppData\\Local\\MCP_Rhino\\bin\\MCP_Rhino.Router.exe",
      "args": []
    }
  }
}
```

每个 agent session 会自动 spawn 自己的 Router；不要手工先运行
`MCP_Rhino.Router.exe`。多个 agent 可以同时连接同一批 Rhino 文档，并各自保持不同选择。

Router 连接后先调用 `rhino_router_list_documents`，再用
`rhino_router_select_document(sessionId)` 选择目标。带顶层 `filePath` 的工具也可在路径唯一时
直接路由。

调试旧 fixed pipe 时才把 client command 指向 `MCP_Rhino.Bridge.exe`。Bridge path 仍只连接
一个拥有 `\\.\pipe\mcp_rhino` 的 Rhino 进程，不提供外部多文档选择。

> 多个 Rhino 进程并行时，`\\.\pipe\mcp_rhino` 仍然只作为 single-owner debug/test 入口；
> 外部 agent 走每文档 `mcp_rhino_route_*` endpoint，`_Mcpchat` 则继续使用自己的
> panel-bound pipe。

### Step 5 —— 开始聊天

推荐在 Rhino 里运行 `_Mcpchat`，它会打开绑定到当前已保存文档的 standalone Companion。外部
MCP Client 默认通过 Router 选择任一已打开文档；只有显式 Bridge 开发配置才走
`\\.\pipe\mcp_rhino` debug/test 入口。

第一句建议显式告诉 LLM 当前文档路径，避免它猜出错误 `filePath`：

> 当前 Rhino 文档的绝对路径是 `C:\Projects\MCP_Rhino\Runtime_Test\MCP_rhino_test.3dm`，接下来所有 tool 调用都用这个 filePath。帮我列出当前文档的图层与对象数。

之后就可以发各种自然语言指令，例如：

- "把 `Wall::Concrete` 图层上所有曲面的总面积算出来。"
- "在当前原点画 10 条从原点出发、长度 5、方向随机的直线。"
- "审计那条 GUID 为 `xxx` 的曲线和相邻曲面在端点是不是 G2 光滑。"

写入类的指令执行后 Rhino 视口会立刻刷新；`Ctrl+Z` 一步回退。

## 架构数据路径

```
MCP Client (Codex / Claude Code / …)
        │    stdio (MCP JSON-RPC)
        ▼
MCP_Rhino.Router.exe  (one process per MCP client session)
        │    discovery + selected document
        ▼
route endpoint  mcp_rhino_route_<ProcessId>_<RuntimeSerialNumber>
        ▼
McpNamedPipeServer  (inside Rhino .rhp)
        │    MCP SDK dispatcher
        ▼
*InLive Tool  (Tools/**)
        │    ILiveRhinoDocumentAccessor → RhinoApp.InvokeOnUiThread
        ▼
RhinoDoc.FromRuntimeSerialNumber(...)   ← 不依赖 foreground activation
```

`MCP_Rhino.Bridge.exe` + `\\.\pipe\mcp_rhino` remains the single-owner developer/debug path and
continues to target `RhinoDoc.ActiveDoc`.

`_Mcpchat` / Companion 路径使用 per-document pipe：

```text
Rhino _Mcpchat
        ▼
MCP_Rhino.Companion.exe
        │    launches Claude Code / Codex CLI with MCP config
        ▼
MCP_Rhino.Bridge.exe --pipe mcp_rhino_<ProcessId>_<RuntimeSerialNumber>
        │    named pipe
        ▼
Bound MCP server inside Rhino .rhp
        ▼
RhinoDoc.FromRuntimeSerialNumber(...)
```

每次写入被 `BeginUndoRecord` / `EndUndoRecord` 包裹，所以"一次 MCP tool 调用 = 一条 Undo 条目"。

## 架构与指南

- [Project_Guides/MCP_Rhino Architecture.md](Project_Guides/MCP_Rhino%20Architecture.md) —— 目录归属、Live Only 执行模式、命名指南。
- [Project_Guides/MCP_Rhino Plan Log.md](Project_Guides/MCP_Rhino%20Plan%20Log.md) —— 每次能力演进必须产出 Plan / Exet / Test 三件套的命名与结构。

## 出问题时去哪看

| 症状 | 去哪看 |
| --- | --- |
| 客户端连不上 | [Project_Archive/Project_Test/260422_TEST_mcp-client-integration/README.md](Project_Archive/Project_Test/260422_TEST_mcp-client-integration/README.md) 排错表 |
| Bridge 握手排查 | [Project_Archive/Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1](Project_Archive/Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1)（不依赖任何 MCP Client 的探针） |
| 某个能力的执行细节 | 对应 `Project_Exet/YYMMDD_EXET_<capability>.md` |
| 某个能力的设计背景 | 对应 `Project_Plan/YYMMDD_PLAN_<capability>.md` |

## License

内部项目，暂无公开 license。
