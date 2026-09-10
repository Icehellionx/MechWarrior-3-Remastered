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

[assembly: AssemblyTitle("MechWarrior 3 Remastered Launcher")]
[assembly: AssemblyDescription("Launches MechWarrior 3 and Pirate's Moon and opens their original manuals.")]
[assembly: AssemblyCompany("MechWarrior 3 Remastered contributors")]
[assembly: AssemblyProduct("MechWarrior 3 Remastered")]
[assembly: AssemblyCopyright("Copyright © 2026 MechWarrior 3 Remastered contributors")]
[assembly: AssemblyVersion("1.2.2.0")]
[assembly: AssemblyFileVersion("1.2.2.0")]

internal sealed class GameRequest
{
    public bool PiratesMoon;
    public bool UsesRip;
    public string GameRoot;
    public string Media;
}

internal sealed class LauncherForm : Form
{
    private static readonly Color Background = Color.FromArgb(10, 10, 12);
    private static readonly Color Panel = Color.FromArgb(24, 24, 27);
    private static readonly Color Accent = Color.FromArgb(190, 18, 24);
    private static readonly Color Steel = Color.FromArgb(196, 202, 207);
    private readonly string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    private readonly Label status = new Label();
    private readonly List<Button> actions = new List<Button>();

    public LauncherForm()
    {
        Text = "MechWarrior 3 Remastered";
        ClientSize = new Size(744, 456);
        BackColor = Background;
        ForeColor = Steel;
        Font = new Font("Segoe UI", 10F);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        Controls.Add(new Label { BackColor = Accent, Location = new Point(0, 0), Size = new Size(744, 5) });
        PictureBox mark = new PictureBox { Location = new Point(24, 18), Size = new Size(54, 54), SizeMode = PictureBoxSizeMode.Zoom };
        try { mark.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch { }
        Controls.Add(mark);
        Controls.Add(new Label { Text = "MECHWARRIOR 3", Font = new Font("Arial", 22F, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(91, 16) });
        Controls.Add(new Label { Text = "R E M A S T E R E D", Font = new Font("Arial", 10F, FontStyle.Bold), ForeColor = Accent, AutoSize = true, Location = new Point(94, 52) });
        Controls.Add(new Label { Text = "SELECT OPERATION", Font = new Font("Consolas", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(125, 129, 133), AutoSize = true, Location = new Point(586, 49) });

        Button mw3 = MakeTile("MECHWARRIOR 3", "Launch base campaign", new Point(24, 92), GameImage(false));
        Button pm = MakeTile("PIRATE'S MOON", Directory.Exists(Path.Combine(root, "Pirates Moon")) ? "Launch expansion" : "Expansion not installed", new Point(380, 92), GameImage(true));
        Button mw3Manual = MakeTile("MW3 MANUAL", "Open original PDF manual", new Point(24, 230), ResourceImage("MW3.ManualCover.png", null) ?? BookImage("3"));
        Button pmManual = MakeTile("PIRATE'S MOON MANUAL", "Open original PDF manual", new Point(380, 230), ResourceImage("PiratesMoon.ManualCover.png", null) ?? BookImage("PM"));
        mw3.Click += async delegate { await LaunchAsync(false); };
        pm.Click += async delegate { await LaunchAsync(true); };
        mw3Manual.Click += delegate { OpenManual("MechWarrior 3 Manual.pdf"); };
        pmManual.Click += delegate { OpenManual("MechWarrior 3 Pirate's Moon Manual.pdf"); };

        status.Text = "SYSTEM READY";
        status.Font = new Font("Consolas", 9F, FontStyle.Bold);
        status.ForeColor = Color.FromArgb(145, 150, 154);
        status.Location = new Point(25, 407);
        status.Size = new Size(694, 23);
        status.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(new Label { BackColor = Color.FromArgb(66, 67, 70), Location = new Point(24, 389), Size = new Size(696, 1) });
        Controls.Add(status);
    }

    private Button MakeTile(string title, string subtitle, Point location, Image image)
    {
        Button button = new Button();
        button.Text = title + Environment.NewLine + subtitle;
        button.Location = location;
        button.Size = new Size(340, 120);
        button.BackColor = Panel;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(91, 25, 29);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 19, 22);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 18, 23);
        button.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        button.Image = image;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.Padding = new Padding(18, 0, 10, 0);
        actions.Add(button);
        Controls.Add(button);
        return button;
    }

    private Image GameImage(bool pm)
    {
        Image bundled = ResourceImage(pm ? "PiratesMoon.Game.png" : "MW3.Game.png", null);
        if (bundled != null) return bundled;
        string exe = Path.Combine(root, pm ? "Pirates Moon" : "MechWarrior 3", "Mech3fixup.exe");
        try
        {
            using (Icon icon = Icon.ExtractAssociatedIcon(exe)) return new Bitmap(icon.ToBitmap(), new Size(58, 58));
        }
        catch { return new Bitmap(SystemIcons.Application.ToBitmap(), new Size(58, 58)); }
    }

    private static Image ResourceImage(string name, Image fallback)
    {
        try
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (Image source = Image.FromStream(stream)) return new Bitmap(source);
        }
        catch { return fallback; }
    }

    private static Image BookImage(string label)
    {
        Bitmap image = new Bitmap(58, 58);
        using (Graphics graphics = Graphics.FromImage(image))
        using (Pen red = new Pen(Accent, 3F))
        using (Brush paper = new SolidBrush(Color.FromArgb(205, 211, 216)))
        using (Font font = new Font("Arial", label.Length > 1 ? 9F : 14F, FontStyle.Bold))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.FillRectangle(paper, 11, 7, 36, 44);
            graphics.DrawRectangle(red, 11, 7, 36, 44);
            graphics.DrawLine(red, 19, 8, 19, 50);
            graphics.DrawString(label, font, Brushes.Black, label.Length > 1 ? 23 : 28, label.Length > 1 ? 22 : 18);
        }
        return image;
    }

    private async Task LaunchAsync(bool pm)
    {
        GameRequest request;
        try { request = LauncherRuntime.Prepare(pm); }
        catch (Exception ex) { ShowError(ex.Message); return; }
        if (request == null) return;
        SetBusy(true);
        try
        {
            await Task.Run(delegate { LauncherRuntime.Run(request, UpdateStatus); });
            UpdateStatus("SYSTEM READY");
        }
        catch (Exception ex) { ShowError(ex.Message); UpdateStatus("LAUNCH FAILED"); }
        finally { SetBusy(false); }
    }

    private void OpenManual(string name)
    {
        try
        {
            string path = Path.Combine(root, "Manuals", name);
            if (!File.Exists(path)) throw new FileNotFoundException("The installed manual is missing.", path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            UpdateStatus("OPENED " + name.ToUpperInvariant());
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void SetBusy(bool busy)
    {
        foreach (Button button in actions) button.Enabled = !busy;
    }

    private void UpdateStatus(string text)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke((Action<string>)UpdateStatus, text);
        else status.Text = text.ToUpperInvariant();
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

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
        bool rip = pm && cfg.TryGetValue("PiratesMoonMediaType", out mediaType) && mediaType.Equals("Rip", StringComparison.OrdinalIgnoreCase);
        string media = null;
        string key = pm ? "PiratesMoonIso" : "Mw3Iso";
        if (!rip && (!cfg.TryGetValue(key, out media) || !File.Exists(media)))
        {
            media = SelectMedia(pm);
            if (media == null) return null;
        }
        return new GameRequest { PiratesMoon = pm, UsesRip = rip, GameRoot = gameRoot, Media = media };
    }

    public static void Run(GameRequest request, Action<string> report)
    {
        if (Process.GetProcessesByName("Mech3fixup").Length != 0)
            throw new InvalidOperationException("Close the running MechWarrior game before starting another title.");

        report("Preparing " + (request.PiratesMoon ? "Pirate's Moon" : "MechWarrior 3") + "...");
        Log("Launch requested for " + (request.PiratesMoon ? "Pirate's Moon" : "MechWarrior 3") +
            "; OS=" + Environment.OSVersion.VersionString + "; 64-bit OS=" + Environment.Is64BitOperatingSystem + ".");
        KillAudioPlayer();
        Thread.Sleep(2000);
        if (!request.UsesRip) MountIso(request.Media);
        SetRegistry(request.GameRoot, request.PiratesMoon);

        string exe = Path.Combine(request.GameRoot, "Mech3fixup.exe");
        if (!File.Exists(exe)) throw new FileNotFoundException("The installed game executable is missing.", exe);
        string ddraw = Path.Combine(request.GameRoot, "ddraw.dll");
        if (!File.Exists(ddraw)) throw new FileNotFoundException("The DDrawCompat graphics wrapper is missing. Reinstall MechWarrior 3 Remastered.", ddraw);
        string audioPlayer = Path.Combine(request.GameRoot, "mcicda", "cdaudioplr.exe");
        Log("Compatibility files: ddraw=" + FileVersion(ddraw) + "; CD audio helper=" +
            (File.Exists(audioPlayer) ? FileVersion(audioPlayer) : "missing or quarantined") + ".");
        string outputPath = Path.Combine(request.GameRoot, "mech3.out");
        const int maxAttempts = 4;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try { if (File.Exists(outputPath)) File.Delete(outputPath); } catch { }
            report(attempt == 1 ? "Launching game..." : "Video initialization failed; retrying (" + attempt + "/" + maxAttempts + ")...");
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
            bool videoFailure = blockedVideoError || (runtime.TotalSeconds < 20 && HasVideoInitializationFailure(outputPath, started));
            KillAudioPlayer();
            Log("Attempt " + attempt + " exited with code " + exitCode + " after " +
                runtime.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                " seconds; video dialog=" + blockedVideoError + "; classified video failure=" + videoFailure + ".");
            if (!videoFailure)
            {
                if (!File.Exists(audioPlayer)) Log("Warning: CD audio helper is unavailable; gameplay can continue without music.");
                return;
            }
            if (attempt == maxAttempts)
                throw new InvalidOperationException("MechWarrior 3 could not initialize video after four recovery attempts. " +
                    "The launcher restored the tested renderer settings between attempts. Please attach the newest mech3.out and " +
                    "DDrawCompat-Mech3fixup.log files plus " + DiagnosticLog + " to a bug report.");

            // A failed first-second D3DIM startup can leave the game's selected
            // adapter/mode values changed. Re-applying the tested renderer state
            // makes the next attempt a real recovery instead of an identical retry
            // with settings poisoned by the previous failure.
            ResetVideoSettings(request.PiratesMoon);
            Thread.Sleep(4000);
        }
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

    private static void KillAudioPlayer()
    {
        foreach (Process stale in Process.GetProcessesByName("cdaudioplr"))
            try { stale.Kill(); stale.WaitForExit(3000); } catch { }
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
        if (pm)
            MessageBox.Show("This Pirate's Moon installation was made from an ISO, which is required when launching. RIP ZIP/folder installations do not require their source after setup.", "Locate Pirate's Moon ISO", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static void MountIso(string iso)
    {
        string escaped = iso.Replace("'", "''");
        string command = "$ErrorActionPreference='Stop'; $p='" + escaped + "'; $i=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue; if(-not $i -or -not $i.Attached){Mount-DiskImage -ImagePath $p | Out-Null}";
        ProcessStartInfo info = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"");
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        using (Process process = Process.Start(info))
        {
            process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Windows could not mount the selected ISO. Right-click the ISO, choose Mount, then try again.");
        }
    }

    private static void SetRegistry(string gameRoot, bool pm)
    {
        string slashRoot = gameRoot.TrimEnd('\\') + "\\";
        string product = pm ? "MechWarrior 3 EP1" : "MechWarrior 3";
        using (RegistryKey hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default))
        using (RegistryKey install = hkcu.CreateSubKey("Software\\Classes\\VirtualStore\\MACHINE\\SOFTWARE\\WOW6432Node\\MicroProse\\" + product + "\\1.0"))
        {
            install.SetValue("InstallPath", slashRoot, RegistryValueKind.String);
            if (pm) install.SetValue("Program", slashRoot, RegistryValueKind.String);
            install.SetValue("Version", pm ? "1.0" : "1.2", RegistryValueKind.String);
            install.SetValue("InstallOptions", 0x00050707, RegistryValueKind.DWord);
        }
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

internal static class Launcher
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 0 && (args[0].Equals("mw3", StringComparison.OrdinalIgnoreCase) || args[0].Equals("pm", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                GameRequest request = LauncherRuntime.Prepare(args[0].Equals("pm", StringComparison.OrdinalIgnoreCase));
                if (request != null) LauncherRuntime.Run(request, delegate { });
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "MechWarrior 3 Remastered", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            return;
        }
        Application.Run(new LauncherForm());
    }
}
