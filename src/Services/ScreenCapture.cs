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

        private const uint SRCCOPY = 0x00CC0020;
        private const uint CAPTUREBLT = 0x40000000;

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
