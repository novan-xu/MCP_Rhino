# Reference Image Agent Access And Briefing Test

## Purpose

Validate that `ReferenceImageObjectModelingAgent` is exposed through a thin MCP tool wrapper and that raw image-only requests return an explicit `IMAGE_BRIEF_REQUIRED` gap.

## Checks

- `RunReferenceImageObjectModelingAgentTool` is an MCP tool type with explicit safety metadata.
- The tool description states the structured `briefRequest` boundary.
- `GetReferenceImageBriefSchema` returns brief guidance and an object-type example.
- Raw image-only wrapper calls return `IMAGE_BRIEF_REQUIRED`.
- Structured wrapper calls delegate to the agent and, in CLI fallback mode, stop at `LIVE_RHINO_REQUIRED`.

## Validation Commands

```powershell
dotnet .\.validation\exec-debug\MCP_Rhino.Server.dll reference-image-agent-access-and-briefing-smoke-test
dotnet .\.validation\exec-release\MCP_Rhino.Server.dll reference-image-agent-access-and-briefing-smoke-test
```
