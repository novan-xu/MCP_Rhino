# Project_Test

本项目全部 **轻量化冒烟测试单元（Smoke Test Unit）** 的归档根目录。

> 当前仅提供开发者级别的冒烟测试，用于验证 `Tool → Skill → Service → Rhino` 全链路在真实 `.3dm`
> 文件上的行为。尚未接入 xUnit / NUnit 等正式测试框架。

---

## 1. 目录约定

```text
Project_Test/
├── README.md
└── <YYMMDD>_TEST_<capability-name>/
    └── DeveloperCommandHandler.<Feature>SmokeTest.cs
```

一个 plan 对应一个测试文件夹，命名与产物主体保持严格一致：

- `Project_Plan/260422_PLAN_geometry-analysis-tools.md`
- `Project_Exet/260422_EXET_geometry-analysis-tools.md`
- `Project_Test/260422_TEST_geometry-analysis-tools/`

命名指南：

- 目录前缀 `<YYMMDD>` 与对应 Plan / EXET 完全一致。
- 目录后段 `<capability-name>` 与对应 Plan / EXET 的 `<capability-name>` 完全一致。
- 若同一 plan 因迭代需要补测，追加文件到原目录即可，不新建新的日期前缀。

---

## 2. 测试输入约定

测试统一直接使用 `Runtime_Test/` 下的 `.3dm` fixture。

- 不再引入 `_validation/<slug>/` 工作副本约定。
- 冒烟测试应优先选择只读路径；若测试本身涉及 mutation，必须在同一轮 smoke 末尾恢复 fixture 基线状态，并用断言证明状态已回归。
- EXET 文档中的「测试记录」只记录实际使用的 `Runtime_Test/...` 路径，不再写工作副本路径。

---

## 3. 编译接入

`Project_Test/**/*.cs` 通过
`src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` 中的
`<Compile Include="..\\..\\Project_Test\\**\\*.cs" LinkBase="Project_Test" />`
自动接入主工程编译。

- 冒烟测试源文件保持 `namespace MCP_Rhino.Server.Infrastructure.CLI`。
- 继续采用 `DeveloperCommandHandler` 的 `partial class` 扩展方式。
- 不需要为每个测试单元新建 csproj，也不新增 NuGet 依赖。

---

## 4. CLI 入口注册

每个冒烟测试至少注册一条 CLI 命令。当前仓库约定通过
[DeveloperCommandHandler.cs](/C:/01_Projects/MCP_Rhino/src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs)
里的 partial 扩展钩子注册，而不是修改主 `TryHandle` switch。

```csharp
partial void RegisterExtensionHandlers();

partial void RegisterExtensionHandlers()
{
    _extensionHandlers["<feature>-smoke-test"] = Handle<Feature>SmokeTest;
}
```

约定：

- TEST 文件夹名用 `<capability-name>`，CLI 命令名用 `<feature>-smoke-test`，两者语义对应，但不是同一个占位符。
- 同一能力只允许一个 partial 文件实现 `RegisterExtensionHandlers()`。
- `TryHandle` 会先查 `_extensionHandlers`，只有未命中时才走主 switch。
- Rhino 内 live smoke 也必须使用每期独立的 Rhino 命令名，不再共用 `_McpDevSmoke` 这类统一入口。

示例：

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-analysis-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

```text
_McpGeometryAnalysisSmoke
```

---

## 5. 单个冒烟测试单元的最低要求

一个合格的轻量化冒烟测试单元必须：

1. **入参单一**：首参数为 `.3dm` 路径，其它参数有默认值；缺参数时输出用法并返回失败。
2. **统一使用 `Runtime_Test/` fixture**：不再复制工作副本；若测试会写文档，必须在测试结束前恢复到基线状态。
3. **退出码清晰**：
   - 全部 checkpoint 通过 → `ExitCode = 0`
   - 任一断言失败、源文件缺失、参数解析失败 → `ExitCode = 1`
4. **checkpoint 汇报**：维护 `List<string> checkpoints`，每通过一步追加一条记录，结尾统一输出。
5. **断言复用**：使用 `RequireSuccess / RequireFailure / Require*` 这类 helper 判断 `OperationResponse<T>` 与文档状态。
6. **happy path + hard error 双覆盖**：
   - happy path：每个 Tool 至少一次成功调用，并直接从 `Runtime_Test` 中的目标文档读回状态做断言。
   - hard error：每类业务校验失败至少一次。
7. **状态守恒或显式回归**：
   - 只读测试要证明对象数 / 图层数 / 文档字符串数 / user text key 数不变。
   - 写入测试若会改动 fixture，必须在测试末尾显式回归到基线状态，并验证回归成功。

---

## 6. 新增测试单元的操作步骤

写一条 plan `Project_Plan/<YYMMDD>_PLAN_<capability-name>.md` 之后：

1. 建目录：`Project_Test/<YYMMDD>_TEST_<capability-name>/`
2. 在目录内建 `DeveloperCommandHandler.<Feature>SmokeTest.cs`
3. 声明 `namespace MCP_Rhino.Server.Infrastructure.CLI` 并扩展 `partial class DeveloperCommandHandler`
4. 在测试文件里实现 `partial void RegisterExtensionHandlers()`，注册 `"<feature>-smoke-test"`
5. 若测试需要的 Skill / Service 尚未注入 `DeveloperCommandHandler`，补齐构造参数与 DI 注册
6. `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo`
7. `dotnet run --project src/MCP_Rhino.Server -- <feature>-smoke-test Runtime_Test/<fixture>.3dm`
8. 在 `Project_Exet/<YYMMDD>_EXET_<capability-name>.md` 中记录 checkpoint、退出码与验收结果

---

## 7. 现有测试单元

| 目录 | CLI 命令 | Plan | 关注范围 |
| --- | --- | --- | --- |
| [260420_TEST_geometry-create-modify-tools](./260420_TEST_geometry-create-modify-tools/) | `geometry-smoke-test` | [260420_PLAN_geometry-create-modify-tools.md](../Project_Plan/260420_PLAN_geometry-create-modify-tools.md) | 几何创建 / 修改全链路 smoke |

---

## 8. 暂未覆盖事项

- 未接入 xUnit / NUnit / MSTest
- 无测试结果聚合（JSON / HTML / CI 报告）
- 无性能 / 压力测试基线
- 无并发冒烟

以上事项如需补齐，另起新的 plan + test 单元，不在本 README 当前约定内。

