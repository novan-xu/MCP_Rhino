# Standard Four Point Surface Rebuild EXET

## 对应计划

- Plan: `Project_Plan/260428_PLAN_standard-four-point-surface-rebuild.md`
- 执行日期: 2026-04-28

## 关联产物

- Test: `Project_Test/260428_TEST_standard-four-point-surface-rebuild/`
- Commit / PR: 当前工作区未提交

## 执行结果 / 实际落地范围

- 新增 domain default：`SurfaceDirectionTweakDefaults.StandardPostRebuildOperations()`。
- Direction tweak tools 现在在 `operations` 为空或省略时默认执行：
  1. `FlipNormal`
  2. `SwapUV`
- 新增标准四点 surface rebuild Skill：
  - `StandardFourPointSurfaceRebuildSkill`
- 新增 MCP 可见 Tool：
  - `ApplyStandardFourPointSurfaceRebuild`
- Tool description 明确声明该工具是 LLM 遇到 “rebuild 4 point surfaces/panels” 时的默认标准路径。
- 新增 Rhino live smoke command：
  - `_McpStandardFourPointSurfaceRebuildSmoke`

## 与计划的偏差

- 当前标准 workflow 复用既有两个 live mutation 分子，因此会产生两个 Undo records：
  - `MCP:RedefineSurfacePointOrder`
  - `MCP:TweakSurfaceDirections`
- 未在本轮合并为单一 Undo record；该项保留为后续优化。

## 施工中发现并修复的问题

- `ApplyTweakSurfaceDirections` / `PreviewTweakSurfaceDirections` 原先要求显式 operations；已改为 nullable/optional，并由 orchestrator 统一 normalize，避免 Tool 层和 Skill 层出现不同默认值。
- 标准 workflow response 合并 rebuild phase 与 direction phase 的 skipped / warning / metadata 信息，便于排查 post direction tweak 是否遗漏。

## 测试记录

- `dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- standard-four-point-surface-rebuild-smoke-test Runtime_Test\MCP_rhino_test.3dm`
  - Exit code: 0
  - Result: CLI fallback confirmed standard rebuild and default direction tweak require live Rhino
- `dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet build MCP_Rhino.sln`
  - Exit code: 0
  - Result: 0 warnings, 0 errors
- `dotnet test MCP_Rhino.sln --no-build`
  - Exit code: 0
- `dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -- surface-direction-tweak-smoke-test Runtime_Test\MCP_rhino_test.3dm`
  - Exit code: 0
  - Result: existing direction tweak CLI fallback remains valid

## 验收判据对齐

- Default direction operation set is now `FlipNormal + SwapUV`.
- Standard four-point rebuild workflow applies local lower-left / clockwise point-order rebuild and post direction tweak.
- LLM routing guidance is embedded in the MCP tool description.
- CLI fallback rejects mutation without live Rhino.
- Server, Bridge, solution build, and no-build tests passed.

## 回退验证

- Runtime geometry edits are covered by Rhino Undo records.
- Code rollback is isolated to:
  - default operation normalization
  - new standard rebuild Skill / Tool / response
  - CLI / AgentRegistration / plugin smoke registration

## 当前遗留项

- Reload the Rhino plugin and run `_McpStandardFourPointSurfaceRebuildSmoke`.
- Consider merging rebuild and direction tweak into one Undo record in a later pass.

## 结论

The standard four-point surface rebuild capability is implemented and non-live verification passed. After Rhino reload, the new tool should be the default LLM-facing path for four-point surface rebuild requests.
