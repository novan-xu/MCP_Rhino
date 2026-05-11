# Reference Image Agent Access And Briefing EXET

## Corresponding Plan

- Plan: `Project_Plan/260509_PLAN_reference-image-agent-access-and-briefing.md`
- Execution date: 2026-05-09

## Associated Artifacts

- Test folder: `Project_Test/260509_TEST_reference-image-agent-access-and-briefing/`
- Commit / PR: not created in this execution session

## Execution Result / Actual Scope

Implemented the access strategy:

- Added `Tools/Modeling/RunReferenceImageObjectModelingAgentTool.cs` as a thin MCP wrapper around `ReferenceImageObjectModelingAgent`.
- Added `Tools/Reference/GetReferenceImageBriefSchemaTool.cs` so clients can discover the required structured `briefRequest` schema.
- Added `ReferenceImageBriefSchemaResponse`.
- Updated the runtime policy bundle to tell panel/Companion routes to produce a structured brief before calling the modeling agent.
- Updated `Project_Guides/MCP_Rhino Architecture.md` to define `Tools/Modeling` as thin externally callable wrappers for goal-level modeling agents.
- Added smoke coverage for wrapper exposure, safety metadata, image-only gap behavior, schema availability, and structured delegation.

## Deviations From Plan

- No separate `IReferenceImageBriefProvider` was implemented. The execution chose the smaller Stage A boundary: schema exposure plus explicit `IMAGE_BRIEF_REQUIRED` behavior. A provider registry remains a future extension.
- No Rhino live smoke command was added. The MCP wrapper is exposed through the shared tool surface and validated by the MCP surface governance smoke.

## Issues Found And Fixed During Construction

- The agent existed in DI and developer smokes but not on the MCP surface. The wrapper makes it visible anywhere `.AddRhinoTools()` is registered.
- Raw image-only requests previously failed with a generic structured-observations validation error. The wrapper now returns a structured `IMAGE_BRIEF_REQUIRED` capability gap for image-only requests.

## Test Record

Debug build:

```powershell
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug -p:OutputPath="c:\01_Projects\MCP_Rhino\.validation\exec-debug\"
```

Result:

- exit code: 0
- 0 warnings, 0 errors

Release build:

```powershell
dotnet build .\src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release -p:OutputPath="c:\01_Projects\MCP_Rhino\.validation\exec-release\"
```

Result:

- exit code: 0
- 0 warnings, 0 errors

Agent access and briefing smoke:

```powershell
dotnet .\.validation\exec-debug\MCP_Rhino.Server.dll reference-image-agent-access-and-briefing-smoke-test
dotnet .\.validation\exec-release\MCP_Rhino.Server.dll reference-image-agent-access-and-briefing-smoke-test
```

Result for both Debug and Release:

- `[OK] reference-image agent wrapper is MCP-exposed and delegates structured requests.`
- `[OK] raw image-only requests return IMAGE_BRIEF_REQUIRED and the brief schema is discoverable.`

MCP surface governance smoke:

```powershell
dotnet .\.validation\exec-debug\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
dotnet .\.validation\exec-release\MCP_Rhino.Server.dll mcp-surface-structure-governance-smoke-test
```

Result:

- exit code: 0 in both configurations
- discovered 142 MCP tools
- `Modeling :: RunReferenceImageObjectModelingAgentTool.RunReferenceImageObjectModelingAgent` was present
- `Reference :: GetReferenceImageBriefSchemaTool.GetReferenceImageBriefSchema` was present
- all MCP tools had descriptions and explicit safety metadata

## Acceptance Criteria Alignment

- `RunReferenceImageObjectModelingAgent` appears in MCP inventory: satisfied.
- Debug and Release expose the same wrapper through assembly scanning: satisfied.
- Tool has explicit safety annotations and description: satisfied.
- Wrapper delegates structured requests to the agent: satisfied by CLI fallback smoke.
- Raw image-only request returns `IMAGE_BRIEF_REQUIRED`: satisfied.
- Brief schema discoverable through read-only reference tool: satisfied.
- Runtime guidance explains image-to-brief boundary: satisfied.
- Material/shadow cues remain non-geometry: satisfied by material texture smoke and policy code.

## Rollback Validation

Rollback removes:

- `src/MCP_Rhino.Server/Tools/Modeling/RunReferenceImageObjectModelingAgentTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/GetReferenceImageBriefSchemaTool.cs`
- `src/MCP_Rhino.Server/Contracts/Responses/ReferenceImageBriefSchemaResponse.cs`
- runtime policy additions
- `Tools/Modeling` guide entry
- `Project_Test/260509_TEST_reference-image-agent-access-and-briefing/`
- this EXET file

The internal `ReferenceImageObjectModelingAgent` and existing developer smoke remain intact.

## Current Residual Items

- Implement an optional `IReferenceImageBriefProvider` when there is an approved image-understanding provider.
- Add a live Rhino smoke command if direct in-Rhino validation of the wrapper becomes necessary.
- Add companion UI affordances for reviewing/editing generated briefs before mutation.

## Conclusion

The reference-image object modeling agent is now available through the standard MCP tool surface in all host modes that register `.AddRhinoTools()`. Raw image-only usage is explicit and safe: callers must supply a structured brief or use a future image-brief provider.
