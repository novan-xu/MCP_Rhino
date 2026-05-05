# Surface Direction Tweak

## 背景

Surface point-order rebuild 已经可以按 surface-local gravity frame 得到正确点序。该重建会让部分 surface 的自然 normal 或 U/V parameter direction 与建模预期相反。Rhino 的 `Dir` / Direction Display 命令提供 `UReverse`、`VReverse`、`SwapUV`、`FlipNormal` 等方向调整能力，可以调整 surface parameterization / face orientation，而不重新计算点序或移动几何形状。

## 目标

- 新增 live-only MCP 能力，用于批量预览与应用 surface direction tweak。
- 支持单个 `Surface` 与 single-face `Brep` panel。
- 支持操作：`ReverseU`、`ReverseV`、`SwapUV`、`FlipNormal`。
- Apply 走 Rhino live document 与 Rhino Undo record，不写离线 `.3dm`。
- Preview 不修改文档，返回 tweak 前后的 U/V tangent 与 normal 摘要。

## 架构归属

- `Tools/Geometry/Rebuild`：新增 MCP 原子 Tool，暴露 preview/apply。
- `Application/Services/Rebuild`：新增 use-case orchestrator，负责请求校验与响应组织。
- `Application/Interfaces`：新增 live adapter 抽象。
- `Infrastructure/Rhino/Live`：封装 RhinoCommon `Surface.Reverse(direction, true)`、`Surface.Transpose(true)`、BrepFace orientation tweak 与 `RhinoDoc` mutation。
- `Contracts` / `Domain`：新增请求、响应 DTO 与 enum。
- `Project_Test/260428_TEST_surface-direction-tweak`：新增 live smoke / CLI fallback smoke。

## 关键设计

- Preview：在 live document 中读取对象，duplicate geometry，在 duplicate 上应用 direction tweak，比较 before / after direction snapshot，不开启 Undo。
- Apply：在 live document 中一次 Undo record 内 duplicate geometry、应用 tweak、通过现有 metadata replay 语义保留属性并替换对象。
- `ReverseU` 映射 RhinoCommon `Surface.Reverse(0, true)`；`ReverseV` 映射 `Surface.Reverse(1, true)`；`SwapUV` 映射 `Surface.Transpose(true)`。
- `FlipNormal` 对 single-face Brep 优先切换 `BrepFace.OrientationIsReversed`；对裸 `Surface` 通过 `Reverse(0, true)` 实现 normal 翻转。
- 多面 Brep 暂不支持，避免在 polysurface 上误改多个 face 的 trim/normal 关系。

## 涉及文件

- `src/MCP_Rhino.Server/Domain/Enums/SurfaceDirectionTweakKind.cs`
- `src/MCP_Rhino.Server/Domain/Models/SurfaceDirectionSnapshot.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/*SurfaceDirectionTweak*.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/*SurfaceDirectionTweak*.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ISurfaceDirectionTweakOrchestrator.cs`
- `src/MCP_Rhino.Server/Application/Interfaces/ILiveSurfaceDirectionTweakService.cs`
- `src/MCP_Rhino.Server/Application/Services/Rebuild/SurfaceDirectionTweakOrchestrator.cs`
- `src/MCP_Rhino.Server/Infrastructure/Rhino/Live/LiveSurfaceDirectionTweakService.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/PreviewTweakSurfaceDirectionsTool.cs`
- `src/MCP_Rhino.Server/Tools/Geometry/Rebuild/ApplyTweakSurfaceDirectionsTool.cs`
- `Project_Test/260428_TEST_surface-direction-tweak/*`

## 使用方式

- Preview: `PreviewTweakSurfaceDirections(filePath, confirmedObjectIds, operations)`
- Apply: `ApplyTweakSurfaceDirections(filePath, confirmedObjectIds, operations)`
- 典型 operations：`["ReverseU"]`、`["ReverseV"]`、`["SwapUV"]`、`["ReverseU","ReverseV"]`。

## 验收标准

- CLI fallback 下 preview/apply 返回 `LIVE_RHINO_REQUIRED`。
- Live smoke 可以创建 temporary surfaces / Breps，preview 显示 direction snapshot 发生预期变化。
- Apply 使用单个 Undo record，保留对象 ID 与 metadata。
- Build 通过：`dotnet build src/MCP_Rhino.Server/MCP_Rhino.Server.csproj`、`dotnet build src/MCP_Rhino.Bridge/MCP_Rhino.Bridge.csproj`。
- Test 通过：`dotnet test MCP_Rhino.sln --no-build` 与新增 smoke slug。

## 风险与回退方案

- RhinoCommon direction APIs 改变 parameterization，可能改变内部 control-point indexing，但不应移动 3D shape；若下游依赖 raw CV index，应以 preview snapshot 先确认。
- Trimmed / multi-face Brep 的方向处理更复杂，本轮仅支持 single-face Brep；多面对象返回 skipped。
- 回退方式是 Rhino Undo record；代码回退可删除本能力新增文件与 DI/CLI 注册。

## 后续扩展方向

- 增加按 layer/filter 选择对象的 skill wrapper。
- 增加目标方向自动对齐模式，例如让 U 方向对齐 surface-local right/down。
- 支持 polysurface face-level subobject direction tweak。
