# 260421_PLAN_layer-management-tools

## Context

MCP_Rhino 目前的图层能力只有两类读工具 (`FindLayerCandidates`、`FilterObjectsByLayer`) 以及对象级的 `SetLayer`(把对象挪到**已存在**的图层)。图层自身的增 / 删 / 改 (重命名 / reparent / 改颜色 / 显隐 / 锁定)**完全缺失**——用户调 `CreatePoints` 或 `ApplyObjectEdits.SetLayer` 时若目标图层不存在,只能手动去 Rhino UI 建好再重试。

本次计划补齐这部分能力,让 LLM 能在 live Rhino 里完整管理图层树(含 Purge 连带清对象、出图相关属性 PlotColor/PlotWeight、线型/材质绑定),同时延续既有的 live-only mutation 契约 (Undo 纳入 Rhino 栈、不落盘、主线程封送)。

## 目标

- **1 个 offline read**:`GetLayersTool` 列出文档全部图层及其完整属性。
- **4 个 live mutation**:`CreateLayersTool` / `ModifyLayersTool` / `DeleteLayersTool` / `PurgeLayersTool`,全部批量、全部进一次 Undo 记录。**Delete 删除整棵子树**(被用户指定的目标层 + 它下面的所有后代层一并移除;**整个子树上的对象统一迁到用户指定目标层的父层**——这是用户可见的结果,不管子树有几层深);**Purge 连带清光**(被删层及子层上的所有对象物理删除);两者语义不同、分开暴露。
- **3 个 preview-of-mutation**:`PreviewModifyLayersTool` / `PreviewDeleteLayersTool` / `PreviewPurgeLayersTool`,走 live 只读、不开 Undo。
- **保持 MCP API 面不破**:现有 5 个图层相关 Tool 不改签名、不改行为;新增 Tool 独立落地。
- **属性覆盖面**(一期):名称 / 父级(FullPath)/ 显示颜色 / 显隐(Visible)/ 锁定(Locked)/ **打印颜色**(PlotColor)/ **打印宽度**(PlotWeight)/ **线型**(LinetypeName)/ **材质**(RenderMaterialName)。
- **非目标**:层模板 / 链接参考图层 (`AddReferenceLayer`) / `CurrentLayerIndex` setter / 按 GUID 定位的老式入参一期不做。

## 架构归属

全部落到现有目录,不新增一级文件夹。

- **Tools/Layers/**(既有目录)—— 8 个新 Tool:
  - `GetLayersTool.cs`(offline read + `OFFLINE_READ_STALE` 告警)
  - `CreateLayersTool.cs`(live)
  - `ModifyLayersTool.cs`(live)
  - `DeleteLayersTool.cs`(live,删除整棵子树 + 对象上浮)
  - `PurgeLayersTool.cs`(live,**连带删对象**)
  - `PreviewModifyLayersTool.cs`(live 只读)
  - `PreviewDeleteLayersTool.cs`(live 只读)
  - `PreviewPurgeLayersTool.cs`(live 只读)
- **Application/Services/** —— 新增 `RhinoLayerManagementService.cs`,承载 `Get / Create / Modify / Delete / Purge / PreviewModify / PreviewDelete / PreviewPurge` 八个方法。构造依赖 `IRhinoDocumentRepository`(offline read) 与 `ILiveRhinoDocumentAccessor`(live 写 + stale 检测)。`IEditResultFormatter` **不作为必需依赖**;如后续 CLI 需要文本格式化,沿用 service 内 `Format*` 或单独补 formatter,不在一期接口设计里预绑定。
- **Domain/Models/** —— 新增 `RhinoLayerDetail.cs`。字段:`LayerIndex / LayerName / FullPath / ParentFullPath / ObjectCount / Color (RhinoDisplayColor) / Visible / Locked / IsCurrentLayer / PlotColor (RhinoDisplayColor?) / PlotWeight (double?) / LinetypeName (string?) / RenderMaterialName (string?)`。其中 `PlotColor / PlotWeight / LinetypeName / RenderMaterialName` 允许 `null`,用于表达"未显式设置 / 继承文档或父层状态",避免与空字符串、`0` 混淆。`RhinoLayerCandidate` 不动,继续服务 `FindLayerCandidatesTool`。
- **Contracts/Requests/** —— 10 个新 Request DTO:
  - `GetLayersRequest`
  - `LayerCreationEntryRequest` + `CreateLayersRequest`
  - `LayerModificationEntryRequest` + `ModifyLayersRequest`。其中 `PlotColor / PlotWeight / LinetypeName / RenderMaterialName` 不仅要支持"设置为某值",还要支持"清空显式值、恢复继承";一期采用显式 clear flag(如 `ClearPlotColor / ClearPlotWeight / ClearLinetype / ClearRenderMaterial`)表达,避免 `null` 同时承担"不改"与"清空"两种语义
  - `DeleteLayersRequest`、`PurgeLayersRequest`(两者都以 `IReadOnlyList<string> FullPaths` 为核心字段,分开 DTO 便于 Tool 文档区分语义)
  - `PreviewModifyLayersRequest`(entries 与 `ModifyLayersRequest` 一致;Preview 不复用 Apply Request 以避免"多传一个 Preview 字段"这种语义膨胀)
  - `PreviewDeleteLayersRequest`、`PreviewPurgeLayersRequest`(都是 `FullPaths` 列表)
  - Delete / Modify / Purge 的 entry 一律以 `FullPath` 作请求定位键(RhinoCommon 的 `LayerIndex` 会因删除重排,不可作 client 端标识);但 **service 在 apply / preview 入口会先把请求解析成当前文档快照下的稳定目标**,后续执行不再依赖可变的 FullPath 字符串逐项现查。
- **Contracts/Responses/** —— 8 个新 Response DTO:
  - `LayerReadResponse`(`FilePath / TotalCount / Warnings / Entries: IReadOnlyList<RhinoLayerDetail>`)
  - `LayerMutationResponse` + `LayerMutationResultResponse`(镜像 `DocumentUserStringMutationResponse` / `...ResultResponse`:`FilePath / RequestedCount / SucceededCount / FailedCount / Warnings / Results`;每个 result 含 `RequestedFullPath / ResolvedFullPath / Success / Message`。其中 `ResolvedFullPath` 对 rename / reparent 尤其重要,便于 client 直接拿到变更后的路径继续调用。Purge 情况下 Message 会写明 `"Purged layer [X], removed Y objects"`,不另起新响应类型)
  - `LayerModificationPreviewResponse` + `LayerModificationImpactResponse`(每个 impact 含:`TargetFullPath / ResolvedNewFullPath / FieldDiffs: IReadOnlyList<string>`,例如 `"Color: (255,0,0) -> (0,255,0)"`;若是 rename / reparent,附 `AffectedSubLayers: IReadOnlyList<LayerSubtreeEntry>` 列表说明连带影响)
  - `LayerDeletionPreviewResponse` + `LayerDeletionImpactResponse`(每个 impact 含 `TargetFullPath / DirectObjectCount / DescendantObjectCount / TotalAffectedObjectCount / AffectedSubLayers: IReadOnlyList<LayerSubtreeEntry> / CurrentLayerInSubtree: bool`)。Delete 与 Purge **继续共用 DTO**,但不复用歧义字段语义: Delete 视 `TotalAffectedObjectCount` 为"将迁移的对象总数",Purge 视其为"将删除的对象总数"。`CurrentLayerInSubtree=true` 在 Apply 阶段直接转成硬拒绝(见关键设计 #4),Preview 阶段只报告不阻断。
  - `LayerSubtreeEntry`(`FullPath / DirectObjectCount`)—— Modify / Delete / Purge 三个 Preview 共用,供 `AffectedSubLayers` 字段装填。客户端拿到的形状统一:"受牵连子层各自有多少对象"。
- **Server/DependencyInjection.cs** —— 在 `AddRhinoApplication()` 里注册 `RhinoLayerManagementService`。无须改 Offline / Live 适配注册 —— 新 Service 直接复用 `IRhinoDocumentRepository` + `ILiveRhinoDocumentAccessor`。
- **Skills / Agents** —— 不新增。layer 操作足够原子,没有出现多 Service 编排的复合流程。若后续出现"批量把 `Floor::Old_*` 全部改名到 `Floor::New_*`"这类组合需求,再考虑抽 `LayerBatchRenameSkill`。

## 关键设计

1. **RhinoCommon / Rhino3dm API 选型**
   - **Create**:`doc.Layers.AddPath(string layerPath)` 或 `AddPath(layerPath, Color)` —— 自动补父链,契合"用户传 `Floor::Level 3` 就自动建好 `Floor`"的语义;后续字段(Visible/Locked/PlotColor/PlotWeight/Linetype/RenderMaterial)由 `Modify` 路径覆盖,Service 内部串一次 `AddPath → FindByFullPath → Modify` 完成。
   - **自动创建父层的披露**:`AddPath` 在父链缺失时**静默**用 RhinoCommon 默认属性(ByParent 颜色、可见、未锁定、默认线型/材质)建好父层。用户可能以为整条链都按它给的叶子层属性建出来。Service 在调用 `AddPath` 前先快照现有图层 FullPath 集合,调用后 diff 出真正被自动创建的父层路径,并在对应 `LayerMutationResultResponse.Message` 里附加 `"(auto-created parent layers: [Floor])"`。若父层全部已存在,Message 不变。这不引入新 warning code,信息贴着被创建的叶子层走,客户端处理更直接。
   - **Modify**:`doc.Layers.Modify(Layer newSettings, int layerIndex, bool quiet)`。流程:先把 request entries 解析成当前文档快照下的目标层引用,再 `Layers[index]` 拿到当前 Layer 的**副本**(`.Duplicate()` 或 new Layer 拷贝字段)→ 按 Request 覆盖字段 / clear flag → 回写。`quiet=true` 抑制 Rhino 对话框。
   - **Delete**:由 Project_Test/260421_TEST_layer-behavior-check/ probe 实测,`doc.Layers.Delete(int, quiet=true)` **对持有对象的层直接返回 false,不迁移对象、不软删**。一期用户可见语义:"**删除整棵子树,所有对象统一迁到用户指定目标层的父层**"。Service 内部按深度优先实现:(a) 对每个目标层(用户指定的顶层)及其所有后代层(任意深度)收集 `doc.Objects` 中 `Attributes.LayerIndex == index` 的对象,逐个 `ObjectAttributes.Duplicate` → `LayerIndex = <用户指定目标层的父层 index>` → `doc.Objects.ModifyAttributes(objId, attrs, quiet: true)`;(b) 从最深的后代层开始,自底向上依次 `doc.Layers.Delete(index, quiet=true)`(此时各层已空,返回 true);(c) 最后删除用户指定的目标层。关键点:**所有被迁移的对象都落到同一个目的层(用户指定目标层的父层),而不是各自原父层** —— 跟 Rhino UI "Delete Layers" 命令的 "Move objects up" 行为一致,避免子层对象卡在即将消失的中间层上。任一步失败 → per-item fail,已做的 ModifyAttributes 随当前 Undo record 一并回退。
   - **Purge**:`doc.Layers.Purge(int layerIndex, bool quiet=true)`,probe 实测:**自动级联到子层 + 所有对象**(父 + 子都 `IsDeleted=True`、两层的 objects 从 `doc.Objects` 物理移除),返回 `true`。Service 不需要手工递归,只要在 Apply 前统计 direct + descendant 对象数用于 Message 回显即可(`"Purged layer [X], removed Y objects across Z sub-layers"`)。
   - **父级变更**:`Modify` 时把 `Layer.ParentLayerId` 改到目标父层的 `Id`;根层传 `Guid.Empty`。
   - **线型绑定**:`LinetypeName` → live 经 `doc.Linetypes.Find(name, ignoreCase: false)` 解析 `LinetypeIndex`;offline 经 `model.AllLinetypes.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.Ordinal))`。两侧**均为大小写敏感**,避免"client 传 `continuous` 在 offline 命中、live 不命中"的 behaviour drift。找不到 per-item fail `"Linetype not found: {name}"`。
   - **材质绑定**:`RenderMaterialName` → live 经 `doc.Materials.Find(name, ignoreCase: false)` 解析 `RenderMaterialIndex`;offline 经 `model.Materials.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal))`。同样大小写敏感。找不到 per-item fail。注意 Rhino 8 的 RDK 渲染材质未暴露 —— 本期仅支持文档级 `Materials` 表。
   - **风格备注**:本项目其他查询/去重默认走 `OrdinalIgnoreCase`(图层名、类型名、UserAttribute key 等);linetype / material 这里刻意用 `Ordinal` 收紧,不追求宽松匹配。理由是 RhinoCommon `LinetypeTable.Find` / `MaterialTable.Find` 的默认形态是大小写敏感的,offline 侧对齐 live 侧避免"client 传 `continuous` 在 offline 命中、live 不命中"的行为漂移。若后续出现大量 client 抱怨,可以再放开成 `OrdinalIgnoreCase`,但**必须两侧同步改**。
   - **查询所有图层**:`doc.Layers`(或 offline `model.AllLayers`),过滤 `IsDeleted=false`。对象计数遍历 `doc.Objects` / `model.Objects` 按 `Attributes.LayerIndex` 分组(与 `RhinoObjectFilterService.FindLayerCandidates` 的统计方式一致)。

2. **Offline read 的等价实现**
   - `GetLayersTool` 直接 `_repository.Read(filePath).AllLayers`,字段 1:1 映射到 `RhinoLayerDetail`;对象数从 `model.Objects` 统计;LinetypeName / RenderMaterialName 通过 `model.AllLinetypes[LinetypeIndex]` / `model.Materials[RenderMaterialIndex]` 反查。
   - 沿用 `RhinoObjectFilterService.CreateOfflineWarnings` 的 stale 检测模式(调 `ILiveRhinoDocumentAccessor.TryGetActiveDocumentState`),命中时附 `OFFLINE_READ_STALE` 软警告。
   - `IsCurrentLayer` 字段在 offline 路径下**恒为 `false`** —— Rhino3dm `File3dm` 不保证稳定暴露 `CurrentLayerIndex`,且"当前层"本身是 live session 概念,对未打开的磁盘文件无意义。不产生 warning,不新增 warning code。**一期也不提供 live `GetLayers`**(本 plan 只定义 offline read);如后续有"读取真实当前层"的诉求,由扩展方向里的 `SetCurrentLayer` 工具 / live read 分支承载,不在本期范围。

3. **Live mutation 模板严格对齐 `RhinoDocumentUserStringService.Mutate`**
   - 每个写方法用 `_documentAccessor.ExecuteWithUndo(filePath, "MCP: CreateLayers", document => ...)` 包裹(描述对应 `CreateLayers` / `ModifyLayers` / `DeleteLayers` / `PurgeLayers`)。
   - Lambda 返回 `OperationResponse<(bool Mutated, LayerMutationResponse Result)>`;`Mutated = Results.Any(r => r.Success)`。
   - 对 `Modify / Delete / Purge` 而言,进入 per-item 执行前先完成一次 **request normalization + target resolution**:去重、检测批内冲突、把请求路径映射到当前文档中的稳定目标,避免前一项 rename / reparent / delete 后影响后一项寻址。
   - 一期明确禁止的批内冲突(upfront fail,不进入部分执行):
     - 同一 target 被重复命中(同一 `FullPath` 在 Delete / Purge / Modify 批中出现多次);
     - Modify 批内 rename / reparent 之后 `ResolvedNewFullPath` 与现有层或同批其他目标的 `ResolvedNewFullPath` 冲突;
     - Modify 批内出现循环依赖(例如同批把 `A` 变成 `B` 的子层,又把 `B` 变成 `A` 的子层);
     - **Modify 批内同时 rename / reparent 祖先层与其后代层**(两项操作共同会让后代项的 `ResolvedNewFullPath` 依赖执行顺序或与同批其他目标冲突);
     - Create 自动补父链同时又显式 Create 同一父路径且属性不一致。
   - **Delete / Purge 允许同批出现同一子树下多层**(如 `["Old", "Old::A", "Old::B"]`);service 内部按"深度优先 / 最深层先执行"排序,子层先被处理完再处理父层,避免"父层消失导致子层寻址失效"。该行为在 Tool 文档中明确说明。
   - **跨动作类别无需单独校验**:`ModifyLayers` / `DeleteLayers` / `PurgeLayers` 是三个独立 Tool,一个 Request 只承载一种动作,不存在跨类别混装入口。客户端若在一次会话里先 rename 再 delete,那是两次 Tool 调用、两个 Undo record,服务端只对每次调用内部做一致性校验。
   - 批量项 per-item `try/catch`,失败单项不阻断其他项 —— 与 document user string 写入一致。
   - 成功项汇总后 `document.Views.Redraw()`。
   - 一次 Tool 调用 = 一个 Undo 条目,用户 `Ctrl+Z` 一步回退整批 —— Purge 场景尤其重要(避免用户误删一批后无法回滚)。

4. **校验与硬错误**
   - **Create**:`FullPath` 非空、不含分隔符开头 / 结尾 / 连续 `::`;已存在则 per-item `Success=false` 带 `"Layer already exists: {path}"`(不阻断同批其他项)。`LinetypeName` / `RenderMaterialName` 若提供但解析失败 → 单项失败,消息含未解析的名称。
   - **Modify / Delete / Purge**:`FullPath` 必须命中已存在图层;命不中 → per-item `Success=false`。
   - **Modify.ParentFullPath** 非空时必须命中现存图层;传 `""` 表示迁到根层。**自引用 / 循环引用**(把 `A` 的父改到 `A::B` 或自身)检测后拒绝(`"Circular parent: ..."`)。
   - **Modify** 的属性写入分三类语义:字段缺席 = 不改;字段给值 = 显式覆盖;`Clear*` = 清空显式值并恢复继承。`null` 不承担"清空"含义,避免 client 语义歧义。
   - **Delete / Purge** 若**目标层本身或其任一后代层** = `CurrentLayerIndex` 所指层,提前失败(`"Cannot delete the current layer (or a subtree containing it): {fullPath}"` / `"Cannot purge the current layer (or a subtree containing it): {fullPath}"`),让用户先切换当前层。检查范围覆盖整棵将被删除的子树,而不只是顶层 —— 例如目标 `Floor`、当前层 `Floor::A` 时同样拒绝,避免 Apply 成功后当前层消失、Rhino 自己重置到某个任意合法层、用户接下来建几何落到意料之外的层。Rhino `Delete` / `Purge` 返回 `false` 时也写入失败消息(例如 "layer has persistent references")。
   - **批内冲突**:具体条目清单见关键设计 #3,不在此重复。**额外一项**:`DeleteLayers` 目标是根层(无父层可迁移)时整项拒绝 —— 根层下的对象没有 "上浮到父层" 的去处。客户端若确实想清根层,用 `PurgeLayers` 连带删对象。
   - **Purge** 在 Apply 调用前 Service 内部会先算一次 impact(直接对象数 + 子层数),并把数值写入结果 Message,方便用户事后复盘。
   - Contracts 层面复用现有硬错误集合:`NO_ACTIVE_DOCUMENT` / `ACTIVE_DOC_UNSAVED` / `FILE_NOT_ACTIVE` / `LIVE_RHINO_REQUIRED` / `RHINO_MAIN_THREAD_BUSY` 由 `ILiveRhinoDocumentAccessor` 自动返回,Service 不重复处理。

5. **Preview 设计**
   - 三个 Preview 全部走 `_documentAccessor.Execute`(无 Undo record,只读 `RhinoDoc`)。
   - **PreviewModifyLayers**:每项返回 `ResolvedNewFullPath`(rename / reparent 后的完整路径)和 `FieldDiffs` 列表(`"Color: (r1,g1,b1) -> (r2,g2,b2)"` 等逐字段字符串),若 rename / reparent 会影响子层,附 `AffectedSubLayers: IReadOnlyList<LayerSubtreeEntry>`(每项含 `FullPath`(变更前的路径)+ `DirectObjectCount`;客户端可根据 `ResolvedNewFullPath` 自行推出变更后的新路径)。无实变更的字段不写入 Diff —— 与 "空改动不产生 Undo 条目" 语义一致。
   - **PreviewDeleteLayers**:每项返回 `DirectObjectCount / DescendantObjectCount / TotalAffectedObjectCount / AffectedSubLayers / CurrentLayerInSubtree`。`TotalAffectedObjectCount` 表示"将被迁移到用户指定目标层父层的对象总数";`CurrentLayerInSubtree=true` 提示客户端 Apply 会被拒绝。
   - **PreviewPurgeLayers**:与 Delete 同结构,但 `TotalAffectedObjectCount` 表示"将被删除的对象总数"。实现上继续复用 `LayerDeletionPreviewResponse` / `LayerDeletionImpactResponse` DTO,字段形状保持稳定,差异只体现在 Tool 名称与 Message 文案。
   - 不对 Create 做 preview —— Create 无前置状态可破坏,Preview 相当于回显参数,语义增值有限(与 Architecture 里"Preview-of-mutation 要对齐 Apply 文档状态"的原则相符;Create 不涉及既有文档状态)。

6. **对既有写入 Tool 的隐性价值**
   - `ApplyObjectEdits.SetLayer` 和 `Create*.LayerFullPath` 要求目标层已存在;有了 `CreateLayersTool` 后,典型流程变为"先 CreateLayers → 再 CreatePoints / ApplyObjectEdits",一次会话内不再卡图层缺失。
   - Purge 的"连带清对象"能力与 `DeleteObjects` 工具配合:既可以 `PreviewPurgeLayers` 看有多少对象被牵连,也可以先 `DeleteObjects` 筛出部分对象单独保留,再 `DeleteLayers` 软删图层。

## 涉及文件

**新增**:
- `src/MCP_Rhino.Server/Tools/Layers/GetLayersTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/CreateLayersTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/ModifyLayersTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/DeleteLayersTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/PurgeLayersTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/PreviewModifyLayersTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/PreviewDeleteLayersTool.cs`
- `src/MCP_Rhino.Server/Tools/Layers/PreviewPurgeLayersTool.cs`
- `src/MCP_Rhino.Server/Application/Services/RhinoLayerManagementService.cs`
- `src/MCP_Rhino.Server/Domain/Models/RhinoLayerDetail.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/GetLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/LayerCreationEntryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/CreateLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/LayerModificationEntryRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ModifyLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/DeleteLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PurgeLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewModifyLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewDeleteLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/PreviewPurgeLayersRequest.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerReadResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerMutationResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerMutationResultResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerModificationPreviewResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerModificationImpactResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerDeletionPreviewResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerDeletionImpactResponse.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/LayerSubtreeEntry.cs`
- `Project_Test/260421_TEST_layer-management-tools/DeveloperCommandHandler.LayerManagementSmokeTest.cs`

**修改**:
- `src/MCP_Rhino.Server/Server/DependencyInjection.cs` —— `AddRhinoApplication()` 中 `AddSingleton<RhinoLayerManagementService>()`.
- `src/MCP_Rhino.Server/Infrastructure/CLI/DeveloperCommandHandler.cs` + `DeveloperCommandHandler.Parsing.cs` —— 新增 `layer-management-smoke-test` 子命令,对齐 `online-mutation-refactor-smoke-test` 的模板,覆盖全部 8 个 Tool。
- `src/MCP_Rhino.Server/Infrastructure/Plugin/McpDevSmokeCommand.cs` —— 追加 `layer-management-smoke-test` 入口或参数分支,保证 Rhino 内 `_McpDevSmoke` 可复用 developer handler 跑 live smoke。

**复用(不改)**:
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveRhinoDocumentAccessor.cs`(既有 `Execute` / `ExecuteWithUndo` / `TryGetActiveDocumentState` 三个入口)。
- `src/MCP_Rhino.Server/Application/Interfaces/IRhinoDocumentRepository.cs`(offline read)。
- `src/MCP_Rhino.Server/Contracts/Responses/ObjectEditWarning.cs`(`OFFLINE_READ_STALE` 等 warning code 复用,不新增 warning code)。
- `src/MCP_Rhino.Server/Domain/Models/RhinoDisplayColor.cs`(Color / PlotColor 字段复用此 DTO)。
- `src/MCP_Rhino.Server/Application/Services/RhinoDocumentUserStringService.Mutate`(mutation 模板参考,不复制代码)。

## 使用方式

MCP Client 配置不变(仍通过 `MCP_Rhino.Bridge.exe` 连 `\\.\pipe\mcp_rhino`)。新增 Tool 一览:

- `GetLayers(filePath)` —— 返回全部图层详情 + 对象数 + 打印/线型/材质属性;offline 可用。
- `CreateLayers(filePath, entries=[{fullPath, color?, visible?, locked?, plotColor?, plotWeight?, linetypeName?, renderMaterialName?}])` —— 批量建层,自动补父链。
- `ModifyLayers(filePath, entries=[{fullPath, newName?, newParentFullPath?, color?, visible?, locked?, plotColor?, plotWeight?, linetypeName?, renderMaterialName?, clearPlotColor?, clearPlotWeight?, clearLinetype?, clearRenderMaterial?}])` —— 以 `fullPath` 定位,按字段覆盖;`clear*` 用于恢复继承而非传 `null`。
- `DeleteLayers(filePath, fullPaths=[...])` —— **删除整棵子树**:被指定层及其所有后代层一并移除,**整棵子树上的对象统一迁到用户指定目标层的父层**(不是各自原父层)。Service 内部走"最深层先删"。整个批次的对象迁移 + 层删除合并到同一个 Undo record。
- `PurgeLayers(filePath, fullPaths=[...])` —— 硬删,连带清光被指定层及所有子层上的对象。
- `PreviewModifyLayers(filePath, entries=[...])` —— 返回 `ResolvedNewFullPath` / `FieldDiffs` / `AffectedSubLayers`(每项含 `FullPath` + `DirectObjectCount`)。
- `PreviewDeleteLayers(filePath, fullPaths=[...])` —— 返回 `DirectObjectCount` / `DescendantObjectCount` / `TotalAffectedObjectCount`(将被迁移到目标层父层的总数) / `AffectedSubLayers` / `CurrentLayerInSubtree`(true 即 Apply 会被拒绝)。
- `PreviewPurgeLayers(filePath, fullPaths=[...])` —— 同 Delete 的字段形状,`TotalAffectedObjectCount` 改为"将被**删除**的对象总数"。

典型流程:
- `PreviewPurgeLayers → PurgeLayers`(先看会删掉多少对象再动手)。
- `GetLayers → CreateLayers → CreatePoints(落在新层)`(新建层再建几何)。
- `PreviewModifyLayers → ModifyLayers`(想 reparent / rename 前先看影响面)。

## 验证

1. **build**:`dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo` 无警告通过;`dotnet build src/MCP_Rhino.Bridge/...` 同步通过。
2. **CLI fallback smoke**:`dotnet run --project src/MCP_Rhino.Server -- layer-management-smoke-test test-files/MCP_METtest.3dm`
   - `GetLayers` 返回 offline 读取结果,字段含 PlotColor/PlotWeight/LinetypeName/RenderMaterialName;`Warnings.Count == 0`(无 live 宿主可查 stale)。
   - `CreateLayers` / `ModifyLayers` / `DeleteLayers` / `PurgeLayers` / `PreviewModifyLayers` / `PreviewDeleteLayers` / `PreviewPurgeLayers` 全部返回 `LIVE_RHINO_REQUIRED`。
   - 工作副本对象数和图层数无变化。
3. **Live 手工 smoke(Rhino 内)**:
   - 载入 `.rhp`,打开已保存的 `.3dm`,在 Rhino 命令行跑 `_McpDevSmoke`(由 `McpDevSmokeCommand` 转发到 `layer-management-smoke-test`,或接受参数切到该分支)。
   - 跑完后:Edit → Undo 能把 Create / Modify / Delete / Purge 各一步回退;viewport 面板里新建 layer 实时可见;关闭文档无落盘。
4. **对 Undo 栈的空条目检查**(best-effort):`ModifyLayers` 传入已是目标颜色的 entries(无实际改动),观察 Rhino Undo History 面板。**当前 probe 未能验证此行为** —— 用 `Untitled.3dm` 跑时 `BeginUndoRecord` 返回 serial=0(新建未保存文档 Undo recording 不稳定),无法判断 UndoManager 对空记录的策略。实现阶段在已保存的 .3dm 上重测一次;`ExecuteWithUndo` 对 `undoRecord==0` 已经做了短路保护,即使底层不按预期丢弃空记录,最多是多一个空条目,不影响功能正确性。
5. **边界用例**:
   - Create 重名 → per-item `Success=false`,其他项仍成功。
   - Create `linetypeName="NotExist"` → 单项 fail,消息含 `"Linetype not found"`。
   - Delete / Purge 目标**本身**是当前层 → 整项拒绝。
   - Delete / Purge 目标的**任一后代层**是当前层(例如目标 `Floor`、当前层 `Floor::A`)→ 同样整项拒绝,错误消息含具体目标 FullPath;Preview 阶段 `CurrentLayerInSubtree=true` 会提前告知。
   - Modify 循环父级(`A` 的父级设为 `A::B`)→ 该项拒绝。
   - Modify 既不传某字段、也不传对应 `Clear*` → 该字段保持不变;传 `Clear*` 后读取结果应回到继承态(`null` / 未显式设置)。
   - Modify 同批次同时 rename / reparent 祖先层与其后代层 → upfront fail,避免结果依赖执行顺序。
   - Delete / Purge 同批次命中同一子树多层(如 `["Old", "Old::A"]`)→ 接受,按深度优先执行,均成功。
   - Delete 根层(无父层可迁)→ 整项拒绝,错误消息提示改用 Purge。
   - Purge 一个空层 → 成功,Message 写 `"Purged layer [X], removed 0 objects"`。
   - Purge 一个带子层的层 → 子层也被清除(RhinoCommon `Purge` 行为);preview 应已列出子层,smoke test 验证 `DescendantObjectCount` / `TotalAffectedObjectCount` 反映完整影响面。

## 后续扩展方向

- 批量重命名 Skill:`LayerBatchRenameSkill`,支持正则 / 通配符批量改名 + reparent。
- 引用图层 / Worksession 图层:暴露 `AddReferenceLayer` 与只读标记。
- `LayerTable.CurrentLayerIndex` setter:提供 `SetCurrentLayer` 工具,写入 / 建几何默认落到某层。
- RDK 渲染材质绑定:若将来 MCP 需要操作 RDK 材质表而非文档 `Materials` 表,追加对应字段与 Service 分支。
