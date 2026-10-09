using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinPeLauncher.Services
{
    internal static class ScreenCapture
    {
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h,
            IntPtr hdcSrc, int xSrc, int ySrc, uint rop);
        [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CURSORINFO pci);
        [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);
        [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr hdc, int x, int y,
            IntPtr hIcon, int cxWidth, int cyWidth, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);

        private const uint SRCCOPY = 0x00CC0020;
        private const uint CAPTUREBLT = 0x40000000;
        private const int CURSOR_SHOWING = 0x00000001;
        private const int DI_NORMAL = 0x0003;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public int fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        internal static string CapturePrimary()
        {
            Rectangle r = Screen.PrimaryScreen.Bounds;
            using (Bitmap bmp = CaptureScreen(r))
            {
                string root = MediaRoot();
                string dir = Path.Combine(root, "Screenshots");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "IMG-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".jpg");
                SaveJpeg(bmp, file, 92);
                return file;
            }
        }

        private static Bitmap CaptureScreen(Rectangle r)
        {
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBmp = CreateCompatibleBitmap(screenDc, r.Width, r.Height);
            IntPtr old = SelectObject(memDc, hBmp);
            try
            {
                BitBlt(memDc, 0, 0, r.Width, r.Height, screenDc, r.X, r.Y, SRCCOPY | CAPTUREBLT);
                Bitmap tmp = Image.FromHbitmap(hBmp);
                Bitmap copy = new Bitmap(tmp);
                tmp.Dispose();
                DrawCursor(copy, r);
                return copy;
            }
            finally
            {
                SelectObject(memDc, old);
                DeleteObject(hBmp);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // BitBlt does not include the mouse cursor; composite it on top afterwards.
        private static void DrawCursor(Bitmap bmp, Rectangle screenRect)
        {
            try
            {
                CURSORINFO ci = new CURSORINFO();
                ci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                if (!GetCursorInfo(ref ci)) return;
                if (ci.flags != CURSOR_SHOWING || ci.hCursor == IntPtr.Zero) return;

                int x = ci.ptScreenPos.X - screenRect.X;
                int y = ci.ptScreenPos.Y - screenRect.Y;

                // ptScreenPos is the hotspot; shift so the drawn icon lands correctly.
                ICONINFO ii;
                if (GetIconInfo(ci.hCursor, out ii))
                {
                    x -= ii.xHotspot;
                    y -= ii.yHotspot;
                    if (ii.hbmMask != IntPtr.Zero) DeleteObject(ii.hbmMask);
                    if (ii.hbmColor != IntPtr.Zero) DeleteObject(ii.hbmColor);
                }

                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    try { DrawIconEx(hdc, x, y, ci.hCursor, 0, 0, 0, IntPtr.Zero, DI_NORMAL); }
                    finally { g.ReleaseHdc(hdc); }
                }
            }
            catch { }
        }

        internal static string MediaRoot()
        {
            string removable = null;
            try
            {
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady) continue;
                        if (d.DriveType == DriveType.Network || d.DriveType == DriveType.Ram) continue;
                        string root = d.RootDirectory.FullName;
                        if (Directory.Exists(Path.Combine(root, "CORESYSTEM"))) return root;
                        if (d.DriveType == DriveType.Removable && removable == null) removable = root;
                    }
                    catch { }
                }
            }
            catch { }
            if (removable != null) return removable;
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static void SaveJpeg(Bitmap bmp, string path, int quality)
        {
            ImageCodecInfo jpeg = null;
            foreach (ImageCodecInfo c in ImageCodecInfo.GetImageEncoders())
            {
                if (c.FormatID == ImageFormat.Jpeg.Guid) { jpeg = c; break; }
            }
            if (jpeg == null)
            {
                bmp.Save(path, ImageFormat.Jpeg);
                return;
            }
            using (EncoderParameters p = new EncoderParameters(1))
            {
                p.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
                bmp.Save(path, jpeg, p);
            }
        }
    }
}
