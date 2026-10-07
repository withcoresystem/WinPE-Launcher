using System;
using System.Diagnostics;
using System.Text;

namespace WinPeLauncher.Services
{
    internal static class Exec
    {
        internal static string Run(string fileName, string arguments, int timeoutMs)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(fileName, arguments);
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                using (Process proc = Process.Start(psi))
                {
                    if (proc == null) return "";

                    StringBuilder sb = new StringBuilder();
                    StringBuilder errSb = new StringBuilder();

                    proc.OutputDataReceived += (s, e) => { if (e.Data != null) sb.AppendLine(e.Data); };
                    proc.ErrorDataReceived += (s, e) => { if (e.Data != null) errSb.AppendLine(e.Data); };
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    if (proc.WaitForExit(timeoutMs))
                    {
                        proc.WaitForExit(200);

                        string result = sb.ToString().TrimEnd();
                        string err = errSb.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(err))
                            result += " [STDERR: " + err + "]";
                        result += " [EXITCODE:" + proc.ExitCode + "]";
                        return result;
                    }

                    try { proc.Kill(); } catch { }
                    return "[TIMEOUT]";
                }
            }
            catch (Exception ex)
            {
                return "[ERR] " + ex.Message;
            }
        }
    }
}
