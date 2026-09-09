using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("MechWarrior 3 Remastered Setup")]
[assembly: AssemblyDescription("Installs the MechWarrior 3 modern-Windows preservation build from user-supplied original media.")]
[assembly: AssemblyCompany("MechWarrior 3 Remastered contributors")]
[assembly: AssemblyProduct("MechWarrior 3 Remastered")]
[assembly: AssemblyCopyright("Copyright © 2026 MechWarrior 3 Remastered contributors")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal sealed class InstallerForm : Form
{
    private readonly TextBox mw3Iso = new TextBox();
    private readonly CheckBox installPm = new CheckBox();
    private readonly TextBox pmIso = new TextBox();
    private readonly TextBox destination = new TextBox();
    private readonly Button install = new Button();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly Label status = new Label();

    public InstallerForm()
    {
        Text = "MechWarrior 3 Remastered Setup";
        ClientSize = new Size(670, 365);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        Label heading = new Label { Text = "Install MechWarrior 3 Remastered", Font = new Font("Segoe UI", 16F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 18) };
        Label intro = new Label { Text = "Your game discs are not included. Select your legally owned ISO files; Setup extracts the game and adds the modern-Windows fixes.", AutoSize = false, Size = new Size(620, 42), Location = new Point(25, 55) };
        Controls.Add(heading); Controls.Add(intro);
        AddPicker("MechWarrior 3 ISO (required)", mw3Iso, 105, PickMw3);

        installPm.Text = "Also install Pirate's Moon (optional)";
        installPm.AutoSize = true; installPm.Location = new Point(25, 166);
        installPm.CheckedChanged += delegate { pmIso.Enabled = installPm.Checked; Controls["browsePm"].Enabled = installPm.Checked; };
        Controls.Add(installPm);
        AddPicker(null, pmIso, 190, PickPm, "browsePm");
        pmIso.Enabled = false; Controls["browsePm"].Enabled = false;

        AddPicker("Install location", destination, 243, PickDestination);
        destination.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "MechWarrior 3 Remastered");

        progress.Location = new Point(25, 301); progress.Size = new Size(490, 20); progress.Style = ProgressBarStyle.Marquee; progress.Visible = false;
        status.Location = new Point(25, 327); status.Size = new Size(490, 25);
        install.Text = "Install"; install.Location = new Point(535, 298); install.Size = new Size(105, 32);
        install.Click += async delegate { await InstallAsync(); };
        Controls.Add(progress); Controls.Add(status); Controls.Add(install);
    }

    private void AddPicker(string label, TextBox box, int top, EventHandler click, string buttonName = null)
    {
        if (label != null) Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(25, top) });
        int boxTop = label == null ? top : top + 21;
        box.Location = new Point(25, boxTop); box.Size = new Size(530, 24); Controls.Add(box);
        Button browse = new Button { Text = "Browse...", Location = new Point(565, boxTop - 1), Size = new Size(75, 26), Name = buttonName ?? Guid.NewGuid().ToString() };
        browse.Click += click; Controls.Add(browse);
    }

    private void PickMw3(object sender, EventArgs e) { PickIso(mw3Iso, "Select your MechWarrior 3 ISO"); }
    private void PickPm(object sender, EventArgs e) { PickIso(pmIso, "Select your Pirate's Moon ISO"); }
    private void PickIso(TextBox target, string title)
    {
        using (OpenFileDialog dialog = new OpenFileDialog())
        {
            dialog.Title = title; dialog.Filter = "Disc images (*.iso)|*.iso|All files (*.*)|*.*";
            if (dialog.ShowDialog(this) == DialogResult.OK) target.Text = dialog.FileName;
        }
    }
    private void PickDestination(object sender, EventArgs e)
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Choose the installation folder";
            dialog.SelectedPath = destination.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK) destination.Text = dialog.SelectedPath;
        }
    }

    private async Task InstallAsync()
    {
        if (!File.Exists(mw3Iso.Text)) { MessageBox.Show(this, "Select a valid MechWarrior 3 ISO."); return; }
        if (installPm.Checked && !File.Exists(pmIso.Text)) { MessageBox.Show(this, "Select a valid Pirate's Moon ISO, or clear the optional expansion box."); return; }
        string finalRoot = Path.GetFullPath(destination.Text.Trim());
        if (Directory.Exists(finalRoot) && Directory.EnumerateFileSystemEntries(finalRoot).Any()) { MessageBox.Show(this, "The destination must be empty. Choose a new folder so existing files are never overwritten."); return; }

        install.Enabled = false; progress.Visible = true; status.Text = "Preparing installer payload...";
        try
        {
            await Task.Run(delegate { PerformInstall(finalRoot); });
            progress.Visible = false; status.Text = "Installation complete.";
            MessageBox.Show(this, "MechWarrior 3 Remastered is installed. Shortcuts were added to the desktop and Start menu.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            progress.Visible = false; install.Enabled = true; status.Text = "Installation failed.";
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetStatus(string text) { BeginInvoke((Action)delegate { status.Text = text; }); }

    private void PerformInstall(string finalRoot)
    {
        string stageRoot = finalRoot + ".installing";
        string payloadRoot = Path.Combine(Path.GetTempPath(), "MW3Remastered-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (Directory.Exists(stageRoot)) Directory.Delete(stageRoot, true);
            Directory.CreateDirectory(stageRoot); Directory.CreateDirectory(payloadRoot);
            ExtractPayload(payloadRoot);

            SetStatus("Extracting MechWarrior 3 from your ISO...");
            string mw3Drive = MountAndGetRoot(mw3Iso.Text);
            string mw3Root = Path.Combine(stageRoot, "MechWarrior 3");
            ExtractGame(mw3Drive, mw3Root, Path.Combine(payloadRoot, "tools", "UnshieldSharp.exe"));
            CopyVideo(mw3Drive, mw3Root);

            SetStatus("Applying the official 1.2 patch...");
            ApplyPatch12(Path.Combine(payloadRoot, "patch12"), mw3Root);
            string patchedExe = Path.Combine(mw3Root, "Mech3.exe");
            if (!File.Exists(patchedExe) || new FileInfo(patchedExe).Length != 2384384)
                throw new InvalidDataException("The selected MW3 media/patch did not produce the supported US v1.2 executable.");
            InstallCompatibility(payloadRoot, mw3Root, false);
            InstallCodec(mw3Root);

            if (installPm.Checked)
            {
                SetStatus("Extracting and configuring Pirate's Moon...");
                string pmDrive = MountAndGetRoot(pmIso.Text);
                string pmRoot = Path.Combine(stageRoot, "Pirates Moon");
                ExtractGame(pmDrive, pmRoot, Path.Combine(payloadRoot, "tools", "UnshieldSharp.exe"));
                CopyVideo(pmDrive, pmRoot);
                string pmExe = Path.Combine(pmRoot, "Mech3.exe");
                if (!File.Exists(pmExe) || Sha256(pmExe) != "F2B2BFFE513DD3FE252BF435BAB40A0526809DEB901083806F87EF225192A821")
                    throw new InvalidDataException("The selected Pirate's Moon ISO does not contain the supported US retail executable.");
                InstallCompatibility(payloadRoot, pmRoot, true);
            }

            File.Copy(Path.Combine(payloadRoot, "MW3Launcher.exe"), Path.Combine(stageRoot, "MW3Launcher.exe"), true);
            File.Copy(Path.Combine(payloadRoot, "THIRD_PARTY_NOTICES.md"), Path.Combine(stageRoot, "THIRD_PARTY_NOTICES.md"), true);
            File.Copy(Path.Combine(payloadRoot, "REDISTRIBUTION.md"), Path.Combine(stageRoot, "REDISTRIBUTION.md"), true);
            File.Copy(Path.Combine(payloadRoot, "PAYLOAD_MANIFEST.sha256"), Path.Combine(stageRoot, "PAYLOAD_MANIFEST.sha256"), true);
            File.Copy(Path.Combine(payloadRoot, "README.txt"), Path.Combine(stageRoot, "README.txt"), true);
            File.Copy(Path.Combine(payloadRoot, "Uninstall.exe"), Path.Combine(stageRoot, "Uninstall.exe"), true);
            CopyDirectory(Path.Combine(payloadRoot, "Third-Party"), Path.Combine(stageRoot, "Third-Party"));
            File.WriteAllText(Path.Combine(stageRoot, "install.cfg"), "Mw3Iso=" + Path.GetFullPath(mw3Iso.Text) + Environment.NewLine + "PiratesMoonIso=" + (installPm.Checked ? Path.GetFullPath(pmIso.Text) : "") + Environment.NewLine);

            if (Directory.Exists(finalRoot)) Directory.Delete(finalRoot);
            Directory.Move(stageRoot, finalRoot);
            CreateShortcuts(finalRoot, installPm.Checked);
            RegisterUninstall(finalRoot);
        }
        catch
        {
            if (Directory.Exists(stageRoot)) try { Directory.Delete(stageRoot, true); } catch { }
            throw;
        }
        finally { if (Directory.Exists(payloadRoot)) try { Directory.Delete(payloadRoot, true); } catch { } }
    }

    private static void ExtractPayload(string destination)
    {
        using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))
        {
            if (resource == null) throw new InvalidOperationException("The embedded installer payload is missing.");
            string archive = Path.Combine(destination, "payload.zip");
            using (FileStream output = File.Create(archive)) resource.CopyTo(output);
            ZipFile.ExtractToDirectory(archive, destination);
            File.Delete(archive);
        }
    }

    private static string MountAndGetRoot(string iso)
    {
        string escaped = Path.GetFullPath(iso).Replace("'", "''");
        string script = "$ErrorActionPreference='Stop';$p='" + escaped + "';$i=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue;if(-not $i -or -not $i.Attached){$i=Mount-DiskImage -ImagePath $p -PassThru}else{$i=Get-DiskImage -ImagePath $p};$v=$i|Get-Volume;Write-Output ($v.DriveLetter+':\\')";
        ProcessStartInfo info = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + script.Replace("\"", "\\\"") + "\"");
        info.UseShellExecute = false; info.CreateNoWindow = true; info.RedirectStandardOutput = true; info.RedirectStandardError = true;
        using (Process process = Process.Start(info))
        {
            string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Could not mount ISO: " + error.Trim());
            string root = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            if (String.IsNullOrEmpty(root) || !Directory.Exists(root)) throw new InvalidOperationException("The ISO mounted, but its drive could not be located.");
            return root.Trim();
        }
    }

    private static void ExtractGame(string discRoot, string gameRoot, string extractor)
    {
        Directory.CreateDirectory(gameRoot);
        string[] headers = Directory.GetFiles(discRoot, "data1.hdr", SearchOption.AllDirectories);
        string header = headers.Where(delegate(string h) { return File.Exists(Path.ChangeExtension(h, ".cab")); })
            .OrderByDescending(delegate(string h) { return new FileInfo(Path.ChangeExtension(h, ".cab")).Length; }).FirstOrDefault();
        if (header != null)
        {
            if (!File.Exists(extractor)) throw new FileNotFoundException("The InstallShield extraction tool is missing.");
            Run(extractor, "--output=\"" + gameRoot + "\" \"" + header + "\"", Path.GetDirectoryName(header));
        }
        else
        {
            string exe = Directory.GetFiles(discRoot, "Mech3.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (exe == null) throw new InvalidDataException("This ISO does not contain a recognizable MechWarrior 3 installation.");
            CopyDirectory(Path.GetDirectoryName(exe), gameRoot);
        }
        if (!File.Exists(Path.Combine(gameRoot, "Mech3.exe"))) throw new InvalidDataException("Game extraction completed without Mech3.exe.");
    }

    private static void CopyVideo(string discRoot, string gameRoot)
    {
        string video = Directory.GetDirectories(discRoot, "video", SearchOption.AllDirectories).FirstOrDefault();
        if (video != null) CopyDirectory(video, Path.Combine(gameRoot, "video"));
    }

    private static void ApplyPatch12(string patchRoot, string gameRoot)
    {
        foreach (string entry in Directory.GetFileSystemEntries(patchRoot))
        {
            if (Path.GetFileName(entry).Equals("Patch_Files", StringComparison.OrdinalIgnoreCase)) continue;
            string target = Path.Combine(gameRoot, Path.GetFileName(entry));
            if (Directory.Exists(entry)) CopyDirectory(entry, target); else File.Copy(entry, target, true);
        }
        File.Copy(Path.Combine(patchRoot, "Patch_Files", "Mech3.exe"), Path.Combine(gameRoot, "Mech3.exe"), true);
    }

    private static void InstallCompatibility(string payload, string gameRoot, bool pm)
    {
        string compat = Path.Combine(payload, "compat");
        File.Copy(Path.Combine(compat, "zipfixup.dll"), Path.Combine(gameRoot, "zipfixup.dll"), true);
        File.Copy(Path.Combine(compat, "zfapply.exe"), Path.Combine(gameRoot, "zfapply.exe"), true);
        File.Copy(Path.Combine(compat, "ddraw.dll"), Path.Combine(gameRoot, "ddraw.dll"), true);
        File.Copy(Path.Combine(payload, "config", "DDrawCompat.ini"), Path.Combine(gameRoot, "DDrawCompat.ini"), true);
        CopyDirectory(Path.Combine(payload, "shaders"), Path.Combine(gameRoot, "common-shaders-master"));
        Run(Path.Combine(gameRoot, "zfapply.exe"), "", gameRoot);
        if (!File.Exists(Path.Combine(gameRoot, "Mech3fixup.exe"))) throw new InvalidOperationException("ZipperFixup did not create Mech3fixup.exe.");

        File.Copy(Path.Combine(compat, "winmm.dll"), Path.Combine(gameRoot, "winmm.dll"), true);
        File.Copy(Path.Combine(payload, "config", "winmm.ini"), Path.Combine(gameRoot, "winmm.ini"), true);
        string player = Path.Combine(gameRoot, "mcicda"); string tracks = Path.Combine(player, "music"); Directory.CreateDirectory(tracks);
        File.Copy(Path.Combine(compat, "cdaudioplr.exe"), Path.Combine(player, "cdaudioplr.exe"), true);
        File.Copy(Path.Combine(compat, "cdaudio_vol.ini"), Path.Combine(player, "cdaudio_vol.ini"), true);
        string sourceMusic = Path.Combine(payload, "music", pm ? "pm" : "mw3");
        foreach (string track in Directory.GetFiles(sourceMusic, "*.mp3")) File.Copy(track, Path.Combine(tracks, Path.GetFileName(track)), true);
    }

    private static void InstallCodec(string gameRoot)
    {
        string source = Path.Combine(gameRoot, "Ir50_32.dll");
        if (!File.Exists(source) || Sha256(source) != "56760E0EA8C8709F4A0C34BEE7289A87188AEDD8BDDFD05F8F62BEE2F3F91238") return;
        string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "ir50_32.dll");
        if (!File.Exists(target)) File.Copy(source, target);
        if (Sha256(target) == "56760E0EA8C8709F4A0C34BEE7289A87188AEDD8BDDFD05F8F62BEE2F3F91238")
        using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
        using (RegistryKey key = hklm.CreateSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Drivers32")) key.SetValue("vidc.iv50", "ir50_32.dll", RegistryValueKind.String);
    }

    private static void Run(string file, string args, string working)
    {
        ProcessStartInfo info = new ProcessStartInfo(file, args); info.WorkingDirectory = working; info.UseShellExecute = false; info.CreateNoWindow = true; info.RedirectStandardInput = true;
        using (Process process = Process.Start(info)) { process.StandardInput.Close(); process.WaitForExit(); if (process.ExitCode != 0) throw new InvalidOperationException(Path.GetFileName(file) + " failed with exit code " + process.ExitCode + "."); }
    }
    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (string dir in Directory.GetDirectories(source)) CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
    }
    private static string Sha256(string path)
    {
        using (SHA256 sha = SHA256.Create()) using (FileStream stream = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

    private static void CreateShortcuts(string root, bool pm)
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        string menu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs", "MechWarrior 3 Remastered"); Directory.CreateDirectory(menu);
        CreateShortcut(Path.Combine(desktop, "MechWarrior 3 Remastered.lnk"), root, "mw3", Path.Combine(root, "MechWarrior 3", "Mech3fixup.exe"));
        CreateShortcut(Path.Combine(menu, "MechWarrior 3 Remastered.lnk"), root, "mw3", Path.Combine(root, "MechWarrior 3", "Mech3fixup.exe"));
        if (pm) { CreateShortcut(Path.Combine(desktop, "MechWarrior 3 - Pirate's Moon.lnk"), root, "pm", Path.Combine(root, "Pirates Moon", "Mech3fixup.exe")); CreateShortcut(Path.Combine(menu, "Pirate's Moon.lnk"), root, "pm", Path.Combine(root, "Pirates Moon", "Mech3fixup.exe")); }
    }
    private static void CreateShortcut(string path, string root, string args, string icon)
    {
        Type type = Type.GetTypeFromProgID("WScript.Shell"); dynamic shell = Activator.CreateInstance(type); dynamic link = shell.CreateShortcut(path);
        link.TargetPath = Path.Combine(root, "MW3Launcher.exe"); link.Arguments = args; link.WorkingDirectory = root; link.IconLocation = icon + ",0"; link.Save();
    }
    private static void RegisterUninstall(string root)
    {
        using (RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (RegistryKey key = hklm.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MW3Remastered"))
        {
            key.SetValue("DisplayName", "MechWarrior 3 Remastered"); key.SetValue("DisplayVersion", "1.0.0"); key.SetValue("Publisher", "Community preservation project");
            key.SetValue("InstallLocation", root); key.SetValue("DisplayIcon", Path.Combine(root, "MW3Launcher.exe"));
            key.SetValue("UninstallString", "\"" + Path.Combine(root, "Uninstall.exe") + "\""); key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
    }
}

internal static class InstallerProgram
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new InstallerForm());
    }
}
