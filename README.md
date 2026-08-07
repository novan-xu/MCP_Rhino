# MCP_Rhino

让兼容 MCP 的 agent 通过唯一的 Router 连接方式操作正在运行的 Rhino 8 文档：读取几何、筛选图层、创建或修改对象、执行分析与导出。所有 Rhino 写入都进入 Undo 栈，可用 `Ctrl+Z` 回退。

## 项目形态

- [src/MCP_Rhino.Server/](src/MCP_Rhino.Server/)：Rhino 插件，构建产出 `MCP_Rhino.Server.rhp`。Debug 与 Release 使用相同的 Router-only 启动形态，并为每个已保存打开文档发布独立 route endpoint。
- [src/MCP_Rhino.Transport/](src/MCP_Rhino.Transport/)：Server 与 Router 共享的 BCL-only discovery、descriptor、attestation 与 route protocol。
- [src/MCP_Rhino.Router/](src/MCP_Rhino.Router/)：唯一 MCP stdio gateway；每个 MCP client session 启动自己的 Router，并独立选择目标 Rhino 文档。
- [src/MCP_Rhino.Server/Tools/](src/MCP_Rhino.Server/Tools/)：几何、编辑、图层、分析、Grasshopper、文件、绘图与工作流等 MCP tools。

项目不再构建、安装或支持 fixed debug pipe、stdio Bridge、Companion、`_Mcpchat` 或 panel-bound MCP transport。

## 快速开始（Windows 11 + Rhino 8）

### 前置条件

- .NET 8 SDK
- Rhino 8，默认安装路径 `C:\Program Files\Rhino 8\`
- 支持 stdio MCP 的客户端

### 1. 构建与安装

推荐构建 Release bundle：

```powershell
Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1
```

从输出 bundle 运行 `Installer\Install-McpRhino.ps1`。安装器把 Rhino 插件放到 Rhino 8 当前用户 package 目录，把 Router 放到 `%LOCALAPPDATA%\MCP_Rhino\bin`。只有显式传入 `-ConfigureClient -ClientConfigPath <path>` 时，安装器才修改客户端 JSON；否则输出可复制的 Router 配置。

开发构建：

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

主要产物：

- `src\MCP_Rhino.Server\bin\<Configuration>\net8.0\MCP_Rhino.Server.rhp`
- `src\MCP_Rhino.Router\bin\<Configuration>\net8.0\MCP_Rhino.Router.exe`

Rhino 非默认安装位置时，先更新 [src/MCP_Rhino.Server/MCP_Rhino.Server.csproj](src/MCP_Rhino.Server/MCP_Rhino.Server.csproj) 中 `RhinoCommon`、`Grasshopper` 与 `GH_IO` 的 `HintPath`。

### 2. 启动 Rhino 并打开文档

安装后的插件在 Rhino 启动时自动加载。打开并保存 `.3dm`；未保存文档会出现在 Router discovery 中，但不可路由。成功启动时 Rhino 命令行会显示：

```text
MCP_Rhino Router-only plugin loaded for Rhino PID <ProcessId>.
MCP_Rhino routed document endpoint dispatcher started.
```

### 3. 配置 MCP client

客户端只启动已安装 Router：

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

不要手工启动共享 Router daemon。每个 MCP client session 自己启动一个 Router process。

### 4. 选择文档并调用工具

1. 调用 `rhino_router_list_documents`。
2. 使用返回的 opaque `sessionId` 调用 `rhino_router_select_document`。
3. 调用普通 Rhino tools。选中的 session 与显式 `filePath` 冲突时，Router 返回 `DOCUMENT_TARGET_CONFLICT`。

多个 agent 可以同时连接同一批 Rhino 文档，并维持互不影响的选择。Route endpoint 绑定 `RhinoDoc.RuntimeSerialNumber`，不依赖当前前台窗口或 active tab。

### Grasshopper authoring

Grasshopper 1 tools use a second explicit target: after selecting the Rhino document through the
Router, call `StartGrasshopper` or `ListGrasshopperDefinitions`, then pass the returned opaque
`definitionSessionId` to every graph read, preview, apply, solve, or clear operation. Targeting never
follows the active Grasshopper canvas.

The authoring surface supports installed components (including installed Python/C#/script-code
components), number sliders, wires, bounded data inspection, solve diagnostics, and stale-safe
preview/apply clearing. Script/code components are classified and warned as executable code; the
relevant tools are open-world. The graph contract does not accept raw script source text.

Grasshopper graph changes use Grasshopper's native undo stack—one undo entry per successful batch—
instead of creating an empty Rhino document undo record. `.gh`/`.ghx` disk reads and writes are not
used.

## 数据路径

```text
MCP Client
    │ stdio MCP JSON-RPC
    ▼
MCP_Rhino.Router.exe
    │ discovery + per-session document selection
    ▼
mcp_rhino_route_<ProcessId>_<RuntimeSerialNumber>
    │ current-user named pipe + endpoint attestation
    ▼
McpNamedPipeServer inside MCP_Rhino.Server.rhp
    │ MCP tool/resource surface
    ▼
RoutedLiveRhinoDocumentAccessor
    │ RhinoApp.InvokeOnUiThread
    ▼
RhinoDoc.FromRuntimeSerialNumber(...)
```

Mutation 断线后不会自动重放；结果不确定时返回 `MUTATION_OUTCOME_UNKNOWN`。每次成功写入由一个 Rhino Undo record 包裹。

## 测试

```powershell
powershell -ExecutionPolicy Bypass -File Project_Test\260804_TEST_router-only-transport\RouterOnlyTransportSmoke.ps1
dotnet run --project Project_Test\260729_TEST_multi-document-rhino-router\RouterProtocolSmoke\RouterProtocolSmoke.csproj -c Debug
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

## 架构与指南

- [Project_Guides/MCP_Rhino Architecture.md](Project_Guides/MCP_Rhino%20Architecture.md)：目录归属、Router-only transport、Live Only 执行模式与安全契约。
- [Runtime_Workflow/MCP_Rhino Workflow.md](Runtime_Workflow/MCP_Rhino%20Workflow.md)：运行时能力路由与 gap handling。
- [Project_Guides/MCP_Rhino Plan Log.md](Project_Guides/MCP_Rhino%20Plan%20Log.md)：PLAN / EXET / TEST 三件套规则。

## License

内部项目，暂无公开 license。
