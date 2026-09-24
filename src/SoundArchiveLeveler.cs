// Applies the reviewed level candidate to user-supplied US v1.2 sound banks.
// No game audio is embedded in the installer.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal static class SoundArchiveLeveler
{
    private const int EntrySize = 148;
    private const string HighHash = "71C4688E38D59E03D3E0A63C8EF90CCE0359103890904AAAB5DC461647F484A4";
    private const string LowHash = "E259704B36069339BAD035AE571F7733A6655ED2020C4D7E24B5C8872A3B09C3";
    private const string LeveledHighHash = "20AD72D51EAFFB4447A7CE09B408B017CFAA5A7034A82E73B85B539355579BCB";
    private const string LeveledLowHash = "612F8EDB1E26884AAD04E34F3E73D41D7661F8652F97EB765C8C28EAC9A37D98";
    private static readonly Dictionary<string, double> GainDb = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        { "env_big_launch.wav", -9.0 },
        { "wep_ac10_fire.wav", -9.0 },
        { "wep_uac10_fire.wav", -9.0 },
        { "mech_nuke.wav", -6.0 },
        { "env_powdown.wav", -6.0 },
        { "wep_ac_hit.wav", -6.0 },
        { "veh_hover.wav", -6.0 }
    };
    private static readonly Dictionary<string, int> ExpectedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        { "env_big_launch.wav", 1 },
        { "wep_ac10_fire.wav", 4 },
        { "wep_uac10_fire.wav", 4 },
        { "mech_nuke.wav", 1 },
        { "env_powdown.wav", 1 },
        { "wep_ac_hit.wav", 4 },
        { "veh_hover.wav", 1 }
    };

    internal static void ApplyToStagedGame(string gameRoot)
    {
        string zbd = Path.Combine(gameRoot, "zbd");
        string backup = Path.Combine(gameRoot, "OriginalSoundArchives");
        if (Directory.Exists(backup)) throw new InvalidDataException("The game already has an original-sound backup folder.");
        foreach (string name in new[] { "soundsH.zbd", "soundsL.zbd" })
            VerifyHash(Path.Combine(zbd, name), name == "soundsH.zbd" ? HighHash : LowHash);
        Directory.CreateDirectory(backup);
        string candidates = Path.Combine(backup, ".candidates");
        Directory.CreateDirectory(candidates);
        try
        {
            foreach (string name in new[] { "soundsH.zbd", "soundsL.zbd" })
            {
                string source = Path.Combine(zbd, name);
                File.Copy(source, Path.Combine(backup, name));
                Patch(source, Path.Combine(candidates, name), name == "soundsH.zbd" ? HighHash : LowHash);
                VerifyHash(Path.Combine(candidates, name), name == "soundsH.zbd" ? LeveledHighHash : LeveledLowHash);
            }
            foreach (string name in new[] { "soundsH.zbd", "soundsL.zbd" })
                File.Copy(Path.Combine(candidates, name), Path.Combine(zbd, name), true);
        }
        finally { Directory.Delete(candidates, true); }
    }

    private static void VerifyHash(string path, string expected)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream input = File.OpenRead(path))
            if (!BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(Path.GetFileName(path) + " is not the reviewed sound archive.");
    }

#if SOUND_LEVEL_TOOL
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: SoundArchiveLeveler <original-zbd-folder> <candidate-output-folder>");
            return 2;
        }
        try
        {
            string source = Path.GetFullPath(args[0]);
            string output = Path.GetFullPath(args[1]);
            if (source.Equals(output, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The output folder must differ from the original folder.");
            Directory.CreateDirectory(output);
            foreach (string name in new[] { "soundsH.zbd", "soundsL.zbd" })
            {
                string input = Path.Combine(source, name);
                string destination = Path.Combine(output, name);
                if (Path.GetFullPath(input).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The output must not replace an original archive.");
                Patch(input, destination, name == "soundsH.zbd" ? HighHash : LowHash);
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
#endif

    private static void Patch(string input, string output, string expectedHash)
    {
        byte[] original = File.ReadAllBytes(input);
        using (SHA256 sha = SHA256.Create())
        {
            string actualHash = BitConverter.ToString(sha.ComputeHash(original)).Replace("-", "");
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(Path.GetFileName(input) + " is not the reviewed US v1.2 sound archive.");
        }
        byte[] leveled = (byte[])original.Clone();
        int header = checked(original.Length - 8);
        if (BitConverter.ToUInt32(original, header) != 1) throw new InvalidDataException("Unsupported ZBD version.");
        uint count = BitConverter.ToUInt32(original, header + 4);
        if (count == 0 || count > 10000) throw new InvalidDataException("Invalid ZBD entry count.");
        int table = checked(header - (int)count * EntrySize);
        if (table <= 0) throw new InvalidDataException("Invalid ZBD table.");
        Dictionary<string, int> found = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int previousEnd = 0;
        for (int index = 0; index < (int)count; index++)
        {
            int row = checked(table + index * EntrySize);
            int start = checked((int)BitConverter.ToUInt32(original, row));
            int length = checked((int)BitConverter.ToUInt32(original, row + 4));
            if (start != previousEnd || length <= 0 || start + (long)length > table)
                throw new InvalidDataException("Invalid or noncontiguous ZBD entry.");
            previousEnd = checked(start + length);
            int nameLength = Array.IndexOf(original, (byte)0, row + 8, 64);
            if (nameLength < 0) nameLength = row + 72;
            string name = Encoding.ASCII.GetString(original, row + 8, nameLength - row - 8);
            double gain;
            if (!GainDb.TryGetValue(name, out gain)) continue;
            PatchWave(original, leveled, start, length, gain);
            found[name] = found.ContainsKey(name) ? found[name] + 1 : 1;
        }
        if (previousEnd != table) throw new InvalidDataException("ZBD data does not end at its table.");
        foreach (KeyValuePair<string, int> expected in ExpectedCounts)
        {
            int actual;
            found.TryGetValue(expected.Key, out actual);
            if (actual != expected.Value)
                throw new InvalidDataException("Unexpected occurrence count for " + expected.Key + ": " + actual + ".");
        }
        string temporary = output + ".partial";
        if (File.Exists(temporary)) throw new IOException("A partial output already exists: " + temporary);
        if (File.Exists(output))
        {
            using (SHA256 sha = SHA256.Create())
                if (!BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(output))).Replace("-", "")
                    .Equals(BitConverter.ToString(sha.ComputeHash(leveled)).Replace("-", ""), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("An existing candidate differs from this build: " + output);
            Console.WriteLine(Path.GetFileName(output) + " already matches this reviewed candidate.");
            return;
        }
        try
        {
            File.WriteAllBytes(temporary, leveled);
            File.Move(temporary, output);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        using (SHA256 sha = SHA256.Create())
            Console.WriteLine(Path.GetFileName(output) + " " + BitConverter.ToString(sha.ComputeHash(leveled)).Replace("-", "") +
                " (original " + expectedHash + ")");
    }

    private static void PatchWave(byte[] original, byte[] leveled, int start, int length, double gainDb)
    {
        int end = checked(start + length);
        if (length < 44 || Encoding.ASCII.GetString(original, start, 4) != "RIFF" ||
            Encoding.ASCII.GetString(original, start + 8, 4) != "WAVE")
            throw new InvalidDataException("Target sound is not a RIFF WAV.");
        int format = 0, bits = 0, dataStart = -1, dataLength = 0;
        for (int cursor = start + 12; cursor + 8 <= end; )
        {
            string id = Encoding.ASCII.GetString(original, cursor, 4);
            int size = BitConverter.ToInt32(original, cursor + 4);
            int chunk = checked(cursor + 8);
            if (size < 0 || chunk + (long)size > end) throw new InvalidDataException("Invalid WAV chunk.");
            if (id == "fmt " && size >= 16)
            {
                format = BitConverter.ToUInt16(original, chunk);
                bits = BitConverter.ToUInt16(original, chunk + 14);
            }
            if (id == "data") { dataStart = chunk; dataLength = size; }
            cursor = checked(chunk + size + (size & 1));
        }
        if (format != 1 || bits != 8 || dataStart < 0 || dataLength == 0)
            throw new InvalidDataException("Target sound is not supported 8-bit PCM.");
        double factor = Math.Pow(10.0, gainDb / 20.0);
        for (int offset = dataStart; offset < dataStart + dataLength; offset++)
        {
            int sample = original[offset] - 128;
            int scaled = 128 + (int)Math.Round(sample * factor, MidpointRounding.AwayFromZero);
            leveled[offset] = (byte)Math.Max(0, Math.Min(255, scaled));
        }
    }
}
