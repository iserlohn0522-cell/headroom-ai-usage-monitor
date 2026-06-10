# Headroom Custom Widget Design

Date: 2026-06-10

## Goal

Fork Headroom and customize it into a small Windows desktop quota widget for the user's personal Claude Code and Codex workflow.

The customized app should show remaining quota only, use battery-style bars, keep API usage conservative, and avoid leaking or copying credentials.

## Baseline

Use `tesuheee/headroom-ai-usage-monitor` as the base project.

Reasons:

- It already supports Claude Code and Codex usage APIs.
- It reads the same local credential files used by the CLIs.
- It has release builds, tests, fixture mode, rate-limit backoff, and token refresh handling.
- Its current default refresh interval is 15 minutes, with a 10 second scheduler check, 1 minute boost mode, and 10 second polling after a reset time has passed.

## UI Scope

The widget has two display modes.

### A Mode: Minimal Stacked View

A Mode is the default.

It is smaller than the original Headroom two-card view and uses a vertical stack:

1. Codex 5h
2. Codex 7d
3. Claude 5h
4. Claude 7d

The widget does not show the words `Codex` or `Claude` in the main surface. The services are identified by fixed order and color.

Each row shows:

- Tiny `5h` or `7d` label.
- Battery-style remaining quota bar.
- Remaining percentage.
- Reset time, preferably inside the bar or tightly attached to it when there is enough room.

### C Mode: Larger Edge Stack

C Mode is slightly larger than A Mode and is meant for pinning near a screen edge.

It also omits the `AI Headroom`, `Codex`, and `Claude` text labels from the main surface. It keeps the same four-row order and color language as A Mode, but uses more spacing and more readable reset text.

### Mode Toggle

The top-right corner contains one small mode control:

- In A Mode, `+` switches to C Mode.
- In C Mode, `-` switches to A Mode.

The selected mode is persisted in settings.

## Launch And Background Behavior

The finished app should be easy to start and should not require a terminal.

Requirements:

- The release package contains a normal Windows executable.
- The user can double-click the executable to launch the widget.
- No command window should remain open while the widget is running.
- The widget runs quietly in the background after launch.
- A tray icon is available for common controls.
- Default behavior is always-on-top enabled.

Tray menu should include at least:

- Show or hide widget.
- Toggle always-on-top.
- Switch A/C display mode.
- Open settings.
- Exit.

Settings should include at least:

- Opacity or transparency.
- Always-on-top.
- Refresh interval if keeping the existing settings surface is cheap.

### Colors

Normal colors:

- Codex 5h: light blue.
- Codex 7d: deeper blue.
- Claude 5h: Claude-like warm accent.
- Claude 7d: darker warm accent.

Low quota:

- No popup or notification.
- The affected battery row turns red at the critical threshold.
- Warning yellow can remain available internally, but the requested visible behavior is red for low quota.

## Display Rules

All quota values are displayed as remaining quota.

The existing used/remaining toggle is removed from the widget UI. Existing settings fields may remain for backward compatibility, but the custom widget forces `showUsed = false` for both services.

Reset time means the quota reset time for the corresponding window:

- 5h row shows the 5-hour reset time.
- 7d row shows the 7-day reset time.

The reset format should stay compact:

- Short relative time for near resets, such as `2h 14m`.
- Compact day/time for longer weekly resets, such as `Sat 09:00`.

## Refresh Policy

Usage API refreshes do not consume Claude Code or Codex conversation tokens. They are separate usage API calls.

The app should still avoid unnecessary API pressure:

- Normal refresh interval: 5 minutes.
- Scheduler wake/check interval: keep lightweight, around 10 seconds.
- After a reset time has passed: keep short polling so the display updates promptly.
- On HTTP 429: keep existing `Retry-After` handling and default backoff.
- Boost mode can be removed from the main UI. The underlying code may remain if it does not add visible clutter.

## Security

The customization must not weaken Headroom's credential posture.

Requirements:

- Read credentials from the existing CLI credential files only:
  - `%USERPROFILE%\.claude\.credentials.json`
  - `%USERPROFILE%\.codex\auth.json`
- Do not copy tokens into the app settings file.
- Do not upload usage data, tokens, account IDs, or debug logs anywhere.
- Do not add analytics, telemetry, crash upload, or remote logging.
- Settings store only UI preferences, refresh intervals, thresholds, and service visibility.
- Debug logs must not include access tokens or refresh tokens.

## Implementation Boundaries

Keep Headroom's working data layer unless a bug is found:

- Reuse usage clients.
- Reuse parsers.
- Reuse credential stores.
- Reuse fixture mode for UI verification.
- Reuse rate-limit backoff behavior.

Primary changes should be in:

- Drawing code.
- Hit targets and input handling for A/C mode switching.
- Settings model/store for the selected widget mode.
- Defaults for refresh interval and display mode.
- README notes for the custom fork.

## Verification

Before completion:

- Build the app from source.
- Run existing parser/settings/refresh tests.
- Build or run fixture mode for normal, low quota, exhausted, login required, and no-data fixtures.
- Verify both A and C layouts visually.
- Verify that all visible percentages are remaining percentages.
- Verify that no used/remaining toggle remains on the widget surface.
- Verify that low quota rows turn red without a popup.
- Verify that credentials are not copied into settings or repository files.
