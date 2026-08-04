# Companion Image Attachments Smoke

Validates the Companion image attachment ingress path without launching Rhino or WebView2.

Run:

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- companion-image-attachments-smoke-test
```

The smoke checks that:

- the UI has a real image file picker and sends attachment payloads
- the host parses `CompanionUserMessage`
- session adapters are no longer text-only
- attachment validation keeps count, size, MIME, and base64 limits
- Claude keeps broad local file tools disabled
- Codex uses `codex exec --image`
- runtime guidance routes reference-image modeling through structured briefs
