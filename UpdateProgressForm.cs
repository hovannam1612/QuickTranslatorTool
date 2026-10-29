namespace QuickTranslator;

/// <summary>Tiny "Downloading update…" window with a progress bar.</summary>
internal sealed class UpdateProgressForm : Form
{
    private readonly Label _label = new() { Text = "Downloading update…", AutoSize = true, Location = new Point(16, 16) };
    private readonly ProgressBar _bar = new() { Location = new Point(16, 46), Size = new Size(328, 20), Maximum = 100 };

    public UpdateProgressForm()
    {
        Text = "Quick Translator update";
        Icon = IconFactory.AppIcon;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 84);
        BackColor = Theme.Current.Window;
        ForeColor = Theme.Current.Text;
        Controls.Add(_label);
        Controls.Add(_bar);
    }

    public void SetPercent(int percent)
    {
        _bar.Value = Math.Clamp(percent, 0, 100);
        _label.Text = $"Downloading update… {_bar.Value}%";
    }
}
