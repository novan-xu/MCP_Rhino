# 260422_EXET_mcp-client-integration

## 对应计划

- 计划文档：`Project_Plan/260422_PLAN_mcp-client-integration.md`
- 执行日期：2026-04-22

## 关联产物

- 测试目录：`Project_Test/260422_TEST_mcp-client-integration/`
- 仓库根对外文档：`README.md`
- CLI smoke 入口：**无**（本期显式不注册新 slug，理由见 PLAN §关键设计 §4 与下方「与计划的偏差」§2）
- Rhino live smoke 命令：**无**（同上）
- commit / PR：N/A（当前直接在工作区执行）

## 执行结果 / 实际落地范围

本期属于"集成 / 运维"层产物，不新增业务代码。实际落地：

**新增文档与配置**

- `Project_Plan/260422_PLAN_mcp-client-integration.md` —— 计划定稿。
- `Project_Exet/260422_EXET_mcp-client-integration.md` —— 本文件。
- `Project_Test/260422_TEST_mcp-client-integration/README.md` —— 启动步骤 / 手动验收清单 / 排错表 / 已知约束 / 扩展性说明。
- `Project_Test/260422_TEST_mcp-client-integration/samples/claude_desktop_config.json` —— Claude Desktop `mcpServers` 片段，`command` 占位符 `{{BRIDGE_EXE_ABSOLUTE_PATH}}`。
- `Project_Test/260422_TEST_mcp-client-integration/samples/claude_code.mcp.json` —— Claude Code 版本，带 `"type": "stdio"` 字段。
- `Project_Test/260422_TEST_mcp-client-integration/samples/handshake_probe.ps1` —— PowerShell 探针，spawn Bridge 并发送 `initialize` + `tools/list`，用于在不依赖任何 MCP Client 的情况下隔离排查 Bridge↔Rhino 段。
- `README.md`（仓库根）—— 对外门面文档，一屏搞定项目介绍 + Step 1-5 启动 + 架构数据路径 + 外部链接导航。

**修改**：无。**生产代码**：本期零改动。

## 与计划的偏差

1. **Claude Code 样例文件加了 `"type": "stdio"` 字段**
   - PLAN 未显式要求该字段，但 Claude Code 的 `.mcp.json` / `~/.claude.json` 在新版上该字段是 stdio server 的标准写法，补上更稳妥。
   - 不影响 Claude Desktop 样例（后者不需要此字段）。
   - 视为 PLAN §关键设计 §5 "样例原地重录"范围内的细节，不需要回改 PLAN。

2. **显式不注册新 Live Smoke CLI slug（延续 PLAN §关键设计 §4）**
   - `Project_Guides/MCP_Rhino Plan Log.md` §Live Smoke CLI 入口约定要求每期新 slug；本期零代码变更，没有新 code path 可 smoke。
   - 端到端验证必须通过真实 MCP Client + Rhino GUI 手工走一遍，写 C# 自动化反而制造假阳性。
   - 每期能力自己的 `_Mcp<Feature>Smoke` 覆盖各自 live path；集成链路没退化靠"能力侧 smoke 全绿"间接证明。
   - 本偏差在 PLAN 里已预先声明并在此重复记录。

3. **新增了仓库根 `README.md`**
   - PLAN 的 §涉及文件 明确列入 `README.md`（根目录）；此处归属确认为 **"执行内行为"**，不是偏差。特别记录是因为历史上该仓库没有根 README，本期是第一次建立项目门面。

## 施工中发现并修复的问题

1. **JSON 样例里的路径占位符写法**
   - 初版考虑用 `<BRIDGE_EXE_ABSOLUTE_PATH>` 作占位，但 `<>` 在部分 JSON 编辑器的 schema 校验下会 warning。改为 `{{BRIDGE_EXE_ABSOLUTE_PATH}}`，mustache 风格，跨 schema 校验器更干净。

2. **handshake_probe.ps1 的响应读取策略**
   - 初版用阻塞 `ReadLine()`，MCP server 若迟迟不返回会卡死进程。改为带 10 秒 deadline 的非阻塞轮询（`Peek()` + `Start-Sleep 50ms`），同时只等 2 条响应（`initialize` + `tools/list`），到期主动 `Kill`，避免脚本在 Rhino 未开时无限挂起。

3. **根 README 与 TEST README 职责划分**
   - 施工中两者内容有重合倾向。按 PLAN §关键设计 §6 的分工定下的规矩：根 README 简写门面、TEST README 详写验收与排错；未来出现口径分叉以 TEST README 为真。施工中两份文档都引用同一套 Step 1-4 主脊，未来维护时只要盯住这个结构即可避免飘移。

## 测试记录

### 构建（Release）

命令：

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c Release --nologo
dotnet build src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj -c Release --nologo
```

结果：

- Server：退出码 `0`，`Build succeeded. 0 Warning(s) 0 Error(s) Time Elapsed 00:00:05.09`
- Bridge：退出码 `0`，`Build succeeded. 0 Warning(s) 0 Error(s) Time Elapsed 00:00:00.93`

产物验证：

```text
src/MCP_Rhino.Server/bin/Release/net8.0/
  - MCP_Rhino.Server.dll
  - MCP_Rhino.Server.rhp      ← CopyRhinoPluginAssembly target 产出

src/MCP_Rhino.Bridge/bin/Release/net8.0/
  - MCP_Rhino.Bridge.dll
  - MCP_Rhino.Bridge.exe
```

### 样例 JSON 合法性

命令：

```bash
node -e "JSON.parse(require('fs').readFileSync('Project_Test/260422_TEST_mcp-client-integration/samples/claude_desktop_config.json','utf8'))"
node -e "JSON.parse(require('fs').readFileSync('Project_Test/260422_TEST_mcp-client-integration/samples/claude_code.mcp.json','utf8'))"
```

结果：两份样例均 `OK`，解析无异常。

### 端到端手动验收

本期手动验收清单共 7 条，见 `Project_Test/260422_TEST_mcp-client-integration/README.md` §端到端手动验收清单。

当前执行环境是普通 shell，不持有可交互 Rhino Plugin live session，因此：

- 第 1 条（Release 构建）：✅ 已执行，全部通过。
- 第 2-7 条（加载 .rhp / `_McpGeometryAnalysisSmoke` / Client 连接 / `tools/list` / `GetObjectMetricsInLive` / `CreateLines` + Undo）：**代码层面具备执行条件**，产物已就绪，但需要用户在真实 Rhino 8 会话中按 README 步骤复核。未复核条目列在「当前遗留项」。

### 扩展性断言

PLAN §验收标准 "扩展性断言" 条：本期产物声明"后续任意新增 `[McpServerToolType]+[McpServerTool]` Tool 不改 samples / README 即可被客户端看到"。

本期无法通过运行态证明（要等下一期新能力落地时才能回归），但可通过静态机制论证：

- `src/MCP_Rhino.Server/Server/ToolRegistration.cs:10` 通过 `WithToolsFromAssembly` 反射枚举，对新 Tool 零配置。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs:102-105` 的 `.AddMcpServer().AddRhinoTools().WithStreamServerTransport(...)` 链对 Tool 集合无感。
- 客户端通过 MCP 协议的 `tools/list` 动态拿清单，Bridge 只搬字节。

故扩展性断言成立；下一期任一 `*InLiveTool` 落地时即可作为回归验证点。

## 验收判据对齐

- `dotnet build`（Server + Bridge）：✅ 已通过。
- `samples/*.json` 可被 JSON parser 解析：✅ 已通过（node `JSON.parse` 零异常）。
- `Project_Test/260422_TEST_mcp-client-integration/README.md` 引用 3 份 samples：✅ 存在并引用。
- 根 `README.md` 存在；"使用步骤"与 TEST README 在口径上一致：✅ 两文档共享 Step 1-4 主脊，根 README 简写、TEST README 详写。
- 端到端手动验收清单（7 条）：🟡 第 1 条通过；第 2-7 条待 Rhino 会话复核。
- 扩展性断言：🟢 静态论证成立；需等下一期能力落地做运行态回归。

## 回退验证

- 本期零生产代码改动。全部产物均为 `Project_Plan/` / `Project_Exet/` / `Project_Test/` / `README.md` 下新增文件。
- 回退方式：`git revert` 本期 commit 即可——不影响任何既有 Tool / Skill / Agent / Live adapter 行为。
- 客户端侧回退：从客户端 MCP 配置里删除 `mcp-rhino` 条目；.rhp 可通过 Rhino `_PlugInManager` Uninstall。
- 验证：回退后再跑 `_McpGeometryAnalysisSmoke` 等既有 smoke，结果应与本期执行前一致（本期未改 smoke 行为）。

## 当前遗留项

1. **真实 Rhino 8 会话内端到端验收**（需要用户手动执行）：
   - 加载 `MCP_Rhino.Server.rhp`，确认命令行 `Named pipe ready: \\.\pipe\mcp_rhino`。
   - Open + Save `test-files/MCP_rhino_test.3dm`。
   - 运行 `_McpGeometryAnalysisSmoke`，确认全部检查点通过。
   - 把 `samples/claude_desktop_config.json` 或 `samples/claude_code.mcp.json` 合并到客户端配置，重启客户端。
   - 在客户端 prompt：`tools/list` 条目数 ≥ 当前 `Tools/**` 中 `[McpServerToolType]` 标注类数量。
   - 调 `GetObjectMetricsInLive` 返回非 null 度量。
   - 调 `CreateLines` 写入 + `Ctrl+Z` 回退。

2. **样例在不同版本 MCP Client 上的兼容性未全量验证**：
   - 本期样例针对 Claude Desktop + Claude Code 两种格式，Cursor / VS Code MCP 扩展 / Cline / Continue 的字段差异未穷举。
   - README 已声明"samples 原地重录"的策略；后续踩坑时原地补样例即可。

3. **扩展性断言的运行态回归**：
   - 需等下一期新能力（例如 `260422_PLAN_file-import-export-tools` / `260422_PLAN_geometry-advanced-edit-tools` / `260422_PLAN_interaction-command-tools`）落地后，在其 EXET 里引用本期 README 并确认未修改，作为运行态回归证明。

## 结论

本期 `mcp-client-integration` 已完成全部文档与配置样例产物沉淀：PLAN / EXET / TEST 三件套齐全，仓库根首次建立 README；两个 csproj Release 构建通过，`.rhp` 与 `Bridge.exe` 产物就位；两份 JSON 样例静态解析干净。端到端集成链路在代码层面是连通的（PLAN §背景已列证据），剩余工作仅为用户在真实 Rhino 8 会话里按 README 跑一遍 7 条验收清单。

本期不引入新 Live Smoke CLI slug，是对 `Project_Guides/MCP_Rhino Plan Log.md` §Live Smoke CLI 入口约定的显式偏差，已在 PLAN 与本 EXET 重复记录；偏差理由是"集成层零代码变更无 smoke 对象"。
