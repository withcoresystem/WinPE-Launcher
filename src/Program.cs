using System;
using System.Windows.Forms;

namespace WinPeLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            // MUST register first: Main() must not contain any AntdUI token
            // (the JIT could load AntdUI before the handler runs).
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            Bootstrap();
        }

        private static void Bootstrap()
        {
            ApplyConfigTimeZone();

            // Font Inter embedded in the exe - before creating any control (WinPE lacks Segoe UI).
            WinPeLauncher.Services.UiFonts.Init();

            // UI scale: apps-config.json "uiScale" (default 1). Neutralise the OS DPI so the
            // chosen scale is authoritative for the bar AND AntdUI dialogs (set before AntdUI
            // is first used, otherwise its DPI cache keeps the system value).
            float dpiScale = 1f;
            try
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
                    dpiScale = g.DpiX / 96f;
            }
            catch { }
            if (dpiScale <= 0f || float.IsNaN(dpiScale) || float.IsInfinity(dpiScale)) dpiScale = 1f;
            float cfgScale = WinPeLauncher.Services.AppConfig.GetUiScale();
            float uiScale = cfgScale > 0f ? cfgScale : 1f;
            if (uiScale <= 0f || float.IsNaN(uiScale) || float.IsInfinity(uiScale)) uiScale = 1f;

            WinPeLauncher.Services.UiFonts.Scale = uiScale;
            WinPeLauncher.Services.UiFonts.DpiScale = dpiScale;

            AntdUI.Localization.Provider = new EnglishLocalization();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Application.Run(new MainForm());
        }

        private static System.Reflection.Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string name = new System.Reflection.AssemblyName(args.Name).Name;
                if (!string.Equals(name, "AntdUI", StringComparison.OrdinalIgnoreCase)) return null;
                foreach (System.Reflection.Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
                    if (loaded.GetName().Name == "AntdUI") return loaded;
                using (System.IO.Stream s = typeof(Program).Assembly.GetManifestResourceStream("AntdUI.dll"))
                {
                    if (s == null) return null;
                    byte[] buf = new byte[s.Length];
                    int read = 0;
                    while (read < buf.Length)
                    {
                        int n = s.Read(buf, read, buf.Length - read);
                        if (n <= 0) break;
                        read += n;
                    }
                    return System.Reflection.Assembly.Load(buf);
                }
            }
            catch { return null; }
        }

        // Apply the timezone from apps-config.json ("timezone") before creating MainForm.
        // WinPE has no tzutil -> use PowerShell Set-TimeZone (async, does not block startup).
        private static void ApplyConfigTimeZone()
        {
            try
            {
                string tz = WinPeLauncher.Services.AppConfig.GetTimeZone();
                if (string.IsNullOrEmpty(tz)) return;
                for (int i = 0; i < tz.Length; i++)
                {
                    char c = tz[i];
                    if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '.' || c == '_' || c == '+' || c == '-')) return;
                }
                if (string.Equals(TimeZoneInfo.Local.Id, tz, StringComparison.OrdinalIgnoreCase)) return;
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -Command \"Set-TimeZone -Id '" + tz + "'\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            ShowError(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ShowError(e.ExceptionObject as Exception);
        }

        private static void ShowError(Exception ex)
        {
            try
            {
                MessageBox.Show(
                    ex == null ? "Unknown error." : ex.ToString(),
                    "WinPE Launcher",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }
    }

    internal sealed class EnglishLocalization : AntdUI.ILocalization
    {
        public string GetLocalizedString(string key)
        {
            if (key == "NoData") return "No applications";
            if (key == "OK") return "OK";
            if (key == "Cancel") return "Cancel";
            return null;
        }
    }
}
