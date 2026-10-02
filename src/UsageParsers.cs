using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Headroom
{
    static class UsageParsers
    {
        public static UsageData ParseClaudeApi(string json)
        {
            var data = NewData("Claude", json);
            var root = Json.ParseObject(json);
            if (root == null) { data.Status = "fetch_error"; return data; }
            AddClaude(data, root, "five_hour", "five-hour", "5h");
            AddClaude(data, root, "seven_day", "weekly", "7d");
            Finish(data);
            return data;
        }

        public static UsageData ParseCodexApi(string json)
        {
            var data = NewData("Codex", json);
            var root = Json.ParseObject(json);
            if (root == null) { data.Status = "fetch_error"; return data; }
            data.Plan = Json.String(root, "plan_type");
            var limits = Json.Object(root, "rate_limit") ?? root;
            AddCodex(data, Json.Object(limits, "primary_window"), "primary");
            AddCodex(data, Json.Object(limits, "secondary_window"), "secondary");
            // A valid returned window describes this snapshot's actual capabilities.
            // An empty/malformed response is unknown, never proof of no limit.
            bool known = data.Windows.Any(w => w.Support == WindowSupport.Supported);
            AddAbsent(data, "five-hour", "5h", known);
            AddAbsent(data, "weekly", "7d", known);
            Finish(data);
            return data;
        }

        static UsageData NewData(string name, string json)
        {
            return new UsageData { Name=name, Source=name + " API", UpdatedAt=DateTime.Now, HasWindowMetadata=true };
        }

        static void AddAbsent(UsageData data, string id, string label, bool known)
        {
            if (!data.Windows.Any(w => w.Id == id)) data.Windows.Add(new AllowanceWindow {
                Id=id, Label=label, Support=known ? WindowSupport.Unsupported : WindowSupport.Unknown });
        }

        static void AddClaude(UsageData data, Dictionary<string, object> root, string key, string id, string label)
        {
            var raw = Json.Object(root, key);
            var window = new AllowanceWindow { Id=id, Label=label, Support=raw != null ? WindowSupport.Supported :
                (root.ContainsKey(key) && root[key] == null ? WindowSupport.Unsupported : WindowSupport.Unknown) };
            if (raw != null) {
                double? used = ValidPercent(Json.Double(raw, "utilization"));
                window.Remaining = used.HasValue ? 100 - used.Value : (double?)null;
                window.Reset = Json.String(raw, "resets_at");
                if (!used.HasValue) window.ReadState = "error";
                if (id == "five-hour") { data.FiveHourUsed=used; data.FiveHourReset=ConvertIsoToLegacyFormat(window.Reset); }
                else { data.WeeklyUsed=used; data.WeeklyReset=ConvertIsoToLegacyFormat(window.Reset); }
            }
            data.Windows.Add(window);
        }

        static void AddCodex(UsageData data, Dictionary<string, object> raw, string position)
        {
            if (raw == null || raw.Count == 0) return;
            long? duration = Json.Long(raw, "limit_window_seconds");
            string id = duration == 18000 ? "five-hour" : duration == 604800 ? "weekly" : position;
            string label = duration == 18000 ? "5h" : duration == 604800 ? "7d" :
                duration.HasValue && duration > 0 ? (duration.Value % 3600 == 0 ? (duration.Value / 3600).ToString(CultureInfo.InvariantCulture) + "h" : (duration.Value / 60.0).ToString("0.#", CultureInfo.InvariantCulture) + "m") : position;
            double? used = ValidPercent(Json.Double(raw, "used_percent"));
            long? reset = Json.Long(raw, "reset_at");
            string resetText = null;
            if (reset.HasValue && reset > 0) {
                try { resetText = DateTimeOffset.FromUnixTimeSeconds(reset.Value).ToString("o"); } catch { }
            }
            // A reset_after_seconds field is only relative to this observation; never to paint time.
            if (resetText == null) {
                long? after = Json.Long(raw, "reset_after_seconds");
                if (after.HasValue && after >= 0 && after < 31536000) resetText = new DateTimeOffset(data.UpdatedAt).AddSeconds(after.Value).ToString("o");
            }
            var window = new AllowanceWindow { Id=id, Label=label, Support=WindowSupport.Supported,
                Remaining=used.HasValue ? 100-used.Value : (double?)null, Reset=resetText,
                ReadState=used.HasValue ? null : "error" };
            if (data.Windows.Any(w => w.Id == id)) { data.Status="fetch_error"; return; }
            data.Windows.Add(window);
            if (id == "five-hour") { data.FiveHourUsed=used; data.FiveHourReset=ConvertIsoToLegacyFormat(resetText); }
            if (id == "weekly") { data.WeeklyUsed=used; data.WeeklyReset=ConvertIsoToLegacyFormat(resetText); }
        }

        static void Finish(UsageData data)
        {
            if (data.Status == null && !data.Windows.Any(w => w.Remaining.HasValue)) data.Status="no_data";
        }

        internal static double? ValidPercent(double? value)
        {
            return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value) && value >= 0 && value <= 100 ? value : null;
        }

        static string ConvertIsoToLegacyFormat(string iso)
        {
            DateTimeOffset dto;
            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
                return dto.ToLocalTime().ToString("yyyy/M/d H:mm", CultureInfo.InvariantCulture);
            return iso;
        }
    }
}
