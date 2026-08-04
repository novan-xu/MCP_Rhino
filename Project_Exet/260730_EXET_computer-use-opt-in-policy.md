# Computer Use Opt-In Policy Execution Report

## 对应计划

- Plan: `Project_Plan/260730_PLAN_computer-use-opt-in-policy.md`
- Execution date: 2026-07-30

## 关联产物

- Test folder: `Project_Test/260730_TEST_computer-use-opt-in-policy/`
- Policy verification: `Project_Test/260730_TEST_computer-use-opt-in-policy/verify-agents-computer-use-opt-in.ps1`
- Commit / PR: none

## 执行结果 / 实际落地范围

Added a hard constraint to `AGENTS.md` that prohibits Computer Use and other Windows UI automation unless the user explicitly requests that automation in the current request. The rule also states that mentioning an application or file, or asking to open or go to one, is not sufficient authorization.

## 与计划的偏差

None.

## 施工中发现并修复的问题

No implementation issue was found. Existing unrelated working-tree changes were left untouched.

## 测试记录

Command:

```powershell
& '.\Project_Test\260730_TEST_computer-use-opt-in-policy\verify-agents-computer-use-opt-in.ps1'
```

Result:

```text
PASS: AGENTS.md requires explicit current-request authorization for Computer Use.
```

Exit code: `0`.

The verification asserts that both required policy statements exist and that the policy is located inside the `Hard Constraints` section. Debug and Release builds were not run because this change does not affect the MCP server, Rhino plugin host, bridge, tool registration, panel/session routing, or runtime prompt injection.

## 验收判据对齐

- Explicit opt-in requirement: passed.
- Current-request scope: passed.
- App/file and open/go-to clarification: passed.
- Placement under `Hard Constraints`: passed.
- Unrelated working-tree changes preserved: passed.

## 回退验证

The change is isolated to one policy bullet plus its PLAN, TEST, and EXET records. Removing those additions restores the previous repository behavior without affecting runtime code.

## 当前遗留项

None.

## 结论

The repository now requires explicit user authorization before Computer Use or any other Windows UI automation may be used.
