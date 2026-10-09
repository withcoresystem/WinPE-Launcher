using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WinPeLauncher.Services;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal static class ChatDialog
    {
        private const string ReadyText = "Ready. Replies are limited to 500 words.";

        private const int WM_MOUSEWHEEL = 0x020A;

        internal static void Show(Control anchor)
        {
            float k = (UiFonts.Scale <= 0f ? 1f : UiFonts.Scale) / (UiFonts.DpiScale <= 0f ? 1f : UiFonts.DpiScale);
            Func<int, int> S = v => (int)Math.Round(v * k);

            Panel panel = new Panel();
            int width = S(520);
            int panelH = S(430);
            try
            {
                Rectangle wa = Screen.FromControl(anchor).WorkingArea;
                Form bar = anchor.FindForm();
                int limit = (bar != null && bar.Bounds.Top > 0) ? bar.Bounds.Top : wa.Bottom;
                int maxH = limit - wa.Top - S(8) - S(104);
                if (maxH < panelH) panelH = Math.Max(S(300), maxH);
            }
            catch { }
            int logH = panelH - S(82);
            panel.Size = new Size(width, panelH);
            panel.BackColor = DialogBack;
            panel.Font = DialogUi.Font;
            try
            {
                Bitmap aiAvatar = MakeAiAvatar(S(24));
                Bitmap meAvatar = MakeMeAvatar(S(24));

                ChatTranscript log = new ChatTranscript();
                log.SetBounds(0, 0, width, logH);
                log.Font = DialogUi.Font;

                AntdUI.Input input = DialogUi.MakeInput("Ask a question... (Enter to send)", false);
                input.SetBounds(0, logH + S(8), width, S(34));

                Label status = DialogUi.MakeLabel("", ForeDim, ContentAlignment.MiddleLeft);
                status.SetBounds(0, logH + S(50), width, S(26));

                panel.Controls.Add(log);
                panel.Controls.Add(input);
                panel.Controls.Add(status);

                if (AppConfig.GetAssistant() != null)
                {
                    status.Text = ReadyText;
                    status.ForeColor = ForeDim;
                }
                else
                {
                    status.Text = "Missing endpoint/apiKey in apps-config.json - fill it in first.";
                    status.ForeColor = ErrRed;
                }

                List<HistoryEntry> conv = new List<HistoryEntry>();
                foreach (HistoryEntry e in LoadHistory())
                {
                    conv.Add(e);
                    ChatBubble b = log.Add(e.Text, e.Me ? meAvatar : aiAvatar, e.Me ? "You" : "Agent", e.Me);
                    AttachMenu(b, () => Conversation(conv));
                }

                bool busy = false;

                AntdUI.Modal.Config config = new AntdUI.Modal.Config(anchor.FindForm(), "Smart Assistant", (object)panel);
                config.SetColorScheme(AntdUI.TAMode.Dark);
                config.SetOk("Send");
                config.SetCancel("Close");
                config.SetMask(false);
                config.SetDraggable(false);
                config.SetLoadingDisableCancel(true);
                config.SetDefaultAcceptButton(true);
                config.SetFont(DialogUi.Font);
                config.OkFont = DialogUi.Font;
                config.CancelFont = DialogUi.Font;
                config.OnOk = cfg =>
                {
                    if (busy) return false;
                    string text = DialogUi.ReadUi(panel, () => (input.Text ?? "").Trim());
                    if (text.Length == 0) return false;
                    busy = true;
                    HistoryEntry u = new HistoryEntry { Me = true, Text = text };
                    Ui(panel, () =>
                    {
                        input.Text = "";
                        status.Text = "Thinking...";
                        status.ForeColor = Accent;
                        conv.Add(u);
                        ChatBubble bu = log.Add(u.Text, meAvatar, "You", true);
                        AttachMenu(bu, () => Conversation(conv));
                        log.Invalidate(); log.Update();
                    });
                    Send(text, reply =>
                    {
                        busy = false;
                        HistoryEntry a = new HistoryEntry { Me = false, Text = reply };
                        Ui(panel, () =>
                        {
                            conv.Add(a);
                            ChatBubble ba = log.Add(a.Text, aiAvatar, "Agent", false);
                            AttachMenu(ba, () => Conversation(conv));
                            status.Text = ReadyText;
                            status.ForeColor = ForeDim;
                            log.Invalidate(); log.Update();
                            input.Invalidate(); input.Update();
                            status.Invalidate(); status.Update();
                            panel.Invalidate(); panel.Update();
                        });
                    });
                    return false;
                };

                TranscriptWheelFilter wheel = new TranscriptWheelFilter(log);
                Application.AddMessageFilter(wheel);
                bool prevAnim = AntdUI.Config.Animation;
                try
                {
                    AntdUI.Config.Animation = true;
                    DialogDock.Dock(anchor, config, Accent);
                    AntdUI.Modal.open(config);
                }
                finally
                {
                    AntdUI.Config.Animation = prevAnim;
                    try { Application.RemoveMessageFilter(wheel); } catch { }
                    DialogDock.Stop();
                    DialogBorder.Hide();
                }
            }
            finally
            {
                try { panel.Dispose(); } catch { }
            }
        }

        private static string Conversation(List<HistoryEntry> conv)
        {
            StringBuilder sb = new StringBuilder();
            foreach (HistoryEntry e in conv)
                sb.Append(e.Me ? "You: " : "Agent: ").Append(e.Text).Append(Environment.NewLine).Append(Environment.NewLine);
            return sb.ToString();
        }

        private static void AttachMenu(ChatBubble bubble, Func<string> whole)
        {
            try
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Font = DialogUi.Font;

                menu.Items.Add("Copy", null, (s, e) =>
                {
                    try
                    {
                        RichTextBox r = bubble.RichText;
                        string t = (r != null && r.SelectionLength > 0) ? r.SelectedText : bubble.SourceText;
                        if (!string.IsNullOrEmpty(t)) Clipboard.SetText(t);
                    }
                    catch { }
                });

                if (bubble.RichText != null)
                {
                    menu.Items.Add("Select all", null, (s, e) =>
                    {
                        try { bubble.RichText.SelectAll(); bubble.RichText.Focus(); } catch { }
                    });
                }

                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Copy entire conversation", null, (s, e) =>
                {
                    try
                    {
                        string t = whole();
                        if (!string.IsNullOrEmpty(t)) Clipboard.SetText(t);
                    }
                    catch { }
                });

                bubble.ContextMenuStrip = menu;
                if (bubble.RichText != null) bubble.RichText.ContextMenuStrip = menu;
            }
            catch { }
        }

        private static async void Send(string prompt, Action<string> done)
        {
            string reply;
            try
            {
                reply = await ChatEngine.AskAsync(prompt);
            }
            catch (Exception ex)
            {
                reply = "Error: " + ex.Message;
            }
            try { done(reply); } catch { }
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

        private static int Scale(Control anchor, int px)
        {
            try
            {
                using (Graphics g = anchor.CreateGraphics())
                    return (int)Math.Round(px * g.DpiX / 96f);
            }
            catch { return px; }
        }

        private sealed class TranscriptWheelFilter : IMessageFilter
        {
            private readonly ChatTranscript _log;

            internal TranscriptWheelFilter(ChatTranscript log) { _log = log; }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_MOUSEWHEEL || _log == null || _log.IsDisposed || !_log.IsHandleCreated) return false;
                try
                {
                    long lp = m.LParam.ToInt64();
                    int sx = (short)(lp & 0xFFFF), sy = (short)((lp >> 16) & 0xFFFF);
                    Rectangle screen = _log.RectangleToScreen(_log.ClientRectangle);
                    if (!screen.Contains(sx, sy)) return false;
                    long wp = m.WParam.ToInt64();
                    int delta = (short)((wp >> 16) & 0xFFFF);
                    if (delta == 0) return false;
                    int lines = SystemInformation.MouseWheelScrollLines;
                    if (lines <= 0) lines = 3;
                    _log.ScrollBy(-(delta / 120) * lines * 24);
                    return true;
                }
                catch { return false; }
            }
        }

        private static Bitmap MakeAiAvatar(int px)
        {
            try
            {
                string svg = null;
                using (Stream s = typeof(ChatDialog).Assembly.GetManifestResourceStream("assistant.svg"))
                {
                    if (s != null)
                        using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                            svg = r.ReadToEnd();
                }
                if (!string.IsNullOrEmpty(svg))
                    return AntdUI.SvgExtend.SvgToBmp(svg, px, px, Accent);
            }
            catch { }
            return null;
        }

        private static Bitmap MakeMeAvatar(int px)
        {
            try
            {
                Bitmap bmp = new Bitmap(px, px);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (Brush b = new SolidBrush(Accent)) g.FillEllipse(b, 0, 0, px - 1, px - 1);
                    using (Font f = new Font("Segoe UI", px * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        string s = "U";
                        SizeF sz = g.MeasureString(s, f);
                        g.DrawString(s, f, Brushes.White, (px - sz.Width) / 2f, (px - sz.Height) / 2f);
                    }
                }
                return bmp;
            }
            catch { return null; }
        }

        private static string NormalizeNewlines(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
        }

        private struct HistoryEntry
        {
            internal bool Me;
            internal string Text;
        }

        private static List<HistoryEntry> LoadHistory()
        {
            List<HistoryEntry> list = new List<HistoryEntry>();
            try
            {
                string path = ChatEngine.SessionPath;
                if (!File.Exists(path)) return list;
                Regex rx = new Regex("\"role\"\\s*:\\s*\"(user|assistant)\"\\s*,\\s*\"text\"\\s*:\\s*\"(.*)\"\\s*}\\s*$");
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    Match m = rx.Match(line);
                    if (!m.Success) continue;
                    HistoryEntry e = new HistoryEntry();
                    e.Me = m.Groups[1].Value == "user";
                    e.Text = NormalizeNewlines(Unescape(m.Groups[2].Value));
                    if (!string.IsNullOrEmpty(e.Text)) list.Add(e);
                }
            }
            catch { }
            return list;
        }

        private static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\\r", "").Replace("\\n", Environment.NewLine).Replace("\\t", "\t")
                 .Replace("\\\"", "\"").Replace("\\\\", "\\");
            try
            {
                s = Regex.Replace(s, "\\\\u([0-9a-fA-F]{4})",
                    m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            }
            catch { }
            return s;
        }
    }
}
