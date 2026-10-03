using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

internal sealed class DiagnosticsForm : Form
{
    private readonly TextBox report = new TextBox { Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AccessibleName = "Shareable diagnostics report", Text = "Reading installation..." };
    private readonly Button copy = new Button { Text = "COPY REPORT", Width = 140, Height = 32, Enabled = false };
    internal DiagnosticsForm()
    {
        Text = "Diagnostics"; ClientSize = new Size(720, 540); MinimumSize = new Size(560, 380);
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10F);
        FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        Button close = new Button { Text = "CLOSE", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        close.Click += delegate { Close(); };
        Button logs = new Button { Text = "OPEN RAW LOGS", Width = 150, Height = 32 };
        copy.Click += delegate {
            if (!TryCopyReport(Clipboard.SetText)) MessageBox.Show(this, "Could not copy the report. Select the text and copy it manually.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        logs.Click += delegate {
            try
            {
                if (!Directory.Exists(InstallationDiagnostics.DirectoryPath)) throw new DirectoryNotFoundException();
                Process.Start(new ProcessStartInfo(InstallationDiagnostics.DirectoryPath) { UseShellExecute = true });
            }
            catch { MessageBox.Show(this, "The log folder is unavailable. Logs appear after a launcher run. Review raw logs for personal paths before sharing.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        };
        footer.Controls.Add(close); footer.Controls.Add(copy); footer.Controls.Add(logs);
        Controls.Add(report); Controls.Add(footer); CancelButton = close;
        Shown += delegate { ActiveControl = close; };
    }
    internal void SetReport(string text) { report.Text = text; copy.Enabled = true; report.Select(0, 0); }
    internal bool TryCopyReport(Action<string> copyText)
    {
        try { copyText(report.Text); return true; }
        catch { return false; }
    }
}
