using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WinPeLauncher.Services;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal static class SystemDiagnosticDialog
    {
        private const int WM_MOUSEWHEEL = 0x020A;

        internal static void Show(Control anchor)
        {
            float k = (UiFonts.Scale <= 0f ? 1f : UiFonts.Scale) / (UiFonts.DpiScale <= 0f ? 1f : UiFonts.DpiScale);
            Func<int, int> S = v => (int)Math.Round(v * k);

            Panel panel = new Panel();
            int width = S(480);
            int height = S(470);
            panel.Size = new Size(width, height);
            panel.BackColor = DialogBack;
            panel.Font = DialogUi.Font;
            try
            {
                ScrollHost host = new ScrollHost();
                host.SetBounds(0, 0, width, height - S(26));
                host.BackColor = DialogBack;

                RichTextBox box = new RichTextBox();
                box.ReadOnly = true;
                box.BorderStyle = BorderStyle.None;
                box.BackColor = DialogBack;
                box.ForeColor = ForeMain;
                box.Font = UiFonts.Mono(UiFonts.P(9.5f));
                box.WordWrap = true;
                box.ScrollBars = RichTextBoxScrollBars.None;
                box.DetectUrls = false;
                box.ShortcutsEnabled = true;
                host.SetContent(box);

                Label status = DialogUi.MakeLabel("", ForeDim, ContentAlignment.MiddleLeft);
                status.Font = UiFonts.Regular(UiFonts.P(9.5f));
                status.SetBounds(0, height - S(24), width, S(22));

                panel.Controls.Add(host);
                panel.Controls.Add(status);

                bool busy = false;

                Action<List<DiagSection>> apply = secs =>
                {
                    box.Rtf = BuildRtf(secs);
                    host.Recalc();
                    host.ScrollToTop();
                };

                Action collect = () =>
                {
                    if (busy) return;
                    busy = true;
                    status.Text = "Collecting...";
                    status.ForeColor = Accent;
                    box.Rtf = InfoRtf("Collecting system information...");
                    host.Recalc();
                    SystemDiagnostics.CollectAsync().ContinueWith(task =>
                    {
                        List<DiagSection> secs;
                        try { secs = task.Result; }
                        catch (Exception ex)
                        {
                            secs = new List<DiagSection>();
                            DiagSection s = new DiagSection { Name = "Diagnostic" };
                            s.Rows.Add(new DiagRow { Key = "Error", Value = ex.Message });
                            secs.Add(s);
                        }
                        Ui(panel, () =>
                        {
                            busy = false;
                            apply(secs);
                            status.Text = "Updated - " + secs.Count + " section(s).";
                            status.ForeColor = ForeDim;
                        });
                    });
                };

                AntdUI.Modal.Config config = new AntdUI.Modal.Config(anchor.FindForm(), "System Diagnostic", (object)panel);
                Font dlgFont = UiFonts.Regular(UiFonts.P(9.5f));
                config.SetColorScheme(AntdUI.TAMode.Dark);
                config.SetOk("Refresh");
                config.SetCancel("Close");
                config.SetMask(false);
                config.SetDraggable(false);
                config.SetLoadingDisableCancel(true);
                config.SetDefaultAcceptButton(false);
                config.SetFont(dlgFont);
                config.OkFont = dlgFont;
                config.CancelFont = dlgFont;
                config.OnOk = cfg => { collect(); return false; };

                List<DiagSection> cached = SystemDiagnostics.LastResult;
                if (cached != null)
                {
                    apply(cached);
                    status.Text = "Cached - click Refresh to update.";
                    status.ForeColor = ForeDim;
                }

                ScrollWheelFilter filter = new ScrollWheelFilter(host);
                bool prevAnim = AntdUI.Config.Animation;
                Application.AddMessageFilter(filter);
                try
                {
                    AntdUI.Config.Animation = true;
                    DialogDock.Dock(anchor, config, Accent);
                    if (cached == null) collect();
                    AntdUI.Modal.open(config);
                }
                finally
                {
                    AntdUI.Config.Animation = prevAnim;
                    try { Application.RemoveMessageFilter(filter); } catch { }
                    DialogDock.Stop();
                    DialogBorder.Hide();
                }
            }
            finally
            {
                try { panel.Dispose(); } catch { }
            }
        }

        private static string BuildRtf(List<DiagSection> sections)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Inter;}{\f1 Hack;}}{\colortbl ;");
            AppendColor(sb, ForeMain);
            AppendColor(sb, ForeDim);
            AppendColor(sb, Accent);
            sb.Append(@"}\cf1\fs").Append(UiFonts.HalfPoint(19)).Append(' ');

            if (sections.Count == 0)
                sb.Append(@"\cf2 No data collected.\par ");

            foreach (DiagSection sec in sections)
            {
                sb.Append(@"\cf3\b\fs").Append(UiFonts.HalfPoint(22)).Append(' ').Append(MarkdownRtf.Escape(sec.Name)).Append(@"\b0\fs").Append(UiFonts.HalfPoint(19)).Append(@"\par ");
                foreach (DiagRow row in sec.Rows)
                {
                    sb.Append(@"\cf2 ").Append(MarkdownRtf.Escape(row.Key)).Append(@":\cf1  \f1 ")
                      .Append(MarkdownRtf.Escape(row.Value)).Append(@"\f0\par ");
                }
                sb.Append(@"\par ");
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string InfoRtf(string message)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0 Inter;}}{\colortbl ;");
            AppendColor(sb, ForeDim);
            sb.Append(@"}\cf1\fs").Append(UiFonts.HalfPoint(19)).Append(' ').Append(MarkdownRtf.Escape(message)).Append(@"\par}");
            return sb.ToString();
        }

        private static void AppendColor(StringBuilder sb, Color c)
        {
            sb.Append(@"\red").Append(c.R).Append(@"\green").Append(c.G).Append(@"\blue").Append(c.B).Append(';');
        }

        private static void Ui(Control control, Action action)
        {
            try
            {
                if (control == null || control.IsDisposed) return;
                if (control.InvokeRequired) control.Invoke(action);
                else action();
            }
            catch { }
        }

        private sealed class ScrollWheelFilter : IMessageFilter
        {
            private readonly ScrollHost _host;

            internal ScrollWheelFilter(ScrollHost host) { _host = host; }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_MOUSEWHEEL || _host == null || _host.IsDisposed || !_host.IsHandleCreated) return false;
                try
                {
                    long lp = m.LParam.ToInt64();
                    int sx = (short)(lp & 0xFFFF), sy = (short)((lp >> 16) & 0xFFFF);
                    Rectangle screen = _host.RectangleToScreen(_host.ClientRectangle);
                    if (!screen.Contains(sx, sy)) return false;
                    long wp = m.WParam.ToInt64();
                    int delta = (short)((wp >> 16) & 0xFFFF);
                    if (delta == 0) return false;
                    int lines = SystemInformation.MouseWheelScrollLines;
                    if (lines <= 0) lines = 3;
                    _host.ScrollBy(-(delta / 120) * lines * 24);
                    return true;
                }
                catch { return false; }
            }
        }
    }
}
