using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

internal static class LauncherParitySmoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        string repo = Path.GetFullPath(args[0]);
        string fixture = Path.Combine(repo, ".local", "parity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            LaunchFailureLogsRejectStaleAndUnavailableEvidence(fixture);
            using (LauncherForm textLayout = new LauncherForm())
            {
                foreach (Button button in textLayout.Controls.OfType<Button>().Where(delegate(Button b) { return b.Top == 402; }))
                    Assert(TextRenderer.MeasureText(button.Text, button.Font).Width <= button.Width - 8,
                        "Footer action text is clipped: " + button.Text);
            }
            string game = Path.Combine(fixture, "MechWarrior 3"); Directory.CreateDirectory(game);
            string pm = Path.Combine(fixture, "Pirates Moon"); Directory.CreateDirectory(pm);
            string wrapper = Path.Combine(repo, "..", "tools", "releases", "DDrawCompat-MW3-field-baseline-v0.7.1-r18-primary-surface-retry", "ddraw.dll");
            foreach (string path in new string[] { game, pm }) { File.Copy(wrapper, Path.Combine(path, "ddraw.dll")); File.WriteAllText(Path.Combine(path, "Mech3fixup.exe"), "fixture"); }
            File.Copy(Path.Combine(repo, "config", "DDrawCompat.ini"), Path.Combine(game, "DDrawCompat.ini"));
            File.Copy(Path.Combine(repo, "config", "DDrawCompat-PiratesMoon.ini"), Path.Combine(pm, "DDrawCompat.ini"));
            string state = Path.Combine(fixture, "state");
            GraphicsProfileService service = new GraphicsProfileService(fixture, state, delegate { return false; });
            GraphicsProfile baseProfile = service.Load(false);
            byte[] pmBefore = File.ReadAllBytes(Path.Combine(pm, "DDrawCompat.ini"));
            service.Apply(false, baseProfile, "off");
            Assert(service.Load(false).Values["Antialiasing"] == "off", "MSAA-off choice did not persist.");
            Assert(GraphicsProfileService.Equal(pmBefore, File.ReadAllBytes(Path.Combine(pm, "DDrawCompat.ini"))), "Base settings changed PM.");
            string expected = new System.Text.UTF8Encoding(false, true).GetString(baseProfile.Original).Replace("Antialiasing = msaa4x(0)", "Antialiasing = off");
            Assert(File.ReadAllText(baseProfile.Path) == expected, "Settings altered adjacent policy or comments.");
            Refuses(delegate { service.Apply(false, baseProfile, "msaa4x(0)"); }, "Stale profile accepted.");
            Refuses(delegate { service.Apply(false, service.Load(false), "msaa8x"); }, "Unqualified AA accepted.");
            GraphicsProfileService busy = new GraphicsProfileService(fixture, state, delegate { return true; });
            Refuses(delegate { busy.Load(false); }, "Running-game refusal absent.");
            foreach (string name in new string[] { "DDrawCompat-Mech3fixup.ini", "DDrawCompat-Mech3fixup.ini.launcher-state", "DDrawCompat-Mech3fixup.ini.launcher-backup", "DDrawCompat-Mech3fixup.ini.launcher-backup.tmp", "DDrawCompatOverlay-Mech3fixup.ini" })
            {
                string path = Path.Combine(game, name); File.WriteAllText(path, "private fixture");
                Refuses(delegate { service.Load(false); }, "Override/recovery state accepted: " + name); File.Delete(path);
            }
            using (LaunchLease lease = LaunchLease.Acquire(state))
                Refuses(delegate { service.Apply(false, service.Load(false), "msaa4x(0)"); }, "Concurrent launch lease ignored.");
            GraphicsProfile current = service.Load(false);
            File.AppendAllText(current.Path, "\r\nAntialiasing = off\r\n");
            Refuses(delegate { service.Load(false); }, "Duplicate settings accepted.");
            File.WriteAllBytes(current.Path, current.Original);
            File.AppendAllText(current.Path, "\r\nCustomPolicy = on\r\n");
            Refuses(delegate { service.Load(false); }, "Custom policy accepted.");
            File.WriteAllBytes(current.Path, current.Original);
            // Actual write failure: atomic replacement must leave exact old bytes and no temporary file.
            using (FileStream locked = new FileStream(current.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Refuses(delegate { service.Apply(false, current, "msaa4x(0)"); }, "Locked profile write unexpectedly succeeded.");
            Assert(GraphicsProfileService.Equal(File.ReadAllBytes(current.Path), current.Original), "Failed Apply changed profile bytes.");
            Assert(Directory.GetFiles(game, "*.tmp").Length == 0, "Apply leaked a temporary file.");
            service.Apply(false, service.Load(false), "msaa4x(0)");
            Assert(GraphicsProfileService.Equal(File.ReadAllBytes(current.Path), baseProfile.Original), "Default restoration was not byte-identical.");
            service.Apply(true, service.Load(true), "off");
            Assert(service.Load(true).Values["PresentDelay"] == "on(50)" && service.Load(true).Values["VSync"] == "on", "PM timing was lost.");
            Assert(service.Load(true).Values["PresentationEdgeRepair"] == "4", "PM edge repair was lost during AA Apply.");
            service.Apply(true, service.Load(true), "msaa4x(0)");
            Assert(GraphicsProfileService.Equal(pmBefore, File.ReadAllBytes(Path.Combine(pm, "DDrawCompat.ini"))), "PM restoration was not byte-identical.");
            string pmPath = Path.Combine(pm, "DDrawCompat.ini");
            string pmText = File.ReadAllText(pmPath);
            File.WriteAllText(pmPath, pmText.Replace("PresentationEdgeRepair = 4", "PresentationEdgeRepair = 0"));
            Refuses(delegate { service.Load(true); }, "Disabled PM edge repair accepted.");
            File.WriteAllText(pmPath, pmText.Replace("PresentationEdgeRepair = 4", ""));
            Refuses(delegate { service.Load(true); }, "Missing PM edge repair accepted.");
            File.WriteAllBytes(pmPath, pmBefore);
            File.WriteAllText(Path.Combine(game, "ddraw.dll"), "wrong");
            Refuses(delegate { service.Load(false); }, "Unknown wrapper accepted.");
            File.Copy(wrapper, Path.Combine(game, "ddraw.dll"), true);

            Directory.CreateDirectory(state);
            string secret = "C:\\Users\\PRIVATE_PERSON\\private.iso";
            File.WriteAllText(Path.Combine(state, "launcher.log"),
                "2026-10-02 12:00:00.000 Attempt 2 [upscaled renderer without MSAA] exited with code 0 after 60.0 seconds; video dialog=False; early abnormal exit=False; classified video failure=False.\r\n" +
                "2026-10-02 12:00:00.000 Warning: " + secret + "\r\n" +
                "2026-10-02 12:00:00.000 Attempt 1 [standard renderer] exited with code 0 after 60.0 seconds; video dialog=False; early abnormal exit=False; classified video failure=False. " + secret);
            File.WriteAllText(Path.Combine(game, "DDrawCompatOverlay-Mech3fixup.ini"), "Antialiasing = " + secret);
            string report = InstallationDiagnostics.Build(fixture, state);
            Assert(!report.Contains("PRIVATE_PERSON") && !report.Contains(fixture) && !report.Contains("private.iso"), "Report leaked private text.");
            Assert(report.Contains("Attempt 2") && report.Contains("value omitted"), "Recognized recovery/override information absent.");
            File.WriteAllText(Path.Combine(game, "DDrawCompatOverlay-Mech3fixup.ini"), new string('x', 65537));
            Assert(InstallationDiagnostics.Build(fixture, state).Contains("configuration unavailable"), "Oversized profile was not bounded.");
            File.Delete(Path.Combine(game, "DDrawCompatOverlay-Mech3fixup.ini"));
            Assert(InstallationDiagnostics.Build(Path.Combine(fixture, "absent"), state).Contains("not installed"), "Missing install not reported.");
            Refuses(delegate { InstalledDocument.Require(fixture, "missing.md"); }, "Missing notices did not produce an error.");
            File.WriteAllText(Path.Combine(fixture, "THIRD_PARTY_NOTICES.md"), "fixture");
            Assert(File.Exists(InstalledDocument.Require(fixture, "THIRD_PARTY_NOTICES.md")), "Installed notices not found.");
            Assert(GraphicsSettingsForm.FitPicture(new Size(1920, 1080)) == new Size(1440, 1080), "16:9 preview incorrect.");
            Assert(GraphicsSettingsForm.FitPicture(new Size(3440, 1440)) == new Size(1920, 1440), "Ultrawide preview incorrect.");
            Assert(GraphicsSettingsForm.FitPicture(new Size(1080, 1920)) == new Size(1080, 810), "Portrait preview incorrect.");
            using (GraphicsSettingsForm settings = new GraphicsSettingsForm(fixture))
            {
                Button applyButton = settings.Controls.OfType<FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<Button>()).Single(b => b.Text == "APPLY");
                Assert(!applyButton.Enabled, "Apply was available before profile loading.");
                Render(settings, Path.Combine(repo, ".local", "settings-parity.png"));
                PumpUntil(delegate { return applyButton.Enabled; });
                ComboBox[] choices = settings.Controls.OfType<FlowLayoutPanel>().SelectMany(p => p.Controls.OfType<ComboBox>()).ToArray();
                Assert(choices[1].SelectedIndex == 0, "Loaded default AA selection is wrong.");
                choices[1].SelectedIndex = 1;
                Render(settings, Path.Combine(repo, ".local", "settings-loaded-parity.png"));
                settings.Show();
                ((Button)settings.CancelButton).PerformClick();
                Application.DoEvents();
                Assert(settings.IsDisposed, "Settings Cancel did not close the modeless window.");
                Assert(GraphicsProfileService.Equal(File.ReadAllBytes(baseProfile.Path), baseProfile.Original), "Cancel wrote a profile.");
            }
            using (DiagnosticsForm diagnostics = new DiagnosticsForm())
            {
                diagnostics.SetReport(report);
                Assert(!diagnostics.TryCopyReport(delegate { throw new InvalidOperationException("Clipboard unavailable"); }), "Clipboard failure escaped.");
                string copied = null;
                Assert(diagnostics.TryCopyReport(delegate(string text) { copied = text; }) && copied == report, "Copy Report did not return the sanitized report.");
                Render(diagnostics, Path.Combine(repo, ".local", "diagnostics-parity.png"));
                diagnostics.Show();
                ((Button)diagnostics.CancelButton).PerformClick();
                Application.DoEvents();
                Assert(diagnostics.IsDisposed, "Diagnostics Close did not close the modeless window.");
            }
            using (LauncherForm form = new LauncherForm())
            {
                foreach (string name in new string[] { "HELP", "CREDITS", "DIAGNOSTICS", "SETTINGS", "UNINSTALL" })
                    Assert(form.Controls.OfType<Button>().Count(b => b.Text == name) == 1, "Footer action missing/duplicated: " + name);
                Button[] footer = form.Controls.OfType<Button>().Where(b => b.Top == 402).ToArray();
                foreach (Button a in footer) foreach (Button b in footer)
                    if (a != b) Assert(!a.Bounds.IntersectsWith(b.Bounds), "Footer actions overlap.");
                Render(form, Path.Combine(repo, ".local", "launcher-parity.png"));
                form.Show();
                Button settingsAction = form.Controls.OfType<Button>().Single(b => b.Text == "SETTINGS");
                settingsAction.PerformClick(); settingsAction.PerformClick();
                Assert(form.OwnedForms.OfType<GraphicsSettingsForm>().Count() == 1, "Repeated Settings opening created duplicate dialogs.");
                ((Button)form.OwnedForms.OfType<GraphicsSettingsForm>().Single().CancelButton).PerformClick();
                settingsAction.PerformClick();
                Assert(form.OwnedForms.OfType<GraphicsSettingsForm>().Count() == 1, "Settings could not reopen after Cancel.");
                ((Button)form.OwnedForms.OfType<GraphicsSettingsForm>().Single().CancelButton).PerformClick();
                Button diagnosticAction = form.Controls.OfType<Button>().Single(b => b.Text == "DIAGNOSTICS");
                diagnosticAction.PerformClick(); diagnosticAction.PerformClick();
                Assert(form.OwnedForms.OfType<DiagnosticsForm>().Count() == 1, "Repeated Diagnostics opening created duplicate dialogs.");
                ((Button)form.OwnedForms.OfType<DiagnosticsForm>().Single().CancelButton).PerformClick();
                diagnosticAction.PerformClick();
                Assert(form.OwnedForms.OfType<DiagnosticsForm>().Count() == 1, "Diagnostics could not reopen after Close.");
                ((Button)form.OwnedForms.OfType<DiagnosticsForm>().Single().CancelButton).PerformClick();
                Application.DoEvents(); form.Hide();
                foreach (float factor in new float[] { 1.5F, 2F })
                using (LauncherForm scaled = new LauncherForm())
                {
                    scaled.Scale(new SizeF(factor, factor));
                    foreach (Button button in scaled.Controls.OfType<Button>()) Assert(scaled.ClientRectangle.Contains(button.Bounds), "Scaled launcher action is clipped.");
                    Render(scaled, Path.Combine(repo, ".local", "launcher-parity-" + (int)(factor * 100) + ".png"));
                }
            }
            using (LauncherHelpForm help = new LauncherHelpForm())
            {
                Assert(help.CancelButton != null && help.FormBorderStyle == FormBorderStyle.Sizable, "Help Close/Escape/resizing missing.");
                Assert(LauncherHelpForm.HelpText.Contains("Z or Joystick Rz") && LauncherHelpForm.HelpText.Contains("separate controls"), "MW3 help guidance lost.");
                Render(help, Path.Combine(repo, ".local", "help-parity.png"));
            }
            Console.WriteLine("Launcher parity contracts passed: profile ownership, contention, failed-write preservation, redaction, document errors, 4:3 geometry and dialogs.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { Directory.Delete(fixture, true); }
    }
    private static void Refuses(Action action, string message) { bool refused = false; try { action(); } catch { refused = true; } Assert(refused, message); }
    private static void LaunchFailureLogsRejectStaleAndUnavailableEvidence(string fixture)
    {
        string log = Path.Combine(fixture, "video-evidence.out");
        DateTime started = DateTime.UtcNow;
        System.Reflection.MethodInfo read = typeof(LauncherRuntime).GetMethod("HasVideoInitializationFailure", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Func<bool> failed = delegate { return (bool)read.Invoke(null, new object[] { log, started }); };
        Assert(!failed(), "Missing log supplied video-failure evidence.");
        File.WriteAllText(log, "Error opening video... ABORTING RUN");
        File.SetLastWriteTimeUtc(log, started.AddSeconds(1));
        Assert(failed(), "Fresh video failure was rejected.");
        File.SetLastWriteTimeUtc(log, started.AddSeconds(-3));
        Assert(!failed(), "Stale video failure contaminated this launch.");
        File.WriteAllText(log, "Normal game output");
        File.SetLastWriteTimeUtc(log, started.AddSeconds(1));
        Assert(!failed(), "Unrelated output was classified as video failure.");
        File.WriteAllText(log, "Error opening video... ABORTING RUN");
        File.SetLastWriteTimeUtc(log, started.AddSeconds(1));
        using (FileStream locked = new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert(!failed(), "Unreadable log was treated as proven failure.");
    }
    private static void PumpUntil(Func<bool> complete)
    {
        System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();
        while (!complete() && timer.ElapsedMilliseconds < 5000) { Application.DoEvents(); System.Threading.Thread.Sleep(5); }
        Assert(complete(), "Dialog did not finish loading within five seconds.");
    }
    private static void Render(Form form, string path)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000);
        form.Show(); form.PerformLayout(); Application.DoEvents();
        using (Bitmap image = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size)); image.Save(path); }
        form.Hide();
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
