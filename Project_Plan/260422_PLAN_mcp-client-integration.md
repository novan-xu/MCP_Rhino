# 260422_PLAN_mcp-client-integration

## 背景

MCP_Rhino 代码层的端到端链路已经闭环：

- Rhino Plugin `OnLoad` 启动 `\\.\pipe\mcp_rhino` 管道服务（`src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs:29-45`）。
- `McpNamedPipeServer` 用 MCP SDK `WithStreamServerTransport(...)` 承载 JSON-RPC（`src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs:50-88`）。
- `LiveRhinoDocumentAccessor` 把 RhinoCommon 调用经 `RhinoApp.InvokeOnUiThread` 封送到 UI 主线程，并包裹 `BeginUndoRecord` / `EndUndoRecord`（`src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs:146-184, 43-90`）。
- 插件内用真实的 `AddLiveRhinoAdapters()` 注入 live 服务而非 CLI fallback（`src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs:117-124`）。
- `MCP_Rhino.Bridge` 实现 stdio↔pipe 双向泵（`src/MCP_Rhino.Bridge/Program.cs`）。

缺的不是代码，而是"把用户手里的 MCP Client（Claude Desktop / Claude Code / …）接到 Rhino 内 active `RhinoDoc` 上"这条落地链路的**标准化集成产物**：构建脚本、客户端配置样例、手动验收清单、排错表。当前每个能力都在自己的 plan / exet 末尾局部重述启动步骤，随着 Tool 越加越多，这种"能力里夹启动文档"的分散模式会退化为单点知识。

## 目标

1. 建立一次性、**capability-agnostic** 的客户端集成产物，沉淀启动、验收、排错到单一位置。
2. 核心诉求：**未来任何新增的 Tool / Skill / Agent 不需要重走这条 plan**。依赖 3 个稳定机制承接扩展性：
   - `WithToolsFromAssembly` 反射发现（`src/MCP_Rhino.Server/Server/ToolRegistration.cs:10`）。
   - `AddLiveRhinoAdapters()` 作为新 live service 的既有落点（`src/MCP_Rhino.Server/Server/DependencyInjection.cs:27-39`）。
   - `MCP_Rhino.Bridge` 只搬字节，对 Tool 无感。
3. 产出可被客户端直接复制粘贴的配置样例；客户端侧感知到的只有一条 `mcp-rhino` 服务。
4. 同时在仓库根目录落一份 `README.md`，把"怎么用"沉淀到项目门面，新人 / 新客户端 onboarding 不再需要翻 Plan。

## 架构归属

本期**不新增**任何 `Tools/` `Skills/` `Agents/` `Application/` `Domain/` `Infrastructure/` `Contracts/` `Prompts/` 代码——代码层已完整，本期属于"集成 / 运维"层。

产物落在：

- `Project_Plan/260422_PLAN_mcp-client-integration.md`：本文件。
- `Project_Exet/260422_EXET_mcp-client-integration.md`：执行后补写。
- `Project_Test/260422_TEST_mcp-client-integration/`：README + 配置样例 + 可选探针脚本。
- `README.md`（仓库根）：对外门面文档，与 TEST 目录的 README 在"使用步骤"一节保持口径一致。

本期**不引入**新的 Live Smoke CLI slug，显式偏离 `Project_Rules/MCP_Rhino Plan Log.md` §Live Smoke CLI 入口约定；偏差理由见 §关键设计 §4。

## 关键设计

### 1. 扩展性靠既有机制承接，而非新增抽象

| 未来变更场景 | 本 plan 产物是否需要改 | 原因 |
| --- | --- | --- |
| 新增 `*InLiveTool` / 任意 `[McpServerToolType]` | **否** | `WithToolsFromAssembly` 反射枚举；客户端通过 `tools/list` 动态拿清单 |
| 新增 live 服务（注册到 `AddLiveRhinoAdapters()`） | **否** | 插件 DI 配置已使用该方法；新 service 落在既有约定点 |
| 新增 Skill / Agent | **否** | Agent/Skill 通过 `AddRhinoApplication` 注册，不改客户端视角 |
| 启用 MCP Prompts / Resources / Sampling | **是（仅插件端小改）** | `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs:102-105` 的 SDK builder 链追加 `.WithPromptsFromAssembly()` 等；客户端配置仍不用改 |
| 切换 transport（WebSocket / HTTP SSE / gRPC） | **是（需另起 plan）** | `MCP_Rhino.Bridge` 需重写；客户端 `command` 字段会变 |
| Rhino 大版本升级（8 → 9+） | **是（cosmetic）** | `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj:20-23` 的 `HintPath` 调整；不影响客户端侧 |

### 2. 客户端只知 Bridge，不知 Tool

客户端配置中 `command` 固定指向 `MCP_Rhino.Bridge.exe`，不暴露 pipe 名、不列 Tool。未来工具增加时，客户端无需任何重启之外的动作——断开 Bridge 再重连即可刷新 `tools/list`。

### 3. 单实例管道的已知约束（显式承认，不在本期解决）

`src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs:61` 的 `maxNumberOfServerInstances=1` 意味着同一时刻只有一个客户端连接；当前客户端断开后 accept 循环会自动再开新实例。本期以"同一时间只跑一个 MCP Client"为默认假设，写进 `Project_Test/…/README.md` 的"已知约束"小节；提升并发需独立起 plan。

### 4. 不新增 Live Smoke Slug 的理由

`Project_Rules/MCP_Rhino Plan Log.md` §Live Smoke CLI 入口约定要求"每次能力演进的 live smoke 必须以唯一 CLI slug 注册"。本期显式偏差：**不注册新 slug**。

理由：

- 本期不改变 Tool 集合、不改 DI、不改 live adapter——没有可 smoke 的新 code path。
- 端到端的真实验证必须有真实 MCP Client + Rhino GUI，属于手工验收；强行写 C# 自动化反而制造假阳性。
- 反过来，每一条既有能力的 `_Mcp<Feature>Smoke` 命令已经覆盖自己的 live path，把它们跑通即可证明集成链路没退化。

该偏差会在 `Project_Exet/260422_EXET_mcp-client-integration.md` 的「与计划的偏差」章节再次记录。

### 5. 配置样例作为"契约"沉淀

`Project_Test/…/samples/` 下的 JSON 不是 snippet 文档，而是可直接粘贴生效的**契约**：

- 样例里用 `{{BRIDGE_EXE_ABSOLUTE_PATH}}` 占位，不写死用户机器路径；README 明确替换方式。
- 只承诺 `command` 字段的形状与 Bridge.exe 路径，不承诺具体客户端的 JSON schema 稳定性。客户端 schema 演进时原地重录样例。

### 6. 根 README 与 TEST README 的职责分离

- 仓库根 `README.md`：**面向首次接触项目的人**。讲清"这是什么 / 怎么装 / 怎么用 LLM 操作 Rhino"，一屏到几屏篇幅。
- `Project_Test/260422_TEST_mcp-client-integration/README.md`：**面向集成验收**。强调复现步骤、手动验收断言、排错表、已知约束。

两者在"Step 1 构建 / Step 2 装插件 / Step 3 开存文档 / Step 4 接客户端"这一条主脊上文本对齐；根 README 简写、TEST README 详写。未来出现口径分叉时，以 TEST README 为真。

## 涉及文件

**新增（文档与配置样例，不进二进制产物）**：

- `Project_Plan/260422_PLAN_mcp-client-integration.md` —— 本文件。
- `Project_Exet/260422_EXET_mcp-client-integration.md` —— execute 完成后补写。
- `Project_Test/260422_TEST_mcp-client-integration/README.md`
- `Project_Test/260422_TEST_mcp-client-integration/samples/claude_desktop_config.json`
- `Project_Test/260422_TEST_mcp-client-integration/samples/claude_code.mcp.json`
- `Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1`
- `README.md`（仓库根目录）

**修改**：无。

**复用（不改）**：

- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpNamedPipeServer.cs`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/plugin.manifest`
- `src/MCP_Rhino.Bridge/Program.cs`
- `src/MCP_Rhino.Server/Server/ToolRegistration.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoDocumentAccessor.cs`

## 使用方式

**Step 1 — 构建**

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release
```

产出：

- `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
- `src\MCP_Rhino.Bridge\bin\Release\net8.0\MCP_Rhino.Bridge.exe`

**Step 2 — 注册 .rhp**

在 Rhino 8 里拖入 `.rhp` 或运行 `_PlugInManager` → `Install`。成功标志是命令行出现：

```text
MCP_Rhino plugin loaded. Named pipe ready: \\.\pipe\mcp_rhino
```

**Step 3 — 准备目标文档**

`_Open` 一个 `.3dm` 并 `_Save` 过（否则所有 live Tool 会返回 `ACTIVE_DOC_UNSAVED`）。

**Step 4a — 零客户端冒烟**

在 Rhino 里运行任一已落地能力的 smoke 命令，例如：

```text
_McpGeometryAnalysisSmoke
```

确认 live path 没退化。

**Step 4b — 接入 MCP Client**

把 `Project_Test/260422_TEST_mcp-client-integration/samples/` 下对应客户端的样例合并到其 MCP 配置，`command` 替换为本机 `MCP_Rhino.Bridge.exe` 绝对路径。重启客户端。插件端会打印：

```text
MCP_Rhino pipe client connected: \\.\pipe\mcp_rhino
```

**典型调用（由 LLM 生成 tool call）**：

- 读：`GetObjectMetricsInLive(filePath=<ActiveDoc.Path>, objectIds=[...])`。
- 写：`CreateLines(filePath=<ActiveDoc.Path>, lines=[...])`，Rhino 视口立刻出线；`Ctrl+Z` 可回退。

> 建议在首条 prompt 中明确告诉 LLM：当前 Rhino 文档的绝对路径是什么。否则它容易猜一个错误的 `filePath`，得到 `FILE_NOT_ACTIVE`。

## 验收标准

**构建与静态**

- 两个 csproj `dotnet build -c Release` 通过。
- `samples/*.json` 能被标准 JSON parser 解析。
- `Project_Test/260422_TEST_mcp-client-integration/README.md` 存在且引用了 3 份 samples。
- 根 `README.md` 存在；其"使用步骤"与 TEST README 在口径上一致。

**端到端手动验收（在真实 Rhino 8 会话里）**

- 加载 .rhp 后 Rhino 命令行出现 `Named pipe ready: \\.\pipe\mcp_rhino`。
- Bridge 进程启动后 Rhino 命令行出现 `MCP_Rhino pipe client connected`。
- MCP Client `tools/list` 返回条目数 ≥ 仓库当前 `Tools/**` 下 `[McpServerToolType]` 标注类的总数。
- `GetObjectMetricsInLive` 对 `test-files/MCP_rhino_test.3dm` 的对象返回非 null 度量值。
- `CreateLines` 执行后 `doc.Objects.Count` +N、视口可见新几何、`Ctrl+Z` 后计数与视口均回退。

**扩展性断言（回归验证项）**

假设后续任意一期新增 `FooBarInLiveTool`（遵循 `[McpServerToolType] + [McpServerTool]` 约定），**本 plan 的 samples / README 不需要任何改动**即可让客户端看到并调用该 Tool。验收方式：在新能力的 EXET 文档里引用本 plan 的 README 并确认未修改。

## 风险与回退方案

**风险**

- **单实例管道**（`maxNumberOfServerInstances=1`）：同时只接一个 client，多 IDE 场景下后启的会超时失败。缓解：README「已知约束」明示；升级走独立 plan。
- **RhinoCommon HintPath 写死 `C:\Program Files\Rhino 8\...`**：本机 Rhino 装别处会链接失败。缓解：README 指出修改点；未来可切 NuGet `RhinoCommon` package。
- **`WithToolsFromAssembly` 反射发现**：新 Tool 忘打 `[McpServerToolType]` 会被静默忽略。缓解：每期能力的 smoke 断言预期 tool 名出现（现状已遵守）。
- **客户端配置格式演进**：Claude Desktop / Claude Code 可能调整 JSON schema。缓解：samples 原地重录；README 标注当期验证的客户端版本。

**回退方案**

- 本期无生产代码改动，回退 = 从客户端配置里移除 `mcp-rhino` 条目；.rhp 可通过 `_PlugInManager` Uninstall。
- PLAN / EXET / TEST / README 产物整体 `git revert` 即可清空，不影响既有能力。

## 后续扩展方向

- **多客户端并发**：将 `maxNumberOfServerInstances` 放宽 + 给每条连接独立 scope（独立 plan）。
- **客户端侧一键启动器**：独立 exe 负责"启动 Rhino + 加载 .rhp + 等待管道 + 启 Bridge"（独立 plan）。
- **MCP Prompts / Resources / Sampling**：在 `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs:102-105` SDK builder 链追加；客户端配置可复用本期 samples。
- **Transport 替换**：若部署场景要求网络可达（远程 Rhino 服务器），引入 WebSocket / HTTP SSE Bridge（独立 plan，本期 samples 失效但 PLAN 结构可复用）。
- **Rhino 版本抽象**：把 `HintPath` 改为条件选择或 NuGet 包（独立 plan）。
