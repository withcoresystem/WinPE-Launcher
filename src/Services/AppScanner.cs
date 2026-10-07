using System;
using System.Collections.Generic;
using System.IO;

namespace WinPeLauncher.Services
{
    internal static class AppScanner
    {
        internal static List<AppEntry> Scan()
        {
            List<AppEntry> result = new List<AppEntry>();
            try
            {
                List<string> roots = FindRoots();
                List<AppEntry> config = AppConfig.Load();
                for (int i = 0; i < config.Count; i++)
                {
                    AppEntry entry = config[i];
                    if (!string.IsNullOrEmpty(entry.Path) && !Path.IsPathRooted(entry.Path))
                    {
                        string resolved = Resolve(roots, entry.Path);
                        if (resolved != null) entry.Path = resolved;
                    }
                    entry.Script = IsScript(entry.Path);
                    if (entry.Script && string.IsNullOrEmpty(entry.Icon)) entry.Icon = "CodeOutlined";
                    else if (!entry.Script && string.IsNullOrEmpty(entry.Icon)) entry.Icon = "AppstoreOutlined";
                    result.Add(entry);
                }
            }
            catch { }
            return result;
        }

        private static string Resolve(List<string> roots, string relative)
        {
            string norm;
            try { norm = relative.Replace('/', '\\').TrimStart('\\'); }
            catch { return null; }
            string[] folders = { "Softwares", "Scripts", "" };
            for (int r = 0; r < roots.Count; r++)
            {
                for (int f = 0; f < folders.Length; f++)
                {
                    try
                    {
                        string baseDir = string.IsNullOrEmpty(folders[f])
                            ? Path.Combine(roots[r], "CORESYSTEM")
                            : Path.Combine(roots[r], "CORESYSTEM", folders[f]);
                        string full = Path.Combine(baseDir, norm);
                        if (File.Exists(full)) return full;
                    }
                    catch { }
                }
            }
            return null;
        }

        private static bool IsScript(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                return ext == ".ps1" || ext == ".bat" || ext == ".cmd";
            }
            catch { return false; }
        }

        private static List<string> FindRoots()
        {
            List<string> roots = new List<string>();
            DriveInfo[] drives;
            try { drives = DriveInfo.GetDrives(); }
            catch { return roots; }
            for (int i = 0; i < drives.Length; i++)
            {
                DriveInfo d = drives[i];
                try
                {
                    if (!d.IsReady) continue;
                    if (d.DriveType == DriveType.Network || d.DriveType == DriveType.Ram) continue;
                    roots.Add(d.RootDirectory.FullName);
                }
                catch { }
            }
            return roots;
        }
    }
}
