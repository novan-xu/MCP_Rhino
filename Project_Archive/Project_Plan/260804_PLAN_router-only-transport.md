# Router-only transport plan

## 背景

2026-07-30 落地的多文档 Router 已能发现每个 Rhino 8 进程发布的已保存文档、按 opaque session id 选择精确文档，并把完整 MCP tool/resource surface 代理到绑定的 route endpoint。仓库仍同时保留 fixed developer debug pipe、`MCP_Rhino.Bridge`、`_Mcpchat`、Companion 和 panel-bound pipe，导致默认配置与实际推荐路径不一致，也保留了 active-document、single-owner pipe 和多套生命周期语义。

用户已明确要求放弃 Router 之外的连接方式，并把 Router 设为项目默认连接方式。

## 目标

- Router 成为唯一受支持、唯一构建、唯一安装、唯一文档化的 MCP 客户端连接方式。
- Debug 与 Release 插件都在启动时发布 per-document route endpoints，不再有 build-mode transport 分叉。
- 删除 fixed `mcp_rhino` debug pipe、Bridge、Companion、`_Mcpchat` 和 Rhino-hosted panel-bound execution 的运行入口。
- 仓库 `.mcp.json` 和安装器生成的客户端配置都直接启动已安装的 `MCP_Rhino.Router.exe`。
- 保持 Router 的 per-session selection、attestation、current-user pipe 与不重放 mutation 的现有安全契约。

## 架构归属

- `MCP_Rhino.Server/Infrastructure/Plugin`：只负责 route endpoint 生命周期，不再拥有其他 MCP transport/UI session。
- `MCP_Rhino.Transport`：继续拥有 discovery、descriptor、attestation 和 route protocol。
- `MCP_Rhino.Router`：继续作为唯一 stdio MCP gateway。
- `Packaging/MCP_Rhino`：只发布 Server plugin、Transport 依赖与 Router executable。
- `Runtime_Workflow/` 与 `Project_Guides/`：把 Router-only 约束设为正式规则。

## 关键设计

1. 插件统一 `PlugInLoadTime.AtStartup`；Debug/Release 都启动 `RoutedDocumentEndpointDispatcher`。
2. `ServerBootstrap` 只维护 routed pipe registrations；删除 global/bound pipe dictionaries 与 host factories。
3. `McpPipeNames` 只保留 routed document pipe 生成。
4. solution 与 package build 移除 Bridge/Companion projects；旧项目源码和 panel/companion server code 从活动代码中删除。
5. 移除依赖旧连接语义的 Rhino commands、CLI smoke hooks、runtime prompt bundle 与测试编译项；历史 PLAN/EXET/TEST 文档保持只读。
6. 新增 Router-only 静态 smoke，防止默认配置、solution、packaging、plugin startup 或活动文档重新引入旧路径。

## 涉及文件

- `.mcp.json`
- `MCP_Rhino.sln`
- `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`
- `src/MCP_Rhino.Server/Infrastructure/Plugin/**`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/**`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Program.cs`
- `src/MCP_Rhino.Bridge/**`（删除）
- `src/MCP_Rhino.Companion/**`（删除）
- `Packaging/MCP_Rhino/**`
- `README.md`
- `Runtime_Workflow/MCP_Rhino Workflow.md`
- `Project_Guides/MCP_Rhino Architecture.md`
- `Project_Test/260804_TEST_router-only-transport/**`

## 使用方式

1. Rhino 8 启动并加载 packaged `MCP_Rhino.Server.rhp`。
2. 每个已保存打开文档发布一个 current-user route endpoint 和 registry descriptor。
3. MCP client 启动 `%LOCALAPPDATA%\\MCP_Rhino\\bin\\MCP_Rhino.Router.exe`。
4. client 调用 `rhino_router_list_documents`，再按 session id 调用 `rhino_router_select_document`，随后调用 Rhino tools。

## 验收标准

- `.mcp.json`、generic client config 与 installer-owned entry 都使用 Router。
- solution/package 不再构建或安装 Bridge/Companion。
- 活动插件代码不再启动 fixed debug pipe、panel-bound pipe、Companion 或 `_Mcpchat`。
- Debug 与 Release solution build 均通过。
- Router protocol smoke 通过。
- Router-only static smoke 通过，并禁止活动代码/文档重新出现旧连接入口。
- MCP tool inventory/safety smoke 在 Debug 与 Release 均保持通过。

## 风险与回退方案

- 风险：依赖 `_Mcpchat` 或 fixed pipe 的旧开发流程将立即不可用。
- 风险：旧历史 smoke 源码可能引用已删除类型；活动构建必须停止编译这些已退役 smoke，同时保留历史文档与源码记录的可追溯性说明。
- 风险：Debug 插件改为 startup load 后，开发机可能同时存在旧插件副本；安装器现有 single-install ownership 检查继续负责阻止冲突。
- 回退：通过 Git 恢复本计划涉及的删除和修改；重新引入旧路径前必须新建 PLAN 并重新评审多连接语义。

## 后续扩展方向

- 为 Codex/Claude/其他客户端生成 Router-only config health check。
- 增加真实 Rhino 多进程 live Router regression，但只能使用 disposable `.3dm` fixtures。
- 将过期连接项目和 smoke 在满足归档策略后移出活动树。

## Revision 2026-08-04: registered command and legacy-path retirement

The user extended the Router-only requirement to include the Rhino command registry and every installer-owned legacy path.

- Remove the Rhino command classes that expose chat, Companion, panel, Bridge, or ClaudeCode entrypoints.
- Audit the compiled `.rhp`, not only source names, for the retired command and transport symbols.
- Mark both package and installed ownership manifests with `transportMode: router-only`.
- During an upgrade from a pre-Router-only installation, retain the old files only until the replacement Router validates, then destroy the full legacy rollback tree.
- Remove installer-generated staged bundles whose package metadata predates the Router-only contract.
- Prove the cleanup with a simulated legacy installation and a real bundle install/validate cycle under isolated test roots.
- Do not terminate a live Rhino session. A loaded command table can only be refreshed after Rhino closes and restarts with the replacement plugin.

Acceptance additionally requires that no retired command source or compiled symbol exists, and that a simulated legacy upgrade leaves no Bridge/Companion executable, legacy rollback, or legacy staged bundle.
