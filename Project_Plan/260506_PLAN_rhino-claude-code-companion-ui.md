# 260506_PLAN_rhino-claude-code-companion-ui

## Background

The current Claude Code chat surface is an Eto panel hosted inside Rhino. It proves the core
transport path works, but the user experience is too limited:

- the transcript has little visual hierarchy
- user / assistant / system / tool messages are not clearly differentiated
- Claude Code thinking blocks are not surfaced
- tool calls are rendered as plain lines instead of useful cards
- the Rhino panel environment makes modern web-style UI work slower and more constrained

The important product requirement is not "must be a Rhino panel". The important requirement is:

- each chat UI instance is created from a specific saved Rhino document
- each instance is bound to that document's `RhinoDoc.RuntimeSerialNumber`
- tool calls from that chat cannot accidentally operate on another open Rhino document
- multiple documents can run multiple independent chat sessions in parallel

The existing panel-bound MCP architecture already has the correct backend primitive:

- per-document named pipe: `mcp_rhino_<RuntimeSerialNumber>`
- `BoundLiveRhinoDocumentAccessor`
- `MCP_Rhino.Bridge.exe --pipe <name>`
- Claude Code stream-json process wrapper

This plan replaces the Rhino-hosted Eto chat UI with a modern external companion window while
preserving the same per-document binding model.

## Goals

1. Add a modern companion chat UI launched from Rhino by `_Mcpchat`.
2. Preserve strict per-document binding:
   - one saved Rhino document -> one bound MCP pipe -> one Claude Code process -> one companion window
   - no `RhinoDoc.ActiveDoc` following inside companion sessions
3. Render a richer Claude Code conversation:
   - user / assistant / system / diagnostics color differentiation
   - thinking blocks as collapsible sections
   - tool call cards with running / success / failed states
   - markdown-like assistant text rendering
   - status bar with document name, MCP connection state, model, cost, and pipe name
4. Keep Rhino plugin responsibilities small:
   - own document lifecycle
   - start / stop bound MCP pipes
   - launch / focus companion windows
   - clean up sessions when documents close
5. Keep MCP tools, skills, agents, and live document access unchanged except where prompt/context
   injection is needed for better tool use.

Non-goals for v1:

- embedding the official VS Code / JetBrains Claude Code extension
- implementing a general IDE
- editing source code or showing code diffs
- persistent chat history
- multi-user remote collaboration
- Mac support
- replacing the global `\\.\pipe\mcp_rhino` developer debug path

## Architecture Ownership

### New sibling project: `src/MCP_Rhino.Companion/`

Add a Windows desktop companion app:

- project type: `WinExe`
- target framework: `net8.0-windows`
- UI host: WPF + WebView2
- responsibility:
  - own the modern chat window
  - spawn Claude Code
  - generate / read per-doc MCP config passed from Rhino
  - parse Claude Code stream-json
  - render transcript, thinking blocks, and tool cards
  - send user messages to Claude Code stdin

This is a sibling executable like `MCP_Rhino.Bridge`, not a nested Infrastructure folder, because
it is an independently launched UI process and should not depend on RhinoCommon.

### Existing Rhino plugin: `src/MCP_Rhino.Server/Infrastructure/Plugin/`

Extend the plugin host:

- `_Mcpchat` launches or focuses the companion app for the active saved document
- the plugin keeps starting/stopping per-document bound MCP pipes
- the plugin passes the following launch arguments:
  - `--document-path <doc.Path>`
  - `--runtime-serial <doc.RuntimeSerialNumber>`
  - `--pipe <mcp_rhino_serial>`
  - `--bridge <MCP_Rhino.Bridge.exe path>`
  - optional `--model <model-id>`
- on document close, plugin stops the bound pipe and signals/kills the companion process

The existing Eto panel can remain as a temporary fallback or be disabled after the companion is
stable. The v1 plan should not remove working backend pieces.

### Existing bridge: `src/MCP_Rhino.Bridge/`

No behavior change. The companion uses:

```powershell
MCP_Rhino.Bridge.exe --pipe mcp_rhino_<RuntimeSerialNumber>
```

### Existing MCP server / tools

No tool behavior changes in v1. The companion injects a session system prompt telling Claude Code:

- this session is bound to one Rhino document
- always use the bound document path for `filePath`
- do not ask the user for the path
- do not attempt to operate on other open Rhino documents

The bound server remains the actual enforcement mechanism.

## Key Design

### 1. Launch model

`_Mcpchat` should become the single user entry point.

When invoked:

1. validate active `RhinoDoc` exists
2. validate `doc.Path` is non-empty
3. get `runtimeSerial = doc.RuntimeSerialNumber`
4. start or reuse `mcp_rhino_<runtimeSerial>`
5. locate `MCP_Rhino.Companion.exe`
6. launch companion if missing; focus existing companion if already running

Companion discovery order:

1. plugin directory: `MCP_Rhino.Companion.exe`
2. repo build output: `src/MCP_Rhino.Companion/bin/Release/net8.0-windows/MCP_Rhino.Companion.exe`
3. repo debug output
4. `%LOCALAPPDATA%/McNeel/Rhinoceros/8.0/Plug-ins/MCP_Rhino/Companion/MCP_Rhino.Companion.exe`
5. `PATH`

This mirrors the current Bridge discovery pattern.

### 2. Process ownership

The Rhino plugin owns companion process lifetime by document serial:

```text
Dictionary<uint, CompanionSessionHandle>
```

Each handle tracks:

- document runtime serial
- document path
- pipe name
- process id
- launch time
- companion IPC channel name, if used

On `_Mcpchat` for an already-open session:

- bring existing companion to front
- update document path if `_SaveAs` changed the path

On `RhinoDoc.CloseDocument`:

- ask companion to stop Claude Code and close
- stop bound MCP pipe
- kill companion after timeout if still alive

### 3. Companion UI technology

Use WPF + WebView2:

- WPF handles native process args, window lifecycle, and local process management
- WebView2 hosts the modern chat UI
- UI assets are local files under `src/MCP_Rhino.Companion/wwwroot/`

This avoids introducing an Electron/Tauri toolchain in v1 while still enabling a modern UI.

Initial UI files:

```text
src/MCP_Rhino.Companion/
  MCP_Rhino.Companion.csproj
  App.xaml
  App.xaml.cs
  MainWindow.xaml
  MainWindow.xaml.cs
  CompanionOptions.cs
  ClaudeCodeSession.cs
  ClaudeCodeStreamEvent.cs
  McpConfigBuilder.cs
  wwwroot/
    index.html
    styles.css
    app.js
```

For v1, plain HTML/CSS/JS is enough. React/Vite can be considered after the basic lifecycle is
validated.

### 4. UI layout

The companion should feel like a focused operational chat tool, not a marketing page.

Layout:

- top compact status bar:
  - Rhino file name
  - connection status
  - selected model
  - total cost
  - stop button
- main transcript:
  - virtualized or append-only message list
  - user messages aligned right or visually grouped
  - assistant messages aligned left
  - system messages muted
  - diagnostics in warning color
  - thinking blocks collapsible
  - tool cards with icon, tool name, status, input summary, result summary
- bottom composer:
  - multiline input
  - send button
  - interrupt/stop button

Color system:

- neutral background
- assistant text: primary foreground
- user bubble: restrained accent
- system: muted gray
- diagnostics/errors: amber/red
- tool running: blue
- tool success: green
- tool failed: red

Avoid one-hue palettes and avoid decorative gradients/orbs.

### 5. Claude Code stream handling

The companion should reuse the existing stream-json approach:

```powershell
claude --print `
  --output-format=stream-json `
  --input-format=stream-json `
  --verbose `
  --mcp-config <temp-mcp-config> `
  --strict-mcp-config `
  --permission-mode bypassPermissions `
  --disallowedTools "Bash,Edit,Read,Write,Grep,Glob,WebFetch,WebSearch,TodoWrite,Task,NotebookEdit" `
  --append-system-prompt <bound-doc-prompt>
```

The stream parser must preserve:

- `init` MCP server status
- assistant text deltas / messages
- thinking blocks, if present in stream content
- tool use events
- tool result events
- final `result` metadata such as total cost and duration
- stderr diagnostics

Unknown stream event fields should be retained in diagnostics rather than crashing.

### 6. Bound document prompt

The companion owns prompt construction, not the Rhino plugin UI.

Prompt content:

```text
You are running in MCP_Rhino Companion.
This session is bound to exactly one saved Rhino document.
Bound document path: <doc.Path>
Bound runtime serial: <serial>
Bound MCP pipe: <pipe>
Always pass this bound document path when Rhino MCP tools require filePath.
Do not ask the user for the .3dm path.
Do not operate on any other open Rhino document from this session.
The panel-bound server routes by runtime serial, not RhinoDoc.ActiveDoc.
```

This is UX guidance. The hard enforcement remains in `BoundLiveRhinoDocumentAccessor`.

### 7. Inter-process communication

For v1, launch arguments are enough for startup context.

Optional v1.1 local IPC:

- named pipe from Rhino plugin to companion for:
  - focus existing window
  - document path update after `_SaveAs`
  - graceful shutdown on document close

If IPC is not implemented in v1, use process kill on close and relaunch on `_SaveAs` only if
needed.

### 8. Existing Eto panel transition

Keep the current Eto panel code during v1 implementation, but change `_Mcpchat` behavior to prefer
the companion.

Fallback order:

1. launch companion if found
2. if companion is not found, show a clear Rhino command-line message with expected paths
3. optionally fall back to Eto panel only behind a developer command, not as the primary UX

This avoids deleting the only working UI before the companion is validated.

## Involved Files

Planned new files:

```text
src/MCP_Rhino.Companion/MCP_Rhino.Companion.csproj
src/MCP_Rhino.Companion/App.xaml
src/MCP_Rhino.Companion/App.xaml.cs
src/MCP_Rhino.Companion/MainWindow.xaml
src/MCP_Rhino.Companion/MainWindow.xaml.cs
src/MCP_Rhino.Companion/CompanionOptions.cs
src/MCP_Rhino.Companion/ClaudeCodeSession.cs
src/MCP_Rhino.Companion/ClaudeCodeStreamEvent.cs
src/MCP_Rhino.Companion/McpConfigBuilder.cs
src/MCP_Rhino.Companion/wwwroot/index.html
src/MCP_Rhino.Companion/wwwroot/styles.css
src/MCP_Rhino.Companion/wwwroot/app.js
Project_Test/260506_TEST_rhino-claude-code-companion-ui/README.md
Project_Test/260506_TEST_rhino-claude-code-companion-ui/DeveloperCommandHandler.RhinoClaudeCodeCompanionUiSmokeTest.cs
Project_Exet/260506_EXET_rhino-claude-code-companion-ui.md
```

Planned modified files:

```text
MCP_Rhino.sln
src/MCP_Rhino.Server/MCP_Rhino.Server.csproj
src/MCP_Rhino.Server/Infrastructure/Plugin/MCP_Rhino.RhinoPlugin.cs
src/MCP_Rhino.Server/Infrastructure/Plugin/McpChatCommand.cs
src/MCP_Rhino.Server/Infrastructure/Plugin/Panel/RhinoChatPanelHost.cs
src/MCP_Rhino.Server/Infrastructure/ClaudeCode/McpConfigBuilder.cs
Project_Test/260505_TEST_rhino-claude-code-panel/README.md
```

The exact file list may shrink during execution if helper classes can be shared without creating
cross-project coupling.

## Usage

Build:

```powershell
dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo
dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo
dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo
```

Rhino:

```text
_LoadPlugin
```

Load:

```text
src\MCP_Rhino.Server\bin\Release\net8.0\MCP_Rhino.Server.rhp
```

Open or save a `.3dm`, then:

```text
_Mcpchat
```

Expected behavior:

- modern companion window opens
- title/status shows the bound document
- MCP status shows `rhino:connected`
- asking "list layers in this file" calls Rhino tools without asking for a path
- switching active Rhino document does not redirect the companion session

## Acceptance Criteria

### Build

- `dotnet build src\MCP_Rhino.Bridge\MCP_Rhino.Bridge.csproj -c Release --nologo` exits 0
- `dotnet build src\MCP_Rhino.Companion\MCP_Rhino.Companion.csproj -c Release --nologo` exits 0
- `dotnet build src\MCP_Rhino.Server\MCP_Rhino.Server.csproj -c Release --nologo` exits 0
- `.rhp`, Bridge exe, and Companion exe are present in expected output paths

### Companion launch

- `_Mcpchat` on an unsaved document returns a clear "saved document required" message
- `_Mcpchat` on a saved document starts or focuses one companion window for that document
- calling `_Mcpchat` twice for the same document does not start duplicate bound MCP pipes
- two saved Rhino documents can each open an independent companion window

### Binding

- each companion window connects to its own `mcp_rhino_<RuntimeSerialNumber>` pipe
- asking the companion to inspect "this file" does not ask for a path
- switching Rhino's active document does not change which document the companion manipulates
- closing a document stops its bound pipe and closes/stops its companion process

### UI

- user, assistant, system, diagnostics, and tool messages are visually distinct
- tool calls show running, success, and failed states
- thinking blocks are captured and displayed when present in Claude Code stream-json
- stderr diagnostics are visible but visually separated from assistant content
- long transcripts remain usable without severe layout jank
- text wraps correctly at desktop window widths from 900px to 1800px

### Safety

- built-in Claude Code filesystem tools remain disallowed in the companion session
- Rhino MCP calls remain routed through the bound accessor
- companion does not provide a way to target arbitrary other Rhino documents
- mutation tools still rely on Rhino Undo behavior

### Smoke

Add smoke slug:

```text
rhino-claude-code-companion-ui-smoke-test
```

Minimum smoke coverage:

- CLI mode:
  - companion argument parsing accepts required args
  - MCP config generation is parseable
  - Claude Code availability check reports status
  - Bridge discovery reports expected path or clear missing result
- Rhino live mode:
  - active saved document produces a companion launch spec
  - bound pipe starts and stops
  - wrong `filePath` still resolves to bound document through `BoundLiveRhinoDocumentAccessor`

Manual smoke:

- open two saved `.3dm` files
- run `_Mcpchat` in each
- ask both companions to list layers
- confirm results differ correctly when files differ
- switch active Rhino document and repeat
- close one document and confirm only its companion exits

## Risks And Rollback

### Risk: companion packaging adds deployment complexity

Mitigation:

- use a .NET sibling project first
- avoid Node/Electron in v1
- use simple discovery paths similar to Bridge

Rollback:

- leave existing Eto panel available behind a fallback/developer command
- `_Mcpchat` can be reverted to current panel behavior

### Risk: Claude Code stream-json event shape changes

Mitigation:

- parse known fields defensively
- preserve unknown fields in diagnostics
- do not crash UI on unrecognized event types

Rollback:

- show raw stream event JSON in a diagnostics pane

### Risk: process cleanup fails

Mitigation:

- plugin tracks companion process by runtime serial
- companion tracks Claude Code child process
- both sides use graceful shutdown first, then kill after timeout

Rollback:

- command-line cleanup instructions and task-manager fallback during development

### Risk: WebView2 runtime missing

Mitigation:

- detect WebView2 startup failure and print a clear Rhino command-line message
- document install requirement in TEST README

Rollback:

- retain Eto fallback during v1

### Risk: prompt guidance is not enough to prevent path questions

Mitigation:

- companion passes explicit bound document prompt
- add optional first hidden user/system message if Claude Code ignores append prompt
- longer-term: add panel-bound wrapper tools with no `filePath` parameter if needed

Rollback:

- keep current `filePath` schema and rely on bound accessor for enforcement

## Future Extensions

- persist transcript as a sidecar JSONL next to `.3dm`
- add model switcher with explicit "new conversation" behavior
- add quick actions for common Rhino workflows
- render geometry/tool previews in the companion
- add a compact activity timeline per tool call
- add screenshot/viewport capture context
- add WebSocket IPC instead of launch-only args
- support a packaged installer that copies Server, Bridge, and Companion to Rhino plugin folders
- support Mac with a separate companion host technology

## Execution Notes

Implementation should be done as a separate Execute step after this plan is accepted.

Do not remove current panel-bound server infrastructure during the first pass. The first pass should
introduce the companion as the preferred UI while preserving backend behavior and keeping a fallback
path for debugging.
