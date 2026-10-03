using System;
using System.Diagnostics;
using System.IO;

internal static class InstalledDocument
{
    internal static string Require(string root, string relative)
    {
        string path = Path.Combine(root, relative);
        if (!File.Exists(path)) throw new FileNotFoundException("The installed document is missing. Reinstall from the trusted setup to restore it.");
        return path;
    }

    internal static void Open(string root, string relative)
    {
        Process.Start(new ProcessStartInfo(Require(root, relative)) { UseShellExecute = true });
    }
}
