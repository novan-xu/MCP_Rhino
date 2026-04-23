# Task & Skill Orchestration Workflow

> 定义"用户布置任务 → 模型路由 / 执行 / 上报缺口 / 进入构建"的完整协议。

## 目标

规定模型在现有 `tool / skill / agent` 能力集内如何自主路由任务；能力不足时如何停下来上报缺口，并在用户同意后按 `Project_Guides` 走完能力构建链路。

## 基础定位

- **Skill = server 端的工作流 Tool**。`Skills/` 下的 C# skill 类必须由 `*SkillTool`（或同类 MCP Tool）包裹才对模型可见；模型只认 MCP tools list。
- **`AGENTS.md` 是薄入口**，规则本体分散在 `Project_Guides/` 与 `Workflow/`。选 `AGENTS.md` 是因为它是目前跨工具接受度最广的事实标准（Codex 原生、Cursor 支持、Claude Code fallback）；不兼容的客户端可在自身配置里直接把上述两个目录加入规则源。本文件若正式采纳，升格为 `Workflow/MCP_Rhino Workflow.md`。

## 目录职责

| 目录 | 用途 | 示例 |
| --- | --- | --- |
| `Project_Guides/` | 指导模型 **构建新能力**（"怎么新增 / 修改代码"） | `MCP_Rhino Architecture.md`、`MCP_Rhino Plan Log.md` |
| `Workflow/` | 指导模型 **使用现有能力**（"收到任务怎么选 / 怎么跑"） | `task-orchestration-workflow.md`（本文件） |
| `log/` | 跨会话 tool 调用历史，供高频识别使用 | `YYMM.json`（按月归档） |

新增规则文件先判断"教怎么造"还是"教怎么用"，再决定落哪一层。

## 核心原则

1. 用户不显式点名 skill，由模型自主路由。
2. 原子 tool 能完成的不升级到 skill；固定多步流程沉淀为 skill；目标驱动、存在分支或动态编排的交给 agent。
3. 原子 Tool 链和 Skill Tool 都能做时，**默认选 Skill Tool**。
4. 能力不足以可靠完成时不得硬做，停下说明缺口并询问是否构建。
5. 进入能力构建必须遵循 `Project_Guides`，走完整的 `PLAN → EXET → TEST` 链路。

## 标准任务链路

### 1. 用户布置任务

自然语言提出目标、约束、输入文件或预期结果。

### 2. 模型评估阶段（必做、一次性）

评估任务类型、现有 tool / skill / agent 覆盖度以及整体可靠性。在**同一轮回复**中产出两个互斥出口之一：

- **自信分支**：陈述整条计划（"我打算调 A → B → C"）后执行；中途不重复声明，除非任务语义变化。
- **缺口分支**：列出缺什么、归属哪一层、需补什么，**停住等用户指令**；不得自行启动 PLAN。

> 缺口判断依据是**评估阶段的自觉不确定**，不是等执行失败后追认；只要预感能力可能不足就走缺口分支。

### 3. 执行路径选择（仅自信分支进入）

| 路径 | 适用条件 | 示例 |
| --- | --- | --- |
| **Tool** | 目标清晰、输入明确、单个或少数 tool 可完成、无需多轮判断 | 读图层；导出文件；创建基础几何；读属性 / 度量 |
| **Skill** | 固定多步骤工作流、会重复出现、边界清晰、已有 skill 覆盖主要步骤 | 对象筛选 → 编辑前确认；预览 → 应用链路 |
| **Agent** | 目标导向非单一动作、需动态决定下一步、跨 skill 编排、存在条件分支或兜底策略 | 先审计再决定分析方向；按文档状态动态选后续路径 |

## Tool / Skill / Agent 判定边界

- **Tool**：面向单一能力、直接暴露给客户端、尽量原子、不承担编排。
- **Skill**：面向固定工作流、组合多个 tool / service、适合复用、不做高自由度规划。
- **Agent**：面向目标完成，负责判断、调度、编排；优先调 skill，必要时调 tool；不碰底层 Rhino 细节。

> 当前阶段**不引入独立顶层 Orchestrator Agent**。Skill Tool + Atomic Tool + `AGENTS.md` 已够用；等跨会话日志机制跑一段时间、确实出现"多 Skill 动态编排"高频诉求再考虑。

## 选择策略

- 原子 Tool 链与 Skill Tool 都能做时默认选 Skill Tool。
- 描述写作差异：
  - **Skill Tool** 的 `[Description]`：任务导向，贴近"用户意图关键词"（例："审计某图层下的对象数、user text 分布、空图层候选"）。
  - **Atomic Tool** 的 `[Description]`：操作导向（例："按名称前缀过滤图层"）。
- Skill Tool 建议加 `Skill` 中缀辅助模型分类（例如 `AuditLayerUsageSkillTool`）。

## 能力不足时的处理协议

停止执行，输出：(1) 为什么不能完成；(2) 缺什么能力；(3) 应归哪一层（原子 → Tool / 流程 → Skill / 编排 → Agent）；(4) 大致补什么；(5) 询问是否开始构建。

## 用户同意构建后的链路

用户显式确认（例如 "开始写 PLAN"）后按顺序：

1. `Project_Plan/YYMMDD_PLAN_<capability-name>.md`
2. 实现与落地
3. `Project_Test/YYMMDD_TEST_<capability-name>/`
4. `Project_Exet/YYMMDD_EXET_<capability-name>.md`

全程遵循 `Project_Guides` 中关于架构归属、分层、执行模式、命名、产物沉淀的要求。

## Skill 元数据要求

- **承载位置**：代码注册层，以 `*SkillTool` 的 `[Description]` 为准；**不另立** prompt 层或独立索引，避免多源漂移。
- **描述内容**：明确"做什么 / 适用场景 / 何时优先于原子 tool / 输入前提与边界 / 不适用情况"。避免只写功能名。

## Skill 候选识别与提议

- **识别范围**：基于 `log/` 下的持久化 activity log（跨会话）。单会话数据太稀，漏判率高。
- **提议方式**：模型在任务回复末尾**单独附一段**「Skill 候选建议」，写明重复组合、出现次数 / 最近时间、打包后的一句话定位。
- **后续**：用户说 "开始写 PLAN" 才进入 PLAN 流程；若不采纳，候选丢弃，**不沉淀到任何文件**。

## Activity Log 最小可行设计

- **位置**：根目录 `log/`，跨会话长期历史。
- **归档**：按月聚合，文件名 `YYMM.json`（例：2026-04-23 的 log 落在 `log/2604.json`）。
- **格式**：内部 JSONL（每行一条 JSON 记录）便于 append 与 grep；扩展名按约定 `.json`。示例：
  ```json
  {"ts":"2026-04-23T13:36","task":"add spheres on layer X","tools":["CreatePointsTool","GetLayersInLiveTool"]}
  ```
- **写入**：统一由 `AppendActivityLogTool`（需新增）在任务收尾时写，内部按当前时间自动定位月份文件，业务 Tool 不各自写。
- **读取**：`AGENTS.md` 要求"每次任务开头读最近 N 条日志"，N 初始 50；当月不足 N 时顺延读前一月。
- **字段**：仅 `ts` / `task`（一句话摘要）/ `tools`（调用链）。不记参数、不记结果，避免膨胀与隐私泄漏。

## 建议的规则文件结构

```
AGENTS.md                                   # 入口：加载顺序、指向下列文件
Project_Guides/                             # 指导"构建能力"
  MCP_Rhino Architecture.md
  MCP_Rhino Plan Log.md
Workflow/                                   # 指导"使用能力"
  task-orchestration-workflow.md            # 本文件（草稿）
  MCP_Rhino Workflow.md                     # 待采纳：本文件升格后的正式规则
log/                                        # 跨会话 tool 调用历史（按月归档）
  2604.json
  2605.json
```

> 不兼容 `AGENTS.md` 的客户端（Cursor 用 `.cursorrules`、Windsurf 用 `.windsurfrules` 等）可在自身配置里直接引用 `Project_Guides/` 与 `Workflow/` 下的 Markdown 作为规则源；`AGENTS.md` 只是默认入口，不是唯一路径。

## 后续动手路径（供参考）

1. 新建 `AGENTS.md`（薄入口）。
2. 将本文件升格为 `Workflow/MCP_Rhino Workflow.md`。
3. 新建 `AppendActivityLogTool` + `ActivityLogService`（按月写 `log/YYMM.json`）。
4. 从现有 Skills 里挑 1-2 个最高频候选做首批 Skill Tool 样板（如 `AuditLayerUsageSkillTool`），走完 PLAN-EXET-TEST。

这正好对应一份 `YYMMDD_PLAN_workflow-rules.md`，但按 §标准任务链路 第 2 步协议，PLAN 启动需用户显式发令。
