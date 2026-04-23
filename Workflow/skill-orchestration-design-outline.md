# Skill Orchestration Design Outline

> 本文件是一次 skill 调度设计讨论的结论纲要，供进一步修改审阅。与同目录 `task-orchestration-workflow.md` 互为补充：那份文件定义 Tool / Skill / Agent 的层级与路由原则；本文件聚焦"如何让模型自主选 skill、如何发现新 skill 候选、如何把规则落成文件"的具体机制。

## 1. 基础定位

- **Skill = server 端的工作流 Tool**，不是独立的技术栈。`Skills/` 目录下沉淀的 C# skill 类必须由一个 `*SkillTool`（或同类 MCP Tool）包裹后，模型才"看得见"。模型不了解 `Skills/`，它只认 MCP Tool List。
- **CLAUDE.md 是薄入口**，不堆规则本体；规则本体继续放在 `Project_Guides/`（对应当前仓库布局）。本设计纲要如需正式落地，会新增一份工作流规则文件（例如本文件内容被采纳后升格为 `Project_Guides/MCP_Rhino Workflow.md`），以及一份 activity log。

## 2. 模型收到任务后的行为协议

1. **评估阶段（必做、一次性）**：模型解析任务 → 在同一轮回复中给出它打算执行的整条 tool 调用计划。
2. **两个互斥出口：**
   - **自信分支**：陈述整体计划 → 执行；中途不再重复声明整体计划，除非任务语义发生变化。
   - **缺口分支**：明确列出缺口（能力缺什么、应该归属 Tool / Skill / Agent 哪一层、大致需要补什么），**停在这里等用户指令**。模型不得自行启动 PLAN。
3. **进入构建**的唯一触发：用户显式说"开始写 PLAN"（或等价指令）。之后进入 `Project_Guides/MCP_Rhino Plan Log.md` 规定的 `YYMMDD_PLAN_<capability-name>.md` 起草流程。

> 关键：**"声明整体计划"与"列缺口"是同一阶段的两个互斥出口**；不会出现"先执行几步再中途列缺口"这类混合态。

## 3. Skill-Tool 与 Atomic Tool 的分工

- **默认偏好 Skill Tool**：当一个任务原子 Tool 链和某个 Skill Tool 都能完成时，模型应优先选 Skill Tool。
- **描述（`[Description]`）写作原则**：
  - **Skill Tool**：任务导向——"做成什么事"。例："审计某图层下的对象数、user text 分布、空图层候选"。
  - **Atomic Tool**：操作导向——"执行什么动作"。例："按名称前缀过滤图层"。
- 描述差异是模型默认偏好 Skill Tool 的主要依据，因此 Skill Tool 的描述必须写得比原子 Tool 更贴近"用户意图关键词"。
- 命名建议保留现有 `*Tool` 约定，但 Skill Tool 建议引入 `Skill` 中缀以帮助模型分类，例如 `AuditLayerUsageSkillTool`。

## 4. Skill 候选识别与落地流程

### 4.1 识别范围

- **基于持久化 activity log**，不是单会话。原因：单会话内看不到"真·高频"，容易漏判。
- 模型每次任务评估阶段顺带读入最近 N 条 activity log，识别是否有重复出现的 tool 调用组合。

### 4.2 提议流程

1. 模型在任务回复末尾**单独附一段**「Skill 候选建议」，内容仅包含：
   - 检测到的重复组合（tool 链路）
   - 该组合被重复使用的次数与最近出现时间
   - 建议打包为 Skill 的一句话定位
2. 用户判断：
   - 若"开始写 PLAN"，走 PLAN-EXET-TEST 常规链路；
   - 若不采纳，候选建议自然丢弃，**不沉淀到任何文件**（避免噪音）。

### 4.3 PLAN-EXET-TEST 链路

继续按 `Project_Guides/MCP_Rhino Plan Log.md` 规则走，不重新定义。

## 5. 建议的规则文件结构

```
CLAUDE.md                                   # 入口：加载顺序、指向下列文件
Project_Guides/
  MCP_Rhino Architecture.md                 # 已存在：分层规则
  MCP_Rhino Plan Log.md                     # 已存在：PLAN-EXET-TEST 命名与内容
  MCP_Rhino Workflow.md                     # 待采纳：human-model 交互协议（本纲要升格而来）
Workflow/
  task-orchestration-workflow.md            # 已存在：Tool/Skill/Agent 路由原则
  skill-orchestration-design-outline.md     # 本文件：讨论成果
  activity-log.jsonl                        # 待新增：tool 调用活动日志
```

若正式采纳本纲要，`Project_Guides/MCP_Rhino Workflow.md` 建议章节：

- **任务接收协议**（对应本纲要 §2）
- **Tool 选择策略**（对应本纲要 §3）
- **Skill 候选提议流程**（对应本纲要 §4）
- **交互示范**：给 2-3 段典型对话片段，强化模型照搬。
- **禁止行为清单**：不准静默跳过声明、不准自行创建 PLAN、不准中途切换执行路径等。

## 6. Activity Log 最小可行设计

- 格式：**JSONL**。最轻、易 append、可 grep：
  ```json
  {"ts":"2026-04-23T14:02","task":"add spheres on layer X","tools":["CreatePointsTool","GetLayersInLiveTool"]}
  ```
- **写入职责**：模型在每次任务收尾时调用一个统一的 `AppendActivityLogTool`（需新增），而非让每个业务 Tool 自己写——避免污染所有 Tool 实现。
- **读取约定**：CLAUDE.md 指示模型"每次任务开头读最近 N 条活动日志"。N 建议初始 50，可调。
- **字段仅保留关键项**：`ts`、`task`（任务一句话摘要）、`tools`（调用链）。不记参数细节、不记结果，避免 log 膨胀与隐私泄漏风险。

## 7. 与现有 `task-orchestration-workflow.md` 的衔接

本纲要的 §2 / §3 / §4 相当于为既有工作流文件补齐以下缺口：

| 既有文件的"待明确点" | 本纲要的回应 |
| --- | --- |
| skill 元数据放代码注册层、prompt 层还是独立索引？ | **代码注册层**（以 `[Description]` 为准），索引依靠 MCP tools list。 |
| 能力缺口提示是否需要固定模板？ | §2 第 2 步已给出四条固定格式：原因 / 归属层 / 需补内容 / 等指令。 |
| "直接 tool 可做但已有 skill 也能做"的优先级？ | §3：**默认优先 Skill Tool**。 |
| 是否拆成"运行时协议 + 能力建设协议"两份？ | 建议维持单文件（本纲要升格后的 `Workflow.md`），两类协议各占一章即可。 |
| 是否引入独立 Orchestrator Agent？ | **暂缓**。现阶段 Skill Tool + Atomic Tool + 系统提示（CLAUDE.md）已足以支撑。等高频识别机制跑一段时间、确实出现"多 Skill 动态编排"的诉求再考虑。 |

## 8. 后续动手路径（供参考，不在本纲要承诺范围内）

真要落地时，建议一条 PLAN：

1. 新建 `CLAUDE.md`（薄）。
2. 基于本纲要 §2-§4 起草 `Project_Guides/MCP_Rhino Workflow.md`。
3. 新建 `AppendActivityLogTool` + 对应 `Application/Services/ActivityLogService`。
4. 在当前已落地的 Skills 中挑 1-2 个最高频候选作为首批 Skill Tool 样板（例如 `AuditLayerUsageSkillTool`），走完 PLAN-EXET-TEST 全链路。

这正好是一份标准的 `YYMMDD_PLAN_workflow-rules.md`，但**本纲要阶段不主动起草**——按 §2 协议，需由你显式发出"开始写 PLAN"后才能进入。
