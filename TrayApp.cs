using Microsoft.Win32;

namespace QuickTranslator;

/// <summary>Owns the tray icon, the global hotkey and the translation window; keeps the app alive in the background.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "QuickTranslator";

    private readonly Settings _settings = Settings.Load();
    private readonly TranslatorForm _form;
    private readonly KeyboardHook _hook;
    private readonly NotifyIcon _tray;
    private readonly RegisteredWaitHandle _showRequestWait;
    private bool _grabbing;

    private readonly UpdateService _updates = new();
    private ToolStripMenuItem? _updateItem;
    private UpdateInfo? _available;
    private bool _updating;
    private bool _balloonIsUpdate; // the balloon currently shown announces an update, so clicking it starts the update

    public TrayApp(EventWaitHandle showRequest)
    {
        _form = new TranslatorForm(_settings, new TranslatorService(_settings));

        _hook = new KeyboardHook(_form) { Mode = _settings.Trigger, CustomHotkey = _settings.CustomHotkey };
        _hook.Triggered += () => _ = OnHotkeyAsync();
        _hook.Start();

        _tray = new NotifyIcon
        {
            Icon = IconFactory.AppIcon,
            Text = "Quick Translator",
            Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) _form.ShowTranslation(null, Cursor.Position);
        };

        // A second launch of the exe signals this instance instead of starting another one.
        _showRequestWait = ThreadPool.RegisterWaitForSingleObject(
            showRequest,
            (_, _) => _form.BeginInvoke(() => _form.ShowTranslation(null, Cursor.Position)),
            null, Timeout.Infinite, false);

        _tray.BalloonTipClicked += (_, _) =>
        {
            if (_balloonIsUpdate && _available != null) _ = InstallUpdateAsync(_available);
        };

        _tray.ShowBalloonTip(3000, "Quick Translator is running",
            "Select text anywhere and press " + TriggerLabel(_settings.Trigger) + " to translate.", ToolTipIcon.Info);

        _ = CheckForUpdatesAsync(manual: false, startupDelay: TimeSpan.FromSeconds(15));
    }

    // ----------------------------------------------------------------- updates

    private async Task CheckForUpdatesAsync(bool manual, TimeSpan startupDelay = default)
    {
        if (_updating) return;
        if (!_updates.IsConfigured)
        {
            if (manual) Notify("Updates are not configured for this build (no GitHub repository set).", MessageBoxIcon.Information);
            return;
        }

        try
        {
            if (startupDelay > TimeSpan.Zero) await Task.Delay(startupDelay);
            _available = await _updates.CheckAsync(CancellationToken.None);
            RefreshUpdateItem();

            if (_available == null)
            {
                if (manual) Notify($"You're up to date (version {AppInfo.VersionText}).", MessageBoxIcon.Information);
            }
            else if (manual)
            {
                await InstallUpdateAsync(_available);
            }
            else
            {
                _balloonIsUpdate = true;
                _tray.ShowBalloonTip(8000, "Update available",
                    $"Quick Translator {_available.Version.ToString(3)} is available. Click here to update.", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            if (manual) Notify("Could not check for updates:\n" + ex.Message, MessageBoxIcon.Warning); // silent on the automatic check
        }
    }

    private async Task InstallUpdateAsync(UpdateInfo update)
    {
        if (_updating) return;

        string header = $"Quick Translator {update.Version.ToString(3)} is available (you have {AppInfo.VersionText}).";
        string notes = update.Notes.Length > 600 ? update.Notes[..600] + "…" : update.Notes;

        if (!AppInfo.IsInstalled)
        {
            // portable/dev copy: there is no installation to upgrade in place, so send the user to the download page
            if (Ask(header + "\n\nThis copy was not installed with Setup. Open the download page?") == DialogResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(update.PageUrl) { UseShellExecute = true });
            return;
        }

        string question = header + (notes.Length > 0 ? "\n\n" + notes : "") + "\n\nUpdate now? The app will restart.";
        if (Ask(question) != DialogResult.Yes) return;

        _updating = true;
        using var progressForm = new UpdateProgressForm();
        progressForm.Show();
        try
        {
            string installer = await _updates.DownloadAsync(update, new Progress<int>(progressForm.SetPercent), CancellationToken.None);
            progressForm.Close();
            UpdateService.RunInstaller(installer);
            ExitThread(); // the installer takes over, upgrades in place and starts the new version
        }
        catch (Exception ex)
        {
            progressForm.Close();
            Notify("The update failed:\n" + ex.Message, MessageBoxIcon.Error);
        }
        finally
        {
            _updating = false;
        }
    }

    private void RefreshUpdateItem()
    {
        if (_updateItem == null) return;
        _updateItem.Text = _available == null ? "Check for updates…" : $"Update to version {_available.Version.ToString(3)}…";
    }

    private static DialogResult Ask(string text) => MessageBox.Show(new Form { TopMost = true }, text, "Quick Translator update",
        MessageBoxButtons.YesNo, MessageBoxIcon.Question);

    private static void Notify(string text, MessageBoxIcon icon) => MessageBox.Show(new Form { TopMost = true }, text, "Quick Translator",
        MessageBoxButtons.OK, icon);

    private async Task OnHotkeyAsync()
    {
        if (_grabbing) return;
        _grabbing = true;
        try
        {
            var cursor = Cursor.Position;
            string text = await SelectionGrabber.GrabAsync();
            _form.ShowTranslation(text, cursor);
        }
        finally
        {
            _grabbing = false;
        }
    }

    // ----------------------------------------------------------------- tray menu

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Quick Translator v" + AppInfo.VersionText) { Enabled = false });
        menu.Items.Add("Open", null, (_, _) => _form.ShowTranslation(null, Cursor.Position));
        menu.Items.Add(new ToolStripSeparator());

        var hotkeyMenu = new ToolStripMenuItem("Shortcut");
        void SelectMode(TriggerMode mode)
        {
            _settings.Trigger = mode;
            _hook.Mode = mode;
            _settings.Save();
            foreach (ToolStripMenuItem other in hotkeyMenu.DropDownItems) other.Checked = (TriggerMode)other.Tag! == mode;
        }

        foreach (TriggerMode mode in Enum.GetValues<TriggerMode>().Where(m => m != TriggerMode.Custom))
        {
            var item = new ToolStripMenuItem(TriggerLabel(mode)) { Checked = mode == _settings.Trigger, Tag = mode };
            item.Click += (_, _) => SelectMode(mode);
            hotkeyMenu.DropDownItems.Add(item);
        }

        var custom = new ToolStripMenuItem(CustomLabel()) { Checked = _settings.Trigger == TriggerMode.Custom, Tag = TriggerMode.Custom };
        custom.Click += (_, _) =>
        {
            // pause the shortcut so pressing the current one does not trigger a translation while recording
            _hook.Enabled = false;
            using var dialog = new HotkeyDialog(_settings.CustomHotkey);
            var result = dialog.ShowDialog() == DialogResult.OK ? dialog.Result : null;
            _hook.Enabled = true;
            if (result == null) return;

            _settings.CustomHotkey = result;
            _hook.CustomHotkey = result;
            custom.Text = CustomLabel();
            SelectMode(TriggerMode.Custom);
        };
        hotkeyMenu.DropDownItems.Add(custom);
        menu.Items.Add(hotkeyMenu);

        var engineMenu = new ToolStripMenuItem("Translation engine");
        foreach (Engine engine in Enum.GetValues<Engine>())
        {
            var item = new ToolStripMenuItem(engine.Label()) { Checked = engine == _settings.Engine, Tag = engine };
            item.Click += (_, _) =>
            {
                _settings.Engine = engine;
                _settings.Save();
            };
            engineMenu.DropDownItems.Add(item);
        }
        // the engine can also be changed from the translator window, so refresh the ticks whenever the menu opens
        engineMenu.DropDownOpening += (_, _) =>
        {
            foreach (ToolStripMenuItem item in engineMenu.DropDownItems) item.Checked = (Engine)item.Tag! == _settings.Engine;
        };
        menu.Items.Add(engineMenu);

        var autostart = new ToolStripMenuItem("Start with Windows") { Checked = IsAutostartEnabled(), CheckOnClick = true };
        autostart.CheckedChanged += (_, _) => SetAutostart(autostart.Checked);
        menu.Items.Add(autostart);

        menu.Items.Add("Open settings file", null, (_, _) =>
        {
            _settings.Save(); // make sure the file exists
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Settings.FilePath) { UseShellExecute = true });
        });

        _updateItem = new ToolStripMenuItem("Check for updates…");
        _updateItem.Click += (_, _) =>
        {
            if (_available != null) _ = InstallUpdateAsync(_available);
            else _ = CheckForUpdatesAsync(manual: true);
        };
        menu.Items.Add(_updateItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private string TriggerLabel(TriggerMode mode) => mode switch
    {
        TriggerMode.DoubleCtrl => "Ctrl + Ctrl",
        TriggerMode.DoubleAlt => "Alt + Alt",
        TriggerMode.Custom => _settings.CustomHotkey?.ToString() ?? "your custom shortcut",
        _ => "Ctrl + Alt + T",
    };

    private string CustomLabel() =>
        _settings.CustomHotkey is { IsValid: true } hotkey ? "Custom: " + hotkey + "…" : "Custom…";

    // ----------------------------------------------------------------- autostart

    private static bool IsAutostartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) != null;
    }

    private static void SetAutostart(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled) key.SetValue(RunValueName, "\"" + Environment.ProcessPath + "\"");
        else key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    protected override void ExitThreadCore()
    {
        _tray.Visible = false;
        _showRequestWait.Unregister(null);
        _hook.Dispose();
        _tray.Dispose();
        _form.Dispose();
        base.ExitThreadCore();
    }
}
