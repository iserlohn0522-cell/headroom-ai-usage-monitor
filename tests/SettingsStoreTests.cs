using System;
using System.Drawing;
using System.IO;

namespace Headroom
{
    static class SettingsStoreTests
    {
        public static void Run(string root)
        {
            string dir = Path.Combine(Path.GetTempPath(), "HeadroomSettingsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                TestMissingFileCreatesDefault(dir);
                TestLoadsJsonValues(dir);
                TestCustomWidgetDefaultsAndModeRoundTrip(dir);
                TestAppearanceSettingClamps(dir);
                TestLegacyMigration(dir);
                TestInvalidJsonFallsBack(dir);
                Console.WriteLine("SettingsStoreTests: passed");
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        static void TestMissingFileCreatesDefault(string dir)
        {
            string path = Path.Combine(dir, "missing", "settings.json");
            var settings = SettingsStore.Load(path, null);
            Equal("zh-CN", settings.Language, "default language");
            Equal("claude-codex", settings.ServiceOrder, "default service order");
            Equal(232, settings.Width, "default width");
            Equal(94, settings.Height, "default height");
            Size ideal = WidgetLayoutMetrics.IdealSize(settings);
            Equal(settings.Width, ideal.Width, "default width matches layout");
            Equal(settings.Height, ideal.Height, "default height matches layout");
            True(settings.CollapseToBall, "collapse to ball default");
            True(settings.EdgeAutoHide, "edge auto-hide default");
            True(File.Exists(path), "default settings file created");
        }

        static void TestLoadsJsonValues(string dir)
        {
            string path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{" +
                "\"settingsVersion\":2," +
                "\"width\":900," +
                "\"height\":200," +
                "\"language\":\"zh-CN\"," +
                "\"normalIntervalMinutes\":20," +
                "\"widgetMode\":\"edge\"," +
                "\"opacityPercent\":82," +
                "\"overallScalePercent\":115," +
                "\"textScalePercent\":105," +
                "\"barHeight\":16," +
                "\"actionButtonSize\":30," +
                "\"ballSize\":48," +
                "\"collapseToBall\":false," +
                "\"edgeAutoHide\":true," +
                "\"collapseDelayMilliseconds\":1800," +
                "\"boostDurationMinutes\":40," +
                "\"boostIntervalMinutes\":2," +
                "\"alwaysOnTop\":true," +
                "\"showCodex\":false," +
                "\"showClaude\":true," +
                "\"layoutMode\":\"vertical\"," +
                "\"serviceOrder\":\"codex-claude\"," +
                "\"codexLoginMethod\":\"auto\"," +
                "\"claudeLoginMethod\":\"cli\"," +
                "\"fiveHourResetMode\":\"time\"," +
                "\"weeklyResetMode\":\"relative\"," +
                "\"warningRemainingPercent\":60," +
                "\"criticalRemainingPercent\":25" +
                "}");

            var settings = SettingsStore.Load(path, null);
            Equal(900, settings.Width, "width");
            Equal(200, settings.Height, "height");
            Equal("zh-CN", settings.Language, "language");
            Equal(20, settings.NormalIntervalMinutes, "normal interval");
            Equal("edge", settings.WidgetMode, "widget mode");
            Equal(82, settings.OpacityPercent, "opacity");
            Equal(115, settings.OverallScalePercent, "overall scale");
            Equal(105, settings.TextScalePercent, "text scale");
            Equal(16, settings.BarHeight, "bar height");
            Equal(30, settings.ActionButtonSize, "action button size");
            Equal(48, settings.BallSize, "ball size");
            True(!settings.CollapseToBall, "collapse to ball");
            True(settings.EdgeAutoHide, "edge auto-hide");
            Equal(1800, settings.CollapseDelayMilliseconds, "collapse delay");
            Equal(40, settings.BoostDurationMinutes, "boost duration");
            Equal(2, settings.BoostIntervalMinutes, "boost interval");
            True(settings.AlwaysOnTop, "always on top");
            True(!settings.ShowCodex, "show codex");
            True(settings.ShowClaude, "show claude");
            Equal("vertical", settings.LayoutMode, "layout");
            Equal("codex-claude", settings.ServiceOrder, "service order");
            Equal("auto", settings.CodexLoginMethod, "codex login");
            Equal("cli", settings.ClaudeLoginMethod, "claude login");
            Equal("time", settings.FiveHourResetMode, "5h reset mode");
            Equal("relative", settings.WeeklyResetMode, "weekly reset mode");
            Equal(60, settings.WarningRemainingPercent, "warning threshold");
            Equal(25, settings.CriticalRemainingPercent, "critical threshold");

            settings.Width = 777;
            SettingsStore.Save(path, settings);
            var saved = SettingsStore.Load(path, null);
            Equal(777, saved.Width, "saved width");
        }

        static void TestCustomWidgetDefaultsAndModeRoundTrip(string dir)
        {
            string path = Path.Combine(dir, "custom", "settings.json");
            var loaded = SettingsStore.Load(path, null);
            Equal(5, loaded.NormalIntervalMinutes, "normal interval default");
            Equal("compact", loaded.WidgetMode, "widget mode default");
            True(loaded.AlwaysOnTop, "always on top default");
            Equal(94, loaded.OpacityPercent, "opacity default");
            Size compact = WidgetLayoutMetrics.IdealSize(loaded);
            True(compact.Width < 300 && compact.Height < 124, "compact is materially smaller");

            loaded.WidgetMode = "edge";
            loaded.OpacityPercent = 82;
            SettingsStore.Save(path, loaded);

            var reloaded = SettingsStore.Load(path, null);
            Equal("edge", reloaded.WidgetMode, "widget mode round trip");
            Equal(82, reloaded.OpacityPercent, "opacity round trip");
            Size detailed = WidgetLayoutMetrics.IdealSize(reloaded);
            True(detailed.Width > compact.Width && detailed.Height > compact.Height, "detailed mode is larger");

            reloaded.WidgetMode = "compact";
            reloaded.ShowClaude = false;
            reloaded.ShowCodex = true;
            Size singleService = WidgetLayoutMetrics.IdealSize(reloaded);
            True(singleService.Height < compact.Height, "single-service compact mode is shorter");

            reloaded.ShowClaude = true;
            reloaded.TextScalePercent = 150;
            Size largeText = WidgetLayoutMetrics.IdealSize(reloaded);
            True(largeText.Width > compact.Width, "large text expands the layout width");

            Size highDpi = WidgetLayoutMetrics.IdealSize(reloaded, 144);
            True(highDpi.Width > largeText.Width && highDpi.Height > largeText.Height, "high DPI scales the layout");
        }

        static void TestLegacyMigration(string dir)
        {
            string target = Path.Combine(dir, "target", "settings.json");
            string legacy = Path.Combine(dir, "legacy.json");
            File.WriteAllText(legacy, "{\"language\":\"ja\",\"width\":888}");
            var settings = SettingsStore.Load(target, legacy);
            Equal("zh-CN", settings.Language, "legacy language");
            Equal(232, settings.Width, "legacy width migrated");
            Equal(94, settings.Height, "legacy height migrated");
            True(File.Exists(target), "legacy copied");
        }

        static void TestAppearanceSettingClamps(string dir)
        {
            string path = Path.Combine(dir, "clamps.json");
            File.WriteAllText(path, "{" +
                "\"settingsVersion\":2," +
                "\"language\":\"ja\"," +
                "\"showCodex\":false," +
                "\"showClaude\":false," +
                "\"overallScalePercent\":999," +
                "\"textScalePercent\":1," +
                "\"barHeight\":99," +
                "\"actionButtonSize\":1," +
                "\"ballSize\":999," +
                "\"collapseDelayMilliseconds\":1" +
                "}");
            var settings = SettingsStore.Load(path, null);
            Equal("zh-CN", settings.Language, "unsupported language migration");
            True(settings.ShowClaude, "at least one service remains visible");
            Equal(150, settings.OverallScalePercent, "overall scale clamp");
            Equal(70, settings.TextScalePercent, "text scale clamp");
            Equal(24, settings.BarHeight, "bar height clamp");
            Equal(24, settings.ActionButtonSize, "button size clamp");
            Equal(64, settings.BallSize, "ball size clamp");
            Equal(300, settings.CollapseDelayMilliseconds, "collapse delay clamp");
            string canonical = File.ReadAllText(path);
            True(canonical.Contains("\"language\":\"zh-CN\""), "normalized language persisted");
            True(canonical.Contains("\"showClaude\":true"), "service visibility invariant persisted");
        }

        static void TestInvalidJsonFallsBack(string dir)
        {
            string path = Path.Combine(dir, "invalid.json");
            File.WriteAllText(path, "{invalid");
            var settings = SettingsStore.Load(path, null);
            Equal("zh-CN", settings.Language, "invalid default language");
            Equal(232, settings.Width, "invalid default width");
            Equal(94, settings.Height, "invalid default height");
        }

        static void True(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label + ": expected true");
        }

        static void Equal(string expected, string actual, string label)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }

        static void Equal(int expected, int actual, string label)
        {
            if (expected != actual)
                throw new InvalidOperationException(label + ": expected " + expected + ", got " + actual);
        }
    }
}
