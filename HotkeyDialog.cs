namespace QuickTranslator;

/// <summary>Small dialog that records the next key combination the user presses.</summary>
internal sealed class HotkeyDialog : Form
{
    private readonly Label _display = new()
    {
        Text = "Press your shortcut…",
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 16f, FontStyle.Bold),
    };

    private readonly Label _hint = new()
    {
        Text = "Use Ctrl, Alt or Shift together with a key (e.g. Ctrl+Shift+Q).",
        AutoSize = false,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 9f),
    };

    private readonly Button _ok = new() { Text = "Save", DialogResult = DialogResult.OK, Enabled = false, Width = 90, Height = 32 };
    private readonly Button _cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 32 };

    private Hotkey? _captured;

    public Hotkey? Result => DialogResult == DialogResult.OK ? _captured : null;

    public HotkeyDialog(Hotkey? current)
    {
        Text = "Custom shortcut";
        Icon = IconFactory.AppIcon;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 190);

        var theme = Theme.Current;
        BackColor = theme.Window;
        ForeColor = theme.Text;
        _hint.ForeColor = theme.Muted;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 0, 0) };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_ok);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        grid.Controls.Add(_display, 0, 0);
        grid.Controls.Add(_hint, 0, 1);
        grid.Controls.Add(buttons, 0, 2);
        Controls.Add(grid);

        CancelButton = _cancel;

        if (current is { IsValid: true })
        {
            _captured = current;
            _display.Text = current.ToString();
            _ok.Enabled = true;
        }
    }

    /// <summary>Captures every key (including Tab/Enter/Alt combos) before normal dialog handling.</summary>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) return base.ProcessCmdKey(ref msg, keyData); // closes via CancelButton

        var key = keyData & Keys.KeyCode;
        var candidate = new Hotkey
        {
            Ctrl = keyData.HasFlag(Keys.Control),
            Alt = keyData.HasFlag(Keys.Alt),
            Shift = keyData.HasFlag(Keys.Shift),
            Key = Hotkey.IsModifierKey((int)key) ? 0 : (int)key,
        };

        if (candidate.Key == 0)
        {
            // only modifiers so far: live preview
            _display.Text = candidate.ToString() is { Length: > 0 } preview ? preview + "+…" : "Press your shortcut…";
            return true;
        }

        _display.Text = candidate.ToString();
        if (candidate.IsValid)
        {
            _captured = candidate;
            _ok.Enabled = true;
            _hint.Text = "Click Save to use this shortcut.";
        }
        else
        {
            _captured = null;
            _ok.Enabled = false;
            _hint.Text = "Add Ctrl, Alt or Shift to the key.";
        }
        return true;
    }
}
