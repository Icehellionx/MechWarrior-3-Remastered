using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

internal sealed class LauncherHelpForm : Form
{
    internal LauncherHelpForm()
    {
        Text = "Help & controls";
        ClientSize = new Size(680, 520);
        MinimumSize = new Size(540, 380);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10F);
        TextBox content = new TextBox {
            Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, AccessibleName = "Game controls, display and troubleshooting", Text = HelpText
        };
        FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        Button close = new Button { Text = "CLOSE", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        Button calibrate = new Button { Text = "GAME CONTROLLERS", Width = 190, Height = 32 };
        calibrate.Click += delegate {
            try { Process.Start(new ProcessStartInfo("joy.cpl") { UseShellExecute = true }); }
            catch { MessageBox.Show(this, "Could not open Game Controllers. Use your operating system's controller settings to verify axes and buttons.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        footer.Controls.Add(close);
        footer.Controls.Add(calibrate);
        Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
        body.Controls.Add(content);
        Controls.Add(body);
        Controls.Add(footer);
        CancelButton = close;
        Shown += delegate { content.Select(0, 0); ActiveControl = close; };
    }

    internal static readonly string HelpText =
        "REMAP AND SAVE\r\nLaunch a game and open Options > Controls. Choose Mouse/Keybd Default or Joystick Default, change bindings, then select Save. MechWarrior 3 and Pirate's Moon keep separate controls.\r\n\r\n" +
        "JOYSTICKS AND HOTAS\r\nConnect the intended device before launch. Verify axes and buttons in Game Controllers, then select and bind it in-game. Clear unused Joystick Z or Joystick Rz bindings if throttle or torso twist moves on its own. The original engine has POV-hat and key-binding limitations; see the game's manual and readme.\r\n\r\n" +
        "Combining stick, throttle and pedals may require a user-configured virtual device. Controller software can map extra buttons to keyboard keys; keyboard emulation does not supply native analog axes. The launcher installs no virtual-device drivers and does not combine devices. Physical HOTAS, multiple devices and force feedback remain unverified. MW4's button limit and joystick switch are not established MW3 behavior.\r\n\r\n" +
        "DISPLAY\r\nThe centered 4:3 picture preserves original framing on widescreen and ultrawide monitors. Side bars are expected. Presentation scales to the desktop; startup movies still require 640x480 and the qualified gameplay mode is 1024x768. Settings previews do not change Windows resolution or add horizontal world view.\r\n\r\n" +
        "SAVES\r\nEach game stores saved pilots and campaign progress in its pilots folder. Uninstall preserves those saves for reinstall, but removes other game files, settings and diagnostics. Back up custom controls and other personal changes before uninstall.\r\n\r\n" +
        "REPAIR AND REPORT\r\nMissing or quarantined compatibility files require investigation and a fresh trusted installation. Keep Windows security enabled. Diagnostics provides a shareable summary; review raw logs separately before sharing. Include the version, game, operating system, GPU/driver, stage reached and steps to reproduce.\r\n\r\n" +
        "WINE / LINUX (EXPERIMENTAL)\r\nMount your legally owned disc using Linux tools and expose its readable root as a CD-ROM drive in the same Wine prefix. Choose Folder in Setup and keep the mount available for base-game play. The application leaves your mount alone. A full Linux installation/gameplay matrix and Proton/Steam Deck support remain unqualified.\r\n\r\n" +
        "AUDIO\r\nCD music and game sound effects have separate compatibility paths. The optional experimental sound-level adjustment backs up original sound archives; the installed Use-SoundLevelCandidate.ps1 script can restore them. In-game listening remains a qualification gap.";
}
