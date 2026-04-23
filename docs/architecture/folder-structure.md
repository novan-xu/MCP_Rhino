# MCP_Rhino 文件结构说明

## 核心原则

- `Program.cs` 只负责启动。
- `Server/` 只负责依赖注册与模块装配。
- `Tools/` 只负责 MCP 暴露层。
- `Application/` 负责用例与流程编排。
- `Domain/` 负责业务模型与指南。
- `Infrastructure/` 负责 Rhino3dm / 文件系统 / 日志等技术实现。
- `Contracts/` 负责请求响应与消息契约。
- `Skills/` 和 `Agents/` 为未来编排能力预留。
- `Prompts/` 存放与 Agent / Skill 相关的提示词模板。