using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
namespace Headroom
{
    static class AllowanceTests
    {
        static void Equal(object expected, object actual, string message) { if(!object.Equals(expected,actual)) throw new Exception(message + ": expected " + expected + ", got " + actual); }
        public static void Run(string root)
        {
            var cases=(object[])new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Path.Combine(root,"tests","allowance-cases.json")));
            foreach(Dictionary<string,object> test in cases) {
                string name=Json.String(test,"name");
                var data=UsageParsers.ParseCodexApi(Json.Serialize(test["payload"]));
                DateTime now=DateTime.Now; data.UpdatedAt=now.AddMinutes(-(Json.Long(test,"ageMinutes") ?? 0));
                var service=new ServiceState("Codex","",Color.Blue) { Data=data, Status=Json.String(test,"serviceStatus") ?? data.Status };
                var settings=new WidgetSettings { BallWindow=Json.String(test,"preference") ?? "auto" };
                var view=QuotaPresentation.Select(new[]{service},settings,now);
                Equal(Json.String(test,"selected"),view.Window.Id,name + " selected");
                Equal(Json.String(test,"state"),view.Condition.ToString(),name + " state");
                Equal(Json.String(test,"fiveSupport"),data.Windows.First(w=>w.Id=="five-hour").Support.ToString(),name + " support");
                Equal(Json.Double(test,"remaining"),view.Window.Remaining,name + " remaining");
            }
            var c=new ServiceState("Claude","",Color.Gray) { Data=UsageParsers.ParseClaudeApi("{\"five_hour\":{\"utilization\":18},\"seven_day\":{\"utilization\":41}}") };
            var x=new ServiceState("Codex","",Color.Blue) { Data=UsageParsers.ParseCodexApi("{\"primary_window\":{\"used_percent\":32,\"limit_window_seconds\":604800},\"secondary_window\":null}") };
            c.Status=null; x.Status=null;
            var mixed=QuotaPresentation.Rows(new[]{c,x},DateTime.Now,5);
            Equal("Claude/five-hour,Claude/weekly,Codex/weekly",string.Join(",",mixed.Select(v=>v.Service.Name+"/"+v.Window.Id)),"simultaneous mixed windows");
            Equal(null,QuotaPresentation.SphereWindow(new[]{c,x},"Codex","five-hour",DateTime.Now,5),"sphere omits Pro five-hour slot");
            Equal("weekly",QuotaPresentation.SphereWindow(new[]{x,c},"Codex","weekly",DateTime.Now,5).Window.Id,"sphere mapping independent of order");
            foreach(double percent in new[]{0.0,.01,1,10,25,50,75,90,99,99.99,100}) {
                double normalized=QuotaPresentation.LiquidHeight(percent/100),y=1-2*normalized;
                double area=(Math.Acos(y)-y*Math.Sqrt(Math.Max(0,1-y*y)))/Math.PI;
                if(Math.Abs(area-percent/100)>1e-10) throw new Exception("Liquid area mismatch at "+percent);
            }
            if(Math.Abs(QuotaPresentation.LiquidHeight(.25)-.25)<.01) throw new Exception("Liquid must invert area, not use linear height");
            x.Status="fetch_error"; mixed=QuotaPresentation.Rows(new[]{c,x},DateTime.Now,5);
            Equal(QuotaCondition.Fresh,mixed[0].Condition,"error isolated from other provider");
            Equal(QuotaCondition.Error,mixed[2].Condition,"cached provider error visible");
            Equal(68.0,mixed[2].Window.Remaining,"error keeps remaining, not used");
            x.Status=null; x.Data=UsageParsers.ParseCodexApi("{\"primary_window\":{\"used_percent\":12,\"limit_window_seconds\":18000},\"secondary_window\":{\"used_percent\":32,\"limit_window_seconds\":604800}}");
            Equal(4,QuotaPresentation.Rows(new[]{c,x},DateTime.Now,5).Count,"both-window plans simultaneously shown");
            x.Data=UsageParsers.ParseCodexApi("{}");
            Equal(3,QuotaPresentation.Rows(new[]{c,x},DateTime.Now,5).Count,"unknown provider one summary, no phantom windows");
            var unsupportedService=new ServiceState("Test","",Color.Gray) { Status="fetch_error" };
            var unsupportedWindow=new AllowanceWindow { Id="weekly",Label="7d",Support=WindowSupport.Unsupported };
            Equal(QuotaCondition.Error,QuotaPresentation.Map(unsupportedService,unsupportedWindow,DateTime.Now,5).Condition,"error remains visible without supported windows");
            Equal("zh-CN",SettingsStore.NormalizeLanguage("ja"),"Japanese migration");
            Equal("en",SettingsStore.NormalizeLanguage("EN"),"English preserved");
            Equal("zh-CN",SettingsStore.NormalizeLanguage(null),"Chinese default");
            Equal("zh-CN",SettingsStore.NormalizeLanguage("de"),"Unsupported language");
            string dir=Path.Combine(Path.GetTempPath(),"HeadroomQuotaTests",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            string path=Path.Combine(dir,"settings.json");
            File.WriteAllText(path,"{\"settingsVersion\":2,\"language\":\"en\",\"width\":900,\"ballWindow\":\"five-hour\",\"showClaude\":false}");
            var migrated=SettingsStore.Load(path,null);
            Equal(3,migrated.SettingsVersion,"version migration"); Equal("en",migrated.Language,"English survives migration");
            Equal(900,migrated.Width,"v2 dimensions retained"); Equal(false,migrated.ShowClaude,"visibility retained");
            Equal("five-hour",migrated.BallWindow,"explicit preference retained, selection falls back at runtime");
            var clone=migrated.Clone(); clone.Skin="paper"; clone.ProviderManifest="providers.json"; SettingsStore.Save(path,clone);
            Equal("paper",SettingsStore.Load(path,null).Skin,"skin persisted");
            Equal("providers.json",SettingsStore.Load(path,null).ProviderManifest,"provider config persisted");
            File.WriteAllText(path,"{\"settingsVersion\":3,\"showClaude\":false,\"showCodex\":false,\"providerManifest\":\"providers.json\"}");
            var onlyCustom=SettingsStore.Load(path,null);
            Equal(false,onlyCustom.ShowClaude,"custom-only Claude remains disabled"); Equal(false,onlyCustom.ShowCodex,"custom-only Codex remains disabled");
            onlyCustom.ProviderManifest=""; SettingsStore.Save(path,onlyCustom);
            Equal(false,SettingsStore.Load(path,null).ShowClaude,"disconnect does not silently enable a built-in");
            var snapshot=File.ReadAllText(Path.Combine(root,"examples","providers","mock.json"));
            var parsed=AllowanceProviders.ParseSnapshot(snapshot);
            Equal(2,parsed.Windows.Count,"custom windows"); Equal("rolling",parsed.Windows[0].ResetKind,"reset semantics");
            Reject(snapshot.Replace("\"schemaVersion\": 1","\"schemaVersion\": 2"));
            Reject(snapshot.Replace("68", "168"));
            Reject(snapshot.Replace("\"supported\"", "\"unsupported\""));
            Reject(snapshot.Replace("\"remainingPercent\": 68", "\"remainingPercent\": \"68\""));
            Console.WriteLine("AllowanceTests: " + cases.Length + " shared cases + language, migration and contract checks passed");
        }
        static void Reject(string json) { try { AllowanceProviders.ParseSnapshot(json); } catch(FormatException) { return; } throw new Exception("Invalid contract accepted"); }
    }
}
