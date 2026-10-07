using System;
using System.Collections.Generic;
using System.IO;

namespace WinPeLauncher.Services
{
    internal class AppEntry
    {
        internal string Name;
        internal string Path;
        internal string Args;
        internal string Cwd;
        internal string Icon;
        internal bool Script;
    }

    internal class AssistantCfg
    {
        internal string Endpoint;
        internal string ApiKey;
        internal string ApiSecret;
        internal string Model;
        // Optional extra request headers from the config ("headers" object).
        internal Dictionary<string, string> Headers;
    }

    internal static class AppConfig
    {
        // Resolve the config next to the exe itself. Assembly.GetExecutingAssembly()
        // stays correct even when a host process loads this assembly externally
        // (the chat engine reflects GetAssistant from powershell.exe, where
        // AppDomain.CurrentDomain.BaseDirectory would point at the PowerShell folder).
        private static string ConfigPath()
        {
            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir)) dir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(dir, "apps-config.json");
        }

        internal static List<AppEntry> Load()
        {
            List<AppEntry> result = new List<AppEntry>();
            try
            {
                string file = ConfigPath();
                if (!File.Exists(file)) return result;
                Parser p = new Parser(File.ReadAllText(file));
                object root = p.ParseValue();
                List<object> arr = root as List<object>;
                if (arr == null && root is Dictionary<string, object>)
                {
                    Dictionary<string, object> dict = (Dictionary<string, object>)root;
                    foreach (KeyValuePair<string, object> kv in dict)
                    {
                        arr = kv.Value as List<object>;
                        if (arr != null) break;
                    }
                }
                if (arr == null) return result;
                for (int i = 0; i < arr.Count; i++)
                {
                    Dictionary<string, object> o = arr[i] as Dictionary<string, object>;
                    if (o == null) continue;
                    AppEntry entry = new AppEntry();
                    entry.Name = Str(o, "name", "title", "label");
                    entry.Path = Str(o, "path", "exe", "file", "program");
                    entry.Args = Str(o, "args", "arguments", "parameters");
                    entry.Cwd = Str(o, "cwd", "workdir", "workingdir");
                    entry.Icon = Str(o, "icon");
                    if (string.IsNullOrEmpty(entry.Path)) continue;
                    if (string.IsNullOrEmpty(entry.Name)) entry.Name = Path.GetFileNameWithoutExtension(entry.Path);
                    result.Add(entry);
                }
            }
            catch { }
            return result;
        }

        internal static string GetTimeZone()
        {
            try
            {
                string file = ConfigPath();
                if (!File.Exists(file)) return null;
                Parser p = new Parser(File.ReadAllText(file));
                object root = p.ParseValue();
                Dictionary<string, object> dict = root as Dictionary<string, object>;
                if (dict == null) return null;
                return Str(dict, "timezone");
            }
            catch { return null; }
        }

        // Whitebox title on the left of the bar ("whitebox" in apps-config.json).
        // Defaults to WINPE LAUNCHER; capped at 15 characters so the bar layout never overflows.
        internal static string GetWhitebox()
        {
            string def = "WINPE LAUNCHER";
            try
            {
                string file = ConfigPath();
                if (!File.Exists(file)) return def;
                Parser p = new Parser(File.ReadAllText(file));
                Dictionary<string, object> dict = p.ParseValue() as Dictionary<string, object>;
                if (dict == null) return def;
                string v = Str(dict, "whitebox");
                if (string.IsNullOrEmpty(v)) return def;
                v = v.Trim();
                if (v.Length == 0) return def;
                if (v.Length > 15) v = v.Substring(0, 14) + "\u2026";
                return v;
            }
            catch { return def; }
        }

        // Cloud LLM connection settings (the "assistant" section in apps-config.json).
        internal static AssistantCfg GetAssistant()
        {
            try
            {
                string file = ConfigPath();
                if (!File.Exists(file)) return null;
                Parser p = new Parser(File.ReadAllText(file));
                Dictionary<string, object> dict = p.ParseValue() as Dictionary<string, object>;
                if (dict == null) return null;
                object raw;
                if (!dict.TryGetValue("assistant", out raw)) return null;
                Dictionary<string, object> a = raw as Dictionary<string, object>;
                if (a == null) return null;
                AssistantCfg c = new AssistantCfg();
                c.Endpoint = Str(a, "endpoint", "url");
                c.ApiKey = Str(a, "apiKey", "key");
                c.ApiSecret = Str(a, "apiSecret", "secret");
                c.Model = Str(a, "model");
                object hraw;
                if (a.TryGetValue("headers", out hraw) && hraw is Dictionary<string, object>)
                {
                    Dictionary<string, object> hd = (Dictionary<string, object>)hraw;
                    Dictionary<string, string> h = new Dictionary<string, string>();
                    foreach (KeyValuePair<string, object> kv in hd)
                        if (kv.Value is string) h[kv.Key] = (string)kv.Value;
                    if (h.Count > 0) c.Headers = h;
                }
                if (string.IsNullOrEmpty(c.Endpoint)) return null;
                return c;
            }
            catch { return null; }
        }

        // UI scale for the Launcher ("uiScale" in apps-config.json). 1 = 100%.
        // Returns 0 when unset/empty/invalid so the caller can fall back to the default (1).
        internal static float GetUiScale()
        {
            try
            {
                string file = ConfigPath();
                if (!File.Exists(file)) return 0f;
                Parser p = new Parser(File.ReadAllText(file));
                Dictionary<string, object> dict = p.ParseValue() as Dictionary<string, object>;
                if (dict == null) return 0f;
                object v;
                if (!dict.TryGetValue("uiScale", out v)) return 0f;
                if (v is double) return (float)(double)v;
                if (v is string)
                {
                    float f;
                    if (float.TryParse((string)v, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out f)) return f;
                }
            }
            catch { }
            return 0f;
        }

        private static string Str(Dictionary<string, object> o, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                object v;
                if (o.TryGetValue(keys[i], out v) && v is string) return (string)v;
            }
            return null;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            internal Parser(string s)
            {
                _s = s ?? "";
            }

            internal object ParseValue()
            {
                SkipWs();
                if (_i >= _s.Length) return null;
                char c = _s[_i];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (c == '-' || c == '+' || c == '.' || (c >= '0' && c <= '9')) return ParseNumber();
                SkipToken();
                return null;
            }

            private object ParseNumber()
            {
                int start = _i;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') _i++;
                    else break;
                }
                string t = _s.Substring(start, _i - start);
                double d;
                if (double.TryParse(t, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out d)) return d;
                return null;
            }

            private Dictionary<string, object> ParseObject()
            {
                Dictionary<string, object> dict = new Dictionary<string, object>();
                _i++;
                SkipWs();
                while (_i < _s.Length && _s[_i] != '}')
                {
                    SkipWs();
                    if (_i >= _s.Length || _s[_i] != '"') break;
                    string key = ParseString();
                    SkipWs();
                    if (_i < _s.Length && _s[_i] == ':') _i++;
                    object val = ParseValue();
                    if (key != null && !dict.ContainsKey(key)) dict[key] = val;
                    SkipWs();
                if (_i < _s.Length && _s[_i] == ',') _i++;
                else if (_i < _s.Length && _s[_i] == '"') continue; // missing comma between keys
                else break;
                }
                if (_i < _s.Length && _s[_i] == '}') _i++;
                return dict;
            }

            private List<object> ParseArray()
            {
                List<object> list = new List<object>();
                _i++;
                SkipWs();
                while (_i < _s.Length && _s[_i] != ']')
                {
                    int before = _i;
                    object val = ParseValue();
                    if (_i == before) break; // no progress -> break to avoid an infinite loop
                    list.Add(val);
                    SkipWs();
                    if (_i < _s.Length && _s[_i] == ',') { _i++; SkipWs(); }
                    else if (_i < _s.Length && (_s[_i] == '{' || _s[_i] == '[' || _s[_i] == '"')) continue; // missing comma between array elements
                    else break;
                }
                if (_i < _s.Length && _s[_i] == ']') _i++;
                return list;
            }

            private string ParseString()
            {
                _i++;
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c == '\\' && _i < _s.Length)
                    {
                        char e = _s[_i++];
                        if (e == 'n') sb.Append('\n');
                        else if (e == 'r') sb.Append('\r');
                        else if (e == 't') sb.Append('\t');
                        else if (e == 'b') sb.Append('\b');
                        else if (e == 'f') sb.Append('\f');
                        else if (e == 'u')
                        {
                            if (_i + 4 <= _s.Length)
                            {
                                int code;
                                if (int.TryParse(_s.Substring(_i, 4), System.Globalization.NumberStyles.HexNumber, null, out code))
                                    sb.Append((char)code);
                                _i += 4;
                            }
                        }
                        else sb.Append(e);
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            private void SkipToken()
            {
                while (_i < _s.Length && _s[_i] != ',' && _s[_i] != '}' && _s[_i] != ']' && !char.IsWhiteSpace(_s[_i])) _i++;
            }

            // Skip whitespace, BOM and "//" comments (notes inside apps-config.json).
            private void SkipWs()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (char.IsWhiteSpace(c) || c == '\uFEFF') { _i++; continue; }
                    if (c == '/' && _i + 1 < _s.Length && _s[_i + 1] == '/')
                    {
                        while (_i < _s.Length && _s[_i] != '\n') _i++;
                        continue;
                    }
                    break;
                }
            }
        }
    }
}
