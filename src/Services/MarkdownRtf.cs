using System;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;

namespace WinPeLauncher.Services
{
    internal static class MarkdownRtf
    {
        private static readonly Color CodeBg = Color.FromArgb(26, 29, 35);

        internal static string Render(string markdown)
        {
            string text = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");
            StringBuilder sb = new StringBuilder(text.Length * 2 + 256);
            sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0");
            sb.Append(@"{\fonttbl{\f0\fswiss Inter;}{\f1\fmodern Hack;}}");
            AppendColorTable(sb);
            sb.Append(@"\cf1\f0\fs").Append(UiFonts.HalfPoint(19)).Append(' ');

            string[] lines = text.Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string raw = lines[i];
                string trimmed = raw.TrimStart();

                if (IsFence(trimmed))
                {
                    i++;
                    while (i < lines.Length && !IsFence(lines[i].TrimStart()))
                    {
                        sb.Append(@"\f1\highlight4\cf1 ");
                        AppendEscaped(sb, lines[i]);
                        sb.Append(@"\highlight0\cf1\f0\par ");
                        i++;
                    }
                    if (i < lines.Length) i++;
                    continue;
                }

                Match h = Regex.Match(raw, @"^(#{1,6})\s+(.*)$");
                if (h.Success)
                {
                    int level = h.Groups[1].Value.Length;
                    int fs = level == 1 ? 28 : level == 2 ? 24 : level == 3 ? 21 : 20;
                    sb.Append(@"\b\fs").Append(UiFonts.HalfPoint(fs)).Append(' ');
                    AppendInline(sb, h.Groups[2].Value.TrimEnd());
                    sb.Append(@"\b0\fs").Append(UiFonts.HalfPoint(19)).Append(@"\par ");
                    i++;
                    continue;
                }

                if (IsRule(trimmed))
                {
                    sb.Append(@"\cf3 ");
                    for (int k = 0; k < 24; k++) sb.Append(@"\emdash");
                    sb.Append(@"\cf1\par ");
                    i++;
                    continue;
                }

                if (trimmed.StartsWith(">"))
                {
                    while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
                    {
                        string q = lines[i].TrimStart().Substring(1).TrimStart();
                        sb.Append(@"\cf3\i ");
                        AppendInline(sb, q);
                        sb.Append(@"\i0\cf1\par ");
                        i++;
                    }
                    continue;
                }

                if (IsTableRow(raw) && i + 1 < lines.Length && IsTableSeparator(lines[i + 1]))
                {
                    i++;
                    while (i < lines.Length && IsTableRow(lines[i]))
                    {
                        if (IsTableSeparator(lines[i])) { i++; continue; }
                        AppendTableRow(sb, lines[i]);
                        i++;
                    }
                    continue;
                }

                Match ul = Regex.Match(raw, @"^\s*[-*+]\s+(.*)$");
                if (ul.Success)
                {
                    sb.Append(@"\li360\bullet\tab ");
                    AppendInline(sb, ul.Groups[1].Value);
                    sb.Append(@"\par\li0\fs").Append(UiFonts.HalfPoint(19)).Append(@"\cf1\f0 ");
                    i++;
                    continue;
                }

                Match ol = Regex.Match(raw, @"^\s*(\d+)[.)]\s+(.*)$");
                if (ol.Success)
                {
                    sb.Append(@"\li360 ").Append(ol.Groups[1].Value).Append(@".\tab ");
                    AppendInline(sb, ol.Groups[2].Value);
                    sb.Append(@"\par\li0\fs").Append(UiFonts.HalfPoint(19)).Append(@"\cf1\f0 ");
                    i++;
                    continue;
                }

                if (trimmed.Length == 0)
                {
                    sb.Append(@"\par ");
                    i++;
                    continue;
                }

                AppendInline(sb, raw.Trim());
                sb.Append(@"\par ");
                i++;
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendColorTable(StringBuilder sb)
        {
            sb.Append(@"{\colortbl ;");
            AppendColor(sb, Theme.ForeMain);
            AppendColor(sb, Theme.Accent);
            AppendColor(sb, Theme.ForeDim);
            AppendColor(sb, CodeBg);
            sb.Append('}');
        }

        private static void AppendColor(StringBuilder sb, Color c)
        {
            sb.Append(@"\red").Append(c.R).Append(@"\green").Append(c.G).Append(@"\blue").Append(c.B).Append(';');
        }

        private static void AppendTableRow(StringBuilder sb, string line)
        {
            string[] cells = line.Trim().Trim('|').Split('|');
            sb.Append(@"\f1\cf1 ");
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(" | ");
                AppendEscaped(sb, cells[i].Trim());
            }
            sb.Append(@"\f0\par ");
        }

        private static void AppendInline(StringBuilder sb, string s)
        {
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];

                if (c == '`')
                {
                    int end = s.IndexOf('`', i + 1);
                    if (end > i)
                    {
                        sb.Append(@"\f1\highlight4\cf1 ");
                        AppendEscaped(sb, s.Substring(i + 1, end - i - 1));
                        sb.Append(@"\highlight0\cf1\f0 ");
                        i = end + 1;
                        continue;
                    }
                }

                if (c == '*' && i + 1 < s.Length && s[i + 1] == '*')
                {
                    int end = s.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (end > i)
                    {
                        sb.Append(@"\b ");
                        AppendInline(sb, s.Substring(i + 2, end - i - 2));
                        sb.Append(@"\b0 ");
                        i = end + 2;
                        continue;
                    }
                }

                if (c == '*' || c == '_')
                {
                    int end = s.IndexOf(c, i + 1);
                    if (end > i + 1)
                    {
                        sb.Append(@"\i ");
                        AppendInline(sb, s.Substring(i + 1, end - i - 1));
                        sb.Append(@"\i0 ");
                        i = end + 1;
                        continue;
                    }
                }

                if (c == '[')
                {
                    int close = s.IndexOf(']', i + 1);
                    if (close > i && close + 1 < s.Length && s[close + 1] == '(')
                    {
                        int paren = s.IndexOf(')', close + 2);
                        if (paren > close)
                        {
                            string label = s.Substring(i + 1, close - i - 1);
                            string url = s.Substring(close + 2, paren - close - 2);
                            sb.Append(@"\cf2\ul ");
                            AppendEscaped(sb, label);
                            sb.Append(@"\ulnone\cf1 ");
                            if (!string.IsNullOrEmpty(url) && !string.Equals(url, label, StringComparison.Ordinal))
                            {
                                sb.Append(@"\cf3 (");
                                AppendEscaped(sb, url);
                                sb.Append(@")\cf1 ");
                            }
                            i = paren + 1;
                            continue;
                        }
                    }
                }

                AppendEscapedChar(sb, c);
                i++;
            }
        }

        internal static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\n' || c == '\r') { sb.Append(' '); continue; }
                AppendEscapedChar(sb, c);
            }
            return sb.ToString();
        }

        private static void AppendEscaped(StringBuilder sb, string s)
        {
            for (int i = 0; i < s.Length; i++) AppendEscapedChar(sb, s[i]);
        }

        private static void AppendEscapedChar(StringBuilder sb, char c)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); return;
                case '{': sb.Append(@"\{"); return;
                case '}': sb.Append(@"\}"); return;
                case '\t': sb.Append(@"\tab "); return;
            }
            if (c < 128)
            {
                sb.Append(c);
                return;
            }
            int v = c;
            sb.Append(@"\u").Append(v > 32767 ? v - 65536 : v).Append('?');
        }

        private static bool IsFence(string trimmed)
            => trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal);

        private static bool IsRule(string trimmed)
            => Regex.IsMatch(trimmed, @"^([-*_])(\s*\1){2,}\s*$");

        private static bool IsTableRow(string line)
            => line.Trim().Contains("|") && line.Split('|').Length >= 3;

        private static bool IsTableSeparator(string line)
        {
            string t = line.Trim().Trim('|').Trim();
            return t.Length > 0 && t.Contains("-") && Regex.IsMatch(t, @"^[\s:|-]+$");
        }
    }
}
