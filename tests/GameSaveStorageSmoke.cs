using System;
using System.IO;

internal static class GameSaveStorageSmoke
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "MW3SaveStorageSmoke-" + Guid.NewGuid().ToString("N"));
        string install = Path.Combine(root, "install");
        string staged = Path.Combine(root, "staged");
        string backup = Path.Combine(root, "backup");
        try
        {
            WriteSave(install, "MechWarrior 3", "Ace.mw3", new byte[] { 1, 2, 3 });
            WriteSave(install, "MechWarrior 3", Path.Combine("games", "Ace0102.mw3"), new byte[] { 4, 5, 6, 7 });
            WriteSave(install, "Pirates Moon", Path.Combine("games", "Moon0101.mw3"), new byte[] { 8, 9 });
            Assert(GameSaveStorage.IsEmptyOrPreservedSavesOnly(install), "A save-only remainder was rejected.");

            string interrupted = Path.Combine(root, "interrupted");
            WriteSave(interrupted, "MechWarrior 3", "Safe.mw3", new byte[] { 12 });
            File.WriteAllText(Path.Combine(interrupted, "MW3Launcher.exe"), "locked launcher remnant");
            Assert(!GameSaveStorage.IsEmptyOrPreservedSavesOnly(interrupted), "A launcher remnant was accepted as a save-only destination.");
            Assert(GameSaveStorage.DestinationBlockedMessage(interrupted).Contains("previous uninstall is incomplete"), "An interrupted uninstall did not receive the specific recovery diagnostic.");

            GameSaveStorage.CopyPreservedSaves(install, staged);
            AssertBytes(Path.Combine(staged, "MechWarrior 3", "pilots", "Ace.mw3"), new byte[] { 1, 2, 3 });
            AssertBytes(Path.Combine(staged, "Pirates Moon", "pilots", "games", "Moon0101.mw3"), new byte[] { 8, 9 });

            File.WriteAllText(Path.Combine(install, "unexpected.txt"), "do not overwrite");
            Assert(!GameSaveStorage.IsEmptyOrPreservedSavesOnly(install), "Unexpected destination content was accepted.");
            File.Delete(Path.Combine(install, "unexpected.txt"));
            Directory.CreateDirectory(Path.Combine(install, "MechWarrior 3", "keys"));
            Assert(!GameSaveStorage.IsEmptyOrPreservedSavesOnly(install), "Control mappings were incorrectly classified as saves.");
            Directory.Delete(Path.Combine(install, "MechWarrior 3", "keys"));

            string unrelated = Path.Combine(root, "unrelated");
            Directory.CreateDirectory(unrelated);
            File.WriteAllText(Path.Combine(unrelated, "personal.txt"), "keep me");
            AssertThrows(delegate { GameSaveStorage.RemoveInstallationPreservingSaves(unrelated, Path.Combine(root, "unrelated-backup")); }, "An unrelated directory was accepted for removal.");
            Assert(File.Exists(Path.Combine(unrelated, "personal.txt")), "An unrelated file was removed.");

            string malformed = Path.Combine(root, "malformed");
            Directory.CreateDirectory(Path.Combine(malformed, "MechWarrior 3"));
            File.WriteAllText(Path.Combine(malformed, "MechWarrior 3", "pilots"), "not a save directory");
            Assert(!GameSaveStorage.IsEmptyOrPreservedSavesOnly(malformed), "A file masquerading as the pilots directory was accepted.");

            MarkInstallation(install);
            string installedBinary = Path.Combine(install, "installed-binary.dll");
            File.WriteAllText(installedBinary, "remove me");
            File.SetAttributes(installedBinary, FileAttributes.ReadOnly | FileAttributes.Archive);
            GameSaveStorage.RemoveInstallationPreservingSaves(install, backup);
            Assert(!File.Exists(Path.Combine(install, "installed-binary.dll")), "Uninstall debris remained.");
            Assert(GameSaveStorage.IsEmptyOrPreservedSavesOnly(install), "Uninstall left content other than saves.");
            AssertBytes(Path.Combine(install, "MechWarrior 3", "pilots", "games", "Ace0102.mw3"), new byte[] { 4, 5, 6, 7 });
            AssertBytes(Path.Combine(install, "Pirates Moon", "pilots", "games", "Moon0101.mw3"), new byte[] { 8, 9 });
            Assert(!Directory.Exists(backup), "Verified temporary backup was not removed.");

            string noSaves = Path.Combine(root, "no-saves");
            Directory.CreateDirectory(Path.Combine(noSaves, "MechWarrior 3", "pilots"));
            MarkInstallation(noSaves);
            File.WriteAllText(Path.Combine(noSaves, "installed.bin"), "remove me");
            GameSaveStorage.RemoveInstallationPreservingSaves(noSaves, Path.Combine(root, "no-saves-backup"));
            Assert(!Directory.Exists(noSaves), "An empty installation root remained when there were no saves.");

            string stagingFinal = Path.Combine(root, "stale-target");
            string staging = stagingFinal + ".installing";
            Directory.CreateDirectory(Path.Combine(staging, "MechWarrior 3", "video"));
            string staleVideo = Path.Combine(staging, "MechWarrior 3", "video", "C1.AVI");
            File.WriteAllText(staleVideo, "read-only ISO-derived video");
            File.SetAttributes(staleVideo, FileAttributes.ReadOnly | FileAttributes.Archive);
            GameSaveStorage.RemoveInstallerStaging(staging, stagingFinal);
            Assert(!Directory.Exists(staging), "Read-only stale installer staging was not removed.");

            string saveRemainder = Path.Combine(root, "save-remainder");
            WriteSave(saveRemainder, "MechWarrior 3", "Readonly.mw3", new byte[] { 10, 11 });
            File.SetAttributes(Path.Combine(saveRemainder, "MechWarrior 3", "pilots", "Readonly.mw3"), FileAttributes.ReadOnly);
            GameSaveStorage.RemovePreservedSaveRemainder(saveRemainder);
            Assert(!Directory.Exists(saveRemainder), "Verified read-only save remainder was not replaceable after staging.");

            Console.WriteLine("Game save preservation contract passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if (Directory.Exists(root)) try { Directory.Delete(root, true); } catch { } }
    }

    private static void WriteSave(string root, string game, string relative, byte[] content)
    {
        string path = Path.Combine(root, game, "pilots", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, content);
    }

    private static void MarkInstallation(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "MechWarrior 3"));
        foreach (string marker in new[] { "MW3Launcher.exe", "Uninstall.exe", "install.cfg", "PAYLOAD_MANIFEST.sha256" })
            File.WriteAllText(Path.Combine(root, marker), "test marker");
    }

    private static void AssertThrows(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void AssertBytes(string path, byte[] expected)
    {
        byte[] actual = File.ReadAllBytes(path);
        Assert(actual.Length == expected.Length, "Save length changed: " + path);
        for (int i = 0; i < expected.Length; i++) Assert(actual[i] == expected[i], "Save content changed: " + path);
    }

    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
