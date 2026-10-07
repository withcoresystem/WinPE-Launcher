using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal sealed class ScrollHost : Panel
    {
        private const int SideMargin = 2;
        private const int BarW = 9;
        private const int BarMargin = 3;
        private const int MinThumb = 28;

        private readonly Timer _hoverTimer = new Timer();
        private Control _content;
        private int _offset;
        private int _contentHeight;
        private bool _barVisible;
        private bool _dragging;
        private int _dragStartY;
        private int _dragStartOffset;

        internal ScrollHost()
        {
            AutoScroll = false;
            BackColor = DialogBack;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            _hoverTimer.Interval = 200;
            _hoverTimer.Tick += (s, e) => UpdateBarVisibility();
        }

        internal void SetContent(Control content)
        {
            _content = content;
            if (content != null)
            {
                Controls.Add(content);
                content.Left = SideMargin;
                content.Top = 0;
                RichTextBox rtb = content as RichTextBox;
                if (rtb != null) rtb.ContentsResized += (s, e) => { if (IsHandleCreated) BeginInvoke((Action)Recalc); };
            }
            Recalc();
        }

        private int ViewportHeight { get { return ClientSize.Height; } }
        private bool NeedsBar { get { return _contentHeight > ViewportHeight; } }
        private int MaxOffset { get { int m = _contentHeight - ViewportHeight; return m < 0 ? 0 : m; } }
        private int InnerW { get { int w = ClientSize.Width - SideMargin * 2 - (BarW + BarMargin); return w < 120 ? 120 : w; } }

        internal void Recalc()
        {
            if (_content == null || IsDisposed) return;
            try
            {
                _content.Width = InnerW;
                _contentHeight = MeasureContentHeight();
                _offset = Clamp(_offset);
                ApplyOffset();
                Invalidate();
            }
            catch { }
        }

        private int MeasureContentHeight()
        {
            try
            {
                RichTextBox rtb = _content as RichTextBox;
                if (rtb != null)
                {
                    if (!rtb.IsHandleCreated) rtb.CreateControl();
                    if (rtb.IsHandleCreated)
                    {
                        rtb.Height = 5000;
                        int idx = rtb.TextLength > 0 ? rtb.TextLength - 1 : 0;
                        int y = rtb.GetPositionFromCharIndex(idx).Y;
                        int h = y + rtb.Font.Height + 6;
                        if (h < rtb.Font.Height + 6) h = rtb.Font.Height + 6;
                        rtb.Height = h;
                        return h;
                    }
                }
                return Math.Max(ViewportHeight, _content.Height);
            }
            catch { return Math.Max(ViewportHeight, _content.Height); }
        }

        private void ApplyOffset()
        {
            if (_content != null) _content.Top = -_offset;
        }

        private int Clamp(int v)
        {
            if (v < 0) return 0;
            int max = MaxOffset;
            return v > max ? max : v;
        }

        internal void ScrollBy(int delta)
        {
            int before = _offset;
            _offset = Clamp(_offset + delta);
            if (_offset != before) { ApplyOffset(); if (!_barVisible) _barVisible = true; Invalidate(); }
        }

        internal void ScrollToTop()
        {
            _offset = 0;
            ApplyOffset();
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _hoverTimer.Start();
            BeginInvoke((Action)Recalc);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _hoverTimer.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (IsHandleCreated) BeginInvoke((Action)Recalc);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            ScrollBy(-(e.Delta / 120) * 72);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (NeedsBar && _barVisible) DrawBar(e.Graphics);
        }

        private void UpdateBarVisibility()
        {
            bool show = NeedsBar && (PointerInside() || _dragging);
            if (show != _barVisible) { _barVisible = show; Invalidate(); }
        }

        private bool PointerInside()
        {
            try { return ClientRectangle.Contains(PointToClient(Cursor.Position)); }
            catch { return false; }
        }

        private Rectangle TrackRect()
        {
            return new Rectangle(ClientSize.Width - BarW - BarMargin, BarMargin, BarW, Math.Max(1, ClientSize.Height - BarMargin));
        }

        private Rectangle ThumbRect()
        {
            Rectangle t = TrackRect();
            int thumbH = Math.Max(MinThumb, (int)((long)t.Height * ViewportHeight / Math.Max(1, _contentHeight)));
            if (thumbH > t.Height) thumbH = t.Height;
            int max = MaxOffset;
            int travel = t.Height - thumbH;
            int y = max > 0 ? t.Top + (int)((long)travel * _offset / max) : t.Top;
            return new Rectangle(t.Left, y, t.Width, thumbH);
        }

        private void DrawBar(Graphics g)
        {
            Rectangle t = TrackRect();
            using (SolidBrush b = new SolidBrush(Color.FromArgb(60, Line)))
                g.FillRectangle(b, t);
            Rectangle th = ThumbRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = Rounded(th, BarW / 2))
            using (SolidBrush b = new SolidBrush(ForeDim))
                g.FillPath(b, gp);
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath gp = new GraphicsPath();
            gp.AddArc(r.Left, r.Top, d, d, 180, 90);
            gp.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            gp.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            gp.CloseFigure();
            return gp;
        }

        private bool InBar(int x)
        {
            return NeedsBar && _barVisible && x >= ClientSize.Width - BarW - BarMargin;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                int travel = TrackRect().Height - ThumbRect().Height;
                int max = MaxOffset;
                if (travel > 0 && max > 0)
                {
                    int delta = e.Y - _dragStartY;
                    _offset = Clamp(_dragStartOffset + (int)((long)delta * max / travel));
                    ApplyOffset();
                    Invalidate();
                }
                return;
            }
            Cursor = InBar(e.X) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !InBar(e.X)) return;
            Rectangle th = ThumbRect();
            if (th.Contains(e.X, e.Y))
            {
                _dragging = true;
                _dragStartY = e.Y;
                _dragStartOffset = _offset;
            }
            else
            {
                ScrollBy(e.Y < th.Top ? -ViewportHeight : ViewportHeight);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
        }
    }
}
