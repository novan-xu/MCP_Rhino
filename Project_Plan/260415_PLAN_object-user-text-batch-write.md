# 背景

现有 Rhino user key/value 写入能力建立在“对筛选结果应用共享编辑操作”的模型上，`SetUserText` 只能给同一批对象写入同一个固定值。这对于将 GUID、中心点坐标、面积、外部系统返回值等对象级信息回写为 user text 不够通用。

# 目标

- 删除临时加入的 GUID 专用写入特例，避免把信息来源硬编码进 Editing 枚举。
- 新增一套可直接接收对象级 `(ObjectId, Key, Value)` 数据的批量 user text 写入能力。
- 让其他 tool / skill / agent / 外部端口产出的对象级信息，都可以统一通过该能力写入 Rhino user text。

# 架构归属

- **Tools/Editing/**：新增直接暴露给 MCP Client 的对象级 user text 预览与执行 tool。
- **Application/Services/**：新增 `RhinoObjectUserTextService`，负责校验、预览、写入与结果组织。
- **Contracts/Requests/**：新增对象级 user text 写入 DTO，请求结构显式表达 `(ObjectId, Key, Value)`。
- **Infrastructure/**：复用现有 Rhino 文件仓储、文件写保护和结果格式化基础设施。

# 关键设计

1. **删除 GUID 特例**
   - 移除 `SetUserTextToObjectGuid` 枚举与相关解析/执行分支。
   - 避免让 Editing 底层与某个特定信息来源绑定。

2. **对象级 user text 写入模型**
   - 新增 `ObjectScopedUserTextEntryRequest`：每条 entry 显式携带 `ObjectId`、`Key`、`Value`。
   - `ObjectUserTextBatchWriteRequest` 以一组 entry 为输入，支持每个对象写入不同值，甚至不同 key。

3. **预览与执行分离**
   - `PreviewObjectUserTextWritesTool`：在写回前查看对象级 user text 变化。
   - `ApplyObjectUserTextWritesTool`：执行写回，并复用既有文件写保护与备份机制。

4. **校验策略**
   - 禁止空 ObjectId / 空 key。
   - 禁止同一对象的同一个 key 被重复指定。
   - 若 entry 指向文件中不存在的对象，直接失败。

# 涉及文件

- `src/MCP_Rhino.Server/Contracts/Requests/ObjectScopedUserTextEntryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ObjectUserTextBatchWriteRequest.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoObjectUserTextService.cs`
- `src/MCP_Rhino.Server/Tools/Editing/PreviewObjectUserTextWritesTool.cs`
- `src/MCP_Rhino.Server/Tools/Editing/ApplyObjectUserTextWritesTool.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Program.cs`
- 以及清理 GUID 特例的既有编辑文件：
  - `Domain/Enums/ObjectEditOperationType.cs`
  - `Infrastructure/Rhino/RhinoObjectEditValidator.cs`
  - `Infrastructure/Rhino/RhinoObjectEditOperationApplier.cs`
  - `Application/Services/RhinoObjectEditingService.cs`
  - `Tools/Editing/ApplyObjectEditsTool.cs`
  - `Tools/Editing/PreviewObjectEditsTool.cs`

# 使用方式

## MCP Tool

- `PreviewObjectUserTextWrites(filePath, entries)`
- `ApplyObjectUserTextWrites(filePath, entries)`

其中 `entries` 为对象级写入项列表，每条 entry 形如：

```json
{
  "objectId": "bd96690d-1ef7-4b9d-9e0b-9b8c20465cba",
  "key": "center",
  "value": "(1.2,3.4,5.6)"
}
```

## 开发者命令

```powershell
dotnet run --project src/MCP_Rhino.Server -- preview-object-user-text-writes <3dm文件路径> "objectId|key=value;objectId|key=value"
dotnet run --project src/MCP_Rhino.Server -- apply-object-user-text-writes <3dm文件路径> "objectId|key=value;objectId|key=value"
```

# 后续扩展方向

- 增加接受 JSON 文件或资源 URI 的批量导入形式，减少超长参数字符串。
- 在上层 Skill 中增加对象信息 gather → user text 回写的固定流程封装。
- 支持值格式化策略，例如点坐标/数值精度/单位制转换。
- 为对象级 user text 批量写入增加单元测试与集成测试覆盖。