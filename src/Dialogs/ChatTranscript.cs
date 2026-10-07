using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal sealed class ChatTranscript : Panel
    {
        private const int SideMargin = 6;
        private const int ItemGap = 8;
        private const int BarW = 9;
        private const int BarMargin = 3;
        private const int MinThumb = 28;

        internal readonly Color BubbleBack = Color.FromArgb(46, 49, 56);
        internal readonly Color BubbleBackMe = Accent;

        private readonly Timer _hoverTimer = new Timer();

        private int _offset;
        private int _contentHeight;
        private bool _barVisible;
        private bool _dragging;
        private int _dragStartY;
        private int _dragStartOffset;

        internal ChatTranscript()
        {
            AutoScroll = false;
            BackColor = FieldBack;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            _hoverTimer.Interval = 200;
            _hoverTimer.Tick += (s, e) => UpdateBarVisibility();
        }

        private int ViewportHeight { get { return ClientSize.Height; } }

        private bool NeedsBar { get { return _contentHeight > ViewportHeight; } }

        private int MaxOffset
        {
            get { int m = _contentHeight - ViewportHeight; return m < 0 ? 0 : m; }
        }

        private int ContentWidth
        {
            get
            {
                int w = ClientSize.Width - SideMargin * 2 - (BarW + BarMargin);
                return w < 120 ? 120 : w;
            }
        }

        internal ChatBubble Add(string text, Image avatar, string name, bool me)
        {
            ChatBubble b = new ChatBubble(text, avatar, name, me, ContentWidth, BackColor, BubbleBack, BubbleBackMe);
            Controls.Add(b);
            Relayout();
            ScrollToBottom();
            return b;
        }

        internal void Relayout()
        {
            int y = 0;
            int w = ContentWidth;
            foreach (Control c in Controls)
            {
                ChatBubble b = c as ChatBubble;
                if (b == null) continue;
                b.SetWidth(w);
                b.Fit();
                b.Tag = y;
                b.Left = SideMargin;
                b.Top = y - _offset;
                b.Width = w;
                y += b.Height + ItemGap;
            }
            _contentHeight = y > 0 ? y - ItemGap : 0;
            _offset = Clamp(_offset);
            ApplyOffset();
            Invalidate();
        }

        private void ApplyOffset()
        {
            foreach (Control c in Controls)
            {
                ChatBubble b = c as ChatBubble;
                if (b == null || b.Tag == null) continue;
                b.Top = (int)b.Tag - _offset;
            }
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
            if (_offset != before)
            {
                ApplyOffset();
                ShowBarBriefly();
                Invalidate();
            }
        }

        internal void ScrollToBottom()
        {
            int target = MaxOffset;
            if (_offset != target) { _offset = target; ApplyOffset(); }
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _hoverTimer.Start();
            BeginInvoke((Action)(() => { Relayout(); ScrollToBottom(); }));
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            _hoverTimer.Stop();
            base.OnHandleDestroyed(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (IsHandleCreated) BeginInvoke((Action)(() => Relayout()));
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
            bool inside = PointerInside();
            bool show = NeedsBar && (inside || _dragging);
            if (show != _barVisible)
            {
                _barVisible = show;
                Invalidate();
            }
        }

        private void ShowBarBriefly()
        {
            if (!NeedsBar) return;
            if (!_barVisible) { _barVisible = true; }
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
            using (SolidBrush b = new SolidBrush(Color.FromArgb(70, Line)))
            {
                g.FillRectangle(b, t);
            }
            Rectangle th = ThumbRect();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath gp = Rounded(th, BarW / 2))
            using (SolidBrush b = new SolidBrush(ForeDim))
            {
                g.FillPath(b, gp);
            }
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
