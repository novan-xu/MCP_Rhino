# 260422_PLAN_interaction-command-tools

## 背景

MCP_Rhino 当前所有 Tool 都是"无人交互"模式：客户端给定 ObjectId / 坐标 / 文件路径，
Tool 直接执行。但很多 Rhino 工程场景要求 LLM "向用户取信号"或"借助 Rhino 既有命令兜底"：

- **拾取**：`Rhino.Input.Custom.GetPoint` / `GetObject` / `GetCurve` / `GetNumber` / `GetString` /
  `GetOption`，让用户在 Rhino viewport 里点一个位置 / 选一个对象 / 输入一个数值 / 选一个选项，
  作为后续 MCP 调用的输入。这是"LLM + 用户协同建模"最自然的交互形态。
- **命令兜底**：`Rhino.RhinoApp.RunScript(command, echo)`，让 LLM 直接执行任意 Rhino 命令脚本
  字符串。当 MCP_Rhino 还没暴露某个 native 命令的原子 Tool 时，RunScript 是最快的兜底通道；
  且 `_-Command -arg1 -arg2 _Enter` 这种 Rhino 命令行 syntax 对 LLM 来说 token 极省。

RhinoCommon API 已经把所需能力全部暴露：
- `Rhino.Input.Custom.GetPoint / GetObject / GetCurve / GetNumber / GetString / GetOption`
  全部继承自 `GetBaseClass`，提供 `Get()` 同步方法返回 `GetResult` 枚举。
- `Rhino.RhinoApp.RunScript(string command, bool echo)` / `RunScript(string command, int macroOptions, bool echo)`
  返回 `bool`。

但这两类能力有一个**与现有 MCP 架构正交的难点**：
- 它们都必须运行在 **Rhino UI 主线程**。
- 拾取（Get*）天然 **阻塞** —— `GetPoint.Get()` 会一直阻塞直到用户点 / 按 ESC / 按 Enter。
- MCP stdio host 请求跑在后台线程；按现有 `ILiveRhinoDocumentAccessor` 的契约
  （`InvokeOnUiThread` 同步封送 + `> 10s` 报 `RHINO_MAIN_THREAD_BUSY`），
  阻塞用户交互直接撞上"主线程长时间占用"硬约束。

因此本 Plan 必须**新设交互模型**：把"发起拾取请求"与"等待拾取结果"解耦成两步，避免单 Tool
长时间锁死主线程。

## 目标

提供两组能力：

- **A. 异步拾取（Picker，5 个 Tool + 2 个会话管理 Tool）**：把 GetPoint / GetObject / GetCurve /
  GetNumber / GetString 改造成"启动拾取 Session → 用户在 Rhino 里完成 → 客户端轮询 / 取消"模式，
  避免阻塞主线程。
  - `BeginPickPointTool`、`BeginPickObjectTool`、`BeginPickCurveTool`、`BeginPickNumberTool`、`BeginPickStringTool`
    —— 5 个发起 Tool。
  - `PollPickResultTool` —— 轮询/获取结果。
  - `CancelPickTool` —— 取消进行中的会话。
- **B. 命令兜底（Command，2 个 Tool）**：
  - `RunRhinoScriptTool`：执行 Rhino 命令字符串（一行或多行），可选 echo、可选 BeginUndoRecord 包裹。
  - `RunRhinoScriptBatchTool`：把多条命令字符串作为 entry 数组按顺序执行，可选共用一个 Undo record。

**非目标**（一期不做）：
- `GetMeshFace` / `GetSubObject` / `GetGrip` 这类细粒度子对象拾取 —— 用例稀疏，留给后续扩展。
- `Get*` 的多次循环 multi-pick（"连续拾取多个点直到用户按 Enter"）——一期只支持单次拾取；多个点
  需要由客户端串多次 Begin/Poll。
- 在拾取过程中实时显示"动态预览"几何（DynamicDraw）—— 增加复杂度，留给后续 Skill 扩展。
- `GetOption`：交互更复杂（需要预定义选项列表），用例大多可以用 `GetString` 兜住，本期不单独
  暴露。

## 架构归属

- **Tools/Interaction/**（新增子目录）—— 9 个 Tool：
  - `Picker/BeginPickPointTool.cs` / `BeginPickObjectTool.cs` / `BeginPickCurveTool.cs` /
    `BeginPickNumberTool.cs` / `BeginPickStringTool.cs`
  - `Picker/PollPickResultTool.cs`
  - `Picker/CancelPickTool.cs`
  - `Command/RunRhinoScriptTool.cs`
  - `Command/RunRhinoScriptBatchTool.cs`

- **Application/Services/** —— 新增 2 个 Service：
  - `RhinoInteractionPickerService`：承载 `BeginPick<Kind>(...)` / `PollPickResult(...)` /
    `CancelPick(...)` 全部方法；持有 session 状态注册表（详见关键设计 #1）。
  - `RhinoCommandExecutionService`：承载 `RunRhinoScript(...)` / `RunRhinoScriptBatch(...)`。

- **Application/Interfaces/** —— 新增：
  - `ILivePickerSessionDispatcher`：方法 `Begin(PickSpec spec)` / `Poll(Guid sessionId)` /
    `Cancel(Guid sessionId)`；内部把"启动 Get*"调度到 UI 主线程异步执行；不阻塞 MCP 后台线程。
  - `ILiveCommandRunner`：方法 `Run(string command, bool echo, bool useUndoRecord, string undoDescription)`
    / `RunBatch(IReadOnlyList<string> commands, bool echo, bool useUndoRecord, string undoDescription)`。
  - `IPickerSessionRegistry`：纯进程内 ConcurrentDictionary 包装，承担 sessionId → session 状态的
    存取与过期清理。Service 通过此接口操作；让 dispatcher 与 service 共享同一份 session 表，便于
    单元测试替换。

- **Infrastructure/Rhino/Live/** —— 新增：
  - `LiveRhinoPickerSessionDispatcher.cs`：所有 `Rhino.Input.Custom.GetPoint / GetObject / ...`
    调用集中在此。把 `Get()` 调度到 UI 主线程：通过 `RhinoApp.InvokeOnUiThread` 启动一个 task，
    把 `GetResult` + 输出几何写回 session 状态；MCP 后台线程立刻返回 `sessionId`，不等待用户。
  - `LiveRhinoCommandRunner.cs`：所有 `RhinoApp.RunScript` 调用集中在此。
- **Infrastructure/Interaction/** —— 新增：
  - `InMemoryPickerSessionRegistry.cs`：实现 `IPickerSessionRegistry`；用 `ConcurrentDictionary<Guid, PickerSessionState>`
    存放 session；后台线程定时清理 `Status == Completed/Cancelled/Errored && Age > 5min` 的条目。

- **Domain/Models/** —— 新增：
  - `PickSpec`：`Kind(Point|Object|Curve|Number|String) + Prompt + AllowReferenceObjects? +
    AllowPreSelected? + DefaultValue? + LowerBound? + UpperBound? + AllowedObjectTypes[]?`。
  - `PickerSessionState`：`SessionId / FilePath / Status(Pending|Completed|Cancelled|Errored|Expired) /
    StartedAt / CompletedAt? / Spec / Result?`。
  - `PickerResult`：`Kind + GetResultEnum + Point{XYZ}? + ObjectId? + CurvePoints[]? + Number? + Text? + Message?`。
- **Domain/Enums/** —— 新增：
  - `PickerKind`：Point / Object / Curve / Number / String。
  - `PickerStatus`：Pending / Completed / Cancelled / Errored / Expired。
  - `PickerGetResultMapping`：Success / Cancel / Nothing / NoResult / Timeout / Error
    （映射 RhinoCommon `Rhino.Input.GetResult` + 本服务的 Timeout / Expired）。
- **Contracts/Requests/** —— 新增：
  - `BeginPickPointRequest` / `BeginPickObjectRequest` / `BeginPickCurveRequest` /
    `BeginPickNumberRequest` / `BeginPickStringRequest`：`FilePath + Prompt? + 各 Kind 专属字段
    （AllowedObjectTypes / NumberLowerBound / NumberUpperBound / DefaultValue 等） + TimeoutSeconds?`。
  - `PollPickResultRequest`：`SessionId`。
  - `CancelPickRequest`：`SessionId`。
  - `RunRhinoScriptRequest`：`FilePath + Command + Echo? + UseUndoRecord? + UndoDescription?`。
  - `RunRhinoScriptBatchRequest`：`FilePath + Commands[] + Echo? + UseSharedUndoRecord? + UndoDescription?`。
- **Contracts/Responses/** —— 新增：
  - `PickerSessionResponse`：`SessionId / FilePath / Status / StartedAt / Prompt / Kind`。
  - `PickerResultResponse`：`SessionId / FilePath / Status / Kind / GetResult / Point? / ObjectId? /
    CurvePoints[]? / Number? / Text? / Message`。
  - `RunRhinoScriptResponse`：`FilePath / Command / Success / DurationMs / EchoOutput? /
    UndoRecordSerialNumber? / Warnings`。
  - `RunRhinoScriptBatchResponse`：`FilePath / TotalCount / SucceededCount / FailedCount /
    Results: IReadOnlyList<RunRhinoScriptResponse> / Warnings`。
- **Server/DependencyInjection.cs** —— 注册 3 个接口实现 + 2 个 Service。`ToolRegistration.cs`
  无需改动（反射自动发现）。
- **Skills / Agents** —— 一期不新增 Skill：交互 / 命令粒度自洽，没有"Picker + 自动跑 RunScript"
  的复合流程。后续浮现"先让用户拾起点 → 自动调 CreatePoints"这类工序时，再抽
  `InteractiveModelingSkill`。

## 关键设计

1. **Picker session 化（核心架构决策）**
   - 现状障碍：`GetPoint.Get()` 在 UI 主线程上阻塞 → MCP 后台线程经
     `ILiveRhinoDocumentAccessor.Execute(...)` 同步封送等待 → 触发 `RHINO_MAIN_THREAD_BUSY (>10s)`。
   - 方案：拆成 `Begin*` + `Poll` + `Cancel` 三步：
     1. `Begin*`：MCP 后台线程接收请求 → Service 创建 `sessionId` → 通过
        `RhinoApp.InvokeOnUiThread(...)` 在 UI 线程上**异步**启动 `Get*.Get()`（fire-and-forget，
        不等待返回）→ 立刻返回 `PickerSessionResponse{ Status: Pending, SessionId }`。
     2. UI 线程的 task 跑完后把 `GetResult + 输出几何` 写回 `IPickerSessionRegistry`，状态转
        `Completed / Cancelled / Errored / Expired`。
     3. `Poll`：MCP 后台线程查 registry → 返回当前状态；状态为 `Pending` 时 `Result=null`，客户端
        自行决定 sleep 后重试间隔。
     4. `Cancel`：MCP 后台线程查 registry → 通过暴露的 cancel handle 把 UI 线程上的
        `Get*` 请求 `Cancel()`（RhinoCommon 的 `GetBaseClass` 提供 `Get(false /* wait */)` /
        `EnableCancel` 等机制；具体实现走 `EscapeKeyDown` 触发 + 主动 `Get` 实例的 `Cancel()` 方法）。
   - **MCP 后台线程在任一阶段都不会被阻塞超过 100ms**：`Begin*` 仅同步注册 + 异步派发；`Poll`
     仅同步读取 registry；`Cancel` 仅同步发出取消信号；不主动等待用户。

2. **Picker 主线程占用模型**
   - UI 主线程被 `Get*.Get()` 占据期间：用户**仍可在 viewport 中正常拾取**（这正是 RhinoCommon
     `Get*` 的设计行为）；其他 MCP Live Tool 调用如果走 `ExecuteWithUndo` 会被 `Get*` 阻塞，
     直到拾取结束。
   - 因此 `RhinoInteractionPickerService` 在 `Begin*` 时检测：若 registry 中已有 `Pending` 会话
     且 `FilePath` 与本次请求相同 → 整 Request 失败 `PICKER_SESSION_ACTIVE`，避免重复启动两个
     `Get*`（RhinoCommon 不支持嵌套 Get）。
   - 不强行限制"全局只能有一个 session"：跨 file 的 Pending session 允许并存（虽然 RhinoCommon
     仍然只能服务一个 active doc，但本服务不主动跨 doc 调度）。

3. **Picker session 超时与过期**
   - `BeginPickRequest.TimeoutSeconds` 缺省 300 秒；上限 1800 秒；超出 → 硬错误。
   - UI 线程的 task 内部用 `CancellationTokenSource(TimeoutSeconds)` + 自定义循环检测：
     `Get*` 没有"原生 timeout 参数"，本服务通过 `EscapeKeyDown` 主动取消（RhinoCommon `Get*`
     默认监听 ESC）。超时后 session 状态转 `Expired`。
   - Registry 的清理后台 task 每 60 秒扫描，对 `Status != Pending && Age > 5min` 的 session 清理。

4. **Pick 输出几何映射**
   - `Point`：返回 `Point{X, Y, Z}`。
   - `Object`：返回 `ObjectId`，不返回几何（客户端再调 `Get*Tool` 取几何）。
   - `Curve`：返回拾取曲线的 `ObjectId` + 选项 `IncludeSamplePoints` 时附带 50 个等参样点
     `CurvePoints[]`，便于 LLM 直接做几何决策。
   - `Number`：返回 `Number`（double）。
   - `String`：返回 `Text`（string）。
   - `GetResult` 枚举映射：Success / Cancel / Nothing / NoResult；本服务额外定义 Timeout / Error。

5. **Pre-selected / Allow-reference 选项**
   - `BeginPickObjectRequest.AllowPreSelected=true` 时启用 `GetObject.EnablePreSelect(true, true)`，
     允许用户在调用前已选中的对象作为结果（默认 `false`，强制用户重新点）。
   - `BeginPickObjectRequest.AllowReferenceObjects=true` 时允许选择 worksession / linked block
     里的引用几何。

6. **AllowedObjectTypes 过滤**
   - `BeginPickObjectRequest.AllowedObjectTypes`（List<string>）映射 RhinoCommon `ObjectType`
     位运算：Point / Curve / Surface / Brep / Mesh / Annotation / Light / InstanceReference /
     PointCloud / Hatch / TextDot 等。
   - 字段缺省 = 全部允许。
   - 字符串与现有 `RhinoObjectType` 枚举对齐；不匹配的字符串 → 整 Request 失败。

7. **Number 边界 / String 默认值**
   - `BeginPickNumberRequest.LowerBound / UpperBound`：映射 `GetNumber.SetLowerLimit / SetUpperLimit`。
   - `BeginPickNumberRequest.DefaultValue`：映射 `GetNumber.SetDefaultNumber`。
   - `BeginPickStringRequest.DefaultValue`：映射 `GetString.SetDefaultString`。

8. **RunRhinoScript 单条命令**
   - Service 通过 `_documentAccessor.Execute(...)` （或 `ExecuteWithUndo`，按 `UseUndoRecord` 开关）
     拿到 ActiveDoc，把 `RhinoApp.RunScript(command, echo)` 调用调度到 UI 主线程同步执行。
   - **主线程占用**：`RunScript` 同步执行命令，可能耗时（如 `_Loft` / `_BooleanUnion`）。命令耗时
     > 10s 时由 `ILiveRhinoDocumentAccessor` 自动返回 `RHINO_MAIN_THREAD_BUSY`；本 Tool 不二次包装。
   - `UseUndoRecord=true`（默认）时通过 `ExecuteWithUndo` 包裹，命令产生的所有几何 / layer / user
     text 改动统一进入一个 Undo record；`UseUndoRecord=false` 时走 `Execute` —— 命令各自走 Rhino
     默认 Undo 行为（每个原子操作单独 Undo 条目）。
   - `Echo=false` 默认；`Echo=true` 时 RhinoApp 命令行回显（不影响返回结果）。

9. **RunRhinoScriptBatch 批量命令**
   - `Commands[]` 顺序执行；任一条命令 fail（`RhinoApp.RunScript` 返回 `false`）→ per-entry
     `Success=false`，**默认不阻断后续命令**（与现有批量 Tool 行为一致）。
   - `UseSharedUndoRecord=true`（默认）时整批包一个 Undo record；`false` 时每条命令独立 Undo 行为。
   - 若需要"任一命令失败立刻终止"，由客户端拆分多次调用实现，本期不引入 `StopOnFailure` 字段。

10. **RunRhinoScript 安全护栏**
    - `Command` 字符串前缀允许的命令必须以 `_` 开头（强制 Rhino 国际化命令名，避免 locale 漂移）；
      不以 `_` 开头 → warning（不阻断）。
    - **黑名单命令**：`_-NewFloatingViewport` / `_Open` / `_Save` / `_SaveAs` / `_Exit` / `_-Save` /
      `_-Open` —— 这些会改变 ActiveDoc / 退出 Rhino / 切换文档，与 MCP 一致性约定冲突；命中
      black list → 整 Request 失败 `COMMAND_BLACKLISTED`。
    - 用户可在 `MCP_Rhino` 配置（待引入）覆盖黑名单；本期硬编码默认黑名单。
    - **不做白名单**：command 字符串完全自由意味着 LLM 拥有 Rhino 全部能力的入口，安全性靠以下
      手段管控：黑名单 + Rhino Undo 兜底 + 用户随时 ESC 终止；白名单会限制 RunScript 的兜底价值。

11. **错误码沿用 + 新增**
    - 沿用：`LIVE_RHINO_REQUIRED` / `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE`
      / `RHINO_MAIN_THREAD_BUSY`。
    - 新增：
      - `PICKER_SESSION_NOT_FOUND`：`Poll` / `Cancel` 时 sessionId 在 registry 中不存在或已被清理。
      - `PICKER_SESSION_ACTIVE`：同 file 已有 Pending session。
      - `PICKER_SESSION_EXPIRED`：session 已超时（Status=Expired）。
      - `PICKER_INVALID_OBJECT_TYPE`：`AllowedObjectTypes` 包含不识别字符串。
      - `COMMAND_BLACKLISTED`：命令命中黑名单。

12. **session 状态结构与并发**
    - `IPickerSessionRegistry` 用 `ConcurrentDictionary<Guid, PickerSessionState>`；状态字段使用
      `volatile` 或锁保护，确保 Poll 看到最新状态。
    - `PickerSessionState.Result` 在 `Status == Pending` 时为 `null`；状态转移时整体替换（不允许
      partial update）。
    - 进程内 registry，重启 MCP 服务进程后所有 Pending session 丢失；客户端拿到
      `PICKER_SESSION_NOT_FOUND` 后应主动重新 `Begin*`。

## 涉及文件

**新增（Tools）** —— 9 个文件：
- `src/MCP_Rhino.Server/Tools/Interaction/Picker/BeginPickPointTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Picker/BeginPickObjectTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Picker/BeginPickCurveTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Picker/BeginPickNumberTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Picker/BeginPickStringTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Picker/PollPickResultTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Picker/CancelPickTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Command/RunRhinoScriptTool.cs`
- `src/MCP_Rhino.Server/Tools/Interaction/Command/RunRhinoScriptBatchTool.cs`

**新增（Application）**：
- `src/MCP_Rhino.Server/Application/Services/RhinoInteractionPickerService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoCommandExecutionService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILivePickerSessionDispatcher.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveCommandRunner.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/IPickerSessionRegistry.cs`

**新增（Infrastructure）**：
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoPickerSessionDispatcher.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoCommandRunner.cs`
- `src/MCP_Rhino.Server/Infrastructure/Interaction/InMemoryPickerSessionRegistry.cs`

**新增（Domain）**：
- `src/MCP_Rhino.Server/Domain/Models/PickSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/PickerSessionState.cs`
- `src/MCP_Rhino.Server/Domain/Models/PickerResult.cs`
- `src/MCP_Rhino.Server/Domain/Enums/PickerKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/PickerStatus.cs`
- `src/MCP_Rhino.Server/Domain/Enums/PickerGetResultMapping.cs`

**新增（Contracts）**：
- `src/MCP_Rhino.Server/Contracts/Requests/BeginPickPointRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/BeginPickObjectRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/BeginPickCurveRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/BeginPickNumberRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/BeginPickStringRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PollPickResultRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CancelPickRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/RunRhinoScriptRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/RunRhinoScriptBatchRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/PickerSessionResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/PickerResultResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/RunRhinoScriptResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/RunRhinoScriptBatchResponse.cs`

**新增（Test）**：
- `Project_Test/260422_TEST_interaction-command-tools/`，包含：
  - `PickerSessionSmokeTest.cs`：覆盖 5 个 Begin / Poll / Cancel；超时 / 重复 active session
    / 状态过期。
  - `RunScriptSmokeTest.cs`：覆盖单条 + 批量；`UseUndoRecord` 开 / 关；黑名单命中。
  - `samples/`：一份带几条曲线 / 几个对象的 `.3dm`，便于 PickObject / PickCurve smoke。

**修改**：
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs` —— 注册 3 个接口实现 + 2 个 Service +
  `IPickerSessionRegistry` 单例。
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs` + `.Parsing.cs` ——
  追加 `interaction-picker-smoke-test` / `interaction-command-smoke-test` 两个子命令。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs` —— 接入新 smoke 分支。

**复用（不改）**：
- `ILiveRhinoDocumentAccessor`（`Execute` / `ExecuteWithUndo`）。
- `OperationResponse<T>` / `ObjectEditWarning`。
- `RhinoObjectType` 枚举（`AllowedObjectTypes` 字符串映射沿用现有命名）。

## 使用方式

MCP Tool 调用示例：

- 拾取一个点：
  ```
  BeginPickPoint(filePath, prompt:"请点击放置柱子的位置", timeoutSeconds:60)
   → {sessionId:"abc-123", status:"Pending"}
  PollPickResult(sessionId:"abc-123")  // 客户端轮询，间隔 500ms
   → {status:"Pending"}                // 用户尚未点击
  PollPickResult(sessionId:"abc-123")
   → {status:"Completed", point:{X:100, Y:200, Z:0}, getResult:"Success"}
  ```
- 拾取一条曲线（带样点）：
  ```
  BeginPickCurve(filePath, prompt:"请选一条曲线作为 sweep rail", includeSamplePoints:true, timeoutSeconds:120)
   → {sessionId:"def-456"}
  PollPickResult(sessionId:"def-456")
   → {status:"Completed", objectId:"GUID", curvePoints:[{...}, ...], getResult:"Success"}
  ```
- 拾取一个数字（带边界 / 默认值）：
  ```
  BeginPickNumber(filePath, prompt:"输入半径 (mm)", defaultValue:50, lowerBound:1, upperBound:1000)
   → {sessionId:"ghi-789"}
  PollPickResult(sessionId:"ghi-789")
   → {status:"Completed", number:75, getResult:"Success"}
  ```
- 取消进行中的会话：
  ```
  CancelPick(sessionId:"abc-123")
   → {status:"Cancelled"}
  ```
- 直接跑 Rhino 命令：
  ```
  RunRhinoScript(filePath, command:"_-Loft _Pause _Pause _Enter", echo:false, useUndoRecord:true)
   → {success:true, durationMs:120, undoRecordSerialNumber:42}
  ```
- 批量跑命令（共用 Undo record）：
  ```
  RunRhinoScriptBatch(filePath, commands:["_-Layer _N MCP_TEMP _Enter", "_SelAll", "_Move 0,0,0 0,0,1000 _Enter"], useSharedUndoRecord:true)
   → {totalCount:3, succeededCount:3}
  ```

典型流程：
- "让用户在 viewport 里指 3 个点 → MCP 自动连成多段线" → 三次 `BeginPickPoint + PollPickResult`
  循环 → `CreateLines` 串两条线段，全程不阻塞 MCP 后台线程。
- "MCP 还没暴露 _Loft 的原子 Tool" → `RunRhinoScript("_-Loft _SelLast _Enter _Enter ...")`
  作为兜底，与未来即将到来的 `LoftTool` 平滑迁移。

## 验收标准

构建 / smoke：
- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 无 Warning 通过。
- CLI fallback：`dotnet run --project src/MCP_Rhino.Server -- interaction-picker-smoke-test Runtime_Test/MCP_rhino_test.3dm`：
  - 所有 7 个 Picker Tool 返回 `LIVE_RHINO_REQUIRED`。
  - 所有 2 个 Command Tool 返回 `LIVE_RHINO_REQUIRED`。
  - 工作副本对象数 / 图层数 / user text 无变化。
- Live 手工 smoke（Rhino 内 `_McpDevSmoke interaction-picker` / `interaction-command`）：
  - **Picker**：
    - `BeginPickPoint` 返回 `sessionId` < 100ms；MCP 后台线程未阻塞（同时跑其他 read Tool 不
      被卡住）。
    - 用户在 viewport 中点击 → `PollPickResult` 返回 `Status=Completed`，坐标值与点击位置一致
      （误差 < `doc.ModelAbsoluteTolerance`）。
    - `CancelPick` 后 `PollPickResult` 返回 `Status=Cancelled`。
    - `BeginPickPoint` + 60s 不操作 → `PollPickResult` 在 60s 后返回 `Status=Expired`。
    - 同一 file 已有 Pending session 时再次 `BeginPick*` → `PICKER_SESSION_ACTIVE`。
  - **Command**：
    - `RunRhinoScript("_NoEcho _Properties _Enter")` 返回 `Success=true`。
    - `RunRhinoScript("_SaveAs ...")` → `COMMAND_BLACKLISTED`。
    - `UseUndoRecord=true` 时 Edit → Undo 一步还原命令所有副作用。
    - `RunRhinoScriptBatch` 中第 2 条命令 fail → 第 3 条仍执行，结果 `succeededCount=2 / failedCount=1`。

边界用例：
- `BeginPick*` `TimeoutSeconds <= 0` / `> 1800` → 整 Request 失败。
- `Poll` 不存在的 sessionId → `PICKER_SESSION_NOT_FOUND`。
- `Cancel` 已 Completed 的 session → 接受，幂等返回 `Status=Completed`，无副作用。
- `BeginPickObject.AllowedObjectTypes=["Bogus"]` → `PICKER_INVALID_OBJECT_TYPE`。
- `RunRhinoScript` 命令字符串为空 → 整 Request 失败。
- `RunRhinoScript` 命令未以 `_` 开头 → warning，命令仍执行。
- `RunRhinoScriptBatch.Commands` 为空 → 整 Request 失败。

副作用 / Undo：
- `Begin* / Poll / Cancel` 不直接改 ActiveDoc 内容；副作用仅来自用户后续在 Rhino 里完成的 Pick 动作
  本身（Pick 不写文档）。
- `RunRhinoScript(useUndoRecord:true)` 调用前后 Undo 面板新增 1 条；`useUndoRecord:false` 时 Undo
  面板按命令本身的原子行为各自记录（多条 / 0 条不固定）。

## 风险与回退方案

风险：
- **Picker session 与其他 Live Tool 的主线程争用**：Pending Picker 期间，其他走 `ExecuteWithUndo`
  的 Tool 会被 `Get*` 阻塞 UI 主线程。缓解：Tool 文档明确"Picker 期间不要并发其他 mutation Tool"；
  超时机制兜底，Picker 不会无限制持有主线程；后续可考虑在 `ILiveRhinoDocumentAccessor` 加 "session
  awareness"，但本期不做。
- **RhinoCommon `GetBaseClass` Cancel API 边界**：`Cancel()` 需要在 Get 实例所在线程调用；本服务通过
  `RhinoApp.InvokeOnUiThread` 发送 cancel 信号。EXET 阶段需在真实 Rhino 内验证 Cancel 行为
  （部分 Get 子类可能对 Cancel 响应延迟）。
- **PickCurve 的 `IncludeSamplePoints` 性能**：高度复杂曲线生成 50 样点可能耗时；缓解：固定 50 点 +
  采用 `Curve.DivideByCount` 简单实现；如果遇到极端 case，文档允许设 `0` 关闭样点。
- **RunScript 的副作用不可控**：LLM 直接拼命令字符串可能改变文档状态而 MCP 看不见。缓解：黑名单
  覆盖最危险命令；Undo record 兜底；Tool 文档强烈建议优先调原子 Tool，RunScript 仅作兜底。
- **Picker session 状态丢失**：MCP 服务进程重启会清空 registry。缓解：`PICKER_SESSION_NOT_FOUND`
  错误码 + 客户端重连后重新 `Begin*`。如需持久化，后续接 file-based registry。
- **拼写 / 语义异常**：`_Loft` 在不同 Rhino locale 下可能命名不同；缓解：强制 `_` 前缀走国际化
  名；warning 在不以 `_` 开头时提示。

回退方案：
- 单 Tool 失败：从 `ToolRegistration` 黑名单单独下线。
- Picker / Command 子能力独立回退：所有新文件均落在新增子目录 + 新增 Service，不修改既有
  Service / Tool / Skill 签名；`git revert` 整批 commit 即可还原。
- 用户体验回退：若 Picker 模式被反馈"轮询体验差"，可在后续 plan 中引入 server-sent event /
  websocket 通知；本期保持 polling 模型最低复杂度。

## 后续扩展方向

- **多次拾取循环**：`BeginPickPointSequence` 一次会话内连续拾取多个点直到 `Enter`；返回 `Points[]`。
- **GetSubObject / GetGrip**：子对象 / 控制点拾取，需要扩展 PickerKind 枚举与 Result 字段。
- **DynamicDraw 预览**：在拾取过程中按 LLM 提供的 spec 实时画出"幽灵"几何（如柱子轮廓），帮用户判断
  位置；需要在 Picker dispatcher 中接入 `Rhino.Display.DisplayConduit`。
- **Picker session 持久化**：把 registry 存到文件 / 进程外缓存，避免 MCP 重启丢失 Pending。
- **Picker → Skill 的串联**：抽 `InteractiveModelingSkill`，把"拾点 → 自动建几何 → 拾下一个"三步
  打包成单 Tool 调用，简化客户端轮询负担。
- **RunScript 命令模板**：抽 `RunRhinoScriptTemplateTool`，预编排参数化命令模板（`{point}`、`{objectId}`），
  把"自由文本命令"升级为"参数化命令"，安全性与可观测性同步提升。
- **RunScript 输出捕获**：当前仅返回成功 / 失败；后续接入 RhinoApp 命令行 stdout 捕获，把
  `_What`、`_PointsOn` 等查询命令的回显作为字符串返回给 LLM。
- **白名单模式**：当 LLM 用例稳定后，提供"白名单 only" 配置开关，把 RunScript 收紧为可信命令集，
  彻底关闭非预期命令。

