# TEST: mcp-client-integration

本目录是 `Project_Plan/260422_PLAN_mcp-client-integration.md` 的集成验收产物。负责把"怎样让一个 MCP Client 真的操作到 Rhino active `RhinoDoc`"沉淀成可复制、可复现的步骤。

## 目录内容

- `README.md`（本文件）：端到端启动步骤 / 手动验收清单 / 排错表 / 已知约束。
- `samples/claude_desktop_config.json`：Claude Desktop 的 `mcpServers` 片段。
- `samples/claude_code.mcp.json`：Claude Code 的 `.mcp.json` / `~/.claude.json` 片段。
- `samples/handshake_probe.ps1`：可选 PowerShell 探针——在不依赖任何 MCP Client 的前提下，spawn `MCP_Rhino.Bridge.exe` 并发送一次 `initialize` + `tools/list` JSON-RPC，打印返回值。用于把"Bridge ↔ Rhino"和"Client ↔ Bridge"两段解耦排查。

## 前置

- Windows 11 + Rhino 8（已安装到 `C:\Program Files\Rhino 8\System\netcore\RhinoCommon.dll`；若路径不同需先改 `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` 的 `HintPath`）。
- .NET 8 SDK。
- 任意 MCP Client：Claude Desktop / Claude Code / Cursor / VS Code MCP 扩展 / Cline / Continue 任选其一。

## Step 1 — 构建

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release
```

产出：

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`

验收点：两条命令均返回 `Build succeeded`。

## Step 2 — 把 .rhp 加载进 Rhino

方式一（最快）：把 `MCP_Rhino.Server.rhp` 拖入 Rhino 视口，对话框里确认 `Load this plug-in`。

方式二：在 Rhino 命令行运行 `_PlugInManager` → `Install...` → 选择 `MCP_Rhino.Server.rhp`。

验收点：Rhino 命令行出现

```text
MCP_Rhino plugin loaded. Named pipe ready: \\.\pipe\mcp_rhino
```

## Step 3 — 准备目标文档

在 Rhino 里 `_Open` 一个 `.3dm`（例如 `Runtime_Test/MCP_rhino_test.3dm`）并 `_Save` 过。未存盘的文档会让所有 `*InLive` Tool 返回 `ACTIVE_DOC_UNSAVED`。

## Step 4a — 零客户端冒烟（推荐先跑）

在 Rhino 里运行任一已落地能力的 smoke 命令，例如：

```text
_McpGeometryAnalysisSmoke
```

验收点：命令执行无红字，对象数 / 图层数 / 文档字符串数 / Undo 条目前后不变。跑得过 = "插件 + live adapter + 主线程封送"全链路健康；Client 接不上时这一条仍能用来缩小故障范围。

## Step 4b — 接入 MCP Client

把 `samples/` 下对应格式的 JSON 合并到客户端配置，`command` 字段替换为本机 `MCP_Rhino.Bridge.exe` 绝对路径。重启客户端。

验收点：Rhino 命令行再出现一行

```text
MCP_Rhino pipe client connected: \\.\pipe\mcp_rhino
```

之后就在客户端的对话框里用自然语言发指令即可。**LLM 需要显式知道 `RhinoDoc.ActiveDoc.Path`**——建议第一句就明示，例如：

> "当前 Rhino 文档的绝对路径是 `<REPO_ROOT>\Runtime_Test\MCP_rhino_test.3dm`，接下来所有 tool 调用都用这个 filePath。帮我列出当前文档的图层与对象数。"

## 端到端手动验收清单

| # | 验收项 | 期望 |
| --- | --- | --- |
| 1 | Step 1 构建 | 两个 csproj `Build succeeded`，exit code 0 |
| 2 | Step 2 加载 .rhp | Rhino 命令行出现 `Named pipe ready` |
| 3 | Step 4a 零客户端 smoke | 全部检查点通过 |
| 4 | Step 4b 客户端连上 | Rhino 命令行出现 `pipe client connected` |
| 5 | `tools/list` 数量 | ≥ 当前 `src/MCP_Rhino.Server/Tools/**` 中 `[McpServerToolType]` 标注类数量 |
| 6 | 读类 tool 真实数据 | 调 `GetObjectMetricsInLive` 针对现有对象返回非 null 度量值 |
| 7 | 写类 tool + Undo | 调 `CreateLines`，Rhino 视口出现新线；`Ctrl+Z` 后消失，`doc.Objects.Count` 回到原值 |

全部通过即确认本期集成链路成立。

## 排错表

| 症状 | 最可能原因 | 定位 |
| --- | --- | --- |
| Bridge 打 `Failed to connect ... within 5000 ms` | Rhino 没开 / 插件没加载 / plugin OnLoad 抛异常 | 先在 Rhino 命令行找 `Named pipe ready`；没有就看 `_PlugInManager` |
| 所有 `*InLive` 返回 `NO_ACTIVE_DOCUMENT` | Rhino 里没有任何打开的文档 | `_Open` 一个 .3dm |
| 返回 `ACTIVE_DOC_UNSAVED` | 新建文档没保存过 | `_Save` 到磁盘 |
| 返回 `FILE_NOT_ACTIVE` | 传的 `filePath` 跟 `doc.Path` 不是同一个 | 让客户端用绝对路径，且与 Rhino 标题栏里的文件一致 |
| 返回 `RHINO_MAIN_THREAD_BUSY` | 主线程被长任务（大命令 / 模态对话框）堵住 > 10s | 关掉前台阻塞，或拆批调用 |
| Plugin 加载失败 | `RhinoCommon.dll` HintPath 在本机不存在 | 调整 `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` 或改装 Rhino 8 到默认路径 |
| `tools/list` 少了你刚加的 Tool | 没打 `[McpServerToolType]` / `[McpServerTool]` 属性，或 Server 没重建 | 属性补齐 + `dotnet build` + 重新加载 .rhp + 重连 Bridge |
| 第二个 MCP Client 连不上 | 管道 `maxNumberOfServerInstances=1`，设计如此 | 让前一个客户端先断开；多并发需独立起 plan |

## 已知约束

- **单实例管道**：`src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs:61` 设为 `maxNumberOfServerInstances=1`，同一时刻只接 1 条 MCP 连接。前一个 Client 断开后 accept loop 会自动重开管道实例，不需要重启 Rhino。
- **主线程 10s 超时**：`src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs:12` 的 `MainThreadTimeout = 10s`；Rhino 主线程被 UI 操作 / 模态对话框阻塞超过 10 秒时所有 `*InLive` Tool 返回 `RHINO_MAIN_THREAD_BUSY`。
- **Bridge 连接超时**：`src/MCP_Rhino.Bridge/Program.cs:4` 的 `ConnectTimeoutMs = 5000`，启动时 5 秒内连不上就退出。启动顺序：先加载 .rhp、再启 Client（由 Client spawn Bridge）。

## 扩展性说明（为什么未来新 Tool 不用改本目录）

本集成链路每一层都对 Tool 名字无感：

- `MCP_Rhino.Bridge` 只是 stdio↔pipe 的字节泵。
- 插件内 `.AddRhinoTools()` 走 `WithToolsFromAssembly`（`src/MCP_Rhino.Server/Server/ToolRegistration.cs:10`），反射发现所有 `[McpServerToolType]`。
- 客户端通过 MCP 协议 `tools/list` 动态拿到 Tool 清单。

所以后续任何一期只要遵循"新 Tool 打 `[McpServerToolType]+[McpServerTool]`、新 live service 注册到 `AddLiveRhinoAdapters()`"的既有约定，**samples/ 与本 README 均无需修改**。这一点也是本期 PLAN §关键设计 §1 的核心承诺。

哪些变更会让本期产物失效？

- 换 transport（WebSocket / HTTP SSE / gRPC）——Bridge 要重写，samples 里 `command` 会变。
- 启用 MCP Prompts / Resources / Sampling——插件端 SDK builder 链要加调用；但 samples 里客户端配置仍不变。
- Rhino 大版本升级到 9+——改 csproj 的 `HintPath`；samples 不变。

这三类都应另起 plan，不回填本期产物。
