# Router-only transport execution

## 对应计划

- 计划：[Project_Plan/260804_PLAN_router-only-transport.md](../Project_Plan/260804_PLAN_router-only-transport.md)
- 执行日期：2026-08-04

## 关联产物

- 测试目录：[Project_Test/260804_TEST_router-only-transport/](../Project_Test/260804_TEST_router-only-transport/)
- 独立静态审计：`RouterOnlyTransportSmoke.ps1`
- Bundle 审计：`AssertRouterOnlyBundle.ps1`
- 复用的 Router protocol smoke：`Project_Test/260729_TEST_multi-document-rhino-router/RouterProtocolSmoke/`
- Commit / PR：未创建

## 执行结果 / 实际落地范围

- `MCP_Rhino.Router.exe` 已成为唯一受支持的 MCP client transport。
- 本地忽略配置 `.mcp.json` 与 `.codex/config.toml` 已切换到已安装 Router 的绝对路径。
- tracked generic client config 与 installer-owned config 原本已使用 Router，并新增静态回归断言。
- solution 删除 `MCP_Rhino.Bridge` 与 `MCP_Rhino.Companion` projects。
- package build 只 publish Server 与 Router；生成 bundle 包含 Router 和 `.rhp`，不包含 Bridge/Companion executables。
- Debug/Release 插件统一为 `PlugInLoadTime.AtStartup`，只启动 `RoutedDocumentEndpointDispatcher`。
- `ServerBootstrap` 只维护 routed pipe registrations；fixed/global 与 panel-bound host lifecycle 已删除。
- 删除 Bridge、Companion、Panel、ClaudeCode transport/UI、bound accessor/factory、旧 transport Rhino commands 与 runtime prompt bundle，共 61 个 tracked source files。
- 精确排除 9 个已退役连接 capability 的历史 smoke 文件夹，避免已删除生产类型重新成为活动构建依赖；历史 PLAN/EXET/TEST 内容未修改。
- `Scripts/met_1d_takeoff_by_lot.py` 已改为启动 Router、发现并选择目标文档，再调用业务 tools。
- README、Runtime Workflow、Architecture、Plan Log 与 plugin manifest 已更新为 Router-only 契约。

## 与计划的偏差

- 没有安装新 bundle 到当前运行中的 Rhino。安装器要求 Rhino/Router 进程关闭；本次范围是仓库能力建设与默认配置收敛，不主动关闭用户 Rhino 进程。
- Package bundle 仅作为验证产物临时生成，审计完成后从 TEST 目录删除，避免提交 106 个生成文件；测试脚本可重复生成并复核。
- 旧 Bridge project 的 tracked source 与 project file 已删除；当前 Codex task 仍由启动时加载的旧 Bridge process 承载，因此其被锁定的本地 `src/MCP_Rhino.Bridge/bin` 缓存无法在本次 task 中删除。该缓存不属于 solution、package 或 Git 产物。

## 施工中发现并修复的问题

- 仓库 `.mcp.json` 仍指向 repository Bridge build，导致本 task 最初命中 fixed active-document path 并返回 `FILE_NOT_ACTIVE`；已改为 installed Router。
- `.codex/config.toml` 也保留 Bridge command；已同步切换 Router，并纳入静态 smoke。
- 一个未跟踪的 takeoff 脚本硬编码 Bridge 且没有 Router document selection；已改为 `list → exact path match → select`。
- 历史 smoke 默认会被 Server project 全量编译。删除旧 transport 类型后，这些 smoke 不再代表受支持能力，因此使用精确 folder excludes，并在 Plan Log 中增加“明确退役后才可精确排除”的规则。
- 首次测试 bundle 输出到非忽略目录，验证后已清理并把 README 命令改到已有 `artifacts/` ignore 规则覆盖的目录。

## 测试记录

### Router-only repository smoke

```powershell
powershell -ExecutionPolicy Bypass -File Project_Test\260804_TEST_router-only-transport\RouterOnlyTransportSmoke.ps1
```

- Exit code：0
- 关键结果：tracked/local client configs、solution、Server project、plugin、bootstrap、packaging、workflow 与 architecture 全部通过 Router-only 断言。

### Debug solution build

```powershell
dotnet build .\MCP_Rhino.sln -c Debug --nologo
```

- Exit code：0
- 0 warnings / 0 errors
- Server、Transport、Router、RouterProtocolSmoke 全部构建成功。

### Release solution build

```powershell
dotnet build .\MCP_Rhino.sln -c Release --nologo
```

- Exit code：0
- 0 warnings / 0 errors
- Server、Transport、Router、RouterProtocolSmoke 全部构建成功。

### Router protocol smoke

```powershell
dotnet run --project Project_Test\260729_TEST_multi-document-rhino-router\RouterProtocolSmoke\RouterProtocolSmoke.csproj -c Debug --no-build
```

- Exit code：0
- 验证三项 Router control tools、分页 surface、unique path routing、explicit selection、独立 Router selections、target conflict、duplicate path ambiguity、private attestation、stale/corrupt descriptor 与并发 clients。
- 终值：`[OK] multi-document-rhino-router-smoke-test`

### MCP safety smoke（Debug / Release）

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- mcp-tool-safety-annotations-smoke-test
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- mcp-tool-safety-annotations-smoke-test
```

- 两条命令 Exit code：0
- 两个 configuration 都验证 159 tools。
- 两个 configuration 都确认没有 bare method-level `[McpServerTool]`。

### Package build 与 bundle audit

```powershell
Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -OutputRoot Project_Test\260804_TEST_router-only-transport\artifacts
powershell -ExecutionPolicy Bypass -File Project_Test\260804_TEST_router-only-transport\AssertRouterOnlyBundle.ps1 -BundleRoot Project_Test\260804_TEST_router-only-transport\artifacts\MCP_Rhino-1.0.0
```

- 两条命令 Exit code：0
- Bundle file count：106
- `MCP_Rhino.Router.exe`：存在
- `MCP_Rhino.Server.rhp`：存在
- `MCP_Rhino.Bridge.exe`：不存在
- `MCP_Rhino.Companion.exe`：不存在
- Bundled Router `--validate-install`：通过
- 生成 bundle 已在验证后删除。

### Python syntax check

```powershell
python -m py_compile Scripts\met_1d_takeoff_by_lot.py
```

- Exit code：0

## 验收判据对齐

- Router 是默认且唯一 client connection：满足。
- Debug/Release plugin shape 一致：满足。
- 旧 transport 不再构建、安装、注册或启动：满足。
- Router discovery / selection / attestation / conflict 语义保持：满足。
- package 只交付 Router client executable：满足。
- 规范、工作流与 README 同步：满足。
- Debug/Release build 与 MCP safety smoke：满足。

## 回退验证

- 未执行破坏性回退命令。
- 所有 tracked 删除由 Git 保留，可通过单独的新 PLAN 恢复。
- 恢复旧 transport 会重新引入多套 document binding 与生命周期语义，不应作为小修复直接回滚。

## 当前遗留项

- 新 bundle 尚未安装到当前运行中的 Rhino；部署需要关闭 Rhino/Router 后运行 installer，并重新启动 Rhino。
- 当前 task 使用中的旧 Bridge binary cache 被 Windows 锁定；task 结束后可作为普通生成缓存清理，不影响 Router-only source/package。
- 未对生产 `.3dm` 执行 mutation；本次 live access 验证在施工前已通过 installed Router 对 `test.3dm` 成功执行 `GetLayers`。

## 结论

仓库已从多连接并存收敛为 Router-only architecture。Router 现在是默认配置、唯一 solution/package client、唯一 plugin endpoint lifecycle 与唯一受支持运行时工作流；旧 Bridge、Companion、`_Mcpchat`、panel-bound 和 fixed debug pipe 已退出活动实现。

## Follow-up hardening: registered commands and legacy paths

The Router-only delivery was extended on 2026-08-04 to retire the old Rhino command registry entries and installer-owned legacy copies.

### Implementation

- Added `transportMode: router-only` to the package definition, generated bundle manifest, and installed ownership manifest.
- The installer now rejects bundles or installed-state validation without the Router-only marker.
- After a pre-Router-only installation is replaced and the new Router passes `--validate-install`, the installer removes its complete legacy rollback tree.
- Installer-generated staged bundles without the Router-only marker are removed during that successful legacy upgrade.
- Added source-level assertions for the retired Rhino command files and command names.
- Added binary scanning of `MCP_Rhino.Server.rhp` for `Mcpchat`, the Bridge/panel/Companion smoke commands, `StartBoundPipeServer`, `TryShowChatPanel`, and Companion assembly symbols.
- Added an isolated installer-upgrade smoke that begins with synthetic Bridge/Companion executables, an old command-bearing `.rhp`, a legacy rollback, and a legacy staged bundle.

### Verification

```powershell
powershell -ExecutionPolicy Bypass -File Project_Test\260804_TEST_router-only-transport\RouterOnlyTransportSmoke.ps1
Packaging\MCP_Rhino\Build-McpRhinoPackage.ps1 -OutputRoot Project_Test\260804_TEST_router-only-transport\artifacts
powershell -ExecutionPolicy Bypass -File Project_Test\260804_TEST_router-only-transport\AssertRouterOnlyBundle.ps1 -BundleRoot Project_Test\260804_TEST_router-only-transport\artifacts\MCP_Rhino-1.0.0
powershell -ExecutionPolicy Bypass -File Project_Test\260804_TEST_router-only-transport\RouterOnlyInstallerUpgradeSmoke.ps1 -BundleRoot Project_Test\260804_TEST_router-only-transport\artifacts\MCP_Rhino-1.0.0
dotnet build MCP_Rhino.sln -c Debug --nologo
dotnet build MCP_Rhino.sln -c Release --nologo
```

- All commands exited 0.
- Debug and Release builds completed with 0 warnings and 0 errors.
- The compiled plugin contains none of the retired command/transport symbols.
- The simulated legacy upgrade leaves no Bridge/Companion executable path, legacy rollback, legacy staged bundle, or old Rhino command symbol; installed-state validation passes.
- Router protocol smoke and Debug/Release MCP safety smokes still pass; the inventory remains 159 tools.

### Live deployment boundary

The verified bundle was first staged at `C:\Users\nxu\AppData\Local\MCP_Rhino\staged\1.0.0-20260804152027893` while Rhino was running. After all Rhino processes had exited, that exact audited bundle was installed successfully. The installed manifest now reports `transportMode: router-only`; installed Bridge/Companion file count, legacy process count, legacy rollback count, legacy staged-bundle count, and retired plugin-symbol count are all zero. The task-start Bridge process was then stopped and its remaining repository build-cache directory was deleted. The next Rhino start will build its command table from the audited Router-only `.rhp`.
