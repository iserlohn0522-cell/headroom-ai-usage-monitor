# Headroom — AI Usage Monitor for Claude Code & Codex


[![Platform](https://img.shields.io/badge/Platform-Windows-0078D4)](https://github.com/iserlohn0522-cell/headroom-ai-usage-monitor)
[![Language](https://img.shields.io/badge/Language-C%23-239120)](https://learn.microsoft.com/dotnet/csharp/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Headroom monitors remaining coding-plan allowance in a desktop widget. The native Windows frontend supports Claude Code and Codex; a portable Python/Tk frontend adapts the widget for macOS and Linux with local snapshot providers and an optional read-only Codex CLI adapter.

This fork is customized as a personal desktop quota widget. It has a genuinely small compact mode and a wider detailed mode. The panel surface keeps only three clear controls: pin, mode switch, and refresh all.

When the pointer leaves the widget, it can collapse into a quota ball. The sphere shows Claude 5h on the outer arc, Claude weekly on the inner arc and Codex weekly as liquid area. A thin Codex 5h arc appears only when supported. Hovering the native Windows sphere restores the panel; the portable sphere shows a hover legend. Weekly-only Codex snapshots select weekly and omit the unsupported five-hour track. A quota ball near an outer desktop edge can retract so that only a small colored handle remains.

The UI supports Simplified Chinese and English, with Simplified Chinese as the new-install default. Older Japanese UI preferences migrate to Simplified Chinese; Japanese reset-time input remains supported for API compatibility.

Refresh policy: the normal usage API refresh interval is 5 minutes. The widget may poll more frequently around reset boundaries so the display updates quickly. Usage API refreshes do not consume Claude Code or Codex conversation tokens, but the app still respects API rate limits and backs off on HTTP 429.

Security posture: the app reads existing local CLI credential files and does not copy access tokens into settings. It does not upload telemetry, analytics, usage data, account IDs, or tokens. Settings contain UI preferences and refresh behavior only.

## Features

- **Compact monitoring** — Claude Code and Codex 5-hour and weekly remaining quotas in one small widget
- **Two useful sizes** — compact `232×94` default and a roomier detailed view
- **Quota ball and edge hide** — idle collapse, hover restore, and simultaneous allowance arcs and an area-correct liquid disk
- **Chinese and English UI** — localized panel tooltips, tray menu, settings, status, login, and OAuth completion pages
- **Full settings from the tray** — quick tray controls for opacity, overall scale, service visibility, and auto-hide, plus detailed text, quota-bar, button, and ball sizing
- **Low-quota warnings** — each quota row turns yellow or red at configurable thresholds
- **Account controls** — log in or log out of Claude Code / Codex from the Settings dialog
- **OAuth-aware status** — reads CLI-compatible credentials, refreshes tokens, and backs off when the usage API returns rate limits

## Requirements

Runtime:

- 64-bit Windows 10 or Windows 11.
- .NET Framework 4.8 or later. Windows 10 22H2 and Windows 11 include a compatible .NET Framework 4.x runtime.
- The release zip does not require the .NET Framework Developer Pack.

Source builds:

- Windows PowerShell or PowerShell 7.
- .NET Framework 4.8 Developer Pack, or Visual Studio Build Tools with the .NET Framework 4.8 targeting pack.
- `build.ps1` intentionally uses `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe`; that is the shared CLR 4 toolchain path for .NET Framework 4.x, not a .NET Framework 4.0 target.

Install the build prerequisites with winget:

```powershell
winget install --id Microsoft.DotNet.Framework.DeveloperPack_4 --version 4.8 --source winget
```

## Getting Started

1. Download the latest versioned `Headroom-vX.Y.Z.zip` from [Releases](https://github.com/iserlohn0522-cell/headroom-ai-usage-monitor/releases) and unzip it.
2. Run the versioned `Headroom-vX.Y.Z.exe`.
3. On first launch, right-click the tray icon and open **Settings… → Account**, then click **Login** for each service.
   - By default, Headroom uses its built-in Browser OAuth flow. After signing in, the tab shows "Login complete" and Headroom picks up the new credentials automatically.
   - You can switch each service to **CLI** or **Auto** from **Settings → Account**. Auto uses the CLI when available and falls back to Browser OAuth.
   - For Claude CLI login, type `/login` in the opened terminal. Codex CLI starts `codex login` directly.
   - You can also log out and manage sessions from **Settings → Account**.

## Widget behavior

- **Compact** hides reset text and minimizes the panel footprint.
- **Detailed** shows full service names and reset information.
- **Quota ball** shows one selected provider/window (`CX 7d` means Codex weekly). Right-click → **Ball allowance** changes it; values are never averaged.
- **Edge hide** leaves a small colored arc visible when the quota ball is near an outer desktop edge.
- Only reported windows receive bars. Weekly-only snapshots omit 5h; missing/unreadable data never becomes zero.
- Warning rows turn yellow; critical and exhausted rows turn red.

## Buttons

| Panel control | Action |
|---------------|--------|
| Pin | Toggle always on top |
| Expand / collapse arrows | Switch compact and detailed modes |
| Circular arrow | Refresh all visible services now |

Exit and the complete settings surface remain available from the tray icon; there is intentionally no close button on the widget.

## Settings

Open **Settings…** from the tray menu.

- **General** — language, always on top, opacity, enable/disable each service, quota-ball and edge-hide behavior
- **Appearance** — overall and text scale, quota-bar height, panel-button size, and quota-ball size
- **Account** — login/logout controls and per-service login method (Browser OAuth / CLI / Auto)
- **Layout** — compact/detailed mode, service order, and reset-time format
- **Refresh** — normal interval (5 minutes by default)
- **Thresholds** — yellow at 50%, red at 30% (configurable)

## How it works

The app reads OAuth tokens from `%USERPROFILE%\.claude\.credentials.json` (Claude Code) and
`%USERPROFILE%\.codex\auth.json` (Codex), calls the respective usage APIs directly, and
renders a custom dark UI. Login method is configurable per service: Browser OAuth uses
Headroom's PKCE flow (system browser + localhost callback), CLI launches the installed
Claude/Codex CLI, and Auto preserves the old CLI-when-available behavior. Refresh tokens are used to keep
the access token alive without re-login, and 429 responses trigger a backoff instead of retrying in a tight loop. Settings are stored in
`%LOCALAPPDATA%\Headroom\settings.json`.

## Fixture mode

For UI verification without spending quota, start Headroom with a fixture folder:

```powershell
.\build.ps1 -DebugFixture
.\debug\Headroom.fixture.exe --fixture .\docs\fixtures\03-weekly-exhausted
```

The folder must contain `claude.json` and `codex.json` in the same shape as the live API
responses. Headroom watches those files and refreshes automatically when they change.
Set `HEADROOM_SETTINGS_PATH` to an isolated JSON path during fixture runs so visual tests do not modify the normal user settings.

## Build and Test from Source

```powershell
.\build.ps1
```

Run the parser/settings tests:

```powershell
.\tests\run-tests.ps1
```

Build the fixture binary for UI verification:

```powershell
.\build.ps1 -DebugFixture
```

If `build.ps1` reports that .NET Framework 4.8 reference assemblies are missing,
install the .NET Framework 4.8 Developer Pack. This is only required for building
from source; it is not required to run the released `Headroom.exe`.

To create a release archive:

```powershell
.\build.ps1 -Version 2.0.0
```

The archive is written to `releases/Headroom-vX.Y.Z.zip`. The `-Version` value is embedded in the exe version metadata and shown in Settings. Plain local `.\build.ps1` builds show `dev`.


## Capability-aware skins and portable frontend (v3 preview)

Right-click the widget or tray → **Skin**: Midnight (smooth dark), Paper (light cards), Terminal (monospaced segmented bars). **Ball allowance** chooses the provider/window; weekly is the fallback when 5h is absent. The footer and quota details explain it. `0% / Exhausted` is real zero; `— / No data` is unknown. Cached loading/stale/error values carry `~` and patterned tracks. The labeled ball is 64–96px; smaller legacy sizes migrate to 64px.

Settings v2 → v3 preserves language, service visibility and window preferences. Unavailable saved windows fall back at display time. Existing English remains English; new installs and legacy Japanese choices use Simplified Chinese.

- [Portable setup and actual platform coverage](portable/README.md)
- [Stable provider contract, JSON Schema and safe mock](docs/providers/README.md)
- [中文说明](README.zh-CN.md)
- [Visual previews and validation evidence](docs/previews/current/README.md)

```shell
python -m portable.app
python examples/providers/mock_provider.py --out .local-demo --scenario weekly-only
python -m portable.app --providers .local-demo/manifest.json
```

Reproduce Windows renders using synthetic data and isolated settings:

```powershell
.\build.ps1 -DebugFixture
.\debug\Headroom.fixture.exe --render-previews .\docs\previews\2026-10-02
$env:HEADROOM_KEEP_TEST_ARTIFACTS = '1'
.\tests\run-tests.ps1
python -m unittest discover -s tests -p test_portable.py -v
```

Windows was built and visually exercised here. [Hosted CI](https://github.com/iserlohn0522-cell/headroom-ai-usage-monitor/actions/runs/37033746827) also passed all 16 portable tests on Windows, macOS arm64 and Linux, including actual Tk integration (Linux under Xvfb). Manual macOS/Linux desktop behavior remains unverified. No new OAuth grants or live authenticated calls were made for this change.

## Portable sphere

See [portable setup and platform coverage](portable/README.md) for Python/Tk on Windows, macOS and Linux. No account is needed for the labeled demo. CI runs synthetic quota/provider and Tk integration tests on standard hosted Windows/macOS/Linux runners; desktop tray, Wayland behavior and user-machine packaging require separate validation.
