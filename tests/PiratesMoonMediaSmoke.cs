using System;
using System.IO;
using System.Linq;

internal static class PiratesMoonMediaSmoke
{
    private static int Main(string[] args)
    {
        if (args.Length > 1 || (args.Length == 1 && !File.Exists(args[0])))
        {
            Console.Error.WriteLine("Optionally pass the verified Pirate's Moon retail executable fixture.");
            return 2;
        }

        string root = Path.Combine(Path.GetTempPath(), "mw3-pm-media-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            if (args.Length == 1)
            {
                string executable = Path.Combine(root, "Mech3.exe");
                File.Copy(args[0], executable);
                string sourceHash = PiratesMoonMedia.Sha256(executable);
                byte[] original = File.ReadAllBytes(executable);

                PiratesMoonMedia.RemoveRuntimeDiscCheck(executable);

                byte[] patched = File.ReadAllBytes(executable);
                int[] differences = Enumerable.Range(0, original.Length).Where(i => original[i] != patched[i]).ToArray();
                int[] expectedDifferences = sourceHash == PiratesMoonMedia.DiscRetailExecutableSha256
                    ? Enumerable.Range(0x1967F7, 12).Concat(new[] { 0x21322A, 0x213236 }).ToArray()
                    : new[] { 0x21322A, 0x213236 };
                if (!differences.SequenceEqual(expectedDifferences))
                    throw new InvalidOperationException("Runtime disc-check patch changed unexpected executable bytes.");
                if (PiratesMoonMedia.Sha256(executable) != PiratesMoonMedia.NoDiscExecutableSha256)
                    throw new InvalidOperationException("Runtime disc-check patch produced the wrong executable hash.");

                bool rejected = false;
                try { PiratesMoonMedia.RemoveRuntimeDiscCheck(executable); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Already-patched input was not rejected.");
            }

            VerifyCueBinConversion(root);

            Console.WriteLine("Pirate's Moon ISO runtime policy tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void VerifyCueBinConversion(string root)
    {
        string archive = Path.Combine(root, "iso-version");
        Directory.CreateDirectory(archive);
        string bin = Path.Combine(archive, "PiratesMoon.bin");
        byte[] raw = new byte[2352];
        using (FileStream output = File.Create(bin))
        {
            for (int sector = 0; sector < 22; sector++)
            {
                Array.Clear(raw, 0, raw.Length);
                for (int i = 1; i <= 10; i++) raw[i] = 0xff;
                raw[15] = 1;
                raw[16] = (byte)sector;
                if (sector == 16)
                {
                    raw[16] = 1;
                    raw[17] = (byte)'C'; raw[18] = (byte)'D'; raw[19] = (byte)'0'; raw[20] = (byte)'0'; raw[21] = (byte)'1';
                    raw[22] = 1;
                    raw[96] = 20;
                    raw[103] = 20;
                }
                output.Write(raw, 0, raw.Length);
            }
        }
        File.WriteAllText(Path.Combine(archive, "PiratesMoon.cue"),
            "FILE \"PiratesMoon.bin\" BINARY\r\n" +
            "  TRACK 01 MODE1/2352\r\n" +
            "    INDEX 01 00:00:00\r\n" +
            "  TRACK 02 AUDIO\r\n" +
            "    INDEX 01 00:00:20\r\n");

        string iso = Path.Combine(root, "converted.iso");
        PiratesMoonMedia.CreateMountableIsoFromExtractedArchive(archive, iso);
        if (new FileInfo(iso).Length != 20L * 2048)
            throw new InvalidOperationException("CUE/BIN conversion included the audio track or lost data sectors.");
        using (FileStream converted = File.OpenRead(iso))
        {
            converted.Position = 19L * 2048;
            if (converted.ReadByte() != 19)
                throw new InvalidOperationException("CUE/BIN conversion produced incorrect Mode-1 payload bytes.");
        }
    }
}
