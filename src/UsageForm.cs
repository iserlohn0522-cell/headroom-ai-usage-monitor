using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Headroom
{
    sealed partial class UsageForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
        [DllImport("user32.dll", EntryPoint = "SendMessage")]
        static extern IntPtr SendMessageIcon(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref LayeredSize psize, IntPtr hdcSrc, ref LayeredPoint pptSrc, uint crKey, [In] ref BlendFunction pblend, uint dwFlags);
        [DllImport("user32.dll", ExactSpelling = true)] static extern IntPtr  GetDC(IntPtr hWnd);
        [DllImport("user32.dll", ExactSpelling = true)] static extern int     ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll",  ExactSpelling = true)] static extern IntPtr  CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll",  ExactSpelling = true)] static extern bool    DeleteDC(IntPtr hDC);
        [DllImport("gdi32.dll",  ExactSpelling = true)] static extern IntPtr  SelectObject(IntPtr hDC, IntPtr hObj);
        [DllImport("gdi32.dll",  ExactSpelling = true)] static extern bool    DeleteObject(IntPtr hObj);
        [DllImport("gdi32.dll",  ExactSpelling = true)] static extern IntPtr  CreateDIBSection(IntPtr hdc, ref BitmapInfo pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [StructLayout(LayoutKind.Sequential)]
        struct BlendFunction { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        struct LayeredPoint   { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)]
        struct LayeredSize    { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfoHeader { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfo { public BitmapInfoHeader bmiHeader; public int bmiColors; }

        const int WmNcLButtonDown = 0xA1;
        const int HtCaption = 0x2;
        const int WmDpiChanged = 0x02E0;
        const float LabelFontSize   = 10.8f;
        const float PercentFontSize = 16.5f;
        const float ResetFontSize   =  9.9f;

        readonly Timer paintTimer = new Timer();
        readonly Timer schedulerTimer = new Timer();
        readonly Dictionary<string, Rectangle> hits = new Dictionary<string, Rectangle>();
        readonly Dictionary<string, Rectangle> silentHits = new Dictionary<string, Rectangle>();
        string pendingSilentKey = "";
        Point pendingSilentScreenStart;
        Point pendingSilentWindowStart;
        bool silentDragging;
        readonly WidgetSettings settings = WidgetSettings.Load();
        readonly ToolTip toolTip = new ToolTip { InitialDelay = 2000, ReshowDelay = 100, ShowAlways = true };
        readonly Timer tooltipTimer = new Timer();
        readonly NotifyIcon trayIcon = new NotifyIcon();
        readonly ContextMenuStrip trayMenu = new ContextMenuStrip();
        Icon appIcon;
        Icon smallWindowIcon;
        Icon largeWindowIcon;
        string pendingTooltipText = "";
        Point pendingTooltipLocation;

        static readonly HttpClient httpClient;
        FileSystemWatcher claudeCredWatcher;
        FileSystemWatcher codexCredWatcher;
        FileSystemWatcher fixtureWatcher;
        DateTime lastClaudeCredNotify = DateTime.MinValue;
        DateTime lastCodexCredNotify = DateTime.MinValue;
        DateTime lastFixtureNotify = DateTime.MinValue;
        ServiceState claude = new ServiceState("Claude", ClaudeUrl, Color.FromArgb(215, 154, 101));
        ServiceState codex = new ServiceState("Codex", CodexUrl, Color.FromArgb(132, 205, 252));

        static UsageForm()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        string hoverKey = "";
        bool sideRailVisible;
        double sideRailOpacity;
        int spinnerFrame;
        int paintSubtick;
        bool collapsedToBall;
        bool autoCollapseSuspended;
        bool settingsPreviewActive;
        bool dpiChangeActive;
        int runtimeDpi = 96;
        Size expandedSize;
        Point expandedLocation;
        string collapsedDockEdge = "";
        string collapsedScreenDeviceName = "";
        Rectangle collapsedWorkArea = Rectangle.Empty;
        DateTime collapseDueAt = DateTime.MaxValue;

        bool English
        {
            get { return string.Equals(settings.Language, "en", StringComparison.OrdinalIgnoreCase); }
        }

        string UiFontName
        {
            get { return English ? "Segoe UI" : "Microsoft YaHei UI"; }
        }

        static readonly Dictionary<string, bool> cliAvailabilityCache = new Dictionary<string, bool>();
        static readonly object cliCacheLock = new object();

        public UsageForm()
        {
            Text = "Headroom";
            AutoScaleMode = AutoScaleMode.None;
            Width = settings.Width;
            Height = settings.Height;
            ApplyLayoutMinimumSize();
            expandedSize = Size;
            expandedLocation = Location;
            FormBorderStyle = FormBorderStyle.None;
            TopMost = settings.AlwaysOnTop;
            KeyPreview = true;
            ShowInTaskbar = false;
            trayIcon.DoubleClick += OnTrayIconDoubleClick;
            SetupTrayIcon();

            claude.ManuallyLoggedOut = settings.ClaudeLoggedOut;
            codex.ManuallyLoggedOut = settings.CodexLoggedOut;
            if (claude.ManuallyLoggedOut) MarkLoggedOut(claude);
            if (codex.ManuallyLoggedOut) MarkLoggedOut(codex);

            if (HeadroomOptions.FixtureMode) SetupFixtureWatcher();
            else SetupCredentialWatchers();

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += (s, e) =>
            {
                if (pendingSilentKey.Length > 0)
                {
                    if (!silentDragging)
                    {
                        string capturedKey = pendingSilentKey;
                        BeginInvoke(new Action(async () => await HandleClickAsync(capturedKey)));
                    }
                    pendingSilentKey = "";
                    silentDragging = false;
                }
            };
            MouseEnter += (s, e) =>
            {
                CancelAutoCollapse();
                if (collapsedToBall) ExpandFromBall();
            };
            MouseLeave += (s, e) =>
            {
                hoverKey = "";
                sideRailVisible = false;
                ScheduleAutoCollapse();
                Invalidate();
            };
            Resize += (s, e) =>
            {
                if (!collapsedToBall && !dpiChangeActive)
                {
                    settings.Width = WidgetLayoutMetrics.ToLogicalPixels(Width, runtimeDpi);
                    settings.Height = WidgetLayoutMetrics.ToLogicalPixels(Height, runtimeDpi);
                    if (!settingsPreviewActive) settings.Save();
                }
                Invalidate();
            };
            KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.F5) await RefreshAllAsync(true);
                if (e.KeyCode == Keys.S || e.KeyCode == Keys.F2) await ShowSettingsDialog();
            };

            paintTimer.Interval = 40;
            paintTimer.Tick += (s, e) =>
            {
                if (!Visible) return;
                UpdateAutoCollapseState();
                UpdateSideRailVisibilityFromCursor();
                UpdateSideRailOpacity();
                paintSubtick = (paintSubtick + 1) % 6;
                if (paintSubtick == 0) spinnerFrame = (spinnerFrame + 1) % 4;
                UpdateBarAnimation(codex,  false);
                UpdateBarAnimation(claude, false);
                RenderLayered();
            };
            paintTimer.Start();

            tooltipTimer.Interval = 2000;
            tooltipTimer.Tick += (s, e) =>
            {
                tooltipTimer.Stop();
                if (!string.IsNullOrEmpty(pendingTooltipText))
                    toolTip.Show(pendingTooltipText, this, pendingTooltipLocation.X, pendingTooltipLocation.Y, 3000);
            };

            schedulerTimer.Interval = 10000;
            schedulerTimer.Tick += async (s, e) => await RunScheduledRefreshAsync();
            schedulerTimer.Start();

            Shown += async (s, e) =>
            {
                SetupTrayIcon();
                await RefreshAllAsync(true);
                ScheduleAutoCollapse();
            };

            FormClosed += (s, e) =>
            {
                try { if (claudeCredWatcher != null) claudeCredWatcher.Dispose(); } catch { }
                try { if (codexCredWatcher  != null) codexCredWatcher.Dispose();  } catch { }
                try { if (fixtureWatcher != null) fixtureWatcher.Dispose(); } catch { }
                try { paintTimer.Stop(); paintTimer.Dispose(); } catch { }
                try { schedulerTimer.Stop(); schedulerTimer.Dispose(); } catch { }
                try { tooltipTimer.Stop(); tooltipTimer.Dispose(); } catch { }
                try { toolTip.Dispose(); } catch { }
                try { trayIcon.Visible = false; trayIcon.Dispose(); } catch { }
                try { trayMenu.Dispose(); } catch { }
                DisposeWindowIcons();
            };
        }

        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x80000; return cp; } // WS_EX_LAYERED
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmDpiChanged)
            {
                int newDpi = m.WParam.ToInt32() & 0xffff;
                dpiChangeActive = true;
                runtimeDpi = Math.Max(48, newDpi);
                try
                {
                    base.WndProc(ref m);
                    ApplyCurrentDpiLayout();
                }
                finally { dpiChangeActive = false; }
                return;
            }
            if (m.Msg == Program.ShowExistingMessage)
            {
                ShowFromSecondInstance();
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x14) { m.Result = IntPtr.Zero; return; } // WM_ERASEBKGND: suppress
            base.WndProc(ref m);
        }

        internal void ShowFromSecondInstance()
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(ShowFromSecondInstance)); } catch { }
                return;
            }

            Visible = true;
            if (collapsedToBall) ExpandFromBall();
            Activate();
            BringToFront();
            SetupTrayIcon();
        }

        void RenderLayered()
        {
            if (!Visible || !IsHandleCreated || Width <= 0 || Height <= 0) return;
            IntPtr screenDC = GetDC(IntPtr.Zero);
            IntPtr memDC    = CreateCompatibleDC(screenDC);
            IntPtr hBmp = IntPtr.Zero, oldBmp = IntPtr.Zero;
            try
            {
                var bi = new BitmapInfo();
                bi.bmiHeader.biSize     = Marshal.SizeOf(typeof(BitmapInfoHeader));
                bi.bmiHeader.biWidth    = Width;
                bi.bmiHeader.biHeight   = -Height; // top-down
                bi.bmiHeader.biPlanes   = 1;
                bi.bmiHeader.biBitCount = 32;
                // biCompression = 0 (BI_RGB)
                IntPtr ppvBits;
                hBmp   = CreateDIBSection(memDC, ref bi, 0, out ppvBits, IntPtr.Zero, 0);
                oldBmp = SelectObject(memDC, hBmp);

                // Draw directly into the DIB section as pre-multiplied alpha
                using (var bmp = new System.Drawing.Bitmap(Width, Height, Width * 4, System.Drawing.Imaging.PixelFormat.Format32bppPArgb, ppvBits))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                    PaintContent(g);

                var sz    = new LayeredSize  { cx = Width, cy = Height };
                var srcPt = new LayeredPoint { X = 0, Y = 0 };
                byte alpha = (byte)Math.Max(35, Math.Min(255, settings.OpacityPercent * 255 / 100));
                var blend = new BlendFunction { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screenDC, IntPtr.Zero, ref sz, memDC, ref srcPt, 0, ref blend, 2); // ULW_ALPHA
            }
            finally
            {
                if (hBmp != IntPtr.Zero) { SelectObject(memDC, oldBmp); DeleteObject(hBmp); }
                DeleteDC(memDC);
                ReleaseDC(IntPtr.Zero, screenDC);
            }
        }

        async Task RunScheduledRefreshAsync()
        {
            DateTime now = DateTime.Now;
            schedulerTimer.Interval = RefreshPolicy.SchedulerIntervalMs(settings, claude, codex, now);

            if (settings.ShowCodex)  await MaybeRefreshAsync(codex);
            if (settings.ShowClaude) await MaybeRefreshAsync(claude);
        }

        async Task MaybeRefreshAsync(ServiceState service)
        {
            var decision = RefreshPolicy.Evaluate(service, settings, DateTime.Now);
            if (decision.RateLimited)
            {
                service.Status = "rate_limited";
                return;
            }
            if (decision.RateLimitExpired)
                service.RateLimitedUntil = null;
            if (decision.BoostExpired)
                service.BoostUntil = null;

            if (decision.ShouldRefresh)
                await RefreshServiceAsync(service, false);
        }

        static void UpdateBarAnimation(ServiceState svc, bool showUsed)
        {
            const double EaseFactor = 0.18;
            const double SnapThreshold = 0.08;
            int? target5 = svc.Data.FiveHourDisplayPercent(showUsed);
            int? targetW = svc.Data.WeeklyDisplayPercent(showUsed);
            if (target5.HasValue)
            {
                if (!svc.DisplayedFivePct.HasValue) svc.DisplayedFivePct = 0;
                double cur = svc.DisplayedFivePct.Value;
                double diff = target5.Value - cur;
                svc.DisplayedFivePct = Math.Abs(diff) < SnapThreshold ? (double)target5.Value : cur + diff * EaseFactor;
            }
            else
            {
                svc.DisplayedFivePct = null;
            }
            if (targetW.HasValue)
            {
                if (!svc.DisplayedWeekPct.HasValue) svc.DisplayedWeekPct = 0;
                double cur = svc.DisplayedWeekPct.Value;
                double diff = targetW.Value - cur;
                svc.DisplayedWeekPct = Math.Abs(diff) < SnapThreshold ? (double)targetW.Value : cur + diff * EaseFactor;
            }
            else
            {
                svc.DisplayedWeekPct = null;
            }
        }

        async Task RefreshAllAsync(bool manual)
        {
            var tasks = new List<Task>();
            if (settings.ShowCodex)  tasks.Add(RefreshServiceAsync(codex, manual));
            if (settings.ShowClaude) tasks.Add(RefreshServiceAsync(claude, manual));
            await Task.WhenAll(tasks);
        }

        async Task RefreshServiceAsync(ServiceState service, bool manual)
        {
            if (service.RateLimitedUntil.HasValue && service.RateLimitedUntil.Value > DateTime.Now)
            {
                service.Status = "rate_limited";
                Invalidate();
                return;
            }
            service.RateLimitedUntil = null;
            if (service.Name == "Claude") await RefreshClaudeViaApiAsync(service, manual);
            else                          await RefreshCodexViaApiAsync(service, manual);
        }

        static void LoadFixture(ServiceState service, string name, string fileName, Func<string, UsageData> parser)
        {
            string path = Path.Combine(HeadroomOptions.FixtureDir, fileName);
            if (!File.Exists(path))
            {
                service.Data = new UsageData
                {
                    Name = name,
                    Source = "Fixture",
                    UpdatedAt = DateTime.Now,
                    Status = "fixture_missing"
                };
                service.Status = "fixture_missing";
                service.LastRefresh = DateTime.Now;
                return;
            }

            string json = File.ReadAllText(path);
            if (Regex.IsMatch(json, "\"status\"\\s*:\\s*\"login_required\"", RegexOptions.IgnoreCase))
            {
                service.Data = new UsageData
                {
                    Name = name,
                    Source = "Fixture",
                    UpdatedAt = DateTime.Now,
                    Status = "login_required"
                };
            }
            else
            {
                service.Data = parser(json);
                service.Data.Name = name;
                service.Data.Source = "Fixture";
            }
            service.Status = service.Data.Status;
            service.RateLimitedUntil = null;
            service.LastRefresh = DateTime.Now;
        }

        void ApplyFetchResult(ServiceState service, UsageFetchResult result)
        {
            if (!string.IsNullOrEmpty(result.DebugName))
                WriteDebug(result.DebugName, result.DebugText);
            if (result.Data != null)
                service.Data = result.Data;
            service.Status = result.Status ?? (service.Data == null ? "fetch_error" : service.Data.Status);
            if (result.RateLimitedUntil.HasValue)
            {
                service.RateLimitedUntil = result.RateLimitedUntil;
                service.Status = "rate_limited";
            }
            else if (service.Status != "fetch_error")
            {
                service.RateLimitedUntil = null;
            }
            service.LastRefresh = DateTime.Now;
        }

        static string TryReadFileWithRetry(string path)
        {
            for (int i = 0; i < 3; i++)
            {
                try { return File.ReadAllText(path); }
                catch (FileNotFoundException) { return null; }
                catch (DirectoryNotFoundException) { return null; }
                catch (IOException) { System.Threading.Thread.Sleep(50); }
                catch { return null; }
            }
            return null;
        }

        static bool IsCliAvailable(string exeName)
        {
            lock (cliCacheLock)
            {
                bool cached;
                if (cliAvailabilityCache.TryGetValue(exeName, out cached)) return cached;
            }
            bool result = false;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c where " + exeName)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p.WaitForExit(2000)) result = p.ExitCode == 0;
                    else { try { p.Kill(); } catch { } }
                }
            }
            catch { result = false; }
            lock (cliCacheLock) { cliAvailabilityCache[exeName] = result; }
            return result;
        }

        static void InvalidateCliCache(string exeName)
        {
            lock (cliCacheLock) { cliAvailabilityCache.Remove(exeName); }
        }

        async Task OpenLoginAsync(ServiceState service)
        {
            service.ManuallyLoggedOut = false;
            SetLoggedOutSetting(service, false);
            string cliName = service.Name == "Claude" ? "claude" : "codex";
            string loginMethod = service.Name == "Claude" ? settings.ClaudeLoginMethod : settings.CodexLoginMethod;

            bool useCli = string.Equals(loginMethod, "cli", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(loginMethod, "auto", StringComparison.OrdinalIgnoreCase) && IsCliAvailable(cliName));
            bool useBrowser = string.Equals(loginMethod, "browser", StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(loginMethod, "auto", StringComparison.OrdinalIgnoreCase) && !IsCliAvailable(cliName));

            if (useCli)
            {
                if (!IsCliAvailable(cliName))
                {
                    service.Status = "login_required";
                    Invalidate();
                    MessageBox.Show(
                        T("未找到 CLI。请在设置中把登录方式改为浏览器 OAuth。",
                          "CLI was not found. Change the login method to Browser OAuth in Settings."),
                        "Headroom", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string title  = T("Headroom：完成登录后可关闭此窗口",
                                  "Headroom: close this window after sign-in");
                string banner = service.Name == "Claude"
                    ? T("[Headroom] 请在下方输入 /login 完成登录，完成后可关闭此窗口。",
                        "[Headroom] Type /login below to sign in. You can close this window once login completes.")
                    : T("[Headroom] 浏览器将打开登录页面，完成后可关闭此窗口。",
                        "[Headroom] A browser will open for sign-in. You can close this window once login completes.");
                try
                {
                    CliLoginLauncher.Start(service.Name, title, banner);
                    service.Status = "login_pending";
                    Invalidate();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(T("无法启动 CLI：", "Failed to launch CLI: ") + ex.Message,
                        "Headroom", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }

            if (!useBrowser)
                useBrowser = true;

            service.Status = "login_pending";
            Invalidate();

            bool ok = false;
            try
            {
                if (service.Name == "Claude")
                    ok = await StartClaudePkceLoginAsync(service);
                else
                    ok = await StartCodexPkceLoginAsync(service);
            }
            catch (Exception ex)
            {
                WriteDebug(cliName + "-pkce-error.txt", ex.ToString());
            }

            if (ok)
            {
                SetupCredentialWatchers();
                service.ManuallyLoggedOut = false;
                service.RateLimitedUntil = null;
                await RefreshServiceAsync(service, true);
            }
            else if (service.Status == "login_pending")
            {
                service.Status = "login_required";
                Invalidate();
            }
        }

        Task LogoutServiceAsync(ServiceState service)
        {
            MarkLoggedOut(service);
            service.ManuallyLoggedOut = true;
            SetLoggedOutSetting(service, true);
            Invalidate();
            return Task.CompletedTask;
        }

        void MarkLoggedOut(ServiceState service)
        {
            service.Data = new UsageData { Name = service.Name, Source = service.Name + " API", UpdatedAt = DateTime.Now, Status = "login_required" };
            service.Status = "login_required";
            service.LastRefresh = DateTime.MinValue;
            service.RateLimitedUntil = null;
            service.DisplayedFivePct = null;
            service.DisplayedWeekPct = null;
        }

        void SetLoggedOutSetting(ServiceState service, bool value)
        {
            if (service.Name == "Claude") settings.ClaudeLoggedOut = value;
            else settings.CodexLoggedOut = value;
            settings.Save();
        }

        void SetupCredentialWatchers()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            try
            {
                string claudeDir = Path.Combine(userProfile, ".claude");
                Directory.CreateDirectory(claudeDir);
                if (claudeCredWatcher != null)
                {
                    try { claudeCredWatcher.Dispose(); } catch { }
                    claudeCredWatcher = null;
                }
                claudeCredWatcher = new FileSystemWatcher(claudeDir, ".credentials.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                };
                claudeCredWatcher.Changed += (s, e) => OnClaudeCredentialChanged();
                claudeCredWatcher.Created += (s, e) => OnClaudeCredentialChanged();
            }
            catch (Exception ex)
            {
                WriteDebug("watcher-claude-error.txt", ex.ToString());
            }
            try
            {
                string codexDir = Path.Combine(userProfile, ".codex");
                Directory.CreateDirectory(codexDir);
                if (codexCredWatcher != null)
                {
                    try { codexCredWatcher.Dispose(); } catch { }
                    codexCredWatcher = null;
                }
                codexCredWatcher = new FileSystemWatcher(codexDir, "auth.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                };
                codexCredWatcher.Changed += (s, e) => OnCodexCredentialChanged();
                codexCredWatcher.Created += (s, e) => OnCodexCredentialChanged();
            }
            catch (Exception ex)
            {
                WriteDebug("watcher-codex-error.txt", ex.ToString());
            }
        }

        void SetupFixtureWatcher()
        {
            try
            {
                Directory.CreateDirectory(HeadroomOptions.FixtureDir);
                fixtureWatcher = new FileSystemWatcher(HeadroomOptions.FixtureDir, "*.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true,
                };
                fixtureWatcher.Changed += (s, e) => OnFixtureChanged();
                fixtureWatcher.Created += (s, e) => OnFixtureChanged();
                fixtureWatcher.Deleted += (s, e) => OnFixtureChanged();
                fixtureWatcher.Renamed += (s, e) => OnFixtureChanged();
            }
            catch (Exception ex)
            {
                WriteDebug("watcher-fixture-error.txt", ex.ToString());
            }
        }

        void OnClaudeCredentialChanged()
        {
            if ((DateTime.Now - lastClaudeCredNotify).TotalSeconds < 1) return;
            lastClaudeCredNotify = DateTime.Now;
            try
            {
                BeginInvoke(new Action(async () =>
                {
                    claude.ManuallyLoggedOut = false;
                    claude.RateLimitedUntil = null;
                    settings.ClaudeLoggedOut = false;
                    settings.Save();
                    await RefreshServiceAsync(claude, true);
                }));
            }
            catch { }
        }

        void OnCodexCredentialChanged()
        {
            if ((DateTime.Now - lastCodexCredNotify).TotalSeconds < 1) return;
            lastCodexCredNotify = DateTime.Now;
            try
            {
                BeginInvoke(new Action(async () =>
                {
                    codex.ManuallyLoggedOut = false;
                    codex.RateLimitedUntil = null;
                    settings.CodexLoggedOut = false;
                    settings.Save();
                    await RefreshServiceAsync(codex, true);
                }));
            }
            catch { }
        }

        void OnFixtureChanged()
        {
            if ((DateTime.Now - lastFixtureNotify).TotalMilliseconds < 500) return;
            lastFixtureNotify = DateTime.Now;
            try
            {
                BeginInvoke(new Action(async () => await RefreshAllAsync(true)));
            }
            catch { }
        }

        string T(string zh, string en)
        {
            return English ? en : zh;
        }

        internal static void WriteDebug(string name, string text)
        {
            DebugLog.Write(name, text);
        }

        internal static string DebugDirectory
        {
            get { return DebugLog.DirectoryPath; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { runtimeDpi = Math.Max(48, DeviceDpi); } catch { runtimeDpi = 96; }
            dpiChangeActive = true;
            try { ApplyCurrentDpiLayout(); }
            finally { dpiChangeActive = false; }
            try
            {
                DisposeWindowIcons();
                appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                smallWindowIcon = new Icon(appIcon, 16, 16);
                largeWindowIcon = new Icon(appIcon, 48, 48);
                Icon = appIcon;
                trayIcon.Icon = appIcon;
                SendMessageIcon(Handle, 0x80, new IntPtr(0), smallWindowIcon.Handle);
                SendMessageIcon(Handle, 0x80, new IntPtr(1), largeWindowIcon.Handle);
            }
            catch { }
            RenderLayered();
        }

        void DisposeWindowIcons()
        {
            try { if (smallWindowIcon != null) smallWindowIcon.Dispose(); } catch { }
            try { if (largeWindowIcon != null) largeWindowIcon.Dispose(); } catch { }
            try { if (appIcon != null) appIcon.Dispose(); } catch { }
            smallWindowIcon = null;
            largeWindowIcon = null;
            appIcon = null;
        }

        void SetupTrayIcon()
        {
            trayMenu.Items.Clear();

            trayMenu.Items.Add(T(Visible ? "隐藏面板" : "显示面板", Visible ? "Hide widget" : "Show widget"), null, (s, e) =>
            {
                Visible = !Visible;
                if (Visible)
                {
                    if (collapsedToBall) ExpandFromBall();
                    Activate();
                }
                SetupTrayIcon();
            });
            trayMenu.Items.Add(T("立即刷新", "Refresh now"), null, async (s, e) => await RefreshAllAsync(true));
            trayMenu.Items.Add(new ToolStripSeparator());

            var pinItem = new ToolStripMenuItem(T("置顶显示", "Always on top"))
            {
                Checked = settings.AlwaysOnTop,
                CheckOnClick = false
            };
            pinItem.Click += (s, e) =>
            {
                settings.AlwaysOnTop = !settings.AlwaysOnTop;
                TopMost = settings.AlwaysOnTop;
                settings.Save();
                SetupTrayIcon();
                Invalidate();
            };
            trayMenu.Items.Add(pinItem);

            var modeMenu = new ToolStripMenuItem(T("面板模式", "Panel mode"));
            var compactItem = new ToolStripMenuItem(T("紧凑", "Compact"))
            {
                Checked = !string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase)
            };
            compactItem.Click += (s, e) => SetWidgetMode("compact");
            var detailedItem = new ToolStripMenuItem(T("详细", "Detailed"))
            {
                Checked = string.Equals(settings.WidgetMode, "edge", StringComparison.OrdinalIgnoreCase)
            };
            detailedItem.Click += (s, e) => SetWidgetMode("edge");
            modeMenu.DropDownItems.Add(compactItem);
            modeMenu.DropDownItems.Add(detailedItem);
            trayMenu.Items.Add(modeMenu);

            var displayMenu = new ToolStripMenuItem(T("显示额度", "Visible quotas"));
            var claudeItem = new ToolStripMenuItem("Claude") { Checked = settings.ShowClaude };
            claudeItem.Click += (s, e) => SetServiceVisible("Claude", !settings.ShowClaude);
            var codexItem = new ToolStripMenuItem("Codex") { Checked = settings.ShowCodex };
            codexItem.Click += (s, e) => SetServiceVisible("Codex", !settings.ShowCodex);
            displayMenu.DropDownItems.Add(claudeItem);
            displayMenu.DropDownItems.Add(codexItem);
            trayMenu.Items.Add(displayMenu);

            var opacityMenu = new ToolStripMenuItem(T("透明度", "Opacity"));
            AddPercentMenuItems(opacityMenu, new[] { 50, 70, 85, 94, 100 }, settings.OpacityPercent, SetOpacityPercent);
            trayMenu.Items.Add(opacityMenu);

            var sizeMenu = new ToolStripMenuItem(T("整体尺寸", "Overall size"));
            AddPercentMenuItems(sizeMenu, new[] { 75, 90, 100, 115, 130, 150 }, settings.OverallScalePercent, SetOverallScalePercent);
            trayMenu.Items.Add(sizeMenu);

            var behaviorMenu = new ToolStripMenuItem(T("自动隐藏", "Auto hide"));
            var collapseItem = new ToolStripMenuItem(T("离开后缩成悬浮球", "Collapse to quota ball"))
            {
                Checked = settings.CollapseToBall
            };
            collapseItem.Click += (s, e) =>
            {
                settings.CollapseToBall = !settings.CollapseToBall;
                if (!settings.CollapseToBall && collapsedToBall) ExpandFromBall();
                settings.Save();
                SetupTrayIcon();
            };
            var edgeHideItem = new ToolStripMenuItem(T("悬浮球贴外缘缩边", "Retract ball at outer edge"))
            {
                Checked = settings.EdgeAutoHide,
                Enabled = settings.CollapseToBall
            };
            edgeHideItem.Click += (s, e) =>
            {
                settings.EdgeAutoHide = !settings.EdgeAutoHide;
                if (collapsedToBall)
                {
                    if (settings.EdgeAutoHide)
                    {
                        Rectangle expandedBounds = new Rectangle(expandedLocation, expandedSize);
                        Screen sourceScreen = Screen.FromRectangle(expandedBounds);
                        collapsedScreenDeviceName = sourceScreen.DeviceName;
                        collapsedWorkArea = sourceScreen.WorkingArea;
                        collapsedDockEdge = NearestDockEdge(expandedBounds, collapsedWorkArea, UiScale(16));
                        if (!IsOuterDockEdge(sourceScreen, collapsedDockEdge))
                            collapsedDockEdge = "";
                    }
                    else
                    {
                        collapsedDockEdge = "";
                    }
                    ResizeCollapsedBallForCurrentDpi();
                }
                settings.Save();
                SetupTrayIcon();
            };
            behaviorMenu.DropDownItems.Add(collapseItem);
            behaviorMenu.DropDownItems.Add(edgeHideItem);
            trayMenu.Items.Add(behaviorMenu);

            var languageMenu = new ToolStripMenuItem(T("语言", "Language"));
            var chineseItem = new ToolStripMenuItem("简体中文") { Checked = !English };
            chineseItem.Click += (s, e) => SetLanguage("zh-CN");
            var englishItem = new ToolStripMenuItem("English") { Checked = English };
            englishItem.Click += (s, e) => SetLanguage("en");
            languageMenu.DropDownItems.Add(chineseItem);
            languageMenu.DropDownItems.Add(englishItem);
            trayMenu.Items.Add(languageMenu);

            trayMenu.Items.Add(T("完整设置…", "Settings…"), null, async (s, e) => await ShowSettingsDialog());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add(T("退出 Headroom", "Exit Headroom"), null, (s, e) => Close());

            trayIcon.Text = "Headroom";
            if (trayIcon.Icon == null)
                trayIcon.Icon = Icon ?? SystemIcons.Application;
            trayIcon.ContextMenuStrip = trayMenu;
            trayIcon.Visible = true;
        }

        void AddPercentMenuItems(ToolStripMenuItem parent, int[] values, int current, Action<int> apply)
        {
            foreach (int value in values)
            {
                var item = new ToolStripMenuItem(value + "%") { Checked = value == current };
                int captured = value;
                item.Click += (s, e) => apply(captured);
                parent.DropDownItems.Add(item);
            }
        }

        void SetWidgetMode(string mode)
        {
            settings.WidgetMode = string.Equals(mode, "edge", StringComparison.OrdinalIgnoreCase) ? "edge" : "compact";
            ApplyIdealSize();
            settings.Save();
            SetupTrayIcon();
            Invalidate();
        }

        void SetServiceVisible(string service, bool visible)
        {
            if (service == "Claude") settings.ShowClaude = visible;
            else settings.ShowCodex = visible;
            if (!settings.ShowClaude && !settings.ShowCodex)
            {
                if (service == "Claude") settings.ShowCodex = true;
                else settings.ShowClaude = true;
            }
            ApplyIdealSize();
            settings.Save();
            SetupTrayIcon();
            Invalidate();
        }

        void SetOpacityPercent(int value)
        {
            settings.OpacityPercent = Math.Max(35, Math.Min(100, value));
            settings.Save();
            SetupTrayIcon();
            Invalidate();
        }

        void SetOverallScalePercent(int value)
        {
            settings.OverallScalePercent = Math.Max(70, Math.Min(150, value));
            ApplyIdealSize();
            settings.Save();
            SetupTrayIcon();
            Invalidate();
        }

        void SetLanguage(string language)
        {
            settings.Language = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "zh-CN";
            settings.Save();
            SetupTrayIcon();
            Invalidate();
        }

        void OnTrayIconDoubleClick(object sender, EventArgs e)
        {
            Visible = true;
            if (collapsedToBall) ExpandFromBall();
            Activate();
            SetupTrayIcon();
        }
    }
}
