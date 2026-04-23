# Task & Skill Orchestration Workflow

> 本文件整合自前期两份 Workflow 草稿（`task-orchestration-workflow.md` 路由协议 + `skill-orchestration-design-outline.md` skill 调度机制），定义 MCP_Rhino 下"用户布置任务 → 模型路由 / 执行 / 上报缺口 / 进入构建"的完整协议，供审阅修改。

## 目标

定义用户布置任务后，模型如何在现有 `tool / skill / agent` 能力集内自主选择执行路径；当现有能力不足时，如何中止直接执行、说明缺口，并在用户同意后进入符合 `Project_Guides` 的能力构建链路。

## 基础定位

- **Skill = server 端的工作流 Tool**，不是独立技术栈。`Skills/` 目录下沉淀的 C# skill 类必须由一个 `*SkillTool`（或同类 MCP Tool）包裹后，模型才"看得见"。模型不了解 `Skills/`，它只认 MCP Tool List。
- **CLAUDE.md 是薄入口**，不堆规则本体；规则本体在 `Project_Guides/` 下分文件维护。本文件若正式采纳为规则的一部分，升格路径为 `Project_Guides/MCP_Rhino Workflow.md`。

## 核心原则

1. 用户不需要显式指定使用哪个 skill。
2. 模型先做任务路由判断，再进入执行。
3. 能直接完成的任务，不为了"用上 skill"而强行调用 skill。
4. 固定多步骤工作流优先沉淀为 skill。
5. 目标导向、存在分支判断或动态编排的任务优先交由 agent。
6. 当现有能力不足以可靠完成任务时，模型不得硬做，必须先说明缺口并询问是否构建。
7. 一旦进入能力构建，必须遵循 `PLAN -> EXET -> TEST` 链路，并对齐 `Project_Guides`。
8. 原子 Tool 与 Skill Tool 均能完成任务时，**默认优先 Skill Tool**（详见 §选择策略）。

## 标准任务链路

### 1. 用户布置任务

用户以自然语言提出目标、约束、输入文件或预期结果。

### 2. 模型评估阶段（必做、一次性）

模型收到任务后，先判断以下问题：

1. 任务是原子操作，还是复合流程。
2. 任务是否已有现成 tool 可直接完成。
3. 任务是否符合某个已有 skill 的固定工作流。
4. 任务是否需要 agent 做动态规划、分支决策或跨 skill 编排。
5. 当前能力集是否足以可靠完成任务。

评估阶段在**同一轮回复**中产出两个互斥出口之一：

- **自信分支**：在回复中陈述整条计划（"我打算调 A → B → C"），然后执行；中途不再重复声明整体计划，除非任务语义变化。
- **缺口分支**：明确列出缺口（缺什么能力、应归属 Tool / Skill / Agent 哪一层、大致需要补什么），**停在这里等用户指令**。模型不得自行启动 PLAN。

> 关键：**"声明整体计划"与"列缺口"是同一阶段的两个互斥出口**；不会出现"先执行几步再中途列缺口"这类混合态。

### 3. 执行路径选择（仅自信分支进入）

#### A. 直接调用 Tool

满足以下条件时，直接调用 tool：

- 任务目标清晰。
- 输入结构明确。
- 单个 tool 或少量直接 tool 调用即可完成。
- 中途不需要复杂判断或多轮确认。

适用示例：读取图层；导出文件；创建单类基础几何；获取对象属性或度量结果。

#### B. 调用 Skill

满足以下条件时，优先调用 skill：

- 任务是固定的多步骤工作流。
- 该流程会重复出现。
- 流程边界清晰，适合复用。
- 已有 skill 能覆盖主要步骤。

适用示例：对象筛选后再进入编辑前确认；图层候选解析后再执行批量筛查；一组稳定的预览 → 应用操作链路。

#### C. 调用 Agent

满足以下条件时，优先调用 agent：

- 任务是目标导向，不是单一动作。
- 需要动态决定下一步做什么。
- 可能需要组合多个 skill 或 tool。
- 存在条件分支、兜底策略或阶段性判断。

适用示例：先审计模型，再决定是否做进一步几何分析；先筛选对象，再判断走修改、导出还是生成报告；根据文档状态、执行模式和当前结果动态选择后续路径。

## Tool / Skill / Agent 判定边界

### Tool

- 面向单一能力。
- 直接暴露给客户端。
- 尽量原子。
- 不承担复杂流程编排。

### Skill

- 面向固定工作流。
- 组合多个 tool、service 或 use case。
- 适合重复复用。
- 不承担高自由度任务规划。

### Agent

- 面向目标完成。
- 负责判断、调度和编排。
- 优先调用已有 skill，其次在必要时直接调用 tool 或 service。
- 不直接承载底层 Rhino 读写细节。

## 选择策略

- **默认优先 Skill Tool**：当原子 Tool 链和某个 Skill Tool 都能完成任务时，选 Skill Tool。
- **描述（`[Description]`）写作差异**：
  - **Skill Tool**：任务导向 —— "做成什么事"。例："审计某图层下的对象数、user text 分布、空图层候选"。
  - **Atomic Tool**：操作导向 —— "执行什么动作"。例："按名称前缀过滤图层"。
- 描述差异是模型偏好 Skill Tool 的主要依据，因此 Skill Tool 的描述必须写得比原子 Tool 更贴近"用户意图关键词"。
- 命名保留现有 `*Tool` 约定，但 Skill Tool 建议引入 `Skill` 中缀帮助模型分类（例如 `AuditLayerUsageSkillTool`）。

## 能力不足时的处理协议

当模型判断现有 tools / skills / agents 不能可靠完成任务时，必须停止直接执行，并输出以下内容：

1. 当前为什么不能完成。
2. 缺失的是哪类能力。
3. 该能力应归属哪一层：
   - 缺原子能力 → 新增 Tool。
   - 缺固定流程 → 新增 Skill。
   - 缺动态编排 → 新增 Agent。
4. 构建该能力大致需要补充什么内容。
5. 询问用户是否开始构建。

## 用户同意构建后的链路

一旦用户显式确认（例如 "开始写 PLAN"），必须进入完整能力演进流程：

1. 先产出 `Project_Plan/YYMMDD_PLAN_<capability-name>.md`。
2. 再执行实现与落地。
3. 补充 `Project_Test/YYMMDD_TEST_<capability-name>/`。
4. 完成验证后沉淀 `Project_Exet/YYMMDD_EXET_<capability-name>.md`。

该链路必须遵循 `Project_Guides` 中关于以下内容的要求：

- 架构归属。
- Tool / Skill / Agent 分层。
- 在线 / 离线执行模式。
- 命名指南。
- Plan / Exet / Test 产物沉淀方式。

## Skill 自动触发的元数据要求

如果希望模型不依赖用户点名就能自主判断是否使用某个 skill，则每个 skill 必须具备清晰的触发说明。Skill 描述应明确写出：

1. 它做什么。
2. 适用于哪些任务。
3. 何时优先于直接 tool 调用。
4. 输入前提和适用边界。
5. 不适用的情况。

建议避免只写功能名，而不写触发场景。

## Skill 候选识别与提议流程

### 识别范围

- **基于持久化 activity log**，不是单会话。原因：单会话看不到"真·高频"，容易漏判。
- 模型每次任务评估阶段顺带读入最近 N 条 activity log，识别是否有重复出现的 tool 调用组合。

### 提议流程

1. 模型在任务回复末尾**单独附一段**「Skill 候选建议」，仅包含：
   - 检测到的重复组合（tool 链路）。
   - 该组合被重复使用的次数与最近出现时间。
   - 建议打包为 Skill 的一句话定位。
2. 用户判断：
   - 若说 "开始写 PLAN"，走 PLAN-EXET-TEST 常规链路。
   - 若不采纳，候选建议自然丢弃，**不沉淀到任何文件**（避免噪音）。

## Activity Log 最小可行设计

- 格式：**JSONL**。最轻、易 append、可 grep：
  ```json
  {"ts":"2026-04-23T14:02","task":"add spheres on layer X","tools":["CreatePointsTool","GetLayersInLiveTool"]}
  ```
- **写入职责**：模型在每次任务收尾时调用一个统一的 `AppendActivityLogTool`（需新增），而非每个业务 Tool 自行写入 —— 避免污染所有 Tool 实现。
- **读取约定**：CLAUDE.md 指示模型"每次任务开头读最近 N 条活动日志"。N 建议初始 50，可调。
- **字段仅保留关键项**：`ts`、`task`（任务一句话摘要）、`tools`（调用链）。不记参数细节、不记结果，避免 log 膨胀与隐私泄漏。

## 建议的规则文件结构

```
CLAUDE.md                                   # 入口：加载顺序、指向下列文件
Project_Guides/
  MCP_Rhino Architecture.md                 # 已存在：分层规则
  MCP_Rhino Plan Log.md                     # 已存在：PLAN-EXET-TEST 命名与内容
  MCP_Rhino Workflow.md                     # 待采纳：本文件升格而来
Workflow/
  activity-log.jsonl                        # 待新增：tool 调用活动日志
```

## 建议的统一输出格式

### 情况 A：现有能力足够

- 说明模型选择了 Tool / Skill / Agent 中的哪一层。
- 简述选择原因。
- 开始执行。

### 情况 B：现有能力不足

- 说明当前无法可靠完成。
- 说明缺少什么能力。
- 说明建议新增到哪一层。
- 询问是否进入构建流程。

### 情况 C：用户同意构建

- 明确进入 `PLAN -> EXET -> TEST`。
- 先整理能力目标、边界和归属。
- 再开始规划文档。

## 顶层调度规则

可作为后续 system prompt、agent prompt 或 orchestrator prompt 的基础版本：

1. 收到任务后，先判断是 Tool、Skill 还是 Agent 级任务。
2. 若一个现有 tool 可直接完成，则直接调用 tool。
3. 若任务符合已有固定工作流，则优先调用 skill。
4. 若任务需要动态规划、多步选择或跨 skill 编排，则调用 agent。
5. 原子 Tool 链和 Skill Tool 均能做时，默认选 Skill Tool。
6. 若现有能力不足以可靠完成，则不得直接硬做，必须说明能力缺口并询问是否构建。
7. 若用户同意构建，则必须遵循 `Project_Guides`，进入完整的 `PLAN -> EXET -> TEST` 链路。

## 设计决策记录

本文件的核心分歧已经在一次系统化对齐中收束，早期"待进一步明确的点"列表转换为以下固定决策：

| 议题 | 决策 |
| --- | --- |
| Skill 元数据放代码注册层、prompt 层还是独立索引？ | 代码注册层（以 `[Description]` 为准），索引依靠 MCP tools list。 |
| 能力缺口提示是否需要固定模板？ | 见 §能力不足时的处理协议，四条固定项外加"询问是否构建"。 |
| 直接 tool 可做但已有 skill 也能做，优先级如何？ | 默认优先 Skill Tool（见 §选择策略）。 |
| 是否拆成"运行时协议 + 能力建设协议"两份？ | 维持单文件，两类协议分章承载。 |
| 是否引入独立 Orchestrator Agent？ | 暂缓。现阶段 Skill Tool + Atomic Tool + 系统提示已足以支撑；等高频识别机制跑一段时间、确实出现"多 Skill 动态编排"的诉求再考虑。 |
| "高频" 识别范围：单会话 vs 跨会话？ | 引入持久化 activity log，跨会话识别。 |
| Skill 候选提议后的下一步？ | 仅列出候选，等用户显式 "开始写 PLAN" 再进入 PLAN 流程。 |
| "声明再执行" 的颗粒度？ | 任务开头声明一次整体计划；执行阶段不再重复声明，除非任务语义变化。 |
| "有不确定就报告" 的触发节奏？ | 模型自觉不确定即在评估阶段提出缺口，不等失败后追认。 |

## 后续动手路径（供参考）

1. 新建 `CLAUDE.md`（薄）。
2. 将本文件升格为 `Project_Guides/MCP_Rhino Workflow.md`。
3. 新建 `AppendActivityLogTool` + 对应 `Application/Services/ActivityLogService`。
4. 在当前已落地的 Skills 中挑 1-2 个最高频候选作为首批 Skill Tool 样板（例如 `AuditLayerUsageSkillTool`），走完 PLAN-EXET-TEST 全链路。

这正好对应一份标准 `YYMMDD_PLAN_workflow-rules.md`，但按 §标准任务链路 第 2 步的协议，PLAN 启动需由用户显式发令。
