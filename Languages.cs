namespace QuickTranslator;

internal sealed record Language(string Code, string Name)
{
    public override string ToString() => Name;
}

internal static class Languages
{
    public const string Auto = "auto";

    /// <summary>Real languages (no "Auto"), sorted by name.</summary>
    public static readonly IReadOnlyList<Language> All = new Language[]
    {
        new("af", "Afrikaans"), new("sq", "Albanian"), new("ar", "Arabic"), new("hy", "Armenian"),
        new("az", "Azerbaijani"), new("eu", "Basque"), new("be", "Belarusian"), new("bn", "Bengali"),
        new("bs", "Bosnian"), new("bg", "Bulgarian"), new("ca", "Catalan"), new("zh-CN", "Chinese (Simplified)"),
        new("zh-TW", "Chinese (Traditional)"), new("hr", "Croatian"), new("cs", "Czech"), new("da", "Danish"),
        new("nl", "Dutch"), new("en", "English"), new("et", "Estonian"), new("fi", "Finnish"),
        new("fr", "French"), new("gl", "Galician"), new("ka", "Georgian"), new("de", "German"),
        new("el", "Greek"), new("gu", "Gujarati"), new("he", "Hebrew"), new("hi", "Hindi"),
        new("hu", "Hungarian"), new("is", "Icelandic"), new("id", "Indonesian"), new("ga", "Irish"),
        new("it", "Italian"), new("ja", "Japanese"), new("kn", "Kannada"), new("kk", "Kazakh"),
        new("km", "Khmer"), new("ko", "Korean"), new("lo", "Lao"), new("lv", "Latvian"),
        new("lt", "Lithuanian"), new("mk", "Macedonian"), new("ms", "Malay"), new("ml", "Malayalam"),
        new("mt", "Maltese"), new("mr", "Marathi"), new("mn", "Mongolian"), new("my", "Myanmar (Burmese)"),
        new("ne", "Nepali"), new("no", "Norwegian"), new("fa", "Persian"), new("pl", "Polish"),
        new("pt", "Portuguese"), new("pa", "Punjabi"), new("ro", "Romanian"), new("ru", "Russian"),
        new("sr", "Serbian"), new("si", "Sinhala"), new("sk", "Slovak"), new("sl", "Slovenian"),
        new("es", "Spanish"), new("sw", "Swahili"), new("sv", "Swedish"), new("tl", "Tagalog (Filipino)"),
        new("ta", "Tamil"), new("te", "Telugu"), new("th", "Thai"), new("tr", "Turkish"),
        new("uk", "Ukrainian"), new("ur", "Urdu"), new("uz", "Uzbek"), new("vi", "Vietnamese"),
        new("cy", "Welsh"),
    };

    /// <summary>Most-used languages, shown first in the dropdowns.</summary>
    private static readonly string[] PinnedCodes = { "vi", "en", "ja" };

    /// <summary>All languages with the pinned ones first, then the rest alphabetically.</summary>
    public static readonly IReadOnlyList<Language> Ordered = PinnedCodes
        .Select(code => All.First(l => l.Code == code))
        .Concat(All.Where(l => !PinnedCodes.Contains(l.Code)))
        .ToList();

    /// <summary>Where text that is already in <paramref name="target"/> goes instead: English, or Vietnamese when the target is English.</summary>
    public static string FallbackFor(string target) => SameLanguage(target, "en") ? "vi" : "en";

    public static readonly Language AutoDetect = new(Auto, "Auto detect");

    public static string NameOf(string code)
    {
        if (string.IsNullOrEmpty(code)) return "?";
        if (code == Auto) return AutoDetect.Name;
        code = Normalize(code);
        return All.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase))?.Name ?? code;
    }

    /// <summary>True when both codes denote the same base language (zh-CN and zh-TW count as the same).</summary>
    public static bool SameLanguage(string a, string b) =>
        string.Equals(Primary(Normalize(a)), Primary(Normalize(b)), StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps legacy/regional codes returned by the services onto the codes used in <see cref="All"/>.</summary>
    public static string Normalize(string code) => code.ToLowerInvariant() switch
    {
        "iw" => "he",
        "fil" => "tl",
        "nb" or "nn" => "no",
        "zh" => "zh-CN",
        "zh-cn" => "zh-CN",
        "zh-tw" => "zh-TW",
        _ => code,
    };

    private static string Primary(string code)
    {
        int dash = code.IndexOf('-');
        return dash < 0 ? code : code[..dash];
    }
}
