using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
namespace Headroom
{
    partial class UsageForm
    {
        // Uses the production painter; isolated fixture settings, no CLI or account access.
        internal static void RenderPreviews(string directory)
        {
            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("HEADROOM_SETTINGS_PATH",Path.Combine(directory,"preview-settings.json"));
            using(var form=new UsageForm()) {
                form.paintTimer.Stop(); form.schedulerTimer.Stop();
                form.RunNativeSmoke(directory);
                form.settings.CollapseToBall=false; form.settings.EdgeAutoHide=false;
                form.settings.ProviderManifest=""; form.customProviders.Clear(); form.customPaths.Clear();
                form.settings.ShowClaude=true; form.settings.ShowCodex=true; form.settings.OverallScalePercent=100; form.settings.TextScalePercent=100;
                foreach(string skin in new[]{"midnight","paper","terminal"}) {
                    foreach(string mode in new[]{"compact","edge","ball"}) {
                        form.ConfigurePreview("weekly-only"); form.settings.Skin=skin; form.settings.Language="en";
                        form.SavePreview(directory,skin+"-"+mode+"-weekly-only",mode);
                    }
                }
                form.settings.Skin="midnight";
                foreach(string state in new[]{"both","exhausted","loading","unknown","unsupported","stale","error","cached-error"}) {
                    form.ConfigurePreview(state); form.settings.Language="en"; form.settings.ShowClaude=false;
                    form.SavePreview(directory,"state-"+state+"-panel","edge");
                    form.SavePreview(directory,"state-"+state+"-ball","ball");
                }
                form.ConfigurePreview("weekly-only"); form.settings.Skin="paper"; form.settings.Language="zh-CN"; form.settings.ShowClaude=true;
                form.SavePreview(directory,"chinese-paper-panel","edge");
                form.SavePreview(directory,"chinese-paper-ball","ball");
                foreach(string skin in new[]{"midnight","paper","terminal"}) {
                    form.settings.Skin=skin;form.settings.Language="en";form.settings.ShowClaude=true;
                    foreach(string state in new[]{"weekly-only","both","cached-error","loading","stale","exhausted","unsupported"}) {
                        form.ConfigurePreview(state);form.SavePreview(directory,"floating-"+skin+"-"+state,"ball");
                    }
                }
                foreach(string skin in new[]{"midnight","paper","terminal"}) {
                    form.settings.Skin=skin;form.settings.Language="en";form.settings.ShowClaude=true;
                    foreach(int value in new[]{0,1,25,50,75,99,100}) {
                        form.ConfigurePreview("weekly-only");
                        form.SphereQuota("Codex","weekly").Window.Remaining=value;
                        form.SphereQuota("Claude","five-hour").Window.Remaining=100-value;
                        form.SphereQuota("Claude","weekly").Window.Remaining=50;
                        form.SavePreview(directory,"sphere-"+skin+"-"+value,"ball");
                    }
                }
                form.ConfigurePreview("weekly-only");form.settings.Skin="midnight";
                form.claude.Status="fetch_error";form.SavePreview(directory,"sphere-claude-error","ball");
                form.ConfigurePreview("weekly-only");form.SphereQuota("Claude","five-hour").Window.Remaining=0;
                form.SavePreview(directory,"sphere-claude-exhausted","ball");
                form.ConfigurePreview("weekly-only");form.settings.Skin="paper";
                form.settings.BallSize=96;form.SavePreview(directory,"floating-large-paper","ball");form.settings.BallSize=64;
                form.runtimeDpi=144; form.settings.TextScalePercent=150;
                form.SavePreview(directory,"dpi144-text150-paper-panel","edge");
                form.SavePreview(directory,"dpi144-paper-ball","ball");
            }
        }
        void RequireSmoke(bool condition,string message) { if(!condition) throw new InvalidOperationException("Native UI smoke: " + message); }
        void RunNativeSmoke(string directory)
        {
            runtimeDpi=96;settings.OverallScalePercent=100;settings.TextScalePercent=100;settings.BallSize=64;
            var checks=new System.Collections.Generic.List<string>();
            ConfigurePreview("weekly-only");
            string fixture="{\"primary_window\":{\"used_percent\":32,\"limit_window_seconds\":604800},\"secondary_window\":null}";
            File.WriteAllText(Path.Combine(directory,"codex.json"),fixture);
            LoadFixture(codex,"Codex","codex.json",UsageParsers.ParseCodexApi);
            RequireSmoke(BallQuota().Window.Id=="weekly" && BallQuota().Window.Remaining==68,"fixture to weekly ball");
            checks.Add("Fixture loading selects Codex weekly at 68%, without a five-hour bar");
            settings.BallWindow="five-hour";
            RequireSmoke(BallQuota().Window.Id=="weekly" && BallQuota().FallbackReason=="preferred-unavailable","old preference fallback");
            checks.Add("Saved five-hour preference falls back visibly to weekly");
            settings.BallWindow="auto";
            ApplyFetchResult(codex,UsageFetchResult.FetchError(null,null));
            RequireSmoke(BallQuota().Condition==QuotaCondition.Error && BallQuota().Window.Remaining==68,"retain failed reading");
            checks.Add("Failed refresh retains last reading with Error state");
            LoadFixture(codex,"Codex","codex.json",UsageParsers.ParseCodexApi);
            RequireSmoke(BallQuota().Condition==QuotaCondition.Fresh,"recover fixture");
            checks.Add("Valid subsequent refresh recovers to Fresh");
            settings.WidgetMode="compact";
            HandleClickAsync("widgetMode").GetAwaiter().GetResult();
            RequireSmoke(settings.WidgetMode=="edge","mode action");
            settings.CollapseToBall=true; settings.EdgeAutoHide=false;
            CollapseToQuotaBall(); RequireSmoke(collapsedToBall && Width>=64,"ball collapse");
            ExpandFromBall(); RequireSmoke(!collapsedToBall,"ball expand");
            checks.Add("Mode action, labeled ball collapse and expand work");
            settings.BallSize=64;settings.ShowClaude=true;settings.ShowCodex=true;ConfigurePreview("weekly-only");
            RequireSmoke(AllowanceRows().Count==3,"three simultaneous allowances");
            CollapseToQuotaBall(); RequireSmoke(Size==FloatingSize() && Width==64 && Height==64,"floating dimensions");
            runtimeDpi=144;settings.TextScalePercent=150;ResizeCollapsedBallForCurrentDpi();
            RequireSmoke(Width==144 && Height==144,"floating high DPI and text scale");
            runtimeDpi=96;settings.TextScalePercent=100;settings.BallSize=96;ResizeCollapsedBallForCurrentDpi();
            RequireSmoke(Width==96 && Height==96,"floating density resize");
            settings.BallSize=64;ExpandFromBall();
            checks.Add("Mixed Claude 5h + weekly and Codex weekly shown together; density and 144-DPI/150%-text sizing verified");

            SetLanguage("en"); RequireSmoke(English,"English selection");
            SetLanguage("zh-CN"); RequireSmoke(!English,"Chinese selection");
            checks.Add("Chinese and English selection updates the native UI");
            string providerDir=Path.Combine(directory,"native-provider-smoke"); Directory.CreateDirectory(providerDir);
            string snapshot=Path.Combine(providerDir,"mock.json"), manifest=Path.Combine(providerDir,"manifest.json");
            File.WriteAllText(manifest,"{\"schemaVersion\":1,\"providers\":[{\"id\":\"smoke-plan\",\"path\":\"mock.json\"}]}");
            string valid="{\"schemaVersion\":1,\"providerId\":\"smoke-plan\",\"displayName\":\"Smoke Plan\",\"status\":\"ok\",\"observedAt\":\""+DateTimeOffset.UtcNow.ToString("o")+"\",\"windows\":[{\"id\":\"weekly\",\"label\":\"7d\",\"support\":\"supported\",\"remainingPercent\":42}]}";
            File.WriteAllText(snapshot,valid); LoadCustomProviders(manifest);
            RequireSmoke(customProviders.Count==1,"manifest loading");
            var custom=customProviders[0]; RefreshCustomProvider(custom);
            settings.BallService="smoke-plan";
            RequireSmoke(BallQuota().Condition==QuotaCondition.Fresh && BallQuota().Window.Remaining==42,"custom selection");
            File.WriteAllText(snapshot,"{partial"); RefreshCustomProvider(custom);
            RequireSmoke(BallQuota().Condition==QuotaCondition.Error && BallQuota().Window.Remaining==42,"custom read failure");
            File.WriteAllText(snapshot,valid.Replace(":42",":17")); RefreshCustomProvider(custom);
            RequireSmoke(BallQuota().Condition==QuotaCondition.Fresh && BallQuota().Window.Remaining==17,"custom recovery");
            checks.Add("Custom manifest loads; partial-write failure keeps cached 42%; next valid file recovers to 17%");
            SetServiceVisible("Claude",false); SetServiceVisible("Codex",false);
            RequireSmoke(VisibleServices().Count==1 && VisibleServices()[0].Item1==custom,"custom-only visibility");
            DisconnectCustomProviders();
            RequireSmoke(customProviders.Count==0 && !settings.ShowClaude && !settings.ShowCodex && VisibleServices()[0].Item1==noProviders,"disconnect does not reactivate built-ins");
            checks.Add("Custom provider works alone; disconnect leaves an empty selection without enabling built-ins");
            settings.BallService="codex"; settings.ShowClaude=true; settings.ShowCodex=true;
            File.WriteAllText(Path.Combine(directory,"native-smoke.json"),Json.Serialize(new { platform="Windows", checks=checks.ToArray(), passed=checks.Count }));
        }
        void ConfigurePreview(string state)
        {
            settings.BallWindow="auto"; settings.BallService="codex";
            claude.Data=UsageParsers.ParseClaudeApi("{\"five_hour\":{\"utilization\":18,\"resets_at\":\"2026-10-02T17:00:00Z\"},\"seven_day\":{\"utilization\":41,\"resets_at\":\"2026-10-06T12:00:00Z\"}}"); claude.Status=null;
            string used=state=="exhausted" ? "100" : "32";
            codex.Data=UsageParsers.ParseCodexApi("{\"plan_type\":\"pro\",\"rate_limit\":{\"primary_window\":{\"used_percent\":"+used+",\"limit_window_seconds\":604800,\"reset_at\":1791288000},\"secondary_window\":null}}");
            codex.Status=null; codex.IsRefreshing=false;
            if(state=="both") codex.Data=UsageParsers.ParseCodexApi("{\"plan_type\":\"plus\",\"primary_window\":{\"used_percent\":12,\"limit_window_seconds\":18000},\"secondary_window\":{\"used_percent\":32,\"limit_window_seconds\":604800}}");
            if(state=="unknown" || state=="loading" || state=="error") codex.Data=UsageParsers.ParseCodexApi("{}");
            if(state=="unsupported") { codex.Data=UsageParsers.ParseCodexApi("{}"); foreach(var w in codex.Data.Windows) w.Support=WindowSupport.Unsupported; }
            if(state=="stale") codex.Data.UpdatedAt=DateTime.Now.AddMinutes(-30);
            if(state=="error" || state=="cached-error") codex.Status="fetch_error";
            if(state=="loading") codex.IsRefreshing=true;
        }
        void SavePreview(string directory,string name,string mode)
        {
            collapsedToBall=false; settings.WidgetMode=mode=="edge" ? "edge" : "compact";
            MinimumSize=new Size(1,1);
            if(mode=="ball") { collapsedToBall=true; Size=FloatingSize(); }
            else Size=AllowancePanelSize();
            using(var bitmap=new Bitmap(Width,Height,PixelFormat.Format32bppArgb))
            using(var graphics=Graphics.FromImage(bitmap)) { PaintContent(graphics); bitmap.Save(Path.Combine(directory,name+".png"),ImageFormat.Png); }
            File.WriteAllText(Path.Combine(directory,name+".txt"),AllowanceExplanation());
        }
    }
}
