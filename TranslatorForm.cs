using Microsoft.Win32;
using static QuickTranslator.NativeMethods;

namespace QuickTranslator;

/// <summary>The always-on-top translation window.</summary>
internal sealed class TranslatorForm : Form
{
    private readonly Settings _settings;
    private readonly TranslatorService _service;

    private readonly ComboBox _source = NewLanguageCombo(includeAuto: true);
    private readonly ComboBox _target = NewLanguageCombo(includeAuto: false);
    private readonly ComboBox _engine = NewEngineCombo();
    private readonly RoundedButton _swap = new() { Text = "⇄", Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0), Radius = 10 };
    private readonly TextBox _input = NewTextBox(readOnly: false);
    // RichTextBox (read-only) so the card background colour is honoured; scrollbars only appear when needed
    private readonly RichTextBox _output = new()
    {
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        ReadOnly = true,
        DetectUrls = false,
        ScrollBars = RichTextBoxScrollBars.Vertical,
        Font = new Font("Segoe UI", 12f),
    };
    private readonly Label _inputCaption = NewLabel("Text", bold: true);
    private readonly Label _status = NewLabel("Translation", bold: true);
    private readonly Label _count = NewLabel("0 / " + TranslatorService.MaxLength);
    private readonly Label _hint = NewLabel("Esc  hide    ·    Ctrl+Enter  translate    ·    Ctrl+Shift+C  copy result");
    private readonly RoundedButton _copy = new() { Text = "Copy", Size = new Size(86, 32), Margin = new Padding(0) };
    private readonly RoundedButton _clear = new() { Text = "Clear", Size = new Size(64, 28), Margin = new Padding(0) };
    private readonly System.Windows.Forms.Timer _debounce = new() { Interval = 500 };

    // controls recoloured by ApplyTheme
    private readonly List<Card> _cards = new();
    private readonly List<Control> _windowSurfaces = new();
    private readonly List<Control> _cardSurfaces = new();
    private readonly List<Label> _mutedLabels = new();

    private CancellationTokenSource? _cts;
    private string _lastDetected = "";
    private bool _loading = true;
    private bool _statusIsError;

    public TranslatorForm(Settings settings, TranslatorService service)
    {
        _settings = settings;
        _service = service;

        Text = "Quick Translator";
        Icon = IconFactory.AppIcon;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.SizableToolWindow;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        KeyPreview = true;
        MinimumSize = new Size(420, 400);
        ClientSize = _settings.WindowWidth >= 420 && _settings.WindowHeight >= 340
            ? new Size(_settings.WindowWidth, _settings.WindowHeight)
            : new Size(600, 540);

        BuildLayout();
        LoadSettingsIntoControls();
        WireEvents();
        ApplyTheme();

        _loading = false;
        _ = Handle; // create the window handle up-front so the keyboard hook can marshal calls onto the UI thread
    }

    protected override bool ShowWithoutActivation => false;

    // ----------------------------------------------------------------- layout

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(16, 14, 16, 12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _windowSurfaces.Add(root);

        // language bar
        var languages = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
        languages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        languages.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
        languages.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        languages.Controls.Add(_source, 0, 0);
        languages.Controls.Add(_swap, 1, 0);
        languages.Controls.Add(_target, 2, 0);
        _windowSurfaces.Add(languages);

        // input card: caption + clear / text / counter
        _clear.Anchor = AnchorStyles.Right;
        var inputCard = NewCard(
            Header(_inputCaption, _clear),
            _input,
            Footer(null, _count));

        // output card: status / text / copy
        _copy.Anchor = AnchorStyles.Right;
        var outputCard = NewCard(
            Header(_status, _engine),
            _output,
            Footer(null, _copy));

        // shortcut hint
        _hint.Anchor = AnchorStyles.None;
        _hint.Margin = new Padding(0, 2, 0, 0);
        _hint.Font = new Font("Segoe UI", 8.5f);

        root.Controls.Add(languages, 0, 0);
        root.Controls.Add(inputCard, 0, 1);
        root.Controls.Add(outputCard, 0, 2);
        root.Controls.Add(_hint, 0, 3);
        Controls.Add(root);

        _mutedLabels.AddRange(new[] { _count, _hint });
    }

    /// <summary>Rounded card with a header row, a body control that fills the rest, and a footer row.</summary>
    private Card NewCard(Control header, Control body, Control footer)
    {
        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(14, 10, 14, 10), Margin = new Padding(0, 0, 0, 10) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.Margin = new Padding(0, 6, 0, 6);
        grid.Controls.Add(header, 0, 0);
        grid.Controls.Add(body, 0, 1);
        grid.Controls.Add(footer, 0, 2);
        card.Controls.Add(grid);

        _cards.Add(card);
        _cardSurfaces.Add(grid);
        _cardSurfaces.Add(body);
        return card;
    }

    private TableLayoutPanel Header(Control left, Control? right) => TwoColumnRow(left, right);

    private TableLayoutPanel Footer(Control? left, Control? right) => TwoColumnRow(left, right);

    private TableLayoutPanel TwoColumnRow(Control? left, Control? right)
    {
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = new Padding(0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        if (left != null) { left.Anchor = AnchorStyles.Left; row.Controls.Add(left, 0, 0); }
        if (right != null) { right.Anchor = AnchorStyles.Right; row.Controls.Add(right, 1, 0); }
        _cardSurfaces.Add(row);
        return row;
    }

    private static Label NewLabel(string text, bool bold = false) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(0),
        Font = new Font("Segoe UI", bold ? 9.5f : 9f, bold ? FontStyle.Bold : FontStyle.Regular),
    };

    private static ComboBox NewLanguageCombo(bool includeAuto)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            DropDownHeight = 320,
            Margin = new Padding(0),
            Font = new Font("Segoe UI", 10.5f),
        };
        if (includeAuto) combo.Items.Add(Languages.AutoDetect);
        combo.Items.AddRange(Languages.Ordered.Cast<object>().ToArray());
        return combo;
    }

    private static ComboBox NewEngineCombo()
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            FormattingEnabled = true,
            Width = 170,
            Margin = new Padding(8, 0, 0, 0),
            Font = new Font("Segoe UI", 9f),
        };
        combo.Items.AddRange(Enum.GetValues<Engine>().Cast<object>().ToArray());
        combo.Format += (_, e) => { if (e.ListItem is Engine engine) e.Value = engine.Label(); };
        return combo;
    }

    private static TextBox NewTextBox(bool readOnly) => new()
    {
        Multiline = true,
        Dock = DockStyle.Fill,
        BorderStyle = BorderStyle.None,
        ScrollBars = ScrollBars.None,
        ReadOnly = readOnly,
        AcceptsReturn = true,
        Font = new Font("Segoe UI", 12f),
    };

    private void ApplyTheme()
    {
        var t = Theme.Current;
        BackColor = t.Window;
        ForeColor = t.Text;

        foreach (var c in _windowSurfaces) c.BackColor = t.Window;
        foreach (var card in _cards) { card.BackColor = t.Window; card.Fill = t.Card; card.BorderColor = t.Border; card.Invalidate(); }
        foreach (var c in _cardSurfaces) c.BackColor = t.Card;

        foreach (Control box in new Control[] { _input, _output }) { box.BackColor = t.Card; box.ForeColor = t.Text; }
        foreach (var combo in new[] { _source, _target, _engine }) { combo.BackColor = t.Card; combo.ForeColor = t.Text; }
        foreach (var label in new[] { _inputCaption, _count }) label.BackColor = t.Card;
        _status.BackColor = t.Card;
        _inputCaption.ForeColor = t.Text;
        _status.ForeColor = _statusIsError ? t.Error : t.Muted;
        foreach (var label in _mutedLabels) { label.ForeColor = t.Muted; }
        _hint.BackColor = t.Window;

        Style(_swap, t.Subtle, t.SubtleHover, t.Text, t.Window);
        Style(_clear, t.Subtle, t.SubtleHover, t.Muted, t.Card);
        Style(_copy, t.Accent, t.AccentHover, t.AccentText, t.Card);
        Invalidate(true);
    }

    private static void Style(RoundedButton button, Color normal, Color hover, Color text, Color background)
    {
        button.NormalColor = normal;
        button.HoverColor = hover;
        button.TextColor = text;
        button.BackColor = background;
        button.Invalidate();
    }

    private void LoadSettingsIntoControls()
    {
        Select(_source, _settings.Source);
        Select(_target, _settings.Target);
        _engine.SelectedItem = _settings.Engine;
    }

    private static void Select(ComboBox combo, string code)
    {
        var match = combo.Items.Cast<Language>().FirstOrDefault(l => l.Code == code);
        combo.SelectedItem = match ?? combo.Items[0];
    }

    private static string CodeOf(ComboBox combo) => ((Language)combo.SelectedItem!).Code;

    // ----------------------------------------------------------------- events

    private void WireEvents()
    {
        _source.SelectedIndexChanged += (_, _) => OnLanguageChanged();
        _target.SelectedIndexChanged += (_, _) => OnLanguageChanged();
        _engine.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _engine.SelectedItem is not Engine engine) return;
            _settings.Engine = engine;
            _settings.Save();
            _debounce.Stop();
            _ = TranslateAsync();
        };
        _swap.Click += (_, _) => SwapLanguages();
        _input.TextChanged += (_, _) =>
        {
            _count.Text = $"{_input.TextLength} / {TranslatorService.MaxLength}";
            UpdateInputScrollBar();
            _debounce.Stop();
            _debounce.Start();
        };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = TranslateAsync(); };
        _input.SizeChanged += (_, _) => UpdateInputScrollBar();
        ResizeEnd += (_, _) => // remember the size the user dragged the window to
        {
            if (WindowState != FormWindowState.Normal) return;
            _settings.WindowWidth = ClientSize.Width;
            _settings.WindowHeight = ClientSize.Height;
            _settings.Save();
        };
        _copy.Click += (_, _) => CopyTranslation();
        _clear.Click += (_, _) => { _input.Clear(); _input.Focus(); };

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // keep running in the background
                Hide();
            }
        };

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>Shows the input's scrollbar only when the text is taller than the box.</summary>
    private void UpdateInputScrollBar()
    {
        int textHeight = TextRenderer.MeasureText(_input.Text + "x", _input.Font,
            new Size(Math.Max(1, _input.ClientSize.Width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height;
        var wanted = textHeight > _input.ClientSize.Height ? ScrollBars.Vertical : ScrollBars.None;
        if (_input.ScrollBars != wanted) _input.ScrollBars = wanted;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && IsHandleCreated)
            BeginInvoke(ApplyTheme);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape:
                Hide();
                return true;
            case Keys.Control | Keys.Enter:
                _debounce.Stop();
                _ = TranslateAsync();
                return true;
            case Keys.Control | Keys.Shift | Keys.C:
                CopyTranslation();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnLanguageChanged()
    {
        if (_loading) return;
        _settings.Source = CodeOf(_source);
        _settings.Target = CodeOf(_target);
        _settings.Save();
        _debounce.Stop();
        _ = TranslateAsync();
    }

    private void SwapLanguages()
    {
        string oldSource = CodeOf(_source);
        string oldTarget = CodeOf(_target);
        string newTarget = oldSource != Languages.Auto ? oldSource
            : _lastDetected.Length > 0 && Languages.All.Any(l => l.Code == Languages.Normalize(_lastDetected)) ? Languages.Normalize(_lastDetected)
            : Languages.FallbackFor(oldTarget);

        // Move the translation into the input so the swap behaves like a "translate back".
        string translated = _output.Text;

        _loading = true;
        Select(_source, oldTarget);
        Select(_target, newTarget);
        _loading = false;

        _input.Text = translated;
        OnLanguageChanged();
    }

    private async void CopyTranslation()
    {
        if (_output.Text.Length == 0) return;
        try
        {
            Clipboard.SetText(_output.Text);
            _copy.Text = "Copied ✓";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _copy.Text = "Busy…";
        }

        await Task.Delay(1200);
        if (!IsDisposed) _copy.Text = "Copy";
    }

    // ----------------------------------------------------------------- public API

    /// <summary>Shows the window (near <paramref name="anchor"/> if it was hidden) and translates <paramref name="text"/>.</summary>
    public void ShowTranslation(string? text, Point anchor)
    {
        bool wasVisible = Visible;
        if (!wasVisible) PositionNear(anchor);

        // the engine may have been changed from the tray menu since the window was last shown
        if (_engine.SelectedItem is not Engine current || current != _settings.Engine)
        {
            _loading = true;
            _engine.SelectedItem = _settings.Engine;
            _loading = false;
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            _debounce.Stop();
            _input.Text = text; // raises TextChanged, which restarts the debounce timer...
            _debounce.Stop();   // ...so stop it and translate right away instead
            _ = TranslateAsync();
        }

        ActivateWindow();
        if (string.IsNullOrWhiteSpace(text)) _input.SelectAll();
        _input.Focus();
    }

    private void ActivateWindow()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        MakeTopMost(Handle);
        Activate();
        SetForegroundWindow(Handle);
    }

    private void PositionNear(Point anchor)
    {
        var area = Screen.FromPoint(anchor).WorkingArea;
        int x = Math.Min(Math.Max(anchor.X + 12, area.Left), area.Right - Width);
        int y = Math.Min(Math.Max(anchor.Y + 12, area.Top), area.Bottom - Height);
        Location = new Point(x, y);
    }

    // ----------------------------------------------------------------- translating

    private void SetStatus(string text, bool isError = false)
    {
        _statusIsError = isError;
        _status.Text = text;
        _status.ForeColor = isError ? Theme.Current.Error : Theme.Current.Muted;
    }

    private async Task TranslateAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        string text = _input.Text.Trim();
        if (text.Length == 0)
        {
            _output.Clear();
            SetStatus("Translation");
            return;
        }

        SetStatus("Translating…");
        string source = CodeOf(_source);
        string target = CodeOf(_target);
        string fallback = Languages.FallbackFor(target);

        try
        {
            var result = await _service.TranslateAsync(text, source, target, ct);

            // Auto mode: text that is already in the target language goes to the fallback language instead.
            if (source == Languages.Auto && result.DetectedLanguage.Length > 0
                && Languages.SameLanguage(result.DetectedLanguage, target)
                && !Languages.SameLanguage(fallback, target))
            {
                target = fallback;
                result = await _service.TranslateAsync(text, result.DetectedLanguage, target, ct);
            }

            ct.ThrowIfCancellationRequested();
            _lastDetected = result.DetectedLanguage;
            _output.Text = result.Text;

            string from = source == Languages.Auto && result.DetectedLanguage.Length > 0
                ? Languages.NameOf(result.DetectedLanguage) + " (detected)"
                : Languages.NameOf(source);
            SetStatus($"{from}  →  {Languages.NameOf(target)}   ·   {result.Provider}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // superseded by a newer request
        }
        catch (Exception ex)
        {
            _output.Clear();
            SetStatus("Error: " + (ex is TaskCanceledException ? "request timed out" : ex.Message), isError: true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _cts?.Cancel();
            _debounce.Dispose();
        }
        base.Dispose(disposing);
    }
}
