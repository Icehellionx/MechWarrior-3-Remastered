using System;
using System.Diagnostics;
using System.IO;

internal static class CdAudioParentLifetimeSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--launch-child")
        {
            Process player = Process.Start(new ProcessStartInfo(args[1])
            {
                WorkingDirectory = Path.GetDirectoryName(args[1]),
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Console.WriteLine(player.Id);
            player.Dispose();
            return 0;
        }

        if (args.Length != 1 || !File.Exists(args[0])) return 2;
        string self = Process.GetCurrentProcess().MainModule.FileName;
        Process launcher = Process.Start(new ProcessStartInfo(self, "--launch-child \"" + args[0] + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        });
        string output = launcher.StandardOutput.ReadToEnd().Trim();
        launcher.WaitForExit();
        if (launcher.ExitCode != 0) return Fail("The short-lived parent failed to launch the helper.");

        int playerId;
        if (!Int32.TryParse(output, out playerId)) return Fail("The parent did not report the helper process ID.");
        try
        {
            using (Process player = Process.GetProcessById(playerId))
            {
                if (!player.WaitForExit(5000)) return Fail("CD audio helper remained alive after its parent exited.");
            }
        }
        catch (ArgumentException) { }

        Console.WriteLine("CD audio parent lifetime: passed.");
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
