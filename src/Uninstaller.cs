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
[assembly: AssemblyVersion("1.2.7.0")]
[assembly: AssemblyFileVersion("1.2.7.0")]

internal static class Uninstaller
{
    private const uint MoveFileDelayUntilReboot = 0x00000004;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existingFile, string newFile, uint flags);

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0].Equals("--cleanup", StringComparison.Ordinal))
        {
            Environment.CurrentDirectory = Path.GetTempPath();
            CleanupFiles(args[1], args[2]);
            return;
        }

        string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        bool confirmed = args.Length == 1 && args[0].Equals("--confirmed", StringComparison.Ordinal);
        if (!confirmed && MessageBox.Show("Remove MechWarrior 3 Remastered? Saved pilots and campaign progress will remain in the installation folder for a future reinstall. All other game files, settings, diagnostics, and shortcuts will be removed.", "Uninstall MechWarrior 3 Remastered", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            GameSaveStorage.ValidateInstallationForRemoval(root);
            if (InstalledProcessScope.HasRunningGame(Path.Combine(root, "MechWarrior 3")) ||
                InstalledProcessScope.HasRunningGame(Path.Combine(root, "Pirates Moon")))
                throw new InvalidOperationException("Close MechWarrior 3 and Pirate's Moon before uninstalling.");
            InstalledProcessScope.StopAudioPlayers(Path.Combine(root, "MechWarrior 3"), delegate { });
            InstalledProcessScope.StopAudioPlayers(Path.Combine(root, "Pirates Moon"), delegate { });
            DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "MechWarrior 3 Remastered.lnk"));
            LauncherShortcutPolicy.RemoveOwnedLegacyDesktopShortcuts();
            string menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "MechWarrior 3 Remastered");
            if (Directory.Exists(menu)) Directory.Delete(menu, true);
            GameInstallRegistry.RemoveOwnedRegistrations(Path.Combine(root, "MechWarrior 3"), false);
            GameInstallRegistry.RemoveOwnedRegistrations(Path.Combine(root, "Pirates Moon"), true);
            GameInstallRegistry.RemoveUserSettings(false);
            GameInstallRegistry.RemoveUserSettings(true);
            using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                hklm.DeleteSubKeyTree("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MW3Remastered", false);
            string diagnostics = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MechWarrior 3 Remastered");
            if (Directory.Exists(diagnostics)) Directory.Delete(diagnostics, true);

            StartCleanupWorker(root);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static void StartCleanupWorker(string root)
    {
        string worker = Path.Combine(Path.GetTempPath(), "MW3Remastered-Uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(Application.ExecutablePath, worker, true);
        ProcessStartInfo cleanup = CreateCleanupStartInfo(worker, root, Process.GetCurrentProcess().Id);
        Process.Start(cleanup);
    }

    internal static ProcessStartInfo CreateCleanupStartInfo(string worker, string root, int parentProcessId)
    {
        ProcessStartInfo cleanup = new ProcessStartInfo(worker, "--cleanup " + Quote(root) + " " + parentProcessId);
        cleanup.CreateNoWindow = true;
        cleanup.UseShellExecute = false;
        // A child otherwise inherits the installed uninstaller's current
        // directory, which prevents Windows from removing that directory.
        cleanup.WorkingDirectory = Path.GetTempPath();
        return cleanup;
    }

    private static void CleanupFiles(string root, string parentProcessId)
    {
        string backup = Path.Combine(Path.GetTempPath(), "MW3Remastered-Saves-" + Guid.NewGuid().ToString("N"));
        try
        {
            int processId;
            if (Int32.TryParse(parentProcessId, out processId))
            {
                try { using (Process parent = Process.GetProcessById(processId)) { parent.WaitForExit(15000); } }
                catch (ArgumentException) { }
            }
            InstalledProcessScope.StopLaunchers(root, delegate { });
            bool hadSaves = GameSaveStorage.HasPreservedSaves(root);
            GameSaveStorage.RemoveInstallationPreservingSaves(root, backup);
            MessageBox.Show(hadSaves
                ? "MechWarrior 3 Remastered was removed. Saved pilots and campaign progress remain in the original installation folder."
                : "MechWarrior 3 Remastered was removed.",
                "Uninstall complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message + Environment.NewLine + Environment.NewLine +
                "If cleanup stopped after saves were backed up, recover them from:" + Environment.NewLine + backup,
                "Uninstall failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // The worker runs outside the installation so it can remove the
            // launcher and original uninstaller, then deletes itself on reboot.
            MoveFileEx(Application.ExecutablePath, null, MoveFileDelayUntilReboot);
        }
    }

    private static string Quote(string value) { return "\"" + value.Replace("\"", "\"\"") + "\""; }
    private static void DeleteShortcut(string path) { if (File.Exists(path)) File.Delete(path); }
}
