using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("MechWarrior 3 Remastered Launcher")]
[assembly: AssemblyDescription("Launches MechWarrior 3 from user-supplied original media on modern Windows.")]
[assembly: AssemblyCompany("MechWarrior 3 Remastered contributors")]
[assembly: AssemblyProduct("MechWarrior 3 Remastered")]
[assembly: AssemblyCopyright("Copyright © 2026 MechWarrior 3 Remastered contributors")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class Launcher
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            Dictionary<string, string> cfg = ReadConfig(Path.Combine(root, "install.cfg"));
            bool pm = args.Length > 0 && args[0].Equals("pm", StringComparison.OrdinalIgnoreCase);
            string gameRoot = Path.Combine(root, pm ? "Pirates Moon" : "MechWarrior 3");
            string isoKey = pm ? "PiratesMoonIso" : "Mw3Iso";
            string iso;
            if (!cfg.TryGetValue(isoKey, out iso) || !File.Exists(iso))
            {
                using (OpenFileDialog picker = new OpenFileDialog())
                {
                    picker.Title = "Locate your " + (pm ? "Pirate's Moon" : "MechWarrior 3") + " ISO";
                    picker.Filter = "Disc images (*.iso)|*.iso|All files (*.*)|*.*";
                    if (picker.ShowDialog() != DialogResult.OK) return;
                    iso = picker.FileName;
                }
            }

            if (Process.GetProcessesByName("Mech3fixup").Length != 0)
                throw new InvalidOperationException("Close the running MechWarrior game before starting another title.");
            foreach (Process stale in Process.GetProcessesByName("cdaudioplr"))
                try { stale.Kill(); stale.WaitForExit(3000); } catch { }
            Thread.Sleep(1500);

            MountIso(iso);
            SetRegistry(gameRoot, pm);
            string exe = Path.Combine(gameRoot, "Mech3fixup.exe");
            if (!File.Exists(exe)) throw new FileNotFoundException("The installed game executable is missing.", exe);

            ProcessStartInfo start = new ProcessStartInfo(exe);
            start.WorkingDirectory = gameRoot;
            start.UseShellExecute = false;
            using (Process game = Process.Start(start)) game.WaitForExit();
            Thread.Sleep(750);
            foreach (Process stale in Process.GetProcessesByName("cdaudioplr"))
                try { stale.Kill(); } catch { }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "MechWarrior 3 Remastered", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Dictionary<string, string> ReadConfig(string path)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(path))
        {
            int split = line.IndexOf('=');
            if (split > 0) values[line.Substring(0, split)] = line.Substring(split + 1);
        }
        return values;
    }

    private static void MountIso(string iso)
    {
        string escaped = iso.Replace("'", "''");
        string command = "$ErrorActionPreference='Stop'; $p='" + escaped + "'; " +
            "$i=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue; " +
            "if(-not $i -or -not $i.Attached){Mount-DiskImage -ImagePath $p | Out-Null}";
        ProcessStartInfo info = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"");
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        using (Process process = Process.Start(info))
        {
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Windows could not mount the selected ISO. Right-click the ISO, choose Mount, then try again.");
        }
    }

    private static void SetRegistry(string gameRoot, bool pm)
    {
        string slashRoot = gameRoot.TrimEnd('\\') + "\\";
        string product = pm ? "MechWarrior 3 EP1" : "MechWarrior 3";
        using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
        using (RegistryKey install = hkcu.CreateSubKey("Software\\Classes\\VirtualStore\\MACHINE\\SOFTWARE\\WOW6432Node\\MicroProse\\" + product + "\\1.0"))
        {
            install.SetValue("InstallPath", slashRoot, RegistryValueKind.String);
            if (pm) install.SetValue("Program", slashRoot, RegistryValueKind.String);
            install.SetValue("Version", pm ? "1.0" : "1.2", RegistryValueKind.String);
            install.SetValue("InstallOptions", 0x00050707, RegistryValueKind.DWord);
        }
        using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
        using (RegistryKey settings = hkcu.CreateSubKey("Software\\MicroProse\\" + product + "\\1.0"))
        {
            settings.SetValue("HWCardFlag", 1, RegistryValueKind.DWord);
            settings.SetValue("HWCardDev", 0, RegistryValueKind.DWord);
            settings.SetValue("InGameVMode", 5, RegistryValueKind.DWord);
            settings.SetValue("SoundVolume", BitConverter.GetBytes(1.0f), RegistryValueKind.Binary);
            settings.SetValue("CDVolume", BitConverter.GetBytes(0.10f), RegistryValueKind.Binary);
            settings.SetValue("TextureMemory_HW", 3, RegistryValueKind.DWord);
            settings.SetValue("GfxFlags_HW", 0x1f, RegistryValueKind.DWord);
            settings.SetValue("Shadow", 1, RegistryValueKind.DWord);
            settings.SetValue("ShadowParts_HW", 1, RegistryValueKind.DWord);
            settings.SetValue("EffectsLevel_HW", 0, RegistryValueKind.DWord);
            settings.SetValue("ObjectLOD_HW", 0, RegistryValueKind.DWord);
        }
    }
}
