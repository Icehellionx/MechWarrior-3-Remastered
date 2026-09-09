using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("MechWarrior 3 Remastered Uninstaller")]
[assembly: AssemblyDescription("Removes the MechWarrior 3 Remastered preservation build.")]
[assembly: AssemblyCompany("MechWarrior 3 Remastered contributors")]
[assembly: AssemblyProduct("MechWarrior 3 Remastered")]
[assembly: AssemblyCopyright("Copyright © 2026 MechWarrior 3 Remastered contributors")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

internal static class Uninstaller
{
    [STAThread]
    private static void Main()
    {
        string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        if (MessageBox.Show("Remove MechWarrior 3 Remastered? Saved pilots and settings inside the installation folder will also be removed.", "Uninstall MechWarrior 3 Remastered", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "MechWarrior 3 Remastered.lnk"));
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "MechWarrior 3 - Pirate's Moon.lnk"));
            string menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "MechWarrior 3 Remastered");
            if (Directory.Exists(menu)) Directory.Delete(menu, true);
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                hklm.DeleteSubKeyTree("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MW3Remastered", false);

            string command = "/c ping 127.0.0.1 -n 2 > nul & rmdir /s /q \"" + root + "\"";
            ProcessStartInfo cleanup = new ProcessStartInfo("cmd.exe", command);
            cleanup.CreateNoWindow = true; cleanup.UseShellExecute = false;
            Process.Start(cleanup);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private static void DeleteShortcut(string path) { if (File.Exists(path)) File.Delete(path); }
}
