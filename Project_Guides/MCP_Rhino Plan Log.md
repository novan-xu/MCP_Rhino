# MCP_Rhino Change Records And Archive Guide

## Purpose

Keep enough durable evidence for a later agent to understand what changed, why, what was verified,
and what remains. Match the record size to the work; do not require three separate artifacts for
every task. This guide owns the recordkeeping rules referenced by AGENTS.md and the runtime workflow.

## Choose the recordkeeping level

| Work | Required record |
| --- | --- |
| Substantial capability or architecture change | PLAN before implementation, EXET after execution, and meaningful validation. Create a matching TEST folder only for new standalone validation assets; otherwise link existing tests and results. |
| Small fix, documentation edit, narrow refactor, or routine housekeeping | One concise EXET record. No separate PLAN or placeholder TEST folder. Add or update real tests only where the change warrants them. |
| Routine Rhino task or ad hoc script using existing supported runners | Runtime activity history and useful result evidence in the task response or actual output artifacts. No construction PLAN, EXET, or TEST set. |

Substantial work includes new tool/skill/agent families, changed public contracts, new execution
modes, transport or document-routing changes, broad refactors, and changes to installation or
rollback behavior. Small means a bounded change with understood behavior and a straightforward
rollback; line count alone does not decide the level. If a small change grows into substantial
work, write a PLAN before the expanded implementation and link the existing record.

An explicit user instruction to implement the repository change authorizes the necessary planning;
do not ask again merely to create its record. A runtime gap still requires explicit confirmation
before switching to construction.

The level changes documentation requirements, not validation requirements. Required Debug/Release
builds, MCP safety checks, plug-in identity checks, and installation attestation remain governed by
the Architecture guide. Report failed or incomplete validation honestly; do not mark the work
complete to satisfy a record template.

## Names and locations

| Artifact | Active location | Name |
| --- | --- | --- |
| Plan, when required | Project_Plan/ | YYMMDD_PLAN_<capability-name>.md |
| Execution or lightweight change record | Project_Exet/ | YYMMDD_EXET_<capability-name>.md |
| New standalone test assets, when needed | Project_Test/ | YYMMDD_TEST_<capability-name>/ |

Use the work item's creation date and a short lowercase English name separated by hyphens.
Artifacts for the same work item share that date/name. Append follow-up results to the existing
record rather than starting another record for each retry. Include the actual execution and
follow-up dates inside EXET; filename dates do not prove archive eligibility.

Keep existing historical names and records. Do not retroactively remove PLAN/TEST artifacts or
relabel an incomplete legacy set as lightweight just to make it eligible for archive.

## Lightweight EXET

State "Record level: lightweight", the execution date, and status. One short document should cover:

- Reason and scope: what problem or request prompted the work.
- Changes: what changed and which files or artifacts matter.
- Validation: commands/checks and actual outcomes, or why no execution test was appropriate.
- Remaining work and limits: unresolved issues and what was not verified.
- Related evidence: commit/PR links when available, existing tests, output paths, or archive manifest.
- Rollback notes when reverting is not obvious.

These can be short paragraphs or bullets; separate headings for every field are unnecessary.
Do not create empty test folders, duplicate existing tests, or write tests that only restate the
implementation to complete a document checklist.

## Substantial changes: PLAN and EXET

PLAN is written before implementation and covers the background, goal, architecture ownership,
key design, affected files, usage, acceptance criteria, risks, and rollback. Include future
extensions only when useful. Keep it proportional to the decision being made.

After execution, EXET records "Record level: full", execution date, and status, and links the PLAN.
It covers the actual scope, meaningful deviations/issues, validation evidence, acceptance outcome,
rollback status, and remaining work. Link the matching TEST folder when present; otherwise identify
the existing test coverage and where results are recorded. Do not copy the PLAN into EXET.

Record deviations in EXET rather than rewriting the original plan. If a material scope change
requires a revised plan, append a dated revision note. Failed/partial attempts can and should be
recorded; only a successfully validated result may be described as complete. Paths, commands,
exit codes, counts, and claims must agree with actual evidence.

## Test assets and evidence

- Prefer updating an existing test suite when it owns the behavior. Create a matching Project_Test
  folder only for new standalone smoke code, fixture descriptions, scripts, or useful validation
  evidence. Do not create a README solely to make a third artifact exist.
- Link tests and results from EXET, including the command, relevant assertion/outcome, and limits.
  A documentation-only change can be validated by focused review and diff checks without a build.
- Keep .3dm fixtures under ignored Runtime_Test/local/; use anonymous repository-relative fixture
  paths in records. Sanitized JSON examples and useful non-sensitive outputs may be retained.
- Preserve real regression assets. Remove a redundant standalone suite only when EXET identifies
  its replacement coverage and explains why it is no longer needed.
- Changes to the MCP server, Rhino host, Router compatibility, tool registration, endpoint lifecycle,
  or document routing require Debug and Release validation. Prefer
  dotnet build .\MCP_Rhino.sln -c Debug and dotnet build .\MCP_Rhino.sln -c Release;
  explain any narrower project-level substitute in EXET.
- For runtime history, follow Runtime_Workflow's AppendActivityLogTool contract. This policy does
  not add parameters, results, or script bodies to its minimal activity-log schema.

## Archive completed work

Archive into the same artifact roots under Project_Archive/. Preserve each original filename and
folder name. Archive records and any supporting tests for a work item together:

- Full/legacy set: move its PLAN, EXET, and existing TEST assets together. A full record may
  explicitly link existing test coverage instead of owning a TEST folder. Missing required
  companions or unclear coverage block archival.
- Lightweight set: move its EXET and any tests it actually owns. The documented absence of a PLAN
  or separate TEST folder is intentional, not an unmatched set.
- Shared test suites remain with their owning work item; do not duplicate or move them merely
  because another EXET references them.

Eligibility must be established by reading EXET, including later addenda, rather than by filename
or a keyword alone. Require successful completion, no unresolved required acceptance work, and
more than three days since the execution date. Also retain work with execution, revalidation,
publication, or revisions in the last three days. Use the user's current date; if only a date is
known, require a calendar-date difference greater than three.

Do not archive incomplete, failed, blocked, current, still-active, or ambiguous work, directory
READMEs, or legacy sets missing required companions. Clearly identified future enhancements or
optional checks do not by themselves make a completed scope incomplete. Later supersession does
not turn a failed attempt into a successful one.

Before moving:

1. Record the selected work items, completion evidence, execution dates, and reasons for retaining
   other older work in a manifest linked from this task's EXET.
2. Check exact source/destination paths stay inside this repository, all required companions exist,
   and destinations do not already exist. Refuse overwrite or directory merging.
3. Review build inputs, solution/project references, script root discovery, and active links that
   depend on moved paths. Preserve ignored local test assets; do not delete them as archive cleanup.

After moving:

- Verify the entire selected set arrived intact (file inventory/hashes), preserving names and
  recording any necessary path-only repairs separately.
- Update active documentation and executable references so tests remain discoverable/runnable.
  Historical prose may retain historical paths; when editing a historical record, repair its links.
- Preserve the Server's active and archived test compilation globs and exact exclusions. Retired
  suites stay excluded, standalone test projects stay outside the Server compile, and supported
  smoke tests remain included. Never use a broad exclusion to hide an archive-induced build error.
- Validate affected builds and focused tests; record results and actual limitations in EXET.
- If a move fails partway, restore the moved members from the manifest before reporting that work
  item as archived. Never report a partial set as successfully archived.

The Server compiles both Project_Test/**/*.cs and Project_Archive/Project_Test/**/*.cs.
Production implementation must not live in either test root. Excluding an additional retired suite
still requires a record explaining the retired capability and why its smoke no longer represents
supported behavior.

## Live Smoke CLI 入口约定

需要新增的 live smoke 必须以 **唯一** 的 CLI slug 注册到 `DeveloperCommandHandler`，避免不同 Plan 之间互相抢占入口或重复注册。

- **capability-name 与 slug 分离**：
  - capability-name 用于 Plan / EXET / TEST 产物命名，例如 `geometry-analysis-tools`。
  - CLI slug 用于命令入口命名，例如 `geometry-analysis-smoke-test`。
  - 二者应语义对应，但**不是同一个占位符**，不要把 TEST 文件夹名写成 `..._TEST_<cli-slug>/`。
- **slug 命名**：形如 `<feature>-smoke-test`，全小写、短横线分隔，必须全仓唯一。
- **注册入口**：每个能力的 slug 只能由
  `Project_Test/<YYMMDD>_TEST_<capability-name>/DeveloperCommandHandler.<Feature>SmokeTest.cs`
  这一个 partial 文件承担注册，但该文件**不得**实现 `RegisterExtensionHandlers()` 聚合入口。
  `RegisterExtensionHandlers()` 只能由
  `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs` 主文件持有唯一实现。
  新能力应按既有 hook 聚合模式接入：
  - 在 `DeveloperCommandHandler.cs` 声明
    `partial void Register<Feature>Handlers();`
  - 在 `DeveloperCommandHandler.cs` 的 `RegisterExtensionHandlers()` 聚合实现中追加
    `Register<Feature>Handlers();`
  - 在本能力的 `Project_Test/.../DeveloperCommandHandler.<Feature>SmokeTest.cs` 中实现
    `partial void Register<Feature>Handlers()`，并写入
    `_extensionHandlers["<feature>-smoke-test"] = Handle<Feature>SmokeTest;`。
  这样每个能力仍然只拥有一个注册 partial 文件，同时避免多个测试文件重复实现同一个
  `RegisterExtensionHandlers()` partial 方法。
- **pluginMode 分派在入口内部完成**：是否需要按 `McpRhinoPlugin.Instance is null` 分派，由 `Handle<Feature>SmokeTest` 内部自行决定；不允许为同一能力再申请第二条 live 专属 slug。
- **Rhino live smoke 命令也必须能力独占**：每次新能力的 live smoke 都应新增一个专属 `RhinoCommand`，例如 `McpGeometryAnalysisSmokeCommand` 对应 `_McpGeometryAnalysisSmoke`，并在命令内部直接调用该能力自己的 smoke slug。不再使用共享的 `_McpDevSmoke` 入口，以便多个功能组能在 Rhino 内并行复核。
- **slug 归属**：一条 slug 一旦出现在某份 Plan / TEST / EXET 中即视为该能力独占，后续能力不得改写其语义；若回收复用，必须在新的 Plan / EXET 中显式声明。
