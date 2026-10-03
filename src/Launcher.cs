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
[assembly: AssemblyVersion("1.2.8.0")]
[assembly: AssemblyFileVersion("1.2.8.0")]

internal sealed class GameRequest
{
    public bool PiratesMoon;
    public bool RequiresDisc;
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
        ClientSize = new Size(744, 488);
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
        Button pm = MakeTile("PIRATE'S MOON", File.Exists(Path.Combine(root, "Pirates Moon", "Mech3fixup.exe")) ? "Launch expansion" : "Expansion not installed", new Point(380, 92), GameImage(true));
        Button mw3Manual = MakeTile("MW3 MANUAL", "Open original PDF manual", new Point(24, 230), ResourceImage("MW3.ManualCover.png", null) ?? BookImage("3"));
        Button pmManual = MakeTile("PIRATE'S MOON MANUAL", "Open original PDF manual", new Point(380, 230), ResourceImage("PiratesMoon.ManualCover.png", null) ?? BookImage("PM"));
        mw3.Click += async delegate { await LaunchAsync(false); };
        pm.Click += async delegate { await LaunchAsync(true); };
        mw3Manual.Click += delegate { OpenManual("MechWarrior 3 Manual.pdf"); };
        pmManual.Click += delegate { OpenManual("MechWarrior 3 Pirate's Moon Manual.pdf"); };

        AddFooterAction("HELP", 24, delegate { using (LauncherHelpForm help = new LauncherHelpForm()) help.ShowDialog(this); });
        AddFooterAction("CREDITS", 143, delegate { OpenDocument("THIRD_PARTY_NOTICES.md"); });
        AddFooterAction("DIAGNOSTICS", 262, async delegate { await OpenDiagnosticsAsync(); });
        AddFooterAction("SETTINGS", 381, delegate { OpenSettings(); });
        AddFooterAction("UNINSTALL", 605, delegate { StartUninstall(); });

        status.Text = "SYSTEM READY";
        status.Font = new Font("Consolas", 9F, FontStyle.Bold);
        status.ForeColor = Color.FromArgb(145, 150, 154);
        status.Location = new Point(25, 451);
        status.Size = new Size(695, 23);
        status.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(new Label { BackColor = Color.FromArgb(66, 67, 70), Location = new Point(24, 389), Size = new Size(696, 1) });
        Controls.Add(status);
    }

    private GraphicsSettingsForm settingsDialog;
    private DiagnosticsForm diagnosticsDialog;

    private void AddFooterAction(string text, int left, EventHandler click)
    {
        Button button = new Button { Text = text, Location = new Point(left, 402), Size = new Size(115, 30),
            BackColor = Panel, ForeColor = Steel, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold) };
        button.FlatAppearance.BorderColor = Color.FromArgb(91, 25, 29);
        button.Click += click;
        actions.Add(button);
        Controls.Add(button);
    }

    private void OpenDocument(string relative)
    {
        try { InstalledDocument.Open(root, relative); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void OpenSettings()
    {
        if (settingsDialog != null && !settingsDialog.IsDisposed) { settingsDialog.Activate(); return; }
        settingsDialog = new GraphicsSettingsForm(root);
        settingsDialog.Show(this);
    }

    private async Task OpenDiagnosticsAsync()
    {
        if (diagnosticsDialog != null && !diagnosticsDialog.IsDisposed) { diagnosticsDialog.Activate(); return; }
        DiagnosticsForm dialog = new DiagnosticsForm();
        diagnosticsDialog = dialog;
        dialog.Show(this);
        try
        {
            string report = await Task.Run(delegate { return InstallationDiagnostics.Build(root); });
            if (!dialog.IsDisposed) dialog.SetReport(report);
        }
        catch { if (!dialog.IsDisposed) dialog.SetReport("Diagnostics unavailable. Game launch is independent of this report."); }
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
            InstalledDocument.Open(root, Path.Combine("Manuals", name));
            UpdateStatus("OPENED " + name.ToUpperInvariant());
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void StartUninstall()
    {
        if (MessageBox.Show(this,
            "Remove MechWarrior 3 Remastered? Saved pilots and campaign progress will be kept for a future reinstall. All other game files, settings, diagnostics, and shortcuts will be removed.",
            "Uninstall MechWarrior 3 Remastered", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            string uninstaller = Path.Combine(root, "Uninstall.exe");
            if (!File.Exists(uninstaller)) throw new FileNotFoundException("The installed uninstaller is missing.", uninstaller);
            Process.Start(new ProcessStartInfo(uninstaller, "--confirmed") { UseShellExecute = true, Verb = "runas" });
            Close();
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
