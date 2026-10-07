using System;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;

namespace WinPeLauncher.Services
{
    internal enum TimeSyncResult
    {
        Synced,
        Accurate,
        Busy,
        NoInternet,
        Failed
    }

    internal static class TimeSyncService
    {
        private static readonly string[] Sources =
        {
            "http://www.google.com",
            "http://www.microsoft.com",
            "http://www.cloudflare.com",
            "http://esd.coresystem.vn"
        };

        private const int ThresholdMinutes = 5;
        private const int SourceTimeoutMs = 10000;

        private static readonly object Gate = new object();
        private static bool _busy;
        private static DateTime _lastSuccessUtc = DateTime.MinValue;

        internal static TimeSyncResult Sync(bool force, out string message)
        {
            message = "";
            lock (Gate)
            {
                if (_busy)
                {
                    message = "Time sync already running";
                    return TimeSyncResult.Busy;
                }
                _busy = true;
            }
            try
            {
                if (!force && (DateTime.UtcNow - _lastSuccessUtc).TotalSeconds < 60)
                {
                    message = "Clock synchronized recently";
                    return TimeSyncResult.Accurate;
                }

                DateTime? server = GetInternetDateTime();
                if (server == null)
                {
                    message = "No internet time source reachable";
                    return TimeSyncResult.NoInternet;
                }

                double diffMinutes = Math.Abs((server.Value - DateTime.UtcNow).TotalMinutes);
                if (diffMinutes <= ThresholdMinutes)
                {
                    _lastSuccessUtc = DateTime.UtcNow;
                    message = "Clock accurate (" + diffMinutes.ToString("0.0") + " min off)";
                    return TimeSyncResult.Accurate;
                }

                if (!IsWinPE)
                {
                    message = "Clock off by " + diffMinutes.ToString("0.0") + " min - clock change allowed only in WinPE";
                    return TimeSyncResult.Failed;
                }

                if (SetSystemClock(server.Value))
                {
                    _lastSuccessUtc = DateTime.UtcNow;
                    message = "Time synchronized";
                    return TimeSyncResult.Synced;
                }

                message = "Failed to set system clock";
                return TimeSyncResult.Failed;
            }
            catch (Exception ex)
            {
                message = "Time sync failed: " + ex.Message;
                return TimeSyncResult.Failed;
            }
            finally
            {
                lock (Gate) { _busy = false; }
            }
        }

        private static DateTime? GetInternetDateTime()
        {
            for (int i = 0; i < Sources.Length; i++)
            {
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Sources[i]);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    req.Timeout = SourceTimeoutMs;
                    req.ReadWriteTimeout = SourceTimeoutMs;
                    req.UserAgent = "WINPE-LAUNCHER/1.0";
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        string date = resp.Headers[HttpResponseHeader.Date];
                        DateTime parsed;
                        if (!string.IsNullOrEmpty(date) &&
                            DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out parsed))
                            return parsed.ToUniversalTime();
                    }
                }
                catch { }
            }
            return null;
        }

        private static bool IsWinPE
        {
            get
            {
                return string.Equals(Environment.GetEnvironmentVariable("SystemDrive"), "X:", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static bool SetSystemClock(DateTime utc)
        {
            try
            {
                SYSTEMTIME st = new SYSTEMTIME();
                st.wYear = (ushort)utc.Year;
                st.wMonth = (ushort)utc.Month;
                st.wDay = (ushort)utc.Day;
                st.wHour = (ushort)utc.Hour;
                st.wMinute = (ushort)utc.Minute;
                st.wSecond = (ushort)utc.Second;
                st.wMilliseconds = (ushort)utc.Millisecond;
                return SetSystemTime(ref st);
            }
            catch
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetSystemTime(ref SYSTEMTIME lpSystemTime);
    }
}
