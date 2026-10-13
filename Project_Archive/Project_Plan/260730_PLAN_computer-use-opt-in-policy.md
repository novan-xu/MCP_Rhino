# Computer Use Opt-In Policy Plan

## 背景

The repository instructions do not currently state that Windows Computer Use is opt-in. This allowed an agent to infer UI-automation permission from a request to navigate to a Rhino document.

## 目标

Add a repository-level hard constraint that prohibits Computer Use and other Windows UI automation unless the user explicitly requests that automation in the current request.

## 架构归属

This is workflow-infrastructure policy owned by the repository entry point, `AGENTS.md`. It does not change Rhino tools, skills, agents, runtime prompts, or MCP registration.

## 关键设计

- Place the rule under `AGENTS.md` → `Hard Constraints` so it applies before runtime routing.
- Require explicit, current-request authorization for Computer Use or Windows UI automation.
- Clarify that naming an application or file, or asking to open/go to one, is not authorization by itself.

## 涉及文件

- `AGENTS.md`
- `Project_Test/260730_TEST_computer-use-opt-in-policy/verify-agents-computer-use-opt-in.ps1`
- `Project_Exet/260730_EXET_computer-use-opt-in-policy.md`

## 使用方式

Agents read `AGENTS.md` before repository actions. They may use Computer Use only when the user explicitly asks for Computer Use or Windows UI automation in the current request.

## 验收标准

- `AGENTS.md` contains an unambiguous opt-in-only Computer Use rule under `Hard Constraints`.
- The rule rejects implied authorization from app/file navigation requests.
- The focused policy verification script exits successfully.
- No unrelated working-tree changes are modified.

## 风险与回退方案

The rule may prevent otherwise convenient UI navigation. The intended recovery is to ask the user to explicitly authorize Computer Use. Reverting the single policy bullet restores the previous behavior.

## 后续扩展方向

If additional automation surfaces require opt-in treatment, extend this policy deliberately without weakening the explicit authorization requirement.
