using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

internal static class GameSaveStorage
{
    private static readonly string[] GameDirectories = { "MechWarrior 3", "Pirates Moon" };
    private const string SaveDirectory = "pilots";

    internal static bool IsEmptyOrPreservedSavesOnly(string installRoot)
    {
        if (!Directory.Exists(installRoot)) return true;
        if ((File.GetAttributes(installRoot) & FileAttributes.ReparsePoint) != 0) return false;
        return IsAllowedTree(installRoot, installRoot);
    }

    internal static string DestinationBlockedMessage(string installRoot)
    {
        string launcher = Path.Combine(installRoot, "MW3Launcher.exe");
        string uninstaller = Path.Combine(installRoot, "Uninstall.exe");
        if (File.Exists(launcher) && !File.Exists(uninstaller))
            return "A previous uninstall is incomplete because MW3Launcher.exe is still present. Close every MechWarrior 3 launcher, remove that leftover launcher file, and retry. Saved pilots can remain in this folder.";
        return "The destination must be empty or contain only saved pilots preserved by this uninstaller. Choose a different folder so other existing files are never overwritten.";
    }

    private static bool IsAllowedTree(string installRoot, string directory)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(directory))
        {
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) return false;
            string relative = path.Substring(installRoot.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
            bool isDirectory = (attributes & FileAttributes.Directory) != 0;
            if (!IsAllowedSavePath(relative, isDirectory)) return false;
            if (isDirectory && !IsAllowedTree(installRoot, path)) return false;
        }
        return true;
    }

    internal static void CopyPreservedSaves(string sourceRoot, string destinationRoot)
    {
        foreach (string gameDirectory in GameDirectories)
        {
            string source = Path.Combine(sourceRoot, gameDirectory, SaveDirectory);
            if (!Directory.Exists(source)) continue;
            EnsureOrdinaryDirectory(source);
            string destination = Path.Combine(destinationRoot, gameDirectory, SaveDirectory);
            CopyTreeVerified(source, destination);
        }
    }

    internal static bool HasPreservedSaves(string installRoot)
    {
        foreach (string gameDirectory in GameDirectories)
        {
            string saves = Path.Combine(installRoot, gameDirectory, SaveDirectory);
            if (Directory.Exists(saves) && HasAnyFile(saves)) return true;
        }
        return false;
    }

    internal static void RemoveInstallationPreservingSaves(string installRoot, string backupRoot)
    {
        if (String.IsNullOrWhiteSpace(installRoot) || String.IsNullOrWhiteSpace(backupRoot))
            throw new ArgumentException("Installation and save-backup paths are required.");
        string normalizedInstall = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar);
        string normalizedBackup = Path.GetFullPath(backupRoot).TrimEnd(Path.DirectorySeparatorChar);
        ValidateInstallationForRemoval(normalizedInstall);
        if (normalizedBackup.StartsWith(normalizedInstall + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The save backup must be outside the installation being removed.");

        if (Directory.Exists(normalizedBackup)) DeleteDirectoryWithRetry(normalizedBackup);
        Directory.CreateDirectory(normalizedBackup);
        CopyPreservedSaves(normalizedInstall, normalizedBackup);
        bool hasSaves = HasPreservedSaves(normalizedBackup);

        bool restored = false;
        try
        {
            DeleteDirectoryWithRetry(normalizedInstall);
            if (hasSaves)
            {
                Directory.CreateDirectory(normalizedInstall);
                CopyPreservedSaves(normalizedBackup, normalizedInstall);
            }
            restored = true;
        }
        catch
        {
            try
            {
                Directory.CreateDirectory(normalizedInstall);
                CopyPreservedSaves(normalizedBackup, normalizedInstall);
            }
            catch { }
            throw;
        }
        finally
        {
            // Never discard the backup unless its contents were copied back and
            // byte-verified. A failed backup path is included in the UI error.
            if (restored && Directory.Exists(normalizedBackup)) DeleteDirectoryWithRetry(normalizedBackup);
        }
    }

    internal static void RemoveInstallerStaging(string stagingRoot, string finalRoot)
    {
        string normalizedStaging = Path.GetFullPath(stagingRoot).TrimEnd(Path.DirectorySeparatorChar);
        string expectedStaging = Path.GetFullPath(finalRoot).TrimEnd(Path.DirectorySeparatorChar) + ".installing";
        if (!normalizedStaging.Equals(expectedStaging, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to remove an unexpected installer staging directory.");
        if (Directory.Exists(normalizedStaging)) DeleteDirectoryWithRetry(normalizedStaging);
    }

    internal static void RemovePreservedSaveRemainder(string installRoot)
    {
        if (!IsEmptyOrPreservedSavesOnly(installRoot))
            throw new InvalidOperationException("Refusing to replace a destination containing files other than preserved saved pilots.");
        if (Directory.Exists(installRoot)) DeleteDirectoryWithRetry(Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar));
    }

    internal static void ValidateInstallationForRemoval(string installRoot)
    {
        if (String.IsNullOrWhiteSpace(installRoot))
            throw new ArgumentException("An installation path is required.");
        string normalized = Path.GetFullPath(installRoot).TrimEnd(Path.DirectorySeparatorChar);
        string volumeRoot = Path.GetPathRoot(normalized);
        if (String.IsNullOrEmpty(normalized) || String.IsNullOrEmpty(volumeRoot) ||
            normalized.Equals(volumeRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to remove a filesystem root.");
        if (!Directory.Exists(normalized) || (File.GetAttributes(normalized) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("The removal target is missing or is a linked directory.");

        string[] markers = { "MW3Launcher.exe", "Uninstall.exe", "install.cfg", "PAYLOAD_MANIFEST.sha256" };
        foreach (string marker in markers)
            if (!File.Exists(Path.Combine(normalized, marker)))
                throw new InvalidOperationException("The removal target is not a complete MechWarrior 3 Remastered installation.");
        if (!Directory.Exists(Path.Combine(normalized, "MechWarrior 3")))
            throw new InvalidOperationException("The removal target does not contain the installed base game.");
    }

    private static bool IsAllowedSavePath(string relative, bool isDirectory)
    {
        foreach (string gameDirectory in GameDirectories)
        {
            if (relative.Equals(gameDirectory, StringComparison.OrdinalIgnoreCase)) return isDirectory;
            string saves = Path.Combine(gameDirectory, SaveDirectory);
            if (relative.Equals(saves, StringComparison.OrdinalIgnoreCase)) return isDirectory;
            if (relative.StartsWith(saves + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static bool HasAnyFile(string directory)
    {
        EnsureOrdinaryDirectory(directory);
        if (Directory.GetFiles(directory).Length != 0) return true;
        foreach (string child in Directory.GetDirectories(directory))
            if (HasAnyFile(child)) return true;
        return false;
    }

    private static void CopyTreeVerified(string source, string destination)
    {
        EnsureOrdinaryDirectory(source);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A save path is a link and cannot be preserved safely: " + file);
            string target = Path.Combine(destination, Path.GetFileName(file));
            File.Copy(file, target, true);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file));
            if (new FileInfo(file).Length != new FileInfo(target).Length || !HashesMatch(file, target))
                throw new IOException("A saved-pilot file could not be verified after copying: " + file);
        }
        foreach (string directory in Directory.GetDirectories(source))
        {
            EnsureOrdinaryDirectory(directory);
            CopyTreeVerified(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void EnsureOrdinaryDirectory(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("A save directory is a link and cannot be preserved safely: " + path);
    }

    private static bool HashesMatch(string first, string second)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream left = File.OpenRead(first))
        using (FileStream right = File.OpenRead(second))
        {
            byte[] leftHash = sha.ComputeHash(left);
            byte[] rightHash = sha.ComputeHash(right);
            if (leftHash.Length != rightHash.Length) return false;
            for (int i = 0; i < leftHash.Length; i++) if (leftHash[i] != rightHash[i]) return false;
            return true;
        }
    }

    private static void DeleteDirectoryWithRetry(string path)
    {
        if (!Directory.Exists(path)) return;
        PrepareTreeForDeletion(path);
        Exception last = null;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            try { Directory.Delete(path, true); return; }
            catch (IOException ex)
            {
                last = ex;
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException ex)
            {
                last = ex;
                Thread.Sleep(100);
            }
        }
        throw new IOException("The installation could not be completely removed. Preserved saves remain backed up for recovery. " +
            (last == null ? "" : last.Message), last);
    }

    private static void PrepareTreeForDeletion(string directory)
    {
        EnsureOrdinaryDirectory(directory);
        foreach (string child in Directory.GetDirectories(directory))
        {
            EnsureOrdinaryDirectory(child);
            PrepareTreeForDeletion(child);
            File.SetAttributes(child, FileAttributes.Normal);
        }
        foreach (string file in Directory.GetFiles(directory))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("An installed file is a link and cannot be removed safely: " + file);
            File.SetAttributes(file, FileAttributes.Normal);
        }
        File.SetAttributes(directory, FileAttributes.Normal);
    }
}
