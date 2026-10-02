using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Headroom
{
    // Stable transport: a versioned, credential-free JSON snapshot. No plugin code is executed.
    static class AllowanceProviders
    {
        public const int MaxBytes = 1024 * 1024;
        public static string ReadLocalJson(string path)
        {
            if (new Uri(Path.GetFullPath(path)).IsUnc || !string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)) throw new FormatException("Local JSON required");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint)!=0) throw new FormatException("Snapshot links are not supported");
            using (var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) {
                if(stream.Length>MaxBytes) throw new FormatException("Provider file exceeds 1 MiB");
                using(var reader=new StreamReader(stream)) {
                    char[] buffer=new char[MaxBytes+1]; int count=reader.ReadBlock(buffer,0,buffer.Length);
                    if(count>MaxBytes) throw new FormatException("Provider file exceeds limit");
                    return new string(buffer,0,count);
                }
            }
        }
        public static Dictionary<string,string> ReadManifest(string path)
        {
            var root=Json.ParseObject(ReadLocalJson(path));
            if(!VersionOne(root)) throw new FormatException("Unsupported manifest version");
            object raw; if(!root.TryGetValue("providers",out raw) || !(raw is object[])) throw new FormatException("Expected providers array");
            var entries=(object[])raw; if(entries.Length>8) throw new FormatException("At most eight providers");
            string dir=Path.GetDirectoryName(Path.GetFullPath(path));
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in entries) {
                var item=entry as Dictionary<string,object>;
                string id=Required(item,"id",40), relative=Required(item,"path",240);
                if(!Regex.IsMatch(id,"^[a-z][a-z0-9-]*$") || id=="claude" || id=="codex") throw new FormatException("Invalid or reserved provider id");
                if(Path.IsPathRooted(relative)) throw new FormatException("Provider path must be relative");
                string full=Path.GetFullPath(Path.Combine(dir,relative));
                if(!full.StartsWith(dir+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new FormatException("Provider path must stay within manifest directory");
                if(new Uri(full).IsUnc || Path.GetExtension(full).ToLowerInvariant()!=".json") throw new FormatException("Local JSON required");
                string current=full;
                while(!string.Equals(current,dir,StringComparison.OrdinalIgnoreCase)) {
                    if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new FormatException("Provider links are not supported");
                    current=Path.GetDirectoryName(current);
                }
                if(result.ContainsKey(id)) throw new FormatException("Duplicate provider id");
                result.Add(id,full);
            }
            return result;
        }
        public static UsageData ParseSnapshot(string json)
        {
            var root=Json.ParseObject(json);
            if(!VersionOne(root)) throw new FormatException("Unsupported snapshot version");
            string id=Required(root,"providerId",40);
            if(!Regex.IsMatch(id,"^[a-z][a-z0-9-]*$")) throw new FormatException("Invalid provider id");
            string status=Required(root,"status",16);
            if(status!="ok" && status!="loading" && status!="error") throw new FormatException("Invalid provider status");
            var data=new UsageData { Name=Required(root,"displayName",40), Source=id, HasWindowMetadata=true,
                Status=status=="error" ? "fetch_error" : status=="loading" ? "updating" : null,
                UpdatedAt=Timestamp(Required(root,"observedAt",40)).LocalDateTime };
            object raw; if(!root.TryGetValue("windows",out raw) || !(raw is object[])) throw new FormatException("Expected windows array");
            var windows=(object[])raw; if(windows.Length>16) throw new FormatException("At most sixteen windows");
            var ids=new HashSet<string>();
            foreach(var value in windows) {
                var item=value as Dictionary<string,object>;
                string windowId=Required(item,"id",40), support=Required(item,"support",16);
                if(!Regex.IsMatch(windowId,"^[a-z][a-z0-9-]*$") || !ids.Add(windowId)) throw new FormatException("Invalid or duplicate window id");
                if(support!="supported" && support!="unsupported" && support!="unknown") throw new FormatException("Invalid support state");
                var window=new AllowanceWindow { Id=windowId, Label=Required(item,"label",40),
                    Support=support=="supported" ? WindowSupport.Supported : support=="unsupported" ? WindowSupport.Unsupported : WindowSupport.Unknown };
                double? remaining=Json.Double(item,"remainingPercent");
                object percentRaw;
                if(item.TryGetValue("remainingPercent",out percentRaw) && percentRaw!=null && (!remaining.HasValue || !UsageParsers.ValidPercent(remaining).HasValue || percentRaw is string || percentRaw is bool)) throw new FormatException("Invalid remaining percentage");
                if(support!="supported" && remaining.HasValue) throw new FormatException("Unsupported/unknown window cannot have a balance");
                window.Remaining=remaining;
                window.ReadState=Json.String(item,"state") ?? "fresh";
                if(!new[]{"fresh","loading","stale","error","unknown"}.Contains(window.ReadState)) throw new FormatException("Invalid window state");
                if(window.ReadState=="unknown" && remaining.HasValue) throw new FormatException("Unknown state cannot carry a balance");
                var reset=Json.Object(item,"reset");
                object resetRaw;
                if(item.TryGetValue("reset",out resetRaw) && resetRaw!=null && reset==null) throw new FormatException("Invalid reset object");
                if(reset!=null) {
                    window.ResetKind=Required(reset,"kind",16);
                    if(!new[]{"fixed","rolling","unknown"}.Contains(window.ResetKind)) throw new FormatException("Invalid reset kind");
                    string at=Json.String(reset,"at");
                    if(at!=null) { Timestamp(at); window.Reset=at; }
                }
                data.Windows.Add(window);
            }
            return data;
        }
        static bool VersionOne(Dictionary<string,object> root)
        {
            object value;
            return root!=null && root.TryGetValue("schemaVersion",out value) && (value is int || value is long) && Convert.ToInt64(value)==1;
        }
        static string Required(Dictionary<string,object> root,string key,int max)
        {
            object value;
            if(root==null || !root.TryGetValue(key,out value) || !(value is string)) throw new FormatException("Missing " + key);
            string text=(string)value;
            if(string.IsNullOrWhiteSpace(text)||text.Length>max||text.Any(char.IsControl)) throw new FormatException("Invalid " + key);
            return text;
        }
        static DateTimeOffset Timestamp(string text)
        {
            DateTimeOffset instant;
            if(!Regex.IsMatch(text,@"(Z|[+-]\d{2}:\d{2})$") || !DateTimeOffset.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out instant)) throw new FormatException("Timestamp needs ISO 8601 timezone");
            return instant;
        }
    }
}
