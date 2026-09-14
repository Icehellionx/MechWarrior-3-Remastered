using System;
using System.IO;

internal static class GameControlStorageSmoke
{
    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "mw3-control-storage-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            GameControlStorage.EnsureWritable(root);
            string keys = Path.Combine(root, "keys");
            if (!Directory.Exists(keys)) throw new InvalidOperationException("Control-profile directory was not created.");
            if (Directory.GetFiles(keys, ".mw3-remastered-write-test-*.tmp").Length != 0)
                throw new InvalidOperationException("Control-profile write probe was not cleaned up.");

            string profile = Path.Combine(keys, "player.zrd");
            File.WriteAllText(profile, "player controls");
            GameControlStorage.EnsureWritable(root);
            if (File.ReadAllText(profile) != "player controls")
                throw new InvalidOperationException("Existing control profile was modified.");

            Console.WriteLine("Control-profile storage tests passed.");
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
}
