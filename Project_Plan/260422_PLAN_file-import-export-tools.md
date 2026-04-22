# 260422_PLAN_file-import-export-tools

## 背景

MCP_Rhino 目前完全没有"跨文件 / 跨格式"的能力。LLM 既无法把当前 `.3dm` 导出成 dwg /
dxf / ifc / stl / pdf / jpg / png（下游施工 / 工艺 / 出图常用格式），也无法以**外部参考**
形态把别的 `.3dm` 拉进当前会话（worksession / linked block）—— 而这两个操作恰恰是 Rhino
工程化协作里最常见的"出图打包"与"多人协同"入口。

RhinoCommon v8 已经提供：
- 导出：`RhinoDoc.WriteFile(path, FileWriteOptions)` —— 通过文件扩展名自动匹配文件类型
  插件（dwg / dxf / iges / step / stl / ply / obj / 3ds / fbx / x_t / x_b / sat / ifc 等）。
- 视图捕获：`RhinoView.CaptureToBitmap(...)` / `RhinoView.CaptureToFile(...)` 用于 jpg / png /
  bmp / tiff；PDF 需要走 `_-Print` 命令或 `Rhino.UI.Print.PrintInstance`。
- Worksession：`Rhino.RhinoDoc.WorkSession.Attach(path)` / `Detach(path)` / `Refresh(path)`，
  把外部 `.3dm` 以**只读引用**形态挂入当前文档。
- Linked Block：`doc.InstanceDefinitions.AddLinked(path, layerStyle, name, description)` +
  `doc.Objects.AddInstanceObject(definitionIndex, transform)`，把外部 `.3dm` 以**外部参考块**
  形态嵌入当前文档；嵌入后该 Block 可以在 Rhino UI 里用 "Update Linked Block" 同步源文件改动。

按 `MCP_Rhino Architecture.md` 中"任何写入 / 改变文档状态的能力都强制 live"原则：
- **Export**：`RhinoDoc.WriteFile` 不改变 ActiveDoc 的文档状态，但需要从 `RhinoDoc` 拿几何序列化 —
  本质上是 read 行为。本期 Export 默认走 **Live**（`RhinoDoc.ActiveDoc`），原因是 RhinoCommon 的
  WriteFile 必须基于运行中 doc，且部分格式（pdf / jpg / png）依赖 viewport；同时提供 offline 读
  → 临时构造 RhinoDoc 的能力代价过大、收益不足，本期不做。
- **Import (worksession / linked block)**：直接修改 ActiveDoc 状态（attach worksession / 写入新的
  InstanceDefinition + InstanceObject），强制走 **Live + Undo record**。

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
  - `ExportToImageTool`：基于 `RhinoView.CaptureToFile`，输出 jpg / png / bmp / tiff。
  - `ExportToPdfTool`：基于 `Rhino.UI.Print.PrintInstance` 或 `_-Print` 命令，输出 PDF。
- **导入（外部参考）共 6 个 Tool（live-only，全部进 Undo record）**：
  - `AttachWorksessionTool` / `DetachWorksessionTool` / `RefreshWorksessionTool` /
    `ListWorksessionAttachmentsTool`（list 仅读 worksession 状态，不改文档；为对称性放在此 Plan
    中，与 attach / detach 并列）。
  - `AddLinkedBlockTool` / `UpdateLinkedBlockTool` —— 添加外部参考块定义并放置实例；按定义名 /
    源文件路径触发同步刷新。
- 所有 Export Tool 接受**目标文件路径** + **可选范围**（`SelectedObjectIds[]`、`SelectedLayers[]`、
  `ViewName`、`PaperSize` 等），**不接受**"修改 ActiveDoc 状态"参数；执行后写盘并返回写出文件
  绝对路径 / 大小 / 写出对象数 / warnings。
- 所有外部参考 Tool 接受**源文件路径** + **附加选项**（layer style、insertion plane、scale），
  执行后通过 `ExecuteWithUndo` 包裹，确保 `Ctrl+Z` 可还原。

## 架构归属

- **Tools/File/**（既有目录，目前装 DocumentUserString）—— 在内部按子能力分两个子目录：
  - **Tools/File/Export/** —— 6 个 Export Tool。
  - **Tools/File/Reference/** —— 6 个 Worksession / LinkedBlock Tool。
- **Application/Services/** —— 新增：
  - `RhinoFileExportService`：承载 `ExportTo<Format>(...)` 7 个方法；暴露统一的 "writefile +
    optional view capture" 路径；不区分 dwg / dxf / ifc / stl 这种"WriteFile-style"路径与
    image / pdf 这种"Capture / Print-style"路径，由内部 strategy 分派（见关键设计 #2）。
  - `RhinoExternalReferenceService`：承载 `AttachWorksession / DetachWorksession /
    RefreshWorksession / ListWorksessionAttachments / AddLinkedBlock / UpdateLinkedBlock`。
- **Application/Interfaces/** —— 新增：
  - `ILiveFileExporter`：方法 `Export(RhinoDoc doc, FileExportSpec spec)`，分派 dwg / dxf / ifc / stl / image / pdf。
  - `ILiveExternalReferenceManager`：方法 `AttachWorksession(...)` / `DetachWorksession(...)` /
    `RefreshWorksession(...)` / `ListWorksession(RhinoDoc doc)` / `AddLinkedBlock(...)` /
    `UpdateLinkedBlock(...)`。
- **Infrastructure/Rhino/Live/** —— 新增：
  - `LiveRhinoFileExporter.cs`：所有 `RhinoDoc.WriteFile` / `RhinoView.CaptureToFile` /
    `Rhino.UI.Print.PrintInstance` 调用集中在此。
  - `LiveRhinoExternalReferenceManager.cs`：所有 `WorkSession.Attach / Detach / Refresh` /
    `InstanceDefinitions.AddLinked / RefreshLinked` / `Objects.AddInstanceObject` 调用集中在此。
- **Domain/Models/** —— 新增：
  - `FileExportSpec`：`OutputPath / Format(EnumExport) / ScopeKind(All|Selected|VisibleOnly|ByLayer) /
    SelectedObjectIds[] / LayerFullPaths[] / ViewName? / PaperSize{WidthMm, HeightMm}? / DotsPerInch? /
    BackgroundTransparent? / FormatOptions(IDictionary<string,string>)`。
  - `WorksessionAttachmentResult`：`SourceFilePath / Attached(bool) / WasAlreadyAttached(bool) /
    Message`。
  - `LinkedBlockSpec`：`SourceFilePath / DefinitionName / Description? / LayerStyle(Active|Reference) /
    InsertionPoint{XYZ} / TransformScale{XYZ}? / TransformRotationRadians?`。
  - `LinkedBlockResult`：`DefinitionId / DefinitionIndex / DefinitionName / InstanceObjectId? /
    SourceFilePath / Updated(bool) / WasAlreadyDefined(bool)`。
- **Domain/Enums/** —— 新增：
  - `FileExportFormat`：Dwg / Dxf / Ifc / Stl / Pdf / Jpg / Png / Bmp / Tiff。
  - `FileExportScopeKind`：All / Selected / VisibleOnly / ByLayer。
  - `LinkedBlockLayerStyle`：Active / Reference（对应 `Rhino.DocObjects.InstanceDefinitionUpdateType`
    与 layer style 的组合）。
  - `WorksessionAttachmentStatus`：Attached / NotAttached / Stale。
- **Contracts/Requests/** —— 12 个新 Request DTO + 若干 entry：
  - 每个 Export Tool 1 个 Request：`ExportToDwgRequest` / `ExportToDxfRequest` /
    `ExportToIfcRequest` / `ExportToStlRequest` / `ExportToImageRequest` / `ExportToPdfRequest`。
  - Worksession：`AttachWorksessionRequest` / `DetachWorksessionRequest` /
    `RefreshWorksessionRequest` / `ListWorksessionAttachmentsRequest`。
  - Linked Block：`AddLinkedBlockRequest` / `UpdateLinkedBlockRequest`。
- **Contracts/Responses/** —— 新增：
  - `FileExportResponse`：`FilePath / OutputPath / Format / ExportedObjectCount /
    OutputFileSizeBytes / DurationMs / Warnings`。
  - `WorksessionMutationResponse` + `WorksessionAttachmentResponse`：（FilePath /
    AttachedFiles[] / SucceededCount / FailedCount / Warnings / Results）。
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
   - 校验 `OutputPath` 父目录存在 + 目标路径未被同名 `.3dm` 占用（不允许覆盖原始文件）。

2. **Export 内部 strategy 分派**
   - dwg / dxf / ifc / stl：纯 `RhinoDoc.WriteFile(path, FileWriteOptions)`。
     - `FileWriteOptions.SuppressDialogBoxes = true`（避免阻塞 UI 线程）。
     - `FileWriteOptions.WriteSelectedObjectsOnly = (ScopeKind == Selected)`；先把
       `SelectedObjectIds` 经 `doc.Objects.Select(...)` 标记选中，导出后恢复原选中态。
     - `FileWriteOptions.WriteUserData = true`，保留 user text / 文档属性。
   - jpg / png / bmp / tiff：`RhinoView.CaptureToFile(view, path, width, height, transparent)`；
     `ViewName` 缺省取 `doc.Views.ActiveView.MainViewport.Name`。
   - pdf：调 `Rhino.UI.Print.PrintInstance`（或回退到 `RhinoApp.RunScript("_-Print ...", echo:false)`）；
     `PaperSize` 缺省 A3 横向；DPI 缺省 300。
   - 任何格式调用前 `Stopwatch.StartNew()`，调用后写入 `DurationMs` 与 `OutputFileSizeBytes`。

3. **Export `Selected` 范围实现的并发安全**
   - `ScopeKind=Selected` 时：
     1. 缓存 `doc.Objects.GetSelectedObjects(includeLights:true, includeGrips:true)` 当前选中
        ObjectId 列表。
     2. `doc.Objects.UnselectAll()`；按 `SelectedObjectIds` 重新标记。
     3. WriteFile / 命令执行后，恢复缓存的选中态。
   - 整段流程在 `ExecuteWithUndo` 之外（导出本身不改文档）；对 viewport 有视觉副作用（短暂选中变化），
     文档建议用户在跑 Tool 期间避免人工选择。

4. **Worksession 的 Attach / Detach / Refresh 必走 Undo？**
   - Worksession 不属于"document content" —— 它是 RhinoDoc 之外的会话级状态，**不进入 Undo 栈**。
     RhinoCommon 的 `WorkSession.Attach` 也不会触发 `BeginUndoRecord` 内的 Undo 条目。
   - 因此本 Plan 把 Worksession 类操作走 `_documentAccessor.Execute(...)` 而非 `ExecuteWithUndo`，
     并在 Tool 文档明确 "Worksession 操作不可通过 Ctrl+Z 还原；如需还原请调 Detach"。
   - **此处与架构规则的偏差需要显式声明**：架构 §86–110 把 "改变文档状态的能力"统一要求 live + Undo
     record；worksession 状态本身不在 RhinoDoc 自身的 Undo scope 内，是 RhinoCommon 的设计决策；
     Plan 沿用 RhinoCommon 的语义，不强行包 Undo。

5. **LinkedBlock 必走 Undo**
   - `InstanceDefinitions.AddLinked` + `Objects.AddInstanceObject` 修改 RhinoDoc 内的
     InstanceDefinitionTable / Objects，进入 Undo scope。
   - 走 `_documentAccessor.ExecuteWithUndo(filePath, "MCP: AddLinkedBlock", ...)`。
   - `UpdateLinkedBlock` 调 `InstanceDefinitions.RefreshLinkedBlock(definitionIndex)`，刷新源文件改动。

6. **WriteFile 路径校验**
   - `OutputPath` 必须为绝对路径，否则硬错误。
   - 父目录不存在 → 硬错误（不自动创建，避免误写到意外位置）。
   - 目标已存在文件 → 默认覆盖，但记录 `Warnings: ["overwriting existing file"]`；提供
     `OverwriteExisting=false` 选项强制拒绝覆盖。
   - 目标路径与当前 ActiveDoc 路径相同（无论扩展名）→ 硬错误，避免误覆盖源文件。

7. **PDF 路径的 fallback 策略**
   - 优先使用 `Rhino.UI.Print.PrintInstance` API（Rhino 8+）。
   - 若运行时 `Print` 类不可用（早期版本 / SDK 不一致）→ fallback 到 `RhinoApp.RunScript` 触发
     `_-Print` 命令，传参形式严格按 Rhino 命令行 syntax；fallback 路径不保证所有 PaperSize 选项可控，
     在 warnings 中提示。
   - 两条路径都 fallback 失败 → 单次调用失败，Message 含触发的 Print API 名。

8. **Image 导出的 viewport 选择**
   - `ViewName` 缺省取 `doc.Views.ActiveView`；显式指定时遍历 `doc.Views` 找匹配的 `View.MainViewport.Name`。
   - 命中 0 view → 硬错误；命中多 view（重名） → 取第一个 + warning。
   - `ScopeKind=Selected` 仅对 dwg / dxf / ifc / stl 生效；image / pdf 通过 viewport 自带的"Show
     selected only" 模拟（暂不暴露，本期 image / pdf 仅截当前 viewport 完整画面）。

9. **批量 Export（不在本期）**
   - 一次 Tool 调用只导出一种格式 / 一份文件。批量"导一组 dwg + 一组 jpg" 由客户端串多次 Tool 调用
     完成；如未来浮现高频复合需求，再抽 `BatchExportSkill`。

10. **Worksession Refresh 的 stale 处理**
    - `RefreshWorksession` 命中已被外部修改的 `.3dm` 时调 `WorkSession.Refresh(path)`；
      对未 attach 的路径返回 per-entry `Success=false`。
    - `ListWorksessionAttachments` 通过 `RhinoDoc.WorkSession.ModelNames` /
      `WorkSession.ModelPath(index)` 枚举，附 `WorksessionAttachmentStatus` 字段（基于
      `WorkSession.ModelStatus` 推断 / 或直接写 Attached）。

11. **Linked Block 的更新策略**
    - `AddLinkedBlock` 的 `LayerStyle`：
      - `Active`：把外部参考块对象按当前 ActiveDoc 的图层结构落盘。
      - `Reference`：保持源 `.3dm` 的图层名，加 `_<DefinitionName>::` 前缀（Rhino 默认行为）。
    - `UpdateLinkedBlock`：调 `InstanceDefinitions.RefreshLinkedBlock(definitionIndex)`，刷新源文件改动；
      若源文件不存在 / 路径变化 → per-entry 失败。

12. **错误码沿用**
    - 所有 live 路径错误码（`LIVE_RHINO_REQUIRED` / `NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` /
      `FILE_NOT_ACTIVE` / `RHINO_MAIN_THREAD_BUSY`）由 `ILiveRhinoDocumentAccessor` 自动返回，
      不重复包装。
    - 新错误码：
      - `EXPORT_OUTPUT_PARENT_NOT_FOUND`：父目录不存在。
      - `EXPORT_OUTPUT_OVERWRITE_BLOCKED`：禁止覆盖原 `.3dm` / 设置了 `OverwriteExisting=false`。
      - `EXPORT_VIEW_NOT_FOUND`：image / pdf 指定 `ViewName` 命中 0 view。
      - `WORKSESSION_FILE_NOT_FOUND`：attach / refresh 的源文件不存在。
      - `LINKED_BLOCK_DEFINITION_EXISTS`：AddLinkedBlock 时同名 definition 已存在。
      - `LINKED_BLOCK_DEFINITION_NOT_FOUND`：UpdateLinkedBlock 时未找到 definition。

## 涉及文件

**新增（Tools）** —— 12 个文件：
- `src/MCP_Rhino.Server/Tools/File/Export/{ExportToDwg,ExportToDxf,ExportToIfc,ExportToStl,ExportToImage,ExportToPdf}Tool.cs`
- `src/MCP_Rhino.Server/Tools/File/Reference/{AttachWorksession,DetachWorksession,RefreshWorksession,ListWorksessionAttachments,AddLinkedBlock,UpdateLinkedBlock}Tool.cs`

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
- `src/MCP_Rhino.Server/Domain/Models/LinkedBlockSpec.cs`
- `src/MCP_Rhino.Server/Domain/Models/LinkedBlockResult.cs`
- `src/MCP_Rhino.Server/Domain/Enums/FileExportFormat.cs`
- `src/MCP_Rhino.Server/Domain/Enums/FileExportScopeKind.cs`
- `src/MCP_Rhino.Server/Domain/Enums/LinkedBlockLayerStyle.cs`
- `src/MCP_Rhino.Server/Domain/Enums/WorksessionAttachmentStatus.cs`

**新增（Contracts）**：12 个 Request + 4 个 Response，路径 `src/MCP_Rhino.Server/Contracts/{Requests,Responses}/`。

**新增（Test）**：
- `Project_Test/260422_TEST_file-import-export-tools/`，包含：
  - `FileExportSmokeTest.cs`（Export 6 个 Tool 的 happy path + 边界 + 写盘断言）。
  - `ExternalReferenceSmokeTest.cs`（Worksession / LinkedBlock 6 个 Tool 的 happy path + Undo 验证）。
  - `samples/`：一份小型 `.3dm` 作为 worksession / linked block 源文件；测试结束清理生成的导出
    产物（dwg / stl / jpg / pdf 等）。

**修改**：
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs` —— 注册 2 个 interface 实现 + 2 个 Service。
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs` + `.Parsing.cs` ——
  追加 `file-export-smoke-test` / `external-reference-smoke-test` 两个子命令。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs` —— 接入新 smoke 分支。

**复用（不改）**：
- `ILiveRhinoDocumentAccessor`（`Execute` / `ExecuteWithUndo`）。
- `ObjectEditWarning`（warning code 体系，新增 6 个 code）。
- `OperationResponse<T>`（统一响应壳）。

## 使用方式

MCP Tool 调用示例：

- 导出全部为 dwg：`ExportToDwg(filePath, outputPath="C:/out/site.dwg", scope={Kind:"All"})`
- 仅导选中曲面为 stl（用于 3D 打印）：`ExportToStl(filePath, outputPath, scope={Kind:"Selected", SelectedObjectIds:[g1,g2]}, formatOptions={Binary:"true", Resolution:"High"})`
- 视图截屏：`ExportToImage(filePath, outputPath="C:/out/perspective.png", viewName="Perspective", paperSize={WidthMm:1920, HeightMm:1080}, dotsPerInch=144, backgroundTransparent=true)`
- 出图 PDF：`ExportToPdf(filePath, outputPath, viewName="Top", paperSize={WidthMm:420, HeightMm:297}, dotsPerInch=300)`
- 挂载 worksession：`AttachWorksession(filePath, sourceFilePaths=["C:/refs/site_grid.3dm", "C:/refs/utilities.3dm"])`
- 列出当前 worksession：`ListWorksessionAttachments(filePath)`
- 把外部 `.3dm` 作为外部参考块插入：`AddLinkedBlock(filePath, entries=[{sourceFilePath:"C:/refs/column.3dm", definitionName:"COL_TYPE_A", layerStyle:"Reference", insertionPoint:{X:0,Y:0,Z:0}}, ...])`
- 同步外部参考块：`UpdateLinkedBlock(filePath, definitionNames:["COL_TYPE_A"])`

典型流程：
- "把当前 ActiveDoc 出三件套：dwg + 顶视图 PDF + 透视 PNG"：
  `ExportToDwg(...)` → `ExportToPdf(... viewName:"Top" ...)` → `ExportToImage(... viewName:"Perspective" ...)`。
- "多人协同：把 site / utilities 两个文件挂入 worksession，再把 column 作为外部参考块"：
  `AttachWorksession([site, utilities])` → `AddLinkedBlock(column)` → `RefreshWorksession([site])`（源文件
  被同事更新后刷新）。

## 验收标准

构建 / smoke：
- `dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 无 Warning 通过。
- CLI fallback：`dotnet run --project src/MCP_Rhino.Server -- file-export-smoke-test test-files/MCP_rhino_test.3dm`：
  - 所有 12 个 Tool 返回 `LIVE_RHINO_REQUIRED`。
  - 工作副本对象数 / 图层数 / user text 无变化。
- Live 手工 smoke（Rhino 内 `_McpDevSmoke file-export` / `_McpDevSmoke external-reference`）：
  - **Export**：
    - `ExportToDwg / Dxf / Ifc / Stl`：目标路径生成有效文件；文件大小 > 0；用 Rhino 重新打开 dwg
      / dxf / stl 能看到对象（IFC 视环境是否安装查看器）；导出后 ActiveDoc 对象数 / Undo 栈无变化。
    - `ExportToImage`：jpg / png / bmp / tiff 文件生成；像素尺寸与请求一致；`backgroundTransparent=true`
      时 png 透明背景生效。
    - `ExportToPdf`：A3 PDF 文件生成；DPI / 页面方向与请求一致。
  - **Reference**：
    - `AttachWorksession`：Rhino UI File → Manage Worksession 中可见新挂载文件；列表里显示。
    - `DetachWorksession`：相应文件从 worksession 中消失。
    - `RefreshWorksession`：源文件改动后调用，viewport 实时刷新引用几何。
    - `AddLinkedBlock`：Rhino UI Block Manager 中能看到新 linked definition；Undo 一步还原
      （definition + instance object 一并消失）。
    - `UpdateLinkedBlock`：源文件改动后调用，instance object 几何更新。

边界用例：
- `outputPath` 父目录不存在 → `EXPORT_OUTPUT_PARENT_NOT_FOUND`。
- `outputPath` 与 ActiveDoc 路径相同 → `EXPORT_OUTPUT_OVERWRITE_BLOCKED`。
- `OverwriteExisting=false` 且目标已存在 → `EXPORT_OUTPUT_OVERWRITE_BLOCKED`。
- `viewName` 命中 0 view → `EXPORT_VIEW_NOT_FOUND`。
- `AttachWorksession` 源文件不存在 → `WORKSESSION_FILE_NOT_FOUND`。
- `AttachWorksession` 同一源文件重复挂载 → 单条 `Success=true / WasAlreadyAttached=true`，不报错。
- `AddLinkedBlock` 同名 definition 已存在 → `LINKED_BLOCK_DEFINITION_EXISTS`，per-entry 失败。
- `UpdateLinkedBlock` 未找到 definition → `LINKED_BLOCK_DEFINITION_NOT_FOUND`。
- `ScopeKind=Selected` 但 `SelectedObjectIds` 为空 → 整 Request 失败。

副作用 / Undo：
- Export 调用前后：`doc.Objects.Count` / `doc.Layers.ActiveCount` / `doc.UserData` 无变化；Rhino Undo
  History 面板**无新条目**。
- Worksession 调用前后：Undo 面板**无新条目**（与设计 #4 一致）；Rhino UI 中可见 worksession 状态变化。
- LinkedBlock 调用前后：Undo 面板**有 1 个新条目**；`Ctrl+Z` 一步还原 definition + instance。

## 风险与回退方案

风险：
- **WriteFile 行为依赖文件类型插件**：dwg / dxf / ifc / stl 需要 Rhino 内置插件已加载；某些
  Rhino installation 可能未启用 IFC 插件。缓解：`Export` 调用前调
  `Rhino.PlugIns.PlugIn.GetEnabledPlugInList(...)` 检查 plugin 是否启用，未启用 → 硬错误 + 提示
  用户在 Rhino 选项中启用插件。
- **PDF API 依赖 Rhino 8+ `Rhino.UI.Print` 命名空间稳定性**：早期版本 SDK 命名变化大。缓解：双
  路径（API 优先 + RunScript fallback），并在 EXET 阶段在目标 Rhino 版本上跑一次 smoke 确认主路径。
- **viewport 截屏受 active view 状态影响**：用户在跑 Tool 时手动切换 view 会影响 image / pdf
  内容。缓解：Tool 文档建议"截屏前不要操作 Rhino UI"；不引入"锁定 view"机制。
- **Worksession 不进 Undo 栈**：与"全部 mutation 必须可 Undo"的项目一致性预期不符。缓解：在 Tool
  文档与 EXET 「与计划的偏差」中显式说明此为 RhinoCommon 设计决策；同时提供 `DetachWorksession`
  作为人工撤销路径。
- **Linked Block 源文件路径漂移**：源文件移动后 `RefreshLinkedBlock` 失败。缓解：`UpdateLinkedBlock`
  返回 per-definition `Success=false` + Message 含原源路径。
- **Export 大文件性能**：高密度 Brep dwg 导出可能 > 10s 触发主线程超时。缓解：Tool 文档建议
  大模型走分图层 / 分对象批次；不引入 background worker。
- **测试产物清理**：smoke 会生成 dwg / stl / pdf 等文件，需要在测试 teardown 中删除避免污染仓库。

回退方案：
- 单 Tool 失败：从 `ToolRegistration` 黑名单单独下线。
- 整批回退：所有新文件均落在新增子目录 + 新增 Service，不修改既有 Service / Tool / Skill 签名；
  `git revert` 整批 commit 即可还原。
- 回退后 ActiveDoc 状态：Export 不改文档，回退无残留；LinkedBlock / Worksession 已经 Apply 的部分
  需要客户端调对应 `Detach / Remove` Tool 主动清理（回退仅删了"未来调用入口"，已 Apply 的状态保留）。

## 后续扩展方向

- **通用 Import**：把 dwg / dxf / iges / step 等格式的几何 import 成本地 Rhino 几何（基于
  `RhinoDoc.ReadFile`）。需先解决"import 几何与既有几何冲突 / layer 命名冲突"策略，待 LLM 用例
  浮现后再做。
- **批量出图 Skill**：`OutputPackagingSkill`，按层 / 按视图组合，一次性导一套 dwg + jpg + pdf
  并打 zip 包。
- **Bind Linked Block**：把 linked block "脱离引用、落地为本地 block definition"，沿用 RhinoCommon
  `InstanceDefinitions.MakeEmbedded(definitionIndex)`。
- **Image / PDF 高级选项**：阴影 / 材质 / 出图样式 / 显示模式（Rendered / Wireframe / Shaded） 选择，
  目前默认走当前 viewport display mode。
- **Worksession 状态查询升级**：增加 `IsModified` / `LastSavedAt` 等元数据，便于 LLM 判断是否需要
  Refresh。
- **导出格式扩展**：iges / step / obj / fbx / 3ds / sat / x_t / x_b / ply / 3mf 等。本期保留最常用的
  6 种，扩展工作量主要是 schema + smoke，不影响架构。
- **导出范围进阶**：`ScopeKind=ByLayer` 已列入 schema，本期实现简单转发；后续可与 `LayerQueries`
  统一表达，避免新加字段。

