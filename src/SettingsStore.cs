using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Headroom
{
    static class SettingsStore
    {
        public static WidgetSettings Load(string targetPath, string legacyPath)
        {
            var settings = new WidgetSettings();
            try
            {
                if (!File.Exists(targetPath))
                {
                    if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                        File.Copy(legacyPath, targetPath, false);
                    }
                    else
                    {
                        Save(targetPath, settings);
                        return settings;
                    }
                }

                string json = File.ReadAllText(targetPath);
                var root = Json.ParseObject(json);
                if (root == null)
                {
                    DebugLog.Write("settings-load-error.txt", "Invalid JSON: " + targetPath);
                    return settings;
                }

                int storedSettingsVersion = ReadInt(root, "settingsVersion", 0);
                bool needsUiMigration = storedSettingsVersion < WidgetSettings.CurrentSettingsVersion;
                bool needsCanonicalSave = false;
                settings.SettingsVersion = storedSettingsVersion;
                settings.Width = ReadInt(root, "width", settings.Width);
                settings.Height = ReadInt(root, "height", settings.Height);
                string storedLanguage = ReadString(root, "language", settings.Language);
                settings.Language = NormalizeLanguage(storedLanguage);
                if (!string.Equals(storedLanguage, settings.Language, StringComparison.OrdinalIgnoreCase))
                    needsCanonicalSave = true;
                settings.NormalIntervalMinutes = ReadInt(root, "normalIntervalMinutes", settings.NormalIntervalMinutes);
                settings.BoostDurationMinutes = ReadInt(root, "boostDurationMinutes", settings.BoostDurationMinutes);
                settings.BoostIntervalMinutes = ReadInt(root, "boostIntervalMinutes", settings.BoostIntervalMinutes);
                settings.AlwaysOnTop = ReadBool(root, "alwaysOnTop", settings.AlwaysOnTop);
                settings.OpacityPercent = ClampOpacity(ReadInt(root, "opacityPercent", settings.OpacityPercent));
                settings.OverallScalePercent = Clamp(ReadInt(root, "overallScalePercent", settings.OverallScalePercent), 70, 150);
                settings.TextScalePercent = Clamp(ReadInt(root, "textScalePercent", settings.TextScalePercent), 70, 150);
                settings.BarHeight = Clamp(ReadInt(root, "barHeight", settings.BarHeight), 8, 24);
                settings.ActionButtonSize = Clamp(ReadInt(root, "actionButtonSize", settings.ActionButtonSize), 24, 40);
                settings.BallSize = Clamp(ReadInt(root, "ballSize", settings.BallSize), 32, 64);
                settings.CollapseToBall = ReadBool(root, "collapseToBall", settings.CollapseToBall);
                settings.EdgeAutoHide = ReadBool(root, "edgeAutoHide", settings.EdgeAutoHide);
                settings.CollapseDelayMilliseconds = Clamp(ReadInt(root, "collapseDelayMilliseconds", settings.CollapseDelayMilliseconds), 300, 5000);
                settings.ShowCodex = ReadBool(root, "showCodex", settings.ShowCodex);
                settings.ShowClaude = ReadBool(root, "showClaude", settings.ShowClaude);
                if (!settings.ShowCodex && !settings.ShowClaude)
                {
                    settings.ShowClaude = true;
                    needsCanonicalSave = true;
                }
                settings.LayoutMode = NormalizeLayoutMode(ReadString(root, "layoutMode", settings.LayoutMode));
                settings.WidgetMode = NormalizeWidgetMode(ReadString(root, "widgetMode", settings.WidgetMode));
                settings.ServiceOrder = NormalizeServiceOrder(ReadString(root, "serviceOrder", settings.ServiceOrder));
                settings.CodexShowUsed = ReadBool(root, "codexShowUsed", settings.CodexShowUsed);
                settings.ClaudeShowUsed = ReadBool(root, "claudeShowUsed", settings.ClaudeShowUsed);
                settings.CodexLoggedOut = ReadBool(root, "codexLoggedOut", settings.CodexLoggedOut);
                settings.ClaudeLoggedOut = ReadBool(root, "claudeLoggedOut", settings.ClaudeLoggedOut);
                settings.CodexLoginMethod = NormalizeLoginMethod(ReadString(root, "codexLoginMethod", settings.CodexLoginMethod));
                settings.ClaudeLoginMethod = NormalizeLoginMethod(ReadString(root, "claudeLoginMethod", settings.ClaudeLoginMethod));
                settings.FiveHourResetMode = NormalizeResetMode(ReadString(root, "fiveHourResetMode", settings.FiveHourResetMode));
                settings.WeeklyResetMode = NormalizeResetMode(ReadString(root, "weeklyResetMode", settings.WeeklyResetMode));
                settings.WarningRemainingPercent = ReadInt(root, "warningRemainingPercent", settings.WarningRemainingPercent);
                settings.CriticalRemainingPercent = ReadInt(root, "criticalRemainingPercent", settings.CriticalRemainingPercent);
                if (needsUiMigration)
                {
                    settings.SettingsVersion = WidgetSettings.CurrentSettingsVersion;
                    Size ideal = WidgetLayoutMetrics.IdealSize(settings);
                    settings.Width = ideal.Width;
                    settings.Height = ideal.Height;
                }
                if (needsUiMigration ||
                    (needsCanonicalSave && storedSettingsVersion <= WidgetSettings.CurrentSettingsVersion))
                    Save(targetPath, settings);
            }
            catch (Exception ex)
            {
                DebugLog.Write("settings-load-error.txt", ex.ToString());
            }
            return settings;
        }

        public static void Save(string path, WidgetSettings settings)
        {
            try
            {
                var root = new Dictionary<string, object>
                {
                    { "settingsVersion", WidgetSettings.CurrentSettingsVersion },
                    { "width", settings.Width },
                    { "height", settings.Height },
                    { "language", settings.Language },
                    { "normalIntervalMinutes", settings.NormalIntervalMinutes },
                    { "boostDurationMinutes", settings.BoostDurationMinutes },
                    { "boostIntervalMinutes", settings.BoostIntervalMinutes },
                    { "alwaysOnTop", settings.AlwaysOnTop },
                    { "opacityPercent", settings.OpacityPercent },
                    { "overallScalePercent", settings.OverallScalePercent },
                    { "textScalePercent", settings.TextScalePercent },
                    { "barHeight", settings.BarHeight },
                    { "actionButtonSize", settings.ActionButtonSize },
                    { "ballSize", settings.BallSize },
                    { "collapseToBall", settings.CollapseToBall },
                    { "edgeAutoHide", settings.EdgeAutoHide },
                    { "collapseDelayMilliseconds", settings.CollapseDelayMilliseconds },
                    { "showCodex", settings.ShowCodex },
                    { "showClaude", settings.ShowClaude },
                    { "layoutMode", settings.LayoutMode },
                    { "widgetMode", settings.WidgetMode },
                    { "serviceOrder", settings.ServiceOrder },
                    { "codexShowUsed", settings.CodexShowUsed },
                    { "claudeShowUsed", settings.ClaudeShowUsed },
                    { "codexLoggedOut", settings.CodexLoggedOut },
                    { "claudeLoggedOut", settings.ClaudeLoggedOut },
                    { "codexLoginMethod", settings.CodexLoginMethod },
                    { "claudeLoginMethod", settings.ClaudeLoginMethod },
                    { "fiveHourResetMode", settings.FiveHourResetMode },
                    { "weeklyResetMode", settings.WeeklyResetMode },
                    { "warningRemainingPercent", settings.WarningRemainingPercent },
                    { "criticalRemainingPercent", settings.CriticalRemainingPercent },
                };
                FileWrites.WriteUtf8Atomic(path, Json.Serialize(root) + "\n");
            }
            catch (Exception ex)
            {
                DebugLog.Write("settings-save-error.txt", ex.ToString());
            }
        }

        static int ReadInt(Dictionary<string, object> root, string key, int fallback)
        {
            long? value = Json.Long(root, key);
            return value.HasValue ? (int)value.Value : fallback;
        }

        static bool ReadBool(Dictionary<string, object> root, string key, bool fallback)
        {
            bool? value = Json.Bool(root, key);
            return value.HasValue ? value.Value : fallback;
        }

        static string ReadString(Dictionary<string, object> root, string key, string fallback)
        {
            string value = Json.String(root, key);
            return value ?? fallback;
        }

        static string NormalizeResetMode(string value)
        {
            return string.Equals(value, "time", StringComparison.OrdinalIgnoreCase) ? "time" : "relative";
        }

        static string NormalizeLayoutMode(string value)
        {
            return string.Equals(value, "vertical", StringComparison.OrdinalIgnoreCase) ? "vertical" : "horizontal";
        }

        static string NormalizeLanguage(string value)
        {
            return string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh-CN";
        }

        static string NormalizeWidgetMode(string value)
        {
            return string.Equals(value, "edge", StringComparison.OrdinalIgnoreCase) ? "edge" : "compact";
        }

        static int ClampOpacity(int value)
        {
            return Math.Max(35, Math.Min(100, value));
        }

        static int Clamp(int value, int minimum, int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        static string NormalizeServiceOrder(string value)
        {
            return string.Equals(value, "codex-claude", StringComparison.OrdinalIgnoreCase) ? "codex-claude" : "claude-codex";
        }

        static string NormalizeLoginMethod(string value)
        {
            if (string.Equals(value, "cli", StringComparison.OrdinalIgnoreCase)) return "cli";
            if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase)) return "auto";
            return "browser";
        }
    }
}
