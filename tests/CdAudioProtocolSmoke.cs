using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

internal static class CdAudioProtocolSmoke
{
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 1;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x80;
    private static readonly IntPtr InvalidHandle = new IntPtr(-1);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateMailslot(string name, uint maximumMessageSize, uint readTimeout, IntPtr securityAttributes);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr file, byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);
    [DllImport("kernel32.dll")]
    private static extern bool WriteFile(IntPtr file, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);
    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static int Main(string[] args)
    {
        if (args.Length != 2) return Fail("Usage: CdAudioProtocolSmoke helper.exe expected-track-count");
        int expected;
        if (!Int32.TryParse(args[1], out expected)) return Fail("Invalid expected track count.");
        IntPtr replies = CreateMailslot(@"\\.\Mailslot\winmm_Mailslot", 0, 5000, IntPtr.Zero);
        if (replies == InvalidHandle) return Fail("Could not create wrapper reply mailslot: " + Marshal.GetLastWin32Error());

        Process player = null;
        try
        {
            player = Process.Start(new ProcessStartInfo(Path.GetFullPath(args[0]))
            {
                WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(args[0])),
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (player == null) return Fail("Could not start the CD audio helper.");
            if (!SendWithRetry("1 mci_tracks", 3000)) return Fail("Helper command mailslot did not become ready.");

            byte[] buffer = new byte[64];
            uint read;
            if (!ReadFile(replies, buffer, (uint)buffer.Length, out read, IntPtr.Zero))
                return Fail("Did not receive a helper response: " + Marshal.GetLastWin32Error());
            string response = Encoding.ASCII.GetString(buffer, 0, (int)read).TrimEnd('\0', ' ', '\r', '\n');
            if (!response.Equals(expected + " tracks", StringComparison.Ordinal))
                return Fail("Unexpected track response: " + response);

            if (!SendWithRetry("1 exit", 1000)) return Fail("Could not send helper shutdown command.");
            if (!player.WaitForExit(3000)) return Fail("Helper did not exit after the protocol shutdown command.");
            Console.WriteLine("CD audio protocol: passed (" + response + ")");
            return 0;
        }
        finally
        {
            CloseHandle(replies);
            if (player != null && !player.HasExited) { try { player.Kill(); } catch { } }
            if (player != null) player.Dispose();
        }
    }

    private static bool SendWithRetry(string message, int timeoutMs)
    {
        Stopwatch timer = Stopwatch.StartNew();
        do
        {
            IntPtr slot = CreateFile(@"\\.\Mailslot\cdaudioplr_Mailslot", GenericWrite, FileShareRead,
                IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
            if (slot != InvalidHandle)
            {
                try
                {
                    byte[] data = new byte[64];
                    byte[] encoded = Encoding.ASCII.GetBytes(message);
                    Array.Copy(encoded, data, encoded.Length);
                    uint written;
                    return WriteFile(slot, data, (uint)data.Length, out written, IntPtr.Zero) && written == data.Length;
                }
                finally { CloseHandle(slot); }
            }
            Thread.Sleep(50);
        } while (timer.ElapsedMilliseconds < timeoutMs);
        return false;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
