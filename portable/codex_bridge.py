"""Opt-in read-only Codex app-server adapter. Never starts a thread, login, or model turn."""
from __future__ import annotations
import json
import os
import queue
import shutil
import subprocess
import threading
import time
from .core import MAX_BYTES, parse_codex


def from_rate_limits(result):
    if not isinstance(result, dict):
        raise ValueError("Invalid rate-limit response")
    buckets = result.get("rateLimitsByLimitId")
    limits = buckets.get("codex") if isinstance(buckets, dict) else None
    if limits is None:
        limits = result.get("rateLimits")
    if not isinstance(limits, dict):
        raise ValueError("No Codex rate-limit bucket")
    # Other metered buckets and credits are not combined with included Codex allowance.
    payload = {"plan_type": limits.get("planType")}
    for key in ("primary", "secondary"):
        raw = limits.get(key)
        if raw is None:
            payload[key + "_window"] = None
        elif isinstance(raw, dict):
            duration = raw.get("windowDurationMins")
            payload[key + "_window"] = {"used_percent": raw.get("usedPercent"), "reset_at": raw.get("resetsAt"),
                "limit_window_seconds": duration * 60 if isinstance(duration, (int, float)) and not isinstance(duration, bool) else None}
        else:
            raise ValueError("Invalid rate-limit window")
    return parse_codex(payload)


def fetch_codex(timeout=15, executable=None, popen=subprocess.Popen):
    executable = executable or shutil.which("codex")
    if not executable:
        raise RuntimeError("Codex CLI not found; use an existing signed-in installation")
    process = popen([executable, "app-server"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                    stderr=subprocess.DEVNULL, text=True, encoding="utf-8", bufsize=1,
                    creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    messages = queue.Queue(maxsize=64)
    def read():
        try:
            while True:
                line = process.stdout.readline(MAX_BYTES + 1)
                if not line:
                    messages.put_nowait(None)
                    return
                if len(line) > MAX_BYTES:
                    messages.put_nowait(None)
                    return
                messages.put_nowait(json.loads(line))
        except (ValueError, OSError, queue.Full):
            try:
                messages.put_nowait(None)
            except queue.Full:
                pass
    thread = threading.Thread(target=read, daemon=True)
    thread.start()
    deadline = time.monotonic() + timeout
    def send(message):
        process.stdin.write(json.dumps(message) + "\n")
        process.stdin.flush()
    def receive(request_id):
        while True:
            wait = deadline - time.monotonic()
            if wait <= 0:
                raise TimeoutError("Codex usage read timed out")
            try:
                message = messages.get(timeout=wait)
            except queue.Empty:
                raise TimeoutError("Codex usage read timed out") from None
            if message is None:
                raise RuntimeError("Codex usage connection closed")
            if not isinstance(message, dict):
                raise ValueError("Invalid RPC response")
            # Reject server requests before matching response IDs; the two directions may reuse IDs.
            if "id" in message and "method" in message:
                send({"id": message["id"], "error": {"code": -32601, "message": "Read-only usage client"}})
                continue
            if message.get("id") == request_id:
                if "error" in message:
                    raise RuntimeError("Codex usage read failed; check existing CLI sign-in")
                return message.get("result")
    try:
        send({"id": 1, "method": "initialize", "params": {"clientInfo": {"name": "headroom_usage", "title": "Headroom", "version": "3.0.0"}}})
        receive(1)
        send({"method": "initialized", "params": {}})
        send({"id": 2, "method": "account/rateLimits/read"})
        return from_rate_limits(receive(2))
    finally:
        process.terminate()
        try:
            process.wait(timeout=2)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=2)
        for stream in (process.stdin, process.stdout):
            if stream:
                stream.close()
        thread.join(timeout=1)
