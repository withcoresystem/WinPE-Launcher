using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinPeLauncher
{
    internal static class DialogDock
    {
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out Rect r);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        private delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        private struct Rect { public int L, T, R, B; }

        internal static void Dock(Control anchor, Color border)
        {
            try
            {
                Form bar = anchor.FindForm();
                if (bar == null) return;
                int pid = Process.GetCurrentProcess().Id;
                int margin = Scale(anchor, 8);
                Rectangle barBounds = bar.Bounds;
                System.Threading.Thread t = new System.Threading.Thread(() =>
                {
                    try
                    {
                        IntPtr found = IntPtr.Zero;
                        for (int i = 0; i < 600 && found == IntPtr.Zero; i++)
                        {
                            uint self = (uint)pid;
                            EnumWindows((h, l) =>
                            {
                                if (!IsWindowVisible(h)) return true;
                                uint wpid;
                                GetWindowThreadProcessId(h, out wpid);
                                if (wpid != self) return true;
                                Rect r;
                                if (!GetWindowRect(h, out r)) return true;
                                int w = r.R - r.L, hh = r.B - r.T;
                                if (hh <= barBounds.Height + 20) return true;
                                if (w < 200) return true;
                                found = h;
                                return false;
                            }, IntPtr.Zero);
                            if (found == IntPtr.Zero) System.Threading.Thread.Sleep(5);
                        }
                        if (found == IntPtr.Zero) return;

                        for (int i = 0; i < 15; i++)
                        {
                            Rect r;
                            if (GetWindowRect(found, out r))
                            {
                                int w = r.R - r.L, hh = r.B - r.T;
                                int x = barBounds.Right - w - margin;
                                int y = barBounds.Top - hh - margin;
                                SetWindowPos(found, IntPtr.Zero, x, y, 0, 0, 0x0001 | 0x0004 | 0x0010);
                                if (GetWindowRect(found, out r))
                                    DialogBorder.ShowOrUpdate(anchor, Rectangle.FromLTRB(r.L, r.T, r.R, r.B), border);
                            }
                            System.Threading.Thread.Sleep(40);
                        }
                    }
                    catch { }
                });
                t.IsBackground = true;
                t.Start();
            }
            catch { }
        }

        private static int Scale(Control anchor, int px)
        {
            try
            {
                using (Graphics g = anchor.CreateGraphics())
                    return (int)Math.Round(px * g.DpiX / 96f);
            }
            catch { return px; }
        }
    }
}
