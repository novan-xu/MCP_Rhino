# Reference Image Object Modeling Agent EXET

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_reference-image-object-modeling-agent.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Test folder: `Project_Test/260508_TEST_reference-image-object-modeling-agent/`
- Commit / PR: not created in this execution session

## Execution Result / Actual Scope

Implemented the first orchestrated `ReferenceImageObjectModelingAgent` slice.

Added:

- `ReferenceImageObjectModelingAgent`
- `ReferenceImageObjectModelingAgentRequest`
- `ReferenceImageObjectModelingAgentResponse`
- `ReferenceImageObjectModelingPlan`
- agent status and capability-gap enums
- agent prompt template under `src/MCP_Rhino.Server/Prompts/Modeling/`
- DI registration in `AgentRegistration`
- CLI smoke slug: `reference-image-object-modeling-agent-smoke-test`
- Rhino live smoke command: `_McpReferenceImageObjectModelingAgentSmoke`

The agent now sequences:

- structured brief construction
- primitive decomposition
- initial massing
- required visual QA after initial massing
- detail refinement planning
- simple raised-strip detail execution
- material planning, creation, and assignment
- final visual QA capture
- iteration decision and status/gap reporting

## Important Boundary

The agent does not perform raw bitmap object recognition inside the C# Rhino server. It requires structured image observations in `BriefRequest`. A raw image-only request intentionally fails validation until a real multimodal image connector or upstream LLM handoff exists.

## Deviations From Plan

- Bounded iteration is represented in the trace and decision policy, but the first implementation does not re-run multiple geometry correction loops. It reports `NeedsIteration` or a capability gap when the decision says another pass is required.
- Detail execution is limited to simple raised strips. More general grooves, cutouts, handles, repeated details, decals, and texture mapping remain future tool slices.
- Visual QA capture is performed, but image similarity scoring remains outside the server. The returned QA capture/checklist is intended for an LLM or external evaluator.

## Issues Found And Fixed During Construction

- The first agent implementation used `ReferenceImageVisualQaCheckpointKind.FinalReview`, but the existing Visual QA contract names the final gate `FinalAcceptance`. The agent was corrected to use the existing enum value.
- The CLI smoke now explicitly verifies that raw image-only requests fail instead of pretending the Rhino server can infer objects from pixels.

## Test Record

Debug server build with alternate output:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath=C:\01_Projects\MCP_Rhino\.validation\server-debug\
```

Result:

- exit code: 0
- warnings: 0
- errors: 0

Debug agent smoke from alternate output:

```powershell
dotnet C:\01_Projects\MCP_Rhino\.validation\server-debug\MCP_Rhino.Server.dll reference-image-object-modeling-agent-smoke-test
```

Result:

- `[OK] reference-image-object-modeling-agent CLI fallback produced structured plan then live-Rhino gap.`
- `[OK] raw image-only request failed validation until a multimodal image connector exists.`

Debug safety smoke from alternate output:

```powershell
dotnet C:\01_Projects\MCP_Rhino\.validation\server-debug\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 136 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Standard Debug solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

Result:

- exit code: 1
- cause: `src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.dll` locked by `Rhino 8 (51408)`
- Bridge and Companion Debug projects built before the Server copy failed.

Release solution build:

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

Result:

- exit code: 0
- warnings: 0
- errors: 0

Release agent smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll reference-image-object-modeling-agent-smoke-test
```

Result:

- `[OK] reference-image-object-modeling-agent CLI fallback produced structured plan then live-Rhino gap.`
- `[OK] raw image-only request failed validation until a multimodal image connector exists.`

Release safety smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll mcp-tool-safety-annotations-smoke-test
```

Result:

- `[OK] MCP safety annotations verified for 136 tools.`
- `[OK] No bare method-level [McpServerTool] attributes remain.`

Whitespace check:

```powershell
git diff --check
```

Result:

- exit code: 0
- only existing LF-to-CRLF warnings

Live Rhino smoke:

- command added: `_McpReferenceImageObjectModelingAgentSmoke`
- not executed in this terminal session
- expected behavior: builds a timestamped sofa-like reference-object layer from structured image observations, captures initial massing QA, adds a raised-strip detail, creates/applies material, captures final acceptance QA, and reports completion or partial completion.

## Acceptance Alignment

- General-purpose agent name and scope: satisfied.
- Agent uses skills/tools/services instead of direct RhinoCommon access: satisfied.
- Visual QA after initial massing before details/materials: satisfied.
- Structured trace and response include plan, massing, QA, detail, material, decision, warnings, and gaps: satisfied.
- Bounded iteration policy is represented through request limits and decision status: satisfied for first slice.
- Capability gaps are explicit for missing live Rhino, missing QA, missing material/detail operations, and raw image inference: satisfied.
- Tool safety annotation smoke remains passing: satisfied.
- Debug validation: alternate Debug server build and smokes passed; standard Debug solution build remains blocked by the active Rhino lock.
- Release validation: solution build and smokes passed.

## Rollback Verification

Rollback would remove:

- agent class
- agent request/response DTOs
- `ReferenceImageObjectModelingPlan`
- agent status/gap enum additions
- prompt template
- DI registration
- DeveloperCommandHandler agent smoke hook
- Rhino smoke command
- TEST folder and this EXET

Lower-level visual QA, primitive, material, and skill capabilities remain independently usable.

## Current Remaining Items

- Run `_McpReferenceImageObjectModelingAgentSmoke` inside Rhino after loading the rebuilt plugin.
- Add a later true image-understanding connector or upstream multimodal handoff so raw image-only requests can produce structured briefs.
- Add future detail/material tool slices for cutouts, grooves, repeated details, decals, texture images, and mapping controls.
- Add real visual comparison scoring if a local or external vision evaluator becomes available.

## Conclusion

The first `ReferenceImageObjectModelingAgent` implementation is complete and validated in CLI fallback mode. It closes the main orchestration gap by making the model-building process explicit: structured image brief, decomposed massing, early visual QA, detail/material refinement, final QA, and honest gap reporting when the current server cannot do a step.
