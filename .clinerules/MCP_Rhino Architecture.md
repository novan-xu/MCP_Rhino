# MCP_Rhino Architecture Rules

## 目标

本规则用于约束 MCP_Rhino 项目后续新增代码与功能的归属，避免再次退化为单文件、大杂烩结构。

## 目录归属规则

### 1. Program.cs
- 只负责应用启动、Host 构建、调用注册扩展。
- 不放业务逻辑。
- 不放 Rhino3dm 读写细节。

### 2. Server/
- `DependencyInjection.cs`：注册内部服务、仓储、基础设施实现。
- `ToolRegistration.cs`：注册 MCP tool 模块。
- `AgentRegistration.cs`：注册 agent / skill 及未来编排组件。

### 3. Tools/
- 只放直接暴露给 MCP Client 的原子能力。
- Tool 负责参数接收、调用 Application/Service、格式化输出。
- Tool 中禁止堆积大段 Rhino 文件读写逻辑。
- 命名统一使用 `*Tool` 后缀。

### 4. Skills/
- 放置固定流程的复合能力。
- Skill 可以组合多个 service / use case / tool。
- Skill 不直接承担底层文件 API 细节。
- 命名统一使用 `*Skill` 后缀。

### 5. Agents/
- 放置目标驱动、可做决策/调度的执行者。
- Agent 优先调用 Skill 或 Application Service。
- Agent 不直接写 Rhino3dm 细节。
- 命名统一使用 `*Agent` 后缀。

### 6. Application/
- 放置用例、服务、流程编排、接口抽象。
- 这里回答“系统如何完成某个功能”。
- Application 依赖抽象接口，不直接耦合具体基础设施实现。
- 命名建议使用 `*Service`、`*UseCase`、`I*`。

### 7. Domain/
- 放置核心业务模型、值对象、规则、枚举。
- Domain 不依赖 MCP、Rhino3dm、文件系统实现。
- 这里回答“业务概念和规则是什么”。

### 8. Infrastructure/
- 放置 Rhino3dm、文件系统、配置、日志、命令行入口适配等具体实现。
- 所有 `File3dm.Read/Write` 等细节集中在这里。
- 这里回答“具体如何和外部技术打交道”。
- `CLI/` 放置面向开发者 / 终端的命令行适配实现（例如 `DeveloperCommandHandler`），作为外部入口到 Application / Agent 层的薄适配层；`Program.cs` 只负责解析并委托给这里。

### 9. Contracts/
- 放置 Request / Response / Agent 消息 DTO。
- Contracts 只负责数据交换结构，不承载复杂业务规则。
- 命名统一使用 `*Request`、`*Response`、`*Message`。

### 10. Prompts/
- 放置 Agent / Skill 使用的提示词模板。
- 长 prompt 不要硬编码在 C# 类中。
- 可使用 `.md`、`.txt`、`.yaml` 等文本文件组织。

## 新功能归属判断表

- 单一动作、直接暴露给 MCP → `Tools/`
- 固定工作流、组合多个能力 → `Skills/`
- 目标驱动、需要选择步骤或调度 → `Agents/`
- 功能流程编排、服务组织 → `Application/`
- 业务模型、值对象、规则 → `Domain/`
- Rhino3dm / IO / Logging / Config 实现 → `Infrastructure/`
- 请求/响应/消息结构 → `Contracts/`
- LLM 指令模板 → `Prompts/`

## 命名规则

- 避免使用 `Helper`、`Manager`、`Util` 这类宽泛命名。
- 优先使用明确职责命名：
  - `GetRhinoFileSummaryTool`
  - `RhinoGeometryService`
  - `CreateRandomSpheresRequest`
  - `RhinoInspectionAgent`
  - `LayerAuditSkill`

## 演进规则

- 先保持单项目分层；当项目复杂度显著上升时，再考虑升级为多项目 solution。
- 任何新增功能都必须先判断归属，再决定目录位置。
- 如果某个 Tool 开始承担复杂流程，应考虑将流程下沉到 `Application/` 或升级为 `Skill`。
- 如果某个 Skill 开始出现目标判断与动态策略，应考虑升级为 `Agent`。

## 能力规划文档沉淀规则

- 在项目根目录维护 `Project_Plan/` 目录，用于存放每次成功新增能力后的计划总结文档。
- 当用户先在 PLAN MODE 中要求规划某个新能力，并且该能力随后在 ACT MODE 中实现前，必须先补写一份对应的 Markdown 文档到 `Project_Plan/`，再进行实际操作。
- 文档文件名必须使用英文，并遵循格式：`YYMMDD_capability-name.md`。
- 文件名使用全小写英文，单词之间使用连字符 `-`，避免使用中文、空格或不稳定字符。
- 每份文档至少应包含：背景、目标、架构归属、关键设计、涉及文件、使用方式、后续扩展方向。
- 该文档沉淀步骤视为“能力完成”的一部分，不应省略。