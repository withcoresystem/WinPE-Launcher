using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WinPeLauncher.Services
{
    internal static class WifiService
    {
        internal static string LastError = "";

        internal static List<string> ScanNetworks()
        {
            List<string> ssids = new List<string>();
            LastError = "";
            try
            {
                StartWlan();
                Thread.Sleep(1000);

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    string output = Exec.Run("netsh", "wlan show networks", 10000);
                    if (output == "[TIMEOUT]")
                    {
                        LastError = "WLAN not responding (timeout) - try restarting WLAN service";
                        continue;
                    }
                    string[] lines = output.Split('\n', '\r');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string trimmed = lines[i].Trim();
                        Match match = Regex.Match(trimmed, @"^SSID\s+\d*\s*:\s*(.+)$", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            string ssid = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(ssid)) ssids.Add(ssid);
                        }
                    }
                    if (ssids.Count > 0) break;

                    LastError = "Attempt " + (attempt + 1) + "/2: no networks found";
                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                LastError = "[EXCEPTION] " + ex.Message;
            }

            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> result = new List<string>();
            foreach (string s in ssids)
            {
                if (seen.Add(s)) result.Add(s);
            }
            result.Sort();
            return result;
        }

        internal static bool AdapterDetected()
        {
            try
            {
                StartWlan();
                string output = Exec.Run("netsh", "wlan show interfaces", 3000);
                return output.Contains("State") || output.Contains("SSID") || output.Contains("BSSID");
            }
            catch
            {
                return false;
            }
        }

        internal static string Connect(string ssid, string password)
        {
            LastError = "";
            string tempPath = Path.Combine(Path.GetTempPath(), "WinPeWifiProfile.xml");
            try
            {
                StartWlan();
                Thread.Sleep(1000);

                string xmlSsid = EscapeXml(ssid);
                string xmlPass = EscapeXml(password);

                string profileXml =
                    "<?xml version=\"1.0\"?>\r\n" +
                    "<WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\">\r\n" +
                    "    <name>" + xmlSsid + "</name>\r\n" +
                    "    <SSIDConfig><SSID><name>" + xmlSsid + "</name></SSID></SSIDConfig>\r\n" +
                    "    <connectionType>ESS</connectionType>\r\n" +
                    "    <connectionMode>auto</connectionMode>\r\n" +
                    "    <MSM>\r\n" +
                    "        <security>\r\n" +
                    "            <authEncryption><authentication>WPA2PSK</authentication><encryption>AES</encryption><useOneX>false</useOneX></authEncryption>\r\n" +
                    "            <sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>" + xmlPass + "</keyMaterial></sharedKey>\r\n" +
                    "        </security>\r\n" +
                    "    </MSM>\r\n" +
                    "</WLANProfile>";

                File.WriteAllText(tempPath, profileXml, new UTF8Encoding(false));

                Exec.Run("netsh", "wlan delete profile name=\"" + ssid + "\"", 2000);

                string addOut = Exec.Run("netsh", "wlan add profile filename=\"" + tempPath + "\" user=all", 3000);

                string connectOut = Exec.Run("netsh", "wlan connect name=\"" + ssid + "\"", 10000);

                LastError = "";
                if (addOut.Contains("EXITCODE:") && !addOut.Contains("EXITCODE:0"))
                    LastError = "[ADD] " + addOut;
                if (!string.IsNullOrEmpty(connectOut) && !connectOut.Contains("Connection request"))
                    LastError = (LastError.Length > 0 ? LastError + " | " : "") + "[CONNECT] " + connectOut;
                if (LastError.Length == 0) LastError = connectOut;

                for (int i = 0; i < 3; i++)
                {
                    Thread.Sleep(3000);
                    string ipOut = Exec.Run("ipconfig", "", 4000);
                    if (Regex.IsMatch(ipOut, @"IPv4[^:]*:\s*\d+\.\d+\.\d+\.\d+"))
                        return "";
                }

                string intOut = Exec.Run("netsh", "wlan show interfaces", 3000);
                if (intOut.IndexOf(ssid, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    intOut.Contains("State") ||
                    Regex.IsMatch(intOut, @"State\s*:\s*connected", RegexOptions.IgnoreCase))
                    return "Connected but no IP (slow DHCP or none available)";

                return LastError.Length > 0 ? LastError : "No IP address assigned";
            }
            catch (Exception ex)
            {
                return "[EXCEPTION] " + ex.Message;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { }
            }
        }

        private static string EscapeXml(string text)
        {
            return text.Replace("&", "&amp;")
                       .Replace("\"", "&quot;")
                       .Replace("'", "&apos;")
                       .Replace("<", "&lt;")
                       .Replace(">", "&gt;");
        }

        private static void StartWlan()
        {
            Exec.Run("net", "start wlansvc", 5000);
        }
    }
}
