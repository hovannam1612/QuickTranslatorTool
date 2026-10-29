namespace QuickTranslator;

/// <summary>The app icon, loaded from app.ico (embedded in the assembly) so the exe, installer and tray share one source.</summary>
internal static class IconFactory
{
    /// <summary>Full multi-size icon (windows pick the size they need from it).</summary>
    public static Icon AppIcon { get; } = Load();

    /// <summary>Icon sized for the system tray (16px at 100% scaling, larger on high-DPI screens).</summary>
    public static Icon TrayIcon { get; } = new(AppIcon, SystemInformation.SmallIconSize);

    private static Icon Load()
    {
        using var stream = typeof(IconFactory).Assembly.GetManifestResourceStream("app.ico")
            ?? throw new InvalidOperationException("app.ico is not embedded in the assembly.");
        return new Icon(stream);
    }
}
