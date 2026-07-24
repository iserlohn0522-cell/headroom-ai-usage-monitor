using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Headroom
{
    partial class UsageForm
    {
        void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            string key = HitKey(e.Location);
            if (key.Length > 0)
            {
                BeginInvoke(new Action(async () => await HandleClickAsync(key)));
                return;
            }

            string sk = SilentHitKey(e.Location);
            if (sk.Length > 0)
            {
                pendingSilentKey = sk;
                pendingSilentScreenStart = PointToScreen(e.Location);
                pendingSilentWindowStart = Location;
                silentDragging = false;
                return;
            }

            ReleaseCapture();
            SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
        }

        async Task HandleClickAsync(string key)
        {
            if (key == "ball")
            {
                ExpandFromBall();
                return;
            }
            if (key == "pin")
            {
                settings.AlwaysOnTop = !settings.AlwaysOnTop;
                TopMost = settings.AlwaysOnTop;
                settings.Save();
                SetupTrayIcon();
                Invalidate();
                return;
            }
            if (key == "refreshAll")
            {
                await RefreshAllAsync(true);
                return;
            }
            if (key == "widgetMode")
            {
                settings.WidgetMode = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase) ? "compact" : "edge";
                ApplyLayoutMinimumSize();
                ApplyIdealSize();
                settings.Save();
                SetupTrayIcon();
                Invalidate();
                return;
            }

            if (key.EndsWith("-fiveResetLabel"))
            {
                settings.FiveHourResetMode = string.Equals(settings.FiveHourResetMode, "relative", StringComparison.OrdinalIgnoreCase) ? "time" : "relative";
                settings.Save();
                Invalidate();
                return;
            }
            if (key.EndsWith("-weekResetLabel"))
            {
                settings.WeeklyResetMode = string.Equals(settings.WeeklyResetMode, "relative", StringComparison.OrdinalIgnoreCase) ? "time" : "relative";
                settings.Save();
                Invalidate();
                return;
            }

            ServiceState service = key.StartsWith("codex") ? codex : claude;
            if (key.EndsWith("refresh"))
                await RefreshServiceAsync(service, true);
            else if (key.EndsWith("boost"))
            {
                bool activated = ToggleBoost(service);
                if (activated)
                    await RefreshServiceAsync(service, true);
            }
            else if (key.EndsWith("login"))
                await OpenLoginAsync(service);
        }

        string TooltipText(string key)
        {
            if (key == "pin") return T(settings.AlwaysOnTop ? "取消置顶" : "置顶显示", settings.AlwaysOnTop ? "Unpin from top" : "Always on top");
            if (key == "widgetMode")
                return string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase)
                    ? T("切换到紧凑面板", "Switch to compact panel")
                    : T("切换到详细面板", "Switch to detailed panel");
            if (key == "refreshAll") return T("手动刷新全部额度", "Refresh all quotas");
            if (key.EndsWith("-refresh")) return T("更新", "Refresh");
            if (key.EndsWith("-boost"))
            {
                ServiceState svc = key.StartsWith("codex") ? codex : claude;
                return svc.BoostActive ? T("停止高频刷新", "Stop boost") : T("开启高频刷新", "Boost (frequent refresh)");
            }
            if (key.EndsWith("-login")) return T("登录", "Login");
            return "";
        }

        bool ToggleBoost(ServiceState service)
        {
            if (service.BoostUntil.HasValue && service.BoostUntil.Value > DateTime.Now)
            {
                service.BoostUntil = null;
                Invalidate();
                return false;
            }
            service.BoostUntil = DateTime.Now.AddMinutes(Math.Max(1, settings.BoostDurationMinutes));
            Invalidate();
            return true;
        }

        string SilentHitKey(Point p)
        {
            foreach (var kv in silentHits)
                if (kv.Value.Contains(p)) return kv.Key;
            return "";
        }

        string HitKey(Point point)
        {
            foreach (var hit in hits)
            {
                if (hit.Value.Contains(point)) return hit.Key;
            }
            return "";
        }

        bool IsResizeGrip(Point p)
        {
            return p.X >= ClientSize.Width - 18 && p.Y >= ClientSize.Height - 18;
        }

        void UpdateSideRailVisibilityFromCursor()
        {
            Point p = PointToClient(Cursor.Position);
            bool visible = ClientRectangle.Contains(p);
            if (!visible)
            {
                if (!sideRailVisible && hoverKey.Length == 0) return;
                sideRailVisible = false;
                hoverKey = "";
                Cursor = Cursors.Default;
                toolTip.Hide(this);
                tooltipTimer.Stop();
                Invalidate();
                return;
            }

            if (!sideRailVisible)
                sideRailVisible = true;

            RegisterSideRailHits();
            string key = HitKey(p);
            string silentKey = SilentHitKey(p);
            Cursor = (key.Length > 0 || silentKey.Length > 0) ? Cursors.Hand : Cursors.Default;
            if (hoverKey == key) return;
            hoverKey = key;
            toolTip.Hide(this);
            tooltipTimer.Stop();
            pendingTooltipText = "";
            string tip = TooltipText(key);
            if (tip.Length > 0)
            {
                Rectangle hr;
                if (hits.TryGetValue(key, out hr))
                    pendingTooltipLocation = new Point(hr.X + hr.Width / 2, hr.Y + hr.Height + 4);
                else
                    pendingTooltipLocation = new Point(p.X + 12, p.Y + 18);
                pendingTooltipText = tip;
                tooltipTimer.Start();
            }
            Invalidate();
        }

        void UpdateSideRailOpacity()
        {
            sideRailOpacity = collapsedToBall ? 0.0 : 1.0;
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            CancelAutoCollapse();
            if (pendingSilentKey.Length > 0)
            {
                Point screenNow = PointToScreen(e.Location);
                int dx = screenNow.X - pendingSilentScreenStart.X;
                int dy = screenNow.Y - pendingSilentScreenStart.Y;
                if (!silentDragging && Math.Abs(dx) + Math.Abs(dy) > 4)
                    silentDragging = true;
                if (silentDragging)
                    Location = new Point(pendingSilentWindowStart.X + dx, pendingSilentWindowStart.Y + dy);
                return;
            }

            UpdateSideRailVisibilityFromCursor();
            RegisterSideRailHits();

            string key = HitKey(e.Location);
            string sk = SilentHitKey(e.Location);
            Cursor = (key.Length > 0 || sk.Length > 0) ? Cursors.Hand : Cursors.Default;
            if (hoverKey != key)
            {
                hoverKey = key;
                toolTip.Hide(this);
                tooltipTimer.Stop();
                pendingTooltipText = "";
                string tip = TooltipText(key);
                if (tip.Length > 0)
                {
                    Rectangle hr;
                    if (hits.TryGetValue(key, out hr))
                        pendingTooltipLocation = new Point(hr.X + hr.Width / 2, hr.Y + hr.Height + 4);
                    else
                        pendingTooltipLocation = new Point(e.X + 12, e.Y + 18);
                    pendingTooltipText = tip;
                    tooltipTimer.Start();
                }
                Invalidate();
            }
        }

        void ScheduleAutoCollapse()
        {
            if (!settings.CollapseToBall || autoCollapseSuspended || collapsedToBall || !Visible)
            {
                collapseDueAt = DateTime.MaxValue;
                return;
            }
            collapseDueAt = DateTime.Now.AddMilliseconds(
                Math.Max(300, Math.Min(5000, settings.CollapseDelayMilliseconds)));
        }

        void CancelAutoCollapse()
        {
            collapseDueAt = DateTime.MaxValue;
        }

        void UpdateAutoCollapseState()
        {
            if (!settings.CollapseToBall || autoCollapseSuspended || !Visible)
            {
                CancelAutoCollapse();
                if (collapsedToBall && !settings.CollapseToBall)
                    ExpandFromBall();
                return;
            }

            bool cursorInside = Bounds.Contains(Cursor.Position);
            if (collapsedToBall)
            {
                if (cursorInside) ExpandFromBall();
                return;
            }
            if (cursorInside)
            {
                CancelAutoCollapse();
                return;
            }
            if (collapseDueAt == DateTime.MaxValue)
            {
                ScheduleAutoCollapse();
                return;
            }
            if (DateTime.Now >= collapseDueAt)
                CollapseToQuotaBall();
        }

        void CollapseToQuotaBall()
        {
            if (collapsedToBall || !settings.CollapseToBall || autoCollapseSuspended) return;

            expandedSize = Size;
            expandedLocation = Location;
            Rectangle oldBounds = Bounds;
            Screen sourceScreen = Screen.FromRectangle(oldBounds);
            Rectangle workArea = sourceScreen.WorkingArea;
            collapsedDockEdge = settings.EdgeAutoHide
                ? NearestDockEdge(oldBounds, workArea, UiScale(16))
                : "";
            if (!IsOuterDockEdge(sourceScreen, collapsedDockEdge))
                collapsedDockEdge = "";
            collapsedScreenDeviceName = sourceScreen.DeviceName;
            collapsedWorkArea = workArea;

            int ballSize = UiScale(Math.Max(32, Math.Min(64, settings.BallSize)));
            int x = oldBounds.Left + (oldBounds.Width - ballSize) / 2;
            int y = oldBounds.Top + (oldBounds.Height - ballSize) / 2;
            int visiblePixels = Math.Max(UiScale(10), UiScale(12));
            if (collapsedDockEdge == "left") x = workArea.Left - ballSize + visiblePixels;
            else if (collapsedDockEdge == "right") x = workArea.Right - visiblePixels;
            else if (collapsedDockEdge == "top") y = workArea.Top - ballSize + visiblePixels;
            else if (collapsedDockEdge == "bottom") y = workArea.Bottom - visiblePixels;
            else
            {
                x = Math.Max(workArea.Left, Math.Min(workArea.Right - ballSize, x));
                y = Math.Max(workArea.Top, Math.Min(workArea.Bottom - ballSize, y));
            }

            collapsedToBall = true;
            MinimumSize = new Size(1, 1);
            Bounds = new Rectangle(x, y, ballSize, ballSize);
            CancelAutoCollapse();
            hoverKey = "";
            Invalidate();
        }

        void ExpandFromBall()
        {
            if (!collapsedToBall) return;

            Screen sourceScreen = null;
            foreach (Screen candidate in Screen.AllScreens)
            {
                if (string.Equals(candidate.DeviceName, collapsedScreenDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    sourceScreen = candidate;
                    break;
                }
            }
            if (sourceScreen == null)
                sourceScreen = Screen.FromRectangle(Bounds);
            Rectangle workArea = sourceScreen.WorkingArea;
            Size savedSize = SavedWidgetSizeForCurrentDpi();
            Size idealSize = IdealWidgetSize();
            Size targetSize = new Size(
                Math.Max(savedSize.Width, idealSize.Width),
                Math.Max(savedSize.Height, idealSize.Height));
            Point target = expandedLocation;

            if (collapsedDockEdge == "left") target.X = workArea.Left;
            else if (collapsedDockEdge == "right") target.X = workArea.Right - targetSize.Width;
            else if (collapsedDockEdge == "top") target.Y = workArea.Top;
            else if (collapsedDockEdge == "bottom") target.Y = workArea.Bottom - targetSize.Height;

            target.X = Math.Max(workArea.Left, Math.Min(workArea.Right - targetSize.Width, target.X));
            target.Y = Math.Max(workArea.Top, Math.Min(workArea.Bottom - targetSize.Height, target.Y));

            collapsedToBall = false;
            MinimumSize = idealSize;
            Size = targetSize;
            Location = target;
            settings.Width = WidgetLayoutMetrics.ToLogicalPixels(Width, runtimeDpi);
            settings.Height = WidgetLayoutMetrics.ToLogicalPixels(Height, runtimeDpi);
            expandedSize = targetSize;
            expandedLocation = target;
            collapsedDockEdge = "";
            collapsedScreenDeviceName = "";
            collapsedWorkArea = Rectangle.Empty;
            CancelAutoCollapse();
            Invalidate();
        }

        void ResizeCollapsedBallForCurrentDpi()
        {
            if (!collapsedToBall) return;

            Screen sourceScreen = null;
            foreach (Screen candidate in Screen.AllScreens)
            {
                if (string.Equals(candidate.DeviceName, collapsedScreenDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    sourceScreen = candidate;
                    break;
                }
            }
            if (sourceScreen == null)
                sourceScreen = Screen.FromRectangle(new Rectangle(expandedLocation, expandedSize));
            Rectangle workArea = sourceScreen.WorkingArea;
            if (!settings.EdgeAutoHide || !IsOuterDockEdge(sourceScreen, collapsedDockEdge))
                collapsedDockEdge = "";

            int ballSize = UiScale(Math.Max(32, Math.Min(64, settings.BallSize)));
            int x = Bounds.Left + (Bounds.Width - ballSize) / 2;
            int y = Bounds.Top + (Bounds.Height - ballSize) / 2;
            int visiblePixels = Math.Max(UiScale(10), UiScale(12));
            if (collapsedDockEdge == "left") x = workArea.Left - ballSize + visiblePixels;
            else if (collapsedDockEdge == "right") x = workArea.Right - visiblePixels;
            else if (collapsedDockEdge == "top") y = workArea.Top - ballSize + visiblePixels;
            else if (collapsedDockEdge == "bottom") y = workArea.Bottom - visiblePixels;
            else
            {
                x = Math.Max(workArea.Left, Math.Min(workArea.Right - ballSize, x));
                y = Math.Max(workArea.Top, Math.Min(workArea.Bottom - ballSize, y));
            }

            collapsedScreenDeviceName = sourceScreen.DeviceName;
            collapsedWorkArea = workArea;
            MinimumSize = new Size(1, 1);
            Bounds = new Rectangle(x, y, ballSize, ballSize);
            Invalidate();
        }

        static bool IsOuterDockEdge(Screen screen, string edge)
        {
            if (string.IsNullOrEmpty(edge)) return false;
            Rectangle bounds = screen.Bounds;
            Rectangle workArea = screen.WorkingArea;
            Rectangle virtualBounds = SystemInformation.VirtualScreen;
            if (edge == "left") return bounds.Left <= virtualBounds.Left && workArea.Left == bounds.Left;
            if (edge == "right") return bounds.Right >= virtualBounds.Right && workArea.Right == bounds.Right;
            if (edge == "top") return bounds.Top <= virtualBounds.Top && workArea.Top == bounds.Top;
            if (edge == "bottom") return bounds.Bottom >= virtualBounds.Bottom && workArea.Bottom == bounds.Bottom;
            return false;
        }

        static string NearestDockEdge(Rectangle bounds, Rectangle workArea, int threshold)
        {
            int left = Math.Abs(bounds.Left - workArea.Left);
            int right = Math.Abs(bounds.Right - workArea.Right);
            int top = Math.Abs(bounds.Top - workArea.Top);
            int bottom = Math.Abs(bounds.Bottom - workArea.Bottom);
            int nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            if (nearest > threshold) return "";
            if (nearest == left) return "left";
            if (nearest == right) return "right";
            if (nearest == top) return "top";
            return "bottom";
        }

        async Task ShowSettingsDialog()
        {
            autoCollapseSuspended = true;
            settingsPreviewActive = true;
            CancelAutoCollapse();
            if (collapsedToBall) ExpandFromBall();
            Size windowSizeBeforeSettings = Size;
            Point windowLocationBeforeSettings = Location;
            string prevLayout     = settings.LayoutMode;
            bool   prevShowCodex  = settings.ShowCodex;
            bool   prevShowClaude = settings.ShowClaude;

            try
            {
                using (var dlg = new SettingsForm(
                    settings,
                    () => { ApplyLayoutMinimumSize(); ApplyIdealSize(); TopMost = settings.AlwaysOnTop; Invalidate(); },
                    () => claude.Data.Status != "login_required",
                    () => codex.Data.Status != "login_required",
                    () => LogoutServiceAsync(claude),
                    () => LogoutServiceAsync(codex),
                    HeadroomOptions.FixtureMode
                ))
                {
                    dlg.ShowDialog(this);
                    if (dlg.DialogResult == DialogResult.OK)
                    {
                        if (dlg.ResetRequested)
                        {
                            settings.ResetToDefaults();
                            claude.ManuallyLoggedOut = settings.ClaudeLoggedOut;
                            codex.ManuallyLoggedOut = settings.CodexLoggedOut;
                        }

                        bool layoutChanged = settings.LayoutMode    != prevLayout     ||
                                            settings.ShowCodex      != prevShowCodex  ||
                                            settings.ShowClaude     != prevShowClaude;
                        ApplyLayoutMinimumSize();
                        if (layoutChanged || dlg.ResetRequested) ApplyIdealSize();
                        TopMost = settings.AlwaysOnTop;
                        settings.Save();
                        SetupTrayIcon();
                        if (dlg.ResetRequested) await RefreshAllAsync(true);
                    }
                    else
                    {
                        ApplyLayoutMinimumSize();
                        Size restoredSize = new Size(
                            Math.Max(windowSizeBeforeSettings.Width, MinimumSize.Width),
                            Math.Max(windowSizeBeforeSettings.Height, MinimumSize.Height));
                        Size = restoredSize;
                        Location = windowLocationBeforeSettings;
                        settings.Width = WidgetLayoutMetrics.ToLogicalPixels(Width, runtimeDpi);
                        settings.Height = WidgetLayoutMetrics.ToLogicalPixels(Height, runtimeDpi);
                        TopMost = settings.AlwaysOnTop;
                        settings.Save();
                        SetupTrayIcon();
                    }
                    if (dlg.LoginClaude) await OpenLoginAsync(claude);
                    if (dlg.LoginCodex)  await OpenLoginAsync(codex);
                }
            }
            finally
            {
                settingsPreviewActive = false;
                autoCollapseSuspended = false;
                ScheduleAutoCollapse();
                hoverKey = "";
                Invalidate();
            }
        }
    }
}
