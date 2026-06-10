# Headroom Custom Widget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a custom Headroom fork that shows Claude Code and Codex remaining quota as compact battery bars with A/C display mode switching.

**Architecture:** Keep Headroom's data layer, credential handling, parsers, fixture mode, and rate-limit backoff intact. Change the settings defaults and drawing/input surface so the widget always displays remaining quota in two compact battery layouts.

**Tech Stack:** C# WinForms on .NET Framework 4.8, GitHub CLI, existing Headroom PowerShell build and test scripts.

---

## File Structure

Actual fork clone target:

- `D:\headroom-ai-usage-monitor`

Files to modify in the fork:

- `src/Models.cs`: add persisted widget display mode, opacity, always-on-top default, and change default refresh interval to 5 minutes.
- `src/SettingsStore.cs`: load/save the new display mode and opacity settings.
- `src/UsageForm.cs`: initialize service accent colors, force remaining display, size defaults, tray icon, opacity, and remove visible boost/token surface assumptions.
- `src/UsageForm.Drawing.cs`: replace the main card rendering with compact A/C battery rendering.
- `src/UsageForm.Input.cs`: add `+`/`-` mode toggle hit behavior and remove widget-surface used/remaining toggling.
- `src/SettingsForm.cs`: remove or de-emphasize used/remaining controls if they are directly exposed; expose opacity and always-on-top.
- `tests/SettingsStoreTests.cs`: test persisted display mode, opacity, default always-on-top, and 5-minute refresh default.
- `README.md`: document custom fork behavior, refresh policy, and security posture.

Files to create:

- `docs/superpowers/specs/2026-06-10-headroom-custom-widget-design.md`: copy the approved design into the fork.
- `docs/superpowers/plans/2026-06-10-headroom-custom-widget.md`: copy this implementation plan into the fork.

---

### Task 1: Fork And Clone

**Files:**
- Create workspace: `D:\headroom-ai-usage-monitor`
- No source modifications in this task

- [ ] **Step 1: Fork the upstream repository**

Run:

```powershell
gh repo fork tesuheee/headroom-ai-usage-monitor --clone=false
```

Expected:

```text
https://github.com/iserlohn0522-cell/headroom-ai-usage-monitor
```

- [ ] **Step 2: Clone the fork to D drive**

Run:

```powershell
git clone https://github.com/iserlohn0522-cell/headroom-ai-usage-monitor.git D:\headroom-ai-usage-monitor
```

Expected: `D:\headroom-ai-usage-monitor` contains `Headroom.csproj`, `src`, `tests`, and `.git`.

- [ ] **Step 3: Add upstream remote**

Run:

```powershell
git -C D:\headroom-ai-usage-monitor remote add upstream https://github.com/tesuheee/headroom-ai-usage-monitor.git
git -C D:\headroom-ai-usage-monitor remote -v
```

Expected: both `origin` and `upstream` are listed.

- [ ] **Step 4: Copy spec and plan into the fork**

Copy from:

```text
D:\headroom-usage-monitor-custom\docs\superpowers\specs\2026-06-10-headroom-custom-widget-design.md
D:\headroom-usage-monitor-custom\docs\superpowers\plans\2026-06-10-headroom-custom-widget.md
```

To:

```text
D:\headroom-ai-usage-monitor\docs\superpowers\specs\2026-06-10-headroom-custom-widget-design.md
D:\headroom-ai-usage-monitor\docs\superpowers\plans\2026-06-10-headroom-custom-widget.md
```

- [ ] **Step 5: Commit planning files**

Run:

```powershell
git -C D:\headroom-ai-usage-monitor add docs/superpowers
git -C D:\headroom-ai-usage-monitor commit -m "docs: add custom widget plan"
```

Expected: commit succeeds.

---

### Task 2: Settings Model And Defaults

**Files:**
- Modify: `D:\headroom-ai-usage-monitor\src\Models.cs`
- Modify: `D:\headroom-ai-usage-monitor\src\SettingsStore.cs`
- Test: `D:\headroom-ai-usage-monitor\tests\SettingsStoreTests.cs`

- [ ] **Step 1: Add a failing settings test**

Add a test method in `SettingsStoreTests.cs` and call it from `Run`:

```csharp
static void TestCustomWidgetDefaultsAndModeRoundTrip(string root)
{
    string settingsPath = Path.Combine(root, "custom-settings.json");
    var loaded = SettingsStore.Load(settingsPath, null);
    Equal(5, loaded.NormalIntervalMinutes, "normal interval default");
    Equal("compact", loaded.WidgetMode, "widget mode default");
    True(loaded.AlwaysOnTop, "always on top default");
    Equal(94, loaded.OpacityPercent, "opacity default");

    loaded.WidgetMode = "edge";
    loaded.OpacityPercent = 82;
    SettingsStore.Save(settingsPath, loaded);

    var reloaded = SettingsStore.Load(settingsPath, null);
    Equal("edge", reloaded.WidgetMode, "widget mode round trip");
    Equal(82, reloaded.OpacityPercent, "opacity round trip");
}
```

Expected before implementation: compile fails because `WidgetMode` does not exist.

- [ ] **Step 2: Add the setting to `WidgetSettings`**

In `Models.cs`, add:

```csharp
public string WidgetMode = "compact";
public int OpacityPercent = 94;
```

Change:

```csharp
public int NormalIntervalMinutes = 15;
```

To:

```csharp
public int NormalIntervalMinutes = 5;
```

Change:

```csharp
public bool AlwaysOnTop = false;
```

To:

```csharp
public bool AlwaysOnTop = true;
```

Update `CopyFrom`:

```csharp
WidgetMode = other.WidgetMode;
OpacityPercent = other.OpacityPercent;
```

- [ ] **Step 3: Load and save `widgetMode`**

In `SettingsStore.Load`, add:

```csharp
settings.WidgetMode = NormalizeWidgetMode(ReadString(root, "widgetMode", settings.WidgetMode));
settings.OpacityPercent = ClampOpacity(ReadInt(root, "opacityPercent", settings.OpacityPercent));
```

In `SettingsStore.Save`, add:

```csharp
{ "widgetMode", settings.WidgetMode },
{ "opacityPercent", settings.OpacityPercent },
```

Add:

```csharp
static string NormalizeWidgetMode(string value)
{
    return string.Equals(value, "edge", StringComparison.OrdinalIgnoreCase) ? "edge" : "compact";
}

static int ClampOpacity(int value)
{
    return Math.Max(35, Math.Min(100, value));
}
```

- [ ] **Step 4: Run settings tests**

Run:

```powershell
.\tests\run-tests.ps1
```

Expected: settings, parser, and refresh tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\Models.cs src\SettingsStore.cs tests\SettingsStoreTests.cs
git commit -m "feat: add custom widget mode setting"
```

---

### Task 3: Force Remaining Display And Custom Colors

**Files:**
- Modify: `D:\headroom-ai-usage-monitor\src\UsageForm.cs`
- Modify: `D:\headroom-ai-usage-monitor\src\UsageForm.Drawing.cs`

- [ ] **Step 1: Change service colors**

In `UsageForm.cs`, replace service state initialization with:

```csharp
ServiceState claude = new ServiceState("Claude", ClaudeUrl, Color.FromArgb(215, 154, 101));
ServiceState codex = new ServiceState("Codex", CodexUrl, Color.FromArgb(132, 205, 252));
```

- [ ] **Step 2: Force remaining display before rendering**

At the start of `PaintContent(Graphics g)` in `UsageForm.Drawing.cs`, add:

```csharp
settings.CodexShowUsed = false;
settings.ClaudeShowUsed = false;
```

This keeps backward-compatible settings fields but prevents used-mode rendering.

- [ ] **Step 3: Remove the visible token toggle from side rail registration and drawing**

In `DrawSideRail`, do not draw `token`.

In `RegisterSideRailHits`, do not register `token`.

Expected: no visible used/remaining toggle remains on the widget surface.

- [ ] **Step 4: Run tests**

Run:

```powershell
.\tests\run-tests.ps1
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\UsageForm.cs src\UsageForm.Drawing.cs
git commit -m "feat: force remaining quota display"
```

---

### Task 4: Battery Layout Rendering

**Files:**
- Modify: `D:\headroom-ai-usage-monitor\src\UsageForm.Drawing.cs`

- [ ] **Step 1: Add row color helpers**

Add these methods near existing color helpers:

```csharp
Color CodexFiveColor() { return Color.FromArgb(132, 205, 252); }
Color CodexWeekColor() { return Color.FromArgb(31, 101, 214); }
Color ClaudeFiveColor() { return Color.FromArgb(215, 154, 101); }
Color ClaudeWeekColor() { return Color.FromArgb(155, 90, 54); }

Color BatteryColor(int? remaining, Color normal)
{
    if (remaining.HasValue && remaining.Value <= settings.CriticalRemainingPercent)
        return Color.FromArgb(224, 73, 73);
    return normal;
}
```

- [ ] **Step 2: Route `PaintContent` to custom layouts**

Replace the existing card layout body in `PaintContent` with:

```csharp
if (string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase))
    DrawEdgeBatteryWidget(g);
else
    DrawCompactBatteryWidget(g);

if (sideRailOpacity > 0.01)
    DrawSideRail(g);
```

- [ ] **Step 3: Add compact A layout**

Add:

```csharp
void DrawCompactBatteryWidget(Graphics g)
{
    int pad = 8;
    int toggle = 24;
    int w = Math.Max(220, ClientSize.Width - 2);
    int rowH = Math.Max(24, (ClientSize.Height - pad * 2) / 4);
    using (var path = RoundRect(0, 0, ClientSize.Width - 1, ClientSize.Height - 1, 8))
    using (var bg = new SolidBrush(Color.FromArgb(24, 27, 32)))
        g.FillPath(bg, path);

    DrawModeToggle(g, "+");

    int y = pad;
    DrawBatteryRow(g, "5h", codex.Data.FiveHourRemainingPercent(), codex.DisplayedFivePct, codex.Data.FiveHourReset, x: pad, y: y, w: w - pad * 2 - toggle, h: rowH - 3, color: BatteryColor(codex.Data.FiveHourRemainingPercent(), CodexFiveColor()), weekly: false);
    y += rowH;
    DrawBatteryRow(g, "7d", codex.Data.WeeklyRemainingPercent(), codex.DisplayedWeekPct, codex.Data.WeeklyReset, x: pad, y: y, w: w - pad * 2 - toggle, h: rowH - 3, color: BatteryColor(codex.Data.WeeklyRemainingPercent(), CodexWeekColor()), weekly: true);
    y += rowH;
    DrawBatteryRow(g, "5h", claude.Data.FiveHourRemainingPercent(), claude.DisplayedFivePct, claude.Data.FiveHourReset, x: pad, y: y, w: w - pad * 2 - toggle, h: rowH - 3, color: BatteryColor(claude.Data.FiveHourRemainingPercent(), ClaudeFiveColor()), weekly: false);
    y += rowH;
    DrawBatteryRow(g, "7d", claude.Data.WeeklyRemainingPercent(), claude.DisplayedWeekPct, claude.Data.WeeklyReset, x: pad, y: y, w: w - pad * 2 - toggle, h: rowH - 3, color: BatteryColor(claude.Data.WeeklyRemainingPercent(), ClaudeWeekColor()), weekly: true);
}
```

- [ ] **Step 4: Add edge C layout**

Add:

```csharp
void DrawEdgeBatteryWidget(Graphics g)
{
    int pad = 10;
    int toggle = 24;
    int rowH = Math.Max(34, (ClientSize.Height - pad * 2) / 4);
    using (var path = RoundRect(0, 0, ClientSize.Width - 1, ClientSize.Height - 1, 8))
    using (var bg = new SolidBrush(Color.FromArgb(24, 27, 32)))
        g.FillPath(bg, path);

    DrawModeToggle(g, "-");

    int rowW = ClientSize.Width - pad * 2 - toggle;
    int y = pad;
    DrawBatteryRow(g, "5h", codex.Data.FiveHourRemainingPercent(), codex.DisplayedFivePct, codex.Data.FiveHourReset, pad, y, rowW, rowH - 4, BatteryColor(codex.Data.FiveHourRemainingPercent(), CodexFiveColor()), false);
    y += rowH;
    DrawBatteryRow(g, "7d", codex.Data.WeeklyRemainingPercent(), codex.DisplayedWeekPct, codex.Data.WeeklyReset, pad, y, rowW, rowH - 4, BatteryColor(codex.Data.WeeklyRemainingPercent(), CodexWeekColor()), true);
    y += rowH;
    DrawBatteryRow(g, "5h", claude.Data.FiveHourRemainingPercent(), claude.DisplayedFivePct, claude.Data.FiveHourReset, pad, y, rowW, rowH - 4, BatteryColor(claude.Data.FiveHourRemainingPercent(), ClaudeFiveColor()), false);
    y += rowH;
    DrawBatteryRow(g, "7d", claude.Data.WeeklyRemainingPercent(), claude.DisplayedWeekPct, claude.Data.WeeklyReset, pad, y, rowW, rowH - 4, BatteryColor(claude.Data.WeeklyRemainingPercent(), ClaudeWeekColor()), true);
}
```

- [ ] **Step 5: Add battery row and mode toggle drawing**

Add:

```csharp
void DrawModeToggle(Graphics g, string text)
{
    int size = 22;
    int x = ClientSize.Width - size - 6;
    int y = 6;
    hits["widgetMode"] = new Rectangle(x - 4, y - 4, size + 8, size + 8);
    bool hover = hoverKey == "widgetMode";
    using (var path = RoundRect(x, y, size, size, 6))
    using (var bg = new SolidBrush(hover ? Color.FromArgb(48, 54, 64) : Color.FromArgb(32, 37, 45)))
        g.FillPath(bg, path);
    using (var f = new Font("Segoe UI", 13f, FontStyle.Bold))
    using (var b = new SolidBrush(Color.FromArgb(220, 230, 240)))
    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        g.DrawString(text, f, b, new RectangleF(x, y - 1, size, size), sf);
}

void DrawBatteryRow(Graphics g, string label, int? remaining, double? displayed, string resetRaw, int x, int y, int w, int h, Color color, bool weekly)
{
    int labelW = 24;
    int pctW = 36;
    int barX = x + labelW;
    int barW = Math.Max(64, w - labelW - pctW);
    int barH = Math.Max(14, h - 6);
    int barY = y + (h - barH) / 2;
    string pct = remaining.HasValue ? remaining.Value.ToString(CultureInfo.InvariantCulture) + "%" : "--";
    string reset = ResetText(resetRaw, "relative", English, weekly)
        .Replace("Reset in ", "")
        .Replace("Reset ", "");

    using (var small = new Font("Segoe UI", 8.2f, FontStyle.Bold))
    using (var tiny = new Font("Segoe UI", 7.6f, FontStyle.Regular))
    using (var labelBrush = new SolidBrush(Color.FromArgb(190, 198, 208)))
    using (var pctBrush = new SolidBrush(Color.FromArgb(232, 238, 245)))
    {
        TextRenderer.DrawText(g, label, small, new Rectangle(x, y, labelW, h), Color.FromArgb(190, 198, 208), TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        DrawBatteryBar(g, barX, barY, barW, barH, displayed.HasValue ? displayed : remaining, color, reset, tiny);
        TextRenderer.DrawText(g, pct, small, new Rectangle(barX + barW + 4, y, pctW, h), Color.FromArgb(232, 238, 245), TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }
}

void DrawBatteryBar(Graphics g, int x, int y, int w, int h, double? pct, Color color, string reset, Font resetFont)
{
    using (var outline = RoundRect(x, y, w, h, 5))
    using (var bg = new SolidBrush(Color.FromArgb(17, 20, 24)))
    using (var pen = new Pen(Color.FromArgb(150, color), 1.5f))
    {
        g.FillPath(bg, outline);
        g.DrawPath(pen, outline);
    }
    using (var nub = new SolidBrush(Color.FromArgb(150, color)))
        g.FillRectangle(nub, x + w, y + h / 3, 3, Math.Max(4, h / 3));
    if (pct.HasValue)
    {
        int fillW = Math.Max(0, (int)Math.Round((w - 4) * Math.Max(0, Math.Min(100, pct.Value)) / 100.0));
        if (fillW > 0)
        {
            using (var fillPath = RoundRect(x + 2, y + 2, fillW, h - 4, 3))
            using (var fill = new SolidBrush(Color.FromArgb(220, color)))
                g.FillPath(fill, fillPath);
        }
    }
    if (!string.IsNullOrWhiteSpace(reset))
    {
        using (var b = new SolidBrush(Color.FromArgb(220, 238, 242, 248)))
            TextRenderer.DrawText(g, reset, resetFont, new Rectangle(x + 6, y, w - 12, h), Color.FromArgb(220, 238, 242, 248), TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis);
    }
}
```

- [ ] **Step 6: Build fixture binary**

Run:

```powershell
.\build.ps1 -DebugFixture
```

Expected: `debug\Headroom.fixture.exe` builds.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src\UsageForm.Drawing.cs
git commit -m "feat: render custom battery widget"
```

---

### Task 5: Mode Toggle Input And Sizing

**Files:**
- Modify: `D:\headroom-ai-usage-monitor\src\UsageForm.Input.cs`
- Modify: `D:\headroom-ai-usage-monitor\src\UsageForm.Drawing.cs`

- [ ] **Step 1: Add `widgetMode` click behavior**

In `HandleClickAsync`, add a case:

```csharp
if (key == "widgetMode")
{
    settings.WidgetMode = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase) ? "compact" : "edge";
    ApplyIdealSize();
    settings.Save();
    Invalidate();
    return;
}
```

- [ ] **Step 2: Ignore token toggle clicks**

Remove or bypass the `token` click branch so the used/remaining mode cannot be toggled from the widget.

- [ ] **Step 3: Adjust ideal sizes**

In `ApplyIdealSize`, set:

```csharp
if (string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase))
{
    idealW = 240;
    idealH = 190;
}
else
{
    idealW = 300;
    idealH = 124;
}
```

Keep minimum size checks.

- [ ] **Step 4: Run tests**

Run:

```powershell
.\tests\run-tests.ps1
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

Run:

```powershell
git add src\UsageForm.Input.cs src\UsageForm.Drawing.cs
git commit -m "feat: add compact edge mode toggle"
```

---

### Task 6: Tray Icon, Opacity, And Launch Experience

**Files:**
- Modify: `D:\headroom-ai-usage-monitor\src\UsageForm.cs`
- Modify: `D:\headroom-ai-usage-monitor\src\UsageForm.Drawing.cs`
- Modify: `D:\headroom-ai-usage-monitor\src\SettingsForm.cs`

- [ ] **Step 1: Add tray fields**

In `UsageForm.cs`, add fields:

```csharp
readonly NotifyIcon trayIcon = new NotifyIcon();
readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
```

- [ ] **Step 2: Initialize tray icon in the constructor**

After form properties are initialized in `UsageForm()`:

```csharp
SetupTrayIcon();
TopMost = settings.AlwaysOnTop;
```

Add:

```csharp
void SetupTrayIcon()
{
    trayMenu.Items.Clear();
    trayMenu.Items.Add("Show / Hide", null, (s, e) =>
    {
        Visible = !Visible;
        if (Visible) Activate();
    });
    trayMenu.Items.Add(settings.AlwaysOnTop ? "Always on top: On" : "Always on top: Off", null, (s, e) =>
    {
        settings.AlwaysOnTop = !settings.AlwaysOnTop;
        TopMost = settings.AlwaysOnTop;
        settings.Save();
        SetupTrayIcon();
    });
    trayMenu.Items.Add(string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase) ? "Switch to compact" : "Switch to edge", null, (s, e) =>
    {
        settings.WidgetMode = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase) ? "compact" : "edge";
        ApplyIdealSize();
        settings.Save();
        Invalidate();
        SetupTrayIcon();
    });
    trayMenu.Items.Add("Settings", null, async (s, e) => await ShowSettingsDialog());
    trayMenu.Items.Add("Exit", null, (s, e) => Close());

    trayIcon.Text = "Headroom";
    trayIcon.Icon = Icon ?? SystemIcons.Application;
    trayIcon.ContextMenuStrip = trayMenu;
    trayIcon.Visible = true;
    trayIcon.DoubleClick += (s, e) =>
    {
        Visible = true;
        Activate();
    };
}
```

- [ ] **Step 3: Dispose tray icon on close**

In `FormClosed`, add:

```csharp
try { trayIcon.Visible = false; trayIcon.Dispose(); } catch { }
try { trayMenu.Dispose(); } catch { }
```

- [ ] **Step 4: Apply opacity to layered window rendering**

In `RenderLayered`, change:

```csharp
var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
```

To:

```csharp
byte alpha = (byte)Math.Max(35, Math.Min(255, settings.OpacityPercent * 255 / 100));
var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
```

- [ ] **Step 5: Add opacity to settings UI**

In `SettingsForm.cs`, add an opacity numeric input or slider bound to `settings.OpacityPercent`. Clamp saved values to 35-100.

Minimal implementation:

```csharp
var opacity = new NumericUpDown { Minimum = 35, Maximum = 100, Value = settings.OpacityPercent, Width = 70 };
opacity.ValueChanged += (s, e) => settings.OpacityPercent = (int)opacity.Value;
```

Place it near the existing always-on-top control.

- [ ] **Step 6: Verify double-click launch behavior through `Start-Process`**

Run:

```powershell
.\build.ps1 -DebugFixture
Start-Process .\debug\Headroom.fixture.exe -ArgumentList '--fixture', '.\docs\fixtures\01-ok'
```

Expected: the app opens without a terminal window that must remain open. The tray icon appears, default topmost is enabled, and opacity changes affect the widget.

- [ ] **Step 7: Commit**

Run:

```powershell
git add src\UsageForm.cs src\UsageForm.Drawing.cs src\SettingsForm.cs
git commit -m "feat: add tray and opacity controls"
```

---

### Task 7: Documentation And Safety Notes

**Files:**
- Modify: `D:\headroom-ai-usage-monitor\README.md`

- [ ] **Step 1: Update README intro**

Add a paragraph near the top:

```markdown
This fork is customized as a personal desktop quota widget. It always shows remaining quota, uses compact battery bars, and provides two display modes: a minimal stacked view and a slightly larger edge view.
```

- [ ] **Step 2: Document launch behavior**

Add:

```markdown
Launch behavior: unzip the release package and double-click `Headroom.exe`. The app runs as a normal Windows desktop app with a tray icon; no terminal needs to stay open. The widget is always-on-top by default and can be adjusted from the tray menu or Settings.
```

- [ ] **Step 3: Document refresh policy**

Add:

```markdown
Refresh policy: the normal usage API refresh interval is 5 minutes. The widget may poll more frequently around reset boundaries so the display updates quickly. Usage API refreshes do not consume Claude Code or Codex conversation tokens, but the app still respects API rate limits and backs off on HTTP 429.
```

- [ ] **Step 4: Document security posture**

Add:

```markdown
Security posture: the app reads existing local CLI credential files and does not copy access tokens into settings. It does not upload telemetry, analytics, usage data, account IDs, or tokens. Settings contain UI preferences and refresh behavior only.
```

- [ ] **Step 5: Commit**

Run:

```powershell
git add README.md
git commit -m "docs: describe custom widget behavior"
```

---

### Task 8: Final Verification

**Files:**
- No planned source edits unless verification finds a defect

- [ ] **Step 1: Run unit tests**

Run:

```powershell
.\tests\run-tests.ps1
```

Expected: all test groups pass.

- [ ] **Step 2: Build release**

Run:

```powershell
.\build.ps1 -Version 2.1.5-custom.1
```

Expected: release archive appears under `releases`.

- [ ] **Step 3: Build fixture binary**

Run:

```powershell
.\build.ps1 -DebugFixture
```

Expected: fixture executable appears under `debug`.

- [ ] **Step 4: Run fixture samples**

Run at least:

```powershell
.\debug\Headroom.fixture.exe --fixture .\docs\fixtures\01-ok
.\debug\Headroom.fixture.exe --fixture .\docs\fixtures\02-five-hour-exhausted
.\debug\Headroom.fixture.exe --fixture .\docs\fixtures\03-weekly-exhausted
```

Expected:

- A mode starts with four compact rows.
- `+` switches to C mode.
- `-` switches back to A mode.
- Low quota rows turn red.
- No Codex, Claude, or AI Headroom labels appear on the main widget.
- Reset time appears for each 5h and 7d row.
- Tray icon appears and offers show/hide, always-on-top, A/C mode switching, settings, and exit.
- Double-click launch does not require a terminal to remain open.

- [ ] **Step 5: Inspect settings for sensitive data**

Run:

```powershell
Get-Content "$env:LOCALAPPDATA\Headroom\settings.json"
```

Expected: no access tokens, refresh tokens, ID tokens, account IDs, or usage API response bodies.

- [ ] **Step 6: Inspect Git diff for sensitive data**

Run:

```powershell
git status --short
git grep -n "access_token\|refresh_token\|id_token\|Authorization\|Bearer" -- . ':!src/UsageClients.cs' ':!src/CredentialStores.cs' ':!tests'
```

Expected: no new hardcoded tokens or bearer values.

- [ ] **Step 7: Final commit if verification fixes were needed**

Run only if files changed during verification:

```powershell
git add <changed-files>
git commit -m "fix: polish custom widget verification"
```
