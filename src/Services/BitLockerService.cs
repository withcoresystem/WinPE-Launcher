using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinPeLauncher.Services
{
    internal static class BitLockerService
    {
        internal static List<string> GetLockedDrives()
        {
            List<string> locked = new List<string>();
            try
            {
                string output = RunManageBde("-status");
                string currentLetter = null;

                string[] lines = output.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    Match volMatch = Regex.Match(line, @"^.*Volume\s+([A-Z]):");
                    if (volMatch.Success)
                        currentLetter = volMatch.Groups[1].Value;

                    if (line.Contains("Lock Status:") && (line.Contains("Locked") || line.Contains("\tLocked")))
                    {
                        if (currentLetter != null && !locked.Contains(currentLetter))
                            locked.Add(currentLetter);
                    }
                }
            }
            catch { }
            return locked;
        }

        internal static bool UnlockDrive(string driveLetter, string recoveryPassword)
        {
            try
            {
                string digitsOnly = Regex.Replace(recoveryPassword, "[^0-9]", "");
                RunManageBde("-unlock " + driveLetter + ": -RecoveryPassword " + digitsOnly);

                string check = RunManageBde("-status " + driveLetter + ":");
                return check.Contains("Lock Status:") && (check.Contains("Unlocked") || check.Contains("\tUnlocked"));
            }
            catch
            {
                return false;
            }
        }

        private static string RunManageBde(string arguments)
        {
            string output = Exec.Run("manage-bde", arguments, 15000);
            if (output == "[TIMEOUT]") return "";
            return output;
        }
    }
}
