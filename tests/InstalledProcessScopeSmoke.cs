using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

internal static class InstalledProcessScopeSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Directory.Exists(args[0])) return 2;
        string gameRoot = Path.GetFullPath(args[0]);
        string playerPath = Path.Combine(gameRoot, "mcicda", "cdaudioplr.exe");
        List<string> log = new List<string>();
        try
        {
            ProcessStartInfo start = new ProcessStartInfo(playerPath);
            start.WorkingDirectory = Path.GetDirectoryName(playerPath);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            using (Process player = Process.Start(start))
            {
                Thread.Sleep(750);
                Assert(!player.HasExited, "CD audio helper exited before the lifecycle test could stop it.");
                InstalledProcessScope.StopAudioPlayers(gameRoot, log.Add);
                Assert(player.WaitForExit(5000), "CD audio helper remained alive after installation-scoped cleanup.");
            }
            Assert(log.Exists(delegate(string line) { return line.IndexOf("shutdown protocol", StringComparison.OrdinalIgnoreCase) >= 0; }),
                "Cleanup did not confirm the graceful CD audio shutdown protocol.");
            Console.WriteLine("Installation-scoped CD audio cleanup: passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message + Environment.NewLine + String.Join(Environment.NewLine, log.ToArray()));
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
