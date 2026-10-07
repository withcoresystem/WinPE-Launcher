using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinPeLauncher
{
    internal static class DialogBorder
    {
        private static readonly object _lock = new object();
        private static BorderForm _form;
        private static Timer _timer;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;

        internal static void ShowOrUpdate(Control anchor, Rectangle screenRect, Color color)
        {
            if (screenRect.Width <= 0 || screenRect.Height <= 0) return;
            try
            {
                anchor.BeginInvoke((Action)(() =>
                {
                    lock (_lock)
                    {
                        try
                        {
                            if (_form == null || _form.IsDisposed)
                            {
                                DisposeUnlocked();
                                BorderForm f = new BorderForm();
                                f.StartPosition = FormStartPosition.Manual;
                                f.ShowInTaskbar = false;
                                f.FormBorderStyle = FormBorderStyle.None;
                                f.BackColor = color;
                                f.Bounds = screenRect;
                                f.TopMost = true;
                                ApplyRegion(f, screenRect);
                                f.Show();
                                SetWindowPos(f.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                                _form = f;

                                _timer = new Timer();
                                _timer.Interval = 500;
                                _timer.Tick += (s, e) =>
                                {
                                    lock (_lock)
                                    {
                                        if (_form != null && !_form.IsDisposed)
                                            try { SetWindowPos(_form.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE); } catch { }
                                    }
                                };
                                _timer.Start();
                            }
                            else if (_form.Bounds != screenRect)
                            {
                                _form.Bounds = screenRect;
                                ApplyRegion(_form, screenRect);
                                SetWindowPos(_form.Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                            }
                        }
                        catch { }
                    }
                }));
            }
            catch { }
        }

        internal static void Hide()
        {
            try
            {
                lock (_lock) { DisposeUnlocked(); }
            }
            catch { }
        }

        private static void ApplyRegion(Form f, Rectangle r)
        {
            using (GraphicsPath gp = Ring(r.Width, r.Height, 2, 0))
            {
                Region old = f.Region;
                f.Region = new Region(gp);
                if (old != null) old.Dispose();
            }
        }

        private static void DisposeUnlocked()
        {
            try
            {
                if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
                if (_form != null) { _form.Hide(); _form.Dispose(); }
            }
            catch { }
            _form = null;
        }

        private static GraphicsPath Ring(int w, int h, int t, int r)
        {
            GraphicsPath gp = new GraphicsPath(FillMode.Alternate);
            AddRound(gp, new Rectangle(0, 0, w, h), r);
            AddRound(gp, new Rectangle(t, t, Math.Max(1, w - 2 * t), Math.Max(1, h - 2 * t)), Math.Max(0, r - t));
            return gp;
        }

        private static void AddRound(GraphicsPath gp, Rectangle r, int rad)
        {
            int d = rad * 2;
            if (d <= 0 || r.Width <= d || r.Height <= d)
            {
                gp.AddRectangle(r);
                return;
            }
            gp.AddArc(r.Left, r.Top, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
        }

        private sealed class BorderForm : Form
        {
            private const int WM_NCHITTEST = 0x0084;
            private const int HTTRANSPARENT = -1;

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x08000000 | 0x80;
                    return cp;
                }
            }

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_NCHITTEST)
                {
                    m.Result = (IntPtr)HTTRANSPARENT;
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
