using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// Runs the exact packaged staging/finalization method in an isolated test environment.
// It deliberately does not qualify the interactive picker, completion dialog or gameplay.
internal static class PackagedFolderInstallSmoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        bool verifyOnly = args.Length == 4 && args[3] == "--verify";
        if ((!verifyOnly && args.Length != 3) || Environment.GetEnvironmentVariable("MW3_WINE_TEST") != "1")
        { Console.Error.WriteLine("Requires isolated MW3_WINE_TEST=1 lab and setup, disc-folder, destination arguments."); return 2; }
        try
        {
            string setup = Path.GetFullPath(args[0]);
            string disc = Path.GetFullPath(args[1]);
            string root = Path.GetFullPath(args[2]);
            if (!root.StartsWith("C:\\MW3Lab\\Wine", StringComparison.OrdinalIgnoreCase) || Directory.Exists(root + ".installing"))
                throw new InvalidOperationException("Refusing an existing or unowned test destination.");
            string expected = Environment.GetEnvironmentVariable("MW3_TEST_SETUP_SHA256");
            if (String.IsNullOrEmpty(expected) || !System.Text.RegularExpressions.Regex.IsMatch(expected, "^[A-Fa-f0-9]{64}$") || !Hash(setup).Equals(expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unqualified setup identity.");
            Assembly assembly = Assembly.LoadFile(setup);
            if (!verifyOnly)
            {
                MethodInfo allow = assembly.GetType("GameSaveStorage", true).GetMethod("IsEmptyOrPreservedSavesOnly", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (allow == null || !(bool)allow.Invoke(null, new object[] { root }))
                    throw new InvalidOperationException("Refusing a destination that is not empty or preserved saves only.");
            }
            Type media = assembly.GetType("DiscMediaSession", true);
            if (!(bool)media.GetMethod("IsAvailable").Invoke(null, new object[] { disc }))
                throw new InvalidOperationException("Disc folder rejected by packaged media owner.");
            Type type = assembly.GetType("InstallerForm", true);
            if (!verifyOnly) using (Form form = (Form)Activator.CreateInstance(type))
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                ((TextBox)type.GetField("mw3Iso", flags).GetValue(form)).Text = disc;
                string pmMedia = Environment.GetEnvironmentVariable("MW3_WINE_PM_MEDIA");
                if (!String.IsNullOrEmpty(pmMedia))
                {
                    ((CheckBox)type.GetField("installPm", flags).GetValue(form)).Checked = true;
                    ((TextBox)type.GetField("pmMedia", flags).GetValue(form)).Text = pmMedia;
                }
                form.Show(); Application.DoEvents();
                MethodInfo install = type.GetMethod("PerformInstall", flags);
                Task task = Task.Factory.StartNew(delegate { install.Invoke(form, new object[] { root }); });
                Stopwatch timer = Stopwatch.StartNew();
                while (!task.IsCompleted && timer.Elapsed.TotalMinutes < 5)
                { Application.DoEvents(); Thread.Sleep(25); }
                if (!task.IsCompleted) throw new TimeoutException("Packaged install exceeded five minutes; no success recorded.");
                task.GetAwaiter().GetResult();
            }
            string game = Path.Combine(root, "MechWarrior 3");
            if (Hash(Path.Combine(game, "zbd", "soundsH.zbd")) != "20AD72D51EAFFB4447A7CE09B408B017CFAA5A7034A82E73B85B539355579BCB" ||
                Hash(Path.Combine(game, "zbd", "soundsL.zbd")) != "612F8EDB1E26884AAD04E34F3E73D41D7661F8652F97EB765C8C28EAC9A37D98" ||
                Hash(Path.Combine(game, "OriginalSoundArchives", "soundsH.zbd")) != "71C4688E38D59E03D3E0A63C8EF90CCE0359103890904AAAB5DC461647F484A4" ||
                Hash(Path.Combine(game, "OriginalSoundArchives", "soundsL.zbd")) != "E259704B36069339BAD035AE571F7733A6655ED2020C4D7E24B5C8872A3B09C3")
                throw new InvalidOperationException("Default sound adjustment or original backups mismatch.");
            if (Directory.Exists(root + ".installing")) throw new InvalidOperationException("Staging remains.");
            foreach (string name in new[] { "MW3Launcher.exe", "Uninstall.exe", "PAYLOAD_MANIFEST.sha256", "install.cfg", "THIRD_PARTY_NOTICES.md" })
                if (!File.Exists(Path.Combine(root, name))) throw new FileNotFoundException("Committed installation missing " + name);
            if (Hash(Path.Combine(game, "Mech3fixup.exe")) != "4721B34E69EFF60BE03E8433D3DCCD169D301D761C2766460F3E61EF7A69C5D9") throw new InvalidOperationException("Patched game identity mismatch.");
            if (Hash(Path.Combine(game, "ddraw.dll")) != "FD11B9B6B8A8CC23744DFEDC10E23798860F3A96BD8B2B4C75DD2FDB3BE8F8FB") throw new InvalidOperationException("Renderer identity mismatch.");
            string pm = Path.Combine(root, "Pirates Moon");
            if (Directory.Exists(pm) && (Hash(Path.Combine(pm, "Mech3fixup.exe")) != "898C9507100DDE015B8EDA825BB4E72E139874190C986CDEC416A7C3FD6D8F5F" ||
                Hash(Path.Combine(pm, "ddraw.dll")) != "FD11B9B6B8A8CC23744DFEDC10E23798860F3A96BD8B2B4C75DD2FDB3BE8F8FB"))
                throw new InvalidOperationException("Expansion game/renderer identity mismatch.");
            Type links = assembly.GetType("InstalledShellLink", true);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            MethodInfo readLink = links.GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static);
            if (readLink == null) throw new MissingMethodException("Packaged typed shortcut read contract missing.");
            string[] link = (string[])readLink.Invoke(null, new object[] { Path.Combine(desktop, "MechWarrior 3 Remastered.lnk") });
            if (!link[0].Equals(Path.Combine(root, "MW3Launcher.exe"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Final shortcut target mismatch.");
            Console.WriteLine((verifyOnly ? "Committed-tree verification" : "Exact packaged Folder staging/finalization smoke") + " passed; game/renderer hashes, committed files, shortcut target and staging cleanup checked. Gameplay not qualified.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static string Hash(string path)
    { using (SHA256 sha = SHA256.Create()) using (Stream file = File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", ""); }
}
