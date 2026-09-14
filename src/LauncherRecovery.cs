using System;
using System.Diagnostics;
using System.IO;
using System.Text;

internal sealed class VideoRecoveryConfig : IDisposable
{
    private readonly string path;
    private readonly string backupPath;
    private readonly string backupTempPath;
    private readonly string statePath;
    private readonly Action<string> log;
    private bool enabled;
    private bool originalExisted;
    private byte[] originalBytes;
    private string originalText = String.Empty;
    private bool restoreStatePrepared;

    public VideoRecoveryConfig(string path, Action<string> log)
    {
        this.path = path;
        this.log = log ?? delegate { };
        backupPath = path + ".launcher-backup";
        backupTempPath = backupPath + ".tmp";
        statePath = path + ".launcher-state";
        enabled = RecoverInterruptedState();
        if (!enabled) return;

        try
        {
            originalExisted = File.Exists(path);
            if (originalExisted)
            {
                originalBytes = File.ReadAllBytes(path);
                originalText = File.ReadAllText(path);
            }
        }
        catch (Exception ex)
        {
            enabled = false;
            this.log("Warning: could not preserve the existing DDrawCompat process configuration; adaptive recovery is disabled: " + ex.Message);
        }
    }

    public string Apply(int attempt)
    {
        string name;
        string settings;
        switch (attempt)
        {
            case 1:
                return "standard renderer";
            case 2:
                name = "upscaled renderer without MSAA";
                settings = "ForceD3D9On12 = off\r\nResolutionScale = display(1)\r\nAntialiasing = off\r\nRemasterIntroChromaCleanup = off";
                break;
            case 3:
                name = "D3D9On12 upscaled renderer without MSAA";
                settings = "ForceD3D9On12 = on\r\nResolutionScale = display(1)\r\nAntialiasing = off\r\nRemasterIntroChromaCleanup = off";
                break;
            case 4:
                name = "last-resort native-scale renderer";
                settings = "ForceD3D9On12 = off\r\nResolutionScale = app(1)\r\nAntialiasing = off\r\nRemasterIntroChromaCleanup = off";
                break;
            default:
                name = "D3D9On12 last-resort native-scale renderer";
                settings = "ForceD3D9On12 = on\r\nResolutionScale = app(1)\r\nAntialiasing = off\r\nRemasterIntroChromaCleanup = off";
                break;
        }

        if (!enabled) return name + " (override unavailable)";
        try
        {
            PrepareRestoreState();
            string separator = String.IsNullOrEmpty(originalText) || originalText.EndsWith("\n") ? String.Empty : Environment.NewLine;
            string contents = originalText + separator +
                "# Temporary launcher video-recovery profile; restored after the game exits." + Environment.NewLine +
                settings + Environment.NewLine;
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            log("Attempt " + attempt + " applying temporary video recovery profile: " + name + ".");
            return name;
        }
        catch (Exception ex)
        {
            log("Warning: could not apply " + name + ": " + ex.Message);
            Restore();
            enabled = false;
            return name + " (override failed)";
        }
    }

    public void Dispose()
    {
        Restore();
    }

    private void PrepareRestoreState()
    {
        if (restoreStatePrepared) return;
        if (originalExisted)
        {
            File.WriteAllBytes(backupTempPath, originalBytes);
            File.Move(backupTempPath, backupPath);
            File.WriteAllText(statePath, "existing", new UTF8Encoding(false));
        }
        else
        {
            File.WriteAllText(statePath, "absent", new UTF8Encoding(false));
        }
        restoreStatePrepared = true;
    }

    private bool RecoverInterruptedState()
    {
        try
        {
            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, path, true);
                File.Delete(backupPath);
                log("Restored DDrawCompat process configuration left by an interrupted launch.");
            }
            else if (File.Exists(statePath))
            {
                string state = File.ReadAllText(statePath).Trim();
                if (state.Equals("absent", StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(path)) File.Delete(path);
                    log("Removed a temporary DDrawCompat process configuration left by an interrupted launch.");
                }
                else
                {
                    log("Warning: interrupted video recovery has no backup; adaptive recovery is disabled to protect the current configuration.");
                    return false;
                }
            }
            if (File.Exists(statePath)) File.Delete(statePath);
            if (File.Exists(backupTempPath)) File.Delete(backupTempPath);
            return true;
        }
        catch (Exception ex)
        {
            log("Warning: could not recover interrupted DDrawCompat configuration; adaptive recovery is disabled: " + ex.Message);
            return false;
        }
    }

    private void Restore()
    {
        if (!restoreStatePrepared) return;
        try
        {
            if (originalExisted) File.WriteAllBytes(path, originalBytes);
            else if (File.Exists(path)) File.Delete(path);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            if (File.Exists(statePath)) File.Delete(statePath);
            if (File.Exists(backupTempPath)) File.Delete(backupTempPath);
            restoreStatePrepared = false;
        }
        catch (Exception ex)
        {
            log("Warning: could not restore the original DDrawCompat process configuration: " + ex.Message);
        }
    }
}

internal sealed class IsoMountSession : IDisposable
{
    private readonly string imagePath;
    private readonly Action<string> log;
    private readonly string ownershipStatePath;
    private bool ownsMount;
    private bool disposed;
    private string root;

    private IsoMountSession(string imagePath, Action<string> log)
    {
        this.imagePath = Path.GetFullPath(imagePath);
        this.log = log ?? delegate { };
        string stateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MechWarrior 3 Remastered");
        Directory.CreateDirectory(stateDirectory);
        ownershipStatePath = Path.Combine(stateDirectory, "mounted-image.state");
    }

    public bool OwnsMount { get { return ownsMount; } }
    public string Root { get { return root; } }

    public static IsoMountSession Attach(string imagePath, Action<string> log)
    {
        IsoMountSession session = new IsoMountSession(imagePath, log);
        try
        {
            session.Mount();
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (!ownsMount) return;

        try
        {
            string escaped = imagePath.Replace("'", "''");
            string command = "$ErrorActionPreference='Stop'; $p='" + escaped + "'; " +
                "$deadline=[DateTime]::UtcNow.AddSeconds(15); $last=''; " +
                "do{$i=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue; " +
                "if(-not $i -or -not $i.Attached){Write-Output 'EJECTED|verified'; exit 0}; " +
                "try{Dismount-DiskImage -ImagePath $p -ErrorAction Stop}catch{$last=$_.Exception.Message}; " +
                "Start-Sleep -Milliseconds 250}while([DateTime]::UtcNow -lt $deadline); " +
                "Write-Error ('Disc image remained attached after eject attempts. '+$last); exit 3";
            PowerShellResult result = RunPowerShell(command);
            if (result.ExitCode == 0 && OutputIndicatesVerifiedEject(result.Output))
            {
                try { if (File.Exists(ownershipStatePath)) File.Delete(ownershipStatePath); } catch { }
                log("Ejected launcher-mounted disc image.");
            }
            else
                log("Warning: could not eject launcher-mounted disc image: " + Sanitize(result.Error) + ".");
        }
        catch (Exception ex)
        {
            log("Warning: could not eject launcher-mounted disc image: " + Sanitize(ex.Message) + ".");
        }
    }

    internal static bool OutputIndicatesVerifiedEject(string output)
    {
        if (String.IsNullOrEmpty(output)) return false;
        string[] lines = output.Replace("\r", String.Empty).Split('\n');
        foreach (string line in lines)
            if (line.Trim().Equals("EJECTED|verified", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static bool OutputIndicatesOwnedMount(string output)
    {
        if (String.IsNullOrEmpty(output)) return false;
        string[] lines = output.Replace("\r", String.Empty).Split('\n');
        foreach (string line in lines)
            if (line.Trim().Equals("MOUNT|owned", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static string GetReadyRoot(string output)
    {
        if (String.IsNullOrEmpty(output)) return null;
        string[] lines = output.Replace("\r", String.Empty).Split('\n');
        foreach (string line in lines)
        {
            string[] fields = line.Trim().Split('|');
            if (fields.Length >= 2 && fields[0].Equals("READY", StringComparison.OrdinalIgnoreCase))
                return fields[1];
        }
        return null;
    }

    private void Mount()
    {
        string escaped = imagePath.Replace("'", "''");
        string command = "$ErrorActionPreference='Stop'; $p='" + escaped + "'; $owned=$false; " +
            "$i=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue; " +
            "if(-not $i -or -not $i.Attached){Mount-DiskImage -ImagePath $p | Out-Null; $owned=$true}; " +
            "if($owned){Write-Output 'MOUNT|owned'}else{Write-Output 'MOUNT|existing'}; " +
            "$deadline=[DateTime]::UtcNow.AddSeconds(30); " +
            "do{$i=Get-DiskImage -ImagePath $p -ErrorAction SilentlyContinue; " +
            "$volumes=@($i | Get-Volume -ErrorAction SilentlyContinue | Where-Object {$_.DriveLetter}); " +
            "foreach($volume in $volumes){$root=([string]$volume.DriveLetter)+':\\'; " +
            "if(Test-Path -LiteralPath $root -PathType Container){" +
            "Write-Output ('READY|'+$root+'|'+[string]$volume.FileSystemLabel); exit 0}}; " +
            "Start-Sleep -Milliseconds 250}while([DateTime]::UtcNow -lt $deadline); exit 2";
        PowerShellResult result = RunPowerShell(command);
        ownsMount = OutputIndicatesOwnedMount(result.Output);
        root = GetReadyRoot(result.Output);
        if (!ownsMount && HasMatchingOwnershipState())
        {
            ownsMount = true;
            log("Reclaimed a disc image left mounted by an interrupted launcher run.");
        }
        if (ownsMount)
        {
            try { File.WriteAllText(ownershipStatePath, imagePath, new UTF8Encoding(false)); }
            catch (Exception ex) { log("Warning: could not persist disc-image ownership: " + Sanitize(ex.Message) + "."); }
        }
        if (result.ExitCode == 0 && !String.IsNullOrEmpty(root) && Directory.Exists(root))
        {
            log("Disc image is mounted and readable (" + (ownsMount ? "launcher-owned" : "pre-existing") + ").");
            return;
        }

        log("Disc image readiness failed with exit code " + result.ExitCode +
            (String.IsNullOrEmpty(result.Error) ? "." : ": " + Sanitize(result.Error)));
        if (result.ExitCode == 0 || result.ExitCode == 2)
            throw new InvalidOperationException("Windows attached the selected ISO, but its disc drive did not become readable within 30 seconds. Eject the mounted image, right-click the ISO, choose Mount, and try again.");
        throw new InvalidOperationException("Windows could not mount the selected ISO. Right-click the ISO, choose Mount, then try again.");
    }

    private PowerShellResult RunPowerShell(string command)
    {
        // EncodedCommand preserves literal paths and PowerShell syntax across the
        // Windows command-line boundary. The script is generated locally only.
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        ProcessStartInfo info = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " + encodedCommand);
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using (Process process = Process.Start(info))
        {
            StringBuilder errors = new StringBuilder();
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
            {
                if (!String.IsNullOrEmpty(args.Data)) errors.AppendLine(args.Data);
            };
            process.BeginErrorReadLine();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            process.WaitForExit();
            return new PowerShellResult(process.ExitCode, output, errors.ToString().Trim());
        }
    }

    private string Sanitize(string message)
    {
        return String.IsNullOrEmpty(message) ? "unknown error" : message.Replace(imagePath, "<selected ISO>");
    }

    private bool HasMatchingOwnershipState()
    {
        try
        {
            return File.Exists(ownershipStatePath) &&
                Path.GetFullPath(File.ReadAllText(ownershipStatePath).Trim()).Equals(
                    imagePath, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private sealed class PowerShellResult
    {
        public readonly int ExitCode;
        public readonly string Output;
        public readonly string Error;

        public PowerShellResult(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }
    }
}

internal static class LaunchResultClassifier
{
    public static bool IsEarlyAbnormalExit(int exitCode, TimeSpan runtime)
    {
        return exitCode != 0 && runtime.TotalSeconds < 20;
    }
}

internal sealed class LaunchLease : IDisposable
{
    private readonly string path;
    private FileStream stream;

    private LaunchLease(string path, FileStream stream)
    {
        this.path = path;
        this.stream = stream;
    }

    public static LaunchLease Acquire(string stateDirectory)
    {
        Directory.CreateDirectory(stateDirectory);
        string path = Path.Combine(stateDirectory, "launch.lock");
        try
        {
            FileStream stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return new LaunchLease(path, stream);
        }
        catch (IOException)
        {
            throw new InvalidOperationException("Another MechWarrior 3 launch or recovery attempt is already running. Wait for it to finish before trying again.");
        }
    }

    public void Dispose()
    {
        if (stream == null) return;
        stream.Dispose();
        stream = null;
        try { File.Delete(path); } catch { }
    }
}
