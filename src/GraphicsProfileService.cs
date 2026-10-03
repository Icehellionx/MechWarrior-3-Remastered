using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

internal sealed class GraphicsProfile
{
    internal string Path;
    internal byte[] Original;
    internal Dictionary<string, string> Values;
}

// Owns permanent choices. Recovery exclusively owns the process-specific profile.
internal sealed class GraphicsProfileService
{
    internal const string WrapperHash = "FD11B9B6B8A8CC23744DFEDC10E23798860F3A96BD8B2B4C75DD2FDB3BE8F8FB";
    private readonly string root;
    private readonly string stateDirectory;
    private readonly Func<string, bool> running;
    internal GraphicsProfileService(string root) : this(root, InstallationDiagnostics.DirectoryPath, InstalledProcessScope.HasRunningGame) { }
    internal GraphicsProfileService(string root, string stateDirectory, Func<string, bool> running)
    {
        this.root = root; this.stateDirectory = stateDirectory; this.running = running;
    }

    internal GraphicsProfile Load(bool pm)
    {
        string game = Path.Combine(root, pm ? "Pirates Moon" : "MechWarrior 3");
        EnsureIdle(game);
        if (!File.Exists(Path.Combine(game, "Mech3fixup.exe"))) throw new InvalidOperationException("This game is not installed. Select an installed game.");
        if (Hash(Path.Combine(game, "ddraw.dll")) != WrapperHash) throw new InvalidOperationException("The graphics wrapper does not match the qualified build. Settings are unavailable.");
        string path = Path.Combine(game, "DDrawCompat.ini");
        EnsureOrdinary(path);
        byte[] bytes = ReadBounded(path);
        string text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        Dictionary<string, string> values = Parse(text);
        Require(values, "FullscreenMode", "borderless");
        Require(values, "AltTabFix", "keepvidmem(1)");
        Require(values, "DisplayAspectRatio", "4:3");
        Require(values, "PresentationEdgeRepair", "4");
        Require(values, "ResolutionScale", "display(1)");
        Require(values, "SupportedResolutions", "640x480, 1024x768");
        Require(values, "FpsLimiter", "flipstart(30)");
        Require(values, "ResolutionScaleFilter", "bilinear");
        Require(values, "TextureFilter", "af16x");
        Require(values, "LogLevel", "info");
        if (!values.ContainsKey("Antialiasing") || !IsAntialiasing(values["Antialiasing"])) throw new InvalidOperationException("The antialiasing setting is unsupported.");
        if (pm) { Require(values, "VSync", "on"); Require(values, "PresentDelay", "on(50)"); }
        else { Require(values, "CpuAffinityRotation", "off"); Require(values, "RemasterIntroWidescreen", "on"); Require(values, "RemasterIntroChromaCleanup", "on"); Require(values, "PresentationEdgeRepair", "4"); }
        // Refuse unsupported added policy rather than silently ignoring another owner's edits.
        string[] allowed = pm ? new string[] { "FullscreenMode", "AltTabFix", "DisplayAspectRatio", "PresentationEdgeRepair", "FpsLimiter", "ResolutionScale", "ResolutionScaleFilter", "SupportedResolutions", "Antialiasing", "TextureFilter", "LogLevel", "VSync", "PresentDelay" }
            : new string[] { "FullscreenMode", "CpuAffinityRotation", "AltTabFix", "DisplayAspectRatio", "RemasterIntroWidescreen", "RemasterIntroChromaCleanup", "PresentationEdgeRepair", "FpsLimiter", "ResolutionScale", "ResolutionScaleFilter", "SupportedResolutions", "Antialiasing", "TextureFilter", "LogLevel" };
        foreach (string key in values.Keys)
            if (Array.IndexOf(allowed, key) < 0) throw new InvalidOperationException("The profile contains custom policy. Settings will preserve it without editing.");
        return new GraphicsProfile { Path = path, Original = bytes, Values = values };
    }

    internal void Apply(bool pm, GraphicsProfile loaded, string antialiasing)
    {
        if (!IsAntialiasing(antialiasing)) throw new InvalidOperationException("Unsupported antialiasing choice.");
        using (LaunchLease lease = LaunchLease.Acquire(stateDirectory))
        {
            GraphicsProfile current = Load(pm);
            if (loaded == null || !current.Path.Equals(loaded.Path, StringComparison.OrdinalIgnoreCase) || !Equal(current.Original, loaded.Original))
                throw new InvalidOperationException("The profile changed after Settings opened. Reopen Settings before applying.");
            string text = new UTF8Encoding(false, true).GetString(current.Original);
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                int equals = lines[i].IndexOf('=');
                if (equals > 0 && lines[i].Substring(0, equals).Trim().Equals("Antialiasing", StringComparison.OrdinalIgnoreCase))
                {
                    string ending = lines[i].EndsWith("\r") ? "\r" : String.Empty;
                    int comment = lines[i].IndexOfAny(new char[] { '#', ';' }, equals);
                    string suffix = comment < 0 ? String.Empty : " " + lines[i].Substring(comment).TrimEnd('\r');
                    lines[i] = "Antialiasing = " + antialiasing + suffix + ending;
                }
            }
            byte[] replacement = new UTF8Encoding(false).GetBytes(String.Join("\n", lines));
            if (Equal(replacement, current.Original)) return;
            string temporary = current.Path + ".settings-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, replacement);
                EnsureIdle(Path.GetDirectoryName(current.Path));
                if (!Equal(ReadBounded(current.Path), current.Original)) throw new InvalidOperationException("The profile changed during Apply. No settings were written.");
                // One per-game file per Apply: atomic replacement keeps the old file on failure.
                File.Replace(temporary, current.Path, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    private void EnsureIdle(string game)
    {
        EnsureOrdinary(game);
        if (running(game)) throw new InvalidOperationException("Close the game before editing graphics settings.");
        string process = Path.Combine(game, "DDrawCompat-Mech3fixup.ini");
        string[] overrides = { process, process + ".launcher-backup", process + ".launcher-backup.tmp", process + ".launcher-state", Path.Combine(game, "DDrawCompatOverlay-Mech3fixup.ini") };
        foreach (string path in overrides)
            if (File.Exists(path) || Directory.Exists(path)) throw new InvalidOperationException("A process override or recovery record is present. Finish recovery and remove custom overrides before editing settings.");
        foreach (string directory in new string[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) })
            if (File.Exists(Path.Combine(directory, "DDrawCompat", "DDrawCompat.ini")))
                throw new InvalidOperationException("A global or user DDrawCompat profile is present. Settings cannot qualify its additional policy.");
    }

    internal static bool IsAntialiasing(string value) { return value == "msaa4x(0)" || value == "off"; }
    internal static byte[] ReadBounded(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.Length > 65536) throw new InvalidDataException("The profile is too large.");
            using (MemoryStream output = new MemoryStream())
            {
                byte[] buffer = new byte[4096]; int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > 65536) throw new InvalidDataException("The profile is too large.");
                    output.Write(buffer, 0, count);
                }
                return output.ToArray();
            }
        }
    }
    internal static Dictionary<string, string> Parse(string text) { return Parse(text, true); }
    internal static Dictionary<string, string> Parse(string text, bool rejectDuplicates)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in text.TrimStart('\uFEFF').Split('\n'))
        {
            string clean = line.Split('#', ';')[0].Trim();
            if (clean.Length == 0) continue;
            int equals = clean.IndexOf('=');
            if (equals <= 0) throw new InvalidDataException("The profile has an invalid setting.");
            string key = clean.Substring(0, equals).Trim();
            if (rejectDuplicates && values.ContainsKey(key)) throw new InvalidDataException("The profile has duplicate settings.");
            values[key] = clean.Substring(equals + 1).Trim();
        }
        return values;
    }
    internal static string Hash(string path)
    {
        using (SHA256 hash = SHA256.Create()) using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", String.Empty);
    }
    internal static bool Equal(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
    private static void Require(Dictionary<string, string> values, string key, string expected)
    {
        string value;
        if (!values.TryGetValue(key, out value) || value != expected) throw new InvalidOperationException("The profile differs from the qualified baseline. Settings will preserve it without editing.");
    }
    private static void EnsureOrdinary(string path)
    {
        for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Settings cannot edit a linked installation.");
    }
}
