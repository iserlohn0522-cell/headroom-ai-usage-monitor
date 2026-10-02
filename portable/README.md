# Headroom portable sphere - 3.0.0-preview.20261002.4

Python 3.10+ with Tk 8.6+. Runtime uses only the standard library; Pillow is optional for Windows screenshot QA. Extract the source ZIP and run from its top-level folder:

```shell
python3 -m portable.app --settings preview-settings.json
```

On Windows, use `python` if needed. The default is clearly labeled synthetic DEMO data and makes no account requests. Space/double-click toggles the sphere; Escape returns to compact; F5 refreshes. Right-click selects skin/language or a local provider manifest. Hover over the sphere for its legend and every quota/reset; drag moves the window. The normal OS close control exits.

## Sphere mapping

Outer amber arc = Claude 5h; inner green arc = Claude weekly; liquid disk area = Codex weekly. A thin third arc appears only when Codex actually reports 5h. Every value is remaining. Arc length is proportional; liquid height uses circular-segment area inversion, matching the native sphere. ? unknown, - not offered, ... loading, ! error, ~ cached/stale, 0 exhausted; + denotes extra supported windows available in expanded view. Missing windows never become zero-filled phantom tracks.

All three skins are available. The normal OS window frame remains: at the default 64px sphere size, the canvas is 120x90px to accommodate Windows caption controls and the demo/hover caption. A 96px sphere is supported through the existing ballSize setting. No native transparency, tray or automatic edge-retraction parity is claimed.

## Platform setup

- Windows: install Python 3.10+ with Tcl/Tk. `python -m tkinter` should open a test window.
- macOS: use a current python.org installer with bundled Tk; `python3 -m tkinter` verifies it. See [Python's Tk guidance](https://www.python.org/download/mac/tcltk/). Avoid the obsolete Apple-supplied Tk.
- Debian/Ubuntu: install `python3`, `python3-tk` and Chinese fonts if needed (`fonts-noto-cjk`); launch in a graphical session. For other Linux distributions use their corresponding Tk/font packages. A native Wayland session may run Tk through XWayland; this is not evidence of native Wayland positioning support.

```shell
python3 -m portable.app --fixture docs/fixtures/06-pro-weekly-only --settings preview-settings.json
python3 examples/providers/mock_provider.py --out .local-demo --scenario weekly-only
python3 -m portable.app --providers .local-demo/manifest.json --settings preview-settings.json
# Optional: existing signed-in Codex CLI, read-only allowance RPC
python3 -m portable.app --codex-cli
```

[Provider v1 contract](../docs/providers/README.md). Mock/snapshot providers use no network or credentials. Live Codex CLI access is opt-in and has fake-process protocol coverage; portable Claude uses a snapshot exporter. Direct authenticated Claude, tray and edge-retraction remain native-Windows-only.

Settings: Windows `%LOCALAPPDATA%/Headroom/portable-settings.json`; macOS `~/Library/Application Support/Headroom/settings.json`; Linux `$XDG_CONFIG_HOME/headroom/settings.json` (default `~/.config`). `--settings` or `HEADROOM_SETTINGS_PATH` overrides. English is preserved; Chinese is default; Japanese migrates to Chinese. Native Windows settings are separate.

## Validation and limits

Locally verified on Windows: 16 Python tests, including 17 shared quota cases, 11 area round trips, polygon area tolerance, fixed slots/error isolation, actual Tk controls/provider recovery and hover legend. The sphere screenshot matrix includes all skins, 0/1/25/50/75/99/100%, both-window plans, loading, unknown, unsupported, stale and per-provider errors.

The repository CI matrix exercises Windows/macOS/Linux synthetic tests, with Linux Tk under Xvfb. [Hosted CI passed](https://github.com/iserlohn0522-cell/headroom-ai-usage-monitor/actions/runs/37033746827) on commit `ce55cff`: Windows, macOS arm64 and Linux each ran all 16 tests, including real Tk sphere/hover integration (Linux under Xvfb), plus compilation checks. Native Windows release/fixture builds and all five C# test suites passed. These synthetic runner tests do not verify manual desktop behavior, multi-monitor scaling, sleep/wake, tray, native Wayland or macOS packaging. No notarized app bundle, AppImage or bundled Python runtime is supplied.

```shell
python3 -m unittest discover -s tests -p 'test_portable*.py' -v
# Set HEADROOM_UI_TESTS=1 for graphical Tk checks; use xvfb-run on headless Linux.
python3 -m compileall -q portable examples/providers
# Windows capture only, Pillow required; fixtures and isolated settings
python -m portable.app --smoke-test docs/previews/portable-sphere
```

Build a source ZIP without local settings/caches:

```shell
python3 -m portable.package --version 3.0.0-preview.20261002.4 --output releases/Headroom-portable-v3.0.0-preview.20261002.4.zip
```
