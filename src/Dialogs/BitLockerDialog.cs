using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using WinPeLauncher.Services;
using static WinPeLauncher.Theme;

namespace WinPeLauncher
{
    internal static class BitLockerDialog
    {
        internal static string Show(Control anchor, List<string> drives)
        {
            float k = (UiFonts.Scale <= 0f ? 1f : UiFonts.Scale) / (UiFonts.DpiScale <= 0f ? 1f : UiFonts.DpiScale);
            Func<int, int> S = v => (int)Math.Round(v * k);

            Panel panel = new Panel();
            int width = KeyHolderWidth(anchor);
            panel.Size = new Size(S(width), S(176));
            panel.BackColor = DialogBack;
            panel.Font = DialogUi.Font;
            try
            {
                Label lblDrive = DialogUi.MakeLabel("Locked volume", ForeDim, ContentAlignment.MiddleLeft);
                lblDrive.SetBounds(0, 0, width, 18);

                AntdUI.Select selDrive = DialogUi.MakeSelect("Select a locked volume");
                selDrive.SetBounds(0, 22, width, 34);
                for (int i = 0; i < drives.Count; i++)
                    selDrive.Items.Add(new AntdUI.SelectItem(drives[i] + ":", drives[i]));
                if (drives.Count > 0) selDrive.SelectedIndex = 0;

                Label lblKey = DialogUi.MakeLabel("Recovery key (48 digits)", ForeDim, ContentAlignment.MiddleLeft);
                lblKey.SetBounds(0, 66, width, 18);

                AntdUI.Input inpKey = DialogUi.MakeInput("123456-123456-123456-...", false);
                inpKey.SetBounds(0, 90, width, 34);

                Label status = DialogUi.MakeLabel("", ForeDim, ContentAlignment.TopLeft);
                status.SetBounds(0, 134, width, 36);

                panel.Controls.Add(lblDrive);
                panel.Controls.Add(selDrive);
                panel.Controls.Add(lblKey);
                panel.Controls.Add(inpKey);
                panel.Controls.Add(status);

                bool formatting = false;
                inpKey.TextChanged += (s, e) =>
                {
                    if (formatting) return;
                    string cur = inpKey.Text;
                    string digits = Regex.Replace(cur, "[^0-9]", "");
                    if (digits.Length > 48) digits = digits.Substring(0, 48);
                    string formatted = FormatGroups(digits);
                    if (formatted != cur)
                    {
                        formatting = true;
                        try
                        {
                            inpKey.Text = formatted;
                            inpKey.SelectionStart = formatted.Length;
                        }
                        finally
                        {
                            formatting = false;
                        }
                    }
                };

                string unlockedDrive = null;

                DialogUi.ScaleBounds(panel, k);

                AntdUI.Modal.Config config = new AntdUI.Modal.Config(new AntdUI.Target(anchor), "BitLocker Unlock", (object)panel);
                config.SetColorScheme(AntdUI.TAMode.Dark);
                config.SetOk("Unlock");
                config.SetCancel("Cancel");
                config.SetMask(true);
                config.SetMaskClosable(false);
                config.SetLoadingDisableCancel(true);
                config.SetDefaultAcceptButton(true);
                config.SetFont(DialogUi.Font);
                config.OkFont = DialogUi.Font;
                config.CancelFont = DialogUi.Font;
                config.OnOk = cfg =>
                {
                    string drive = DialogUi.ReadUi(panel, () => selDrive.SelectedValue as string);
                    string key = DialogUi.ReadUi(panel, () => inpKey.Text) ?? "";
                    if (string.IsNullOrEmpty(drive))
                    {
                        DialogUi.SetStatus(status, "Select a locked volume", ErrRed);
                        return false;
                    }
                    string digits = Regex.Replace(key, "[^0-9]", "");
                    if (digits.Length != 48)
                    {
                        DialogUi.SetStatus(status, "Recovery key must be 48 digits (found " + digits.Length + ")", ErrRed);
                        return false;
                    }
                    DialogUi.SetStatus(status, "Unlocking " + drive + ": ...", ForeMain);
                    bool ok = BitLockerService.UnlockDrive(drive, key);
                    if (ok)
                    {
                        unlockedDrive = drive;
                        DialogUi.SetStatus(status, "Drive " + drive + ": unlocked", NetOk);
                        return true;
                    }
                    DialogUi.SetStatus(status, "Unlock failed - check the recovery key", ErrRed);
                    return false;
                };

                DialogResult dr = AntdUI.Modal.open(config);
                if (dr == DialogResult.OK && unlockedDrive != null) return unlockedDrive;
                return null;
            }
            finally
            {
                try { panel.Dispose(); } catch { }
            }
        }

        // Holder width fits 48 digits + 7 dashes + 2 spare chars (1 at each end), plus the
        // AntdUI Input inner padding (WaveSize/border/paddgap) so the string is never clipped.
        private static int KeyHolderWidth(Control anchor)
        {
            try
            {
                using (Graphics g = anchor.CreateGraphics())
                {
                    float dpi = 1f;
                    if (dpi <= 0f || float.IsNaN(dpi) || float.IsInfinity(dpi)) dpi = 1f;
                    int border = (int)Math.Ceiling((4f + 0.5f) * dpi);
                    int lineHeight = (int)g.MeasureString(AntdUI.Config.NullText, DialogUi.Font).Height;
                    int pad = (int)(lineHeight * 0.4f);
                    int margin = border + pad;
                    string sample = "000000-000000-000000-000000-000000-000000-000000-000000";
                    sample = "0" + sample + "0";
                    int textWidth = (int)Math.Ceiling(g.MeasureString(sample, DialogUi.Font).Width);
                    int w = textWidth + margin * 2;
                    if (w < 360) w = 360;
                    return w;
                }
            }
            catch { return 440; }
        }

        private static string FormatGroups(string digits)
        {
            if (digits.Length == 0) return "";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && i % 6 == 0) sb.Append('-');
                sb.Append(digits[i]);
            }
            return sb.ToString();
        }
    }
}
