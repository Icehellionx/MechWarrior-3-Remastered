using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

internal static class InstallationDiagnostics
{
    internal static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MechWarrior 3 Remastered");
    internal static string Build(string root) { return Build(root, DirectoryPath); }
    internal static string Build(string root, string logs)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("MechWarrior 3 Remastered diagnostics");
        report.AppendLine("Launcher: " + Assembly.GetExecutingAssembly().GetName().Version);
        report.AppendLine("OS: " + Environment.OSVersion.Platform + " " + Environment.OSVersion.Version + "; 64-bit: " + Environment.Is64BitOperatingSystem);
        report.AppendLine("This report summarizes files/settings; it does not prove gameplay or hardware support.");
        DescribeGame(report, Path.Combine(root, "MechWarrior 3"), "MechWarrior 3");
        DescribeGame(report, Path.Combine(root, "Pirates Moon"), "Pirate's Moon");
        report.AppendLine(); report.AppendLine("Recent launch/recovery (selected events only):");
        try
        {
            string log = Path.Combine(logs, "launcher.log");
            if (!File.Exists(log)) report.AppendLine("No launcher log available.");
            else if (new FileInfo(log).Length > 2 * 1024 * 1024) report.AppendLine("Log exceeds the report limit; review locally.");
            else
            {
                Queue<string> events = new Queue<string>();
                using (StreamReader reader = new StreamReader(log))
                {
                    string line; int count = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (++count > 10000) { events.Clear(); events.Enqueue("Log exceeds the event limit; review locally."); break; }
                        string safe = SafeEvent(line);
                        if (safe == null) continue;
                        events.Enqueue(safe); if (events.Count > 8) events.Dequeue();
                    }
                }
                foreach (string entry in events) report.AppendLine(entry);
                if (events.Count == 0) report.AppendLine("No recognized launch/recovery events.");
            }
        }
        catch { report.AppendLine("Launch summary unavailable; game launch is independent of this report."); }
        report.AppendLine("Wine/Linux and physical HOTAS/force feedback remain unqualified.");
        return report.ToString();
    }

    private static void DescribeGame(StringBuilder report, string game, string title)
    {
        report.AppendLine(); report.AppendLine(title + ":");
        string[] required = { "Mech3fixup.exe", "zipfixup.dll", "ddraw.dll", "winmm.dll", "DDrawCompat.ini", "mcicda/cdaudioplr.exe", "mcicda/NLayer.dll", "IFORCE2.dll", "force_eff.ifr" };
        bool ready = true;
        foreach (string name in required)
        {
            string path = Path.Combine(game, name.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { ready = false; report.AppendLine(name + ": missing"); }
        }
        report.AppendLine("Required file readiness: " + (ready ? "present (not runtime qualification)" : "incomplete / not installed"));
        foreach (string name in new string[] { "ddraw.dll", "DDrawCompat.ini", "DDrawCompat-Mech3fixup.ini", "DDrawCompatOverlay-Mech3fixup.ini" })
        {
            string path = Path.Combine(game, name);
            try
            {
                if (File.Exists(path))
                {
                    string hash = GraphicsProfileService.Hash(path);
                    report.AppendLine(name + " SHA-256: " + hash);
                    if (name == "ddraw.dll") report.AppendLine("Qualified r18 wrapper: " + (hash == GraphicsProfileService.WrapperHash ? "yes" : "no"));
                }
            }
            catch { report.AppendLine(name + ": unreadable"); }
        }
        try
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool custom = false;
            string[] profiles = {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DDrawCompat", "DDrawCompat.ini"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DDrawCompat", "DDrawCompat.ini"),
                Path.Combine(game, "DDrawCompat.ini"), Path.Combine(game, "DDrawCompat-Mech3fixup.ini"), Path.Combine(game, "DDrawCompatOverlay-Mech3fixup.ini")
            };
            for (int i = 0; i < profiles.Length; i++)
            {
                if (!File.Exists(profiles[i])) continue;
                if (i != 2) custom = true;
                foreach (KeyValuePair<string, string> item in GraphicsProfileService.Parse(new UTF8Encoding(false, true).GetString(GraphicsProfileService.ReadBounded(profiles[i])), false)) values[item.Key] = item.Value;
            }
            report.AppendLine("Additional profile layers: " + (custom ? "present; settings below include overrides" : "none"));
            report.AppendLine("Effective configuration candidates (hardware fallback is not measured):");
            foreach (string key in new string[] { "ResolutionScale", "SupportedResolutions", "DisplayAspectRatio", "FullscreenMode", "ResolutionScaleFilter", "TextureFilter", "Antialiasing", "FpsLimiter", "VSync", "PresentDelay", "ForceD3D9On12" })
            {
                string value;
                if (values.TryGetValue(key, out value)) report.AppendLine(key + " = " + SafeValue(value));
            }
            string process = Path.Combine(game, "DDrawCompat-Mech3fixup.ini");
            report.AppendLine("Pending recovery record: " + ((File.Exists(process + ".launcher-state") || File.Exists(process + ".launcher-backup") || File.Exists(process + ".launcher-backup.tmp")) ? "yes; finish recovery before settings edits" : "none"));
        }
        catch { report.AppendLine("Effective configuration unavailable (invalid, missing or oversized profile)."); }
    }

    internal static string SafeValue(string value)
    {
        // Fixed vocabulary: never copy arbitrary profile text, even plausible-looking names.
        string[] allowed = { "off", "on", "on(50)", "borderless", "4:3", "display(1)", "app(1)", "640x480, 1024x768", "bilinear", "point", "af16x", "msaa4x(0)", "flipstart(30)", "wait(30)" };
        return Array.IndexOf(allowed, value) >= 0 ? value : "custom/unsupported (value omitted)";
    }

    internal static string SafeEvent(string line)
    {
        if (line.Length > 1024) return null;
        // Full-line whitelist prevents trailing private paths or injected text being copied.
        Match match = Regex.Match(line, @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} (Attempt [1-5] \[(standard renderer|upscaled renderer without MSAA|D3D9On12 upscaled renderer without MSAA|last-resort native-scale renderer|D3D9On12 last-resort native-scale renderer)( \(override unavailable\)| \(override failed\))?\] exited with code -?\d{1,11} after \d{1,8}\.\d seconds; video dialog=(True|False); early abnormal exit=(True|False); classified video failure=(True|False)\.)$");
        if (match.Success) return line;
        if (Regex.IsMatch(line, @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} Restored DDrawCompat process configuration left by an interrupted launch\.$")) return line;
        return null;
    }
}
