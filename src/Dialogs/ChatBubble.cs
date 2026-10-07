using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WinPeLauncher.Services;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal sealed class Bubble : Panel
    {
        private readonly int _radius;
        private readonly Color _fill;

        internal string Caption;
        internal Color CaptionColor = Color.White;
        internal Padding CaptionPad = new Padding(10, 8, 10, 8);

        internal Bubble(Color fill, int radius, Color outside)
        {
            _radius = radius;
            _fill = fill;
            BackColor = outside;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            if (Width <= 0 || Height <= 0) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int d = _radius * 2;
            using (GraphicsPath gp = new GraphicsPath())
            {
                if (Width <= d || Height <= d)
                {
                    gp.AddRectangle(new Rectangle(0, 0, Width, Height));
                }
                else
                {
                    gp.AddArc(0, 0, d, d, 180, 90);
                    gp.AddArc(Width - d, 0, d, d, 270, 90);
                    gp.AddArc(Width - d, Height - d, d, d, 0, 90);
                    gp.AddArc(0, Height - d, d, d, 90, 90);
                    gp.CloseFigure();
                }
                using (SolidBrush b = new SolidBrush(_fill)) g.FillPath(b, gp);
            }

            if (!string.IsNullOrEmpty(Caption))
            {
                Rectangle rect = new Rectangle(CaptionPad.Left, CaptionPad.Top,
                    Math.Max(0, Width - CaptionPad.Horizontal),
                    Math.Max(0, Height - CaptionPad.Vertical));
                TextRenderer.DrawText(g, Caption, Font, rect, CaptionColor,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            }
        }
    }

    internal sealed class ChatBubble : Panel
    {
        private const int AvatarSize = 24;
        private const int Gap = 8;
        private const int Pad = 10;
        private const int Radius = 8;

        private readonly bool _me;
        private readonly Bubble _bubble;
        private readonly PictureBox _avatar;
        private readonly Label _name;
        private readonly RichTextBox _rtb;
        private readonly int _nameHeight;
        private int _rowWidth;

        internal string SourceText { get; private set; }
        internal bool Me { get { return _me; } }
        internal RichTextBox RichText { get { return _rtb; } }

        internal ChatBubble(string text, Image avatar, string name, bool me, int rowWidth,
            Color transcriptBack, Color bubbleBack, Color bubbleBackMe)
        {
            SourceText = text ?? "";
            _me = me;
            BackColor = transcriptBack;

            _avatar = new PictureBox();
            _avatar.SizeMode = PictureBoxSizeMode.Zoom;
            _avatar.BackColor = Color.Transparent;
            _avatar.Image = avatar;
            Controls.Add(_avatar);

            if (me)
            {
                _bubble = new Bubble(bubbleBackMe, Radius, transcriptBack);
                _bubble.Font = DialogUi.Font;
                _bubble.Caption = text;
                _bubble.CaptionColor = Color.White;
                _bubble.CaptionPad = new Padding(Pad, Pad - 2, Pad, Pad - 2);
            }
            else
            {
                _bubble = new Bubble(bubbleBack, Radius, transcriptBack);

                _name = new Label();
                _name.AutoSize = true;
                _name.Font = UiFonts.Bold(UiFonts.P(8.5f));
                _name.ForeColor = ForeDim;
                _name.BackColor = bubbleBack;
                _name.Text = name;
                _bubble.Controls.Add(_name);
                _nameHeight = _name.PreferredHeight + (Pad - 2);

                _rtb = new RichTextBox();
                _rtb.BorderStyle = BorderStyle.None;
                _rtb.ReadOnly = true;
                _rtb.BackColor = bubbleBack;
                _rtb.ForeColor = ForeMain;
                _rtb.Font = DialogUi.Font;
                _rtb.WordWrap = true;
                _rtb.ScrollBars = RichTextBoxScrollBars.None;
                _rtb.DetectUrls = false;
                _rtb.ShortcutsEnabled = true;
                _rtb.TabStop = true;
                try { _rtb.Rtf = MarkdownRtf.Render(text); }
                catch { _rtb.Text = text; }
                _bubble.Controls.Add(_rtb);
            }

            Controls.Add(_bubble);
            LayoutChildren(rowWidth);
        }

        internal void SetWidth(int w)
        {
            if (w == _rowWidth) return;
            LayoutChildren(w);
        }

        private void LayoutChildren(int w)
        {
            _rowWidth = w;
            int maxBubble = w - AvatarSize - Gap - 4;
            if (maxBubble < 80) maxBubble = 80;
            int innerW = maxBubble - 2 * Pad;
            if (innerW < 40) innerW = 40;

            if (_me)
            {
                _avatar.SetBounds(w - AvatarSize, 0, AvatarSize, AvatarSize);
                Size sz = TextRenderer.MeasureText(_bubble.Caption, _bubble.Font,
                    new Size(innerW, int.MaxValue), TextFormatFlags.WordBreak);
                int bw = Math.Min(maxBubble, sz.Width + 2 * Pad);
                int bh = sz.Height + 2 * (Pad - 2);
                _bubble.SetBounds(w - AvatarSize - Gap - bw, 0, bw, bh);
                Height = Math.Max(bh, AvatarSize);
            }
            else
            {
                _avatar.SetBounds(0, 0, AvatarSize, AvatarSize);
                _bubble.SetBounds(AvatarSize + Gap, 0, maxBubble, Math.Max(40, _bubble.Height));
                _name.Location = new Point(Pad, Pad - 2);
                _rtb.SetBounds(Pad, _nameHeight + 2, innerW, Math.Max(20, _rtb.Height));
                Fit();
            }
        }

        internal void Fit()
        {
            if (_me || _rtb == null) return;
            try
            {
                if (!_rtb.IsHandleCreated) _rtb.CreateControl();
                if (!_rtb.IsHandleCreated) return;
                int innerW = _rowWidth - AvatarSize - Gap - 4 - 2 * Pad;
                if (innerW < 40) innerW = 40;
                _rtb.Width = innerW;
                _rtb.Height = 3000;
                int idx = _rtb.TextLength > 0 ? _rtb.TextLength - 1 : 0;
                int y = _rtb.GetPositionFromCharIndex(idx).Y;
                int h = y + _rtb.Font.Height + 4;
                int minH = _rtb.Font.Height + 4;
                if (h < minH) h = minH;
                _rtb.Height = h;
                int bh = _nameHeight + 2 + h + Pad;
                _bubble.Height = bh;
                int newH = Math.Max(bh, AvatarSize);
                if (Height != newH) Height = newH;
            }
            catch { }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (Width != _rowWidth) LayoutChildren(Width);
        }
    }
}
