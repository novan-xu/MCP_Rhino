# Companion Image Attachments Plan

## Background

The standalone `MCP_Rhino.Companion` UI advertises drag-and-drop attachments and renders dropped files as chips, but the current implementation does not send file content to the LLM backend. The frontend stores only display metadata and posts only `{ text }` through WebView2 IPC. The WPF host and `IAgentSession` contract are also text-only, so both Claude Code and Codex receive no image bytes, no image path, and no attachment metadata.

This explains the runtime response:

> I can't directly inspect the images from this panel - the Read tool isn't available here, and the Rhino reference-image agent needs a structured brief.

That response is accurate for the current system. The Companion intentionally disables broad local file tools such as `Read`, and the Rhino-side `ReferenceImageObjectModelingAgent` is structured-brief-driven. It does not inspect raw pixels. Existing reference-image server work already exposes a modeling agent and a brief schema; the missing capability is the Companion-side image ingress and vision-to-brief bridge.

## Goal

Add a safe, explicit Companion image attachment path so a user can drop or pick an image, ask the Companion to inspect it, and have the selected backend either:

- pass the image to a vision-capable LLM message path for normal chat inspection, or
- convert the image into a structured `BuildReferenceImageModelBriefRequest` before calling Rhino reference-image modeling tools.

The capability should:

- preserve the existing bound-document sandbox
- avoid globally re-enabling local file tools such as `Read`
- support drag-and-drop and the paperclip button
- make unsupported backend/image combinations fail with clear diagnostics
- keep Rhino server raw-pixel inference out of scope
- keep the existing reference-image modeling agent brief-driven

Non-goals:

- enabling arbitrary workspace or repository file access from Companion
- allowing the LLM to scan directories or read unrelated local files
- moving image pixel inference into `MCP_Rhino.Server`
- replacing the structured reference-image brief contract
- adding external cloud storage or a general file manager

## Architecture Ownership

- `src/MCP_Rhino.Companion/wwwroot/`
  - Owns browser-side attachment selection, drag/drop capture, attachment chip state, size/type validation, and send payload creation.
  - Must not pretend an attachment was sent if host transfer fails.

- `src/MCP_Rhino.Companion/MainWindow.xaml.cs`
  - Owns WebView2 IPC parsing and conversion from UI JSON payloads to typed Companion request objects.
  - Should validate attachment payload size/count before calling the session backend.

- `src/MCP_Rhino.Companion/`
  - Add Companion-local DTOs such as `CompanionAttachment`, `CompanionUserMessage`, and validation helpers.
  - Extend `IAgentSession` from text-only sends to message sends with attachments.
  - Implement backend-specific attachment handling in `ClaudeCodeSession` and `CodexCliSession`.

- `src/MCP_Rhino.Server/Tools/Modeling` and `src/MCP_Rhino.Server/Tools/Reference`
  - Existing structured reference-image tools remain the server-side modeling route.
  - Server raw image inspection remains out of scope unless a later plan adds a dedicated provider contract.

- `src/MCP_Rhino.Server/Prompts/Runtime/`
  - Update runtime guidance only as needed so Companion-launched LLMs understand how to use attachments and when to produce a structured reference-image brief.

- `Project_Test/260509_TEST_companion-image-attachments/`
  - Owns smoke tests and static contract checks for UI IPC, session DTOs, backend adapter behavior, and prompt guidance.

## Key Design

### 1. Represent Attachments As First-Class Message Data

Replace the text-only send path with a typed message contract:

- `CompanionUserMessage`
  - `Text`
  - `Attachments`

- `CompanionAttachment`
  - `Id`
  - `Name`
  - `MediaType`
  - `Kind` (`Image` or `File`)
  - `SizeBytes`
  - `Base64Data` or a tightly scoped temp-file path

Initial scope should support common image types:

- PNG
- JPEG
- WebP if supported by the selected backend

Other files should be rejected or carried only as metadata until there is a separate non-image file plan.

### 2. Keep Attachment Transfer Explicit And Bounded

The browser should read dropped/picked images with `FileReader` and send bounded base64 payloads through WebView2 IPC.

Suggested initial limits:

- maximum 4 attachments per message
- maximum 10 MB per image before base64
- maximum 20 MB total attachment bytes per message
- image MIME allowlist only

The host should independently revalidate count, byte length, MIME type, and base64 shape. Frontend validation is UX only; host validation is the trust boundary.

### 3. Implement The Paperclip Button

Add a hidden `<input type="file" multiple accept="image/png,image/jpeg,image/webp">` and wire the existing paperclip button to it.

Drag/drop and file-picker selection should share the same code path, so chips, validation, removal, and send behavior stay consistent.

### 4. Fix Attachment-Only Send Semantics

The current UI allows an attachment-only send, clears the chip list, and sets the UI busy even though the host ignores blank text. The new behavior should be:

- allow attachment-only sends if at least one supported image is attached
- include attachments in the IPC payload
- echo the user message with chips only after the host accepts the message or after the frontend has a reliable local accepted state
- do not clear attachments on failed host validation
- never enter busy state for a rejected send

### 5. Backend Adapter Strategy

Each backend must declare whether it supports image input through the current CLI mode.

Claude Code route:

- Extend `StreamJsonWriter` to write multimodal content blocks if the installed CLI supports image blocks in stream-json input.
- If the current Claude Code CLI route does not support image blocks, return a diagnostic such as `Claude Code image attachments are not supported by this installed CLI/input mode.`

Codex route:

- Extend `CodexCliSession` only if the current `codex exec` stdin path supports image input or an attachment argument route.
- If the current Codex CLI route is text-only, return a diagnostic rather than silently converting images to names or paths.

The implementation must probe or encapsulate backend capability rather than assuming both backends support the same payload shape.

### 6. Vision-To-Brief Route For Rhino Modeling

Normal image questions can be answered directly by a vision-capable backend if the backend accepts image input.

For modeling requests such as "build this image in Rhino":

1. the LLM inspects the attached image
2. the LLM produces a structured `BuildReferenceImageModelBriefRequest`
3. the LLM calls `RunReferenceImageObjectModelingAgent`

The Rhino server should continue to reject raw image-only modeling requests with `IMAGE_BRIEF_REQUIRED`. Companion attachment support should not change that server contract; it gives the LLM the missing visual context needed to fill the brief.

### 7. Preserve The Sandbox

Do not solve this by removing `Read` from `DisallowedTools`.

The safe approach is a narrow attachment channel:

- only user-selected files are available
- only for the active message
- no directory traversal
- no arbitrary local file reads
- no access to the bound `.3dm` file through local file tools

If temp files are used instead of base64, create them under the existing pipe-scoped Companion workspace and delete them after the turn or session.

### 8. Runtime Prompt Guidance

Update the Companion runtime prompt/policy with concise routing guidance:

- attached images are message context, not Rhino document truth
- for visual inspection questions, inspect the attached image directly if backend vision is available
- for reference-image modeling, convert visible observations into `briefRequest` before calling `RunReferenceImageObjectModelingAgent`
- if no attached image content is available, report the capability gap instead of pretending to inspect it

Avoid adding repository construction rules to the runtime prompt.

## Involved Files

Likely production files:

- `src/MCP_Rhino.Companion/wwwroot/index.html`
- `src/MCP_Rhino.Companion/wwwroot/app.js`
- `src/MCP_Rhino.Companion/wwwroot/styles.css`
- `src/MCP_Rhino.Companion/MainWindow.xaml.cs`
- `src/MCP_Rhino.Companion/IAgentSession.cs`
- `src/MCP_Rhino.Companion/ClaudeCodeSession.cs`
- `src/MCP_Rhino.Companion/CodexCliSession.cs`
- `src/MCP_Rhino.Companion/StreamJsonWriter.cs`
- `src/MCP_Rhino.Companion/CompanionUiEvent.cs`
- `src/MCP_Rhino.Companion/CompanionUserMessage.cs`
- `src/MCP_Rhino.Companion/CompanionAttachment.cs`
- `src/MCP_Rhino.Companion/CompanionAttachmentValidator.cs`
- `src/MCP_Rhino.Companion/RuntimePolicyPrompt.cs`
- `src/MCP_Rhino.Server/Prompts/Runtime/McpRhinoRuntimePolicyBundle.md`

Possible test/support files:

- `Project_Test/260509_TEST_companion-image-attachments/DeveloperCommandHandler.CompanionImageAttachmentsSmokeTest.cs`
- `Project_Test/260509_TEST_companion-image-attachments/README.md`
- `Project_Test/260509_TEST_companion-image-attachments/sample-image-message.json`

Likely existing files to reference but not redesign:

- `src/MCP_Rhino.Server/Tools/Modeling/RunReferenceImageObjectModelingAgentTool.cs`
- `src/MCP_Rhino.Server/Tools/Reference/GetReferenceImageBriefSchemaTool.cs`
- `src/MCP_Rhino.Server/Contracts/Requests/ReferenceImageObjectModelingSkillRequests.cs`

## Usage

Image inspection:

1. User drops or picks an image in Companion.
2. Companion validates and sends the image with the text prompt.
3. Backend receives a multimodal message.
4. LLM answers based on the image content.

Reference-image modeling:

1. User drops or picks an image and asks to model the object.
2. Backend inspects the image.
3. LLM creates a structured `briefRequest`.
4. LLM calls `RunReferenceImageObjectModelingAgent`.
5. Rhino tools create geometry in the bound live document.

Unsupported route:

1. User attaches an image while the selected backend cannot receive images.
2. Companion keeps the UI stable and reports a diagnostic.
3. No fake image inspection, no chip loss, and no stuck busy state.

## Acceptance Criteria

- Drag/drop stores real image content, not just chip metadata.
- Paperclip selection works and uses the same validation path as drag/drop.
- WebView2 IPC includes text plus attachment payloads.
- `MainWindow` validates attachment count, MIME type, and byte limits before sending.
- `IAgentSession` accepts a structured message with attachments.
- Claude and Codex adapters either send images through a verified backend-supported route or return a clear unsupported diagnostic.
- Attachment-only messages do not get dropped silently.
- Rejected attachment sends do not clear chips or leave the UI busy.
- User transcript chips represent attachments that were actually accepted for send.
- Companion runtime guidance explains image inspection and reference-image brief routing.
- Raw image-only calls to the Rhino modeling agent still return `IMAGE_BRIEF_REQUIRED`.
- Existing bound-document and local-file sandbox rules remain intact.
- Debug solution build passes: `dotnet build .\MCP_Rhino.sln -c Debug`.
- Release solution build passes: `dotnet build .\MCP_Rhino.sln -c Release`.
- The Companion project builds in Release and includes updated `wwwroot` assets.
- Smoke/static checks cover:
  - `post("send")` includes attachments
  - host code no longer ignores attachment-only messages
  - `IAgentSession` is no longer text-only
  - `Read` remains disallowed in Companion Claude launch
  - reference-image modeling still requires structured brief input

## Risks And Rollback

Risk: CLI image input formats may differ or may not be available in the installed backend version.

Mitigation: isolate backend-specific image serialization, add capability checks, and fail with explicit diagnostics when unsupported.

Risk: base64 IPC payloads could become large and slow.

Mitigation: enforce conservative size limits first. If needed later, switch to pipe-scoped temp files with lifecycle cleanup.

Risk: enabling image attachments could accidentally weaken the local file sandbox.

Mitigation: keep `Read` and other broad file tools disallowed. Only pass user-selected image payloads through the explicit attachment channel.

Risk: the LLM may still call the Rhino modeling agent without a structured brief.

Mitigation: keep tool descriptions, runtime prompt guidance, and `IMAGE_BRIEF_REQUIRED` behavior aligned.

Rollback:

- revert Companion UI attachment payload changes
- revert `MainWindow` IPC parsing changes
- restore text-only `IAgentSession`
- remove Companion attachment DTO/validator files
- revert backend adapter multimodal send changes
- revert runtime prompt additions
- remove `Project_Test/260509_TEST_companion-image-attachments/`

The existing reference-image modeling agent and server-side brief schema remain valid and do not need rollback.

## Future Extensions

- multi-image reference sets with roles such as front, side, back, and detail
- an editable generated-brief review panel before Rhino mutation
- persistent per-session attachment thumbnails in chat history
- OCR/text extraction for screenshots or labels when backend vision supports it
- bounded non-image attachments such as `.txt`, `.csv`, or `.gh` under separate file-specific plans
- provider abstraction for external vision models if CLI backends cannot reliably accept image input
