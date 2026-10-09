using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinPeLauncher
{
    // Docks an AntdUI modal to the bottom-right corner, just above the launcher bar.
    // AntdUI's Modal has no public position option and its default centering (on the
    // thin, full-width bar) lands the dialog at the bottom-centre, partly off-screen.
    //
    // To avoid a visible "appear centred, then jump" flash we do not move the modal
    // after it is shown. Instead we hand AntdUI a transparent anchor form the size of
    // the modal, placed at the target corner: the modal is centred on that anchor and
    // is therefore *born* at the bottom-right. A UI-thread timer then only refines the
    // final position and paints the accent ring (DialogBorder).
    internal static class DialogDock
    {
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();

        private static Timer _timer;
        private static Control _anchor;
        private static AntdUI.Modal.Config _config;
        private static Color _border;
        private static Rectangle _barBounds;
        private static int _margin;
        private static int _ticks;
        private static int _settle;
        private static bool _pinned;
        private static DockAnchor _helper;

        internal static void Dock(Control anchor, AntdUI.Modal.Config config, Color border)
        {
            try
            {
                Stop();
                Form bar = anchor.FindForm();
                if (bar == null) return;
                _anchor = anchor;
                _config = config;
                _border = border;
                _margin = Scale(anchor, 8);
                _barBounds = bar.Bounds;
                _ticks = 0;
                _settle = 0;
                _pinned = false;

                // Place a transparent anchor so AntdUI centres the modal at the target
                // corner from the start (no post-show move / flash).
                try
                {
                    Size outer = MeasureModal(config, anchor);
                    if (outer.Width > 0 && outer.Height > 0)
                    {
                        // 1x1 invisible anchor at the target centre: AntdUI centres the
                        // modal on it, so the modal is born with its bottom-right at the
                        // corner. Keeping it tiny avoids any visible rectangle if the
                        // layered opacity is not honoured (no DWM composition).
                        int cx = _barBounds.Right - _margin - outer.Width / 2;
                        int cy = _barBounds.Top - _margin - outer.Height / 2;
                        _helper = new DockAnchor();
                        _helper.ShowInTaskbar = false;
                        _helper.FormBorderStyle = FormBorderStyle.None;
                        _helper.StartPosition = FormStartPosition.Manual;
                        _helper.Opacity = 0.0;
                        _helper.BackColor = Color.Black;
                        _helper.TopMost = true;
                        _helper.Bounds = new Rectangle(cx, cy, 1, 1);
                        _helper.Show();
                        config.Target = new AntdUI.Target(_helper);
                    }
                }
                catch { }

                _timer = new Timer();
                _timer.Interval = 40;
                _timer.Tick += OnTick;
                _timer.Start();
            }
            catch { }
        }

        // Stop only the repositioning timer; the anchor form must stay alive while the
        // modal is open (the modal is owned by it, so disposing it would close the dialog).
        private static void HaltTimer()
        {
            try
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.Tick -= OnTick;
                    _timer.Dispose();
                    _timer = null;
                }
            }
            catch { }
        }

        // Called from every dialog's finally, once Modal.open has returned.
        internal static void Stop()
        {
            HaltTimer();
            try
            {
                if (_helper != null)
                {
                    _helper.Hide();
                    _helper.Dispose();
                    _helper = null;
                }
            }
            catch { }
            _anchor = null;
            _config = null;
        }

        private static void OnTick(object sender, EventArgs e)
        {
            try
            {
                _ticks++;
                Form modal = ResolveModal();
                if (modal != null && modal.IsHandleCreated && !modal.IsDisposed)
                {
                    if (!modal.TopMost) modal.TopMost = true;
                    // Pin the position while it settles (~400ms after it first appears),
                    // then stop moving it (so it stays where the user puts it) - but keep
                    // the accent ring in sync with wherever the dialog actually is.
                    if (!_pinned)
                    {
                        int x = _barBounds.Right - modal.Width - _margin;
                        int y = _barBounds.Top - modal.Height - _margin;
                        if (modal.Location.X != x || modal.Location.Y != y)
                            modal.Location = new Point(x, y);
                        _settle++;
                        if (_settle >= 10) _pinned = true;
                    }
                    DialogBorder.ShowOrUpdate(_anchor, modal.Bounds, _border);
                }
            }
            catch { }
        }

        private static Form ResolveModal()
        {
            try
            {
                if (_config != null)
                {
                    FieldInfo fi = typeof(AntdUI.Modal.Config).GetField(
                        "Layered",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (fi != null)
                    {
                        Form f = fi.GetValue(_config) as Form;
                        if (f != null) return f;
                    }
                }
            }
            catch { }
            // Fallback: the modal is the active window during ShowDialog.
            try
            {
                IntPtr hwnd = GetActiveWindow();
                if (hwnd != IntPtr.Zero)
                {
                    Form f = Control.FromHandle(hwnd) as Form;
                    if (f != null && f.Height > _barBounds.Height + 20 && f.Width >= 200) return f;
                }
            }
            catch { }
            return null;
        }

        // Replicates AntdUI's LayeredFormModal outer-size math for a Control-content
        // dialog with a title, so the anchor can be sized to the modal and the modal
        // lands exactly at the target corner.
        private static Size MeasureModal(AntdUI.Modal.Config config, Control anchor)
        {
            try
            {
                Control content = config.Content as Control;
                if (content == null) return Size.Empty;
                float dpi = 1f;
                using (Graphics g = anchor.CreateGraphics()) dpi = g.DpiX / 96f;
                if (dpi <= 0f || float.IsNaN(dpi) || float.IsInfinity(dpi)) dpi = 1f;

                int paddingx = (int)Math.Round(config.Padding.Width * dpi);
                int paddingy = (int)Math.Round(config.Padding.Height * dpi);
                int cpaddingx2 = (int)Math.Round(config.ContentPadding.Width * dpi) * 2;
                int cpaddingy2 = (int)Math.Round(config.ContentPadding.Height * dpi) * 2;
                int butt_h = (int)Math.Round(config.BtnHeight * dpi);
                int gap = (int)Math.Round(8f * dpi);

                int w = content.Width + paddingx * 2 + cpaddingx2;

                Font font = config.Font ?? DialogUi.Font;
                int titleH;
                using (Font titleFont = new Font(font.FontFamily, font.Size * 1.14f, FontStyle.Bold))
                {
                    titleH = TextRenderer.MeasureText(config.Title ?? "", titleFont, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
                }
                int h = titleH + gap + content.Height + butt_h + cpaddingy2 + paddingy * 2;
                return new Size(w, h);
            }
            catch { return Size.Empty; }
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

        private sealed class DockAnchor : Form
        {
            private const int WS_EX_NOACTIVATE = 0x08000000;
            private const int WS_EX_TOOLWINDOW = 0x00000080;

            protected override bool ShowWithoutActivation { get { return true; } }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
                    return cp;
                }
            }
        }
    }
}
