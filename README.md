# MCP_Rhino

让 LLM（Claude / 其它兼容 MCP 的大模型）通过 [Model Context Protocol](https://modelcontextprotocol.io/) 直接操作**正在运行的 Rhino 8** 里的当前文档——读取几何、筛图层、建/改/删对象、做几何分析——所有写入动作都自动进入 Rhino 的 Undo 栈，按 `Ctrl+Z` 即可回退。

## 项目形态

- [src/MCP_Rhino.Server/](src/MCP_Rhino.Server/) —— 主工程，构建产出 `MCP_Rhino.Server.rhp`（Rhino 插件）。插件加载后会尝试启动固定 debug/test Named Pipe（`\\.\pipe\mcp_rhino`），并为 `_Mcpchat` 启动 process-scoped panel pipes（`\\.\pipe\mcp_rhino_<ProcessId>_<RuntimeSerialNumber>`）。
- [src/MCP_Rhino.Bridge/](src/MCP_Rhino.Bridge/) —— 独立 `.exe`，承担 **stdio ↔ Named Pipe** 桥接，给 MCP Client 直接 spawn。

能力矩阵（当前已落地的 MCP Tool 分类）：几何创建 / 几何修改 / 对象编辑 / 对象与文档级 UserString / 图层管理 / 对象筛查 / 几何分析。详见 [src/MCP_Rhino.Server/Tools/](src/MCP_Rhino.Server/Tools/) 下各子目录。

## 快速开始（Windows 11 + Rhino 8）

### 前置
- .NET 8 SDK
- Rhino 8（默认安装路径 `C:\Program Files\Rhino 8\`）
- 任一 MCP Client：Claude Desktop / Claude Code / Cursor / VS Code MCP 扩展 / Cline / Continue 等

### Step 1 —— 构建两个产物

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release
```

构建产出：

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`

> Rhino 如果装在非默认路径，需要先改 [src/MCP_Rhino.Server/MCP_Rhino.Server.csproj](src/MCP_Rhino.Server/MCP_Rhino.Server.csproj) 里 `RhinoCommon` 的 `HintPath`。

### Step 2 —— 把 .rhp 加载进 Rhino

拖入 Rhino 视口，或运行 `_PlugInManager` → `Install...`。成功标志是命令行出现：

```text
MCP_Rhino plugin loaded. Developer debug pipe requested: \\.\pipe\mcp_rhino
```

### Step 3 —— 打开并保存一个 .3dm

Rhino 里 `_Open` 目标文件并 `_Save` 过（必须已落盘，否则所有工具会返回 `ACTIVE_DOC_UNSAVED`）。仓库里有一个 fixture 可用：[Runtime_Test/MCP_rhino_test.3dm](Runtime_Test/MCP_rhino_test.3dm)。

### Step 4 —— 接入 MCP Client

**Claude Desktop** —— 编辑 `%APPDATA%\Claude\claude_desktop_config.json`：

```json
{
  "mcpServers": {
    "mcp-rhino": {
      "command": "C:\\Projects\\MCP_Rhino\\src\\MCP_Rhino.Bridge\\bin\\Release\\net8.0\\MCP_Rhino.Bridge.exe"
    }
  }
}
```

**Claude Code** —— 推荐在**仓库根**放一个 `.mcp.json`。最省事的做法：把 [Project_Test/260422_TEST_mcp-client-integration/samples/claude_code.mcp.json](Project_Test/260422_TEST_mcp-client-integration/samples/claude_code.mcp.json) 拷到 `<repo>/.mcp.json`，把 `{{BRIDGE_EXE_ABSOLUTE_PATH}}` 替换成本机 `MCP_Rhino.Bridge.exe` 的绝对路径（路径里的反斜杠要写成 `\\`）。首次启动 Claude Code 会提示 `Enable mcp-rhino?`，点同意即生效。`.mcp.json` 已被 [.gitignore](.gitignore) 忽略，每人在自己机器上写自己的路径即可。

> ⚠️ **不要用 `claude mcp add mcp-rhino ...` 的默认形式**——默认是 `local` scope，只写到 `~/.claude.json` 的 `projects[<path>].mcpServers`；实测 VSCode 里的 Claude Code 扩展不会把这里的条目注入到会话中（`/mcp` 会报 `No MCP servers configured`）。要走 CLI，必须显式加 `--scope user`：
>
> ```powershell
> claude mcp add --scope user mcp-rhino "C:\Projects\MCP_Rhino\src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe"
> ```

**其它 MCP Client** —— 新增一条 stdio server，`command` 填 Bridge.exe 绝对路径即可。

重启客户端后 Rhino 命令行会出现：

```text
MCP_Rhino pipe client connected: \\.\pipe\mcp_rhino
```

这条日志 = 端到端通了。

> 多个 Rhino 进程并行时，`\\.\pipe\mcp_rhino` 仍然只作为单 owner debug/test 入口；每个 `_Mcpchat` 会自动使用独立的 `mcp_rhino_<ProcessId>_<RuntimeSerialNumber>` 管道。

### Step 5 —— 开始聊天

在客户端的**对话框**里用自然语言发指令。**不是**在 Rhino 里输指令——Rhino 只负责执行。

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
MCP_Rhino.Bridge.exe
        │    named pipe  \\.\pipe\mcp_rhino
        ▼
McpNamedPipeServer  (inside Rhino .rhp)
        │    MCP SDK dispatcher
        ▼
*InLive Tool  (Tools/**)
        │    ILiveRhinoDocumentAccessor → RhinoApp.InvokeOnUiThread
        ▼
RhinoDoc.ActiveDoc   ← 你在 Rhino 视口里看到结果
```

每次写入被 `BeginUndoRecord` / `EndUndoRecord` 包裹，所以"一次 MCP tool 调用 = 一条 Undo 条目"。

## 架构与指南

- [Project_Guides/MCP_Rhino Architecture.md](Project_Guides/MCP_Rhino%20Architecture.md) —— 目录归属、在线/离线执行模式、命名指南。
- [Project_Guides/MCP_Rhino Plan Log.md](Project_Guides/MCP_Rhino%20Plan%20Log.md) —— 每次能力演进必须产出 Plan / Exet / Test 三件套的命名与结构。

## 出问题时去哪看

| 症状 | 去哪看 |
| --- | --- |
| 客户端连不上 | [Project_Test/260422_TEST_mcp-client-integration/README.md](Project_Test/260422_TEST_mcp-client-integration/README.md) 排错表 |
| Bridge 握手排查 | [Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1](Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1)（不依赖任何 MCP Client 的探针） |
| 某个能力的执行细节 | 对应 `Project_Exet/YYMMDD_EXET_<capability>.md` |
| 某个能力的设计背景 | 对应 `Project_Plan/YYMMDD_PLAN_<capability>.md` |

## License

内部项目，暂无公开 license。
