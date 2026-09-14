using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

internal static class AudioOutputProbe
{
    [DllImport("winmm.dll")]
    private static extern uint waveOutGetNumDevs();

    private static int Main(string[] args)
    {
        if (args.Length != 1 || !File.Exists(args[0]))
        {
            Console.Error.WriteLine("Usage: AudioOutputProbe <audio-file>");
            return 2;
        }

        uint devices = waveOutGetNumDevs();
        Console.WriteLine("Wave output devices visible to this account: " + devices);
        if (devices == 0)
        {
            Console.Error.WriteLine("No wave output device is available to this account.");
            return 2;
        }

        using (Mp3WaveOutPlayer player = new Mp3WaveOutPlayer())
        {
            if (!player.Start(Path.GetFullPath(args[0])))
            {
                Console.Error.WriteLine("The managed MP3 decoder/waveOut player could not start.");
                return 1;
            }
            Thread.Sleep(150);
            if (!player.Pause()) { Console.Error.WriteLine("waveOut pause failed."); return 1; }
            Thread.Sleep(50);
            if (!player.Resume()) { Console.Error.WriteLine("waveOut resume failed."); return 1; }
            Thread.Sleep(150);
            player.Stop();
            Console.WriteLine("Managed MP3 decode and waveOut play/pause/resume passed.");
            return 0;
        }
    }
}
