# 背景

当前项目已经具备对象级 user text 的批量写入与按 user attributes 筛查能力，但仍存在两个明显缺口：

- **文档级 user string** 完全未暴露，无法读取、设置或删除 Rhino 文件自身的 key/value 元数据。
- **对象级 user string** 缺少独立读取与独立删除入口，用户只能通过筛查结果侧面查看，或通过通用编辑工具间接删除。

Rhino / rhino3dm API 已提供对应底层能力：

- `File3dm.Strings`
- `File3dmStringTable.SetString(...)`
- `File3dmStringTable.Delete(...)`
- `ObjectAttributes.GetUserStrings()`
- `ObjectAttributes.DeleteUserString(...)`

因此需要补齐一组围绕 user key/value 的原子 MCP tools。

# 目标

- 新增 **文档级 user string** 的读取、写入、删除工具。
- 新增 **对象级 user string** 的读取、删除工具。
- 复用现有文件写保护、备份、结果格式化与 CLI 入口模式。
- 保持新增能力遵循当前单项目分层架构，不把 Rhino3dm 细节堆积在 Tool 中。

# 架构归属

- **Tools/File/**
  - 面向 MCP 暴露文档级 user string 原子能力。
- **Tools/Editing/**
  - 面向 MCP 暴露对象级 user string 读取与删除能力。
- **Application/Services/**
  - `RhinoDocumentUserStringService`：封装文档级 user string 读写删逻辑。
  - 扩展 `RhinoObjectUserTextService`：增加对象级读取与删除逻辑。
- **Contracts/Requests/**
  - 新增文档级 / 对象级 user string 请求 DTO。
- **Contracts/Responses/**
  - 新增文档级 / 对象级 user string 响应 DTO。
- **Infrastructure/**
  - 继续复用 `IRhinoDocumentRepository`、`IFileMutationSafeguard`、CLI 适配层等现有基础设施。

# 关键设计

1. **文档级 key/value 支持两种写法**
   - 普通键值：`key=value`
   - section/entry 键值：`section|entry=value`
   - 删除同理：`key` 或 `section|entry`

2. **对象级读取支持按 ObjectId 精确读取**
   - 输入一组对象 GUID。
   - 返回每个对象的图层、名称、全部 user strings。
   - 若未提供对象列表，可扩展为读取全部对象，但首版以指定对象为主。

3. **对象级删除采用独立工具**
   - 输入 `(ObjectId, Key)` 列表。
   - 内部复用与对象级写入一致的“复制属性 → 删除原对象 → 重新写入对象”策略。

4. **文档级写入 / 删除纳入文件修改保护链路**
   - 在写回前执行 readiness / snapshot / archive cleanup。
   - 避免直接覆盖打开中的 Rhino 文件。

5. **Tool 保持轻量，业务逻辑下沉到 Service**
   - Tool 只负责接参、调用 Service、返回响应。
   - Service 负责校验、执行、汇总与 CLI 文本格式化。

# 涉及文件

- `Project_Plan/260420_user-string-tools.md`
- `src/MCP_Rhino.Server/Application/Services/RhinoDocumentUserStringService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectUserTextService.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/DocumentUserString*.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ObjectUserText*.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/DocumentUserString*.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ObjectUserTextReadResponse.cs`
- `src/MCP_Rhino.Server/Tools/File/GetDocumentUserStringsTool.cs`
- `src/MCP_Rhino.Server/Tools/File/SetDocumentUserStringsTool.cs`
- `src/MCP_Rhino.Server/Tools/File/DeleteDocumentUserStringsTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/GetObjectUserStringsTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/DeleteObjectUserTextTool.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.Parsing.cs`

# 使用方式

## MCP Tool

- `GetDocumentUserStrings(filePath)`
- `SetDocumentUserStrings(filePath, entries)`
- `DeleteDocumentUserStrings(filePath, entries)`
- `GetObjectUserStrings(filePath, objectIds)`
- `DeleteObjectUserText(filePath, entries)`

## CLI 示例

```powershell
dotnet run --project src/MCP_Rhino.Server -- get-document-user-strings <3dm文件路径>
dotnet run --project src/MCP_Rhino.Server -- set-document-user-strings <3dm文件路径> "author=novan;meta|phase=sd"
dotnet run --project src/MCP_Rhino.Server -- delete-document-user-strings <3dm文件路径> "author;meta|phase"
dotnet run --project src/MCP_Rhino.Server -- get-object-user-strings <3dm文件路径> "guid1,guid2"
dotnet run --project src/MCP_Rhino.Server -- delete-object-user-text <3dm文件路径> "guid1|code;guid2|zone"
```

# 后续扩展方向

- 增加文档级 user string 的 section 过滤读取。
- 为对象级读取增加“按筛查结果读取 user text”的 Skill。
- 增加文档级 / 对象级 user string 的 preview-only 能力。
- 为新增工具补充单元测试与集成测试。