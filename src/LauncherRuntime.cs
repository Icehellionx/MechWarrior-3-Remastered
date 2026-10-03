using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class LauncherRuntime
{
    private const uint WmClose = 0x0010;
    private static readonly string DiagnosticDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MechWarrior 3 Remastered");
    private static readonly string DiagnosticLog = Path.Combine(DiagnosticDirectory, "launcher.log");
    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximum);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public static GameRequest Prepare(bool pm)
    {
        string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        Dictionary<string, string> cfg = ReadConfig(Path.Combine(root, "install.cfg"));
        string gameRoot = Path.Combine(root, pm ? "Pirates Moon" : "MechWarrior 3");
        if (!Directory.Exists(gameRoot)) throw new DirectoryNotFoundException((pm ? "Pirate's Moon" : "MechWarrior 3") + " is not installed.");
        string mediaType;
        bool noDisc = pm && cfg.TryGetValue("PiratesMoonMediaType", out mediaType) &&
            (mediaType.Equals("Rip", StringComparison.OrdinalIgnoreCase) || mediaType.Equals("NoDisc", StringComparison.OrdinalIgnoreCase));
        string media = null;
        string key = pm ? "PiratesMoonIso" : "Mw3Iso";
        if (!noDisc && (!cfg.TryGetValue(key, out media) || !DiscMediaSession.IsAvailable(media)))
        {
            media = SelectMedia(pm);
            if (media == null) return null;
        }
        return new GameRequest { PiratesMoon = pm, RequiresDisc = !noDisc, GameRoot = gameRoot, Media = media };
    }

    public static void Run(GameRequest request, Action<string> report)
    {
        using (LaunchLease lease = LaunchLease.Acquire(DiagnosticDirectory))
        {
            RunCore(request, report);
        }
    }

    private static void RunCore(GameRequest request, Action<string> report)
    {
        if (InstalledProcessScope.HasRunningGame(request.GameRoot))
            throw new InvalidOperationException("Close the running MechWarrior game before starting another title.");

        DiscMediaSession mediaMount = null;
        try
        {
        report("Preparing " + (request.PiratesMoon ? "Pirate's Moon" : "MechWarrior 3") + "...");
        Log("Launch requested for " + (request.PiratesMoon ? "Pirate's Moon" : "MechWarrior 3") +
            "; launcher=" + Assembly.GetExecutingAssembly().GetName().Version +
            "; OS=" + Environment.OSVersion.VersionString + "; 64-bit OS=" + Environment.Is64BitOperatingSystem + ".");
        InstalledProcessScope.StopAudioPlayers(request.GameRoot, Log);
        Thread.Sleep(2000);
        if (request.RequiresDisc)
        {
            report("Checking selected disc media...");
            mediaMount = DiscMediaSession.Open(request.Media, Log);
        }
        try
        {
            GameControlStorage.EnsureWritable(request.GameRoot);
            Log("Control-profile storage is writable.");
        }
        catch (InvalidOperationException ex)
        {
            // Preserve game launch for older Program Files installations. New
            // setup runs create this storage under the writable default path.
            Log("Warning: " + ex.Message);
        }
        SetRegistry(request.GameRoot, request.PiratesMoon);

        string exe = Path.Combine(request.GameRoot, "Mech3fixup.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("The installed game executable is missing.", exe);
        string ddraw = Path.Combine(request.GameRoot, "ddraw.dll");
        if (!File.Exists(ddraw)) throw new FileNotFoundException("The DDrawCompat graphics wrapper is missing. Reinstall MechWarrior 3 Remastered.", ddraw);
        string audioPlayer = Path.Combine(request.GameRoot, "mcicda", "cdaudioplr.exe");
        Log("Compatibility files: ddraw=" + FileVersion(ddraw) + "; CD audio helper=" +
            (File.Exists(audioPlayer) ? FileVersion(audioPlayer) : "missing or quarantined") + ".");
        string outputPath = Path.Combine(request.GameRoot, "mech3.out");
        string ddrawLogPath = Path.Combine(request.GameRoot, "DDrawCompat-Mech3fixup.log");
        string processConfigPath = Path.Combine(request.GameRoot, "DDrawCompat-Mech3fixup.ini");
        string launchId = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture);
        const int maxAttempts = 5;
        using (VideoRecoveryConfig recoveryConfig = new VideoRecoveryConfig(processConfigPath, Log))
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                string recoveryProfile = recoveryConfig.Apply(attempt);
                try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
                report(attempt == 1 ? "Launching game..." :
                    "Video initialization failed; trying " + recoveryProfile + " (" + attempt + "/" + maxAttempts + ")...");
                DateTime started = DateTime.UtcNow;
                ProcessStartInfo start = new ProcessStartInfo(exe) { WorkingDirectory = request.GameRoot, UseShellExecute = false };
                bool blockedVideoError;
                int exitCode = -1;
                using (Process game = Process.Start(start))
                {
                    blockedVideoError = WaitForExitAndDismissVideoError(game, started);
                    try { exitCode = game.ExitCode; } catch { }
                }
                TimeSpan runtime = DateTime.UtcNow - started;
                bool abnormalEarlyExit = LaunchResultClassifier.IsEarlyAbnormalExit(exitCode, runtime);
                bool videoFailure = blockedVideoError || abnormalEarlyExit ||
                    (runtime.TotalSeconds < 20 && HasVideoInitializationFailure(outputPath, started));
                InstalledProcessScope.StopAudioPlayers(request.GameRoot, Log);
                ArchiveAttemptLogs(request, launchId, attempt, outputPath, ddrawLogPath, started);
                Log("Attempt " + attempt + " [" + recoveryProfile + "] exited with code " + exitCode + " after " +
                    runtime.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                    " seconds; video dialog=" + blockedVideoError + "; early abnormal exit=" + abnormalEarlyExit +
                    "; classified video failure=" + videoFailure + ".");
                if (LaunchResultClassifier.IsLoaderFailure(exitCode))
                    throw new InvalidOperationException("The game could not load a required runtime library (exit code 0x" +
                        unchecked((uint)exitCode).ToString("X8") + "). Verify the game installation and required runtimes. " +
                        "Graphics recovery cannot repair this failure. Diagnostics: " + DiagnosticLog);
                if (!videoFailure)
                {
                    if (attempt > 1) Log("Video initialization recovered with profile: " + recoveryProfile + ".");
                    if (!File.Exists(audioPlayer)) Log("Warning: CD audio helper is unavailable; gameplay can continue without music.");
                    return;
                }
                if (attempt == maxAttempts)
                    throw new InvalidOperationException("MechWarrior 3 could not initialize video after five recovery profiles. " +
                        "Per-attempt diagnostics were saved under " + Path.Combine(DiagnosticDirectory, "attempts", launchId) +
                        ". Please attach that folder plus " + DiagnosticLog + " to a bug report.");

                // A failed first-second D3DIM startup can leave the game's selected
                // adapter/mode values changed. Re-applying the tested renderer state
                // makes the next attempt a real recovery instead of using settings
                // poisoned by the previous failure.
                ResetVideoSettings(request.PiratesMoon);
                Thread.Sleep(4000);
            }
        }
        }
        finally
        {
            InstalledProcessScope.StopAudioPlayers(request.GameRoot, Log);
            if (mediaMount != null)
            {
                if (mediaMount.OwnsMount) report("Ejecting disc image...");
                mediaMount.Dispose();
            }
        }
    }

    private static void ArchiveAttemptLogs(GameRequest request, string launchId, int attempt,
        string outputPath, string ddrawLogPath, DateTime started)
    {
        try
        {
            string archive = Path.Combine(DiagnosticDirectory, "attempts", launchId);
            Directory.CreateDirectory(archive);
            string prefix = request.PiratesMoon ? "pm" : "mw3";
            ArchiveFreshFile(outputPath, Path.Combine(archive, prefix + "-attempt-" + attempt + ".out"), started);
            ArchiveFreshFile(ddrawLogPath, Path.Combine(archive, "DDrawCompat-" + prefix + "-attempt-" + attempt + ".log"), started);
            Log("Attempt " + attempt + " diagnostics archived under " + archive + ".");
        }
        catch (Exception ex)
        {
            Log("Warning: could not archive attempt " + attempt + " diagnostics: " + ex.Message);
        }
    }

    private static void ArchiveFreshFile(string source, string destination, DateTime started)
    {
        if (File.Exists(source) && File.GetLastWriteTimeUtc(source) >= started.AddSeconds(-1))
            File.Copy(source, destination, true);
    }

    private static bool WaitForExitAndDismissVideoError(Process game, DateTime started)
    {
        bool found = false;
        while (!game.WaitForExit(200))
        {
            if (!found && (DateTime.UtcNow - started).TotalSeconds <= 30)
                found = CloseVideoErrorDialog(game.Id);
            if (found && !game.WaitForExit(5000))
            {
                try { game.Kill(); } catch { }
                game.WaitForExit();
            }
            if ((DateTime.UtcNow - started).TotalSeconds > 30 && !found)
            {
                game.WaitForExit();
                break;
            }
        }
        return found;
    }

    private static bool CloseVideoErrorDialog(int processId)
    {
        bool found = false;
        EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner != (uint)processId) return true;
            StringBuilder title = new StringBuilder(128);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString().Equals("Video Error", StringComparison.OrdinalIgnoreCase))
            {
                PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
                found = true;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static bool HasVideoInitializationFailure(string outputPath, DateTime started)
    {
        try
        {
            return File.Exists(outputPath) && File.GetLastWriteTimeUtc(outputPath) >= started.AddSeconds(-1) &&
                File.ReadAllText(outputPath).IndexOf("Error opening video... ABORTING RUN", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }

    private static string FileVersion(string path)
    {
        try
        {
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
            return String.IsNullOrEmpty(version.FileVersion) ? "unversioned" : version.FileVersion;
        }
        catch { return "unreadable"; }
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(DiagnosticDirectory);
            File.AppendAllText(DiagnosticLog, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    private static string SelectMedia(bool pm)
    {
        DialogResult choice = MessageBox.Show(
            "Choose Yes to locate the original ISO, or No to select its already mounted CD drive/folder. Keep a mapped CD drive available while playing under Wine.",
            "Locate " + (pm ? "Pirate's Moon" : "MechWarrior 3") + " disc",
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        if (choice == DialogResult.Cancel) return null;
        if (choice == DialogResult.No)
        {
            using (FolderBrowserDialog picker = new FolderBrowserDialog())
            {
                picker.Description = "Select the readable root of the mounted game disc";
                return picker.ShowDialog() == DialogResult.OK ? picker.SelectedPath : null;
            }
        }
        using (OpenFileDialog picker = new OpenFileDialog())
        {
            picker.Title = "Locate your " + (pm ? "Pirate's Moon" : "MechWarrior 3") + " ISO";
            picker.Filter = "Disc images (*.iso)|*.iso|All files (*.*)|*.*";
            return picker.ShowDialog() == DialogResult.OK ? picker.FileName : null;
        }
    }

    private static Dictionary<string, string> ReadConfig(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("The launcher configuration is missing. Reinstall MechWarrior 3 Remastered.", path);
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadAllLines(path))
        {
            int split = line.IndexOf('=');
            if (split > 0) values[line.Substring(0, split)] = line.Substring(split + 1);
        }
        return values;
    }

    private static void SetRegistry(string gameRoot, bool pm)
    {
        GameInstallRegistry.WriteVirtualStoreRegistration(gameRoot, pm);
        string product = pm ? "MechWarrior 3 EP1" : "MechWarrior 3";
        using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
        using (RegistryKey settings = hkcu.CreateSubKey("Software\\MicroProse\\" + product + "\\1.0"))
        {
            // These three values identify the tested HAL, primary adapter and
            // 1024x768 mode. They are boot prerequisites, not player preferences.
            settings.SetValue("HWCardFlag", 1, RegistryValueKind.DWord);
            settings.SetValue("HWCardDev", 0, RegistryValueKind.DWord);
            settings.SetValue("InGameVMode", 5, RegistryValueKind.DWord);
            SetIfMissing(settings, "SoundVolume", BitConverter.GetBytes(1.0f), RegistryValueKind.Binary);
            SetIfMissing(settings, "TextureMemory_HW", 3, RegistryValueKind.DWord);
            SetIfMissing(settings, "GfxFlags_HW", 0x1f, RegistryValueKind.DWord);
            SetIfMissing(settings, "Shadow", 1, RegistryValueKind.DWord);
            SetIfMissing(settings, "ShadowParts_HW", 1, RegistryValueKind.DWord);
            SetIfMissing(settings, "EffectsLevel_HW", 0, RegistryValueKind.DWord);
            SetIfMissing(settings, "ObjectLOD_HW", 0, RegistryValueKind.DWord);
            if (settings.GetValue("RemasterDefaultsVersion") == null)
            {
                settings.SetValue("CDVolume", BitConverter.GetBytes(0.60f), RegistryValueKind.Binary);
                settings.SetValue("RemasterDefaultsVersion", 1, RegistryValueKind.DWord);
            }
        }
    }

    private static void ResetVideoSettings(bool pm)
    {
        string product = pm ? "MechWarrior 3 EP1" : "MechWarrior 3";
        using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
        using (RegistryKey settings = hkcu.CreateSubKey("Software\\MicroProse\\" + product + "\\1.0"))
        {
            settings.SetValue("HWCardFlag", 1, RegistryValueKind.DWord);
            settings.SetValue("HWCardDev", 0, RegistryValueKind.DWord);
            settings.SetValue("InGameVMode", 5, RegistryValueKind.DWord);
        }
        Log("Restored tested Direct3D HAL, primary-adapter and 1024x768 startup values before retry.");
    }

    private static void SetIfMissing(RegistryKey key, string name, object value, RegistryValueKind kind)
    {
        if (key.GetValue(name) == null) key.SetValue(name, value, kind);
    }
}

