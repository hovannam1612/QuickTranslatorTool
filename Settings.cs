using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuickTranslator;

internal enum Engine
{
    GoogleFree,
    GoogleCloud,
    MyMemory,
}

internal static class EngineExtensions
{
    public static string Label(this Engine engine) => engine switch
    {
        Engine.GoogleFree => "Google (free)",
        Engine.GoogleCloud => "Google Cloud (API key)",
        _ => "MyMemory (free)",
    };
}

internal sealed class Settings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickTranslator", "settings.json");

    /// <summary>Source language code, or "auto" to detect it.</summary>
    public string Source { get; set; } = "auto";

    /// <summary>Language to translate into.</summary>
    public string Target { get; set; } = "vi";

    /// <summary>Translation window client size; 0 means "use the default".</summary>
    public int WindowWidth { get; set; }

    public int WindowHeight { get; set; }

    public TriggerMode Trigger { get; set; } = TriggerMode.DoubleCtrl;

    /// <summary>The shortcut used when <see cref="Trigger"/> is <see cref="TriggerMode.Custom"/>.</summary>
    public Hotkey? CustomHotkey { get; set; }

    public Engine Engine { get; set; } = Engine.GoogleFree;

    /// <summary>API key for the official Google Cloud Translation API (only used by <see cref="Engine.GoogleCloud"/>).</summary>
    public string GoogleApiKey { get; set; } = "";

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions) ?? new Settings();
        }
        catch
        {
            // corrupt file: fall back to defaults
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // not being able to persist settings must never crash the app
        }
    }
}
