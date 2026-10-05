using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

internal static class InstallerMediaPolicySmoke
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length < 2 || args.Length > 3) throw new ArgumentException("Expected setup path, true/false Wine expectation and optional expected mounted CD root.");
            bool wine = Boolean.Parse(args[1]);
            Assembly setup = Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));
            Type media = setup.GetType("DiscMediaSession", true);
            if ((bool)media.GetProperty("IsWine").GetValue(null, null) != wine)
                throw new Exception("Wine runtime detection mismatch.");
            Type installer = setup.GetType("InstallerForm", true);
            BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            using (Form form = (Form)Activator.CreateInstance(installer))
            {
                string expected = wine ? "CD folder..." : "ISO...";
                Button primary = (Button)form.Controls["browseMw3"];
                if (primary.Text != expected) throw new Exception("Wrong primary media route.");
                if (TextRenderer.MeasureText(primary.Text, primary.Font).Width > primary.Width - 8)
                    throw new Exception("Primary media button label is clipped.");
                if (installer.GetField("levelSounds", flags) != null ||
                    installer.GetMethod("PerformInstall", flags).GetParameters().Length != 1)
                    throw new Exception("Sound adjustment still requires a choice.");
                if (wine && (form.Controls["browsePm"].Text != "ZIP..." ||
                    !form.Controls["browseMw3Label"].Text.Contains("mounted CD") ||
                    !form.Controls["wineMediaHint"].Text.Contains("ISO/BIN/CUE")))
                    throw new Exception("Wine disc instructions missing.");
                if (args.Length == 3 && !System.IO.Path.GetFullPath(((TextBox)installer.GetField("mw3Iso", flags).GetValue(form)).Text)
                    .Equals(System.IO.Path.GetFullPath(args[2]), StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Mapped MW3 CD was not selected automatically.");
            }
            Console.WriteLine("Packaged media policy passed in " + (wine ? "Wine" : "Windows") + "; runtime detection, CD picker policy, button geometry and default sound adjustment checked.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
