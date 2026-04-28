# 260423_EXET_geometry-edit-surface-composite

## 对应计划

- 计划文件：`Project_Plan/260423_PLAN_geometry-edit-surface-composite.md`
- 执行日期：2026-04-26

## 关联产物

- 测试文件夹：`Project_Test/260423_TEST_geometry-edit-surface-composite/`
- 相关 commit / PR：未生成

## 执行结果 / 实际落地范围

- 新增 surface composite MCP tools：
  - `PreviewEditSurfaceGeometry`
  - `ApplyEditSurfaceGeometry`
- 新增 surface 专属 Domain / Contract：
  - `SurfaceEditSpec`
  - `SurfaceEditOperationSpec`
  - `EditableSurfacePointGrid`
  - `SurfacePointSelectorSpec`
  - `SurfaceEditDerivedOperationParameters`
  - `SurfaceControlPointGridSnapshot`
  - `SurfacePointSelectorKind`
  - `SurfacePointLayoutKind`
  - `PreviewEditSurfaceGeometryRequest`
  - `ApplyEditSurfaceGeometryRequest`
- 新增 `ISurfaceEditOrchestrator` / `SurfaceEditOrchestrator`，与 curve orchestrator 并列。
- 扩展既有 edit 抽象与 live 实现：
  - `IGeometryEditValidator`
  - `IGeometryEditStrategyResolver`
  - `IDerivedPointOperationEvaluator`
  - `IGeometryReconstructor`
  - `IGeometryTransformExecutionBridge`
- 支持 surface DirectOverride：
  - `Flat` layout：row-major `Index = u * CountV + v`
  - `Grid` layout：二维 `Rows[U][V]`
- 支持 surface DerivedOperation：
  - `ScaleAboutCentroid`
  - `TranslateByVector`
  - `OffsetAlongNormal`
- 支持 surface selector：
  - `All`
  - `Indices`
  - `UvRange`
  - `EdgeOnly`
- 支持 surface strategy routing：
  - DirectOverride -> `ReconstructFromControlPoints`
  - Derived + All + Translate -> `ExactTransform`
  - Derived + All + uniform Scale -> `ExactTransform`
  - 其余 derived -> `ReconstructFromControlPoints` + `STRATEGY_FORCED_RECONSTRUCTION`
- Apply undo 名称为 `MCP:EditSurfaceGeometry`。
- untrimmed single-face Brep reconstruction 路径会用 `Brep.CreateFromSurface` 回包，避免 Apply 后退化成裸 `Surface`。
- 新增 Rhino live smoke command：`_McpGeometryEditSurfaceCompositeSmoke`。
- 新增 CLI slug：`geometry-edit-surface-composite-smoke-test`。

## 与计划的偏差

- 计划中提到通过 `BrepDowngradeResult.Rebuild` 回包 Brep。本次没有扩展 `BrepDowngradeResult`，而是在 `LiveGeometryReconstructor.ReconstructSurface` 内根据原对象类型判断：原对象是 untrimmed single-face `Brep` 时，用 `Brep.CreateFromSurface(reconstructedSurface)` 生成 replacement geometry。行为目标一致，但实现位置不同。
- 当前非 Rhino Plugin 环境只能验证 live 工具的 `LIVE_RHINO_REQUIRED` fallback；`_McpGeometryEditSurfaceCompositeSmoke` 已编译接入，但尚未在 Rhino 内实跑。
- `PlaneSurface` DirectOverride 通过 `ToNurbsSurface()` 重建，Apply 后可能成为 `NurbsSurface` 语义；已通过 warning `RECONSTRUCTION_SEMANTICS_CHANGED` 暴露。

## 施工中发现并修复的问题

- surface DirectOverride 需要在 Summary descriptor 下先校验 `(CountU, CountV)`，再把 `Grid` layout 归一化为 row-major flat points。
- surface exact transform 不需要 Full descriptor；orchestrator 在 Summary descriptor 下生成 synthetic derived result，用于返回 `ResolvedPointIndices` 与 `DerivedOperationApplied`。
- surface normal offset 使用 `IGeometryFrameSampler` 的 `AtControlPointGrevilles`，返回 `ResolvedUvSamples`，保证 UV 采样可复核。

## 测试记录

构建：

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo
```

- 退出码：0
- 结果：Build succeeded，0 warnings，0 errors

Surface D CLI fallback：

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-surface-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

- 退出码：0
- 关键输出：
  - `PreviewEditSurfaceGeometry rejected in CLI fallback`
  - `ApplyEditSurfaceGeometry rejected in CLI fallback`

A/B/C 回归：

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-derived-routing-smoke-test Runtime_Test/MCP_rhino_test.3dm
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-curve-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-molecular-foundation-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

- 三条命令退出码均为 0
- 均在 CLI fallback mode 下正确返回 live Rhino required guard

未执行：

```text
_McpGeometryEditSurfaceCompositeSmoke
```

- 原因：当前执行环境不是 Rhino Plugin live session。

## 验收判据对齐

- 构建通过。
- 非 Rhino 环境下 surface preview/apply 不做 offline mutation，返回 `LIVE_RHINO_REQUIRED`。
- Rhino command 与 CLI slug 独占接入。
- A/B/C smoke 在本次修改后继续通过。
- Live smoke 覆盖代码已添加：
  - `PlaneSurface` DirectOverride Flat preview/apply
  - `NurbsSurface` DirectOverride Grid apply
  - untrimmed single-face `Brep` ExactTransform apply
  - untrimmed single-face `Brep` DirectOverride reconstruction apply，并校验仍为 `Brep`
  - `UvRange` + `OffsetAlongNormal`
  - `EdgeOnly` + `TranslateByVector`
  - selector / scale / expected-strategy guard

## 回退验证

- 既有 curve composite、derived routing、molecular foundation CLI fallback smoke 均保持退出码 0。
- 既有 `TransformObjects` 公共 undo 名称未改；surface exact transform 只通过 C 计划新增的 internal bridge 传入 `MCP:EditSurfaceGeometry`。

## 当前遗留项

- 需要在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm` 后运行 `_McpGeometryEditSurfaceCompositeSmoke`，完成真实文档 mutation / Undo / viewport / metadata replay 验证。
- closed / periodic NurbsSurface seam case 尚未在当前 smoke 中构造专门样例；代码已加入 `SURFACE_CLOSURE_SEAM_INCONSISTENT` warning 逻辑。
- trimmed Brep / polysurface 仍按计划返回 `EDITABLE_KIND_UNSUPPORTED`，未实现 trim replay。

## 结论

`geometry-edit-surface-composite` 已完成代码落地、DI 注册、MCP tool 暴露、CLI/Rhino smoke 接入与非 Rhino 环境验证。当前闭环状态为：本地构建和 CLI fallback 回归通过，Rhino live smoke 待在 Rhino Plugin 环境中执行。
