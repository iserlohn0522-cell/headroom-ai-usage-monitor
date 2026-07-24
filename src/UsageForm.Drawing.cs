using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Headroom
{
    partial class UsageForm
    {
        delegate void IconPainter(Graphics g, Rectangle r, Color color);

        protected override void OnPaint(PaintEventArgs e)
        {
            RenderLayered();
        }

        void PaintContent(Graphics g)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            hits.Clear();
            silentHits.Clear();

            if (collapsedToBall)
            {
                DrawQuotaBall(g);
                return;
            }

            settings.CodexShowUsed = false;
            settings.ClaudeShowUsed = false;
            DrawCustomBatteryWidget(g);
            DrawSideRail(g);
        }

        List<Tuple<ServiceState, string>> VisibleServices()
        {
            var items = new List<Tuple<ServiceState, string>>();
            bool codexFirst = string.Equals(settings.ServiceOrder, "codex-claude", StringComparison.OrdinalIgnoreCase);
            if (codexFirst)
            {
                if (settings.ShowCodex) items.Add(Tuple.Create(codex, "codex"));
                if (settings.ShowClaude) items.Add(Tuple.Create(claude, "claude"));
            }
            else
            {
                if (settings.ShowClaude) items.Add(Tuple.Create(claude, "claude"));
                if (settings.ShowCodex) items.Add(Tuple.Create(codex, "codex"));
            }
            if (items.Count == 0) items.Add(Tuple.Create(claude, "claude"));
            return items;
        }

        int UiScale(int value)
        {
            return WidgetLayoutMetrics.Scale(value, settings, runtimeDpi);
        }

        int UiScaleTextColumn(int value)
        {
            int scaled = UiScale(value);
            int textScale = Math.Max(70, Math.Min(150, settings.TextScalePercent));
            return Math.Max(1, (int)Math.Round(scaled * textScale / 100.0));
        }

        int UiScaleTextRow(int value)
        {
            int scaled = UiScale(value);
            int textScale = Math.Max(100, Math.Min(150, settings.TextScalePercent));
            return Math.Max(1, (int)Math.Ceiling(scaled * textScale / 100.0));
        }

        Size IdealWidgetSize()
        {
            return WidgetLayoutMetrics.IdealSize(settings, runtimeDpi);
        }

        Size SavedWidgetSizeForCurrentDpi()
        {
            return new Size(
                WidgetLayoutMetrics.ScaleForDpi(Math.Max(1, settings.Width), runtimeDpi),
                WidgetLayoutMetrics.ScaleForDpi(Math.Max(1, settings.Height), runtimeDpi));
        }

        static Point ClampLocationToWorkArea(Point location, Size size, Rectangle workArea)
        {
            int maxX = Math.Max(workArea.Left, workArea.Right - size.Width);
            int maxY = Math.Max(workArea.Top, workArea.Bottom - size.Height);
            return new Point(
                Math.Max(workArea.Left, Math.Min(maxX, location.X)),
                Math.Max(workArea.Top, Math.Min(maxY, location.Y)));
        }

        void ApplyCurrentDpiLayout()
        {
            if (collapsedToBall)
            {
                ResizeCollapsedBallForCurrentDpi();
                return;
            }

            Size ideal = IdealWidgetSize();
            Size saved = SavedWidgetSizeForCurrentDpi();
            Size targetSize = new Size(
                Math.Max(ideal.Width, saved.Width),
                Math.Max(ideal.Height, saved.Height));
            Rectangle workArea = Screen.FromRectangle(Bounds).WorkingArea;
            Point target = ClampLocationToWorkArea(Location, targetSize, workArea);
            MinimumSize = ideal;
            Bounds = new Rectangle(target, targetSize);
            expandedSize = targetSize;
            expandedLocation = target;
        }

        void ApplyLayoutMinimumSize()
        {
            Size ideal = IdealWidgetSize();
            MinimumSize = ideal;
            if (Width < MinimumSize.Width) Width = MinimumSize.Width;
            if (Height < MinimumSize.Height) Height = MinimumSize.Height;
        }

        void ApplyIdealSize()
        {
            if (collapsedToBall) ExpandFromBall();
            Rectangle oldBounds = Bounds;
            Rectangle workArea = Screen.FromRectangle(oldBounds).WorkingArea;
            int anchorThreshold = UiScale(16);
            int leftDistance = Math.Abs(oldBounds.Left - workArea.Left);
            int rightDistance = Math.Abs(oldBounds.Right - workArea.Right);
            int topDistance = Math.Abs(oldBounds.Top - workArea.Top);
            int bottomDistance = Math.Abs(oldBounds.Bottom - workArea.Bottom);
            string horizontalAnchor = rightDistance <= anchorThreshold && rightDistance <= leftDistance
                ? "right"
                : (leftDistance <= anchorThreshold ? "left" : "");
            string verticalAnchor = bottomDistance <= anchorThreshold && bottomDistance <= topDistance
                ? "bottom"
                : (topDistance <= anchorThreshold ? "top" : "");

            Size ideal = IdealWidgetSize();
            MinimumSize = ideal;
            Point target = oldBounds.Location;
            if (horizontalAnchor == "right") target.X = workArea.Right - ideal.Width;
            else if (horizontalAnchor == "left") target.X = workArea.Left;
            if (verticalAnchor == "bottom") target.Y = workArea.Bottom - ideal.Height;
            else if (verticalAnchor == "top") target.Y = workArea.Top;
            target = ClampLocationToWorkArea(target, ideal, workArea);
            Bounds = new Rectangle(target, ideal);
            settings.Width = WidgetLayoutMetrics.ToLogicalPixels(ideal.Width, runtimeDpi);
            settings.Height = WidgetLayoutMetrics.ToLogicalPixels(ideal.Height, runtimeDpi);
            expandedSize = ideal;
            expandedLocation = target;
        }

        void DrawSideRail(Graphics g)
        {
            bool detailed = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase);
            int size = ActionButtonPixels();
            int gap = UiScale(4);
            int padding = UiScale(6);
            int columns = detailed ? 1 : 2;
            int rows = detailed ? 3 : 2;
            int totalWidth = size * columns + gap * (columns - 1);
            int totalHeight = size * rows + gap * (rows - 1);
            int x = ClientSize.Width - padding - totalWidth;
            int y = Math.Max(padding, (ClientSize.Height - totalHeight) / 2);

            DrawIconButton(g, "pin", new Rectangle(x, y, size, size),
                settings.AlwaysOnTop ? Color.FromArgb(120, 195, 255) : Color.FromArgb(156, 164, 177),
                DrawPinIcon);
            if (detailed)
            {
                y += size + gap;
                DrawIconButton(g, "widgetMode", new Rectangle(x, y, size, size),
                    Color.FromArgb(169, 188, 224), DrawModeActionIcon);
                y += size + gap;
                DrawIconButton(g, "refreshAll", new Rectangle(x, y, size, size),
                    Color.FromArgb(133, 215, 183), DrawRefreshActionIcon);
            }
            else
            {
                DrawIconButton(g, "widgetMode", new Rectangle(x + size + gap, y, size, size),
                    Color.FromArgb(169, 188, 224), DrawModeActionIcon);
                DrawIconButton(g, "refreshAll", new Rectangle(x + (totalWidth - size) / 2, y + size + gap, size, size),
                    Color.FromArgb(133, 215, 183), DrawRefreshActionIcon);
            }
        }

        void RegisterSideRailHits()
        {
            if (collapsedToBall) return;
            bool detailed = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase);
            int size = ActionButtonPixels();
            int gap = UiScale(4);
            int padding = UiScale(6);
            int columns = detailed ? 1 : 2;
            int rows = detailed ? 3 : 2;
            int totalWidth = size * columns + gap * (columns - 1);
            int totalHeight = size * rows + gap * (rows - 1);
            int x = ClientSize.Width - padding - totalWidth;
            int y = Math.Max(padding, (ClientSize.Height - totalHeight) / 2);
            hits["pin"] = new Rectangle(x, y, size, size);
            hits["widgetMode"] = detailed
                ? new Rectangle(x, y + size + gap, size, size)
                : new Rectangle(x + size + gap, y, size, size);
            hits["refreshAll"] = detailed
                ? new Rectangle(x, y + (size + gap) * 2, size, size)
                : new Rectangle(x + (totalWidth - size) / 2, y + size + gap, size, size);
        }

        int ActionButtonPixels()
        {
            return UiScale(Math.Max(24, Math.Min(40, settings.ActionButtonSize)));
        }

        void DrawIconButton(Graphics g, string key, Rectangle r, Color color, IconPainter painter)
        {
            hits[key] = r;
            bool hover = hoverKey == key;
            Color top = hover ? Color.FromArgb(66, 76, 91) : Color.FromArgb(39, 45, 54);
            Color bottom = hover ? Color.FromArgb(48, 58, 72) : Color.FromArgb(29, 34, 42);
            using (var path = RoundRect(r.X, r.Y, r.Width, r.Height, Math.Max(6, r.Width / 4)))
            using (var bg = new System.Drawing.Drawing2D.LinearGradientBrush(r, top, bottom, 90f))
            using (var border = new Pen(hover ? Color.FromArgb(100, 126, 160) : Color.FromArgb(58, 67, 80), 0.8f))
            {
                g.FillPath(bg, path);
                g.DrawPath(border, path);
            }

            int inset = Math.Max(4, r.Width / 5);
            var iconRect = Rectangle.Inflate(r, -inset, -inset);
            painter(g, iconRect, hover ? Color.White : color);
        }

        void DrawCustomBatteryWidget(Graphics g)
        {
            bool edge = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase);
            int radius = UiScale(8);
            using (var path = RoundRect(0, 0, Math.Max(1, ClientSize.Width - 1), Math.Max(1, ClientSize.Height - 1), radius))
            {
                Color top = edge ? Color.FromArgb(28, 31, 37) : Color.FromArgb(24, 27, 32);
                Color bottom = edge ? Color.FromArgb(18, 20, 24) : Color.FromArgb(17, 20, 24);
                using (var grad = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, top, bottom, 90f))
                    g.FillPath(grad, path);
                using (var border = new Pen(Color.FromArgb(65, 72, 82), 0.9f))
                    g.DrawPath(border, path);
            }

            int rowCount = CountBatteryRows();
            if (rowCount == 0) rowCount = 1;
            int pad = UiScale(edge ? 8 : 6);
            int buttonGap = UiScale(4);
            int actionSpace = (edge ? ActionButtonPixels() : ActionButtonPixels() * 2 + buttonGap) + UiScale(10);
            int rowGap = UiScale(edge ? 4 : 2);
            int rowH = UiScaleTextRow(edge ? 25 : 19);
            int rowW = Math.Max(UiScale(120), ClientSize.Width - pad * 2 - actionSpace);
            int contentHeight = rowCount * rowH + Math.Max(0, rowCount - 1) * rowGap;
            int y = Math.Max(pad, (ClientSize.Height - contentHeight) / 2);

            foreach (var item in VisibleServices())
            {
                ServiceState service = item.Item1;
                Color fiveColor = service.Name == "Codex" ? CodexFiveColor() : ClaudeFiveColor();
                Color weekColor = service.Name == "Codex" ? CodexWeekColor() : ClaudeWeekColor();
                DrawBatteryRow(g, "5h", service, false, pad, y, rowW, rowH,
                    BatteryColor(service.Data.FiveHourRemainingPercent(), fiveColor), edge);
                y += rowH + rowGap;
                DrawBatteryRow(g, "7d", service, true, pad, y, rowW, rowH,
                    BatteryColor(service.Data.WeeklyRemainingPercent(), weekColor), edge);
                y += rowH + rowGap;
            }
        }

        int CountBatteryRows()
        {
            int count = 0;
            if (settings.ShowCodex) count += 2;
            if (settings.ShowClaude) count += 2;
            return count;
        }

        Color CodexFiveColor() { return Color.FromArgb(132, 205, 252); }
        Color CodexWeekColor() { return Color.FromArgb(31, 101, 214); }
        Color ClaudeFiveColor() { return Color.FromArgb(215, 154, 101); }
        Color ClaudeWeekColor() { return Color.FromArgb(155, 90, 54); }

        void DrawQuotaBall(Graphics g)
        {
            hits["ball"] = ClientRectangle;
            int diameter = Math.Max(20, Math.Min(ClientSize.Width, ClientSize.Height) - 2);
            int x = (ClientSize.Width - diameter) / 2;
            int y = (ClientSize.Height - diameter) / 2;
            var circle = new Rectangle(x, y, diameter, diameter);

            using (var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0)))
                g.FillEllipse(shadow, x + 1, y + 2, diameter - 1, diameter - 1);
            using (var fill = new System.Drawing.Drawing2D.LinearGradientBrush(
                circle, Color.FromArgb(43, 49, 61), Color.FromArgb(19, 23, 30), 135f))
                g.FillEllipse(fill, circle);

            var quotas = new List<Tuple<int?, Color>>();
            if (settings.ShowCodex)
            {
                quotas.Add(Tuple.Create(codex.Data.FiveHourRemainingPercent(), CodexFiveColor()));
                quotas.Add(Tuple.Create(codex.Data.WeeklyRemainingPercent(), CodexWeekColor()));
            }
            if (settings.ShowClaude)
            {
                quotas.Add(Tuple.Create(claude.Data.FiveHourRemainingPercent(), ClaudeFiveColor()));
                quotas.Add(Tuple.Create(claude.Data.WeeklyRemainingPercent(), ClaudeWeekColor()));
            }
            if (quotas.Count == 0)
                quotas.Add(Tuple.Create<int?, Color>(null, Color.FromArgb(120, 130, 145)));

            float ringWidth = Math.Max(3.2f, diameter * 0.105f);
            float inset = ringWidth / 2f + 2f;
            var ringRect = new RectangleF(
                x + inset, y + inset,
                diameter - inset * 2f, diameter - inset * 2f);
            float sector = 360f / quotas.Count;
            float gap = Math.Min(11f, sector * 0.14f);
            float trackSpan = sector - gap;
            int total = 0;
            int known = 0;

            for (int i = 0; i < quotas.Count; i++)
            {
                float start = -90f + i * sector + gap / 2f;
                using (var track = new Pen(Color.FromArgb(78, 88, 101), ringWidth)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                })
                    g.DrawArc(track, ringRect, start, trackSpan);

                int? remaining = quotas[i].Item1;
                if (!remaining.HasValue) continue;
                int clamped = Math.Max(0, Math.Min(100, remaining.Value));
                total += clamped;
                known++;
                float amount = Math.Max(clamped == 0 ? 2.5f : 3f, trackSpan * clamped / 100f);
                Color quotaColor = BatteryColor(clamped, quotas[i].Item2);
                using (var quotaPen = new Pen(quotaColor, ringWidth)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                })
                    g.DrawArc(quotaPen, ringRect, start, Math.Min(trackSpan, amount));
            }

            string centerText = known > 0
                ? Math.Round(total / (double)known).ToString(CultureInfo.InvariantCulture)
                : "··";
            float fontSize = Math.Max(7f, diameter * 0.22f);
            using (var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                TextRenderer.DrawText(
                    g, centerText, font, circle,
                    Color.FromArgb(236, 241, 248),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            using (var highlight = new Pen(Color.FromArgb(52, 255, 255, 255), 1f))
                g.DrawArc(highlight, x + 3, y + 3, diameter - 7, diameter - 7, 205f, 105f);
        }

        Color BatteryColor(int? remaining, Color normal)
        {
            if (remaining.HasValue && remaining.Value <= settings.CriticalRemainingPercent)
                return Color.FromArgb(224, 73, 73);
            if (remaining.HasValue && remaining.Value <= settings.WarningRemainingPercent)
                return Color.FromArgb(232, 169, 36);
            return normal;
        }

        void DrawBatteryRow(Graphics g, string label, ServiceState service, bool weekly, int x, int y, int w, int h, Color color, bool edge)
        {
            int? remaining = weekly ? service.Data.WeeklyRemainingPercent() : service.Data.FiveHourRemainingPercent();
            double? displayed = weekly ? service.DisplayedWeekPct : service.DisplayedFivePct;
            string resetRaw = weekly ? service.Data.WeeklyReset : service.Data.FiveHourReset;
            string reset = edge ? CompactResetText(resetRaw, weekly) : "";
            if (!service.Data.HasAnyValue())
                reset = service.IsRefreshing ? T("更新中", "Updating") : StatusShortText(service.Status ?? service.Data.Status);

            int labelW = UiScaleTextColumn(edge ? 70 : 44);
            int pctW = UiScaleTextColumn(edge ? 40 : 34);
            int gap = UiScale(4);
            int barX = x + labelW;
            int barW = Math.Max(UiScale(64), w - labelW - pctW - gap);
            string pct = remaining.HasValue ? remaining.Value.ToString(CultureInfo.InvariantCulture) + "%" : "--";
            string serviceLabel = edge
                ? service.Name + " " + label
                : (service.Name == "Codex" ? "CX " : "CL ") + label;
            float layoutScale = Math.Max(0.7f, Math.Min(1.5f, settings.OverallScalePercent / 100f));
            float textScale = Math.Max(0.7f, Math.Min(1.5f, settings.TextScalePercent / 100f));
            float dpiScale = Math.Max(0.5f, runtimeDpi / 96f);
            float fontScale = layoutScale * textScale * dpiScale;

            using (var labelFont = new Font(UiFontName, (edge ? 11.2f : 10f) * fontScale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var pctFont = new Font("Segoe UI", (edge ? 11.5f : 10.4f) * fontScale, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var resetFont = new Font(UiFontName, (edge ? 10.1f : 9.1f) * fontScale, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                int desiredBarH = Math.Max(
                    UiScale(Math.Max(8, Math.Min(24, settings.BarHeight))),
                    resetFont.Height + UiScale(1));
                int barH = Math.Max(UiScale(6), Math.Min(h - UiScale(3), desiredBarH));
                int barY = y + (h - barH) / 2;
                TextRenderer.DrawText(g, serviceLabel, labelFont, new Rectangle(x, y, labelW, h), Color.FromArgb(196, 204, 214), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                DrawBatteryBar(g, barX, barY, barW, barH, displayed.HasValue ? displayed : remaining, color, reset, resetFont);
                TextRenderer.DrawText(g, pct, pctFont, new Rectangle(barX + barW + gap, y, pctW, h), Color.FromArgb(232, 238, 245), TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding);
            }
        }

        string CompactResetText(string raw, bool weekly)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            string mode = weekly ? settings.WeeklyResetMode : settings.FiveHourResetMode;
            string text = ResetText(raw, mode, English, weekly);
            return text.Replace("Reset in ", "")
                       .Replace("Reset ", "")
                       .Replace("距重置 ", "")
                       .Replace("重置 ", "")
                       .Replace("リセットまで ", "")
                       .Replace("リセット ", "")
                       .Trim();
        }

        string StatusShortText(string status)
        {
            switch (status)
            {
                case "login_required": return T("请登录", "Login");
                case "login_pending": return T("登录中", "Signing in");
                case "rate_limited": return T("请稍候", "Wait");
                case "fetch_error": return T("错误", "Error");
                case "no_data": return T("无数据", "No data");
                case "no_usage_text": return T("无额度", "No quota");
                case "fixture_missing": return T("缺少数据", "Missing data");
                case "starting": return T("启动中", "Starting");
                case "updating": return T("更新中", "Updating");
                default: return string.IsNullOrWhiteSpace(status) ? T("无数据", "No data") : status;
            }
        }

        void DrawBatteryBar(Graphics g, int x, int y, int w, int h, double? pct, Color color, string reset, Font resetFont)
        {
            int inset = UiScale(2);
            int nubWidth = UiScale(3);
            using (var outline = RoundRect(x, y, w, h, UiScale(5)))
            using (var bg = new SolidBrush(Color.FromArgb(16, 19, 23)))
            using (var pen = new Pen(Color.FromArgb(165, color), 1.4f))
            {
                g.FillPath(bg, outline);
                g.DrawPath(pen, outline);
            }

            using (var nub = new SolidBrush(Color.FromArgb(165, color)))
                g.FillRectangle(nub, x + w, y + Math.Max(UiScale(2), h / 3), nubWidth, Math.Max(UiScale(4), h / 3));

            if (pct.HasValue)
            {
                double clamped = Math.Max(0, Math.Min(100, pct.Value));
                int fillArea = Math.Max(1, w - inset * 2);
                int fillW = Math.Max(
                    clamped <= 0 ? Math.Min(UiScale(3), fillArea) : UiScale(4),
                    (int)Math.Round(fillArea * clamped / 100.0));
                if (fillW > 0)
                {
                    using (var fillPath = RoundRect(x + inset, y + inset, fillW, Math.Max(1, h - inset * 2), UiScale(3)))
                    using (var fill = new SolidBrush(Color.FromArgb(220, color)))
                        g.FillPath(fill, fillPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(reset))
            {
                Color resetColor = Color.FromArgb(226, 238, 242, 248);
                TextRenderer.DrawText(g, reset, resetFont, new Rectangle(x + 6, y, w - 12, h), resetColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        void DrawService(Graphics g, ServiceState state, int x, int y, int w, int h, string keyPrefix)
        {
            bool showUsed = state.Name == "Codex" ? settings.CodexShowUsed : settings.ClaudeShowUsed;
            int? fiveRemain = state.Data.FiveHourRemainingPercent();
            int? weekRemain = state.Data.WeeklyRemainingPercent();
            bool quotaExhausted = (fiveRemain.HasValue && fiveRemain.Value <= 0) || (weekRemain.HasValue && weekRemain.Value <= 0);
            bool exhausted = quotaExhausted || (state.Data.HitLimit && !fiveRemain.HasValue && !weekRemain.HasValue);
            bool stale = state.LastRefresh != DateTime.MinValue &&
                DateTime.Now - state.LastRefresh > TimeSpan.FromMinutes(Math.Max(2, settings.NormalIntervalMinutes * 2));
            Color cardAccent = exhausted ? Color.FromArgb(220, 77, 77) : state.Accent;

            using (var path = RoundRect(x, y, w, h, 12))
            {
                Color topColor = exhausted ? Color.FromArgb(42, 22, 22) : (stale ? Color.FromArgb(38, 34, 22) : Color.FromArgb(30, 30, 34));
                Color bottomColor = exhausted ? Color.FromArgb(32, 18, 18) : (stale ? Color.FromArgb(30, 28, 18) : Color.FromArgb(22, 22, 26));
                using (var grad = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(x, y, w, h), topColor, bottomColor, 90f))
                    g.FillPath(grad, path);
                using (var highlight = new Pen(Color.FromArgb(exhausted ? 18 : 28, 255, 255, 255)))
                    g.DrawPath(highlight, path);
            }
            using (var path = RoundRect(x, y, w, h, 12))
            using (var border = new Pen(exhausted ? Color.FromArgb(80, 40, 40) : (stale ? Color.FromArgb(80, 68, 30) : Color.FromArgb(48, 48, 54)), 0.8f))
                g.DrawPath(border, path);

            using (var dotBrush = new SolidBrush(cardAccent))
                g.FillEllipse(dotBrush, x + 16, y + 17, 8, 8);

            using (var title = new Font("Segoe UI", 13f, FontStyle.Bold))
            using (var label = new Font("Segoe UI", LabelFontSize, FontStyle.Regular))
            using (var reset = new Font("Segoe UI", ResetFontSize, FontStyle.Regular))
            using (var num = new Font("Segoe UI", PercentFontSize, FontStyle.Bold))
            using (var white = new SolidBrush(Color.FromArgb(240, 242, 245)))
            using (var muted = new SolidBrush(Color.FromArgb(210, 215, 228)))
            using (var dim = new SolidBrush(Color.FromArgb(185, 190, 205)))
            {
                g.DrawString(state.Name, title, white, x + 30, y + 10);
                int badgeX = x + 100;
                if (stale)
                    badgeX += DrawBadge(g, T("过期", "Stale"), badgeX, y + 14, Color.FromArgb(110, 85, 20), Color.FromArgb(180, 150, 50)) + 6;
                if (exhausted)
                    badgeX += DrawBadge(g, T("已达上限", "Limit"), badgeX, y + 14, Color.FromArgb(100, 35, 35), Color.FromArgb(220, 100, 100)) + 6;
                if (state.Status == "fetch_error")
                    badgeX += DrawBadge(g, T("错误", "Error"), badgeX, y + 14, Color.FromArgb(90, 62, 28), Color.FromArgb(235, 170, 70)) + 6;
                if (state.Status == "rate_limited")
                    DrawBadge(g, T("等待", "Wait"), badgeX, y + 14, Color.FromArgb(80, 64, 28), Color.FromArgb(220, 185, 80));
                DrawCardControls(g, state, x, y, w, keyPrefix);

                if (!state.Data.HasAnyValue())
                {
                    g.DrawString("--", num, white, x + 58, y + 54);
                    string effectiveStatus = state.Status ?? state.Data.Status ?? "no_data";
                    string status = state.IsRefreshing ? T("更新中", "Updating") : StatusText(effectiveStatus);
                    g.DrawString(status, label, muted, x + 20, y + 88);
                    if (NeedsLogin(effectiveStatus))
                        DrawLoginButton(g, x + w - 100, y + h - 40, keyPrefix + "-login");
                    return;
                }

                int contentTop = y + Math.Max(44, (int)Math.Ceiling(PercentFontSize + 24));
                int contentBottom = y + h - 10;
                int rowHeight = Math.Max(32, (int)Math.Ceiling(Math.Max(PercentFontSize * 1.55, LabelFontSize + ResetFontSize + 14)));
                int totalRows = rowHeight * 2;
                int free = Math.Max(0, contentBottom - contentTop - totalRows);
                int topPad = free / 2;
                int rowGap = Math.Max(2, Math.Min(10, free / 4));
                int firstY = contentTop + topPad;
                int secondY = firstY + rowHeight + rowGap;
                bool fiveLockedByWeekly = weekRemain.HasValue && weekRemain.Value <= 0;
                Color fiveAccent = fiveLockedByWeekly ? Color.FromArgb(58, 60, 68) : state.Accent;
                bool resetTrackingAllowed = state.Status != "rate_limited" && !state.ManuallyLoggedOut;
                DateTime now = DateTime.Now;
                bool fiveResetTracking = resetTrackingAllowed && RefreshPolicy.IsResetDueOrPast(state.Data.FiveHourReset, now);
                bool weekResetTracking = resetTrackingAllowed && RefreshPolicy.IsResetDueOrPast(state.Data.WeeklyReset, now);
                DrawRow(g, T("5小时", "5h"), state.Data.FiveHourDisplayPercent(showUsed), state.DisplayedFivePct, showUsed, false, state.Data.FiveHourReset, state.Data.FiveHourNotStarted, settings.FiveHourResetMode, x, firstY, w, fiveAccent, label, reset, num, white, muted, dim, keyPrefix + "-fiveMode", keyPrefix + "-fiveResetLabel", fiveLockedByWeekly, fiveResetTracking);
                DrawRow(g, T("1周", "1W"), state.Data.WeeklyDisplayPercent(showUsed), state.DisplayedWeekPct, showUsed, true, state.Data.WeeklyReset, state.Data.WeeklyNotStarted, settings.WeeklyResetMode, x, secondY, w, state.Accent, label, reset, num, white, muted, dim, keyPrefix + "-weekMode", keyPrefix + "-weekResetLabel", false, weekResetTracking);
            }
        }

        void DrawCardControls(Graphics g, ServiceState state, int x, int y, int w, string keyPrefix)
        {
            int refreshX = x + w - 34;
            int controlY = y + 12;
            int boostX = refreshX - 28;
            var boostText = BoostText(state);
            int boostTextW = boostText.Length > 0 ? 52 : 0;
            int boostTextX = boostX - boostTextW - 4;

            hits[keyPrefix + "-refresh"] = new Rectangle(refreshX - 4, controlY - 3, 30, 28);
            hits[keyPrefix + "-boost"] = new Rectangle(boostX - 4, controlY - 3, 28, 28);

            using (var small = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var textBrush = new SolidBrush(Color.FromArgb(140, 150, 165)))
            {
                if (boostText.Length > 0)
                    g.DrawString(boostText, small, textBrush, boostTextX, controlY + 3);
            }

            DrawBoostIcon(g, boostX, controlY, state.BoostActive, hoverKey == keyPrefix + "-boost");
            DrawRefreshIcon(g, refreshX, controlY, keyPrefix + "-refresh", state.IsRefreshing);
        }

        string StatusText(string status)
        {
            switch (status)
            {
                case "updating": return T("更新中", "Updating");
                case "rate_limited": return T("触发限流，请稍候", "Rate limited");
                case "fetch_error": return T("暂时无法连接 API", "Temporarily unreachable");
                case "no_data": return T("无数据", "No data");
                case "login_required": return T("请登录", "Please log in");
                case "login_pending": return T("登录中…请在浏览器完成", "Signing in… complete it in your browser");
                case "fixture_missing": return T("缺少测试数据", "Fixture missing");
                case "no_usage_text": return T("无额度文本", "No usage text");
                case "starting": return T("启动中", "Starting");
                default: return status ?? T("无数据", "No data");
            }
        }

        static bool NeedsLogin(string status)
        {
            return status == "login_required" || status == "login_pending";
        }

        string LimitResetText(UsageData data, int? fiveRemain, int? weekRemain, bool english)
        {
            string raw = "";
            string mode = settings.FiveHourResetMode;
            if (fiveRemain.HasValue && fiveRemain.Value <= 0) raw = data.FiveHourReset;
            else if (weekRemain.HasValue && weekRemain.Value <= 0)
            {
                raw = data.WeeklyReset;
                mode = settings.WeeklyResetMode;
            }
            string text = ResetText(raw, mode, english);
            return english ? text.Replace("Reset in ", "in ") : text.Replace("距重置 ", "还有 ");
        }

        static bool TryGetResetRemaining(string raw, out TimeSpan remaining)
        {
            return TryGetResetRemaining(raw, true, out remaining);
        }

        static bool TryGetResetRemaining(string raw, bool rollTimeOnlyToTomorrow, out TimeSpan remaining)
        {
            remaining = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return ResetTimes.TryGetRemaining(raw, DateTime.Now, rollTimeOnlyToTomorrow, out remaining);
        }

        string BoostText(ServiceState state)
        {
            if (!state.BoostUntil.HasValue || state.BoostUntil.Value <= DateTime.Now) return "";
            int min = Math.Max(1, (int)Math.Ceiling((state.BoostUntil.Value - DateTime.Now).TotalMinutes));
            return English ? min + "m left" : "剩余" + min + "分钟";
        }

        void DrawRow(Graphics g, string label, int? pct, double? barPct, bool showUsed, bool weekly, string resetText, bool notStarted, string resetMode, int x, int y, int w, Color accent, Font labelFont, Font resetFont, Font numFont, Brush white, Brush muted, Brush dim, string hitMode = null, string hitReset = null, bool disabled = false, bool resetTracking = false)
        {
            bool empty = !pct.HasValue;
            bool atLimit = pct.HasValue && (showUsed ? pct.Value >= 100 : pct.Value <= 0);
            Color rowColor = disabled ? Color.FromArgb(88, 92, 104) : RowColor(pct, showUsed, accent);
            Color numColor = disabled ? Color.FromArgb(130, 136, 150) : Color.FromArgb(240, 242, 248);
            Color labelColor = disabled ? Color.FromArgb(120, 126, 140) : Color.FromArgb(210, 215, 228);
            Color dimColor = disabled ? Color.FromArgb(105, 111, 124) : Color.FromArgb(185, 190, 205);
            using (var pctBrush = new SolidBrush(numColor))
            using (var labelBrush = new SolidBrush(labelColor))
            using (var dimBrush = new SolidBrush(dimColor))
            using (var resetTrackingBrush = new SolidBrush(Color.FromArgb(245, 196, 70)))
            {
                int labelX = x + 12;
                int label5hW = (int)Math.Ceiling(g.MeasureString(T("5小时", "5h"), labelFont).Width);
                int labelWkW = (int)Math.Ceiling(g.MeasureString(T("1周", "1W"), labelFont).Width);
                int maxLabelW = Math.Max(label5hW, labelWkW);
                string modeText = showUsed ? T("已用", "Used") : T("剩余", "Rem");
                int modeColW = Math.Max(
                    (int)Math.Ceiling(g.MeasureString(T("已用", "Used"), labelFont).Width),
                    (int)Math.Ceiling(g.MeasureString(T("剩余", "Rem"), labelFont).Width));
                int modeX = labelX + maxLabelW + 1;
                int percentX = modeX + modeColW + 2;
                int labelY = y + Math.Max(0, (int)Math.Round((numFont.Size - labelFont.Size) / 2.0));
                int actualLabelW = (int)Math.Ceiling(g.MeasureString(label, labelFont).Width);
                g.DrawString(label, labelFont, labelBrush, labelX + maxLabelW - actualLabelW, labelY);
                if (hitMode != null)
                    silentHits[hitMode] = new Rectangle(modeX - 2, labelY - 2, modeColW + 4, labelFont.Height + 4);
                g.DrawString(modeText, labelFont, dimBrush, modeX, labelY);
                string pctDigits = empty ? "--" : pct.Value.ToString(CultureInfo.InvariantCulture);
                int digitColW = Math.Max(34, (int)Math.Ceiling(g.MeasureString("100", numFont).Width));
                int pctSignW = Math.Max(10, (int)Math.Ceiling(g.MeasureString("%", numFont).Width));
                int pctColW = digitColW + pctSignW + 2;
                using (var pctFormat = new StringFormat())
                {
                    pctFormat.Alignment = StringAlignment.Far;
                    pctFormat.LineAlignment = StringAlignment.Near;
                    pctFormat.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(pctDigits, numFont, pctBrush, new RectangleF(percentX, y - 4, digitColW, numFont.Height + 4), pctFormat);
                    if (!empty)
                        g.DrawString("%", numFont, pctBrush, percentX + digitColW + 2, y - 4);
                }
                int barX = percentX + pctColW + 4;
                int barY = y + Math.Max(5, (int)Math.Round(PercentFontSize * 0.42));
                int barW = Math.Max(70, w - (barX - x) - 14);
                DrawBar(g, barX, barY, barW, 9, barPct, rowColor);

                string reset = notStarted ? T("尚未开始", "Not started") : ResetText(resetText, resetMode, English, weekly);
                if (!string.IsNullOrEmpty(reset))
                {
                    int resetY = y + Math.Max(18, (int)Math.Round(PercentFontSize * 1.0));
                    if (hitReset != null)
                    {
                        SizeF resetSz = g.MeasureString(reset, resetFont);
                        silentHits[hitReset] = new Rectangle(barX - 2, resetY - 2, (int)Math.Ceiling(resetSz.Width) + 4, (int)Math.Ceiling(resetSz.Height) + 4);
                    }
                    if (resetTracking && !disabled)
                        using (var boldReset = new Font(resetFont, FontStyle.Bold))
                            g.DrawString(reset, boldReset, resetTrackingBrush, barX, resetY);
                    else if (atLimit && !disabled)
                        using (var boldReset = new Font(resetFont, FontStyle.Bold))
                            g.DrawString(reset, boldReset, white, barX, resetY);
                    else
                        g.DrawString(reset, resetFont, dimBrush, barX, resetY);
                }
            }
        }

        Color RowColor(int? pct, bool showUsed, Color normal)
        {
            if (!pct.HasValue) return normal;
            int remaining = showUsed ? 100 - pct.Value : pct.Value;
            if (remaining <= settings.CriticalRemainingPercent)
                return Color.FromArgb(224, 73, 73);
            if (remaining <= settings.WarningRemainingPercent)
                return Color.FromArgb(232, 169, 36);
            return normal;
        }

        static string ResetText(string raw)
        {
            return ResetText(raw, false, false);
        }

        static string ResetText(string raw, string mode, bool english)
        {
            return ResetText(raw, string.Equals(mode, "time", StringComparison.OrdinalIgnoreCase), english);
        }

        static string ResetText(string raw, string mode, bool english, bool weekly)
        {
            return ResetText(raw, string.Equals(mode, "time", StringComparison.OrdinalIgnoreCase), english, weekly);
        }

        static string ResetText(string raw, bool preferAbsolute, bool english)
        {
            return ResetText(raw, preferAbsolute, english, true);
        }

        static string ResetText(string raw, bool preferAbsolute, bool english, bool weekly)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var lower = raw.ToLowerInvariant();
            string cleaned = ResetTimes.Clean(raw);
            if (cleaned.Contains("リセットまで"))
                return english
                    ? cleaned.Replace("リセットまで", "Reset in")
                    : cleaned.Replace("リセットまで", "距重置");
            var relative = RelativeResetText(cleaned, preferAbsolute, english, weekly);
            if (!string.IsNullOrEmpty(relative)) return relative;

            DateTime target;
            if (TryParseResetTarget(cleaned, weekly, out target))
            {
                if (preferAbsolute) return (english ? "Reset " : "重置 ") + FormatResetTime(target, english);
                return (english ? "Reset in " : "距重置 ") + FormatDuration(target - DateTime.Now, english);
            }
            if (!weekly && IsTimeOnly(cleaned)) return "";

            if (cleaned.Contains("後にリセット"))
            {
                var s = cleaned.Replace("後にリセット", "").Replace("リセット", "").Trim();
                return english ? "Reset in " + TranslateDurationText(s) : "距重置 " + TranslateDurationTextToChinese(s);
            }
            if (lower.Contains("reset"))
            {
                var m = Regex.Match(cleaned, @"(\d{1,2}/\d{1,2}\s+\d{1,2}:\d{2})");
                if (m.Success) return (english ? "Reset " : "重置 ") + m.Groups[1].Value;
                return cleaned;
            }
            return (english ? "Reset " : "重置 ") + cleaned;
        }

        static string RelativeResetText(string text, bool preferAbsolute, bool english)
        {
            return RelativeResetText(text, preferAbsolute, english, true);
        }

        static string RelativeResetText(string text, bool preferAbsolute, bool english, bool weekly)
        {
            var m = Regex.Match(text, @"(?:(\d+)\s*時間)?\s*(?:(\d+)\s*分)?\s*後にリセット");
            if (!m.Success) return "";
            int hours = m.Groups[1].Success ? int.Parse(m.Groups[1].Value) : 0;
            int minutes = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            var span = new TimeSpan(hours, minutes, 0);
            if (preferAbsolute) return (english ? "Reset " : "重置 ") + FormatResetTime(DateTime.Now.Add(span), english);
            return (english ? "Reset in " : "距重置 ") + FormatDuration(span, english);
        }

        static bool IsTimeOnly(string text)
        {
            return Regex.IsMatch(text.Trim(), @"^\d{1,2}:\d{2}$");
        }

        static bool TryParseResetTarget(string text, out DateTime target)
        {
            return TryParseResetTarget(text, true, out target);
        }

        static bool TryParseResetTarget(string text, bool rollTimeOnlyToTomorrow, out DateTime target)
        {
            return ResetTimes.TryParseTarget(text, DateTime.Now, rollTimeOnlyToTomorrow, out target);
        }

        static string FormatDuration(TimeSpan span)
        {
            return FormatDuration(span, false);
        }

        static string FormatDuration(TimeSpan span, bool english)
        {
            if (span.TotalSeconds <= 0) return english ? "0m" : "0分钟";
            int totalMinutes = Math.Max(0, (int)Math.Ceiling(span.TotalMinutes));
            int days = totalMinutes / (60 * 24);
            int hours = (totalMinutes % (60 * 24)) / 60;
            int minutes = totalMinutes % 60;
            if (english)
            {
                if (days > 0) return days + "d " + hours + "h " + minutes + "m";
                if (hours > 0) return hours + "h " + minutes + "m";
                return minutes + "m";
            }
            if (days > 0) return days + "天 " + hours + "小时 " + minutes + "分钟";
            if (hours > 0) return hours + "小时 " + minutes + "分钟";
            return minutes + "分钟";
        }

        static string TranslateDurationText(string text)
        {
            return text.Replace("時間", "h ").Replace("分", "m").Trim();
        }

        static string TranslateDurationTextToChinese(string text)
        {
            return text.Replace("時間", "小时").Replace("分", "分钟").Trim();
        }

        static string FormatResetTime(DateTime value, bool english)
        {
            var today = DateTime.Now.Date;
            if (value.Date == today) return value.ToString("H:mm");
            if (value.Date == today.AddDays(1)) return (english ? "Tomorrow " : "明天 ") + value.ToString("H:mm");
            return value.ToString("M/d H:mm");
        }

        void DrawLoginButton(Graphics g, int x, int y, string key)
        {
            int buttonW = English ? 86 : 80;
            int buttonH = 28;
            hits[key] = new Rectangle(x, y, buttonW, buttonH);
            bool hover = hoverKey == key;
            using (var p = RoundRect(x, y, buttonW, buttonH, 10))
            {
                Color top = hover ? Color.FromArgb(72, 72, 78) : Color.FromArgb(52, 52, 58);
                Color bottom = hover ? Color.FromArgb(58, 58, 64) : Color.FromArgb(40, 40, 46);
                using (var grad = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(x, y, buttonW, buttonH), top, bottom, 90f))
                    g.FillPath(grad, p);
                using (var border = new Pen(Color.FromArgb(hover ? 90 : 65, 90, 100), 0.8f))
                    g.DrawPath(border, p);
            }
            using (var f = new Font("Segoe UI", 9.2f, FontStyle.Bold))
                TextRenderer.DrawText(g, T("登录", "Login"), f,
                    new Rectangle(x, y, buttonW, buttonH),
                    Color.FromArgb(235, 238, 242),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        int DrawBadge(Graphics g, string text, int x, int y, Color bgColor, Color textColor)
        {
            int badgeW = 32;
            using (var f = new Font("Segoe UI", 8.2f, FontStyle.Bold))
            {
                badgeW = Math.Max(32, (int)Math.Ceiling(g.MeasureString(text, f).Width) + 16);
                using (var path = RoundRect(x, y, badgeW, 20, 10))
                {
                    using (var bg = new SolidBrush(bgColor))
                        g.FillPath(bg, path);
                    using (var border = new Pen(Color.FromArgb(40, textColor.R, textColor.G, textColor.B), 0.6f))
                        g.DrawPath(border, path);
                }
                using (var b = new SolidBrush(textColor))
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    sf.FormatFlags = StringFormatFlags.NoWrap;
                    g.DrawString(text, f, b, new RectangleF(x, y, badgeW, 20), sf);
                }
            }
            return badgeW;
        }

        void DrawServiceMark(Graphics g, string name, int x, int y, Color accent)
        {
            using (var b = new SolidBrush(Color.FromArgb(42, 42, 44)))
            using (var p = RoundRect(x - 9, y - 9, 18, 18, 5))
                g.FillPath(b, p);
            using (var pen = new Pen(accent, 1.8f))
            {
                if (name == "Codex")
                {
                    g.DrawLine(pen, x - 4, y - 4, x + 1, y);
                    g.DrawLine(pen, x + 1, y, x - 4, y + 4);
                    g.DrawLine(pen, x + 3, y + 5, x + 8, y + 5);
                }
                else
                {
                    g.DrawLine(pen, x, y - 7, x + 3, y - 1);
                    g.DrawLine(pen, x + 3, y - 1, x + 8, y);
                    g.DrawLine(pen, x + 8, y, x + 3, y + 1);
                    g.DrawLine(pen, x + 3, y + 1, x, y + 7);
                    g.DrawLine(pen, x, y + 7, x - 3, y + 1);
                    g.DrawLine(pen, x - 3, y + 1, x - 8, y);
                    g.DrawLine(pen, x - 8, y, x - 3, y - 1);
                    g.DrawLine(pen, x - 3, y - 1, x, y - 7);
                }
            }
        }

        void DrawRefreshIcon(Graphics g, int x, int y, string key, bool active)
        {
            var r = new Rectangle(x - 2, y - 2, 24, 24);
            if (hoverKey == key && !active)
            {
                using (var bg = new SolidBrush(Color.FromArgb(48, 48, 54)))
                using (var path = RoundRect(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2, 12))
                    g.FillPath(bg, path);
            }

            string[] frames = { "◜", "◝", "◞", "◟" };
            string s = active ? frames[spinnerFrame] : "↻";
            Color iconColor = active ? Color.FromArgb(100, 180, 255) : Color.FromArgb(200, 205, 212);
            using (var f = new Font("Segoe UI Symbol", active ? 13f : 12.5f, FontStyle.Regular))
            using (var b = new SolidBrush(iconColor))
                g.DrawString(s, f, b, x + (active ? 2 : 1), y + (active ? 0 : -1));
        }

        void DrawBoostIcon(Graphics g, int x, int y, bool on, bool hover)
        {
            var r = new Rectangle(x - 2, y - 2, 24, 24);
            if (hover)
            {
                using (var bg = new SolidBrush(Color.FromArgb(48, 48, 54)))
                using (var path = RoundRect(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2, 12))
                    g.FillPath(bg, path);
            }
            if (on)
            {
                using (var glow = new SolidBrush(Color.FromArgb(30, 255, 210, 50)))
                    g.FillEllipse(glow, x - 1, y - 1, 22, 22);
            }
            int cx = x + 9;
            int cy = y + 10;
            var bolt = new Point[]
            {
                new Point(cx + 1, cy - 8),
                new Point(cx - 3, cy + 1),
                new Point(cx + 1, cy + 1),
                new Point(cx - 1, cy + 8),
                new Point(cx + 3, cy - 1),
                new Point(cx - 1, cy - 1),
            };
            Color boltColor = on ? Color.FromArgb(255, 220, 60) : (hover ? Color.FromArgb(225, 228, 232) : Color.FromArgb(200, 205, 212));
            using (var brush = new SolidBrush(boltColor))
                g.FillPolygon(brush, bolt);
            using (var pen = new Pen(on ? Color.FromArgb(200, 170, 30) : Color.FromArgb(100, 105, 112), 0.6f))
                g.DrawPolygon(pen, bolt);
        }

        void DrawCloseIcon(Graphics g, Rectangle r, Color color)
        {
            using (var pen = new Pen(color, 1.6f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            {
                g.DrawLine(pen, r.X + 6, r.Y + 6, r.X + 14, r.Y + 14);
                g.DrawLine(pen, r.X + 14, r.Y + 6, r.X + 6, r.Y + 14);
            }
        }

        void DrawPinIcon(Graphics g, Rectangle r, Color color)
        {
            float cx = r.Left + r.Width / 2f;
            float top = r.Top + r.Height * 0.12f;
            float shoulder = r.Top + r.Height * 0.48f;
            float bottom = r.Bottom - r.Height * 0.08f;
            float half = r.Width * 0.28f;
            using (var pen = new Pen(color, Math.Max(1.5f, r.Width * 0.09f))
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            })
            {
                g.DrawRectangle(pen, cx - half, top, half * 2f, shoulder - top);
                g.DrawLine(pen, cx - half * 1.25f, shoulder, cx + half * 1.25f, shoulder);
                g.DrawLine(pen, cx, shoulder, cx, bottom);
            }
        }

        void DrawModeActionIcon(Graphics g, Rectangle r, Color color)
        {
            bool detailed = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase);
            float stroke = Math.Max(1.4f, r.Width * 0.085f);
            using (var pen = new Pen(color, stroke)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            })
            {
                float left = r.Left + 1;
                float top = r.Top + 1;
                float right = r.Right - 1;
                float bottom = r.Bottom - 1;
                float cx = r.Left + r.Width / 2f;
                float cy = r.Top + r.Height / 2f;
                float head = Math.Max(2f, r.Width * 0.18f);
                if (detailed)
                {
                    float innerA = cx - 1.5f;
                    float innerB = cx + 1.5f;
                    g.DrawLine(pen, left, top, innerA, cy - 1.5f);
                    g.DrawLine(pen, innerA, cy - 1.5f, innerA - head, cy - 1.5f);
                    g.DrawLine(pen, innerA, cy - 1.5f, innerA, cy - 1.5f - head);
                    g.DrawLine(pen, right, bottom, innerB, cy + 1.5f);
                    g.DrawLine(pen, innerB, cy + 1.5f, innerB + head, cy + 1.5f);
                    g.DrawLine(pen, innerB, cy + 1.5f, innerB, cy + 1.5f + head);
                }
                else
                {
                    g.DrawLine(pen, cx - 1.5f, cy - 1.5f, left, top);
                    g.DrawLine(pen, left, top, left + head, top);
                    g.DrawLine(pen, left, top, left, top + head);
                    g.DrawLine(pen, cx + 1.5f, cy + 1.5f, right, bottom);
                    g.DrawLine(pen, right, bottom, right - head, bottom);
                    g.DrawLine(pen, right, bottom, right, bottom - head);
                }
            }
        }

        void DrawRefreshActionIcon(Graphics g, Rectangle r, Color color)
        {
            bool active = claude.IsRefreshing || codex.IsRefreshing;
            float stroke = Math.Max(1.6f, r.Width * 0.1f);
            float inset = stroke;
            float angle = active ? spinnerFrame * 90f : -35f;
            var arcRect = new RectangleF(r.X + inset, r.Y + inset, r.Width - inset * 2f, r.Height - inset * 2f);
            using (var pen = new Pen(active ? Color.FromArgb(112, 196, 255) : color, stroke)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            })
                g.DrawArc(pen, arcRect, angle, 270f);

            double radians = (angle + 270f) * Math.PI / 180.0;
            float radiusX = arcRect.Width / 2f;
            float radiusY = arcRect.Height / 2f;
            float tipX = arcRect.X + radiusX + (float)Math.Cos(radians) * radiusX;
            float tipY = arcRect.Y + radiusY + (float)Math.Sin(radians) * radiusY;
            float arrow = Math.Max(2.5f, r.Width * 0.18f);
            using (var brush = new SolidBrush(active ? Color.FromArgb(112, 196, 255) : color))
            {
                g.FillPolygon(brush, new[]
                {
                    new PointF(tipX, tipY),
                    new PointF(tipX - arrow, tipY - arrow * 0.2f),
                    new PointF(tipX - arrow * 0.2f, tipY + arrow)
                });
            }
        }

        void DrawGearIcon(Graphics g, Rectangle r, Color color)
        {
            int cx = r.X + r.Width / 2;
            int cy = r.Y + r.Height / 2;
            using (var pen = new Pen(color, 1.5f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
            {
                for (int i = 0; i < 6; i++)
                {
                    double a = i * Math.PI / 3.0;
                    int x1 = cx + (int)Math.Round(Math.Cos(a) * 5.5);
                    int y1 = cy + (int)Math.Round(Math.Sin(a) * 5.5);
                    int x2 = cx + (int)Math.Round(Math.Cos(a) * 8.5);
                    int y2 = cy + (int)Math.Round(Math.Sin(a) * 8.5);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                g.DrawEllipse(pen, cx - 5, cy - 5, 10, 10);
                using (var fill = new SolidBrush(color))
                    g.FillEllipse(fill, cx - 2, cy - 2, 4, 4);
            }
        }

        void DrawLayoutIcon(Graphics g, Rectangle r, Color color)
        {
            bool vertical = string.Equals(settings.LayoutMode, "vertical", StringComparison.OrdinalIgnoreCase);
            using (var pen = new Pen(color, 1.3f))
            using (var fill = new SolidBrush(Color.FromArgb(60, color)))
            {
                if (vertical)
                {
                    int bw = 7, bh = 10, gap = 2;
                    int left  = r.X + r.Width / 2 - bw - gap / 2;
                    int top   = r.Y + r.Height / 2 - bh / 2;
                    g.FillRectangle(fill, left,          top, bw, bh);
                    g.DrawRectangle(pen,  left,          top, bw, bh);
                    g.FillRectangle(fill, left + bw + gap, top, bw, bh);
                    g.DrawRectangle(pen,  left + bw + gap, top, bw, bh);
                }
                else
                {
                    int bw = 10, bh = 7, gap = 2;
                    int left = r.X + r.Width  / 2 - bw / 2;
                    int top  = r.Y + r.Height / 2 - bh - gap / 2;
                    g.FillRectangle(fill, left, top,          bw, bh);
                    g.DrawRectangle(pen,  left, top,          bw, bh);
                    g.FillRectangle(fill, left, top + bh + gap, bw, bh);
                    g.DrawRectangle(pen,  left, top + bh + gap, bw, bh);
                }
            }
        }

        static void DrawBar(Graphics g, int x, int y, int w, int h, double? pct, Color accent)
        {
            int barH = Math.Max(h, 9);
            using (var bgPath = RoundRect(x, y, w, barH, barH / 2))
            {
                using (var bg = new SolidBrush(Color.FromArgb(20, 20, 24)))
                    g.FillPath(bg, bgPath);
                using (var inset = new Pen(Color.FromArgb(35, 35, 40), 0.6f))
                    g.DrawPath(inset, bgPath);
            }
            if (!pct.HasValue) return;
            double clamped = Math.Max(0, Math.Min(100, pct.Value));
            int fillW = Math.Max(clamped <= 0 ? 0 : 6, (int)Math.Round(w * clamped / 100.0));
            if (fillW <= 0) return;

            using (var glowPath = RoundRect(x, y + 1, fillW, barH, barH / 2))
            using (var glow = new SolidBrush(Color.FromArgb(30, accent.R, accent.G, accent.B)))
                g.FillPath(glow, glowPath);

            using (var fillPath = RoundRect(x, y, fillW, barH, barH / 2))
            {
                Color lighter = Color.FromArgb(Math.Min(255, accent.R + 40), Math.Min(255, accent.G + 40), Math.Min(255, accent.B + 40));
                using (var grad = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(x, y, fillW, barH), lighter, accent, 90f))
                    g.FillPath(grad, fillPath);

                using (var hlPath = RoundRect(x + 1, y + 1, Math.Max(4, fillW - 2), barH / 2, barH / 4))
                using (var hl = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
                    g.FillPath(hl, hlPath);
            }
        }

        void DrawTokenToggleIcon(Graphics g, Rectangle r, Color color)
        {
            bool showUsed = settings.ClaudeShowUsed;
            string text = showUsed ? T("用", "U") : T("余", "R");
            using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var br = new SolidBrush(color))
            {
                SizeF sz = g.MeasureString(text, f);
                g.DrawString(text, f, br, r.X + (r.Width - sz.Width) / 2f, r.Y + (r.Height - sz.Height) / 2f);
            }
        }

        void DrawFiveResetIcon(Graphics g, Rectangle r, Color color)
        {
            bool isRelative = string.Equals(settings.FiveHourResetMode, "relative", StringComparison.OrdinalIgnoreCase);
            int cx = r.X + r.Width / 2;
            int cr = 6;
            int cy = r.Y + cr + 1;
            using (var pen = new Pen(color, 1.3f))
            {
                g.DrawEllipse(pen, cx - cr, cy - cr, cr * 2, cr * 2);
                if (isRelative)
                    g.DrawLine(pen, cx, cy, cx, cy - cr + 2);
                else
                    g.DrawLine(pen, cx, cy, cx + cr - 2, cy);
            }
            using (var f = new Font("Segoe UI", 6.5f, FontStyle.Bold))
            using (var br = new SolidBrush(color))
            {
                string lbl = "5h";
                SizeF sz = g.MeasureString(lbl, f);
                g.DrawString(lbl, f, br, cx - sz.Width / 2f, r.Bottom - sz.Height);
            }
        }

        void DrawWeekResetIcon(Graphics g, Rectangle r, Color color)
        {
            bool isRelative = string.Equals(settings.WeeklyResetMode, "relative", StringComparison.OrdinalIgnoreCase);
            int cx = r.X + r.Width / 2;
            int cr = 6;
            int cy = r.Y + cr + 1;
            using (var pen = new Pen(color, 1.3f))
            {
                g.DrawEllipse(pen, cx - cr, cy - cr, cr * 2, cr * 2);
                if (isRelative)
                    g.DrawLine(pen, cx, cy, cx, cy - cr + 2);
                else
                    g.DrawLine(pen, cx, cy, cx + cr - 2, cy);
            }
            using (var f = new Font("Segoe UI", 6f, FontStyle.Bold))
            using (var br = new SolidBrush(color))
            {
                string lbl = T("周", "Wk");
                SizeF sz = g.MeasureString(lbl, f);
                g.DrawString(lbl, f, br, cx - sz.Width / 2f, r.Bottom - sz.Height);
            }
        }

        void DrawResizeGrip(Graphics g)
        {
            int right = ClientSize.Width - 6;
            int bottom = ClientSize.Height - 6;
            using (var dotBrush = new SolidBrush(Color.FromArgb(70, 75, 82)))
            {
                for (int row = 0; row < 3; row++)
                    for (int col = 2 - row; col < 3; col++)
                        g.FillEllipse(dotBrush, right - (2 - col) * 5 - 2, bottom - (2 - row) * 5 - 2, 3, 3);
            }
        }

        static System.Drawing.Drawing2D.GraphicsPath RoundRect(int x, int y, int w, int h, int r)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            w = Math.Max(1, w);
            h = Math.Max(1, h);
            r = Math.Max(0, Math.Min(r, Math.Min(w, h) / 2));
            if (r == 0)
            {
                path.AddRectangle(new Rectangle(x, y, w, h));
                return path;
            }
            int d = r * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
