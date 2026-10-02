using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Headroom
{
    sealed class ServiceState
    {
        public readonly string Name;
        public readonly string Url;
        public readonly Color Accent;
        public UsageData Data;
        public string Status;
        public bool IsRefreshing;
        public bool ManuallyLoggedOut;
        public DateTime LastRefresh = DateTime.MinValue;
        public DateTime? RateLimitedUntil;
        public DateTime? BoostUntil;
        public double? DisplayedFivePct;
        public double? DisplayedWeekPct;

        public ServiceState(string name, string url, Color accent)
        {
            Name = name;
            Url = url;
            Accent = accent;
            Data = new UsageData { Name = name, Source = "starting", Status = "starting" };
            Status = "starting";
        }

        public bool BoostActive
        {
            get { return BoostUntil.HasValue && BoostUntil.Value > DateTime.Now; }
        }
    }

    sealed class UsageData
    {
        public string Name;
        public string Source;
        public string Status;
        public DateTime UpdatedAt;
        public string Plan;
        public readonly System.Collections.Generic.List<AllowanceWindow> Windows = new System.Collections.Generic.List<AllowanceWindow>();
        public bool HasWindowMetadata;
        public double? FiveHourUsed;
        public double? WeeklyUsed;
        public int? FiveHourRemaining = null;
        public int? WeeklyRemaining = null;
        public string FiveHourReset;
        public string WeeklyReset;
        public bool FiveHourNotStarted = false;
        public bool WeeklyNotStarted = false;
        public bool HitLimit = false;

        public bool HasAnyValue()
        {
            return FiveHourUsed.HasValue || WeeklyUsed.HasValue || FiveHourRemaining.HasValue || WeeklyRemaining.HasValue;
        }

        public int? FiveHourRemainingPercent()
        {
            if (FiveHourRemaining.HasValue) return FiveHourRemaining.Value;
            if (FiveHourUsed.HasValue) return Clamp(100 - FiveHourUsed.Value);
            return null;
        }

        public int? FiveHourUsedPercent()
        {
            if (FiveHourUsed.HasValue) return Clamp(FiveHourUsed.Value);
            if (FiveHourRemaining.HasValue) return Clamp(100 - FiveHourRemaining.Value);
            return null;
        }

        public int? FiveHourDisplayPercent(bool showUsed)
        {
            return showUsed ? FiveHourUsedPercent() : FiveHourRemainingPercent();
        }

        public int? WeeklyRemainingPercent()
        {
            if (WeeklyRemaining.HasValue) return WeeklyRemaining.Value;
            if (WeeklyUsed.HasValue) return Clamp(100 - WeeklyUsed.Value);
            return null;
        }

        public int? WeeklyUsedPercent()
        {
            if (WeeklyUsed.HasValue) return Clamp(WeeklyUsed.Value);
            if (WeeklyRemaining.HasValue) return Clamp(100 - WeeklyRemaining.Value);
            return null;
        }

        public int? WeeklyDisplayPercent(bool showUsed)
        {
            return showUsed ? WeeklyUsedPercent() : WeeklyRemainingPercent();
        }

        static int Clamp(double value)
        {
            return Math.Max(0, Math.Min(100, (int)Math.Round(value)));
        }
    }

    sealed class WidgetSettings
    {
        public const int CurrentSettingsVersion = 3;

        public int SettingsVersion = CurrentSettingsVersion;
        public int Width = 232;
        public int Height = 94;
        public string Language = DefaultLanguage();
        public int NormalIntervalMinutes = 5;
        public int BoostDurationMinutes = 30;
        public int BoostIntervalMinutes = 1;
        public bool AlwaysOnTop = true;
        public int OpacityPercent = 94;
        public int OverallScalePercent = 100;
        public int TextScalePercent = 100;
        public int BarHeight = 12;
        public int ActionButtonSize = 30;
        public int BallSize = 64;
        public string Skin = "midnight";
        public string BallService = "codex";
        public string BallWindow = "auto";
        public string ProviderManifest = "";
        public bool CollapseToBall = true;
        public bool EdgeAutoHide = true;
        public int CollapseDelayMilliseconds = 1200;
        public bool ShowCodex = true;
        public bool ShowClaude = true;
        public string LayoutMode = "horizontal";
        public string WidgetMode = "compact";
        public string ServiceOrder = "claude-codex";
        public bool CodexShowUsed = false;
        public bool ClaudeShowUsed = false;
        public bool CodexLoggedOut = false;
        public bool ClaudeLoggedOut = false;
        public string CodexLoginMethod = "browser";
        public string ClaudeLoginMethod = "browser";
        public string FiveHourResetMode = "relative";
        public string WeeklyResetMode = "time";
        public int WarningRemainingPercent = 50;
        public int CriticalRemainingPercent = 30;

        internal static string SettingsPath
        {
            get
            {
                string overridePath = Environment.GetEnvironmentVariable("HEADROOM_SETTINGS_PATH");
                if (!string.IsNullOrWhiteSpace(overridePath))
                    return Path.GetFullPath(overridePath.Trim());

                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Headroom",
                    "settings.json");
            }
        }

        internal static string SettingsDirectory
        {
            get { return Path.GetDirectoryName(SettingsPath); }
        }

        internal static string LegacySettingsPath
        {
            get { return Path.Combine(Application.StartupPath, "settings.json"); }
        }

        static string DefaultLanguage()
        {
            return "zh-CN";
        }

        public static WidgetSettings Load()
        {
            return SettingsStore.Load(SettingsPath, LegacySettingsPath);
        }

        public WidgetSettings Clone()
        {
            var copy = new WidgetSettings();
            copy.CopyFrom(this);
            return copy;
        }

        public void CopyFrom(WidgetSettings other)
        {
            SettingsVersion = other.SettingsVersion;
            Width = other.Width;
            Height = other.Height;
            Language = other.Language;
            NormalIntervalMinutes = other.NormalIntervalMinutes;
            BoostDurationMinutes = other.BoostDurationMinutes;
            BoostIntervalMinutes = other.BoostIntervalMinutes;
            AlwaysOnTop = other.AlwaysOnTop;
            OpacityPercent = other.OpacityPercent;
            OverallScalePercent = other.OverallScalePercent;
            TextScalePercent = other.TextScalePercent;
            BarHeight = other.BarHeight;
            ActionButtonSize = other.ActionButtonSize;
            BallSize = other.BallSize;
            Skin = other.Skin;
            BallService = other.BallService;
            BallWindow = other.BallWindow;
            ProviderManifest = other.ProviderManifest;
            CollapseToBall = other.CollapseToBall;
            EdgeAutoHide = other.EdgeAutoHide;
            CollapseDelayMilliseconds = other.CollapseDelayMilliseconds;
            ShowCodex = other.ShowCodex;
            ShowClaude = other.ShowClaude;
            LayoutMode = other.LayoutMode;
            WidgetMode = other.WidgetMode;
            ServiceOrder = other.ServiceOrder;
            CodexShowUsed = other.CodexShowUsed;
            ClaudeShowUsed = other.ClaudeShowUsed;
            CodexLoggedOut = other.CodexLoggedOut;
            ClaudeLoggedOut = other.ClaudeLoggedOut;
            CodexLoginMethod = other.CodexLoginMethod;
            ClaudeLoginMethod = other.ClaudeLoginMethod;
            FiveHourResetMode = other.FiveHourResetMode;
            WeeklyResetMode = other.WeeklyResetMode;
            WarningRemainingPercent = other.WarningRemainingPercent;
            CriticalRemainingPercent = other.CriticalRemainingPercent;
        }

        public void ResetToDefaults()
        {
            CopyFrom(new WidgetSettings());
        }

        public void Save()
        {
            SettingsStore.Save(SettingsPath, this);
        }
    }

    static class WidgetLayoutMetrics
    {
        public static Size IdealSize(WidgetSettings settings)
        {
            return IdealSize(settings, 96);
        }

        public static Size IdealSize(WidgetSettings settings, int dpi)
        {
            bool detailed = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase);
            int serviceCount = (settings.ShowCodex ? 1 : 0) + (settings.ShowClaude ? 1 : 0);
            if (serviceCount == 0) serviceCount = 1;

            int scale = Math.Max(70, Math.Min(150, settings.OverallScalePercent));
            int textScale = Math.Max(70, Math.Min(150, settings.TextScalePercent));
            int rows = serviceCount * 2;
            int padding = Scale(detailed ? 8 : 6, scale, dpi);
            int rowGap = Scale(detailed ? 4 : 2, scale, dpi);
            int barHeight = Scale(Math.Max(8, Math.Min(24, settings.BarHeight)), scale, dpi);
            int textAwareRowHeight = detailed ? 25 : 19;
            if (textScale > 100)
                textAwareRowHeight = (int)Math.Ceiling(textAwareRowHeight * textScale / 100.0);
            int rowHeight = Math.Max(
                Scale(textAwareRowHeight, scale, dpi),
                barHeight + Scale(detailed ? 8 : 5, scale, dpi));
            int contentHeight = padding * 2 + rows * rowHeight + Math.Max(0, rows - 1) * rowGap;

            int buttonSize = Scale(Math.Max(24, Math.Min(40, settings.ActionButtonSize)), scale, dpi);
            int buttonGap = Scale(4, scale, dpi);
            int actionHeight = detailed
                ? padding * 2 + buttonSize * 3 + buttonGap * 2
                : padding * 2 + buttonSize * 2 + buttonGap;

            int width = Scale(detailed ? 304 : 232, scale, dpi);
            if (textScale > 100)
            {
                int textColumns = detailed ? 110 : 78;
                width += Scale(textColumns * (textScale - 100) / 100, scale, dpi);
            }
            int height = Math.Max(contentHeight, actionHeight);
            return new Size(width, height);
        }

        public static int Scale(int value, WidgetSettings settings)
        {
            return Scale(value, settings, 96);
        }

        public static int Scale(int value, WidgetSettings settings, int dpi)
        {
            return Scale(
                value,
                Math.Max(70, Math.Min(150, settings.OverallScalePercent)),
                dpi);
        }

        public static int ScaleForDpi(int value, int dpi)
        {
            return Scale(value, 100, dpi);
        }

        public static int ToLogicalPixels(int value, int dpi)
        {
            int safeDpi = Math.Max(48, dpi);
            return Math.Max(1, (int)Math.Round(value * 96.0 / safeDpi));
        }

        static int Scale(int value, int percent, int dpi)
        {
            int safeDpi = Math.Max(48, dpi);
            return Math.Max(1, (int)Math.Round(value * percent / 100.0 * safeDpi / 96.0));
        }
    }
}
