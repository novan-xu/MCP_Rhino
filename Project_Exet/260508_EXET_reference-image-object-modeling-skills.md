# Reference Image Object Modeling Skills EXET

## Corresponding Plan

- Plan: `Project_Plan/260508_PLAN_reference-image-object-modeling-skills.md`
- Execution date: 2026-05-08

## Associated Artifacts

- Test folder: `Project_Test/260508_TEST_reference-image-object-modeling-skills/`
- Commit / PR: not created in this execution session

## Execution Result / Actual Scope

Implemented the first deterministic skill layer for `ReferenceImageObjectModelingAgent`:

- `ReferenceImageModelBriefSkill`
- `ReferenceImagePrimitiveDecompositionSkill`
- `ReferenceImageInitialMassingSkill`
- `ReferenceImageDetailRefinementSkill`
- `ReferenceImageMaterialPlanningSkill`
- `ReferenceImageIterationDecisionSkill`

Added shared structured models:

- `ReferenceImageModelBrief`
- `ReferenceImagePrimitiveDecomposition`
- `ReferenceImageRefinementPlan`
- `ReferenceImageMaterialPlan`
- `ReferenceImageModelingTrace`

Added object-modeling enums for:

- edge character
- primitive vocabulary
- part role
- tool family
- material intent
- refinement action
- iteration decision

Added prompt templates under `src/MCP_Rhino.Server/Prompts/Modeling/`.

Added smoke coverage:

- CLI slug: `reference-image-object-modeling-skills-smoke-test`
- Rhino command: `_McpReferenceImageObjectModelingSkillsSmoke`

## Important Boundary

This skill slice does not perform image recognition inside the C# server. It consumes structured observations supplied by an LLM/agent/user and turns them into deterministic briefs, decompositions, plans, and live massing calls.

That boundary is intentional: the MCP Rhino server should not pretend to have multimodal understanding unless an actual image-analysis runtime or model connector is integrated.

## Deviations From Plan

- No MCP skill-tool wrappers were exposed. The skill layer is registered for agent/internal use and tested through CLI/Rhino smoke hooks.
- Image-to-brief extraction is implemented as structured brief construction from supplied observations, not raw bitmap interpretation.
- Initial massing execution supports the primitives currently available from the tools slice: rounded box, ellipsoid, cylinder, cone, capsule, and torus.
- Detail refinement and material planning are planning-only in this slice. Actual detail/material mutation remains owned by lower-level tools and future agent orchestration.

## Issues Found And Fixed During Construction

- The first decomposition heuristic treated a long soft cushion as a capsule because of aspect ratio alone. This was corrected so capsule selection requires semantic cues such as roll, rod, handle, rail, or bolster; broad soft cushions now remain rounded boxes.
- The skill smoke caught a naming mismatch in the refinement plan type before final validation.

## Test Record

Debug server build with alternate output:

```powershell
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath=C:\01_Projects\MCP_Rhino\.validation\server-debug\
```

Result:

- exit code: 0
- warnings: 0
- errors: 0

Debug skills smoke from alternate output:

```powershell
dotnet C:\01_Projects\MCP_Rhino\.validation\server-debug\MCP_Rhino.Server.dll reference-image-object-modeling-skills-smoke-test
```

Result:

- `[OK] reference-image-object-modeling-skills deterministic planning passed for sofa, bottle, and speaker categories.`
- `[OK] initial massing skill returned LIVE_RHINO_REQUIRED in CLI fallback mode.`

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

Release skills smoke:

```powershell
dotnet src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.dll reference-image-object-modeling-skills-smoke-test
```

Result:

- `[OK] reference-image-object-modeling-skills deterministic planning passed for sofa, bottle, and speaker categories.`
- `[OK] initial massing skill returned LIVE_RHINO_REQUIRED in CLI fallback mode.`

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

- command added: `_McpReferenceImageObjectModelingSkillsSmoke`
- not executed in this terminal session
- expected behavior: builds a sofa-like structured brief, decomposes it, then creates initial massing objects on a timestamped layer in the saved active Rhino document

## Acceptance Alignment

- Skills are general-purpose and object-oriented: satisfied.
- Skills produce structured DTOs suitable for future agent decisions and tests: satisfied.
- Initial massing is separated from detail refinement: satisfied.
- Detail refinement avoids texture-like geometry by routing texture cues to material: satisfied.
- Iteration decision consumes QA/trace data but does not own viewport capture or image comparison: satisfied.
- Skills do not access RhinoCommon directly: satisfied.
- No MCP skill tools were exposed, so no new safety annotations were needed.
- Tests cover at least three object categories: sofa, bottle, speaker.
- Debug validation: alternate Debug server build and smokes passed; standard Debug solution build remains blocked by the active Rhino lock.
- Release validation: solution build and smokes passed.

## Rollback Verification

Rollback would remove:

- new domain enums and models
- new contracts for skill requests/responses
- new planning services
- new skill classes
- prompt templates
- DI and agent registration entries
- DeveloperCommandHandler skill smoke hook
- Rhino smoke command
- TEST folder and this EXET

Existing tool, visual QA, material, primitive, and architectural capabilities remain independent.

## Current Remaining Items

- Run `_McpReferenceImageObjectModelingSkillsSmoke` inside Rhino after loading the rebuilt Release plugin.
- Execute the agent plan next so these skills become an orchestrated workflow.
- Later improve raw image brief extraction only if an actual multimodal model/tool connector is introduced.

## Conclusion

The reference-image object-modeling skills layer is implemented as a deterministic planning and handoff layer. It now gives the future agent stable stages for brief construction, primitive decomposition, initial massing, refinement planning, material planning, and iteration decisions without conflating those responsibilities with raw image understanding.
