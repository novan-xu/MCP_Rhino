# Project_Test

本项目全部 **轻量化冒烟测试单元（Smoke Test Unit）** 的归档根目录。

> 当前仅提供开发者级别的冒烟测试，用于验证 `Tool → Skill → Service → Rhino FileIO` 全链路在真实 `.3dm` 文件上的行为。
> 尚未接入 xUnit / NUnit 等正式测试框架。

---

## 1. 目录约定

```
Project_Test/
├── README.md                                    # 本文档：命名与生成约定
└── <YYMMDD>_TEST_<slug>/                        # 单个测试单元，对应同名 plan
    └── DeveloperCommandHandler.<Feature>SmokeTest.cs
```

**一个 plan 一个测试文件夹**，强一一对应：

| Project_Plan                                        | Project_Test                                          |
| --------------------------------------------------- | ----------------------------------------------------- |
| `260420_PLAN_geometry-create-modify-tools.md`       | `260420_TEST_geometry-create-modify-tools/`           |
| `260415_PLAN_rhino-object-editing-agent.md`         | `260415_TEST_rhino-object-editing-agent/`（未来补）   |

命名规则：

- 目录前缀 `<YYMMDD>` 与 plan 前缀完全一致。
- 目录后段 `<slug>` 与 plan 文件的 `<slug>` 完全一致（plan 用 `_PLAN_` 连接，测试用 `_TEST_` 连接）。
- 若同一 plan 因迭代需要补测，追加文件到原目录即可，不新建日期前缀。

---

## 2. 运行时产物目录（runtime output）

冒烟测试运行时生成的工作副本 / 归档 / 报告一律写入：

```
_validation/<slug>/
  ├── <working-copy>.3dm       # 可读写工作副本（从 test-files/ 拷贝）
  └── archive/                 # 每次运行的时间戳归档
```

- `_validation/` 已在 `.gitignore` 内，产物不进版本库。
- `_validation/` 路径由 `<slug>` 驱动，与 `Project_Test/<YYMMDD>_TEST_<slug>/` 语义一一对应（不含日期前缀，避免路径穿越）。
- 新增测试单元时若采用了新的 `<slug>`，需要在 `.gitignore` 里加一行 `_validation/<slug>/`。

---

## 3. 编译接入

`Project_Test/**/*.cs` 通过 `src/MCP_Rhino.Server/MCP_Rhino.Server.csproj` 的 `<Compile Include="..\..\Project_Test\**\*.cs" LinkBase="Project_Test" />` 自动接入主工程编译。

- 冒烟测试源文件保持 `namespace MCP_Rhino.Server.Infrastructure.CLI`，延续 `DeveloperCommandHandler` 的 `partial class` 扩展方式。
- 不需要为每个测试单元新建 csproj，也不新增 NuGet 依赖。
- 新增/删除测试目录后 `dotnet build` 会自动重新 glob。

---

## 4. CLI 入口注册

每个冒烟测试至少注册一条 CLI 命令，入口在 [src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs](../src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs) 的 `TryHandle` 开关：

```csharp
return args[0].ToLowerInvariant() switch
{
    // ...
    "geometry-smoke-test" => HandleGeometrySmokeTest(args),
    "<feature>-smoke-test" => Handle<Feature>SmokeTest(args),   // 新增
    _ => false
};
```

命令名规则：`<feature>-smoke-test`，全小写、短横线分隔，`<feature>` 与 `<slug>` 头部对齐（可酌情简写）。

调用方式（示例）：

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-smoke-test test-files/MCP_METtest.3dm
```

---

## 5. 单个冒烟测试单元的最低要求

一个合格的轻量化冒烟测试单元必须：

1. **入参单一**：首参数为源 `.3dm` 路径，其它参数有默认值；缺参数时 `Console.Error.WriteLine(用法)` 并 `System.Environment.ExitCode = 1`。
2. **源文件不可变**：通过 `File.Copy` 到 `_validation/<slug>/` 下工作副本进行读写，禁止直接修改 `test-files/` 下的源文件。
3. **退出码语义**：
   - 全部 checkpoint 通过 → `ExitCode = 0`。
   - 任一断言失败、源文件缺失、参数解析失败 → 栈信息打到 `Console.Error` 并 `ExitCode = 1`。
4. **checkpoint 汇报**：维护 `List<string> checkpoints`，每通过一步 `checkpoints.Add("<Step> ok: <guid or summary>")`，结尾统一输出。
5. **断言复用**：使用 `RequireSuccess / RequireFailure / Require*` 系列辅助方法判断 `OperationResponse<T>` 与文件状态。
   - 现阶段每个测试文件内部各自声明这组 helper（见 `260420_TEST_geometry-create-modify-tools/DeveloperCommandHandler.GeometrySmokeTest.cs`）。
   - 一旦跨测试复用明显，可考虑上移到 `Project_Test/_shared/SmokeAssertions.cs`，届时更新本约定。
6. **happy path + hard error 双覆盖**：
   - happy path：每个 Tool 至少一次成功调用，并从写回的 `.3dm` 读回对象做几何/属性断言。
   - hard error：每类业务校验失败至少一次，走 `RequireFailure` 断言 `response.Success == false`。
7. **净对象数守恒**（如果不守恒则显式说明）：在测试尾端用 `GetObjectCount` 对比 `initialCount`，差额需与 checkpoint 语义一致。

---

## 6. 新增测试单元的操作步骤

写一条 plan `Project_Plan/<YYMMDD>_PLAN_<slug>.md` 之后：

1. 建目录：`Project_Test/<YYMMDD>_TEST_<slug>/`。
2. 在目录内建 `DeveloperCommandHandler.<Feature>SmokeTest.cs`，声明 `namespace MCP_Rhino.Server.Infrastructure.CLI` 并扩展 `partial class DeveloperCommandHandler`，方法名 `Handle<Feature>SmokeTest(string[] args)`。
3. 在 [DeveloperCommandHandler.cs](../src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs) `TryHandle` 开关中加一条 `"<feature>-smoke-test" => Handle<Feature>SmokeTest(args)`。
4. 如果新 `<slug>` 需要独立的 runtime 目录，在 `.gitignore` 内加 `_validation/<slug>/`。
5. 若测试需要的 Skill / Service 尚未注入 `DeveloperCommandHandler`，在 [DeveloperCommandHandler.cs](../src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs) 构造函数中补依赖（并确保 `DependencyInjection.cs` / `AgentRegistration.cs` 已注册）。
6. `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 验证通过。
7. `dotnet run --project src/MCP_Rhino.Server -- <feature>-smoke-test test-files/<fixture>.3dm` 验证通过。
8. 在 `Project_Execute/<YYMMDD>_EXET_<slug>.md` 执行总结中列出 checkpoint 清单与退出码。

---

## 7. 现有测试单元

| 目录                                            | CLI 命令                | Plan                                                                           | 关注范围                                          |
| ----------------------------------------------- | ----------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------- |
| [260420_TEST_geometry-create-modify-tools](./260420_TEST_geometry-create-modify-tools/) | `geometry-smoke-test`   | [260420_PLAN_geometry-create-modify-tools.md](../Project_Plan/260420_PLAN_geometry-create-modify-tools.md) | 几何创建 / 修改全链路 24 checkpoint（12 happy + 8 hard error + 4 扩展） |

---

## 8. 暂未覆盖事项（显式 out of scope）

- 未接入 xUnit / NUnit / MSTest，单测项目 `tests/MCP_Rhino.UnitTests`、`tests/MCP_Rhino.IntegrationTests` 仍为空脚手架。
- 无测试结果聚合（JSON 报告、HTML 报告、CI 断言）。
- 无性能 / 压力测试基线。
- 无并发冒烟（单工作副本串行执行）。

以上事项如需补齐，另起 plan + test 单元，不在本约定当前迭代内。
