using System;
using Microsoft.Win32;

internal sealed class GameInstallRegistration
{
    public string MachineKeyPath;
    public string VirtualStoreKeyPath;
    public string InstallPath;
    public string Program;
    public string Version;
    public int InstallOptions;
}

internal static class GameInstallRegistry
{
    internal const int CompleteInstallOptions = 0x00050707;

    internal static GameInstallRegistration Describe(string gameRoot, bool piratesMoon)
    {
        if (String.IsNullOrWhiteSpace(gameRoot)) throw new ArgumentException("A game installation path is required.", "gameRoot");
        string installPath = gameRoot.TrimEnd('\\') + "\\";
        string product = piratesMoon ? "MechWarrior 3 EP1" : "MechWarrior 3";
        string machinePath = "SOFTWARE\\MicroProse\\" + product + "\\1.0";
        return new GameInstallRegistration
        {
            MachineKeyPath = machinePath,
            VirtualStoreKeyPath = "Software\\Classes\\VirtualStore\\MACHINE\\SOFTWARE\\WOW6432Node\\MicroProse\\" + product + "\\1.0",
            InstallPath = installPath,
            Program = piratesMoon ? installPath : null,
            Version = piratesMoon ? "1.0" : "1.2",
            InstallOptions = CompleteInstallOptions
        };
    }

    internal static void WriteMachineRegistration(string gameRoot, bool piratesMoon)
    {
        GameInstallRegistration registration = Describe(gameRoot, piratesMoon);
        using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (RegistryKey key = hklm.CreateSubKey(registration.MachineKeyPath))
        {
            string existingInstallPath = key.GetValue("InstallPath") as string;
            if (!CanClaimRegistration(existingInstallPath, registration.InstallPath)) return;
            WriteValues(key, registration);
        }
    }

    internal static void WriteVirtualStoreRegistration(string gameRoot, bool piratesMoon)
    {
        GameInstallRegistration registration = Describe(gameRoot, piratesMoon);
        using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
        using (RegistryKey key = hkcu.CreateSubKey(registration.VirtualStoreKeyPath))
            WriteValues(key, registration);
    }

    internal static void RemoveOwnedRegistrations(string gameRoot, bool piratesMoon)
    {
        GameInstallRegistration registration = Describe(gameRoot, piratesMoon);
        using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
            RemoveIfOwned(hklm, registration.MachineKeyPath, registration.InstallPath);
        using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
            RemoveIfOwned(hkcu, registration.VirtualStoreKeyPath, registration.InstallPath);
    }

    private static void WriteValues(RegistryKey key, GameInstallRegistration registration)
    {
        key.SetValue("InstallPath", registration.InstallPath, RegistryValueKind.String);
        if (registration.Program != null) key.SetValue("Program", registration.Program, RegistryValueKind.String);
        key.SetValue("Version", registration.Version, RegistryValueKind.String);
        key.SetValue("InstallOptions", registration.InstallOptions, RegistryValueKind.DWord);
    }

    internal static bool CanClaimRegistration(string existingInstallPath, string requestedInstallPath)
    {
        return String.IsNullOrEmpty(existingInstallPath) ||
            String.Equals(NormalizePath(existingInstallPath), NormalizePath(requestedInstallPath), StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveIfOwned(RegistryKey root, string keyPath, string expectedInstallPath)
    {
        using (RegistryKey key = root.OpenSubKey(keyPath, false))
        {
            if (key == null) return;
            string actualInstallPath = key.GetValue("InstallPath") as string;
            if (!String.Equals(NormalizePath(actualInstallPath), NormalizePath(expectedInstallPath), StringComparison.OrdinalIgnoreCase)) return;
        }
        root.DeleteSubKeyTree(keyPath, false);
    }

    private static string NormalizePath(string path)
    {
        return String.IsNullOrEmpty(path) ? "" : path.TrimEnd('\\');
    }
}
