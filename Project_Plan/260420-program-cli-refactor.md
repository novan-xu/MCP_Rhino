# Background

`MCP_Rhino Architecture.md` 要求 `Program.cs` 只负责应用启动、Host 构建和注册调用，不应继续承载业务流程、参数解析或 Rhino 相关交互逻辑。当前 `Program.cs` 集中堆积了开发者 CLI 命令分发、请求构建和格式化输出，已经偏离入口文件职责。

# Goal

将开发者 CLI 处理逻辑从 `Program.cs` 迁移到独立组件中，让入口文件恢复轻量职责，同时保持现有调试命令的兼容性。

# Architecture Ownership

- `Program.cs`：仅保留启动与调用入口。
- `Infrastructure/CLI/`：承载命令行这一外部交互适配层。
- `Server/DependencyInjection.cs`：负责注册 CLI 组件。

# Key Design

1. 新增 `Infrastructure/CLI/DeveloperCommandHandler` 作为统一入口。
2. 使用 partial class 拆分为：
   - `DeveloperCommandHandler.cs`：命令分发与处理
   - `DeveloperCommandHandler.Parsing.cs`：参数解析与 Request 构建
   - `DeveloperCommandHandler.Formatting.cs`：CLI 输出格式化
3. `Program.cs` 只通过 DI 获取处理器并调用 `TryHandle(args)`。
4. 保持现有命令名称、参数格式和输出文本不变。

# Files Involved

- `src/MCP_Rhino.Server/Program.cs`
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.Parsing.cs`
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.Formatting.cs`

# Usage

原有开发者命令保持不变，例如：

```powershell
dotnet run --project src/MCP_Rhino.Server -- find-layer-candidates <3dm文件路径> <layerQuery>
dotnet run --project src/MCP_Rhino.Server -- preview-object-edits <3dm文件路径> <editSpec>
dotnet run --project src/MCP_Rhino.Server -- inspect-file-mutation-readiness <3dm文件路径>
```

# Follow-up Extensions

1. 若命令数量继续增长，可进一步按领域拆分为多个 `*CommandHandler`。
2. 可引入命令描述模型，统一帮助文本和参数校验。
3. 若未来存在更多外部入口，可进一步抽象外部适配层与应用编排层边界。