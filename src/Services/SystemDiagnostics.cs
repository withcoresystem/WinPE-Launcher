using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinPeLauncher.Services
{
    internal sealed class DiagRow
    {
        internal string Key;
        internal string Value;
    }

    internal sealed class DiagSection
    {
        internal string Name;
        internal readonly List<DiagRow> Rows = new List<DiagRow>();
    }

    internal static class SystemDiagnostics
    {
        private static readonly object _lock = new object();
        private static string _scriptPath;

        internal static List<DiagSection> LastResult { get; private set; }

        internal static Task<List<DiagSection>> CollectAsync()
        {
            return Task.Run(() => Collect());
        }

        private static string EnsureScript()
        {
            lock (_lock)
            {
                if (_scriptPath != null && File.Exists(_scriptPath)) return _scriptPath;
                string path = Path.Combine(Path.GetTempPath(), "launcher-ng-diag.ps1");
                try
                {
                    using (Stream s = typeof(SystemDiagnostics).Assembly.GetManifestResourceStream("diag-engine.ps1"))
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

        private static List<DiagSection> Collect()
        {
            List<DiagSection> sections = new List<DiagSection>();
            try
            {
                string script = EnsureScript();
                if (script == null)
                {
                    AddSimple(sections, "Diagnostic", "Error", "engine resource missing");
                    return sections;
                }

                string outFile = Path.Combine(Path.GetTempPath(), "launcher-ng-diag.txt");
                try { File.Delete(outFile); } catch { }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -OutFile \"" + outFile + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                using (Process p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        AddSimple(sections, "Diagnostic", "Error", "could not start PowerShell");
                        return sections;
                    }
                    p.StandardOutput.ReadToEnd();
                    p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(90000))
                    {
                        try { p.Kill(); } catch { }
                        AddSimple(sections, "Diagnostic", "Warning", "collection timed out");
                    }
                }

                if (File.Exists(outFile))
                {
                    foreach (string raw in File.ReadAllLines(outFile, Encoding.UTF8))
                        ParseLine(sections, raw);
                }
                else
                {
                    AddSimple(sections, "Diagnostic", "Error", "no output produced");
                }
            }
            catch (Exception ex)
            {
                AddSimple(sections, "Diagnostic", "Error", ex.Message);
            }

            AppendBitLocker(sections);
            LastResult = sections;
            return sections;
        }

        private static void ParseLine(List<DiagSection> sections, string raw)
        {
            if (string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split('\t');
            if (parts.Length < 3) return;

            string section = parts[0].Trim();
            string key = parts[1].Trim();
            string value = parts.Length > 3 ? string.Join("\t", parts, 2, parts.Length - 2) : parts[2];
            if (section.Length == 0) return;

            DiagSection sec = sections.Count > 0 ? sections[sections.Count - 1] : null;
            if (sec == null || sec.Name != section)
            {
                sec = new DiagSection { Name = section };
                sections.Add(sec);
            }
            sec.Rows.Add(new DiagRow { Key = key, Value = value });
        }

        private static void AddSimple(List<DiagSection> sections, string section, string key, string value)
        {
            DiagSection sec = sections.Find(s => s.Name == section);
            if (sec == null) { sec = new DiagSection { Name = section }; sections.Add(sec); }
            sec.Rows.Add(new DiagRow { Key = key, Value = value });
        }

        private static void AppendBitLocker(List<DiagSection> sections)
        {
            try
            {
                string output = Exec.Run("manage-bde", "-status", 15000);
                if (string.IsNullOrWhiteSpace(output) || output == "[TIMEOUT]") return;

                List<KeyValuePair<string, string>> badges = new List<KeyValuePair<string, string>>();
                string vol = null, conv = null, prot = null, lockStatus = null;

                foreach (string raw in output.Replace("\r", "").Split('\n'))
                {
                    string line = raw.Trim();
                    Match m = Regex.Match(line, @"^Volume\s+([A-Za-z]:)");
                    if (m.Success)
                    {
                        AddBitLockerBadge(badges, vol, conv, prot, lockStatus);
                        vol = m.Groups[1].Value;
                        conv = prot = lockStatus = null;
                        continue;
                    }
                    if (vol == null) continue;
                    if (line.StartsWith("Conversion Status:")) conv = After(line, "Conversion Status:");
                    else if (line.StartsWith("Protection Status:")) prot = After(line, "Protection Status:");
                    else if (line.StartsWith("Lock Status:")) lockStatus = After(line, "Lock Status:");
                }
                AddBitLockerBadge(badges, vol, conv, prot, lockStatus);

                if (badges.Count == 0) return;

                DiagSection volumes = sections.Find(s => s.Name == "Volumes");
                foreach (KeyValuePair<string, string> kv in badges)
                {
                    bool matched = false;
                    if (volumes != null)
                    {
                        foreach (DiagRow row in volumes.Rows)
                        {
                            string keyTrim = row.Key.Trim();
                            if (keyTrim.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase)
                                && (keyTrim.Length == kv.Key.Length || keyTrim[kv.Key.Length] == ' ' || keyTrim[kv.Key.Length] == '"'))
                            {
                                row.Value = row.Value + "   " + kv.Value;
                                matched = true;
                                break;
                            }
                        }
                    }
                    if (!matched)
                    {
                        if (volumes == null)
                        {
                            volumes = new DiagSection { Name = "Volumes" };
                            sections.Add(volumes);
                        }
                        volumes.Rows.Add(new DiagRow { Key = kv.Key, Value = kv.Value });
                    }
                }
            }
            catch { }
        }

        private static void AddBitLockerBadge(List<KeyValuePair<string, string>> badges,
            string vol, string conv, string prot, string lockStatus)
        {
            if (string.IsNullOrEmpty(vol)) return;
            bool locked = string.Equals(lockStatus, "Locked", StringComparison.OrdinalIgnoreCase);
            bool protectedOn = string.Equals(prot, "Protection On", StringComparison.OrdinalIgnoreCase);
            bool decrypted = !string.IsNullOrEmpty(conv)
                && conv.StartsWith("Fully Decrypted", StringComparison.OrdinalIgnoreCase);
            if (decrypted && !protectedOn && !locked) return;

            string badge = locked ? "[BitLocker: LOCKED]" : (protectedOn ? "[BitLocker: Unlocked]" : "[BitLocker]");
            badges.Add(new KeyValuePair<string, string>(vol, badge));
        }

        private static string After(string line, string label)
        {
            return line.Substring(label.Length).Trim();
        }
    }
}
