using System;
using System.IO;
using System.Linq;
using System.Text;

internal static class LauncherRecoverySmoke
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "mw3-launcher-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            ExistingConfigIsRestored(root);
            MissingConfigIsRemoved(root);
            UpscaledProfilesPrecedeNativeFallback(root);
            InterruptedExistingConfigRecovers(root);
            InterruptedMissingConfigRecovers(root);
            ProcessPathsAreScoped(root);
            IsoMountOwnershipIsParsed();
            PreMountedDiscFolderIsReadOnly(root);
            ConcurrentLaunchesAreRejected(root);
            EarlyCrashesRemainFailures();
            Console.WriteLine("Launcher recovery tests passed.");
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

    private static void ExistingConfigIsRestored(string root)
    {
        string path = Path.Combine(root, "existing.ini");
        File.WriteAllText(path, "LogLevel = debug\r\nAntialiasing = msaa2x(0)\r\n", new UnicodeEncoding(false, true));
        byte[] expected = File.ReadAllBytes(path);
        using (VideoRecoveryConfig config = new VideoRecoveryConfig(path, delegate { }))
        {
            Assert(config.Apply(2) == "upscaled renderer without MSAA", "Unexpected upscaled recovery profile name.");
            string contents = File.ReadAllText(path);
            Assert(contents.Contains("ForceD3D9On12 = off"), "Native D3D9 override was not written.");
            Assert(contents.Contains("ResolutionScale = display(1)"), "Upscaled recovery did not retain desktop-resolution rendering.");
            Assert(contents.Contains("Antialiasing = off"), "Upscaled recovery did not disable MSAA.");
            Assert(!contents.Contains("ResolutionScale = app(1)"), "Upscaled recovery unexpectedly selected native resolution.");
            Assert(contents.Contains("RemasterIntroChromaCleanup = off"), "Recovery did not disable the optional intro filter.");
        }
        Assert(expected.SequenceEqual(File.ReadAllBytes(path)), "Existing config was not restored byte-for-byte.");
        AssertNoRecoveryFiles(path);
    }

    private static void MissingConfigIsRemoved(string root)
    {
        string path = Path.Combine(root, "missing.ini");
        using (VideoRecoveryConfig config = new VideoRecoveryConfig(path, delegate { }))
        {
            Assert(config.Apply(5) == "D3D9On12 last-resort native-scale renderer", "Unexpected last-resort profile name.");
            string contents = File.ReadAllText(path);
            Assert(contents.Contains("ResolutionScale = app(1)") && contents.Contains("Antialiasing = off"), "Safe overrides were not written.");
            Assert(contents.Contains("RemasterIntroChromaCleanup = off"), "Safe recovery did not disable the optional intro filter.");
        }
        Assert(!File.Exists(path), "Temporary config was not removed.");
        AssertNoRecoveryFiles(path);
    }

    private static void UpscaledProfilesPrecedeNativeFallback(string root)
    {
        string path = Path.Combine(root, "profile-order.ini");
        using (VideoRecoveryConfig config = new VideoRecoveryConfig(path, delegate { }))
        {
            Assert(config.Apply(3) == "D3D9On12 upscaled renderer without MSAA", "Unexpected third recovery profile.");
            string upscaled = File.ReadAllText(path);
            Assert(upscaled.Contains("ForceD3D9On12 = on") && upscaled.Contains("ResolutionScale = display(1)"),
                "Third recovery profile did not retain upscaling.");
            Assert(!upscaled.Contains("ResolutionScale = app(1)"), "Third recovery profile selected native resolution too early.");

            Assert(config.Apply(4) == "last-resort native-scale renderer", "Unexpected fourth recovery profile.");
            Assert(File.ReadAllText(path).Contains("ResolutionScale = app(1)"), "Last-resort native-scale profile was not written.");
        }
        Assert(!File.Exists(path), "Profile-order temporary config was not removed.");
        AssertNoRecoveryFiles(path);
    }

    private static void InterruptedExistingConfigRecovers(string root)
    {
        string path = Path.Combine(root, "interrupted-existing.ini");
        byte[] expected = Encoding.UTF8.GetBytes("LogLevel = trace\r\n");
        File.WriteAllBytes(path, expected);
        VideoRecoveryConfig interrupted = new VideoRecoveryConfig(path, delegate { });
        interrupted.Apply(3);
        using (VideoRecoveryConfig recovered = new VideoRecoveryConfig(path, delegate { }))
        {
            Assert(expected.SequenceEqual(File.ReadAllBytes(path)), "Interrupted existing config was not recovered on restart.");
        }
        AssertNoRecoveryFiles(path);
    }

    private static void InterruptedMissingConfigRecovers(string root)
    {
        string path = Path.Combine(root, "interrupted-missing.ini");
        VideoRecoveryConfig interrupted = new VideoRecoveryConfig(path, delegate { });
        interrupted.Apply(2);
        using (VideoRecoveryConfig recovered = new VideoRecoveryConfig(path, delegate { }))
        {
            Assert(!File.Exists(path), "Interrupted temporary config was not removed on restart.");
        }
        AssertNoRecoveryFiles(path);
    }

    private static void ProcessPathsAreScoped(string root)
    {
        string install = Path.Combine(root, "install");
        string owned = Path.Combine(install, "MechWarrior 3", "mcicda", "cdaudioplr.exe");
        string unrelated = Path.Combine(root, "other", "mcicda", "cdaudioplr.exe");
        Assert(InstalledProcessScope.IsOwnedExecutablePath(owned, install, Path.Combine("mcicda", "cdaudioplr.exe")), "Owned helper path was rejected.");
        Assert(!InstalledProcessScope.IsOwnedExecutablePath(unrelated, install, Path.Combine("mcicda", "cdaudioplr.exe")), "Unrelated helper path was accepted.");
    }

    private static void IsoMountOwnershipIsParsed()
    {
        Assert(IsoMountSession.OutputIndicatesOwnedMount("MOUNT|owned\r\nREADY|D:\\|MECH3"), "Launcher-owned mount was not recognized.");
        Assert(!IsoMountSession.OutputIndicatesOwnedMount("MOUNT|existing\r\nREADY|D:\\|MECH3"), "Pre-existing mount was incorrectly claimed.");
        Assert(!IsoMountSession.OutputIndicatesOwnedMount("READY|D:\\|MECH3"), "Missing ownership marker was incorrectly claimed.");
        Assert(IsoMountSession.GetReadyRoot("MOUNT|owned\r\nREADY|D:\\|MECH3") == "D:\\", "Mounted disc root was not parsed.");
        Assert(IsoMountSession.OutputIndicatesVerifiedEject("EJECTED|verified"), "Verified eject marker was not recognized.");
        Assert(!IsoMountSession.OutputIndicatesVerifiedEject(""), "An empty eject result was accepted.");
        Assert(!IsoMountSession.OutputIndicatesVerifiedEject("Dismount-DiskImage returned"), "An unverified eject result was accepted.");
    }

    private static void PreMountedDiscFolderIsReadOnly(string root)
    {
        string disc = Path.Combine(root, "mapped-disc");
        Directory.CreateDirectory(disc);
        bool rejected = false;
        try { using (DiscMediaSession ignored = DiscMediaSession.Open(disc, delegate { })) { } }
        catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "An arbitrary folder was accepted as disc media.");
        string installer = Path.Combine(disc, "setup");
        Directory.CreateDirectory(installer);
        File.WriteAllText(Path.Combine(installer, "DATA1.HDR"), "test");
        File.WriteAllText(Path.Combine(installer, "DATA1.CAB"), "test");
        using (DiscMediaSession media = DiscMediaSession.Open(disc, delegate { }))
        {
            Assert(media.Root == Path.GetFullPath(disc), "The selected disc folder root changed.");
            Assert(!media.OwnsMount, "The selected disc folder was marked as an owned mount.");
        }
        Assert(Directory.Exists(disc), "Disposing a selected disc folder removed it.");
    }

    private static void ConcurrentLaunchesAreRejected(string root)
    {
        string state = Path.Combine(root, "lease");
        using (LaunchLease first = LaunchLease.Acquire(state))
        {
            bool rejected = false;
            try
            {
                using (LaunchLease second = LaunchLease.Acquire(state)) { }
            }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected, "A concurrent launcher acquired the active launch lease.");
        }
        using (LaunchLease afterRelease = LaunchLease.Acquire(state)) { }
    }

    private static void EarlyCrashesRemainFailures()
    {
        Assert(LaunchResultClassifier.IsEarlyAbnormalExit(unchecked((int)0xC0000005), TimeSpan.FromSeconds(3)),
            "An early access violation was incorrectly classified as a successful launch.");
        Assert(!LaunchResultClassifier.IsEarlyAbnormalExit(0, TimeSpan.FromSeconds(3)),
            "A clean short exit was incorrectly classified as a crash.");
        Assert(!LaunchResultClassifier.IsEarlyAbnormalExit(1, TimeSpan.FromSeconds(30)),
            "A late nonzero exit was incorrectly classified as video initialization failure.");
    }

    private static void AssertNoRecoveryFiles(string path)
    {
        Assert(!File.Exists(path + ".launcher-backup"), "Recovery backup was not cleaned up.");
        Assert(!File.Exists(path + ".launcher-backup.tmp"), "Recovery temporary backup was not cleaned up.");
        Assert(!File.Exists(path + ".launcher-state"), "Recovery state was not cleaned up.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
