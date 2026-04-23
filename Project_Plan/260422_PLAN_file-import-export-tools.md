# 260422_PLAN_file-import-export-tools

## 背景

MCP_Rhino 目前完全没有"跨文件 / 跨格式"的能力。LLM 既无法把当前 `.3dm` 导出成 dwg /
dxf / ifc / stl / pdf / jpg / png（下游施工 / 工艺 / 出图常用格式），也无法以**外部参考**
形态把别的 `.3dm` 拉进当前会话（worksession / linked block）—— 而这两个操作恰恰是 Rhino
工程化协作里最常见的"出图打包"与"多人协同"入口。

经本机 `Rhino 8` 的 `RhinoCommon.xml`（项目当前引用路径）核对，当前**已验证**可用的相关 API 有：
- 导出：`RhinoDoc.WriteFile(path, FileWriteOptions)` —— 通过文件扩展名自动匹配文件类型
  插件（dwg / dxf / iges / step / stl / ply / obj / 3ds / fbx / x_t / x_b / sat / ifc 等）。
  同时 `FileWriteOptions` 已验证存在 `WriteSelectedObjectsOnly` / `WriteUserData` /
  `UpdateDocumentPath`。
- 视图捕获：`RhinoView.CaptureToBitmap(...)` 可直接得到位图；PDF 路径已验证存在
  `Rhino.FileIO.FilePdf.Create()` + `AddPage(ViewCaptureSettings)` + `Write(path)`，以及
  `ViewCaptureSettings(RhinoView, Size, dpi)`。
- Worksession：`RhinoDoc.Worksession`（`Rhino.DocObjects.Worksession`）已验证提供
  `ModelCount` / `ModelPaths` 等**读取**成员，可用于列出现有 worksession 挂载。
- Linked Block：已验证存在 `InstanceDefinitionTable.RefreshLinkedBlock(InstanceDefinition)`、
  `InstanceDefinition.LayerStyle` / `SourceArchive` / `ArchiveFileStatus`，
  以及 `ObjectTable.AddInstanceObject(...)`。

相对地，本轮 API 核对**没有在当前 RhinoCommon 8.17 XML 中验证到**：
- `RhinoView.CaptureToFile(...)`
- `Rhino.UI.Print.PrintInstance`
- `RhinoDoc.Worksession.Attach / Detach / Refresh`
- `InstanceDefinitionTable.AddLinked(...)`

这些能力不等于绝对做不到，但至少说明它们**不能继续作为本期的“已确认托管 API 路径”写进计划**；
若要保留，必须另做 command-macro / spike 验证。

按 `MCP_Rhino Architecture.md` 中"任何写入 / 改变文档状态的能力都强制 live"原则：
- **Export**：`RhinoDoc.WriteFile` 不改变 ActiveDoc 的文档状态，但需要从 `RhinoDoc` 拿几何序列化 —
  本质上是 read 行为。本期 Export 默认走 **Live**（`RhinoDoc.ActiveDoc`），原因是 RhinoCommon 的
  WriteFile 必须基于运行中 doc，且部分格式（pdf / jpg / png）依赖 viewport；同时提供 offline 读
  → 临时构造 RhinoDoc 的能力代价过大、收益不足，本期不做。
- **Import / External Reference**：本期仅保留**已验证托管 API 支撑**的子集。
  - worksession 保留 **read-only list**；
  - linked block 保留 **refresh existing linked definition**；
  - worksession 的 attach / detach / refresh，以及 linked block 的 create/add，移出本期。

非目标：
- 通用 Import（把 dwg / dxf / iges / step 转成本地 Rhino 几何）：与"在线建模一致性"冲突，且与
  "外部参考"语义相比 LLM 用例稀疏，本期不做。
- 把别的 `.3dm` 内的几何"copy paste"到当前文档：worksession / linked block 已经能覆盖典型协作
  需求；如确实需要 "import-and-bake" 形态，可在后续基于 linked block 之上扩展 "Bind Linked Block"。

## 目标

- **导出（Export）共 6 个 Tool（live-only，全部不修改 ActiveDoc 状态，不进 Undo record）**：
  - `ExportToDwgTool` / `ExportToDxfTool`：基于 `RhinoDoc.WriteFile`，输出 dwg / dxf。
  - `ExportToIfcTool`：基于 `RhinoDoc.WriteFile`，输出 ifc（依赖 Rhino 内置 IFC 导出插件已加载）。
  - `ExportToStlTool`：基于 `RhinoDoc.WriteFile`，输出 stl。
  - `ExportToImageTool`：基于 `RhinoView.CaptureToBitmap(...)` / `ViewCapture`，输出 jpg / png / bmp / tiff。
  - `ExportToPdfTool`：基于 `Rhino.FileIO.FilePdf` + `ViewCaptureSettings`，输出 PDF。
- **外部参考（Reference）本期收窄为 2 个 Tool**：
  - `ListWorksessionAttachmentsTool`：读取当前 `RhinoDoc.Worksession.ModelPaths`，列出现有挂载。
  - `UpdateLinkedBlockTool`：对**已存在**的 linked definition 调
    `InstanceDefinitionTable.RefreshLinkedBlock(...)`。
- Export v1 的公开 contract 先收窄到**能稳定落地的最小集**：
  - `ExportToDwg / Dxf / Ifc / Stl`：接受 `outputPath` + 可选 `selectedObjectIds[]` +
    `overwriteExisting` + `formatOptions`。
  - `ExportToImage`：接受 `outputPath` + `viewName` + `imageSizePx{width,height}` +
    `dotsPerInch` + `backgroundTransparent` + `overwriteExisting`。
  - `ExportToPdf`：接受 `outputPath` + `viewName` + `pageSizeMm{width,height}` +
    `dotsPerInch` + `overwriteExisting`。
  - defaults：
    - `overwriteExisting=true`
    - `ExportToImage` 缺省 `imageSizePx=1920x1080`、`dotsPerInch=96`
    - `ExportToPdf` 缺省 `pageSizeMm=A3 横向(420x297)`、`dotsPerInch=300`
  - **本期不把** `SelectedLayers[]` / `ByLayer` / `VisibleOnly` 暴露进 request schema；等 RhinoCommon
    路径与 smoke 证明稳定后再扩。
- 本期的外部参考 contract 只承诺：
  - `ListWorksessionAttachments(filePath)`
  - `UpdateLinkedBlock(filePath, definitionNames[])`
  不引入 `AttachWorksession / DetachWorksession / RefreshWorksession / AddLinkedBlock` 的 request schema。

## 架构归属

- **Tools/File/**（既有目录，目前装 DocumentUserString）—— 在内部按子能力分两个子目录：
  - **Tools/File/Export/** —— 6 个 Export Tool。
  - **Tools/File/Reference/** —— 2 个 Worksession / LinkedBlock Tool。
- **Application/Services/** —— 新增：
  - `RhinoFileExportService`：承载 `ExportTo<Format>(...)` 6 个方法；暴露统一的 "writefile +
    optional view capture" 路径；不区分 dwg / dxf / ifc / stl 这种"WriteFile-style"路径与
    image / pdf 这种"Capture / Print-style"路径，由内部 strategy 分派（见关键设计 #2）。
  - `RhinoExternalReferenceService`：本期仅承载 `ListWorksessionAttachments / UpdateLinkedBlock`。
- **Application/Interfaces/** —— 新增：
  - `ILiveFileExporter`：方法 `Export(RhinoDoc doc, FileExportSpec spec)`，分派 dwg / dxf / ifc / stl / image / pdf。
  - `ILiveExternalReferenceManager`：方法 `ListWorksession(RhinoDoc doc)` /
    `UpdateLinkedBlock(...)`。
- **Infrastructure/Rhino/Live/** —— 新增：
  - `LiveRhinoFileExporter.cs`：所有 `RhinoDoc.WriteFile` / `RhinoView.CaptureToBitmap` /
    `Rhino.FileIO.FilePdf` / `ViewCaptureSettings` 调用集中在此。
  - `LiveRhinoExternalReferenceManager.cs`：本期仅集中
    `RhinoDoc.Worksession.ModelPaths` / `InstanceDefinitions.RefreshLinkedBlock(...)` 调用。
- **Domain/Models/** —— 新增：
  - `FileExportSpec`：`OutputPath / Format(EnumExport) / OverwriteExisting / SelectedObjectIds[] /
    ViewName? / ImageSizePx{Width, Height}? / PageSizeMm{WidthMm, HeightMm}? / DotsPerInch? /
    BackgroundTransparent? / FormatOptions(IDictionary<string,string>)`。
  - `WorksessionAttachmentResult`：`SourceFilePath / Status(Attached|Stale) / Attached(bool) /
    WasAlreadyAttached(bool) / Message`。
  - `LinkedBlockResult`：`DefinitionId / DefinitionIndex / DefinitionName / InstanceObjectId? /
    SourceFilePath / Success(bool) / Updated(bool) / WasAlreadyDefined(bool) / Message(string)`。
- **Domain/Enums/** —— 新增：
  - `FileExportFormat`：Dwg / Dxf / Ifc / Stl / Pdf / Jpg / Png / Bmp / Tiff。
  - `WorksessionAttachmentStatus`：Attached / Stale。
- **Contracts/Requests/** —— 8 个新 Request DTO + 若干 entry：
  - 每个 Export Tool 1 个 Request：`ExportToDwgRequest` / `ExportToDxfRequest` /
    `ExportToIfcRequest` / `ExportToStlRequest` / `ExportToImageRequest` / `ExportToPdfRequest`。
  - Reference：`ListWorksessionAttachmentsRequest` / `UpdateLinkedBlockRequest`。
- **Contracts/Responses/** —— 新增：
  - `FileExportResponse`：`FilePath / OutputPath / Format / ExportedObjectCount /
    OutputFileSizeBytes / DurationMs / Warnings`。
  - `WorksessionAttachmentResponse`：（`FilePath / AttachedFiles[] / Warnings / Results`）。
  - `LinkedBlockMutationResponse` + `LinkedBlockResultResponse`：（FilePath / Results /
    SucceededCount / FailedCount / Warnings）。
- **Server/DependencyInjection.cs** —— 注册 2 个接口实现 + 2 个 Service。`ToolRegistration.cs`
  无需改动（反射自动发现）。
- **Skills / Agents** —— 一期不新增 Skill：导出 / 外部参考粒度自洽，没有 "Export 多种格式 + 自动归档"
  这种复合流程。如后续浮现"出图：导 dwg + 渲染 jpg + 打 PDF + 上传 OneDrive"这类工序，再抽
  `OutputPackagingSkill`。

## 关键设计

1. **Export 全部 live-only**
   - 走 `_documentAccessor.Execute(filePath, doc => exporter.Export(doc, spec))`；
   - 不开 Undo record（导出不改 ActiveDoc 内容）；
   - 校验 `OutputPath` 父目录存在 + 目标路径不等于当前 ActiveDoc 路径（无论扩展名）。
   - `RhinoDoc.WriteFile(...)` 前显式设置 `FileWriteOptions.UpdateDocumentPath = false`，因为当前
     RhinoCommon 文档说明明确写着 `WriteFile` 会改 active document name/path；本期 export 必须把这个
     副作用压掉。

2. **Export 内部 strategy 分派**
   - dwg / dxf / ifc / stl：纯 `RhinoDoc.WriteFile(path, FileWriteOptions)`。
      - `FileWriteOptions.SuppressDialogBoxes = true`（避免阻塞 UI 线程）。
      - 若 `SelectedObjectIds.Any()`：`FileWriteOptions.WriteSelectedObjectsOnly = true`；先把
        `SelectedObjectIds` 经 `doc.Objects.Select(...)` 标记选中，导出后恢复原选中态。
      - 若 `SelectedObjectIds` 为空：导全部对象。
      - `FileWriteOptions.WriteUserData = true`，保留 user text / 文档属性。
      - 若 `SelectedObjectIds` 非空但全部未在 live document 中解析到 → 单次请求硬错误。
      - `formatOptions` 本期先只接受进 schema，不真正注入导出插件；若传入则返回
        `FORMAT_OPTIONS_NOT_APPLIED` warning。
    - jpg / png / bmp / tiff：`RhinoView.CaptureToBitmap(...)` 或 `ViewCapture.CaptureToBitmap(...)`
      先得到 `Bitmap`，再按目标扩展名保存到文件；
      `Format` 自动从 `outputPath` 扩展名推导，支持 `.jpg/.jpeg/.png/.bmp/.tif/.tiff`；
      `ViewName` 缺省取 `doc.Views.ActiveView.MainViewport.Name`；尺寸来自 `ImageSizePx`，不复用 PDF 的
      毫米纸张模型。
      - 依赖 `System.Drawing` 写盘，**仅 Windows 可用**；非 Windows → 硬错误。
    - pdf：优先走 `Rhino.FileIO.FilePdf.Create()` + `AddPage(new ViewCaptureSettings(view, mediaSize, dpi))`
      + `Write(path)`；
      `PageSizeMm` 缺省 A3 横向；DPI 缺省 300。
    - 任何格式调用前 `Stopwatch.StartNew()`，调用后写入 `DurationMs` 与 `OutputFileSizeBytes`。
    - `ExportedObjectCount` 对 `dwg / dxf / ifc / stl` 表示实际写出的对象数；对 `image / pdf`
      表示调用时 live document 中的非删除对象总数，仅作文档级 telemetry，不代表最终画面中实际可见对象数。

3. **Export `SelectedObjectIds` 实现的并发安全**
   - 仅 dwg / dxf / ifc / stl 支持 `SelectedObjectIds`。
   - `SelectedObjectIds.Any()` 时：
     1. 缓存 `doc.Objects.GetSelectedObjects(includeLights:true, includeGrips:true)` 当前选中
        ObjectId 列表。
     2. `doc.Objects.UnselectAll()`；按 `SelectedObjectIds` 重新标记。
     3. WriteFile / 命令执行后，恢复缓存的选中态。
   - 整段流程在 `ExecuteWithUndo` 之外（导出本身不改文档）；对 viewport 有视觉副作用（短暂选中变化），
     文档建议用户在跑 Tool 期间避免人工选择。

4. **Worksession 本期仅做 list，不做 mutation**
   - 本轮 API 核对只验证到 `RhinoDoc.Worksession.ModelCount / ModelPaths` 等读取成员。
   - 因此本期只做 `ListWorksessionAttachments`，走 `_documentAccessor.Execute(...)`；
     不承诺 attach / detach / refresh。

5. **LinkedBlock 本期只做 update existing**
   - 已验证存在 `InstanceDefinitions.RefreshLinkedBlock(InstanceDefinition)`，可刷新现有 linked definition。
   - 本期 `UpdateLinkedBlock` 先走 `_documentAccessor.Execute(...)`，并在 smoke 中实测 Rhino 是否为其创建
     Undo 条目；在 EXET 里按实测结果记录，Plan 先不对 Undo 语义过度承诺。
   - 由于未验证到 `InstanceDefinitions.AddLinked(...)`，`AddLinkedBlock` 移出本期。

6. **WriteFile 路径校验**
   - `OutputPath` 必须为绝对路径，否则硬错误。
   - 父目录不存在 → 硬错误（不自动创建，避免误写到意外位置）。
   - 目标已存在文件 → 默认覆盖，但记录 `EXPORT_OVERWRITING_EXISTING_FILE` warning；提供
      `OverwriteExisting=false` 选项强制拒绝覆盖。
   - 目标路径与当前 ActiveDoc 路径相同（无论扩展名）→ 硬错误，避免误覆盖源文件。

7. **PDF 路径（v1 单路径）**
   - 本期只实现 `Rhino.FileIO.FilePdf.Create()` + `AddPage(ViewCaptureSettings)` + `Write(path)`。
   - 若后续在真实 Rhino 会话里发现 `FilePdf + ViewCaptureSettings` 兼容性问题，再单独补
     `RhinoApp.RunScript("_-Print ...", echo:false)` fallback。
   - 当前版本下该 managed path 失败时，单次调用直接失败，Message 含触发的异常信息。

8. **Image 导出的 viewport 选择**
   - `ViewName` 缺省取 `doc.Views.ActiveView`；显式指定时遍历 `doc.Views` 找匹配的 `View.MainViewport.Name`。
   - 命中 0 view → 硬错误；命中多 view（重名） → 取第一个 + warning。
   - image / pdf 本期只导出当前 viewport 完整画面；不暴露 selected-only / by-layer / visible-only
     语义，避免 schema 先于实现失真。

9. **批量 Export（不在本期）**
   - 一次 Tool 调用只导出一种格式 / 一份文件。批量"导一组 dwg + 一组 jpg" 由客户端串多次 Tool 调用
     完成；如未来浮现高频复合需求，再抽 `BatchExportSkill`。

10. **Worksession list 的状态表示**
    - `ListWorksessionAttachments` 通过 `RhinoDoc.Worksession.ModelPaths` 枚举；
      当前实现基于 `File.Exists(SourceFilePath)` 推断：
      - `Attached`：挂载存在且磁盘文件存在
      - `Stale`：挂载存在但磁盘文件缺失
    - 若后续 probe 能稳定拿到更细的状态，再补更多状态字段。

11. **Linked Block 的更新策略**
    - `UpdateLinkedBlock`：先按 `definitionNames[]` 解析到现有 `InstanceDefinition`，
      再调 `InstanceDefinitions.RefreshLinkedBlock(definition)`。
    - 若 definition 不存在 / 不是 linked definition / 源文件不可用 → per-entry 失败。

12. **错误码沿用**
    - 所有 live 路径错误码（`LIVE_RHINO_REQUIRED` / `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` /
      `FILE_NOT_ACTIVE` / `RHINO_MAIN_THREAD_BUSY`）由 `ILiveRhinoDocumentAccessor` 自动返回，
      不重复包装。
    - 新错误码：
      - `EXPORT_OUTPUT_PARENT_NOT_FOUND`：父目录不存在。
      - `EXPORT_OUTPUT_OVERWRITE_BLOCKED`：禁止覆盖原 `.3dm` / 设置了 `OverwriteExisting=false`。
      - `EXPORT_VIEW_NOT_FOUND`：image / pdf 指定 `ViewName` 命中 0 view。
      - `LINKED_BLOCK_DEFINITION_NOT_FOUND`：UpdateLinkedBlock 时未找到 definition。
   - 运行时 warnings：
      - `EXPORT_OVERWRITING_EXISTING_FILE`：目标文件已存在，本次被覆盖。
      - `SELECTED_OBJECT_IDS_NOT_FOUND`：`selectedObjectIds` 有部分未在 live document 中解析到。
      - `IMAGE_TRANSPARENCY_UNSUPPORTED`：JPG 请求透明背景时自动退化为不透明背景。
      - `EXPORT_VIEW_NAME_DUPLICATE`：显式 `ViewName` 命中多个重名 view，取首个并告警。
      - `FORMAT_OPTIONS_NOT_APPLIED`：`formatOptions` 已被请求接受，但当前导出路径未真正消费。

## 涉及文件

**新增（Tools）** —— 8 个文件：
- `src/MCP_Rhino.Server/Tools/File/Export/{ExportToDwg,ExportToDxf,ExportToIfc,ExportToStl,ExportToImage,ExportToPdf}Tool.cs`
- `src/MCP_Rhino.Server/Tools/File/Reference/{ListWorksessionAttachments,UpdateLinkedBlock}Tool.cs`

**新增（Application）** —— 4 个文件：
- `src/MCP_Rhino.Server/Application/Services/RhinoFileExportService.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoExternalReferenceService.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveFileExporter.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveExternalReferenceManager.cs`

**新增（Infrastructure）** —— 2 个文件：
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoFileExporter.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveRhinoExternalReferenceManager.cs`

**新增（Domain）**：
- `src/MCP_Rhino.Server/Domain/Models/FileExportSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/WorksessionAttachmentResult.cs`
- `src/MCP_Rhino.Server/Domain/Models/LinkedBlockResult.cs`
- `src/MCP_Rhino.Server/Domain/Enums/FileExportFormat.cs`
- `src/MCP_Rhino.Server/Domain/Enums/WorksessionAttachmentStatus.cs`

**新增（Contracts）**：8 个 Request + 4 个 Response，路径 `src/MCP_Rhino.Server/Contracts/{Requests,Responses}/`。

**新增（Test）**：
- `Project_Test/260422_TEST_file-import-export-tools/`，包含：
  - `DeveloperCommandHandler.FileImportExportSmokeTest.cs` —— 本能力唯一 smoke 入口；注册
    `file-import-export-smoke-test`，并在入口内部按 pluginMode 分派 CLI fallback / live smoke。
  - `FileExportSmokeTest.cs`（Export 6 个 Tool 的 happy path + 边界 + 写盘断言）。
  - `ExternalReferenceSmokeTest.cs`（`ListWorksessionAttachments` + `UpdateLinkedBlock` 的 happy path +
    边界断言）。
  - `samples/`：一份小型 `.3dm` 作为 linked block 刷新源文件；测试结束清理生成的导出
    产物（dwg / stl / jpg / pdf 等）。

**修改**：
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs` —— 注册 2 个 interface 实现 + 2 个 Service。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpFileImportExportSmokeCommand.cs` ——
  本能力专属 Rhino live smoke 命令 `_McpFileImportExportSmoke`，内部直接调用
  `file-import-export-smoke-test`。

**复用（不改）**：
- `ILiveRhinoDocumentAccessor`（`Execute` / `ExecuteWithUndo`）。
- `ObjectEditWarning`（warning code 体系，新增 6 个 code）。
- `OperationResponse<T>`（统一响应壳）。

## 使用方式

MCP Tool 调用示例：

- 导出全部为 dwg：`ExportToDwg(filePath, outputPath="C:/out/site.dwg")`
- 仅导选中曲面为 stl（用于 3D 打印）：`ExportToStl(filePath, outputPath, selectedObjectIds=[g1,g2])`
- 视图截屏：`ExportToImage(filePath, outputPath="C:/out/perspective.png", viewName="Perspective", imageSizePx={Width:1920, Height:1080}, dotsPerInch=144, backgroundTransparent=true)`
- 出图 PDF：`ExportToPdf(filePath, outputPath, viewName="Top", pageSizeMm={WidthMm:420, HeightMm:297}, dotsPerInch=300)`
- 列出当前 worksession：`ListWorksessionAttachments(filePath)`
- 同步外部参考块：`UpdateLinkedBlock(filePath, definitionNames:["COL_TYPE_A"])`

典型流程：
- "把当前 ActiveDoc 出三件套：dwg + 顶视图 PDF + 透视 PNG"：
  `ExportToDwg(...)` → `ExportToPdf(... viewName:"Top" ...)` → `ExportToImage(... viewName:"Perspective" ...)`。
- "校对当前外部引用状态，并刷新已有 linked block"：
  `ListWorksessionAttachments()` → `UpdateLinkedBlock(["COL_TYPE_A"])`。

## 验收标准

构建 / smoke：
- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 无 Warning 通过。
- CLI fallback：`dotnet run --project src/MCP_Rhino.Server -- file-import-export-smoke-test test-files/MCP_rhino_test.3dm`：
  - 所有 8 个 Tool 返回明确的 live-only 错误，优先为 `LIVE_RHINO_REQUIRED`。
  - 工作副本对象数 / 图层数 / user text 无变化。
- Live 手工 smoke（Rhino 内 `_McpFileImportExportSmoke`）：
  - **Export**：
    - `ExportToDwg / Dxf / Ifc / Stl`：目标路径生成有效文件；文件大小 > 0；用 Rhino 重新打开 dwg
      / dxf / stl 能看到对象（IFC 视环境是否安装查看器）；导出后 ActiveDoc 对象数 / Undo 栈无变化；
      `doc.Path` 与文档标题不被导出路径改写。
    - `ExportToImage`：jpg / png / bmp / tiff 文件生成；像素尺寸与请求一致；`backgroundTransparent=true`
      时 png 透明背景生效。
      - 非 Windows 环境下 `ExportToImage` 返回硬错误。
    - `ExportToPdf`：A3 PDF 文件生成；DPI / 页面方向与请求一致。
  - **Reference**：
    - `ListWorksessionAttachments`：返回结果与 Rhino 当前 worksession 挂载路径一致。
    - `UpdateLinkedBlock`：源文件改动后调用，instance object 几何更新。

边界用例：
- `outputPath` 父目录不存在 → `EXPORT_OUTPUT_PARENT_NOT_FOUND`。
- `outputPath` 与 ActiveDoc 路径相同 → `EXPORT_OUTPUT_OVERWRITE_BLOCKED`。
- `OverwriteExisting=false` 且目标已存在 → `EXPORT_OUTPUT_OVERWRITE_BLOCKED`。
- `viewName` 命中 0 view → `EXPORT_VIEW_NOT_FOUND`。
- `UpdateLinkedBlock` 未找到 definition → `LINKED_BLOCK_DEFINITION_NOT_FOUND`。
- `selectedObjectIds` 传入空数组 / null → 退化为“导全部对象”，不单独报错。
- `selectedObjectIds` 非空但全部未在 live doc 中解析到 → 单次请求失败。

副作用 / Undo：
- Export 调用前后：`doc.Objects.Count` / `doc.Layers.ActiveCount` / `doc.UserData` 无变化；Rhino Undo
  History 面板**无新条目**。
- `ListWorksessionAttachments`：只读，无 Undo 条目。
- `UpdateLinkedBlock`：Undo 语义以 Rhino 实测为准，在 EXET 记录；PLAN 不预设。

## 风险与回退方案

风险：
- **WriteFile 行为依赖文件类型插件**：dwg / dxf / ifc / stl 需要 Rhino 内置插件已加载；某些
  Rhino installation 可能未启用 IFC 插件。缓解：`Export` 调用前调
  `Rhino.PlugIns.PlugIn.GetEnabledPlugInList(...)` 检查 plugin 是否启用，未启用 → 硬错误 + 提示
  用户在 Rhino 选项中启用插件。
- **`RhinoDoc.WriteFile` 默认可能改写文档路径/标题**：缓解：显式设置
  `FileWriteOptions.UpdateDocumentPath=false`，并把 `doc.Path` 不变写入 smoke 断言。
- **PDF / image 路径依赖 `ViewCaptureSettings` 与 `FilePdf` 组合的实际行为**：缓解：主路径走已验证存在的
  managed API，并在 EXET 阶段记录目标 Rhino 版本的实测结果；若后续发现 `FilePdf` 兼容性问题，再补
  `RunScript("_-Print ...")` fallback。
- **Image 导出依赖 `System.Drawing`**：缓解：当前实现仅支持 Windows；非 Windows 直接返回硬错误。
- **viewport 截屏受 active view 状态影响**：用户在跑 Tool 时手动切换 view 会影响 image / pdf
  内容。缓解：Tool 文档建议"截屏前不要操作 Rhino UI"；不引入"锁定 view"机制。
- **Worksession mutation API 未在当前 RhinoCommon 中验证到**：缓解：本期不承诺
  `Attach/Detach/RefreshWorksession`，单独起 spike / 后续 plan。
- **Linked block create API 未在当前 RhinoCommon 中验证到**：缓解：本期不承诺 `AddLinkedBlock`，
  后续如需支持，优先评估 command-macro 路径。
- **Linked Block 源文件路径漂移**：源文件移动后 `RefreshLinkedBlock` 失败。缓解：`UpdateLinkedBlock`
  返回 per-definition `Success=false` + Message 含原源路径。
- **Export 大文件性能**：高密度 Brep dwg 导出可能 > 10s 触发主线程超时。缓解：Tool 文档建议
  大模型走分图层 / 分对象批次；不引入 background worker。
- **测试产物清理**：smoke 会生成 dwg / stl / pdf 等文件，需要在测试 teardown 中删除避免污染仓库。

回退方案：
- 单 Tool 失败：从 `ToolRegistration` 黑名单单独下线。
- 整批回退：所有新文件均落在新增子目录 + 新增 Service，不修改既有 Service / Tool / Skill 签名；
  `git revert` 整批 commit 即可还原。
- 回退后 ActiveDoc 状态：Export 不改文档，回退无残留；`UpdateLinkedBlock` 只刷新既有定义，不新增持久状态。

## 后续扩展方向

- **通用 Import**：把 dwg / dxf / iges / step 等格式的几何 import 成本地 Rhino 几何（基于
  `RhinoDoc.ReadFile`）。需先解决"import 几何与既有几何冲突 / layer 命名冲突"策略，待 LLM 用例
  浮现后再做。
- **批量出图 Skill**：`OutputPackagingSkill`，按层 / 按视图组合，一次性导一套 dwg + jpg + pdf
  并打 zip 包。
- **Worksession mutation spike**：验证是否存在稳定的 command-macro 路径可支撑
  `Attach / Detach / RefreshWorksession`。
- **Linked block creation spike**：验证 `command-macro` 或其他受支持 API 是否能稳定创建 linked
  definition，再决定是否恢复 `AddLinkedBlock`。
- **Bind Linked Block**：把 linked block "脱离引用、落地为本地 block definition"，沿用 RhinoCommon
  `InstanceDefinitions.MakeEmbedded(definitionIndex)`。
- **Image / PDF 高级选项**：阴影 / 材质 / 出图样式 / 显示模式（Rendered / Wireframe / Shaded） 选择，
  目前默认走当前 viewport display mode。
- **Worksession 状态查询升级**：增加 `IsModified` / `LastSavedAt` 等元数据，便于 LLM 判断是否需要
  Refresh。
- **导出格式扩展**：iges / step / obj / fbx / 3ds / sat / x_t / x_b / ply / 3mf 等。本期保留最常用的
  6 种，扩展工作量主要是 schema + smoke，不影响架构。
- **导出范围进阶**：后续若 `ByLayer / VisibleOnly / selected-only viewport` 在 RhinoCommon 路径上验证稳定，
  再新增显式字段或新 request 变体；本期不把它们提前写进 schema。

