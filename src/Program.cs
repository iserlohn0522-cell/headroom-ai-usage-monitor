using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Headroom
{
    static class Program
    {
        internal static readonly int ShowExistingMessage = RegisterWindowMessage("Headroom.ShowExisting.v1");
        static readonly IntPtr HwndBroadcast = new IntPtr(0xffff);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);

        [STAThread]
        static void Main()
        {
            HeadroomOptions.Configure(Environment.GetCommandLineArgs());
            string mutexName = HeadroomOptions.FixtureMode
                ? @"Local\Headroom.Fixture.Instance"
                : @"Local\Headroom.Instance";
            bool createdNew;
            using (var instanceMutex = new Mutex(true, mutexName, out createdNew))
            using (var showEvent = new EventWaitHandle(
                false, EventResetMode.AutoReset, mutexName + ".Show"))
            {
                if (!createdNew)
                {
                    showEvent.Set();
                    PostMessage(HwndBroadcast, ShowExistingMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var form = new UsageForm())
                {
                    RegisteredWaitHandle showRegistration = ThreadPool.RegisterWaitForSingleObject(
                        showEvent,
                        (state, timedOut) => ((UsageForm)state).ShowFromSecondInstance(),
                        form,
                        Timeout.Infinite,
                        false);
                    try { Application.Run(form); }
                    finally { showRegistration.Unregister(null); }
                }
                GC.KeepAlive(instanceMutex);
            }
        }
    }

    static class HeadroomOptions
    {
        public static bool FixtureMode { get; private set; }
        public static string FixtureDir { get; private set; }

        public static void Configure(string[] args)
        {
            FixtureDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".headroom-fixture");

            string envDir = Environment.GetEnvironmentVariable("HEADROOM_FIXTURE_DIR");
            if (!string.IsNullOrWhiteSpace(envDir))
            {
                FixtureMode = true;
                FixtureDir = envDir.Trim();
            }

            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i] ?? "";
                if (arg.Equals("--fixture", StringComparison.OrdinalIgnoreCase))
                {
                    FixtureMode = true;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                        FixtureDir = args[++i];
                }
                else if (arg.StartsWith("--fixture=", StringComparison.OrdinalIgnoreCase))
                {
                    FixtureMode = true;
                    FixtureDir = arg.Substring("--fixture=".Length).Trim('"');
                }
            }

            if (!Path.IsPathRooted(FixtureDir))
                FixtureDir = Path.GetFullPath(FixtureDir);
        }
    }
}
