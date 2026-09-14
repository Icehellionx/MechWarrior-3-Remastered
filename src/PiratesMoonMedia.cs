using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

internal static class PiratesMoonMedia
{
    internal const string DiscRetailExecutableSha256 = "57CD2EE74BAFDA3EEB9E9353C7C78A8B7C213A81F3040F1BE7C2A164C505DDD9";
    internal const string RipRetailExecutableSha256 = "F2B2BFFE513DD3FE252BF435BAB40A0526809DEB901083806F87EF225192A821";
    internal const string NoDiscExecutableSha256 = "B28ECB70A6A5AFC01074C0ED32BFDABBD189EB3630CB2CDC528107CE67CAEA0E";

    // The commonly used no-disc executable differs from the verified US
    // retail executable only at these two conditional-branch opcodes. Apply
    // the same deterministic transformation to user-supplied ISO installs so
    // runtime behavior no longer depends on Windows virtual optical drives.
    internal static void RemoveRuntimeDiscCheck(string executablePath)
    {
        string sourceHash = Sha256(executablePath);
        if (sourceHash != DiscRetailExecutableSha256 && sourceHash != RipRetailExecutableSha256)
            throw new InvalidDataException("The Pirate's Moon ISO did not produce the supported US retail executable.");

        byte[] executable = File.ReadAllBytes(executablePath);
        if (sourceHash == DiscRetailExecutableSha256)
        {
            PatchBytes(executable, 0x1967F7,
                new byte[] { 0x53, 0xff, 0x15, 0x20, 0xe1, 0x5d, 0x00, 0x83, 0xf8, 0x05, 0x75, 0x2b },
                new byte[] { 0xc6, 0x03, 0x2e, 0xc6, 0x43, 0x01, 0x5c, 0x90, 0x90, 0x90, 0x90, 0x90 });
        }
        PatchByte(executable, 0x21322A, 0x4D, 0x4C);
        PatchByte(executable, 0x213236, 0x48, 0x4C);
        File.WriteAllBytes(executablePath, executable);

        if (Sha256(executablePath) != NoDiscExecutableSha256)
            throw new InvalidDataException("The Pirate's Moon runtime disc-check patch did not produce the verified executable.");
    }

    private static void PatchByte(byte[] executable, int offset, byte expected, byte replacement)
    {
        if (offset < 0 || offset >= executable.Length || executable[offset] != expected)
            throw new InvalidDataException("The Pirate's Moon executable did not match the verified disc-check patch layout.");
        executable[offset] = replacement;
    }

    private static void PatchBytes(byte[] executable, int offset, byte[] expected, byte[] replacement)
    {
        if (expected.Length != replacement.Length)
            throw new InvalidOperationException("The Pirate's Moon patch definition is inconsistent.");
        for (int i = 0; i < expected.Length; i++)
            if (offset + i >= executable.Length || executable[offset + i] != expected[i])
                throw new InvalidDataException("The Pirate's Moon executable did not match the verified disc path-check layout.");
        Buffer.BlockCopy(replacement, 0, executable, offset, replacement.Length);
    }

    internal static string Sha256(string path)
    {
        using (SHA256 hash = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
    }

    internal static void CreateMountableIsoFromExtractedArchive(string archiveRoot, string isoPath)
    {
        string normalizedRoot = Path.GetFullPath(archiveRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string[] cueFiles = Directory.GetFiles(archiveRoot, "*.cue", SearchOption.AllDirectories);
        if (cueFiles.Length != 1)
            throw new InvalidDataException("The Pirate's Moon ISO-version ZIP must contain exactly one CUE file.");

        string cuePath = cueFiles[0];
        string[] lines = File.ReadAllLines(cuePath);
        Match[] fileMatches = lines.Select(delegate(string line) {
            return Regex.Match(line, "^\\s*FILE\\s+\"([^\"]+)\"\\s+BINARY\\s*$", RegexOptions.IgnoreCase);
        }).Where(delegate(Match match) { return match.Success; }).ToArray();
        if (fileMatches.Length != 1)
            throw new InvalidDataException("The Pirate's Moon CUE must reference exactly one binary track file.");

        string binPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(cuePath), fileMatches[0].Groups[1].Value));
        if (!binPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(binPath))
            throw new InvalidDataException("The Pirate's Moon CUE references a missing or unsafe BIN path.");

        int dataStart = -1;
        int dataEnd = -1;
        bool inDataTrack = false;
        bool afterDataTrack = false;
        int trackCount = 0;
        foreach (string line in lines)
        {
            Match track = Regex.Match(line, "^\\s*TRACK\\s+(\\d+)\\s+(\\S+)\\s*$", RegexOptions.IgnoreCase);
            if (track.Success)
            {
                trackCount++;
                if (trackCount == 1)
                {
                    if (track.Groups[1].Value != "01" || !track.Groups[2].Value.Equals("MODE1/2352", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The Pirate's Moon disc image must begin with TRACK 01 MODE1/2352.");
                    inDataTrack = true;
                }
                else
                {
                    if (trackCount == 2 && !track.Groups[2].Value.Equals("AUDIO", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The track following Pirate's Moon data must be audio.");
                    inDataTrack = false;
                    afterDataTrack = true;
                }
                continue;
            }

            Match index = Regex.Match(line, "^\\s*INDEX\\s+01\\s+(\\d{2,3}):(\\d{2}):(\\d{2})\\s*$", RegexOptions.IgnoreCase);
            if (!index.Success) continue;
            int sector = ParseCueSector(index);
            if (inDataTrack && dataStart < 0) dataStart = sector;
            else if (afterDataTrack && dataEnd < 0) dataEnd = sector;
        }
        if (dataStart < 0) throw new InvalidDataException("The Pirate's Moon data track has no INDEX 01 entry.");

        const int rawSectorSize = 2352;
        const int dataOffset = 16;
        const int dataSize = 2048;
        long binLength = new FileInfo(binPath).Length;
        if (dataEnd < 0)
        {
            if (binLength % rawSectorSize != 0) throw new InvalidDataException("The Pirate's Moon BIN has an incomplete raw sector.");
            dataEnd = checked((int)(binLength / rawSectorSize));
        }
        if (dataEnd <= dataStart || binLength < (long)dataEnd * rawSectorSize)
            throw new InvalidDataException("The Pirate's Moon CUE track boundaries exceed the BIN file.");
        int isoSectorCount = ReadIsoVolumeSectorCount(binPath, dataStart);
        if (isoSectorCount < 17 || isoSectorCount > dataEnd - dataStart)
            throw new InvalidDataException("The Pirate's Moon ISO 9660 volume exceeds its declared data track.");
        int conversionEnd = checked(dataStart + isoSectorCount);

        string destination = Path.GetFullPath(isoPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        byte[] sectorBuffer = new byte[rawSectorSize];
        try
        {
            using (FileStream input = File.OpenRead(binPath))
            using (FileStream output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.Position = (long)dataStart * rawSectorSize;
                for (int sectorNumber = dataStart; sectorNumber < conversionEnd; sectorNumber++)
                {
                    ReadExactly(input, sectorBuffer);
                    if (!IsMode1Sector(sectorBuffer))
                        throw new InvalidDataException("The Pirate's Moon BIN contains a non-Mode-1 sector inside its data track.");
                    output.Write(sectorBuffer, dataOffset, dataSize);
                }
            }
            ValidateIso9660(destination);
        }
        catch
        {
            if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
    }

    private static int ParseCueSector(Match index)
    {
        int minutes = Int32.Parse(index.Groups[1].Value);
        int seconds = Int32.Parse(index.Groups[2].Value);
        int frames = Int32.Parse(index.Groups[3].Value);
        if (seconds >= 60 || frames >= 75) throw new InvalidDataException("The Pirate's Moon CUE contains an invalid track index.");
        return checked((minutes * 60 + seconds) * 75 + frames);
    }

    private static int ReadIsoVolumeSectorCount(string binPath, int dataStart)
    {
        byte[] descriptor = new byte[2352];
        using (FileStream input = File.OpenRead(binPath))
        {
            input.Position = checked((long)(dataStart + 16) * descriptor.Length);
            ReadExactly(input, descriptor);
        }
        if (!IsMode1Sector(descriptor) || descriptor[16] != 1 || Encoding.ASCII.GetString(descriptor, 17, 5) != "CD001" || descriptor[22] != 1)
            throw new InvalidDataException("The Pirate's Moon BIN does not contain an ISO 9660 primary volume descriptor.");
        uint littleEndian = BitConverter.ToUInt32(descriptor, 16 + 80);
        uint bigEndian = ((uint)descriptor[16 + 84] << 24) | ((uint)descriptor[16 + 85] << 16) |
            ((uint)descriptor[16 + 86] << 8) | descriptor[16 + 87];
        if (littleEndian == 0 || littleEndian != bigEndian || littleEndian > Int32.MaxValue)
            throw new InvalidDataException("The Pirate's Moon ISO 9660 volume size is invalid.");
        return (int)littleEndian;
    }

    private static void ReadExactly(Stream input, byte[] buffer)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int count = input.Read(buffer, offset, buffer.Length - offset);
            if (count == 0) throw new EndOfStreamException("The Pirate's Moon BIN ended inside a raw sector.");
            offset += count;
        }
    }

    private static bool IsMode1Sector(byte[] sector)
    {
        if (sector[0] != 0 || sector[11] != 0 || sector[15] != 1) return false;
        for (int i = 1; i <= 10; i++) if (sector[i] != 0xff) return false;
        return true;
    }

    private static void ValidateIso9660(string isoPath)
    {
        byte[] descriptor = new byte[7];
        using (FileStream iso = File.OpenRead(isoPath))
        {
            if (iso.Length < 17L * 2048) throw new InvalidDataException("The converted Pirate's Moon data track is too small.");
            iso.Position = 16L * 2048;
            if (iso.Read(descriptor, 0, descriptor.Length) != descriptor.Length)
                throw new InvalidDataException("The converted Pirate's Moon ISO descriptor is incomplete.");
        }
        if (descriptor[0] != 1 || Encoding.ASCII.GetString(descriptor, 1, 5) != "CD001" || descriptor[6] != 1)
            throw new InvalidDataException("The converted Pirate's Moon data track is not a mountable ISO 9660 image.");
    }
}
