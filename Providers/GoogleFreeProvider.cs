using System.Text;
using System.Text.Json;

namespace QuickTranslator.Providers;

/// <summary>
/// Google Translate's public web endpoint (no API key). Unofficial, so Google may rate-limit it;
/// <see cref="TranslatorService"/> falls back to another engine when that happens.
/// </summary>
internal sealed class GoogleFreeProvider : ITranslationProvider
{
    private readonly HttpClient _http;

    public GoogleFreeProvider(HttpClient http) => _http = http;

    public string Name => "Google";

    public async Task<TranslationResult> TranslateAsync(string text, string source, string target, CancellationToken ct)
    {
        string url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t&dj=1&ie=UTF-8&oe=UTF-8"
                     + "&sl=" + Uri.EscapeDataString(source) + "&tl=" + Uri.EscapeDataString(target);

        using var body = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("q", text) });
        using var response = await _http.PostAsync(url, body, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        if (!json.TrimStart().StartsWith('{'))
            throw new HttpRequestException("Google rejected the request (rate limited).");

        using var doc = JsonDocument.Parse(json);
        var sb = new StringBuilder();
        if (doc.RootElement.TryGetProperty("sentences", out var sentences))
        {
            foreach (var s in sentences.EnumerateArray())
                if (s.TryGetProperty("trans", out var t))
                    sb.Append(t.GetString());
        }

        string detected = doc.RootElement.TryGetProperty("src", out var src) ? src.GetString() ?? "" : "";
        return new TranslationResult(sb.ToString(), detected, Name);
    }
}
