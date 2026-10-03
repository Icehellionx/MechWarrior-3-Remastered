using System;
using System.IO;

internal static class LauncherShortcutPolicySmoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        string root = Path.Combine(Path.GetTempPath(), "MW3ShortcutSmoke-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert(LauncherShortcutPolicy.IsOwnedLegacyTarget("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
                "-File \"C:\\Games\\Mechwarrior 3 Remaster\\scripts\\Launch-MechWarrior3-Remaster.ps1\""), "Known legacy MW3 shortcut was not recognized.");
            Assert(LauncherShortcutPolicy.IsOwnedLegacyTarget("C:\\Program Files\\PowerShell\\7\\pwsh.exe",
                "-File \"C:\\Games\\Mechwarrior 3 Modern\\scripts\\Launch-PiratesMoon.ps1\""), "Known legacy Pirate's Moon shortcut was not recognized.");
            Assert(!LauncherShortcutPolicy.IsOwnedLegacyTarget("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe",
                "-File \"C:\\Personal\\launch.ps1\""), "Unrelated PowerShell shortcut was claimed.");

            Directory.CreateDirectory(root);
            string launcher = Path.Combine(root, "MW3Launcher.exe");
            File.WriteAllText(launcher, "fixture");
            string shortcut = Path.Combine(root, "MechWarrior 3 Remastered.lnk");
            LauncherShortcutPolicy.CreatePrimaryShortcut(shortcut, root);
            string[] typed = InstalledShellLink.Read(shortcut);
            Assert(typed[0].Equals(launcher, StringComparison.OrdinalIgnoreCase), "Typed shortcut target did not round-trip.");
            Assert(typed[1] == String.Empty, "Typed shortcut arguments did not round-trip.");
            Assert(typed[2].TrimEnd('\\').Equals(root, StringComparison.OrdinalIgnoreCase), "Typed working directory did not round-trip.");
            Assert(typed[3].Equals(launcher + ",0", StringComparison.OrdinalIgnoreCase), "Typed branded icon did not round-trip.");
            string manual = Path.Combine(root, "manual with spaces.pdf");
            File.WriteAllText(manual, "fixture");
            string manualLink = Path.Combine(root, "Manual.lnk");
            InstalledShellLink.Create(manualLink, manual, root, "Original manual", null);
            Assert(InstalledShellLink.Read(manualLink)[0].Equals(manual, StringComparison.OrdinalIgnoreCase), "Manual shortcut did not round-trip.");
            bool failed = false;
            try { InstalledShellLink.Create(Path.Combine(root, "absent", "failure.lnk"), manual, root, "Failure fixture", null); }
            catch { failed = true; }
            Assert(failed, "Shortcut persistence failure was hidden.");
            // Setup finalization executes on Task.Run's MTA worker, not the UI STA.
            System.Threading.Tasks.Task.Run(delegate {
                string workerLink = Path.Combine(root, "Worker.lnk");
                LauncherShortcutPolicy.CreatePrimaryShortcut(workerLink, root);
                Assert(InstalledShellLink.Read(workerLink)[0].Equals(launcher, StringComparison.OrdinalIgnoreCase), "Installer-worker shortcut did not round-trip.");
            }).GetAwaiter().GetResult();
            if (args.Length == 1 && args[0] == "--typed-only")
            {
                Console.WriteLine("Typed Shell Link persistence, STA/MTA round-trip and failure contracts passed; independent WScript reader not run.");
                return 0;
            }
            // Independently inspect the produced links through Windows scripting automation.
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(type);
            dynamic link = shell.CreateShortcut(shortcut);
            Assert(Path.GetFullPath((string)link.TargetPath).Equals(Path.GetFullPath(launcher), StringComparison.OrdinalIgnoreCase), "Primary shortcut target is not MW3Launcher.exe.");
            Assert(String.IsNullOrEmpty((string)link.Arguments), "Primary shortcut unexpectedly bypasses the launcher menu.");
            Assert(Path.GetFullPath((string)link.WorkingDirectory).Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase), "Primary shortcut working directory is wrong.");
            Assert(((string)link.IconLocation).Equals(launcher + ",0", StringComparison.OrdinalIgnoreCase), "Primary shortcut does not use the branded launcher icon.");
            Console.WriteLine("Launcher shortcut policy passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if (Directory.Exists(root)) try { Directory.Delete(root, true); } catch { } }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
