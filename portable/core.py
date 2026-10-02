"""Credential-free quota semantics. Shared conformance corpus: tests/allowance-cases.json."""
from __future__ import annotations
from dataclasses import dataclass, field
from datetime import datetime, timedelta, timezone
from pathlib import Path
import json
import math
import os
import re
import sys
import unicodedata

MAX_BYTES = 1024 * 1024
UTC = timezone.utc

@dataclass
class Window:
    id: str
    label: str
    support: str = "Unknown"
    remaining: float | None = None
    reset: str | None = None
    reset_kind: str = "unknown"
    state: str = "fresh"

@dataclass
class Snapshot:
    provider_id: str
    name: str
    observed_at: datetime
    windows: list[Window] = field(default_factory=list)
    status: str | None = None
    plan: str | None = None

@dataclass
class View:
    snapshot: Snapshot
    window: Window
    condition: str
    fallback: str | None = None

    @property
    def current(self):
        return self.condition in ("Fresh", "Exhausted")

    @property
    def has_value(self):
        return self.window.support == "Supported" and self.window.remaining is not None


def language(value):
    return "en" if isinstance(value, str) and value.lower() == "en" else "zh-CN"


def settings_path(platform=None, environ=None, home=None):
    platform = platform or sys.platform
    environ = os.environ if environ is None else environ
    home = Path.home() if home is None else Path(home)
    if environ.get("HEADROOM_SETTINGS_PATH"):
        return Path(environ["HEADROOM_SETTINGS_PATH"]).expanduser()
    if platform == "win32":
        return Path(environ.get("LOCALAPPDATA", home / "AppData/Local")) / "Headroom/portable-settings.json"
    if platform == "darwin":
        return home / "Library/Application Support/Headroom/settings.json"
    return Path(environ.get("XDG_CONFIG_HOME", home / ".config")) / "headroom/settings.json"


def migrate_settings(raw):
    # Preserve unknown fields so an older frontend cannot erase a newer preference.
    result = dict(raw) if isinstance(raw, dict) else {}
    result.update(settingsVersion=3, language=language(result.get("language")))
    if result.get("skin") not in ("midnight", "paper", "terminal"):
        result["skin"] = "midnight"
    result.setdefault("ballService", "codex")
    result.setdefault("ballWindow", "auto")
    size=result.get("ballSize",64)
    result["ballSize"]=max(64,min(96,size)) if isinstance(size,int) and not isinstance(size,bool) else 64
    result.setdefault("alwaysOnTop", True)
    result.setdefault("widgetMode", "compact")
    interval=result.get("normalIntervalMinutes",5)
    result["normalIntervalMinutes"]=max(1,min(240,interval)) if type(interval) is int else 5
    for key,default in (("ballService","codex"),("ballWindow","auto"),("providerManifest","")):
        if not isinstance(result.get(key,default),str):
            result[key]=default
    if not isinstance(result.get("alwaysOnTop"),bool):
        result["alwaysOnTop"]=True
    return result


def valid_percent(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value) and 0 <= value <= 100


def timestamp(value):
    if not isinstance(value, str) or not re.search(r"(Z|[+-]\d{2}:\d{2})$", value):
        raise ValueError("Timestamp requires ISO 8601 timezone")
    result = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if result.tzinfo is None:
        raise ValueError("Timestamp requires timezone")
    return result


def required(raw, key, maximum):
    value = raw.get(key) if isinstance(raw, dict) else None
    if not isinstance(value, str) or not value.strip() or len(value) > maximum or any(unicodedata.category(c) == "Cc" for c in value):
        raise ValueError(f"Invalid {key}")
    return value


def parse_snapshot(raw):
    if not isinstance(raw, dict) or type(raw.get("schemaVersion")) is not int or raw["schemaVersion"] != 1:
        raise ValueError("Unsupported snapshot version")
    provider_id = required(raw, "providerId", 40)
    if not re.fullmatch(r"[a-z][a-z0-9-]*", provider_id):
        raise ValueError("Invalid provider ID")
    status = required(raw, "status", 16)
    if status not in ("ok", "loading", "error"):
        raise ValueError("Invalid provider status")
    snap = Snapshot(provider_id, required(raw, "displayName", 40), timestamp(required(raw, "observedAt", 40)),
                    status={"ok": None, "loading": "updating", "error": "fetch_error"}[status])
    windows = raw.get("windows")
    if not isinstance(windows, list) or len(windows) > 16:
        raise ValueError("Expected at most sixteen windows")
    ids = set()
    for item in windows:
        window_id = required(item, "id", 40)
        if not re.fullmatch(r"[a-z][a-z0-9-]*", window_id) or window_id in ids:
            raise ValueError("Invalid/duplicate window ID")
        ids.add(window_id)
        support = required(item, "support", 16)
        if support not in ("supported", "unsupported", "unknown"):
            raise ValueError("Invalid support state")
        remaining = item.get("remainingPercent")
        if remaining is not None and (not valid_percent(remaining) or support != "supported"):
            raise ValueError("Invalid remaining percentage")
        state = item.get("state", "fresh")
        if state not in ("fresh", "loading", "stale", "error", "unknown") or (state == "unknown" and remaining is not None):
            raise ValueError("Invalid window state")
        reset = item.get("reset")
        at, kind = None, "unknown"
        if reset is not None:
            if not isinstance(reset, dict):
                raise ValueError("Invalid reset")
            kind = required(reset, "kind", 16)
            if kind not in ("rolling", "fixed", "unknown"):
                raise ValueError("Invalid reset kind")
            at = reset.get("at")
            if at is not None:
                timestamp(at)
        snap.windows.append(Window(window_id, required(item, "label", 40), support.capitalize(), remaining, at, kind, state))
    return snap


def read_json(path):
    path = Path(path)
    if str(path).startswith(("\\\\", "//")) or path.suffix.lower() != ".json":
        raise ValueError("Local JSON required")
    with path.open("rb") as stream:
        data = stream.read(MAX_BYTES + 1)
    if len(data) > MAX_BYTES:
        raise ValueError("Snapshot exceeds 1 MiB")
    try:
        return json.loads(data.decode("utf-8-sig"), parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Non-finite JSON number")))
    except RecursionError:
        raise ValueError("Provider JSON nesting exceeds limit") from None


def read_manifest(path):
    path = Path(path).resolve()
    raw = read_json(path)
    if not isinstance(raw, dict) or type(raw.get("schemaVersion")) is not int or raw["schemaVersion"] != 1:
        raise ValueError("Unsupported manifest version")
    entries = raw.get("providers")
    if not isinstance(entries, list) or len(entries) > 8:
        raise ValueError("Expected at most eight providers")
    result = {}
    for item in entries:
        provider_id = required(item, "id", 40)
        if not re.fullmatch(r"[a-z][a-z0-9-]*", provider_id) or provider_id in result or provider_id in ("codex", "claude"):
            raise ValueError("Invalid or duplicate/reserved provider ID")
        relative = Path(required(item, "path", 240))
        if relative.is_absolute():
            raise ValueError("Provider path must be relative")
        target = (path.parent / relative).resolve()
        if path.parent not in target.parents or target.suffix.lower() != ".json":
            raise ValueError("Provider file must be JSON within manifest directory")
        result[provider_id] = target
    return result


def parse_codex(raw, now=None):
    now = now or datetime.now(UTC)
    snap = Snapshot("codex", "Codex", now)
    if not isinstance(raw, dict):
        snap.status = "fetch_error"
        return snap
    snap.plan = raw.get("plan_type")
    limits = raw.get("rate_limit") if isinstance(raw.get("rate_limit"), dict) else raw
    for position in ("primary", "secondary"):
        item = limits.get(position + "_window")
        if not isinstance(item, dict) or not item:
            continue
        duration = item.get("limit_window_seconds")
        window_id = {18000: "five-hour", 604800: "weekly"}.get(duration, position) if isinstance(duration, (int, float)) else position
        label = {18000: "5h", 604800: "7d"}.get(duration) if isinstance(duration, (int, float)) else None
        if label is None:
            if isinstance(duration,(int,float)) and duration>0:
                label=f"{duration/3600:g}h" if duration%3600==0 else f"{duration/60:g}m"
            else:
                label=position
        used = item.get("used_percent")
        remaining = 100 - used if valid_percent(used) else None
        reset = None
        try:
            reset_at = item.get("reset_at")
            if isinstance(reset_at, (int, float)) and reset_at > 0:
                reset = datetime.fromtimestamp(reset_at, UTC).isoformat()
        except (OverflowError, OSError, ValueError):
            pass
        after = item.get("reset_after_seconds")
        if reset is None and isinstance(after, (int, float)) and 0 <= after < 31536000:
            reset = (now + timedelta(seconds=after)).isoformat()
        if any(w.id == window_id for w in snap.windows):
            snap.status = "fetch_error"
            continue
        snap.windows.append(Window(window_id, label, "Supported", remaining, reset, state="fresh" if remaining is not None else "error"))
    known = bool(snap.windows)
    for window_id, label in (("five-hour", "5h"), ("weekly", "7d")):
        if not any(w.id == window_id for w in snap.windows):
            snap.windows.append(Window(window_id, label, "Unsupported" if known else "Unknown"))
    if not any(w.remaining is not None for w in snap.windows) and snap.status is None:
        snap.status = "no_data"
    return snap


def parse_claude(raw, now=None):
    snap = Snapshot("claude", "Claude", now or datetime.now(UTC))
    if not isinstance(raw, dict):
        snap.status = "fetch_error"
        return snap
    for key, window_id, label in (("five_hour", "five-hour", "5h"), ("seven_day", "weekly", "7d")):
        item = raw.get(key)
        support = "Supported" if isinstance(item, dict) else "Unsupported" if key in raw and item is None else "Unknown"
        used = item.get("utilization") if isinstance(item, dict) else None
        remaining = 100 - used if valid_percent(used) else None
        snap.windows.append(Window(window_id, label, support, remaining, item.get("resets_at") if isinstance(item, dict) and isinstance(item.get("resets_at"),str) else None,
                                   state="error" if isinstance(item, dict) and remaining is None else "fresh"))
    if not any(w.remaining is not None for w in snap.windows):
        snap.status = "no_data"
    return snap


def map_view(snapshot, window, now=None, interval=5):
    now = now or datetime.now(UTC)
    if (snapshot.status not in (None, "", "ok", "no_data", "starting", "updating") or window.state == "error" or snapshot.observed_at > now + timedelta(minutes=5)):
        state = "Error"
    elif snapshot.status in ("starting", "updating") or window.state == "loading":
        state = "Loading"
    elif window.support == "Unsupported":
        state = "Unsupported"
    elif window.state == "stale" or (window.remaining is not None and now - snapshot.observed_at > timedelta(minutes=max(2, interval * 2))):
        state = "Stale"
    elif window.remaining is None or window.support == "Unknown":
        state = "Unknown"
    else:
        state = "Exhausted" if window.remaining <= 0 else "Fresh"
    return View(snapshot, window, state)


def select(snapshots, settings, now=None):
    if not snapshots:
        return None
    preferred = str(settings.get("ballService", "codex")).lower()
    snap = next((s for s in snapshots if s.provider_id.lower() == preferred), snapshots[0])
    supported = [w for w in snap.windows if w.support == "Supported"]
    preference = settings.get("ballWindow", "auto")
    window = next((w for w in supported if w.id == preference), None)
    explicit_missing = preference != "auto" and window is None
    if window is None:
        window = next((w for w in supported if w.id == "five-hour"), None) or next((w for w in supported if w.id == "weekly"), None) or next(iter(supported), None)
    if window is None:
        window = next((w for w in snap.windows if w.support != "Unsupported"), None) or next(iter(snap.windows), Window("unknown", "?"))
    view = map_view(snap, window, now, settings.get("normalIntervalMinutes", 5))
    if explicit_missing:
        view.fallback = "preferred-unavailable"
    elif window.id == "weekly" and any(w.id == "five-hour" and w.support == "Unsupported" for w in snap.windows):
        view.fallback = "weekly-only"
    return view


def liquid_height(fraction):
    """Bottom segment height / diameter, inverted from circular disk area."""
    fraction=max(0.0,min(1.0,float(fraction)))
    if fraction in (0.0,1.0):return fraction
    low,high=0.0,2.0
    for _ in range(50):
        h=(low+high)/2; y=1-h
        area=(math.acos(y)-y*math.sqrt(max(0,1-y*y)))/math.pi
        if area<fraction:low=h
        else:high=h
    return (low+high)/4


def liquid_segment(fraction, segments=720):
    """Unit-radius polygon below the exact area-derived waterline (screen y down)."""
    if fraction<=0:return []
    y=1-2*liquid_height(fraction)
    start=math.asin(max(-1,min(1,y)));end=math.pi-start
    return [(math.cos(start+(end-start)*i/segments),math.sin(start+(end-start)*i/segments)) for i in range(segments+1)]


def sphere_window(snapshots,provider_id,window_id,now=None,interval=5):
    snap=next((s for s in snapshots if s.provider_id.lower()==provider_id.lower()),None)
    if snap is None:return None
    window=next((w for w in snap.windows if w.id==window_id and w.support=="Supported"),None)
    return map_view(snap,window,now,interval) if window else None
