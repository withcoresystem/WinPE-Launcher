using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;

namespace WinPeLauncher.Services
{
    internal static class UiFonts
    {
        internal static float Scale = 1f;
        internal static float DpiScale = 1f;

        internal static float P(float basePoint)
        {
            float s = Scale <= 0f ? 1f : Scale;
            float d = DpiScale <= 0f ? 1f : DpiScale;
            return basePoint * s / d;
        }

        // Scaled RTF \fs value (half-points). RichTextBox renders \fs at the system DPI,
        // so multiply by uiScale/dpiScale to make the text follow the chosen scale.
        internal static int HalfPoint(int baseHalfPoints)
        {
            float s = Scale <= 0f ? 1f : Scale;
            float d = DpiScale <= 0f ? 1f : DpiScale;
            int v = (int)Math.Round(baseHalfPoints * s / d);
            return v < 1 ? 1 : v;
        }

        private static PrivateFontCollection _pfc;
        private static FontFamily _family;
        private static FontFamily _familyMono;
        private static readonly Dictionary<string, Font> _cache = new Dictionary<string, Font>();
        private static readonly List<GCHandle> _gdiPinned = new List<GCHandle>();

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);

        internal static void Init()
        {
            try
            {
                byte[] regular = Load("Fonts.Inter-Regular.ttf");
                byte[] bold = Load("Fonts.Inter-Bold.ttf");
                byte[] mono = Load("Fonts.Hack-Regular.ttf");
                if (regular == null || bold == null) return;
                _pfc = new PrivateFontCollection();
                Add(_pfc, regular);
                Add(_pfc, bold);
                if (mono != null) Add(_pfc, mono);
                foreach (FontFamily f in _pfc.Families)
                {
                    if (f.Name == "Inter" && _family == null) _family = f;
                    if (f.Name == "Hack" && _familyMono == null) _familyMono = f;
                }
                if (_family == null && _pfc.Families.Length > 0) _family = _pfc.Families[0];
                if (_familyMono == null) _familyMono = _family;

                RegisterGdi(regular);
                RegisterGdi(bold);
                RegisterGdi(mono);
            }
            catch { _family = null; }
        }

        internal static Font Regular(float point)
        {
            return Get(point, false);
        }

        internal static Font Bold(float point)
        {
            return Get(point, true);
        }

        internal static Font Mono(float point)
        {
            string key = "m:" + point;
            Font f;
            if (_cache.TryGetValue(key, out f) && f != null) return f;
            try
            {
                if (_familyMono != null) f = new Font(_familyMono, point, FontStyle.Regular, GraphicsUnit.Point);
                else f = new Font(FontFamily.GenericMonospace, point, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                f = new Font(FontFamily.GenericMonospace, point, FontStyle.Regular, GraphicsUnit.Point);
            }
            _cache[key] = f;
            return f;
        }

        internal static string Info()
        {
            return _family == null ? "fallback" : _family.Name;
        }

        internal static string MonoInfo()
        {
            return _familyMono == null ? "fallback" : _familyMono.Name;
        }

        private static Font Get(float point, bool bold)
        {
            string key = (bold ? "b" : "r") + ":" + point;
            Font f;
            if (_cache.TryGetValue(key, out f) && f != null) return f;
            FontStyle style = bold ? FontStyle.Bold : FontStyle.Regular;
            try
            {
                if (_family != null) f = new Font(_family, point, style, GraphicsUnit.Point);
                else f = new Font("Segoe UI", point, style, GraphicsUnit.Point);
            }
            catch
            {
                f = new Font("Segoe UI", point, style, GraphicsUnit.Point);
            }
            _cache[key] = f;
            return f;
        }

        private static byte[] Load(string name)
        {
            using (Stream s = typeof(UiFonts).Assembly.GetManifestResourceStream(name))
            {
                if (s == null) return null;
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[8192];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                    return ms.ToArray();
                }
            }
        }

        private static void Add(PrivateFontCollection pfc, byte[] bytes)
        {
            GCHandle g = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try { pfc.AddMemoryFont(g.AddrOfPinnedObject(), bytes.Length); }
            finally { g.Free(); }
        }

        private static void RegisterGdi(byte[] bytes)
        {
            if (bytes == null) return;
            try
            {
                GCHandle g = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                uint count = 0;
                IntPtr h = AddFontMemResourceEx(g.AddrOfPinnedObject(), (uint)bytes.Length, IntPtr.Zero, ref count);
                if (h == IntPtr.Zero) g.Free();
                else _gdiPinned.Add(g);
            }
            catch { }
        }
    }
}
