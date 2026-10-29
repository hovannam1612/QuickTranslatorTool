using System.Net;
using System.Text.Json;

namespace QuickTranslator.Providers;

/// <summary>Official Google Cloud Translation API v2 (needs an API key in settings.json).</summary>
internal sealed class GoogleCloudProvider : ITranslationProvider
{
    private readonly HttpClient _http;
    private readonly Func<string> _apiKey;

    public GoogleCloudProvider(HttpClient http, Func<string> apiKey)
    {
        _http = http;
        _apiKey = apiKey;
    }

    public string Name => "Google Cloud";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey());

    public async Task<TranslationResult> TranslateAsync(string text, string source, string target, CancellationToken ct)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google Cloud API key is not set (GoogleApiKey in settings.json).");

        var form = new List<KeyValuePair<string, string>>
        {
            new("q", text),
            new("target", target),
            new("format", "text"),
            new("key", _apiKey().Trim()),
        };
        if (source != Languages.Auto) form.Add(new("source", source));

        using var response = await _http.PostAsync(
            "https://translation.googleapis.com/language/translate/v2", new FormUrlEncodedContent(form), ct);
        string json = await response.Content.ReadAsStringAsync(ct);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new HttpRequestException("Google Cloud API error: " + ExtractError(json, response.StatusCode));

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement.GetProperty("data").GetProperty("translations")[0];
        string translated = WebUtility.HtmlDecode(first.GetProperty("translatedText").GetString() ?? "");
        string detected = first.TryGetProperty("detectedSourceLanguage", out var d) ? d.GetString() ?? "" : source;
        return new TranslationResult(translated, detected, Name);
    }

    private static string ExtractError(string json, HttpStatusCode status)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? status.ToString();
        }
        catch
        {
            return status.ToString();
        }
    }
}
