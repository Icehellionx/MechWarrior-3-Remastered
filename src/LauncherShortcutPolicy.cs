using System;
using System.IO;

internal static class LauncherShortcutPolicy
{
    internal static void CreatePrimaryShortcut(string path, string installRoot)
    {
        Type type = Type.GetTypeFromProgID("WScript.Shell");
        dynamic shell = Activator.CreateInstance(type);
        dynamic link = shell.CreateShortcut(path);
        string launcher = Path.Combine(installRoot, "MW3Launcher.exe");
        link.TargetPath = launcher;
        link.Arguments = "";
        link.WorkingDirectory = installRoot;
        link.IconLocation = launcher + ",0";
        link.Description = "Open the MechWarrior 3 Remastered launcher";
        link.Save();
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
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(type);
            dynamic link = shell.CreateShortcut(path);
            return IsOwnedLegacyTarget((string)link.TargetPath, (string)link.Arguments);
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
