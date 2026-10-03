using System;
using System.IO;

internal static class LauncherShortcutPolicy
{
    internal static void CreatePrimaryShortcut(string path, string installRoot)
    {
        string launcher = Path.Combine(installRoot, "MW3Launcher.exe");
        InstalledShellLink.Create(path, launcher, installRoot, "Open the MechWarrior 3 Remastered launcher", launcher);
    }

    internal static void RemoveOwnedLegacyDesktopShortcuts()
    {
        string[] desktops = {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };
        string[] names = { "MechWarrior 3 Remaster.lnk", "MechWarrior 3 - Pirate's Moon.lnk" };
        foreach (string desktop in desktops)
        foreach (string name in names)
        {
            string path = Path.Combine(desktop, name);
            if (IsOwnedLegacyShortcut(path)) File.Delete(path);
        }
    }

    private static bool IsOwnedLegacyShortcut(string path)
    {
        if (!File.Exists(path)) return false;
        try
        {
            string[] link = InstalledShellLink.Read(path);
            return IsOwnedLegacyTarget(link[0], link[1]);
        }
        catch { return false; }
    }

    internal static bool IsOwnedLegacyTarget(string target, string arguments)
    {
        string executable = Path.GetFileName(target ?? "");
        if (!executable.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) &&
            !executable.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase)) return false;
        string command = arguments ?? "";
        bool knownScript = command.IndexOf("Launch-MechWarrior3-Remaster.ps1", StringComparison.OrdinalIgnoreCase) >= 0 ||
            command.IndexOf("Launch-PiratesMoon.ps1", StringComparison.OrdinalIgnoreCase) >= 0;
        bool knownProject = command.IndexOf("Mechwarrior 3 Remaster", StringComparison.OrdinalIgnoreCase) >= 0 ||
            command.IndexOf("Mechwarrior 3 Modern", StringComparison.OrdinalIgnoreCase) >= 0;
        return knownScript && knownProject;
    }
}
