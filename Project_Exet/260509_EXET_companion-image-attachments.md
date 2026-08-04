# Companion Image Attachments EXET

## Corresponding Plan

- Plan: `Project_Plan/260509_PLAN_companion-image-attachments.md`
- Execution date: 2026-05-09

## Associated Artifacts

- Test folder: `Project_Test/260509_TEST_companion-image-attachments/`
- Smoke registration: `Project_Test/260509_TEST_companion-image-attachments/DeveloperCommandHandler.CompanionImageAttachmentsSmokeTest.cs`
- Sample IPC payload: `Project_Test/260509_TEST_companion-image-attachments/sample-image-message.json`
- Commit / PR: not created in this execution.

## Execution Result / Actual Landing Scope

Implemented a bounded image attachment path for the standalone Companion:

- Browser UI now has a real hidden image file picker behind the paperclip button.
- Drag/drop and paperclip selection share the same image validation and `FileReader` path.
- UI attachments now keep `mediaType`, `sizeBytes`, and `base64Data`, not only chip display metadata.
- WebView2 `send` IPC now sends `{ text, attachments }`.
- The UI waits for host acceptance before clearing text and chips, preventing rejected sends from losing attachments.
- `MainWindow` parses a typed `CompanionUserMessage` and validates attachment count, MIME type, declared size, and base64 payload.
- `IAgentSession` is now message-based instead of text-only.
- Claude Code stream-json input now emits text and image content blocks.
- Codex `exec` writes selected images into a pipe-scoped Companion temp folder and passes them through repeated `-i/--image` arguments.
- Runtime policy now explains attached image context and reference-image `briefRequest` routing.
- The Rhino reference-image modeling agent remains structured-brief-driven and still rejects raw image-only requests.

## Deviations From Plan

- The plan allowed either base64 IPC or temp-file IPC. The implemented route uses base64 from browser to host, then temp files only for Codex because `codex exec --image` requires file paths.
- Claude Code does not expose a local `--image` flag in the checked CLI help. The implementation sends Anthropic-style image content blocks through the existing stream-json input. If an installed Claude Code version rejects that input shape, the backend will report a CLI/stream diagnostic rather than silently pretending to inspect the image.
- No WebView2 UI automation was added. The test artifact is a static/contract smoke because the critical regression was text-only IPC and session contracts.

## Issues Found And Fixed During Execution

- Attachment-only sends previously cleared chips and entered busy state even though the host ignored blank text. The new host validation and accepted-message echo path prevents silent drops.
- The paperclip button was previously a no-op. It now opens a bounded image picker.
- The previous drag overlay advertised non-image files. It now states PNG, JPEG, and WebP images only for this capability slice.

## Test Record

Commands run:

```powershell
node --check src\MCP_Rhino.Companion\wwwroot\app.js
```

- Exit code: 0

```powershell
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Debug --nologo
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.

```powershell
dotnet build .\MCP_Rhino.sln -c Debug
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.
- Refreshed Debug plugin: `src\MCP_Rhino.Server\bin\Debug\net8.0\MCP_Rhino.Server.rhp`
- Observed output size/time: 1,911,808 bytes, 2026-05-09 16:25:27 local.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Debug --no-build -- companion-image-attachments-smoke-test
```

- Exit code: 0
- Key output:
  - `[OK] Companion UI sends real image attachment payloads over WebView2 IPC.`
  - `[OK] IAgentSession is no longer text-only.`
  - `[OK] Claude stream-json writer emits image content blocks.`
  - `[OK] Codex backend passes selected images through codex exec --image files.`
  - `[OK] Rhino reference-image modeling still requires structured brief input.`

```powershell
dotnet build .\MCP_Rhino.sln -c Release
```

- Exit code: 0
- Result: build succeeded, 0 warnings, 0 errors.
- Refreshed Release plugin: `src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp`
- Observed output size/time: 1,799,680 bytes, 2026-05-09 16:25:37 local.

```powershell
dotnet run --project src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --no-build -- companion-image-attachments-smoke-test
```

- Exit code: 0
- Result: companion-image-attachments smoke passed.

## Acceptance Criteria Alignment

- Drag/drop stores real image content: met.
- Paperclip selection works through a real file input: met.
- WebView2 IPC includes text plus attachment payloads: met.
- Host validates attachment count, MIME type, byte limits, and base64: met.
- `IAgentSession` accepts structured messages: met.
- Claude/Codex adapters handle images through backend-specific routes: met for code path; real Claude CLI support remains version-dependent.
- Attachment-only messages no longer get silently dropped by the host text check: met.
- Rejected attachment sends do not clear chips or leave the UI permanently busy: met by host validation rejection path and input reset.
- Runtime guidance explains image inspection and reference-image brief routing: met.
- Raw image-only Rhino modeling requests still return `IMAGE_BRIEF_REQUIRED`: met.
- Debug and Release builds pass: met.
- Smoke/static contract checks pass in Debug and Release: met.

## Rollback Verification

Rollback is limited to this capability slice:

- remove the Companion attachment DTO/validator/temp-file files
- restore `IAgentSession.SendUserMessageAsync(string text, ...)`
- restore text-only `StreamJsonWriter`
- remove UI file input and `FileReader` attachment payload logic
- remove the runtime policy image-attachment section
- remove `RegisterCompanionImageAttachmentsHandlers()` and the `Project_Test/260509_TEST_companion-image-attachments/` folder
- remove this EXET and the matching PLAN if abandoning the capability

The server-side reference-image modeling agent, brief schema, and existing Rhino tools are not rollback targets.

## Current Remaining Items

- Real end-to-end inspection should be checked manually in the Companion with both backends. Codex has an explicit `codex exec --image` route in local help; Claude image blocks depend on the installed Claude Code stream-json input accepting image content.
- This execution supports image attachments only. Non-image files remain intentionally rejected.
- Chat history stores transcript HTML and attachment chips, not persisted image bytes.

## Conclusion

The Companion no longer treats image attachments as UI-only chips. User-selected PNG/JPEG/WebP images now flow through bounded UI validation, WebView2 IPC, host validation, and backend-specific message adapters while preserving the local-file sandbox and the structured reference-image brief contract.
