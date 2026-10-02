using System;
using System.Drawing;
using System.Windows.Forms;
namespace Headroom
{
    partial class UsageForm
    {
        void LoadCustomProviders(string manifest = null)
        {
            customProviders.Clear(); customPaths.Clear();
            string path=manifest ?? HeadroomOptions.ProviderManifest ?? settings.ProviderManifest;
            if(string.IsNullOrWhiteSpace(path)) return;
            try {
                foreach(var entry in AllowanceProviders.ReadManifest(path)) {
                    var service=new ServiceState(entry.Key,"",Color.SlateBlue);
                    customProviders.Add(service); customPaths.Add(service,entry.Value);
                }
            } catch { var service=new ServiceState("Provider config","",Color.Gray); service.Status="fetch_error"; customProviders.Add(service); }
        }
        void DisconnectCustomProviders()
        {
            settings.ProviderManifest="";
            LoadCustomProviders("");
            settings.Save(); ApplyIdealSize(); SetupTrayIcon(); Invalidate();
        }
        void RefreshCustomProvider(ServiceState service)
        {
            string path; if(!customPaths.TryGetValue(service,out path)) return;
            try {
                var data=AllowanceProviders.ParseSnapshot(AllowanceProviders.ReadLocalJson(path));
                if(!string.Equals(data.Source,service.Name,StringComparison.OrdinalIgnoreCase)) throw new FormatException("Provider id differs from manifest");
                service.Data=data; service.Status=data.Status;
            } catch { service.Status="fetch_error"; }
            service.LastRefresh=DateTime.Now;
        }
    }
}
