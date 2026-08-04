# Reference Image Object Modeling Agent Smoke

## Purpose

Validate the first orchestrated `ReferenceImageObjectModelingAgent` slice:

- structured image brief handoff
- primitive decomposition
- initial massing execution
- required massing visual QA checkpoint
- simple raised-strip detail execution
- material planning and assignment
- final decision / gap behavior

## Commands

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- reference-image-object-modeling-agent-smoke-test
```

Live Rhino smoke:

```text
_McpReferenceImageObjectModelingAgentSmoke
```

The live command requires a saved active Rhino document and a loaded MCP_Rhino plugin.
