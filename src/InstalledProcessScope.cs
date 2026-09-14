using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class InstalledProcessScope
{
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const string PlayerSlot = @"\\.\Mailslot\cdaudioplr_Mailslot";
    private static readonly IntPtr InvalidHandle = new IntPtr(-1);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(IntPtr file, byte[] buffer, uint bytesToWrite,
        out uint bytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    public static bool HasRunningGame(string gameRoot)
    {
        return AnyOwnedProcess("Mech3fixup", gameRoot, "Mech3fixup.exe");
    }

    public static void StopLaunchers(string installRoot, Action<string> log)
    {
        List<Process> ownedLaunchers = new List<Process>();
        foreach (Process process in Process.GetProcessesByName("MW3Launcher"))
        {
            bool owned = false;
            try
            {
                string executable = process.MainModule == null ? null : process.MainModule.FileName;
                owned = IsOwnedExecutablePath(executable, installRoot, "MW3Launcher.exe");
                if (owned) ownedLaunchers.Add(process);
            }
            catch { }
            finally { if (!owned) process.Dispose(); }
        }

        foreach (Process process in ownedLaunchers)
        {
            try
            {
                if (process.CloseMainWindow() && process.WaitForExit(3000))
                {
                    if (log != null) log("Closed an installation-owned launcher before cleanup.");
                    continue;
                }

                process.Kill();
                if (!process.WaitForExit(3000))
                    throw new InvalidOperationException("the launcher did not exit within three seconds");
                if (log != null) log("Stopped an installation-owned launcher before cleanup.");
            }
            catch (ArgumentException) { }
            catch (Exception ex)
            {
                throw new InvalidOperationException("An installed launcher is still running and could not be closed. Close it and retry uninstall. " + ex.Message, ex);
            }
            finally { process.Dispose(); }
        }
    }

    public static void StopAudioPlayers(string gameRoot, Action<string> log)
    {
        string installRoot = GetInstallRoot(gameRoot);
        List<Process> ownedPlayers = new List<Process>();
        foreach (Process process in Process.GetProcessesByName("cdaudioplr"))
        {
            bool owned = false;
            try
            {
                string executable = process.MainModule == null ? null : process.MainModule.FileName;
                owned = IsOwnedExecutablePath(executable, installRoot, Path.Combine("mcicda", "cdaudioplr.exe"));
                if (owned) ownedPlayers.Add(process);
            }
            catch { }
            finally { if (!owned) process.Dispose(); }
        }

        if (ownedPlayers.Count == 0) return;
        bool gracefulRequested = RequestAudioShutdown();
        foreach (Process process in ownedPlayers)
        {
            try
            {
                if (gracefulRequested && process.WaitForExit(3000))
                {
                    if (log != null) log("Stopped installation-owned CD audio helper through its shutdown protocol.");
                    continue;
                }

                process.Kill();
                if (!process.WaitForExit(3000))
                    throw new InvalidOperationException("the helper did not exit within three seconds");
                if (log != null) log("Forced an unresponsive installation-owned CD audio helper to stop.");
            }
            catch (Exception ex)
            {
                if (log != null) log("Warning: could not stop an installation-owned CD audio helper: " + ex.Message);
            }
            finally { process.Dispose(); }
        }
    }

    internal static bool IsOwnedExecutablePath(string executable, string installRoot, string requiredSuffix)
    {
        if (String.IsNullOrEmpty(executable) || String.IsNullOrEmpty(installRoot)) return false;
        string fullExecutable = Path.GetFullPath(executable);
        string root = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullExecutable.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
            fullExecutable.EndsWith(Path.DirectorySeparatorChar + requiredSuffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequestAudioShutdown()
    {
        IntPtr slot = CreateFile(PlayerSlot, GenericWrite, FileShareRead, IntPtr.Zero,
            OpenExisting, FileAttributeNormal, IntPtr.Zero);
        if (slot == InvalidHandle) return false;
        try
        {
            byte[] message = new byte[64];
            byte[] command = Encoding.ASCII.GetBytes("0 exit");
            Array.Copy(command, message, command.Length);
            uint written;
            return WriteFile(slot, message, (uint)message.Length, out written, IntPtr.Zero) && written == (uint)message.Length;
        }
        finally { CloseHandle(slot); }
    }

    private static bool AnyOwnedProcess(string processName, string gameRoot, string requiredSuffix)
    {
        string installRoot = GetInstallRoot(gameRoot);
        foreach (Process process in Process.GetProcessesByName(processName))
        {
            try
            {
                string executable = process.MainModule == null ? null : process.MainModule.FileName;
                if (IsOwnedExecutablePath(executable, installRoot, requiredSuffix)) return true;
            }
            catch { }
            finally { process.Dispose(); }
        }
        return false;
    }

    private static string GetInstallRoot(string gameRoot)
    {
        DirectoryInfo parent = Directory.GetParent(Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar));
        return parent == null ? gameRoot : parent.FullName;
    }
}
