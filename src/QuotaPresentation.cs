using System;
using System.Collections.Generic;
using System.Linq;

namespace Headroom
{
    enum WindowSupport { Unknown, Supported, Unsupported }
    enum QuotaCondition { Unknown, Unsupported, Loading, Fresh, Exhausted, Stale, Error }

    sealed class AllowanceWindow
    {
        public string Id;
        public string Label;
        public WindowSupport Support;
        public double? Remaining;
        public string Reset;
        public string ResetKind = "unknown";
        public string ReadState;
    }

    sealed class QuotaView
    {
        public ServiceState Service;
        public AllowanceWindow Window;
        public QuotaCondition Condition;
        public string FallbackReason;
        public bool HasValue { get { return Window.Remaining.HasValue && Window.Support == WindowSupport.Supported; } }
        public bool IsCurrent { get { return Condition == QuotaCondition.Fresh || Condition == QuotaCondition.Exhausted; } }
    }

    static class QuotaPresentation
    {
        public static List<AllowanceWindow> Windows(UsageData data)
        {
            if (data.HasWindowMetadata) return data.Windows;
            return new List<AllowanceWindow> {
                Legacy("five-hour", "5h", data.FiveHourRemainingPercent(), data.FiveHourReset),
                Legacy("weekly", "7d", data.WeeklyRemainingPercent(), data.WeeklyReset)
            };
        }

        static AllowanceWindow Legacy(string id, string label, int? remaining, string reset)
        {
            return new AllowanceWindow { Id = id, Label = label, Remaining = remaining,
                Reset = reset, Support = remaining.HasValue ? WindowSupport.Supported : WindowSupport.Unknown };
        }

        public static QuotaView Map(ServiceState service, AllowanceWindow window, DateTime now, int intervalMinutes)
        {
            var view = new QuotaView { Service = service, Window = window };
            string status = service.Status ?? service.Data.Status;
            if (IsError(status) || window.ReadState == "error" || service.Data.UpdatedAt > now.AddMinutes(5)) view.Condition = QuotaCondition.Error;
            else if (service.IsRefreshing || status == "starting" || status == "updating" || window.ReadState == "loading") view.Condition = QuotaCondition.Loading;
            else if (window.Support == WindowSupport.Unsupported) view.Condition = QuotaCondition.Unsupported;
            else if (window.ReadState == "stale" || (window.Remaining.HasValue &&
                (service.Data.UpdatedAt == DateTime.MinValue || now - service.Data.UpdatedAt > TimeSpan.FromMinutes(Math.Max(2, intervalMinutes * 2))))) view.Condition = QuotaCondition.Stale;
            else if (!window.Remaining.HasValue || window.Support == WindowSupport.Unknown) view.Condition = QuotaCondition.Unknown;
            else view.Condition = window.Remaining.Value <= 0 ? QuotaCondition.Exhausted : QuotaCondition.Fresh;
            return view;
        }

        public static bool IsError(string status)
        {
            return !string.IsNullOrEmpty(status) && status != "no_data" && status != "starting" && status != "updating" && status != "ok";
        }

        // One row per reported non-unsupported window; never fabricate missing windows.
        public static List<QuotaView> Rows(IEnumerable<ServiceState> services, DateTime now, int interval)
        {
            var result = new List<QuotaView>();
            foreach (var service in services) {
                var all = Windows(service.Data);
                var windows = all.Where(w => w.Support != WindowSupport.Unsupported).ToList();
                if (!windows.Any(w => w.Support == WindowSupport.Supported))
                    windows = new List<AllowanceWindow> { new AllowanceWindow {
                        Id="summary", Label="?", Support=all.Count>0 && all.All(w=>w.Support==WindowSupport.Unsupported) ? WindowSupport.Unsupported : WindowSupport.Unknown } };
                result.AddRange(windows.Select(w=>Map(service,w,now,interval)));
            }
            return result;
        }

        public static QuotaView Select(IList<ServiceState> services, WidgetSettings settings, DateTime now)
        {
            if (services.Count == 0) return null;
            ServiceState service = services.FirstOrDefault(s => string.Equals(s.Name, settings.BallService, StringComparison.OrdinalIgnoreCase)) ?? services[0];
            var windows = Windows(service.Data);
            var supported = windows.Where(w => w.Support == WindowSupport.Supported).ToList();
            AllowanceWindow window = supported.FirstOrDefault(w => w.Id == settings.BallWindow);
            bool explicitMissing = settings.BallWindow != "auto" && window == null;
            if (window == null) window = supported.FirstOrDefault(w => w.Id == "five-hour") ?? supported.FirstOrDefault(w => w.Id == "weekly") ?? supported.FirstOrDefault();
            if (window == null) window = windows.FirstOrDefault(w => w.Support != WindowSupport.Unsupported) ?? windows.FirstOrDefault() ??
                new AllowanceWindow { Id = "unknown", Label = "?", Support = WindowSupport.Unknown };
            var view = Map(service, window, now, settings.NormalIntervalMinutes);
            if (explicitMissing) view.FallbackReason = "preferred-unavailable";
            else if (window.Id == "weekly" && windows.Any(w => w.Id == "five-hour" && w.Support == WindowSupport.Unsupported)) view.FallbackReason = "weekly-only";
            return view;
        }

        // Height of a circular segment measured from the bottom, divided by diameter.
        // Invert area, not height: the filled disk area equals remaining allowance.
        public static double LiquidHeight(double fraction)
        {
            fraction=Math.Max(0,Math.Min(1,fraction));
            if(fraction==0 || fraction==1) return fraction;
            double low=0,high=2;
            for(int i=0;i<50;i++) {
                double h=(low+high)/2, y=1-h;
                double area=(Math.Acos(y)-y*Math.Sqrt(Math.Max(0,1-y*y)))/Math.PI;
                if(area<fraction) low=h; else high=h;
            }
            return (low+high)/4;
        }
        public static QuotaView SphereWindow(IEnumerable<ServiceState> services,string serviceName,string windowId,DateTime now,int interval)
        {
            var service=services.FirstOrDefault(s=>string.Equals(s.Name,serviceName,StringComparison.OrdinalIgnoreCase));
            if(service==null) return null;
            var window=Windows(service.Data).FirstOrDefault(w=>w.Id==windowId && w.Support==WindowSupport.Supported);
            return window==null ? null : Map(service,window,now,interval);
        }

        public static string StateText(QuotaView view, bool english)
        {
            switch (view.Condition) {
                case QuotaCondition.Unsupported: return english ? "Not offered" : "不提供";
                case QuotaCondition.Loading: return english ? "Loading" : "更新中";
                case QuotaCondition.Exhausted: return english ? "Exhausted" : "已用尽";
                case QuotaCondition.Stale: return english ? "Stale" : "已过期";
                case QuotaCondition.Error: return english ? "Read error" : "读取失败";
                case QuotaCondition.Unknown: return english ? "No data" : "无数据";
                default: return english ? "Remaining" : "剩余额度";
            }
        }
    }
}
