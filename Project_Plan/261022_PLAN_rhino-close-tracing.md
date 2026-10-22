# Rhino Close Tracing Plan

Status: proposed on 2026-10-22. Planning only; not approved for implementation.

## Background

On 2026-10-22 the user's Rhino 8 closed three times during one MCP_Rhino session with
`MET - Wireframe_STN.3dm` open. Each time, Rhino went through an orderly shutdown seconds after
a full save of the model:

| Rhino PID (start) | Model saved | Rhino shutdown evidence |
| --- | --- | --- |
| 39720 (11:23) | 18:05:12 | Route lost by ~18:06; new Rhino started 18:06:47 |
| 37104 (18:07) | 18:22:58 | Rhino settings written 18:25:30 |
| 42456 (18:34) | 18:40:40 | Settings written and route descriptor removed 18:40:45 |

Extra Rhino launches also appeared at 18:06:47 (49740) and 18:24:14 (22892), next to the one
the user had open.

A read-only investigation found:
- No Rhino crash, hang, or Windows Error Reporting events.
- No installer or Restart Manager session at those times. The ScreenConnect MSI ran at 18:05:56,
  failed, and came after the first close.
- No code path in MCP_Rhino, the Router, or WireframeConnector that exits Rhino or closes
  documents.
- No other agent session transcript that closes or kills Rhino.
- The `.NET Runtime` 1000 events at 18:05:12 come from a Router's generic host when its own
  stdio session ended. They are noise, not a cause.

"Save, then orderly exit within seconds" matches a close request answered with "Save changes?
Yes". The trigger itself left no trace, for three reasons:
- the MCP_Rhino plug-in keeps no log on disk;
- Rhino keeps no persistent command history;
- Windows process-exit auditing is off. Turning it on is a system security setting, outside this
  plan.

## Goal

Make the next unexpected close traceable from one local file. For every close request record:
- the time;
- what triggered it: an `Exit`/`Close` command and which plug-in ran it, a title-bar/taskbar/Alt+F4
  system close, a programmatic `WM_CLOSE`, or a Windows session-end/Restart Manager request;
- the unsaved state of every open document;
- what happened next: saves during shutdown, document closes, and final process exit.

Also record each Rhino start: PID, parent process, and command line. That explains the extra
launches.

Out of scope:
- preventing or vetoing a close;
- any MCP tool, Router, or route-contract change;
- telemetry leaving the machine;
- changes to Windows auditing.

## Architecture ownership

This is Rhino-host lifecycle observation plus local file logging, so it belongs to
`Infrastructure/` (Architecture guide §8). It lives only in the RHP host assembly, next to the
plug-in entry:

- `src/MCP_Rhino.Server/Infrastructure/Plugin/Lifecycle/RhinoCloseTraceLog.cs`: the bounded
  JSONL writer.
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Lifecycle/RhinoCloseTracer.cs`: subscribes and
  unsubscribes the RhinoCommon events, keeps the recent-command ring buffer, and writes records.
- `src/MCP_Rhino.Server/Infrastructure/Plugin/Lifecycle/MainWindowCloseObserver.cs`: an
  observe-only Win32 subclass of Rhino's main window.
- `MCP_Rhino.RhinoPlugin.cs`: start the tracer first in `OnLoad`, stop it last in `OnShutdown`.
  Tracer failures are caught and written to the command line. They never fail the plug-in load.
- `MCP_Rhino.Server.csproj`: exclude `Infrastructure\Plugin\Lifecycle\**` from the isolated
  runtime (and the CLI host), like `RoutedDocumentEndpointDispatcher.cs`.

Nothing crosses the isolated load-context boundary. The MCP tool/resource surface, Router,
Transport, Offline, and the route lifecycle stay unchanged.

## Design

### Signals

| Signal | Source | What it tells us |
| --- | --- | --- |
| Rhino start | `OnLoad` | PID, Rhino version, `Environment.CommandLine`, parent PID and image name (`NtQueryInformationProcess` + `QueryFullProcessImageName`, best effort) |
| Commands | `Command.BeginCommand` / `EndCommand` | Command English name, owning plug-in, result, document serial. Kept in a ring buffer of the last 20; `Exit`/`Close`/`-Exit`/`-Close` are also logged immediately. |
| System close | Main-window subclass: `WM_SYSCOMMAND` with `SC_CLOSE` | Title-bar X, taskbar "Close window", Alt+F4, system menu |
| Window close | Main-window subclass: `WM_CLOSE` | Whether it followed an `SC_CLOSE` or came alone (programmatic), and whether it was sent from another thread or process (`InSendMessageEx`) |
| Session end | Main-window subclass: `WM_QUERYENDSESSION` / `WM_ENDSESSION` | Logoff, shutdown, or Restart Manager (`ENDSESSION_CLOSEAPP`) |
| App closing | `RhinoApp.Closing` | Rhino accepted the close |
| Saves | `RhinoDoc.BeginSaveDocument` / `EndSaveDocument` | Saves after a close request show the "save changes" path |
| Document close | `RhinoDoc.CloseDocument` | Path and serial |
| Process exit | `AppDomain.ProcessExit` | Last line, proves an orderly end. A missing line means a kill or crash. |

At each close-request signal the record also captures:
- every open document's path, runtime serial, and `Modified` flag;
- the foreground window's process ID and image name;
- the recent-command ring buffer.

The subclass uses `SetWindowSubclass` / `DefSubclassProc` / `RemoveWindowSubclass` from
comctl32. It installs on the UI thread, observes only, always chains, and never changes return
values. The Server targets `net8.0` without WinForms, so plain P/Invoke avoids adding a UI
framework reference.

### Log file

- Path: `%LOCALAPPDATA%\MCP_Rhino\Logs\rhino-lifecycle-YYYYMMDD.jsonl`, one file per local date.
- One JSON object per line: `ts` (local ISO-8601 with offset), `pid`, `event`, then
  event-specific fields.
- Bounded:
  - lines up to 8 KiB;
  - up to 5 MiB per file, after which a single "truncated" line is written;
  - files older than 14 days are deleted at startup.
- Writes are synchronous append with `FileShare.ReadWrite`, so a file is complete even if the
  process dies right after.
- Paths of open documents are recorded. Command arguments and model content are not.

## Usage

Nothing to run. After an unexpected close, open the day's file and read from the
`close-request` line. The trigger fields there name the cause. The `save`/`document-close` lines
that follow and the final `process-exit` line show what happened next.

## Acceptance criteria

1. `dotnet build .\MCP_Rhino.sln -c Debug` and `-c Release` succeed. The plug-in identity checks
   still pass, and the tool/resource inventory and safety smokes are unchanged.
2. A live check in Rhino 8, each producing the expected records in order (`close-request` with
   the right trigger, unsaved state, `app-closing`, `save`, `document-close`, `process-exit`):
   - `_Exit` with unsaved changes, answered Yes;
   - title-bar X;
   - Alt+F4;
   - `WM_CLOSE` posted from an external process (PowerShell `PostMessage`). It must be classified
     as programmatic and cross-thread.
3. Cancelling the save prompt logs the request with no `process-exit`. Rhino stays usable, and
   MCP routes still work afterwards.
4. Start-up records show PID, parent, and command line for a double-clicked `.3dm` and a plain
   Rhino launch.
5. A logging failure (for example a read-only log folder) does not affect plug-in load or the MCP
   routes.
6. The installed RHP is attested per the AGENTS.md registration and production memos before
   being reported as installed.

## Risks

- **Subclassing Rhino's main window.** A bug there could destabilise Rhino. Mitigations: observe
  only, always chain, remove on shutdown, catch every exception inside the callback, and have the
  live check cover cancel/close repeatedly.
- **`WM_CLOSE` cannot name the sending process.** The record says "programmatic, cross-thread"
  rather than naming a sender. If that is not enough, the next step is enabling process auditing
  (user-owned) or a separate investigation.
- **Event-order differences across Rhino 8 service releases.** Records are timestamped
  individually; correctness does not depend on order.

## Rollback

Remove the two `OnLoad`/`OnShutdown` calls, or revert the change and reinstall the previous RHP
(1.3.7 rollback exists under `%LOCALAPPDATA%\MCP_Rhino\rollback`). Log files can be deleted
freely; nothing reads them.
