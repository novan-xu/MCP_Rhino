# 260423_EXET_surface-point-order-rebuild

## Review Follow-up (2026-04-26)

- Scope: tightened `PreviewRedefineSurfacePointOrder` and `ApplyRedefineSurfacePointOrder` so rebuild execution only proceeds for quad outer boundaries.
- Runtime behavior: `InspectSurfaceRebuildDescriptor` still reports non-quad boundary diagnostics; Preview/Apply return per-object skip reason `SURFACE_POINT_ORDER_REBUILD_REQUIRES_QUAD_SURFACE` for non-quad targets.
- Tool contract text updated to state the quad-only execution constraint.
- Test artifact updated: `Project_Test/260423_TEST_surface-point-order-rebuild/DeveloperCommandHandler.SurfacePointOrderRebuildSmokeTest.cs` now expects the polygon Brep fixture to be skipped by Preview/Apply while the quad target remains executable.
- Verification:
  - `dotnet build MCP_Rhino.sln` -> exit code 0; build succeeded with 0 warnings and 0 errors.
  - `dotnet run --no-build --project src/MCP_Rhino.Server -- surface-point-order-rebuild-smoke-test Runtime_Test/MCP_rhino_test.3dm` -> exit code 0; CLI fallback still rejects Inspect/Preview/Apply with `LIVE_RHINO_REQUIRED`.
  - `dotnet test MCP_Rhino.sln --no-build` -> exit code 0; no separate test project output in this solution.
- Not run: `_McpSurfacePointOrderRebuildSmoke`, because the current shell is not a Rhino Plugin live session.

## 对应计划

- 计划文件：`Project_Plan/260423_PLAN_surface-point-order-rebuild.md`
- 执行日期：2026-04-26

## 关联产物

- 测试文件夹：`Project_Test/260423_TEST_surface-point-order-rebuild/`
- 相关 commit / PR：未生成

## 执行结果 / 实际落地范围

- 新增 Plan E 专属四个分子：
  - `IBoundaryReferenceCurveAnalyzer`
  - `ISurfaceLocalCoordinateSystemBuilder`
  - `ISurfaceBoundaryPointOrderer`
  - `IBoundaryDrivenSurfaceReconstructor`
- 新增编排入口：
  - `ISurfaceRebuildOrchestrator`
  - `SurfaceRebuildOrchestrator`
- 新增 MCP tools：
  - `InspectSurfaceRebuildDescriptor`
  - `PreviewRedefineSurfacePointOrder`
  - `ApplyRedefineSurfacePointOrder`
- 新增内部 Skill：
  - `SurfacePointOrderRebuildSkill`
- 新增 Domain / Contract 模型：
  - reference edge、boundary loop、LCS、ordered point、point-order plan、rebuild spec、descriptor、preview/apply response 等模型
  - route/topology/guide/direction/start-anchor enums
- 新增 live implementations：
  - `LiveBoundaryReferenceCurveAnalyzer`
  - `LiveSurfaceLocalCoordinateSystemBuilder`
  - `LiveBoundaryDrivenSurfaceReconstructor`
- 扩展 `IGeometryMutationService`：
  - 新增 `ReplaceManyWithMetadata(...)`
  - 支持同一次 Apply 调用内多个成功对象共享一个 Undo record：`MCP:RedefineSurfacePointOrder`
- 新增 Rhino live smoke command：`_McpSurfacePointOrderRebuildSmoke`。
- 新增 CLI slug：`surface-point-order-rebuild-smoke-test`。

## 与计划的偏差

- 自动参考边识别采用稳定的首轮规则：按边 midpoint 的 world Y 优先、world Z 微调、长度降序。计划中的“局部 -Z / 最低边”规则没有完整实现为独立可配置策略；后续若实测需要，可把 scoring 抽成更明确的 Domain policy。
- LCS builder 当前直接基于 boundary loop Newell normal 构造法向，没有显式调用 `IGeometryFrameSampler`。对本期支持的 planar/quad boundary 输入足够稳定，但与计划“基于分子 3 frame sampler”的实现细节不同。
- `SurfacePointOrderRebuildSkill` 已作为内部 Skill 注册，但只提供 Inspect / Preview / Apply 三个薄编排入口；计划里提到的自动拆分 “OK / NEEDS_EXPLICIT_REFERENCE” 批处理策略尚未做成更高阶结果模型。
- Rhino live smoke 已接入但未在当前环境实跑。

## 施工中发现并修复的问题

- Plan E 的 Apply 需要批量共享 Undo，单对象 `ReplaceWithMetadata` 不够用，因此在既有 mutation boundary 内新增了 `ReplaceManyWithMetadata`，没有绕开现有 metadata snapshot/replay 逻辑。
- Preview/Apply 需要区分文档级 live failure 和对象级 skip；当前 `LIVE_RHINO_REQUIRED` / `NO_ACTIVE_DOCUMENT` / `FILE_NOT_ACTIVE` 等直接返回 top-level failure，其余对象级错误进入 skip 结果。
- BoundarySurface 路径限定为 planar、single outer loop、无 inner loop；非平面或内环明确返回错误，不做临时重建规则。
- 收口时将带 `RhinoCommon GeometryBase` 的 `BoundaryRebuildResult` 放在 `Application/Models`，并把 Domain plan/descriptor warning 改为纯字符串 code，避免 Domain 反向依赖 Contracts 或 RhinoCommon。

## 测试记录

构建：

```powershell
dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj --nologo
```

- 退出码：0
- 结果：Build succeeded，0 warnings，0 errors

Plan E CLI fallback：

```powershell
dotnet run --project src/MCP_Rhino.Server -- surface-point-order-rebuild-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

- 退出码：0
- 关键输出：
  - `InspectSurfaceRebuildDescriptor rejected in CLI fallback`
  - `PreviewRedefineSurfacePointOrder rejected in CLI fallback`
  - `ApplyRedefineSurfacePointOrder rejected in CLI fallback`

A-D 回归：

```powershell
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-surface-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-derived-routing-smoke-test Runtime_Test/MCP_rhino_test.3dm
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-curve-composite-smoke-test Runtime_Test/MCP_rhino_test.3dm
dotnet run --project src/MCP_Rhino.Server -- geometry-edit-molecular-foundation-smoke-test Runtime_Test/MCP_rhino_test.3dm
```

- 四条命令退出码均为 0
- 均在 CLI fallback mode 下正确返回 live Rhino required guard

未执行：

```text
_McpSurfacePointOrderRebuildSmoke
```

- 原因：当前执行环境不是 Rhino Plugin live session。

## 验收判据对齐

- 构建通过。
- 非 Rhino 环境下 Inspect / Preview / Apply 不做 offline mutation，返回 `LIVE_RHINO_REQUIRED`。
- 新增三条 MCP Tool、一个内部 Skill、一个 Rhino command 和一个独占 CLI slug。
- Apply 使用 `MCP:RedefineSurfacePointOrder` undo 名称。
- A-D 已有 smoke 在本次修改后继续通过。
- Live smoke 覆盖代码已添加：
  - quad `PlaneSurface` -> `FourPoint`
  - planar polygon `Brep` -> non-quad skip in Preview/Apply
  - batch Apply 两个对象
  - explicit `ReferenceEdgeIndex`
  - missing ObjectId guard
  - cleanup 后对象数和 document strings 数回到原值

## 回退验证

- `geometry-edit-molecular-foundation-smoke-test`
- `geometry-edit-curve-composite-smoke-test`
- `geometry-edit-derived-routing-smoke-test`
- `geometry-edit-surface-composite-smoke-test`

以上四条回归命令均保持退出码 0。

## 当前遗留项

- 需要在 Rhino 中打开并保存 `Runtime_Test/MCP_rhino_test.3dm` 后运行 `_McpSurfacePointOrderRebuildSmoke`，完成真实 mutation / Undo / metadata replay 验证。
- ambiguous reference edge fixture 尚未实测；当前模型和错误码已保留 `REFERENCE_CURVE_AMBIGUOUS`。
- 带 inner loop、非平面多边面、多面 polysurface、Mesh/SubD 仍按计划不支持。
- 自动参考边评分策略需要 Rhino live 实战后再决定是否从 world-axis heuristic 升级为更完整的 local-frame policy。

## 结论

`surface-point-order-rebuild` 已完成代码落地、DI / Skill 注册、MCP tool 暴露、CLI/Rhino smoke 接入与非 Rhino 环境验证。当前闭环状态为：本地构建和 A-D/E CLI fallback 回归通过，Rhino live smoke 待在 Rhino Plugin 环境中执行。
