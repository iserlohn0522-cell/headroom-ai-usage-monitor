using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Headroom
{
    partial class UsageForm
    {
        int allowancePage;
        string ServiceLabel(ServiceState service) { return string.IsNullOrWhiteSpace(service.Data.Name) ? service.Name : service.Data.Name; }
        List<QuotaView> PageRows()
        {
            var rows=AllowanceRows();
            allowancePage=Math.Max(0,Math.Min(allowancePage,(Math.Max(1,rows.Count)-1)/6));
            return rows.Skip(allowancePage*6).Take(6).ToList();
        }
        WidgetSkin Skin { get { return WidgetSkin.Get(settings.Skin); } }
        List<ServiceState> AllowanceServices() { return VisibleServices().Select(x => x.Item1).ToList(); }
        QuotaView BallQuota() { return QuotaPresentation.Select(AllowanceServices(), settings, DateTime.Now); }
        List<QuotaView> AllowanceRows()
        {
            return QuotaPresentation.Rows(AllowanceServices(),DateTime.Now,settings.NormalIntervalMinutes);
        }
        float FloatingScale { get { return runtimeDpi/96f * settings.OverallScalePercent/100f * Math.Max(100,settings.TextScalePercent)/100f * Math.Max(64,Math.Min(96,settings.BallSize))/64f; } }
        Size FloatingSize()
        {
            int diameter=(int)Math.Ceiling(64*FloatingScale);
            return new Size(diameter,diameter);
        }
        Size AllowancePanelSize()
        {
            bool detail = settings.WidgetMode == "edge";
            int rows = Math.Max(1, PageRows().Count);
            return new Size(UiScaleTextColumn(detail ? 460 : 328), UiScaleTextRow((detail ? 44 : 32) * rows + 62));
        }
        void SkinText(Graphics g, string text, Rectangle r, float size, Color color, bool bold, TextFormatFlags align, bool ball = false)
        {
            string family = settings.Skin == "terminal" && English ? "Consolas" : UiFontName;
            using (var font = new Font(family, size * (ball ? Math.Min(Width,Height)/64f : runtimeDpi / 96f * settings.OverallScalePercent / 100f * settings.TextScalePercent / 100f), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
            using (var brush=new SolidBrush(color))
            using (var format=new StringFormat { LineAlignment=StringAlignment.Center, Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.NoWrap,
                Alignment=(align & TextFormatFlags.Right)!=0 ? StringAlignment.Far : (align & TextFormatFlags.HorizontalCenter)!=0 ? StringAlignment.Center : StringAlignment.Near })
                g.DrawString(text,font,brush,r,format);
        }
        Color ViewColor(QuotaView view)
        {
            if (!view.IsCurrent) return Skin.Muted;
            return BatteryColor(view.HasValue ? (int?)Math.Round(view.Window.Remaining.Value) : null, settings.Skin == "midnight" && view.Service.Name == "Claude" ? ClaudeFiveColor() : Skin.Accent);
        }
        string ValueText(QuotaView view)
        {
            if (!view.HasValue) return "—";
            double value=view.Window.Remaining.Value;
            string number=(value>0 && value<1 || value>99 && value<100) ? value.ToString("0.0",CultureInfo.InvariantCulture) : Math.Round(value).ToString(CultureInfo.InvariantCulture);
            return (view.IsCurrent ? "" : "~") + number + "%";
        }
        string WindowName(AllowanceWindow window) { return window.Id == "weekly" ? T("每周", "Weekly") : window.Label; }
        string SelectedWindowName(QuotaView view) { return view.Window.Support==WindowSupport.Supported ? WindowName(view.Window) : view.Window.Support==WindowSupport.Unsupported ? T("无支持窗口", "No supported window") : T("窗口未知", "Window unknown"); }
        string AllowanceExplanation()
        {
            var lines=new List<string> { T("外环=Claude 5h；内环=Claude 每周；液体面积=Codex 每周。", "Outer arc = Claude 5h; inner arc = Claude weekly; liquid area = Codex weekly."), T("细蓝环=Codex 5h（仅在提供时）；所有编码均为剩余额度。", "Thin blue arc = Codex 5h, only when supplied. All encodings mean remaining."), T("! 读取失败；~ 旧读数；… 加载中；? 未知；— 不提供。", "! read error; ~ old reading; ... loading; ? unknown; dash not offered; 0 exhausted; + extra quotas in expanded view.") };
            foreach(var view in AllowanceRows()) {
                lines.Add(ServiceLabel(view.Service)+" / "+SelectedWindowName(view)+" / "+ValueText(view)+" / "+QuotaPresentation.StateText(view,English));
                if(!string.IsNullOrWhiteSpace(view.Window.Reset)) lines.Add(T("重置：", "Reset: ")+view.Window.Reset);
            }
            lines.Add(T("未提供的窗口不显示；~ 表示上次读数。", "Unreported windows are omitted; ~ marks a previous reading."));
            lines.Add(T("右键调整浮窗大小；悬停展开。", "Right-click to adjust floating size; hover to expand."));
            return string.Join("\n",lines.ToArray());
        }
        void PaintAllowancePanel(Graphics g)
        {
            var skin = Skin;
            bool detail = settings.WidgetMode == "edge";
            using (var path = RoundRect(0,0,ClientSize.Width-1,ClientSize.Height-1,UiScale(skin.Radius)))
            using (var brush = new SolidBrush(skin.Background))
            using (var pen = new Pen(skin.Border)) { g.FillPath(brush,path); g.DrawPath(pen,path); }
            int pad=UiScale(12), rail=UiScale(detail ? 45 : 78), w=ClientSize.Width-pad*2-rail;
            SkinText(g,settings.Skin == "terminal" ? "> HEADROOM / " + T("剩余", "REMAINING") : "HEADROOM · " + T("剩余额度", "REMAINING") + (HeadroomOptions.FixtureMode ? " · DEMO" : ""),new Rectangle(pad,UiScale(6),w,UiScale(19)),10,skin.Muted,true,TextFormatFlags.Left);
            int y=UiScale(29), h=UiScaleTextRow(detail ? 44 : 32);
            foreach (var view in PageRows()) {
                Color color=ViewColor(view);
                if (skin.Id=="paper") {
                    using(var fill=new SolidBrush(skin.Surface)) g.FillRectangle(fill,pad-UiScale(4),y-UiScale(2),w+UiScale(8),h-UiScale(3));
                    using(var line=new Pen(color,2)) g.DrawLine(line,pad-UiScale(4),y-UiScale(2),pad-UiScale(4),y+h-UiScale(5));
                }
                string state=QuotaPresentation.StateText(view,English);
                string heading=ServiceLabel(view.Service) + " · " + SelectedWindowName(view);
                SkinText(g,heading,new Rectangle(pad,y,w-UiScale(59),UiScaleTextRow(16)),11,skin.Text,true,TextFormatFlags.Left);
                SkinText(g,ValueText(view),new Rectangle(pad+w-UiScale(59),y,UiScale(59),UiScaleTextRow(16)),12,color,true,TextFormatFlags.Right);
                int by=y+UiScaleTextRow(20), bh=UiScale(6);
                if (view.HasValue) PaintAllowanceTrack(g,new Rectangle(pad,by,w,bh),view,color);
                else SkinText(g,state,new Rectangle(pad,by-UiScale(2),w,UiScale(15)),10,skin.Muted,false,TextFormatFlags.Left);
                if (detail) {
                    string reset = string.IsNullOrWhiteSpace(view.Window.Reset) ? T("重置时间未提供", "Reset not supplied") : T("重置 ", "Reset ") + ResetLabel(view.Window.Reset);
                    string note=view.IsCurrent ? (view.Condition==QuotaCondition.Exhausted ? state + " · " : "") + reset : state + (view.HasValue ? T(" · 上次读数", " · last reading") : "");
                    if(!view.HasValue) note=view.Condition==QuotaCondition.Error ? T("刷新以重试；账号见设置", "Refresh to retry; accounts in Settings") : view.Condition==QuotaCondition.Unsupported ? T("提供方未提供数值额度", "Provider offers no numeric allowance") : T("等待读取提供方数据", "Waiting for provider data");
                    SkinText(g,note,new Rectangle(pad,by+UiScale(8),w,UiScaleTextRow(14)),10,skin.Muted,false,TextFormatFlags.Left);
                } else if (!view.IsCurrent && view.HasValue) {
                    SkinText(g,state,new Rectangle(pad+w/2,by-UiScale(3),w/2,UiScale(14)),9,skin.Text,true,TextFormatFlags.Right);
                }
                y+=h;
            }
            string footer=T("圆球：弧线和液体均表示剩余", "Sphere: arcs + liquid / remaining");
            if (AllowanceRows().Count>6) footer += " · " + (allowancePage+1) + "/" + ((AllowanceRows().Count+5)/6);
            SkinText(g,footer,new Rectangle(pad,ClientSize.Height-UiScale(26),w,UiScale(19)),9,skin.Muted,false,TextFormatFlags.Left);
            hits["allowance-info"]=new Rectangle(pad,ClientSize.Height-UiScale(28),w,UiScale(27));
        }
        string ResetLabel(string raw)
        {
            DateTimeOffset instant;
            return DateTimeOffset.TryParse(raw,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out instant) ? instant.ToLocalTime().ToString("MM-dd HH:mm zzz") : raw;
        }
        void PaintAllowanceTrack(Graphics g, Rectangle r, QuotaView view, Color color)
        {
            using (var bg=new SolidBrush(Skin.Track)) g.FillRectangle(bg,r);
            float fraction=(float)Math.Max(0,Math.Min(100,view.Window.Remaining.Value))/100;
            using (var brush=new SolidBrush(color)) {
                if (Skin.Segmented) {
                    int cells=20; float cell=r.Width/(float)cells;
                    for(int i=0;i<cells;i++) if ((i+1)/(float)cells<=fraction) g.FillRectangle(brush,r.X+i*cell,r.Y,Math.Max(1,cell-2),r.Height);
                } else if(fraction>0) g.FillRectangle(brush,r.X,r.Y,r.Width*fraction,r.Height);
            }
            if (!view.IsCurrent) using(var pen=new Pen(Skin.Background,1)) for(int x=r.X;x<r.Right;x+=6) g.DrawLine(pen,x,r.Bottom,x+6,r.Top);
        }
        QuotaView SphereQuota(string service,string window)
        {
            return QuotaPresentation.SphereWindow(AllowanceServices(),service,window,DateTime.Now,settings.NormalIntervalMinutes);
        }
        string SphereMark(QuotaView view)
        {
            if(view==null) return "-";
            switch(view.Condition) {
                case QuotaCondition.Error:return "!";
                case QuotaCondition.Stale:return "~";
                case QuotaCondition.Loading:return "...";
                case QuotaCondition.Unknown:return "?";
                case QuotaCondition.Unsupported:return "-";
                default:return "";
            }
        }
        void SphereArc(Graphics g,QuotaView view,float radius,Color color,float width)
        {
            if(view==null) return; // Unsupported/unreported windows have no phantom track.
            var rect=new RectangleF(32-radius,32-radius,radius*2,radius*2);
            using(var pen=new Pen(Skin.Track,width)) g.DrawEllipse(pen,rect);
            if(!view.HasValue) {
                using(var pen=new Pen(Skin.Muted,width) { DashStyle=DashStyle.Dot }) g.DrawEllipse(pen,rect);
                return;
            }
            Color ink=view.Condition==QuotaCondition.Exhausted ? Color.FromArgb(224,89,91) : view.IsCurrent ? color : Skin.Muted;
            using(var pen=new Pen(ink,width)) {
                pen.StartCap=LineCap.Flat;pen.EndCap=LineCap.Flat;
                if(!view.IsCurrent || Skin.Segmented) pen.DashStyle=DashStyle.Dot;
                float sweep=(float)(Math.Max(0,Math.Min(100,view.Window.Remaining.Value))*3.6);
                if(sweep>0) g.DrawArc(pen,rect,-90,sweep);
            }
        }
        void SphereText(Graphics g,string text,float y,float size,Color color)
        {
            using(var font=new Font(Skin.Id=="terminal" ? "Consolas" : "Segoe UI",size,FontStyle.Bold,GraphicsUnit.Pixel))
            using(var brush=new SolidBrush(color))
            using(var path=new GraphicsPath())
            using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center }) {
                path.AddString(text,font.FontFamily,(int)FontStyle.Bold,size,new RectangleF(14,y,36,12),format);
                using(var halo=new Pen(Skin.Background,1.4f) { LineJoin=LineJoin.Round }) g.DrawPath(halo,path);
                g.FillPath(brush,path);
            }
        }
        void PaintAllowanceBall(Graphics g)
        {
            hits["ball"]=ClientRectangle;
            var saved=g.Save();g.ScaleTransform(Width/64f,Height/64f);
            var skin=Skin;
            using(var brush=new SolidBrush(skin.Background)) g.FillEllipse(brush,1,1,62,62);
            using(var pen=new Pen(skin.Border,.7f)) g.DrawEllipse(pen,1,1,62,62);
            Color amber=skin.Id=="paper" ? Color.FromArgb(164,92,27) : skin.Id=="terminal" ? Color.FromArgb(190,243,117) : Color.FromArgb(235,177,104);
            Color green=skin.Id=="paper" ? Color.FromArgb(34,125,94) : Color.FromArgb(101,222,167);
            Color blue=skin.Id=="paper" ? Color.FromArgb(57,134,180) : skin.Id=="terminal" ? Color.FromArgb(60,154,108) : Color.FromArgb(83,164,218);
            var c5=SphereQuota("Claude","five-hour");var c7=SphereQuota("Claude","weekly");
            var x5=SphereQuota("Codex","five-hour");var liquid=SphereQuota("Codex","weekly");
            SphereArc(g,c5,29,amber,3);SphereArc(g,c7,24,green,3);SphereArc(g,x5,19.5f,blue,2);
            // Reserve the same liquid disk in every state; rings never move or swap roles.
            var disk=new RectangleF(15,15,34,34);
            using(var fill=new SolidBrush(skin.Surface)) g.FillEllipse(fill,disk);
            if(liquid!=null && liquid.HasValue) {
                float h=(float)(34*QuotaPresentation.LiquidHeight(liquid.Window.Remaining.Value/100));
                var clipped=g.Save();using(var path=new GraphicsPath()) { path.AddEllipse(disk);g.SetClip(path); }
                using(var fill=new SolidBrush(liquid.IsCurrent ? blue : skin.Muted)) g.FillRectangle(fill,15,49-h,34,h);
                if(!liquid.IsCurrent) using(var pen=new Pen(skin.Background,.7f)) for(int x=0;x<64;x+=4) g.DrawLine(pen,x,50,x+24,15);
                g.Restore(clipped);
            }
            var codexService=AllowanceServices().FirstOrDefault(v=>string.Equals(v.Name,"Codex",StringComparison.OrdinalIgnoreCase));
            if(liquid==null && codexService!=null) {
                var windows=QuotaPresentation.Windows(codexService.Data);
                var w=windows.FirstOrDefault(v=>v.Id=="weekly") ?? new AllowanceWindow { Id="weekly",Label="7d",Support=WindowSupport.Unknown };
                liquid=QuotaPresentation.Map(codexService,w,DateTime.Now,settings.NormalIntervalMinutes);
            }
            string value=liquid!=null && liquid.HasValue ? ValueText(liquid) : SphereMark(liquid);
            // Text with a background halo remains readable above and below the liquid surface.
            SphereText(g,"CX7",21,7,skin.Text);
            SphereText(g,value,31,10,liquid!=null && liquid.Condition==QuotaCondition.Exhausted ? Color.FromArgb(244,112,112) : skin.Text);
            var rows=AllowanceRows();
            string marker=rows.Any(v=>v.Condition==QuotaCondition.Error) ? "!" : rows.Any(v=>v.Condition==QuotaCondition.Stale) ? "~" : rows.Any(v=>v.Condition==QuotaCondition.Loading) ? "..." : rows.Any(v=>v.Condition==QuotaCondition.Unknown) ? "?" : rows.Any(v=>v.Condition==QuotaCondition.Exhausted) ? "0" : "";
            // Extra providers/windows are disclosed; hover gives every named allowance.
            if(rows.Any(v=>v.Window.Support==WindowSupport.Supported && !((v.Service.Name=="Claude" || v.Service.Name=="Codex") && (v.Window.Id=="weekly" || v.Window.Id=="five-hour"))) && marker=="") marker="+";
            SphereText(g,marker,48,8,skin.Text);
            g.Restore(saved);
        }
        void FloatingText(Graphics g,string value,Rectangle rect,float size,Color color,bool bold,bool right)
        {
            using(var font=new Font(settings.Skin=="terminal" && English ? "Consolas" : UiFontName,size,bold ? FontStyle.Bold : FontStyle.Regular,GraphicsUnit.Pixel))
            using(var brush=new SolidBrush(color))
            using(var format=new StringFormat { Alignment=right ? StringAlignment.Far : StringAlignment.Near, LineAlignment=StringAlignment.Center, Trimming=StringTrimming.EllipsisCharacter, FormatFlags=StringFormatFlags.NoWrap })
                g.DrawString(value,font,brush,rect,format);
        }
        void AddAllowanceMenus()
        {
            var skins=new ToolStripMenuItem(T("皮肤", "Skin"));
            foreach(string id in new[]{"midnight","paper","terminal"}) {
                string chosen=id; var item=new ToolStripMenuItem(id=="midnight" ? T("午夜 · 柔和", "Midnight · soft") : id=="paper" ? T("纸张 · 明亮", "Paper · light") : T("终端 · 分段", "Terminal · segmented")) { Checked=settings.Skin==id };
                item.Click+=(s,e)=>{settings.Skin=chosen;settings.Save();SetupTrayIcon();Invalidate();}; skins.DropDownItems.Add(item);
            }
            trayMenu.Items.Add(skins);
            var density=new ToolStripMenuItem(T("浮窗大小", "Floating size"));
            foreach(int size in new[]{64,80,96}) {
                int chosen=size;var item=new ToolStripMenuItem(size==64 ? T("紧凑", "Compact") : size==80 ? T("舒适", "Comfortable") : T("大号", "Large")) { Checked=settings.BallSize==size };
                item.Click+=(sender,args)=>{settings.BallSize=chosen;settings.Save();if(collapsedToBall) ResizeCollapsedBallForCurrentDpi();SetupTrayIcon();Invalidate();};density.DropDownItems.Add(item);
            }
            trayMenu.Items.Add(density);
            trayMenu.Items.Add(T("圆球图例…", "Sphere legend..."),null,(sender,args)=>MessageBox.Show(this,AllowanceExplanation(),"Headroom",MessageBoxButtons.OK,MessageBoxIcon.Information));
            if (AllowanceRows().Count>6) trayMenu.Items.Add(T("下一页额度", "Next allowance page"),null,(s,e)=>{allowancePage=(allowancePage+1)%((AllowanceRows().Count+5)/6);if(collapsedToBall) ResizeCollapsedBallForCurrentDpi();else ApplyIdealSize();Invalidate();});
            if (customProviders.Count>0) trayMenu.Items.Add(T("断开本地提供方", "Disconnect local providers"),null,(s,e)=>DisconnectCustomProviders());
            trayMenu.Items.Add(T("添加本地提供方清单…", "Load local provider manifest…"),null,(s,e)=> {
                using(var dialog=new OpenFileDialog { Filter="JSON|*.json", CheckFileExists=true }) if(dialog.ShowDialog(this)==DialogResult.OK) {
                    settings.ProviderManifest=dialog.FileName; settings.Save(); LoadCustomProviders(dialog.FileName); foreach(var custom in customProviders) RefreshCustomProvider(custom); ApplyIdealSize(); SetupTrayIcon(); Invalidate();
                }
            });
        }
    }
}
