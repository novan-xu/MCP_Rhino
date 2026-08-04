# 260505_PLAN_active-doc-only-architecture

## 背景

`Project_Guides/MCP_Rhino Architecture.md` §执行模式指南 当前并列描述两种执行模式：

- **只读路径**：`Live First, Offline Allowed` —— 默认走 `RhinoDoc.ActiveDoc`，但允许能力提供
  显式 Offline 变体（经 `Rhino.FileIO.File3dm` 直接读 `.3dm`）。
- **写入路径**：`Live 强制` —— mutation 与 preview-of-mutation 一律走 RhinoCommon，绑定
  ActiveDoc。

实际代码里 offline 实现集中在 `src/MCP_Rhino.Server/Infrastructure/Rhino/`（注意：架构指南
§8 写的 `Rhino/Offline/` 子目录从未实际落地，offline 文件平铺在 `Rhino/` 一级），包括
`RhinoDocumentRepository`、`RhinoGeometryBuilder`（offline 版本）、`RhinoGeometryValidator`
（offline 版本）、`RhinoObjectEditValidator`（offline 版本）等。CLI 模式入口
（`DeveloperCommandHandler` 中 `find-layer-candidates` / `filter-objects-by-*` /
`get-document-user-strings` 等命令）依赖这一组 offline 服务读 `.3dm` 文件。

随着 `260505_PLAN_rhino-claude-code-panel`（per-doc panel）即将动工，这两种执行模式同时存在
开始变成实际负担：

1. **panel 绑定语义与 offline 工具天然冲突**。Panel-bound MCP server 通过
   `RhinoDoc.RuntimeSerialNumber` 强绑定到当前文档；但只要 `WithToolsFromAssembly` 反射
   注册的工具集合里有任何 offline 读写工具（其参数是任意 `.3dm` 文件路径），LLM 就有路径
   可以"绕开绑定，去读另一个 `.3dm`"。要么在 bound host 层加路径过滤层，要么彻底删除
   offline 工具表面。
2. **offline 没有现实价值**。本仓库的产品定位是"LLM 操作 Rhino 当前打开的文档"。批处理
   服务器、CLI offline 审计这两个 offline 仅有的真实场景，至今没有需求方；所有最近的能力
   plan（`geometry-analysis-tools`、`file-import-export-tools`、`geometry-edit-*`）默认按
   live-only 设计，offline 只是历史包袱。
3. **维护成本翻倍**。`IGeometryBuilder` vs `ILiveGeometryBuilder`、`IGeometryValidator` vs
   `ILiveGeometryValidator`、`IObjectEditSpecValidator` vs `ILiveObjectEditValidator` 这种
   "一个能力两套接口、两套实现"的模式，在每次新工具落地时都需要决定走哪条路径，反而成为
   认知负担。
4. **架构指南与代码已经偏离**。指南承诺的 `Rhino/Offline/` 子目录从未存在；`只读路径
   （Live First, Offline Allowed）` 段落里"若需要支持未打开文件、CLI 或批处理场景，可额外
   提供显式 Offline 变体"实际上没有任何能力沿用。指南规则与实际代码组织不一致已经超过半年。

本期能力把项目正式收敛为"active doc only"：删除全部 offline 读写功能（`File3dm`
直接读、offline 几何构造与校验、offline 用户字符串读写），保留 CLI 进程入口作为轻量 smoke
harness（用于验证工具是否注册、live 调用是否返回正确的 `LIVE_RHINO_REQUIRED`），同步把
架构指南改写为单一 live-only 模式。

本 plan 为 `260505_PLAN_rhino-claude-code-panel` 的**前置依赖**：panel plan 假定不存在
offline 工具表面，bound MCP host 因此可以原样暴露 `WithToolsFromAssembly` 的全量结果，无需
新增 tool 过滤层。

## 目标

1. **删除全部 offline 读写实现**（`File3dm.Read` 直接调用、offline `IGeometryBuilder` /
   `IGeometryValidator` / `IObjectEditSpecValidator` 实现、`RhinoDocumentRepository`），
   连同它们的接口与消费者中的 offline 分支。
2. **删除依赖 offline 的 CLI 命令**（`DeveloperCommandHandler` 主 switch 与 partial 中
   所有"读 `.3dm` 文件路径返回数据"的命令；live-only 的命令保留）。
3. **保留 CLI 进程入口作为轻量 smoke harness**：`Program.cs` + `NullLiveRhinoDocumentAccessor`
   + `AddCliFallbackLiveRhinoAdapters()` 留下；CLI 模式下所有 live tool 仍返回
   `LIVE_RHINO_REQUIRED`，用于"工具是否注册"的反射类 smoke 检查。
4. **把 `Project_Guides/MCP_Rhino Architecture.md` §执行模式指南 改写为单一 live-only
   模式**：合并"只读路径"与"写入路径"为统一的 Live 路径；删除"例外处理"子节里关于
   `--force-offline` 未来开关的承诺；§8 删除 `Rhino/Offline/` / `Rhino/Live/` 子目录拆分
   规则，改为"`Infrastructure/Rhino/` 平铺组织、所有适配器以 `Live*` 前缀命名"。
5. **保留 Filter evaluators**（`LayerFilterCriterionEvaluator` /
   `ObjectTypeFilterCriterionEvaluator` / `UserAttributeFilterCriterionEvaluator`）：它们
   作用于 `Domain/Models/RhinoObjectInfo` 这种 mode-agnostic 域对象，本身不读文件，可以
   继续被 live 路径复用。
6. **历史 PLAN / EXET / TEST 文档保持原状**：不回写已落地的能力 plan，只在本 plan 与本期
   EXET 中说明"自本期之后，新能力默认 live-only"。

**非目标（本期不做）**：

- 删除 CLI 进程入口本身（`Program.cs` 的 `Main`）—— 留作工具注册 smoke harness 与现有
  `_McpDevSmoke` 命令链路兜底。
- 删除 `NullLiveRhinoDocumentAccessor` —— 它不是 offline 实现，是 CLI 模式的 live 接口
  no-op 实现（返回 `LIVE_RHINO_REQUIRED`）。
- 重命名既有 live 服务（`LiveRhinoGeometryBuilder` 等）—— 命名约定本期不变。
- 拆分 `Infrastructure/Rhino/Live/` 目录（已有的子目录组织保留；本期只动 `Rhino/` 一级
  flat 文件）。

## 架构归属

本期主体是**删除**，不新增任何 `Tools/` `Skills/` `Agents/` `Application/Services/`
`Domain/` `Contracts/` 类。改动范围：

- **`Infrastructure/Rhino/`**：删除 offline 实现文件（详见 §涉及文件）。
- **`Application/Interfaces/`**：删除仅被 offline 实现的接口（`IRhinoDocumentRepository`、
  `IGeometryBuilder` 的 offline 变体接口若与 live 互斥则删除；详见 §涉及文件）。
- **`Application/Services/`**：每个 offline 消费者删除其 offline 路径分支；签名保留，
  内部逻辑收敛到 live。
- **`Server/DependencyInjection.cs`**：删除 `AddOfflineRhinoAdapters()` 方法；
  `AddCliFallbackLiveRhinoAdapters()` 减肥（去掉对 offline 接口的注册），保留 live 接口的
  no-op 注册。
- **`Infrastructure/CLI/DeveloperCommandHandler.cs`**：删除所有 offline-only 命令分支
  （主 switch 中的 `find-layer-candidates`、`filter-objects-by-*`、`get-object-user-strings`、
  `get-document-user-strings`、`set-document-user-strings`、`delete-document-user-strings`
  以及对应的 `Handle*` 方法）；保留 live 类 smoke 命令（`geometry-smoke-test`、
  `online-mutation-refactor-smoke-test`、`layer-management-smoke-test`、`layer-behavior-probe`）。
  考虑到这些保留命令也曾依赖 offline 路径，本期需逐一审核它们的实现是否走 offline；若走，
  改为返回 `LIVE_RHINO_REQUIRED`（CLI 模式）或保留 live 调用（plugin 模式）。
- **`Project_Guides/MCP_Rhino Architecture.md`**：§执行模式指南 大改；§8 略调；§命名指南
  无变化；§演进指南 无变化。
- **既有 PLAN / EXET 文档**：不回写。本 plan 完成时，已合并能力的描述会与新指南有少量
  历史性偏差（例如老 plan 的 §架构归属 写着"offline path"），不修复，但在本 plan §历史
  能力对齐 章节登记。

**Live Smoke CLI 入口**：本期偏离 `Project_Guides/MCP_Rhino Plan Log.md` §Live Smoke CLI
入口约定，**不**注册新 slug。理由：本期是删除性变更，没有新 code path 可 smoke；既有
能力的 `_Mcp<Feature>Smoke` 命令在本期完成后应当全部继续通过，作为"删 offline 不退化
live"的回归证明。该偏差与 `260422_PLAN_mcp-client-integration` 的偏差性质一致，已有
先例。

## 关键设计

### 1. "Offline" 的精确定义

本期"offline 功能"指**从磁盘 `.3dm` 文件读写、不依赖运行中的 RhinoDoc** 的代码路径，
具体识别特征：

- 调用 `Rhino.FileIO.File3dm.Read(...)` / `File3dm.Write(...)`
- 实现 / 消费 `IRhinoDocumentRepository`、`IGeometryBuilder`（非 Live 版）、
  `IGeometryValidator`（非 Live 版）、`IObjectEditSpecValidator`（非 Live 版）
- DI 入口：`AddOfflineRhinoAdapters()`

不被定义为"offline"的：

- `NullLiveRhinoDocumentAccessor`：CLI 模式下 `ILiveRhinoDocumentAccessor` 的 no-op，
  不读文件、不写文件，只返回错误。
- Filter evaluators（`Application/Services/Filters/*`）：纯 Domain 逻辑，作用于
  `RhinoObjectInfo` 内存对象。
- `PassThroughEditResultFormatter`：纯格式化逻辑。
- `CLI/DeveloperCommandHandler`：进程入口本身保留；只删除其中调用 offline 服务的
  command branch。

### 2. CLI 模式的去向

保留 `Program.cs` + CLI 模式以承担两个职责：

1. **Live 调用的 CLI fallback smoke**：在没有 Rhino 进程的情况下，`dotnet run --project
   src/MCP_Rhino.Server -- <smoke-slug> [args]` 仍可调用每个能力的 `Handle<Feature>SmokeTest`
   partial。CLI 模式下，由于 `ILiveRhinoDocumentAccessor` 解析为 `NullLiveRhinoDocumentAccessor`，
   所有 live tool 调用都返回 `LIVE_RHINO_REQUIRED`。这是"工具签名是否完整、DI 是否能解析"
   的轻量验证，已在多份历史 EXET 中体现价值。
2. **Plugin 模式的同入口分派**：Rhino plugin 通过 `McpRhinoPlugin.RunDeveloperCommand(args)`
   把 args 路由到同一份 `DeveloperCommandHandler`。Plugin 模式下 live 服务真实运行；CLI
   模式下 fallback 到 no-op。这一双模分派由各 partial 内部按 `McpRhinoPlugin.Instance`
   非空判断，本期不动。

### 3. Filter evaluator / Domain 模型保留

`LayerFilterCriterionEvaluator` 等 evaluator 类操作 `RhinoObjectInfo` 域模型，**不依赖**
任何文件 IO。它们当前注册在 `AddOfflineRhinoAdapters()` 里仅仅是历史巧合（被 offline
filter 服务消费）；本期把这三个 evaluator 注册迁移到 `AddRhinoApplication()`（即 live
注册同一个集合），保留 evaluator 文件本身不动。

### 4. DI 重构

`Server/DependencyInjection.cs` 改动：

```csharp
// 删除：
public static IServiceCollection AddOfflineRhinoAdapters(this IServiceCollection services) { ... }

// AddCliFallbackLiveRhinoAdapters：从重复的 Live 注册收敛为只注册 NullLive 与 Live 共有项。
// 调整后保留 ILiveRhinoDocumentAccessor → NullLiveRhinoDocumentAccessor，以及其他无副作用的
// live builder / validator / metrics calculator（这些类在 CLI 模式下被构造但永远拿不到 Rhino
// 句柄，所有方法在调用 ILiveRhinoDocumentAccessor 时被 NullLive 兜住）。

// AddRhinoApplication：吸收 3 个 IObjectFilterCriterionEvaluator 与 IEditResultFormatter
// 注册（它们与 live/offline 无关，是纯 Application 域服务）。
```

具体调整在 EXET 阶段定稿；PLAN 不写最终代码以保留 Execute 灵活度。

### 5. `DeveloperCommandHandler` 命令裁剪

按当前（读取时刻）`DeveloperCommandHandler.cs:113-148` 的主 switch 列出的命令：

| 命令 | 类型 | 处置 |
| --- | --- | --- |
| `find-layer-candidates` | offline read | **删除** |
| `filter-objects-by-layer` | offline filter | **删除** |
| `filter-objects-by-type` | offline filter | **删除** |
| `filter-objects-by-user-attributes` | offline filter | **删除** |
| `filter-objects-agent` | offline agent | **删除** |
| `preview-object-edits` | offline preview | **删除** |
| `apply-object-edits` | live mutation | **保留**（已强制 live） |
| `preview-object-user-text-writes` | offline preview | **删除** |
| `apply-object-user-text-writes` | live mutation | **保留** |
| `get-object-user-strings` | offline read | **删除** |
| `delete-object-user-text` | live mutation | **保留** |
| `get-document-user-strings` | offline read | **删除** |
| `set-document-user-strings` | live mutation | **保留** |
| `delete-document-user-strings` | live mutation | **保留** |
| `geometry-smoke-test` | dual-mode smoke | **保留** |
| `online-mutation-refactor-smoke-test` | live | **保留** |
| `layer-management-smoke-test` | live | **保留** |
| `layer-behavior-probe` | live probe | **保留** |

具体是否走 offline，需要在 EXET 阶段逐个核对各 service 的实现路径；表中标注按当前最近 plan
的设计意图填写。任何意外发现"声明 live 实际却走 offline"的命令，按 live 强制执行（plugin
模式）或 `LIVE_RHINO_REQUIRED`（CLI 模式）落地。

构造函数依赖列表（`DeveloperCommandHandler.cs:62-86`）随之裁剪：删除不再被任何保留命令
使用的服务字段。`RhinoObjectFilterService`、`RhinoDocumentUserStringService`（仅 read 部分）、
`LayerObjectFilterSkill` 等若被裁剪后仍有 mutation 路径需要调用，则保留；纯 read-only 路径
被裁剪后字段清理。

### 6. Architecture 指南改写

`Project_Guides/MCP_Rhino Architecture.md` §执行模式指南 改写后的结构（要点级别，最终
markdown 在 EXET 阶段定稿）：

```
## 执行模式指南（Live Only）

Rhino 侧的能力只跑一种模式：经 RhinoCommon 操作运行中的 `RhinoDoc`。本仓库不再支持
offline `File3dm` 读写。

### 通用契约
- 所有能力必须由 Rhino Plugin 宿主加载或经 panel-bound MCP server（详见
  `260505_PLAN_rhino-claude-code-panel`）驱动。
- 目标 `RhinoDoc`：
  - 全局 pipe 路径：解析为 `RhinoDoc.ActiveDoc`，必须已 Save (`doc.Path` 非空)，
    且 `doc.Path` 与请求 `FilePath` 一致；否则分别返回
    `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`。
  - Panel-bound 路径：解析为 `RhinoDoc.FromRuntimeSerialNumber(boundSerial)`，必须存在、
    必须已 Save，请求 `FilePath` 必须等于绑定 doc 的 `Path`；否则分别返回
    `DOCUMENT_CLOSED` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`。
- 主线程封送：所有 RhinoCommon 调用经 `ILiveRhinoDocumentAccessor` 走
  `RhinoApp.InvokeOnUiThread`；> 10s 报 `RHINO_MAIN_THREAD_BUSY`。
- Mutation 必须 `BeginUndoRecord` / `EndUndoRecord` 包裹，`CancelUndoRecord` 用于无变更
  / 提前失败。

### 读 / Preview-of-read
- 走 Live 路径，目标 doc 不可写、不开 Undo record。
- 不允许走任何 offline / `File3dm` 兜底；调用方必须在 Rhino 进程内。

### 写 / Mutation / Preview-of-mutation
- 走 Live 路径，使用 RhinoCommon API 修改文档；自动进入 Undo 栈。
- 不允许 `File3dm.Write` 落盘。

### CLI 进程入口
- `Program.cs` + `DeveloperCommandHandler` 的 CLI 模式只承担工具注册 smoke 与 live 调用
  fallback：CLI 模式下 `ILiveRhinoDocumentAccessor` 解析为 `NullLiveRhinoDocumentAccessor`，
  所有 live tool 返回 `LIVE_RHINO_REQUIRED`；用作"DI 是否完整、partial 是否注册"的轻量验证。
- CLI 模式不承担任何业务功能。

### 去归档化（保留）
- `IFileMutationSafeguard` / Archive snapshot / preflight warning 已下线；本期不变。
```

§8 修改：

- 删除 `Rhino 适配子目录按执行模式拆分` 整个 bullet（包含 `Rhino/Offline/`、`Rhino/Live/`
  两段）。
- 改为："`Infrastructure/Rhino/` 平铺组织：所有 RhinoCommon 适配器以 `Live*` 前缀命名
  （历史保留的子目录 `Rhino/Live/` 不强制扁平化，可继续作为分组使用）。"

§命名指南、§演进指南、§计划 / 执行 / 测试产物沉淀：无变化。

### 7. 历史能力对齐

本期完成后，下列已合并能力的 PLAN / EXET 仍包含 offline 描述，**不回写**，但记录在此供
后续读者参照：

- `260415_PLAN_object-user-text-batch-write` —— offline preview 描述将与代码不一致。
- `260415_PLAN_rhino-object-filter-agent` —— `filter-objects-agent` CLI 命令本期被删除。
- `260420_PLAN_geometry-create-modify-tools` —— 早期的 offline `IGeometryBuilder` 引用
  失效。
- `260420_PLAN_program-cli-refactor` —— CLI 路径裁剪后仍部分有效。
- `260421_PLAN_layer-management-tools` —— layer mutation 已 live 强制；read CLI 命令
  随本期删除。

新能力默认按 live-only 设计；所有未来 PLAN 的 §架构归属 不再使用"offline" 一词。

## 涉及文件

**删除**：

- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoDocumentRepository.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoGeometryBuilder.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoGeometryValidator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/RhinoObjectEditValidator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IRhinoDocumentRepository.cs`（若存在该独立文件）
- 任何 offline-only 的 `IGeometryBuilder` / `IGeometryValidator` / `IObjectEditSpecValidator`
  接口文件（这些接口若有 Live 等价物则保留 live 接口、删除非 live 接口；EXET 阶段逐一确认）

**修改**：

- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`：
  - 删除 `AddOfflineRhinoAdapters()` 方法。
  - 缩减 `AddCliFallbackLiveRhinoAdapters()`（去掉 offline 接口注册）。
  - 在 `AddRhinoApplication()` 内吸收 3 个 `IObjectFilterCriterionEvaluator` 与
    `IEditResultFormatter` 注册。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs`：`ConfigurePluginServices`
  调用链删除 `AddOfflineRhinoAdapters()`。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/ServerBootstrap.cs`（若引用 offline DI）：
  同步调整。
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`：
  - 主 switch 删除 §关键设计 §5 表中标"删除"的 6 条命令。
  - 删除对应 `Handle*` 私有方法。
  - 构造函数依赖列表与字段：删除仅被裁剪命令使用的服务。
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.Parsing.cs`（若存在）：
  删除被裁剪命令所需的 parser helper（`ParseGuidCsv`、`ParseUserAttributeConditions` 等
  若不再被其他保留命令使用）。
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectFilterService.cs`、
  `RhinoObjectEditingService.cs`、`RhinoObjectUserTextService.cs`、
  `RhinoDocumentUserStringService.cs`、`RhinoLayerManagementService.cs`：
  - 删除每个 service 内部的 offline 分支（`File3dm` / `IRhinoDocumentRepository` 调用）。
  - 每个 service 收敛到 live-only 路径；签名保留以避免广面 ripple。
- `src/MCP_Rhino.Server/Skills/**/*.cs`（按需）：删除 offline 调用。
- `src/MCP_Rhino.Server/Agents/**/*.cs`（按需）：删除 offline 调用。
- `Project_Guides/MCP_Rhino Architecture.md`：§执行模式指南整段重写、§8 修订，详见
  §关键设计 §6。

**保留（不改）**：

- `src/MCP_Rhino.Server/Infrastructure/Rhino/NullLiveRhinoDocumentAccessor.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/PassThroughEditResultFormatter.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/**/*`（全部）
- `src/MCP_Rhino.Server/Application/Services/Filters/*.cs`（3 个 evaluator）
- `src/MCP_Rhino.Server/Domain/**/*`
- `src/MCP_Rhino.Server/Tools/**/*`、`Skills/**/*`、`Agents/**/*`、`Contracts/**/*`、
  `Prompts/**/*` —— 这些层不直接持有 offline 实现。
- `Project_Plan/`、`Project_Exet/`、`Project_Test/` 既有内容（历史不回写）。
- `MCP_Rhino.Bridge` 整个项目。
- `Project_Guides/MCP_Rhino Plan Log.md`（命名 / 沉淀规则不变）。

**新增**：无。

## 使用方式

本期是删除性能力，对终端用户的体验是"少了一些原本就罕用的 CLI 入口"。

**新能力开发者的视角**：

- 新增 Tool / Skill / Agent 时只考虑 Live 路径，不再写 offline 变体。
- 新增能力的 PLAN §架构归属 不再讨论"offline alternative"。
- 新增能力的 smoke 在 CLI 模式仅断言 `LIVE_RHINO_REQUIRED`（既有约定不变）。

**既有用户**：

- Claude Desktop / Cursor / Cline 等外部 MCP Client 经 `MCP_Rhino.Bridge.exe` 调用 live
  工具：**不变**。所有当前可用工具继续可用。
- `dotnet run --project src/MCP_Rhino.Server -- find-layer-candidates ...` 等 CLI 命令：
  **失效**，命令名将被识别为未知命令（`TryHandle` 返回 `false`）。
- 既有 `_Mcp<Feature>Smoke` Rhino 命令：**不变**，每条 smoke 在 plugin 模式继续 live
  跑通。

## 验收标准

### 构建与静态

- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj -c Release --nologo` 退出码 0、
  零 warning（`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` 已生效）。
- `dotnet build src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj -c Release --nologo` 退出码 0、
  零 warning。
- 仓库内 grep `File3dm.Read` / `File3dm.Write` / `IRhinoDocumentRepository` / `RhinoDocumentRepository`
  匹配数为 0（除架构指南历史段落引用外）。

### 既有 live smoke 回归

每条已落地能力的 `_Mcp<Feature>Smoke` 命令在 Rhino 8 + `Runtime_Test/MCP_rhino_test.3dm`
环境下跑通：

- `_McpGeometryAnalysisSmoke`
- `_McpFileImportExportSmoke`
- `_McpGeometryEditMolecularFoundationSmoke`
- `_McpGeometryEditCurveCompositeSmoke`
- `_McpGeometryEditDerivedRoutingSmoke`
- `_McpGeometryEditSurfaceCompositeSmoke`
- `_McpSurfacePointOrderRebuildSmoke`
- `_McpLayerBehaviorProbe`
- 任何其他 `Project_Test/<...>_TEST_<...>/` 目录已注册的 smoke

每条 smoke 通过的判据沿用各自 EXET 文档定义的检查点；本期不重新发明判据，只断言"删除
offline 没有破坏 live"。

### CLI 模式回归

`dotnet run --project src/MCP_Rhino.Server -- geometry-smoke-test Runtime_Test/MCP_rhino_test.3dm`
等 live-类 smoke 在 CLI 模式仍可调用，全部 live tool 调用返回 `LIVE_RHINO_REQUIRED`，进程
退出码 0（与今天行为一致）。

### 删除的命令验证

`dotnet run --project src/MCP_Rhino.Server -- find-layer-candidates Runtime_Test/MCP_rhino_test.3dm Wall`
返回非 0 退出码或打印"未知命令"信息（具体行为按 `Program.cs` 的 default fallback；EXET
阶段确认）。

### 架构指南验证

- `Project_Guides/MCP_Rhino Architecture.md` §执行模式指南 内容与新结构一致（grep
  `Offline Allowed` 不命中、grep `File3dm.Read` 不命中、grep `--force-offline` 不命中）。
- §8 不再描述 `Rhino/Offline/` 子目录。
- §命名指南、§演进指南内容与本期改动无冲突（不需要更新）。

### 文档对齐

- 本 plan 的 EXET 文档落盘（`Project_Exet/260505_EXET_active-doc-only-architecture.md`）。
- 仓库根 `README.md` 的"快速开始"步骤与新指南一致：不出现"CLI 模式可读 .3dm 文件"的承诺
  （README 现状已经是 live-only 取向，本期只需要复核）。
- `260422_PLAN_mcp-client-integration` 的 README 与 samples 不需修改（它们本来就只描述 live
  路径）。

## 风险与回退方案

### 风险

1. **隐蔽的 offline 调用未被发现**：项目里可能有遗漏的 `File3dm` 调用或 offline 路径。
   缓解：EXET 阶段在删除完成后做一次全仓 grep（`File3dm`、`Rhino.FileIO`、`offline`
   字面量），任何残留作为遗留项登记。
2. **既有 live smoke 隐含依赖 offline 行为**：某些 smoke 在 CLI 模式可能曾依赖 offline
   返回真数据；删除后失败。
   缓解：所有 CLI 模式 smoke 的预期都应当是"返回 `LIVE_RHINO_REQUIRED`"（既有约定）；
   任何依赖真实数据的 smoke 应当只在 plugin 模式跑。EXET 阶段逐条核对。
3. **历史 PLAN 阅读体验下降**：老 PLAN 描述的 offline path 在代码里已不存在。
   缓解：本 plan §历史能力对齐 章节明确登记；新读者通过 plan 顺序（按 YYMMDD）能看到本 plan
   是分水岭。
4. **架构指南改写遗漏 / 引入新不一致**：指南改写的工作量不小，容易漏改。
   缓解：EXET 阶段做指南 vs 代码的 cross-check，至少 5 条断言（关键词覆盖）。
5. **CLI 模式 fallback 实际不可用**：`AddCliFallbackLiveRhinoAdapters` 减肥后可能不再
   注册某些必需依赖。
   缓解：保留任何被 live 服务构造函数依赖的接口；EXET 阶段以构造 `DeveloperCommandHandler`
   为最小验证（CLI 模式启动不抛异常）。
6. **Filter evaluator 的 DI 迁移破坏 live 注册**：把 evaluator 注册从 offline 集合搬到
   `AddRhinoApplication()` 时如果顺序错误，可能导致 live 模式注册重复或缺失。
   缓解：DI 注册顺序敏感；EXET 写一行 smoke 启动一个完整的 ServiceProvider 验证 evaluator
   能正常解析。

### 回退方案

- **整能力回退**：`git revert` 本期 commit。回退后所有 offline 文件恢复，CLI 命令恢复。
  风险：若已经有新能力 PLAN 在本期之上立项（依赖 active-doc-only 假设），需要同步回退
  那些 PLAN（最早受影响：`260505_PLAN_rhino-claude-code-panel`）。
- **部分回退（仅恢复 CLI 命令）**：单独 `git revert` `DeveloperCommandHandler.cs` 的
  改动。需要同步恢复其依赖的 service 字段。
- **架构指南独立回退**：从 git 还原 `Project_Guides/MCP_Rhino Architecture.md` 单文件，
  代码侧改动保留——会出现"指南承诺 offline 但代码已删除"的不一致，仅作为应急策略，不
  推荐。

## 后续扩展方向

- **未来若重新引入 offline**：必须以独立 PLAN 形式，命名建议 `re-introduce-offline-batch-mode`，
  并在 §背景 明确论述用例（批处理服务器、CI 几何校验等）。新 offline 路径与本期 live 路径
  必须严格隔离（独立 namespace、独立接口前缀），避免再次混淆。
- **CLI 模式精简**：未来可考虑把 `Program.cs` 的 CLI 模式压缩为只支持 smoke 命令；目前
  保留以兼容已有 `DeveloperCommandHandler` 习惯，但不强制。
- **自动化的"单一执行模式"约束**：在 CI / build 增加 grep 类校验，禁止新代码再次引入
  `File3dm.Read` / `File3dm.Write`；当前靠 PLAN review 兜住，未来可工具化。
- **`Infrastructure/Rhino/` 平铺扁平化**：若想统一目录组织（删除 `Rhino/Live/` 子目录、
  把所有 live adapter 直接放在 `Rhino/`），独立 PLAN 处理。本期不做。
- **Filter evaluator 抽象上提**：3 个 evaluator 是 mode-agnostic 的纯域逻辑，未来可考虑
  搬到 `Application/Filters/`（与 service 平级），让 `Infrastructure/` 完全只承担 RhinoCommon
  适配。本期不做。
