# MCP_Rhino

让兼容 MCP 的 agent 通过唯一的 Router 连接方式操作 Rhino 8 文档：读取几何、筛选图层、创建或修改对象、执行分析与导出。Live 写入进入 Rhino Undo 栈；离线脚本编辑始终输出带明确后缀的新 `.3dm` 副本，绝不覆盖原文件。

## 项目形态

- [src/MCP_Rhino.Server/](src/MCP_Rhino.Server/)：Rhino 插件，构建产出 `MCP_Rhino.Server.rhp`。Debug 与 Release 使用相同的 Router-only 启动形态，并为每个已保存打开文档发布独立 route endpoint。
- [src/MCP_Rhino.Transport/](src/MCP_Rhino.Transport/)：Server 与 Router 共享的 BCL-only discovery、descriptor、attestation 与 route protocol。
- [src/MCP_Rhino.Router/](src/MCP_Rhino.Router/)：唯一 MCP stdio gateway；每个 MCP client session 启动自己的 Router，并独立选择目标 Rhino 文档。
- [src/MCP_Rhino.Offline/](src/MCP_Rhino.Offline/)：Rhino 外的受限 JavaScript 脚本编辑，使用 Rhino3dm 处理内存模型；不需要 Rhino/Python/Docker 运行。
- [src/MCP_Rhino.RhinoWorker/](src/MCP_Rhino.RhinoWorker/)：独立 headless Rhino 8 worker，用可信 C# 脚本导入、编辑 DWG/DXF/3dm，并只生成带后缀的 3dm 副本。
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

### Custom live Rhino scripts

The MCP connection supports trusted synchronous C# in the same explicitly selected open, saved
document used by other tools. No COM connection, `StartScriptServer`, foreground activation, or
per-task plug-in is required. Preview with `preview_rhino_script`, execute the returned token with
`apply_rhino_script`, and poll `get_rhino_script_status`. Repeating apply with the same token does
not rerun it. `cancel_rhino_script` requests cooperative cancellation.

Example script: `Result = new { doc.Name, Count = doc.Objects.Count };`.
Globals are `doc`, `Parameters` (parsed JSON object), `Result`, `Log(string)`, `CancellationToken`,
and `Checkpoint()`. RhinoCommon geometry and document namespaces are imported. Preview compiles
only; scripts should validate their own model assumptions before edits. Execution owns one Undo
record and rolls back ordinary document edits on failure. C# is full trust, not a sandbox; external
side effects cannot be undone. Do not use async/background Rhino work, commands, ActiveDoc,
manual Undo, or save/close documents. Source-loading directives are unsupported. Time limits are
cooperative; code that ignores cancellation remains Running until it returns. Tokens/results are
bounded and belong to the same backend connection. Existing Router routing rules still apply:
untitled documents must first be saved, and duplicate paths require explicit session selection.

### Offline script editing

Offline tools are available through the same Router even when Rhino is closed:

1. `rhino_offline_inspect_document(filePath)` reads saved disk state and returns scripting help, object IDs, layers, and metadata (objects are paged).
2. `rhino_offline_preview_script(filePath, script)` executes a JavaScript modeling script in memory and returns a one-use preview token, counts, and log.
3. `rhino_offline_apply_script(filePath, previewToken)` publishes the exact preview as `Model_YYYYMMDD_HHMMSS.3dm` using local date and 24-hour time. Same-second collisions wait for an available second; existing files are never overwritten. Use the returned `outputPath` for further work without accumulating suffixes.

```javascript
for (let offset = 0; ; offset += 200) {
    const page = model.objects(offset, 200);
    for (const object of page.items) {
        if (!object.isDefinitionGeometry && object.name.startsWith('Panel')) {
            model.setAttributes([object.id], {
                color: '#4488CC', userText: { Reviewed: 'Yes' }
            });
        }
    }
    if (offset + page.items.length >= page.total) break;
}
model.log('Panel review metadata added');
```

Scripts support loops/conditions, object attributes and user text, document user text, layer creation/editing, affine transforms, object deletion, and point/line/polyline/sphere/box creation. They have no filesystem, process, CLR, network or module access. Full Rhino modeling, Grasshopper and viewport operations remain live-only. This is a modeling script runtime, not arbitrary Python/C# execution.

The source is always read-only. Every apply creates another copy; there is no overwrite/output-path option. A changed source invalidates the preview. Tokens last ten minutes in the same Router session. Known open documents must use live tools; offline reads cannot see unsaved changes in undiscovered sessions. Source/output limit: 256 MiB. See [offline validation and examples](Project_Test/261010_TEST_offline-copy-edit/README.md).

DWG/DXF and native Rhino operations use a separate script workflow, requiring licensed Rhino 8 on Windows:

1. `rhino_offline_preview_rhino_script(filePath, script, modelUnits)` imports a temporary snapshot in a headless Rhino worker. An empty script converts only. C# uses `doc` (RhinoDoc), `Parameters` (JSON), `Result` (JSON-serializable), and `Log(string)`.
2. `rhino_offline_apply_rhino_script(filePath, previewToken)` saves the exact result to a new suffixed sibling `.3dm`.

For example, preview a DWG with `modelUnits="Inches"` and `script="doc.Objects.AddPoint(new Point3d(0,0,4.46645));"`, then apply its token. A trusted `.cs`/`.csx` library can provide one named class through `libraryPath` and `libraryType`. C# runs with full trust; it is not the restricted JavaScript interpreter. Scripts must be synchronous; timeout is 1–600 seconds (default 120). Original input is read-locked, and publication always creates a copy. RhinoCommon stays isolated from Router's Rhino3dm process. See [native DWG validation](Project_Test/261010_TEST_offline-rhino-dwg/README.md).

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

Router 按需连接：空闲的 agent session 不占用任何 route 连接；只有选中或通过 `filePath` 实际使用过的文档才保留一个工作连接，直到文档关闭、generation 变化或 Router 退出。普通 Save 不会触发重连。每个文档 route 最多 32 个并发连接；全部被其他 session 占用时返回可重试的 `DOCUMENT_ENDPOINT_BUSY`（文档仍然打开，结束空闲 session 即可释放；不要改用离线工具）。

## 测试

```powershell
powershell -ExecutionPolicy Bypass -File Project_Archive\Project_Test\260804_TEST_router-only-transport\RouterOnlyTransportSmoke.ps1
dotnet run --project Project_Archive\Project_Test\260729_TEST_multi-document-rhino-router\RouterProtocolSmoke\RouterProtocolSmoke.csproj -c Debug
dotnet run --project Project_Test\261022_TEST_router-route-capacity\RouteCapacitySmoke\RouteCapacitySmoke.csproj -c Debug
dotnet build .\MCP_Rhino.sln -c Debug
dotnet build .\MCP_Rhino.sln -c Release
```

## 架构与指南

- [Project_Guides/MCP_Rhino Architecture.md](Project_Guides/MCP_Rhino%20Architecture.md)：目录归属、Router-only transport、Live 与 explicit offline script 执行模式及安全契约。
- [Runtime_Workflow/MCP_Rhino Workflow.md](Runtime_Workflow/MCP_Rhino%20Workflow.md)：运行时能力路由与 gap handling。
- [Project_Guides/MCP_Rhino Plan Log.md](Project_Guides/MCP_Rhino%20Plan%20Log.md)：按改动规模选择完整记录或单份 EXET，复用测试证据，并归档已完成工作。

## License

内部项目，暂无公开 license。

## Panel cladding repository

The standalone PanelCladdingEditor plug-in moved to
[novan-xu/PanelCladding](https://github.com/novan-xu/PanelCladding) on 2026-10-13.
Its local checkout is `C:\Projects\PanelCladding`. Source, packaging, design assets
and product-specific development records are maintained there; MCP_Rhino builds
independently. Existing Rhino installation paths and plug-in identity are unchanged.
