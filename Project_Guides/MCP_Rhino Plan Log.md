# MCP_Rhino Plan Log Guides

## 目标

本指南用于约束 MCP_Rhino 项目每一次能力演进的计划、执行、测试产物沉淀方式，保证
"规划 → 执行 → 测试"三份产物结构一致、可追溯、可复核。

## 命名约定（三份产物共用）

每次能力演进必须同时产出三份产物，分别沉淀在三个根目录下：

| 产物 | 根目录 | 形态 | 命名格式 |
| --- | --- | --- | --- |
| 规划文件 | `Project_Plan/` | `.md` | `YYMMDD_PLAN_<capability-name>.md` |
| 执行报告 | `Project_Exet/` | `.md` | `YYMMDD_EXET_<capability-name>.md` |
| 测试文件夹 | `Project_Test/` | 目录 | `YYMMDD_TEST_<capability-name>/` |

**具体指南：**

- `YYMMDD` 为能力创建当日日期（6 位数字，无分隔符），三份产物必须相同。
- 前缀 `PLAN` / `EXET` / `TEST` 全部大写，且用 `_` 与日期、能力名隔开。
- `<capability-name>` 全小写英文，多个单词之间用 `-` 连接，禁止中文、空格、下划线或其他不稳定字符；应尽量简短、精炼。
- 同一能力的三份产物必须共享相同的 `YYMMDD` 与 `<capability-name>`，仅中间前缀不同。

**示例（以 `geometry-analysis-tools` 能力于 2026-04-22 立项为例）：**

- `Project_Plan/260422_PLAN_geometry-analysis-tools.md`
- `Project_Exet/260422_EXET_geometry-analysis-tools.md`
- `Project_Test/260422_TEST_geometry-analysis-tools/`

## 能力规划文档沉淀指南

- 在项目根目录维护 `Project_Plan/` 目录，用于存放每次成功新增能力之前的计划总结文档。
- 当用户要求规划某个新能力，并且该能力随后 Execute 实现前，必须先补写一份对应的 Markdown 文档到 `Project_Plan/`，再进行实际操作。
- 文档命名遵循 §命名约定 中的 `YYMMDD_PLAN_<capability-name>.md`。
- 每份文档至少应包含：
  - **背景**
  - **目标**
  - **架构归属**
  - **关键设计**
  - **涉及文件**
  - **使用方式**
  - **验收标准**
  - **风险与回退方案**
  - **后续扩展方向**
- Plan 定稿后若在 Execute 阶段发现偏差，原则上**不回写** Plan 文档，而是在对应 EXET 文档的「与计划的偏差」章节记录；仅当偏差大到需要重新评审时才更新 Plan，并在文末追加 `## 修订记录（YYYY-MM-DD）`。
- 该文档沉淀步骤视为"能力完成"的一部分，不应省略。

## 执行文档沉淀指南

- 在项目根目录维护 `Project_Exet/` 目录，用于存放每次能力落地后的执行总结文档。
- 每一次依据 `Project_Plan/` 中计划文档进行的 Execute 流程，在代码与测试通过之后，必须补写一份对应的 Markdown 文档到 `Project_Exet/`。
- 文档命名遵循 §命名约定 中的 `YYMMDD_EXET_<capability-name>.md`，与对应 PLAN 文档共享同一 `YYMMDD` 与 `<capability-name>`。
- 每份文档至少应包含以下章节：
  - **对应计划**：链接到 `Project_Plan/` 中的计划文档路径，并注明执行日期。
  - **关联产物**：指向本次执行对应的测试文件夹路径（`Project_Test/YYMMDD_TEST_<capability-name>/`），以及相关 commit hash / PR 链接（若有）。
  - **执行结果 / 实际落地范围**
  - **与计划的偏差**
  - **施工中发现并修复的问题**
  - **测试记录**：构建命令、smoke / 集成测试命令、关键断言输出、使用的测试文件路径、最终对象数等可复核信息。
  - **验收判据对齐**
  - **回退验证**
  - **当前遗留项**
  - **结论**
- 允许同一份 EXET 文档追加多轮复检结果，但应原地追加，不新开同主题文档。
- 执行文档中的路径、命令、退出码、对象数等核查信息应与仓库真实运行结果一致；禁止填写臆测值或占位符。
- 未沉淀 EXET 文档的 Execute 不视为闭环。

## 测试文件夹生成指南

- 在项目根目录维护 `Project_Test/` 目录，用于存放每次能力落地所需的测试代码、测试数据、smoke 脚本等产物。
- 在实施 PLAN 过程中需要新增运行所需文件来验证功能时，把相关文件添加到对应的 test 子文件夹中，而不是散落在仓库其他位置。
- 测试文件夹命名遵循 §命名约定 中的 `YYMMDD_TEST_<capability-name>/`，与对应 PLAN / EXET 文档共享同一 `YYMMDD` 与 `<capability-name>`。
- 文件夹内建议包含（按实际需要裁剪）：
  - **测试代码**：`*.cs` 集成 / smoke 测试源文件，或独立脚本。
  - **测试数据**：引用 `Runtime_Test/` 下的 `.3dm` fixture、JSON 请求样例、参考输出等；默认不再引入工作副本命名约定。
  - **README.md（可选）**：说明运行命令、预期输出、依赖的 Rhino / MCP 环境。
- 测试文件夹需在对应 EXET 文档的「关联产物」与「测试记录」章节被显式引用，形成 `PLAN → TEST → EXET` 的闭环。
- 测试产物默认保留在仓库中，用于回归复查；若确认某份测试已被正式集成测试覆盖且不再需要独立留档，可在 EXET 文档中标注后整体删除该测试文件夹。

## Live Smoke CLI 入口约定

每次能力演进的 live smoke 必须以 **唯一** 的 CLI slug 注册到 `DeveloperCommandHandler`，避免不同 Plan 之间互相抢占入口或重复注册。

- **capability-name 与 slug 分离**：
  - capability-name 用于 Plan / EXET / TEST 产物命名，例如 `geometry-analysis-tools`。
  - CLI slug 用于命令入口命名，例如 `geometry-analysis-smoke-test`。
  - 二者应语义对应，但**不是同一个占位符**，不要把 TEST 文件夹名写成 `..._TEST_<cli-slug>/`。
- **slug 命名**：形如 `<feature>-smoke-test`，全小写、短横线分隔，必须全仓唯一。
- **注册入口**：每个能力的 slug 只能由
  `Project_Test/<YYMMDD>_TEST_<capability-name>/DeveloperCommandHandler.<Feature>SmokeTest.cs`
  这一个 partial 文件承担注册。该文件实现 `partial void RegisterExtensionHandlers()`，并写入
  `_extensionHandlers["<feature>-smoke-test"] = Handle<Feature>SmokeTest;`。
- **pluginMode 分派在入口内部完成**：是否需要按 `McpRhinoPlugin.Instance is null` 分派，由 `Handle<Feature>SmokeTest` 内部自行决定；不允许为同一能力再申请第二条 live 专属 slug。
- **Rhino live smoke 命令也必须能力独占**：每次新能力的 live smoke 都应新增一个专属 `RhinoCommand`，例如 `McpGeometryAnalysisSmokeCommand` 对应 `_McpGeometryAnalysisSmoke`，并在命令内部直接调用该能力自己的 smoke slug。不再使用共享的 `_McpDevSmoke` 入口，以便多个功能组能在 Rhino 内并行复核。
- **slug 归属**：一条 slug 一旦出现在某份 Plan / TEST / EXET 中即视为该能力独占，后续能力不得改写其语义；若回收复用，必须在新的 Plan / EXET 中显式声明。
