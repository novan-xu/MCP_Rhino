# Reference Image Visual QA Test

## Purpose

Validate the first reference-image visual QA slice:

- viewport capture returns non-empty image bytes
- QA capture returns target object summaries and checkpoint checklist text
- CLI fallback remains live-only

## Commands

CLI fallback:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- reference-image-visual-qa-smoke-test
```

Live Rhino smoke:

```text
_McpReferenceImageVisualQaSmoke
```

The live smoke requires a saved active Rhino document and a loaded MCP_Rhino plugin.
