# 260422_EXET_file-import-export-tools

## 对应计划

- 计划文件：`Project_Plan/260422_PLAN_file-import-export-tools.md`
- 执行日期：2026-04-23

## 关联产物

- 测试目录：`Project_Test/260422_TEST_file-import-export-tools/`
- Rhino live smoke 命令：`_McpFileImportExportSmoke`
- CLI smoke slug：`file-import-export-smoke-test`
- commit hash / PR：本次会话未生成

## 执行结果 / 实际落地范围

本次已完成以下代码落地：

- 新增 6 个 export tool：
  - `ExportToDwgTool`
  - `ExportToDxfTool`
  - `ExportToIfcTool`
  - `ExportToStlTool`
  - `ExportToImageTool`
  - `ExportToPdfTool`
- 新增 2 个 reference tool：
  - `ListWorksessionAttachmentsTool`
  - `UpdateLinkedBlockTool`
- 新增 `RhinoFileExportService`、`RhinoExternalReferenceService`
- 新增 live adapter：
  - `LiveRhinoFileExporter`
  - `LiveRhinoExternalReferenceManager`
- 新增 export / reference 的 request、response、domain model、enum
- DI 注册完成，工具可被 MCP 反射发现
- 新增能力专属 Rhino 命令 `_McpFileImportExportSmoke`
- 新增测试入口 `Project_Test/260422_TEST_file-import-export-tools/DeveloperCommandHandler.FileImportExportSmokeTest.cs`

实际行为：

- `dwg / dxf / ifc / stl` 走 `RhinoDoc.WriteFile(...)`
- `image` 走 `ViewCapture.CaptureToBitmap(...)`
- `pdf` 走 `FilePdf.Create() + AddPage(ViewCaptureSettings) + Write(path)`
- `worksession` 仅支持 live list
- `linked block` 仅支持 refresh existing definition

## 与计划的偏差

1. `formatOptions`
   当前 request schema 已暴露，但 RhinoCommon 的当前落地路径没有把这些选项真正注入各格式导出插件；实现为接受参数并返回 warning `FORMAT_OPTIONS_NOT_APPLIED`，而不是静默忽略。

2. PDF fallback
   本次只实现了 `FilePdf` 主路径，未补 `_-Print` command fallback。若特定 Rhino 环境下 `FilePdf` 路径不稳定，需要后续单独补齐。

3. live smoke 覆盖范围
   本次会话内只实际执行了 CLI fallback smoke，未在 Rhino 插件宿主内执行 `_McpFileImportExportSmoke`。live smoke 代码已接入并编译通过，但其 happy path 结果尚未在本次 EXET 中实测记录。

4. linked block happy path fixture
   现有 `Runtime_Test/` 未提供已挂接 linked block 的现成夹具，因此当前 smoke 主要覆盖了 `UpdateLinkedBlock` 的 missing-definition 硬错误路径；真实 refresh happy path 需后续补 fixture 后再跑。

## 施工中发现并修复的问题

1. `DeveloperCommandHandler` 的 smoke 注册钩子原先只能容纳一个 partial 实现。
   - 现象：新增第二个能力测试文件后会产生 partial method 实现冲突。
   - 处理：把单一 `RegisterExtensionHandlers()` 改成主入口聚合，再拆出 capability-specific partial hook：
     - `RegisterGeometryAnalysisHandlers()`
     - `RegisterFileImportExportHandlers()`

2. `System.Drawing` 在 net8 + CA1416 下需要显式平台约束。
   - 处理：引入 `System.Drawing.Common`，并对 image export / image smoke 的调用点增加 Windows 平台守卫。

3. RhinoCommon 类型行为与 `IDisposable` 假设不一致。
   - `ViewCapture` / `FilePdf` 未按 `IDisposable` 使用，已修正。

## 测试记录

### 1. 构建

命令：

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj --nologo
```

结果：

- Exit code: `0`
- `Build succeeded.`
- Warning: `0`
- Error: `0`

### 2. CLI fallback smoke

命令：

```powershell
dotnet run --project src\MCP_Rhino.Server -- file-import-export-smoke-test Runtime_Test\MCP_rhino_test.3dm
```

结果：

- Exit code: `0`
- 关键输出：
  - `ExportToDwg rejected in CLI fallback`
  - `ExportToDxf rejected in CLI fallback`
  - `ExportToIfc rejected in CLI fallback`
  - `ExportToStl rejected in CLI fallback`
  - `ExportToImage rejected in CLI fallback`
  - `ExportToPdf rejected in CLI fallback`
  - `ListWorksessionAttachments rejected in CLI fallback`
  - `UpdateLinkedBlock rejected in CLI fallback`

结论：

- 8 个 live-only tool 在 CLI fallback 模式下均按预期返回 live-only 拒绝路径。
- 本次会话未对 `Runtime_Test/MCP_rhino_test.3dm` 做离线写回。

### 3. live smoke

命令入口已接入：

```text
_McpFileImportExportSmoke
```

本次会话执行情况：

- 未执行
- 原因：当前会话未处于 Rhino plugin live 宿主内

## 验收判断对齐

已满足：

- `dotnet build ...` 通过
- CLI fallback live-only 错误路径已覆盖
- 工具、服务、adapter、DI、Rhino smoke 命令、测试入口均已接通

部分满足 / 待 live 验证：

- `ExportToDwg / Dxf / Ifc / Stl / Image / Pdf` 的真实 Rhino happy path
- `ListWorksessionAttachments` 与 Rhino 当前 worksession 状态一致性
- `UpdateLinkedBlock` 的真实 linked block refresh happy path
- Export 后 Rhino `doc.Path` / undo serial / object count 的 live 断言

## 回退验证

- 变更以新增文件为主，现有代码只做了：
  - `DependencyInjection.cs` 注册补充
  - `DeveloperCommandHandler.cs` smoke 注册机制扩展
  - `MCP_Rhino.Server.csproj` 增加 `System.Drawing.Common`
- 未改动既有几何/图层/user text 业务语义
- 本次实测仅执行了 CLI fallback smoke，未对 `.3dm` fixture 写回
- 按文件级回退可直接移除本能力新增文件并撤销上述 3 处接线修改

## 当前遗留项

1. 在 Rhino live 宿主内执行 `_McpFileImportExportSmoke`，补齐真实导出结果与 undo/path 断言记录
2. 为 linked block refresh 准备可复用 fixture，覆盖 happy path
3. 若目标环境出现 PDF 兼容性问题，补 `_-Print` fallback
4. 若后续需要真正支持 `formatOptions`，需额外验证 Rhino 各导出插件的可编程选项注入路径

## 结论

本期 `file-import-export-tools` 已完成主干实现并通过构建与 CLI fallback smoke。当前代码已可进入 Rhino live 环境做最终 happy path 验证；剩余风险主要集中在 Rhino 宿主内的格式插件行为与 linked block 夹具覆盖，而不是架构接线或编译闭合问题。
