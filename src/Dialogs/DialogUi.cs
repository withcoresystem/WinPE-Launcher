using System;
using System.Drawing;
using System.Windows.Forms;
using WinPeLauncher.Services;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal static class DialogUi
    {
        private static Font _font;

        internal static Font Font
        {
            get
            {
                if (_font == null) _font = UiFonts.Regular(UiFonts.P(9.5f));
                return _font;
            }
        }

        internal static AntdUI.Input MakeInput(string placeholder, bool password)
        {
            AntdUI.Input input = new AntdUI.Input();
            input.PlaceholderText = placeholder;
            input.PlaceholderColor = ForeDim;
            input.BackColor = FieldBack;
            input.ForeColor = ForeMain;
            input.BorderColor = Line;
            input.BorderWidth = 1;
            input.Radius = 6;
            input.UseSystemPasswordChar = password;
            input.UseContextMenu = false;
            input.Font = Font;
            return input;
        }

        internal static AntdUI.Select MakeSelect(string placeholder)
        {
            AntdUI.Select select = new AntdUI.Select();
            select.PlaceholderText = placeholder;
            select.PlaceholderColor = ForeDim;
            select.BackColor = FieldBack;
            select.ForeColor = ForeMain;
            select.BorderColor = Line;
            select.BorderWidth = 1;
            select.Radius = 6;
            select.UseContextMenu = false;
            select.Font = Font;
            return select;
        }

        internal static Label MakeLabel(string text, Color color, ContentAlignment align)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.TextAlign = align;
            label.AutoSize = false;
            label.Font = Font;
            return label;
        }

        internal static void SetStatus(Label label, string text, Color color)
        {
            try
            {
                if (label.IsDisposed || !label.IsHandleCreated) return;
                label.BeginInvoke((Action)(() =>
                {
                    if (label.IsDisposed) return;
                    label.Text = text;
                    label.ForeColor = color;
                }));
            }
            catch { }
        }

        internal static void ScaleBounds(Control root, float k)
        {
            if (root == null || k <= 0f || Math.Abs(k - 1f) < 0.001f) return;
            for (int i = 0; i < root.Controls.Count; i++)
            {
                Control c = root.Controls[i];
                c.Location = new Point((int)Math.Round(c.Left * k), (int)Math.Round(c.Top * k));
                c.Size = new Size((int)Math.Round(c.Width * k), (int)Math.Round(c.Height * k));
                ScaleBounds(c, k);
            }
        }

        internal static T ReadUi<T>(Control control, Func<T> read)        {
            try
            {
                if (control.IsDisposed) return default(T);
                if (control.InvokeRequired) return (T)control.Invoke(read);
                return read();
            }
            catch
            {
                return default(T);
            }
        }
    }
}
