using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WinPeLauncher.Services;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    public class MainForm : Form
    {
        private const int BaseHeight = 56;
        private const int BaseFieldHeight = 34;
        private const int BasePad = 16;

        private readonly WhiteboxLabel _brand = new WhiteboxLabel();
        private readonly AntdUI.Select _system = new AntdUI.Select();
        private readonly AntdUI.Select _tools = new AntdUI.Select();
        private readonly AntdUI.Select _apps = new AntdUI.Select();
        private readonly AntdUI.Select _windows = new AntdUI.Select();
        private readonly Panel _divider = new Panel();
        private readonly AntdUI.Label _net = new AntdUI.Label();
        private readonly AntdUI.Label _clock = new AntdUI.Label();
        private readonly AntdUI.Label _clockDate = new AntdUI.Label();
        private readonly Panel _assistantIcon = new Panel();

        private bool _assistantActive;
        private Bitmap _iconIdle;
        private Bitmap _iconActive;
        private int _iconPx;
        private string _assistantSvg;

        private readonly Panel _diagIcon = new Panel();

        private bool _diagActive;
        private Bitmap _diagIconIdle;
        private Bitmap _diagIconActive;
        private int _diagPx;
        private string _diagSvg;

        private readonly Panel _shotIcon = new Panel();

        private Bitmap _shotIdle;
        private Bitmap _shotHover;
        private int _shotPx;
        private string _shotSvg;
        private bool _shotHovering;

        private readonly Panel _netIcon = new Panel();

        private Bitmap _netIconOk;
        private Bitmap _netIconOff;
        private int _netPx;
        private string _netSvg;

        private IntPtr _kbHook = IntPtr.Zero;
        private KeyboardProc _kbProc;

        private readonly Timer _timer = new Timer();

        private Font _fontBold;
        private Font _fontText;

        private bool _resetting;
        private float _scale = 1f;
        private float _dpiScale = 1f;
        private int _ticks;
        private bool _displayHooked;
        private bool _netUp;
        private readonly List<AppEntry> _appEntries = new List<AppEntry>();

        public MainForm()
        {
            Text = "WinPE Launcher";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = BarBack;
            DoubleBuffered = true;
            KeyPreview = true;

            InitScale();
            BuildControls();

            _timer.Interval = 1000;
            _timer.Tick += OnTick;
        }

        #region setup

        private void InitScale()
        {
            _scale = UiFonts.Scale > 0f ? UiFonts.Scale : 1f;
            _dpiScale = UiFonts.DpiScale > 0f ? UiFonts.DpiScale : 1f;
        }

        private void BuildControls()
        {
            _brand.Text = AppConfig.GetWhitebox();
            _brand.ForeColor = Accent;
            _brand.TextAlign = ContentAlignment.MiddleLeft;

            StyleSelect(_system, "System");
            _system.MaxCount = 8;
            _system.Items.Add(new AntdUI.SelectItem("Shutdown", "shutdown").SetIcon("PoweroffOutlined"));
            _system.Items.Add(new AntdUI.SelectItem("Reboot", "reboot").SetIcon("ReloadOutlined"));
            _system.Items.Add(new AntdUI.SelectItem("Wi-Fi", "wifi").SetIcon("WifiOutlined"));
            _system.Items.Add(new AntdUI.SelectItem("BitLocker", "bitlocker").SetIcon("LockOutlined"));
            _system.Items.Add(new AntdUI.SelectItem("Sync Time", "syntime").SetIcon("FieldTimeOutlined"));

            StyleSelect(_tools, "System Tools");
            _tools.MaxCount = 8;
            _tools.Items.Add(new AntdUI.SelectItem("Command Prompt", "cmd").SetIcon("CodeOutlined"));
            _tools.Items.Add(new AntdUI.SelectItem("Notepad", "notepad").SetIcon("FileTextOutlined"));
            _tools.Items.Add(new AntdUI.SelectItem("PowerShell", "powershell").SetIcon("ThunderboltOutlined"));
            _tools.Items.Add(new AntdUI.SelectItem("Task Manager", "taskmgr").SetIcon("DashboardOutlined"));
            _tools.Items.Add(new AntdUI.SelectItem("System Info", "msinfo").SetIcon("InfoCircleOutlined"));

            StyleSelect(_apps, "Applications");
            _apps.MaxCount = 12;
            _apps.Empty = true;
            ApplyApps(AppConfig.Load());

            StyleSelect(_windows, "Opened Windows");
            _windows.MaxCount = 8;
            _windows.Empty = true;

            _divider.BackColor = Line;

            _assistantIcon.BackColor = BarBack;
            _assistantIcon.Cursor = Cursors.Hand;
            _assistantIcon.Paint += OnAssistantPaint;
            _assistantIcon.Click += OnAssistantClick;

            _diagIcon.BackColor = BarBack;
            _diagIcon.Cursor = Cursors.Hand;
            _diagIcon.Paint += OnDiagPaint;
            _diagIcon.Click += OnDiagClick;

            _shotIcon.BackColor = BarBack;
            _shotIcon.Cursor = Cursors.Hand;
            _shotIcon.Paint += OnShotPaint;
            _shotIcon.Click += OnShotClick;
            _shotIcon.MouseEnter += (s, e) => { _shotHovering = true; _shotIcon.Invalidate(); };
            _shotIcon.MouseLeave += (s, e) => { _shotHovering = false; _shotIcon.Invalidate(); };

            _netIcon.BackColor = BarBack;
            _netIcon.Paint += OnNetPaint;

            _net.TextAlign = ContentAlignment.MiddleLeft;
            _net.ForeColor = ForeDim;

            _clock.TextAlign = ContentAlignment.MiddleRight;
            _clock.ForeColor = ForeMain;

            _clockDate.TextAlign = ContentAlignment.MiddleRight;
            _clockDate.ForeColor = ForeDim;

            ApplySelectTweaks();

            Controls.Add(_brand);
            Controls.Add(_system);
            Controls.Add(_tools);
            Controls.Add(_apps);
            Controls.Add(_windows);
            Controls.Add(_divider);
            Controls.Add(_shotIcon);
            Controls.Add(_diagIcon);
            Controls.Add(_assistantIcon);
            Controls.Add(_netIcon);
            Controls.Add(_net);
            Controls.Add(_clock);
            Controls.Add(_clockDate);

            _system.SelectedValueChanged += (s, e) =>
            {
                object tag = e.Value;
                Post(() => RunSystem(tag as string));
            };
            _tools.SelectedValueChanged += (s, e) =>
            {
                object tag = e.Value;
                Post(() => RunTool(tag as string));
            };
            _apps.SelectedValueChanged += (s, e) =>
            {
                object tag = e.Value;
                Post(() => RunApp(tag as string));
            };
            _apps.Click += (s, e) =>
            {
                if (_apps.Items.Count == 0) AntdUI.Message.info(this, "No applications available", DialogUi.Font, 3);
            };
            _system.MouseDown += (s, e) => CloseOthers(_system);
            _tools.MouseDown += (s, e) => CloseOthers(_tools);
            _apps.MouseDown += (s, e) => CloseOthers(_apps);
            _windows.MouseDown += (s, e) =>
            {
                CloseOthers(_windows);
                if (!_windows.ExpandDrop) RefreshWindows();
            };
            _windows.SelectedValueChanged += (s, e) =>
            {
                object tag = e.Value;
                Post(() => ActivateWindow(tag as string));
            };
        }

        private static void StyleSelect(AntdUI.Select select, string placeholder)
        {
            select.PlaceholderText = placeholder;
            select.Placement = AntdUI.TAlignFrom.TL;
            select.BackColor = FieldBack;
            select.ForeColor = ForeMain;
            select.PlaceholderColor = ForeDim;
            select.BorderColor = Line;
            select.BorderWidth = 1;
            select.Radius = 6;
            select.ListAutoWidth = true;
            select.UseContextMenu = false;
        }

        // One-time AntdUI color overrides for the bar selects:
        // - Arrow: default TextQuaternary (black @ 25%) is nearly invisible on the dark field.
        // - Dropdown hover: default FillTertiary is too subtle on the light list.
        private static void ApplySelectTweaks()
        {
            AntdUI.Style.Set(AntdUI.Colour.TextQuaternary, ForeDim, "Select");
            AntdUI.Style.Set(AntdUI.Colour.FillTertiary, Color.FromArgb(230, 241, 255), "Select");
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Font Inter embedded in the exe (UiFonts) - no more Segoe UI fallback on WinPE.
            _fontBold = UiFonts.Bold(UiFonts.P(9f));
            _fontText = UiFonts.Regular(UiFonts.P(9.5f));

            _brand.Font = _fontBold;
            _system.Font = _fontText;
            _tools.Font = _fontText;
            _apps.Font = _fontText;
            _windows.Font = _fontText;
            _net.Font = _fontText;
            _clock.Font = _fontText;
            _clockDate.Font = UiFonts.Regular(UiFonts.P(8.5f));

            PositionBar();
            Arrange();
            UpdateNetwork();
            _clock.Text = NowClock();
            _clockDate.Text = NowDate();
            _timer.Start();
            ReassertTopMost();
            InstallPrintScreenHook();

            AntdUI.ITask.Run(() =>
            {
                List<AppEntry> merged = AppScanner.Scan();
                Post(() =>
                {
                    if (!IsDisposed) ApplyApps(merged);
                });
            });

            try
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
                _displayHooked = true;
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                if (_displayHooked)
                {
                    try { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; }
                    catch { }
                    _displayHooked = false;
                }
                if (_iconIdle != null) { _iconIdle.Dispose(); _iconIdle = null; }
                if (_iconActive != null) { _iconActive.Dispose(); _iconActive = null; }
                if (_diagIconIdle != null) { _diagIconIdle.Dispose(); _diagIconIdle = null; }
                if (_diagIconActive != null) { _diagIconActive.Dispose(); _diagIconActive = null; }
                if (_shotIdle != null) { _shotIdle.Dispose(); _shotIdle = null; }
                if (_shotHover != null) { _shotHover.Dispose(); _shotHover = null; }
                if (_netIconOk != null) { _netIconOk.Dispose(); _netIconOk = null; }
                if (_netIconOff != null) { _netIconOff.Dispose(); _netIconOff = null; }
                if (_kbHook != IntPtr.Zero)
                {
                    try { UnhookWindowsHookEx(_kbHook); } catch { }
                    _kbHook = IntPtr.Zero;
                }
                // Fonts are owned by UiFonts (shared cache) - do not dispose them here.
            }
            base.Dispose(disposing);
        }

        #endregion

        #region layout

        private int S(int px)
        {
            return (int)Math.Round(px * _scale);
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    PositionBar();
                    Arrange();
                }));
            }
            catch
            {
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Arrange();
        }

        private void PositionBar()
        {
            Screen screen = Screen.FromControl(this);
            Rectangle area = screen.Bounds;
            int height = S(BaseHeight);
            Rectangle target = new Rectangle(area.Left, area.Bottom - height, area.Width, height);
            if (Bounds != target) Bounds = target;
        }

        private void Arrange()
        {
            if (_scale <= 0f) return;
            int pad = S(BasePad);
            int field = S(BaseFieldHeight);
            int y = (Height - field) / 2;

            int brandW = S(136);
            if (_fontBold != null)
            {
                try
                {
                    Size measured = TextRenderer.MeasureText(_brand.Text, _fontBold, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    if (measured.Width > 0) brandW = Math.Max(brandW, measured.Width + S(24));
                }
                catch { }
            }
            _brand.SetBounds(pad, y, brandW, field);

            int right = Width - pad;
            int clockWidth = S(86);
            if (_clockDate.Font != null)
            {
                try
                {
                    Size measured = TextRenderer.MeasureText("00-Oct-0000", _clockDate.Font, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    if (measured.Width > 0) clockWidth = Math.Max(clockWidth, measured.Width + S(8));
                }
                catch { }
            }
            // Two-line clock (time + date), block vertically centered inside the field row.
            int clockHalf = field / 2;
            _clock.SetBounds(right - clockWidth, y, clockWidth, clockHalf);
            _clockDate.SetBounds(right - clockWidth, y + clockHalf, clockWidth, field - clockHalf);

            int netTextW = S(40);
            try
            {
                if (_net.Font != null && !string.IsNullOrEmpty(_net.Text))
                {
                    Size nm = TextRenderer.MeasureText(_net.Text, _net.Font, Size.Empty,
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    if (nm.Width > 0) netTextW = nm.Width + S(4);
                }
            }
            catch { }
            _net.SetBounds(_clock.Left - S(12) - netTextW, y, netTextW, field);

            int netIconS = S(16);
            _netIcon.SetBounds(_net.Left - S(6) - netIconS, y + (field - netIconS) / 2, netIconS, netIconS);
            EnsureNetIcon(netIconS);
            _netIcon.Visible = _netIconOk != null;

            int iconS = S(18);
            _assistantIcon.SetBounds(_netIcon.Left - S(12) - iconS, y + (field - iconS) / 2, iconS, iconS);
            EnsureAssistantIcon(iconS);
            _assistantIcon.Visible = _iconIdle != null;

            _diagIcon.SetBounds(_assistantIcon.Left - S(10) - iconS, y + (field - iconS) / 2, iconS, iconS);
            EnsureDiagIcon(iconS);
            _diagIcon.Visible = _diagIconIdle != null;

            _shotIcon.SetBounds(_diagIcon.Left - S(10) - iconS, y + (field - iconS) / 2, iconS, iconS);
            EnsureShotIcon(iconS);
            _shotIcon.Visible = _shotIdle != null;

            _divider.SetBounds(_shotIcon.Left - S(12), y + S(7), Math.Max(1, S(1)), field - S(14));

            int gap = S(10);
            int x = _brand.Right + gap;
            int xEnd = _divider.Left - gap;
            int avail = xEnd - x - gap * 3;
            int equalW = S(152);
            int winMin = S(146);
            if (avail - 3 * equalW < winMin)
            {
                equalW = (avail - winMin) / 3;
                if (equalW < S(96)) equalW = Math.Max(S(80), avail / 4);
            }
            int winW = avail - 3 * equalW;
            if (winW < S(96)) winW = S(96);

            _system.SetBounds(x, y, equalW, field);
            x = _system.Right + gap;
            _tools.SetBounds(x, y, equalW, field);
            x = _tools.Right + gap;
            _apps.SetBounds(x, y, equalW, field);
            x = _apps.Right + gap;
            _windows.SetBounds(x, y, winW, field);
        }

        #endregion

        #region status

        private void OnTick(object sender, EventArgs e)
        {
            _clock.Text = NowClock();
            _clockDate.Text = NowDate();
            _ticks++;
            if (_ticks % 5 == 0) UpdateNetwork();
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SYSTEMTIME
        {
            public short wYear;
            public short wMonth;
            public short wDayOfWeek;
            public short wDay;
            public short wHour;
            public short wMinute;
            public short wSecond;
            public short wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TIME_ZONE_INFORMATION
        {
            public int Bias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string StandardName;
            public SYSTEMTIME StandardDate;
            public int StandardBias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DaylightName;
            public SYSTEMTIME DaylightDate;
            public int DaylightBias;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetTimeZoneInformation(ref TIME_ZONE_INFORMATION lpTimeZoneInformation);

        // Read the timezone from the OS on every tick (TimeZoneInfo.Local is not used because
        // the .NET Framework caches it and the clock would never update after Set-Timezone).
        private static DateTime LocalNow()
        {
            try
            {
                TIME_ZONE_INFORMATION tz = new TIME_ZONE_INFORMATION();
                int status = GetTimeZoneInformation(ref tz);
                if (status < 0) return DateTime.Now;
                int effective = tz.Bias + (status == 2 ? tz.DaylightBias : tz.StandardBias);
                return DateTime.UtcNow.AddMinutes(-effective);
            }
            catch
            {
                return DateTime.Now;
            }
        }

        private static string NowClock()
        {
            return LocalNow().ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string NowDate()
        {
            return LocalNow().ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void UpdateNetwork()
        {
            string state = "Unknown";
            Color color = ForeDim;
            bool up = false;
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < interfaces.Length; i++)
                {
                    NetworkInterface nic = interfaces[i];
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (nic.OperationalStatus == OperationalStatus.Up)
                    {
                        up = true;
                        break;
                    }
                }
                if (up)
                {
                    state = "Connected";
                    color = NetOk;
                }
                else
                {
                    state = "Disconnected";
                    color = ForeDim;
                }
            }
            catch
            {
            }

            _net.ForeColor = color;
            if (_net.Text != state)
            {
                _net.Text = state;
                if (IsHandleCreated) Arrange();
            }
            if (!_netIcon.IsDisposed) _netIcon.Invalidate();

            if (up && !_netUp) AutoSync();
            _netUp = up;
        }

        #endregion

        #region actions

        private void Post(Action action)
        {
            if (_resetting || IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(action);
            }
            catch
            {
            }
        }

        private void Reset(AntdUI.Select select)
        {
            if (select.ExpandDrop) select.ExpandDrop = false;
            if (select.SelectedIndex < 0) return;
            _resetting = true;
            try
            {
                select.SelectedIndex = -1;
            }
            finally
            {
                _resetting = false;
            }
        }

        private void RunSystem(string tag)
        {
            if (_resetting) return;
            if (string.IsNullOrEmpty(tag))
            {
                Reset(_system);
                return;
            }

            Reset(_system);

            switch (tag)
            {
                case "shutdown":
                case "reboot":
                    RunPower(tag == "shutdown");
                    break;
                case "wifi":
                    RunWifi();
                    break;
                case "bitlocker":
                    RunBitLocker();
                    break;
                case "syntime":
                    RunTimeSync();
                    break;
            }
        }

        private void RunTool(string tag)
        {
            if (_resetting) return;
            Reset(_tools);

            if (tag == "cmd") Run("cmd.exe", null);
            else if (tag == "notepad") Run("notepad.exe", null);
            else if (tag == "powershell") Run("powershell.exe", "-NoExit");
            else if (tag == "taskmgr") Run("taskmgr.exe", null);
            else if (tag == "msinfo") Run("msinfo32.exe", null);
        }

        private void RunPower(bool shutdown)
        {
            string wpeutil = Path.Combine(Environment.SystemDirectory, "wpeutil.exe");
            if (!File.Exists(wpeutil))
            {
                AntdUI.Message.warn(this, "wpeutil is only available in Windows PE", DialogUi.Font, 3);
                return;
            }
            string question = shutdown ? "Shut down the machine now?" : "Reboot the machine now?";
            DialogResult answer = MessageBox.Show(
                this, question, "System",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.OK) return;

            Run(wpeutil, shutdown ? "shutdown" : "reboot");
        }

        private void RunApp(string indexText)
        {
            if (_resetting) return;
            Reset(_apps);

            int index;
            if (!int.TryParse(indexText, out index) || index < 0 || index >= _appEntries.Count) return;
            AppEntry app = _appEntries[index];
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                string dir = null;
                if (!string.IsNullOrEmpty(app.Path) && Path.IsPathRooted(app.Path))
                    dir = Path.GetDirectoryName(app.Path);
                if (app.Script)
                {
                    string ext = Path.GetExtension(app.Path).ToLowerInvariant();
                    if (ext == ".ps1")
                    {
                        psi.FileName = "powershell.exe";
                        psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + app.Path + "\"";
                    }
                    else
                    {
                        psi.FileName = "cmd.exe";
                        psi.Arguments = "/c \"" + app.Path + "\"";
                    }
                }
                else
                {
                    psi.FileName = app.Path;
                    if (!string.IsNullOrEmpty(app.Args)) psi.Arguments = app.Args;
                }
                psi.UseShellExecute = false;
                if (!string.IsNullOrEmpty(app.Cwd) && Directory.Exists(app.Cwd)) psi.WorkingDirectory = app.Cwd;
                else if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) psi.WorkingDirectory = dir;
                Process.Start(psi);
                AntdUI.Message.success(this, "Started " + app.Name, DialogUi.Font, 3);
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this, app.Name + " failed: " + ex.Message, DialogUi.Font, 3);
            }
        }

        private void RunWifi()
        {
            string connected = null;
            SetBarInteractable(false);
            try
            {
                connected = WifiDialog.Show(_brand);
            }
            finally
            {
                SetBarInteractable(true);
            }
            if (connected != null) AntdUI.Message.success(this, "Connected to " + connected, DialogUi.Font, 3);
        }

        private void RunBitLocker()
        {
            AntdUI.ITask.Run(() =>
            {
                List<string> locked;
                try
                {
                    locked = BitLockerService.GetLockedDrives();
                }
                catch
                {
                    locked = new List<string>();
                }
                Post(() =>
                {
                    if (IsDisposed) return;
                    if (locked.Count == 0)
                    {
                        AntdUI.Message.info(this, "No locked BitLocker volumes found", DialogUi.Font, 3);
                        return;
                    }
                    string unlocked = null;
                    SetBarInteractable(false);
                    try
                    {
                        unlocked = BitLockerDialog.Show(_brand, locked);
                    }
                    finally
                    {
                        SetBarInteractable(true);
                    }
                    if (unlocked != null) AntdUI.Message.success(this, "Drive " + unlocked + ": unlocked", DialogUi.Font, 3);
                });
            });
        }

        private void RunTimeSync()
        {
            AntdUI.ITask.Run(() =>
            {
                string msg;
                TimeSyncResult result = TimeSyncService.Sync(true, out msg);
                Post(() =>
                {
                    if (IsDisposed) return;
                    if (result == TimeSyncResult.Synced) AntdUI.Message.success(this, msg, DialogUi.Font, 3);
                    else if (result == TimeSyncResult.Accurate || result == TimeSyncResult.Busy) AntdUI.Message.info(this, msg, DialogUi.Font, 3);
                    else if (result == TimeSyncResult.NoInternet) AntdUI.Message.warn(this, msg, DialogUi.Font, 3);
                    else AntdUI.Message.error(this, msg, DialogUi.Font, 3);
                });
            });
        }

        private void AutoSync()
        {
            AntdUI.ITask.Run(() =>
            {
                string msg;
                TimeSyncService.Sync(false, out msg);
            });
        }

        private void Run(string file, string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = file;
                if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
                psi.UseShellExecute = false;
                Process.Start(psi);
                AntdUI.Message.success(this, "Started " + Path.GetFileName(file), DialogUi.Font, 3);
            }
            catch (Exception ex)
            {
                AntdUI.Message.error(this, Path.GetFileName(file) + " unavailable: " + ex.Message, DialogUi.Font, 3);
            }
        }

        #region assistant

        private string LoadAssistantSvg()
        {
            if (_assistantSvg != null) return _assistantSvg;
            try
            {
                using (Stream s = typeof(MainForm).Assembly.GetManifestResourceStream("assistant.svg"))
                {
                    if (s == null) return null;
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                    {
                        _assistantSvg = r.ReadToEnd();
                    }
                }
            }
            catch { }
            return _assistantSvg;
        }

        // Render the 2 tinted bitmaps (inactive = ForeDim, active = Accent) for the current state.
        private void EnsureAssistantIcon(int px)
        {
            if (px <= 0) return;
            if (_iconIdle != null && _iconPx == px) return;
            string svg = LoadAssistantSvg();
            if (string.IsNullOrEmpty(svg)) return;
            Bitmap idle = null;
            Bitmap active = null;
            try
            {
                idle = AntdUI.SvgExtend.SvgToBmp(svg, px, px, ForeDim);
                active = AntdUI.SvgExtend.SvgToBmp(svg, px, px, Accent);
            }
            catch { }
            if (_iconIdle != null) _iconIdle.Dispose();
            if (_iconActive != null) _iconActive.Dispose();
            _iconIdle = idle;
            _iconActive = active;
            _iconPx = px;
        }

        private void OnAssistantPaint(object sender, PaintEventArgs e)
        {
            Bitmap bmp = _assistantActive ? _iconActive : _iconIdle;
            if (bmp == null) return;
            e.Graphics.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
        }

        private void OnAssistantClick(object sender, EventArgs e)
        {
            OpenAssistant();
        }

        // Active state: modal is open / engine is answering.
        internal void SetAssistantActive(bool active)
        {
            try
            {
                if (_assistantActive == active) return;
                _assistantActive = active;
                if (!_assistantIcon.IsDisposed) _assistantIcon.Invalidate();
            }
            catch { }
        }

        private string LoadDiagSvg()
        {
            if (_diagSvg != null) return _diagSvg;
            try
            {
                using (Stream s = typeof(MainForm).Assembly.GetManifestResourceStream("diagnostic.svg"))
                {
                    if (s == null) return null;
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                        _diagSvg = r.ReadToEnd();
                }
            }
            catch { }
            return _diagSvg;
        }

        private void EnsureDiagIcon(int px)
        {
            if (px <= 0) return;
            if (_diagIconIdle != null && _diagPx == px) return;
            string svg = LoadDiagSvg();
            if (string.IsNullOrEmpty(svg)) return;
            Bitmap idle = null;
            Bitmap active = null;
            try
            {
                idle = AntdUI.SvgExtend.SvgToBmp(svg, px, px, ForeDim);
                active = AntdUI.SvgExtend.SvgToBmp(svg, px, px, Accent);
            }
            catch { }
            if (_diagIconIdle != null) _diagIconIdle.Dispose();
            if (_diagIconActive != null) _diagIconActive.Dispose();
            _diagIconIdle = idle;
            _diagIconActive = active;
            _diagPx = px;
        }

        private void OnDiagPaint(object sender, PaintEventArgs e)
        {
            Bitmap bmp = _diagActive ? _diagIconActive : _diagIconIdle;
            if (bmp == null) return;
            e.Graphics.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
        }

        private void OnDiagClick(object sender, EventArgs e)
        {
            OpenDiagnostic();
        }

        internal void SetDiagActive(bool active)
        {
            try
            {
                if (_diagActive == active) return;
                _diagActive = active;
                if (!_diagIcon.IsDisposed) _diagIcon.Invalidate();
            }
            catch { }
        }

        private void OpenDiagnostic()
        {
            if (_resetting) return;
            CloseOthers(null);
            SetBarInteractable(false);
            SetDiagActive(true);
            try
            {
                SystemDiagnosticDialog.Show(_brand);
            }
            finally
            {
                SetBarInteractable(true);
                SetDiagActive(false);
            }
        }

        private string LoadShotSvg()
        {
            if (_shotSvg != null) return _shotSvg;
            try
            {
                using (Stream s = typeof(MainForm).Assembly.GetManifestResourceStream("screenshot.svg"))
                {
                    if (s == null) return null;
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                        _shotSvg = r.ReadToEnd();
                }
            }
            catch { }
            return _shotSvg;
        }

        private void EnsureShotIcon(int px)
        {
            if (px <= 0) return;
            if (_shotIdle != null && _shotPx == px) return;
            string svg = LoadShotSvg();
            if (string.IsNullOrEmpty(svg)) return;
            Bitmap idle = null;
            Bitmap hover = null;
            try
            {
                idle = AntdUI.SvgExtend.SvgToBmp(svg, px, px, ForeDim);
                hover = AntdUI.SvgExtend.SvgToBmp(svg, px, px, Accent);
            }
            catch { }
            if (_shotIdle != null) _shotIdle.Dispose();
            if (_shotHover != null) _shotHover.Dispose();
            _shotIdle = idle;
            _shotHover = hover;
            _shotPx = px;
        }

        private void OnShotPaint(object sender, PaintEventArgs e)
        {
            Bitmap bmp = _shotHovering ? _shotHover : _shotIdle;
            if (bmp == null) return;
            e.Graphics.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
        }

        private void OnShotClick(object sender, EventArgs e)
        {
            CaptureScreenshot();
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, KeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private delegate IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int VK_SNAPSHOT = 0x2C;

        private void InstallPrintScreenHook()
        {
            try
            {
                if (_kbHook != IntPtr.Zero) return;
                _kbProc = HookProc;
                _kbHook = SetWindowsHookEx(WH_KEYBOARD_LL, _kbProc, GetModuleHandle(null), 0);
            }
            catch { }
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
                {
                    if (Marshal.ReadInt32(lParam) == VK_SNAPSHOT)
                    {
                        try { BeginInvoke((Action)CaptureScreenshot); } catch { }
                    }
                }
            }
            catch { }
            return CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }

        private void CaptureScreenshot()
        {
            try
            {
                string path = ScreenCapture.CapturePrimary();
                if (!string.IsNullOrEmpty(path))
                    AntdUI.Notification.success(this, "Screenshot saved", path, AntdUI.TAlignFrom.TR, UiFonts.Regular(9.5f), 5);
                else
                    AntdUI.Notification.warn(this, "Screenshot", "Could not save the screenshot.", AntdUI.TAlignFrom.TR, UiFonts.Regular(9.5f), 5);
            }
            catch (Exception ex)
            {
                try { AntdUI.Notification.error(this, "Screenshot", ex.Message, AntdUI.TAlignFrom.TR, UiFonts.Regular(9.5f), 5); } catch { }
            }
        }

        private string LoadNetSvg()
        {
            if (_netSvg != null) return _netSvg;
            try
            {
                using (Stream s = typeof(MainForm).Assembly.GetManifestResourceStream("network.svg"))
                {
                    if (s == null) return null;
                    using (StreamReader r = new StreamReader(s, Encoding.UTF8))
                        _netSvg = r.ReadToEnd();
                }
            }
            catch { }
            return _netSvg;
        }

        private void EnsureNetIcon(int px)
        {
            if (px <= 0) return;
            if (_netIconOk != null && _netPx == px) return;
            string svg = LoadNetSvg();
            if (string.IsNullOrEmpty(svg)) return;
            Bitmap ok = null;
            Bitmap off = null;
            try
            {
                ok = AntdUI.SvgExtend.SvgToBmp(svg, px, px, NetOk);
                off = AntdUI.SvgExtend.SvgToBmp(svg, px, px, ForeDim);
            }
            catch { }
            if (_netIconOk != null) _netIconOk.Dispose();
            if (_netIconOff != null) _netIconOff.Dispose();
            _netIconOk = ok;
            _netIconOff = off;
            _netPx = px;
        }

        private void OnNetPaint(object sender, PaintEventArgs e)
        {
            Bitmap bmp = _netUp ? _netIconOk : _netIconOff;
            if (bmp == null) return;
            e.Graphics.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
        }

        // Blocks bar interaction while a modal dialog is open WITHOUT disabling the
        // whole form: a disabled AntdUI Label paints a dark "disabled plate" over the
        // whitebox text. Non-interactive children (brand/labels/divider) stay enabled.
        private void SetBarInteractable(bool on)
        {
            try
            {
                foreach (Control c in Controls)
                {
                    if (c == _brand || c == _clock || c == _net || c == _divider) continue;
                    c.Enabled = on;
                }
            }
            catch { }
        }

        // The whitebox brand label paints with its own logic (plain text + accent on
        // the parent background) so modal-mask / AntdUI target effects can never lay
        // a dark "disabled plate" over it while a dialog is open.
        private sealed class WhiteboxLabel : Label
        {
            internal WhiteboxLabel()
            {
                BackColor = Color.Transparent;
                TextAlign = ContentAlignment.MiddleLeft;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        private void OpenAssistant()
        {
            if (_resetting) return;
            CloseOthers(null);
            SetBarInteractable(false);
            SetAssistantActive(true);
            try
            {
                ChatDialog.Show(_brand);
            }
            finally
            {
                SetBarInteractable(true);
                SetAssistantActive(false);
            }
        }

        #endregion

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            ReassertTopMost();
        }

        private void ReassertTopMost()
        {
            try
            {
                TopMost = false;
                TopMost = true;
            }
            catch { }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        #endregion

        #region apps & windows

        private void ApplyApps(List<AppEntry> entries)
        {
            if (_apps.ExpandDrop) _apps.ExpandDrop = false;
            _appEntries.Clear();
            if (entries != null) _appEntries.AddRange(entries);
            _apps.Items.Clear();
            for (int i = 0; i < _appEntries.Count; i++)
            {
                AntdUI.SelectItem app = new AntdUI.SelectItem(_appEntries[i].Name, i.ToString());
                if (!string.IsNullOrEmpty(_appEntries[i].Icon)) app.SetIcon(_appEntries[i].Icon);
                _apps.Items.Add(app);
            }
        }

        private void CloseOthers(AntdUI.Select except)
        {
            ReassertTopMost();
            AntdUI.Select[] all = { _system, _tools, _apps, _windows };
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == except) continue;
                try
                {
                    if (all[i].ExpandDrop) all[i].ExpandDrop = false;
                }
                catch { }
            }
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        private void RefreshWindows()
        {
            List<AntdUI.SelectItem> items = new List<AntdUI.SelectItem>();
            try
            {
                uint self = (uint)Process.GetCurrentProcess().Id;
                EnumWindows((h, l) =>
                {
                    try
                    {
                        if (!IsWindowVisible(h)) return true;
                        if ((GetWindowLong(h, -20) & 0x80) != 0) return true;
                        uint pid;
                        GetWindowThreadProcessId(h, out pid);
                        if (pid == self) return true;
                        int len = GetWindowTextLength(h);
                        if (len <= 0) return true;
                        StringBuilder sb = new StringBuilder(len + 1);
                        GetWindowText(h, sb, sb.Capacity);
                        string title = sb.ToString();
                        if (string.IsNullOrEmpty(title)) return true;
                        StringBuilder cb = new StringBuilder(256);
                        GetClassName(h, cb, 256);
                        string cls = cb.ToString();
                        if (cls == "Shell_TrayWnd" || cls == "Progman" || cls == "WorkerW" ||
                            cls == "Windows.UI.Core.CoreWindow") return true;
                        items.Add(new AntdUI.SelectItem(title, h.ToInt64().ToString()));
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
            if (_windows.ExpandDrop) _windows.ExpandDrop = false;
            _windows.Items.Clear();
            for (int i = 0; i < items.Count && i < 50; i++) _windows.Items.Add(items[i]);
        }

        private void ActivateWindow(string tag)
        {
            if (_resetting) return;
            Reset(_windows);
            if (string.IsNullOrEmpty(tag)) return;
            long value;
            if (!long.TryParse(tag, out value)) return;
            IntPtr hwnd = new IntPtr(value);
            try
            {
                if (IsIconic(hwnd)) ShowWindow(hwnd, 9);
                SetForegroundWindow(hwnd);
                if (GetForegroundWindow() != hwnd)
                {
                    ShowWindow(hwnd, 5);
                    BringWindowToTop(hwnd);
                    SetForegroundWindow(hwnd);
                }
            }
            catch { }
        }

        #endregion
    }
}
