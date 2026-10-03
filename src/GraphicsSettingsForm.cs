using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

internal sealed class GraphicsSettingsForm : Form
{
    private readonly GraphicsProfileService service;
    private readonly ComboBox game = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly ComboBox aa = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly ComboBox monitor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly Button apply = new Button { Text = "APPLY", Width = 100, Height = 32, Enabled = false };
    private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(560, 0) };
    private readonly PictureBox preview = new PictureBox { Width = 560, Height = 150, BackColor = Color.FromArgb(24, 24, 27) };
    private Size currentMonitor;
    private GraphicsProfile loaded;
    private int generation;

    internal GraphicsSettingsForm(string root)
    {
        service = new GraphicsProfileService(root);
        Text = "Graphics settings";
        ClientSize = new Size(610, 500);
        MinimumSize = new Size(610, 500);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 10F);
        currentMonitor = Screen.PrimaryScreen.Bounds.Size;
        FlowLayoutPanel body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(18) };
        game.Items.AddRange(new object[] { "MechWarrior 3", "Pirate's Moon" }); game.SelectedIndex = 0;
        aa.Items.AddRange(new object[] { "4x MSAA (qualified default)", "Off (recovery choice)" });
        monitor.Items.AddRange(new object[] { "Current monitor", "16:9", "16:10", "21:9", "32:9" }); monitor.SelectedIndex = 0;
        body.Controls.Add(new Label { Text = "Game", AutoSize = true }); body.Controls.Add(game);
        body.Controls.Add(new Label { Text = "Antialiasing", AutoSize = true }); body.Controls.Add(aa);
        body.Controls.Add(new Label { Text = "Scaling: bilinear   |   Texture filtering: 16x anisotropic\r\nAdditional filtering choices await engine qualification.", AutoSize = true });
        body.Controls.Add(new Label { Text = "Display preview", AutoSize = true }); body.Controls.Add(monitor); body.Controls.Add(preview);
        body.Controls.Add(new Label { Text = "Centered 4:3 framing; previews do not change display resolution.\r\nStartup movies, gameplay modes and expansion timing are preserved.", AutoSize = true });
        body.Controls.Add(status);
        FlowLayoutPanel footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
        Button close = new Button { Text = "CANCEL", Width = 100, Height = 32, DialogResult = DialogResult.Cancel };
        close.Click += delegate { Close(); };
        footer.Controls.Add(close); footer.Controls.Add(apply);
        Controls.Add(body); Controls.Add(footer); CancelButton = close;
        game.SelectedIndexChanged += async delegate { await LoadAsync(); };
        monitor.SelectedIndexChanged += delegate { preview.Invalidate(); };
        preview.Paint += PaintPreview;
        Shown += async delegate { currentMonitor = Screen.FromControl(Owner ?? this).Bounds.Size; preview.Invalidate(); await LoadAsync(); };
        apply.Click += async delegate { await ApplyAsync(); };
    }

    private async Task LoadAsync()
    {
        int token = ++generation;
        bool pm = game.SelectedIndex == 1;
        loaded = null; apply.Enabled = false; aa.Enabled = false; status.Text = "Reading installed profile...";
        try
        {
            GraphicsProfile profile = await Task.Run(delegate { return service.Load(pm); });
            if (IsDisposed || token != generation) return;
            loaded = profile; aa.SelectedIndex = profile.Values["Antialiasing"] == "off" ? 1 : 0;
            aa.Enabled = true; apply.Enabled = true; status.Text = "Changes apply to this game on the next launch.";
        }
        catch (Exception ex) { if (!IsDisposed && token == generation) status.Text = ex.Message; }
    }

    private async Task ApplyAsync()
    {
        bool pm = game.SelectedIndex == 1;
        GraphicsProfile snapshot = loaded;
        string choice = aa.SelectedIndex == 1 ? "off" : "msaa4x(0)";
        game.Enabled = false; apply.Enabled = false; aa.Enabled = false;
        try
        {
            await Task.Run(delegate { service.Apply(pm, snapshot, choice); });
            if (IsDisposed) return;
            await LoadAsync();
            if (!IsDisposed && loaded != null) status.Text = "Settings saved for the next launch.";
        }
        catch (Exception ex) { if (!IsDisposed) { status.Text = ex.Message; apply.Enabled = false; } }
        finally { if (!IsDisposed) { game.Enabled = true; aa.Enabled = loaded != null; } }
    }

    private void PaintPreview(object sender, PaintEventArgs e)
    {
        double ratio = monitor.SelectedIndex == 0 ? (double)currentMonitor.Width / Math.Max(1, currentMonitor.Height) :
            monitor.SelectedIndex == 1 ? 16.0 / 9 : monitor.SelectedIndex == 2 ? 16.0 / 10 : monitor.SelectedIndex == 3 ? 21.0 / 9 : 32.0 / 9;
        int height = Math.Min(preview.Height - 12, (int)((preview.Width - 12) / ratio));
        int width = (int)(height * ratio);
        Rectangle display = new Rectangle((preview.Width - width) / 2, (preview.Height - height) / 2, width, height);
        e.Graphics.FillRectangle(Brushes.Black, display);
        Size picture = FitPicture(display.Size);
        Rectangle frame = new Rectangle(display.X + (display.Width - picture.Width) / 2, display.Y + (display.Height - picture.Height) / 2, picture.Width, picture.Height);
        e.Graphics.FillRectangle(Brushes.SlateGray, frame);
        TextRenderer.DrawText(e.Graphics, "Original 4:3 picture", Font, frame, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    internal static Size FitPicture(Size display)
    {
        int width = Math.Min(display.Width, display.Height * 4 / 3);
        return new Size(width, width * 3 / 4);
    }
}
