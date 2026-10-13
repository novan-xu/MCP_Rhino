from __future__ import annotations

import argparse
import json
import os
import queue
import subprocess
import sys
import threading
import time
from pathlib import Path
from typing import Any


DEFAULT_ROUTER = Path(os.environ["LOCALAPPDATA"]) / "MCP_Rhino" / "bin" / "MCP_Rhino.Router.exe"


def get_ci(obj: Any, *names: str, default: Any = None) -> Any:
    if not isinstance(obj, dict):
        return default
    lowered = {str(key).lower(): value for key, value in obj.items()}
    for name in names:
        if name in obj:
            return obj[name]
        if name.lower() in lowered:
            return lowered[name.lower()]
    return default


def unwrap_tool_result(response: dict[str, Any], tool_name: str) -> Any:
    if "error" in response:
        raise RuntimeError(f"{tool_name} JSON-RPC error: {response['error']}")

    result = response.get("result")
    payload: Any = result
    if isinstance(result, dict) and "content" in result:
        texts = [
            str(item.get("text", ""))
            for item in result.get("content") or []
            if isinstance(item, dict) and item.get("type") == "text"
        ]
        text = "\n".join(texts).strip()
        if text:
            try:
                payload = json.loads(text)
            except json.JSONDecodeError:
                payload = text

    if isinstance(payload, dict) and any(str(key).lower() == "success" for key in payload):
        if not bool(get_ci(payload, "success", "Success", default=False)):
            raise RuntimeError(f"{tool_name} failed: {get_ci(payload, 'message', 'Message', default='')}")
        data = get_ci(payload, "data", "Data")
        return payload if data is None else data
    return payload


class RouterClient:
    def __init__(self, router: Path, document: str, timeout: float) -> None:
        self.router = router
        # Preserve mapped-drive spelling because the live route reports RhinoDoc.Path verbatim.
        self.document = os.path.abspath(document)
        self.timeout = timeout
        self.process: subprocess.Popen[str] | None = None
        self.stdout: queue.Queue[str | None] = queue.Queue()
        self.stderr: list[str] = []
        self.next_id = 1

    def __enter__(self) -> "RouterClient":
        self.start()
        return self

    def __exit__(self, exc_type: Any, exc: Any, tb: Any) -> None:
        self.close()

    def start(self) -> None:
        if not self.router.is_file():
            raise FileNotFoundError(f"Installed Router executable was not found: {self.router}")

        self.process = subprocess.Popen(
            [str(self.router)],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            encoding="utf-8",
            errors="replace",
            bufsize=1,
        )

        def read_stdout() -> None:
            assert self.process is not None and self.process.stdout is not None
            for line in self.process.stdout:
                if line.strip():
                    self.stdout.put(line)
            self.stdout.put(None)

        def read_stderr() -> None:
            assert self.process is not None and self.process.stderr is not None
            for line in self.process.stderr:
                if line.strip():
                    self.stderr.append(line.rstrip())

        threading.Thread(target=read_stdout, daemon=True).start()
        threading.Thread(target=read_stderr, daemon=True).start()

        self.request(
            "initialize",
            {
                "protocolVersion": "2025-11-25",
                "capabilities": {},
                "clientInfo": {"name": "stone-wireframe-bake", "version": "1.0.0"},
            },
        )
        self.notify("notifications/initialized", {})
        self.request("ping", {})

        documents = self.call_tool("rhino_router_list_documents", {})
        entries = get_ci(documents, "documents", "Documents", default=[]) or []
        selected = next(
            (
                entry
                for entry in entries
                if str(get_ci(entry, "filePath", "FilePath", default="")).casefold()
                == self.document.casefold()
            ),
            None,
        )
        if not isinstance(selected, dict):
            discovered = [str(get_ci(entry, "filePath", "FilePath", default="")) for entry in entries]
            raise RuntimeError(
                f"Router did not discover the exact live document: {self.document}. "
                f"Discovered: {discovered}"
            )

        session_id = str(get_ci(selected, "sessionId", "SessionId", default="")).strip()
        if not session_id:
            raise RuntimeError("The selected Router document did not include a session id.")
        self.call_tool("rhino_router_select_document", {"sessionId": session_id})

    def close(self) -> None:
        if self.process is None:
            return
        try:
            if self.process.stdin and not self.process.stdin.closed:
                self.process.stdin.close()
            self.process.wait(timeout=2)
        except Exception:
            if self.process.poll() is None:
                self.process.kill()
        finally:
            self.process = None

    def notify(self, method: str, params: dict[str, Any]) -> None:
        self.send({"jsonrpc": "2.0", "method": method, "params": params})

    def request(self, method: str, params: dict[str, Any]) -> dict[str, Any]:
        request_id = self.next_id
        self.next_id += 1
        self.send({"jsonrpc": "2.0", "id": request_id, "method": method, "params": params})
        deadline = time.monotonic() + self.timeout
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError(self.error_context(f"Timed out waiting for JSON-RPC id {request_id}."))
            try:
                line = self.stdout.get(timeout=remaining)
            except queue.Empty as exc:
                raise TimeoutError(self.error_context(f"Timed out waiting for JSON-RPC id {request_id}.")) from exc
            if line is None:
                raise RuntimeError(self.error_context(f"Router closed while waiting for JSON-RPC id {request_id}."))
            message = json.loads(line)
            if message.get("id") == request_id:
                return message

    def call_tool(self, name: str, arguments: dict[str, Any]) -> Any:
        response = self.request("tools/call", {"name": name, "arguments": arguments})
        return unwrap_tool_result(response, name)

    def send(self, message: dict[str, Any]) -> None:
        if self.process is None or self.process.stdin is None:
            raise RuntimeError("Router is not running.")
        self.process.stdin.write(json.dumps(message, separators=(",", ":")) + "\n")
        self.process.stdin.flush()

    def error_context(self, message: str) -> str:
        tail = "\n".join(self.stderr[-12:])
        return message if not tail else f"{message}\nRouter stderr:\n{tail}"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Preview or apply the MCP_Rhino STN-MET stone wireframe bake workflow in one live document."
    )
    parser.add_argument("--document", required=True, help="Exact path of the already-open saved Rhino document.")
    parser.add_argument(
        "--assembly",
        action="append",
        default=[],
        help="Optional exact STN-MET-GA-...-... assembly definition name. Repeat to select several.",
    )
    parser.add_argument("--output-mode", choices=["Curve", "WorldZSurface"], default="Curve")
    parser.add_argument("--surface-height-inches", type=float, default=5.0)
    parser.add_argument("--apply", action="store_true", help="Apply after a clean preview. Default is preview only.")
    parser.add_argument("--router", type=Path, default=DEFAULT_ROUTER, help="Installed Router executable path.")
    parser.add_argument("--timeout", type=float, default=300.0, help="Timeout in seconds per Router request.")
    return parser.parse_args()


def failed_count(payload: Any) -> int:
    return int(get_ci(payload, "failedCount", "FailedCount", default=0) or 0)


def main() -> int:
    args = parse_args()
    document = os.path.abspath(args.document)
    arguments = {"filePath": document, "assemblyDefinitionNames": args.assembly,
                 "outputMode": args.output_mode, "surfaceHeightInches": args.surface_height_inches}

    with RouterClient(args.router.resolve(), document, args.timeout) as client:
        preview = client.call_tool("preview_stone_wireframe_bake", arguments)
        result: dict[str, Any] = {"preview": preview}
        if failed_count(preview) > 0:
            print(json.dumps(result, indent=2))
            raise RuntimeError("Preview reported failures; no document mutation was attempted.")

        if args.apply:
            result["apply"] = client.call_tool("apply_stone_wireframe_bake", arguments)

    print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        raise SystemExit(1)
