// Protocol-compatible replacement for the helper from cdaudio-winmm 0.4.0.3.
// The wrapper/protocol design is credited to dippy-dipper/DD and contributors;
// this managed implementation is project code under the repository's MIT license.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

[assembly: System.Reflection.AssemblyTitle("MechWarrior 3 CD Audio Player")]
[assembly: System.Reflection.AssemblyDescription("Plays the locally installed MechWarrior 3 music tracks for cdaudio-winmm.")]
[assembly: System.Reflection.AssemblyCompany("MechWarrior 3 Remastered contributors")]
[assembly: System.Reflection.AssemblyProduct("MechWarrior 3 Remastered")]
[assembly: System.Reflection.AssemblyCopyright("Copyright © 2026 MechWarrior 3 Remastered contributors")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

internal static class CdAudioPlayer
{
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint OpenExisting = 3;
    private const uint FileAttributeNormal = 0x00000080;
    private const uint MailslotWaitForever = 0xffffffff;
    private const string PlayerSlot = @"\\.\Mailslot\cdaudioplr_Mailslot";
    private const string WrapperSlot = @"\\.\Mailslot\winmm_Mailslot";
    private const string Alias = "mw3track";
    private static readonly IntPtr InvalidHandle = new IntPtr(-1);
    private static readonly object StateLock = new object();
    private static volatile bool quitting;
    private static int desiredTrack;
    private static int currentTrack;
    private static int pausedTrack;
    private static int commandVersion;
    private static bool notify;
    private static bool aliasOpen;
    private static uint auxVolume = 0xffffffff;
    private static int volumeOverride = 100;
    private static string musicDirectory;
    private static string extension;
    private static int trackCount;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateMailslot(string name, uint maximumMessageSize, uint readTimeout, IntPtr securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr file, byte[] buffer, uint bytesToRead, out uint bytesRead, IntPtr overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateFile(string name, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll")]
    private static extern bool WriteFile(IntPtr file, byte[] buffer, uint bytesToWrite, out uint bytesWritten, IntPtr overlapped);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern uint mciSendString(string command, StringBuilder result, uint resultLength, IntPtr callback);

    [DllImport("winmm.dll")]
    private static extern uint waveOutSetVolume(IntPtr waveOut, uint volume);

    [STAThread]
    private static void Main()
    {
        bool ownsMutex;
        using (Mutex singleInstance = new Mutex(true, "MechWarrior3RemasteredCdAudioPlayer", out ownsMutex))
        {
            if (!ownsMutex) return;
            string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            musicDirectory = Path.Combine(root, "music");
            LoadTracks();
            LoadVolumeOverride(root);
            ApplyVolume();

            Thread reader = new Thread(ReadCommands);
            reader.IsBackground = true;
            reader.Name = "CD audio command reader";
            reader.Start();

            RunPlayer();
            CloseTrack();
        }
    }

    private static void LoadTracks()
    {
        string[] mp3 = Directory.Exists(musicDirectory) ? Directory.GetFiles(musicDirectory, "track*.mp3") : new string[0];
        string[] wav = Directory.Exists(musicDirectory) ? Directory.GetFiles(musicDirectory, "track*.wav") : new string[0];
        string[] tracks = mp3.Length != 0 ? mp3 : wav;
        extension = mp3.Length != 0 ? ".mp3" : ".wav";
        int highestTrack = 1;
        foreach (string path in tracks)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            int number;
            if (name.StartsWith("track", StringComparison.OrdinalIgnoreCase) &&
                Int32.TryParse(name.Substring(5), out number) && number > highestTrack) highestTrack = number;
        }
        trackCount = tracks.Length == 0 ? 1 : highestTrack;
    }

    private static void LoadVolumeOverride(string root)
    {
        try
        {
            string path = Path.Combine(root, "cdaudio_vol.ini");
            if (!File.Exists(path)) return;
            string first = File.ReadAllLines(path)[0].Trim();
            int parsed;
            if (Int32.TryParse(first, out parsed)) volumeOverride = Math.Max(0, Math.Min(100, parsed));
        }
        catch { }
    }

    private static void ReadCommands()
    {
        IntPtr slot = CreateMailslot(PlayerSlot, 0, MailslotWaitForever, IntPtr.Zero);
        if (slot == InvalidHandle) { quitting = true; return; }
        try
        {
            byte[] buffer = new byte[256];
            uint read;
            while (!quitting && ReadFile(slot, buffer, (uint)buffer.Length, out read, IntPtr.Zero))
            {
                if (read == 0) continue;
                int end = Array.IndexOf<byte>(buffer, 0, 0, (int)read);
                if (end < 0) end = (int)read;
                HandleCommand(Encoding.ASCII.GetString(buffer, 0, end).Trim());
            }
        }
        finally { CloseHandle(slot); }
    }

    private static void HandleCommand(string command)
    {
        string[] fields = command.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 2) return;
        int value;
        if (!Int32.TryParse(fields[0], out value)) return;
        string name = fields[1];
        lock (StateLock)
        {
            if (name == "aux_vol") { auxVolume = unchecked((uint)value); ApplyVolume(); }
            else if (name == "mci_notify") notify = value != 0;
            else if (name == "mci_from") SetDesiredTrack(value);
            else if (name == "mci_to" && value != desiredTrack && value < 99)
            {
                int requested = value - 1;
                if (requested != desiredTrack) SetDesiredTrack(requested);
            }
            else if (name == "mci_stop") SetDesiredTrack(0);
            else if (name == "mci_pause") SetDesiredTrack(100);
            else if (name == "mci_resume") SetDesiredTrack(pausedTrack);
            else if (name == "mci_tracks") SendToWrapper(trackCount + " tracks");
            else if (name == "exit") quitting = true;
        }
    }

    private static void SetDesiredTrack(int value)
    {
        desiredTrack = value;
        commandVersion++;
    }

    private static void RunPlayer()
    {
        int handledVersion = 0;
        while (!quitting)
        {
            int requested;
            int version;
            lock (StateLock) { requested = desiredTrack; version = commandVersion; }
            if (version != handledVersion)
            {
                HandlePlaybackRequest(requested);
                handledVersion = version;
            }
            PollForCompletion(version);
            Thread.Sleep(75);
        }
    }

    private static void HandlePlaybackRequest(int requested)
    {
        if (requested == 100)
        {
            if (aliasOpen && currentTrack > 0)
            {
                SendMci("pause " + Alias, null);
                pausedTrack = currentTrack;
                SendToWrapper("1 mode");
            }
            return;
        }
        if (requested <= 0)
        {
            CloseTrack();
            currentTrack = 0;
            SendToWrapper("1 mode");
            return;
        }
        if (aliasOpen && pausedTrack == requested)
        {
            SendMci("resume " + Alias, null);
            currentTrack = requested;
            pausedTrack = 0;
            SendToWrapper("2 mode");
            return;
        }

        CloseTrack();
        string path = Path.Combine(musicDirectory, "track" + requested.ToString("00") + extension);
        if (!File.Exists(path)) { SendToWrapper("1 mode"); return; }
        string type = extension.Equals(".wav", StringComparison.OrdinalIgnoreCase) ? "waveaudio" : "mpegvideo";
        if (SendMci("open \"" + path.Replace("\"", "\"\"") + "\" type " + type + " alias " + Alias, null) != 0)
        {
            SendToWrapper("1 mode");
            return;
        }
        aliasOpen = true;
        currentTrack = requested;
        pausedTrack = 0;
        ApplyVolume();
        if (SendMci("play " + Alias, null) == 0) SendToWrapper("2 mode");
        else { CloseTrack(); SendToWrapper("1 mode"); }
    }

    private static void PollForCompletion(int playbackVersion)
    {
        if (!aliasOpen || currentTrack == 0 || pausedTrack != 0) return;
        StringBuilder mode = new StringBuilder(32);
        if (SendMci("status " + Alias + " mode", mode) != 0 || mode.ToString().Equals("playing", StringComparison.OrdinalIgnoreCase)) return;
        CloseTrack();
        lock (StateLock)
        {
            if (commandVersion != playbackVersion) return;
            desiredTrack = 0;
            currentTrack = 0;
            if (notify) { SendToWrapper("1 notify_s"); notify = false; }
        }
        SendToWrapper("1 mode");
    }

    private static uint SendMci(string command, StringBuilder result)
    {
        return mciSendString(command, result, result == null ? 0u : (uint)result.Capacity, IntPtr.Zero);
    }

    private static void CloseTrack()
    {
        if (!aliasOpen) return;
        SendMci("stop " + Alias, null);
        SendMci("close " + Alias, null);
        aliasOpen = false;
    }

    private static void ApplyVolume()
    {
        uint value = auxVolume;
        if (volumeOverride < 100)
        {
            uint channel = (uint)Math.Round(65535.0 * volumeOverride / 100.0);
            value = channel | (channel << 16);
        }
        waveOutSetVolume(IntPtr.Zero, value);
    }

    private static void SendToWrapper(string message)
    {
        IntPtr slot = CreateFile(WrapperSlot, GenericWrite, FileShareRead, IntPtr.Zero, OpenExisting, FileAttributeNormal, IntPtr.Zero);
        if (slot == InvalidHandle) return;
        try
        {
            byte[] data = new byte[64];
            byte[] encoded = Encoding.ASCII.GetBytes(message);
            Array.Copy(encoded, data, Math.Min(encoded.Length, data.Length - 1));
            uint written;
            WriteFile(slot, data, (uint)data.Length, out written, IntPtr.Zero);
        }
        finally { CloseHandle(slot); }
    }
}
