# Task & Skill Orchestration Workflow

> 定义“用户布置任务 -> 模型路由 / 执行 / 上报缺口 / 进入构建”的统一协议。

## 目标

规定模型如何在现有 `tool / skill / agent` 能力集内自主路由任务；当能力不足时，如何停止执行、说明缺口，并在用户同意后按 `Project_Guides` 完成能力构建链路。

## 基础定位

- **Skill 是 server 端工作流能力**。`Skills/` 下的 C# skill 需经 `*SkillTool` 或同类 MCP Tool 包装后，才对模型可见；模型实际看到的是 MCP tools list。
- **`AGENTS.md` 是薄入口**。规则主体放在 `Project_Guides/` 与 `Workflow/`。若本文件正式采用，可升格为 `Workflow/MCP_Rhino Workflow.md`。

## 目录职责

| 目录 | 用途 |
| --- | --- |
| `Project_Guides/` | 指导如何构建或修改能力 |
| `Workflow/` | 指导如何使用现有能力、如何路由任务 |
| `log/` | 跨会话 tool 调用历史，供高频模式识别使用 |

新增规则文件前，先判断它是在教“怎么建”，还是在教“怎么用”，再决定归属。

## 核心原则

1. 用户无需显式点名 skill，由模型自主路由。
2. 原子任务优先用 tool；固定多步流程沉淀为 skill；目标导向、含分支或动态编排的任务交给 agent。
3. 原子 Tool 链和 Skill Tool 都能完成时，默认优先 Skill Tool。
4. 现有能力不足以可靠完成时，不得硬做，必须停下说明缺口并询问是否构建。
5. 一旦进入能力构建，必须遵循 `Project_Guides`，走完整 `PLAN -> EXET -> TEST` 链路。

## 标准任务链路

### 1. 用户布置任务

用户以自然语言提出目标、约束、输入文件或预期结果。

### 2. 模型评估阶段

评估任务类型、现有 `tool / skill / agent` 覆盖度，以及整体可行性。评估后只允许进入以下两种分支之一：

- **自信分支**：说明计划路径后直接执行。
- **缺口分支**：说明缺什么、归属哪一层、需补什么，并停下等待用户指令。

缺口判断依据是评估阶段的可信度，不必等执行失败后再追认。

### 3. 执行路径选择

| 路径 | 条件 | 示例 |
| --- | --- | --- |
| **Tool** | 目标清晰、输入明确、少量 tool 可完成、无需多轮判断 | 读图层、导出文件、创建基础几何、读属性或度量 |
| **Skill** | 固定多步流程、重复出现、边界清晰、已有 skill 覆盖主步骤 | 对象筛选后确认、预览后应用 |
| **Agent** | 目标导向、需动态决定下一步、跨 skill 编排、含条件分支或兜底 | 先审计再决定分析方向、按文档状态动态选路径 |

## Tool / Skill / Agent 边界

- **Tool**：单一能力，直接暴露给客户端，尽量原子，不做编排。
- **Skill**：固定工作流，组合多个 tool 或 service，适合复用，不做高自由度规划。
- **Agent**：面向目标完成，负责判断、调度和编排；优先调 skill，必要时调 tool；不碰底层 Rhino 细节。

当前阶段**不引入独立顶层 Orchestrator Agent**。先用 Skill Tool + Atomic Tool + `AGENTS.md`；只有在跨 Skill 动态编排成为高频需求后，再考虑升级。

## 选择策略

- 原子 Tool 链与 Skill Tool 都能完成时，默认选 Skill Tool。
- **Skill Tool 的 `[Description]`** 应写成任务导向，贴近用户意图。
- **Atomic Tool 的 `[Description]`** 应写成操作导向。
- Skill Tool 命名建议保留 `Skill` 中缀，辅助模型分类。

## 能力不足时的处理协议

停止执行，并输出：

1. 为什么当前不能可靠完成。
2. 缺少什么能力。
3. 该能力应归属哪一层：
   - 原子能力 -> `Tool`
   - 固定流程 -> `Skill`
   - 动态编排 -> `Agent`
4. 大致需要补什么。
5. 是否开始构建。

## 用户同意构建后的链路

用户显式确认后，按顺序进入：

1. `Project_Plan/YYMMDD_PLAN_<capability-name>.md`
2. 实现与落地
3. `Project_Test/YYMMDD_TEST_<capability-name>/`
4. `Project_Exet/YYMMDD_EXET_<capability-name>.md`

全程遵循 `Project_Guides` 中关于架构归属、分层、执行模式、命名与产物沉淀的要求。

## Skill 元数据要求

- **承载位置**：代码注册层，以 `*SkillTool` 的 `[Description]` 为准；不另设一份 prompt 层或独立索引，避免多源漂移。
- **描述内容**：至少写清“做什么、适用场景、何时优先于原子 tool、输入前提、边界、不适用情况”。

## Skill 候选识别与提议

- **识别范围**：基于 `log/` 下的持久化 activity log；单会话数据不足以稳定识别。
- **提议方式**：在任务回复末尾单独附一段“Skill 候选建议”，说明重复组合、出现次数或最近时间、以及一句话定位。
- **后续动作**：只有用户明确说“开始写 PLAN”，才进入构建链路；未采纳的候选不沉淀到任何文件。

## Activity Log 最小方案

- **位置**：根目录 `log/`
- **归档**：按月聚合，文件名 `YYMM.json`
- **格式**：JSONL，单行一条记录，便于 append 与 grep
- **写入**：统一经 `AppendActivityLogTool` 写入，业务 Tool 不自行写日志
- **读取**：任务开始时读取最近 `N` 条；当月不足则顺延到上月
- **字段**：仅保留 `ts`、`task`、`tools`，不记参数与结果

示例：

```json
{"ts":"2026-04-23T13:36","task":"add spheres on layer X","tools":["CreatePointsTool","GetLayersInLiveTool"]}
```

## 建议的规则文件结构

```text
AGENTS.md
Project_Guides/
  MCP_Rhino Architecture.md
  MCP_Rhino Plan Log.md
Workflow/
  task-orchestration-workflow.md
  MCP_Rhino Workflow.md
log/
  2604.json
  2605.json
```

不兼容 `AGENTS.md` 的客户端，可在自身配置里直接引用 `Project_Guides/` 与 `Workflow/` 下的 Markdown 作为规则源。

## 后续动作建议

1. 新建 `AGENTS.md` 作为薄入口。
2. 将本文升格为 `Workflow/MCP_Rhino Workflow.md`。
3. 新建 `AppendActivityLogTool` 与 `ActivityLogService`。
4. 从现有 Skills 中挑 1-2 个高频候选做首批 Skill Tool 模板，并按 `PLAN -> EXET -> TEST` 落地。
