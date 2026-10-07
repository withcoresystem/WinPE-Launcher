using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WinPeLauncher.Services
{
    // Runs the PowerShell 5.1 engine (chat-engine.ps1) - one process per exchange.
    internal static class ChatEngine
    {
        private static readonly object _lock = new object();
        private static string _scriptPath;

        // Temporary session: %TEMP% on WinPE is a RAM disk -> lost on reboot.
        internal static string SessionPath
        {
            get { return Path.Combine(Path.GetTempPath(), "launcher-ng-chat.jsonl"); }
        }

        internal static string EnsureScript()
        {
            lock (_lock)
            {
                if (_scriptPath != null && File.Exists(_scriptPath)) return _scriptPath;
                string path = Path.Combine(Path.GetTempPath(), "launcher-ng-engine.ps1");
                try
                {
                    using (Stream s = typeof(ChatEngine).Assembly.GetManifestResourceStream("chat-engine.ps1"))
                    {
                        if (s == null) return null;
                        using (FileStream f = File.Create(path))
                        {
                            byte[] buf = new byte[8192];
                            int n;
                            while ((n = s.Read(buf, 0, buf.Length)) > 0) f.Write(buf, 0, n);
                        }
                    }
                    _scriptPath = path;
                }
                catch { return null; }
                return _scriptPath;
            }
        }

        // Runs on a background thread, returns the reply (UTF-8). Never throws to the caller.
        internal static Task<string> AskAsync(string prompt)
        {
            return Task.Run(() => Ask(prompt));
        }

        private static string Ask(string prompt)
        {
            try
            {
                string script = EnsureScript();
                if (script == null) return "Error: could not extract the engine (resource).";

                string promptFile = Path.Combine(Path.GetTempPath(), "launcher-ng-prompt.txt");
                File.WriteAllText(promptFile, prompt ?? "", new UTF8Encoding(false));
                string exe = Assembly.GetExecutingAssembly().Location;

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\""
                    + " -PromptFile \"" + promptFile + "\""
                    + " -SessionFile \"" + SessionPath + "\""
                    + " -LauncherExe \"" + exe + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                using (Process p = Process.Start(psi))
                {
                    if (p == null) return "Error: could not start PowerShell.";
                    string output = p.StandardOutput.ReadToEnd();
                    string err = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(70000))
                    {
                        try { p.Kill(); } catch { }
                        return "Error: engine timed out after 70 seconds (connection hung).";
                    }
                    p.WaitForExit(500);

                    string result = (output ?? "").Trim();
                    if (result.Length == 0)
                    {
                        string e = (err ?? "").Trim();
                        if (e.Length > 0) return "Error (engine): " + FirstLine(e);
                        return "Error: no response received (check endpoint/model).";
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                return "Error (engine): " + ex.Message;
            }
        }

        private static string FirstLine(string s)
        {
            int i = s.IndexOf('\n');
            return i < 0 ? s : s.Substring(0, i).TrimEnd('\r');
        }
    }
}
