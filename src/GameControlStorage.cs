using System;
using System.IO;

internal static class GameControlStorage
{
    internal static void EnsureWritable(string gameRoot)
    {
        string keys = Path.Combine(gameRoot, "keys");
        string probe = Path.Combine(keys, ".mw3-remastered-write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            Directory.CreateDirectory(keys);
            File.WriteAllText(probe, "control profile write test");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "MechWarrior 3 cannot save remapped controls because its keys folder is not writable. " +
                "Reinstall to the default per-user location, then try again.", ex);
        }
        finally
        {
            try { if (File.Exists(probe)) File.Delete(probe); } catch { }
        }
    }
}
