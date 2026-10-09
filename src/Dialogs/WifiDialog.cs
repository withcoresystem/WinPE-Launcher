using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WinPeLauncher.Services;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal static class WifiDialog
    {
        internal static string Show(Control anchor)
        {
            float k = (UiFonts.Scale <= 0f ? 1f : UiFonts.Scale) / (UiFonts.DpiScale <= 0f ? 1f : UiFonts.DpiScale);
            Func<int, int> S = v => (int)Math.Round(v * k);

            Panel panel = new Panel();
            panel.Size = new Size(S(720), S(268));
            panel.BackColor = DialogBack;
            panel.Font = DialogUi.Font;
            try
            {
                Label lblNets = DialogUi.MakeLabel("Available networks", ForeDim, ContentAlignment.MiddleLeft);
                lblNets.SetBounds(0, 6, 220, 18);

                AntdUI.Button btnScan = new AntdUI.Button();
                btnScan.Text = "Scan networks";
                btnScan.Type = AntdUI.TTypeMini.Primary;
                btnScan.SetBounds(224, 0, 116, 32);
                btnScan.Font = DialogUi.Font;

                ListBox listNets = new ListBox();
                listNets.SetBounds(0, 36, 340, 184);
                listNets.BackColor = FieldBack;
                listNets.ForeColor = ForeMain;
                listNets.BorderStyle = BorderStyle.None;
                listNets.Font = DialogUi.Font;
                listNets.IntegralHeight = false;
                listNets.ItemHeight = S(22);

                Label lblManual = DialogUi.MakeLabel("Manual connection", ForeDim, ContentAlignment.MiddleLeft);
                lblManual.SetBounds(360, 6, 360, 18);

                Label lblSsid = DialogUi.MakeLabel("Network name (SSID)", ForeDim, ContentAlignment.MiddleLeft);
                lblSsid.SetBounds(360, 36, 360, 16);

                AntdUI.Input inputSsid = DialogUi.MakeInput("SSID", false);
                inputSsid.SetBounds(360, 56, 360, 32);

                Label lblPass = DialogUi.MakeLabel("Password (min 6 characters)", ForeDim, ContentAlignment.MiddleLeft);
                lblPass.SetBounds(360, 98, 360, 16);

                AntdUI.Input inputPass = DialogUi.MakeInput("Password", true);
                inputPass.SetBounds(360, 118, 228, 32);

                AntdUI.Checkbox chkShow = new AntdUI.Checkbox();
                chkShow.Text = "Show password";
                chkShow.ForeColor = ForeMain;
                chkShow.SetBounds(596, 124, 124, 20);
                chkShow.Font = DialogUi.Font;
                chkShow.CheckedChanged += (s, e) => { inputPass.UseSystemPasswordChar = !e.Value; };

                Label hint = DialogUi.MakeLabel("Click a network on the left to fill the SSID", ForeDim, ContentAlignment.MiddleLeft);
                hint.SetBounds(360, 158, 360, 16);

                Label status = DialogUi.MakeLabel("Click Scan networks to search for Wi-Fi", ForeDim, ContentAlignment.TopLeft);
                status.SetBounds(0, 232, 720, 36);

                panel.Controls.Add(lblNets);
                panel.Controls.Add(btnScan);
                panel.Controls.Add(listNets);
                panel.Controls.Add(lblManual);
                panel.Controls.Add(lblSsid);
                panel.Controls.Add(inputSsid);
                panel.Controls.Add(lblPass);
                panel.Controls.Add(inputPass);
                panel.Controls.Add(chkShow);
                panel.Controls.Add(hint);
                panel.Controls.Add(status);

                listNets.SelectedIndexChanged += (s, e) =>
                {
                    if (listNets.SelectedIndex < 0) return;
                    string v = listNets.SelectedItem as string;
                    if (string.IsNullOrEmpty(v)) return;
                    inputSsid.Text = v;
                    inputPass.Focus();
                };

                string connectedSsid = null;

                Action doScan = () =>
                {
                    if (!btnScan.Enabled) return;
                    btnScan.Enabled = false;
                    listNets.Items.Clear();
                    status.ForeColor = ForeMain;
                    status.Text = "Scanning for networks...";
                    AntdUI.ITask.Run(() =>
                    {
                        try
                        {
                            bool adapter = WifiService.AdapterDetected();
                            List<string> nets = null;
                            string msg;
                            if (adapter)
                            {
                                nets = WifiService.ScanNetworks();
                                msg = WifiService.LastError;
                            }
                            else
                            {
                                msg = "No Wi-Fi adapter found on this machine";
                            }
                            try
                            {
                                if (panel.IsDisposed || !panel.IsHandleCreated) return;
                                panel.BeginInvoke((Action)(() =>
                                {
                                    if (panel.IsDisposed) return;
                                    listNets.Items.Clear();
                                    if (nets != null)
                                    {
                                        for (int i = 0; i < nets.Count; i++)
                                            listNets.Items.Add(nets[i]);
                                    }
                                    btnScan.Enabled = true;
                                    if (nets != null && nets.Count > 0)
                                    {
                                        status.ForeColor = NetOk;
                                        status.Text = "Found " + nets.Count + " network(s) - click one on the left";
                                    }
                                    else
                                    {
                                        status.ForeColor = ErrRed;
                                        status.Text = msg;
                                    }
                                }));
                            }
                            catch { }
                        }
                        catch { }
                    });
                };

                btnScan.Click += (s, e) => doScan();

                DialogUi.ScaleBounds(panel, k);

                AntdUI.Modal.Config config = new AntdUI.Modal.Config(anchor.FindForm(), "Connect to Wi-Fi", (object)panel);
                config.SetColorScheme(AntdUI.TAMode.Dark);
                config.SetOk("Connect");
                config.SetCancel("Cancel");
                config.SetMask(false);
                config.SetDraggable(false);
                config.SetMaskClosable(false);
                config.SetLoadingDisableCancel(true);
                config.SetDefaultAcceptButton(true);
                config.SetFont(DialogUi.Font);
                config.OkFont = DialogUi.Font;
                config.CancelFont = DialogUi.Font;
                config.OnOk = cfg =>
                {
                    string ssid = (DialogUi.ReadUi(panel, () => inputSsid.Text) ?? "").Trim();
                    string pass = (DialogUi.ReadUi(panel, () => inputPass.Text) ?? "");
                    if (ssid.Length == 0)
                    {
                        DialogUi.SetStatus(status, "Enter or select a network name", ErrRed);
                        return false;
                    }
                    if (pass.Length < 6)
                    {
                        DialogUi.SetStatus(status, "Password must be at least 6 characters", ErrRed);
                        return false;
                    }
                    DialogUi.SetStatus(status, "Connecting to " + ssid + "...", ForeMain);
                    string err = WifiService.Connect(ssid, pass);
                    if (err == "")
                    {
                        connectedSsid = ssid;
                        DialogUi.SetStatus(status, "Connected to " + ssid, NetOk);
                        return true;
                    }
                    DialogUi.SetStatus(status, err, ErrRed);
                    return false;
                };

                DialogResult dr;
                bool prevAnim = AntdUI.Config.Animation;
                try
                {
                    AntdUI.Config.Animation = true;
                    DialogDock.Dock(anchor, config, Accent);
                    dr = AntdUI.Modal.open(config);
                }
                finally
                {
                    AntdUI.Config.Animation = prevAnim;
                    DialogDock.Stop();
                    DialogBorder.Hide();
                }
                if (dr == DialogResult.OK && connectedSsid != null) return connectedSsid;
                return null;
            }
            finally
            {
                try { panel.Dispose(); } catch { }
            }
        }
    }
}
