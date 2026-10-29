using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace QuickTranslator.Providers;

/// <summary>MyMemory free API (no key). Queries are limited to ~500 bytes, so longer text is sent in chunks.</summary>
internal sealed class MyMemoryProvider : ITranslationProvider
{
    private const int MaxChunkBytes = 450;

    private readonly HttpClient _http;

    public MyMemoryProvider(HttpClient http) => _http = http;

    public string Name => "MyMemory";

    public async Task<TranslationResult> TranslateAsync(string text, string source, string target, CancellationToken ct)
    {
        string pair = (source == Languages.Auto ? "Autodetect" : source) + "|" + target;
        var output = new StringBuilder();
        string detected = source == Languages.Auto ? "" : source;

        foreach (string chunk in Chunk(text))
        {
            if (string.IsNullOrWhiteSpace(chunk))
            {
                output.Append(chunk);
                continue;
            }

            string url = "https://api.mymemory.translated.net/get?langpair=" + Uri.EscapeDataString(pair)
                         + "&q=" + Uri.EscapeDataString(chunk);
            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var data = doc.RootElement.GetProperty("responseData");
            string translated = data.GetProperty("translatedText").GetString() ?? "";
            if (doc.RootElement.TryGetProperty("responseStatus", out var status) && status.ValueKind == JsonValueKind.Number && status.GetInt32() != 200)
                throw new HttpRequestException("MyMemory: " + translated);

            output.Append(translated);
            if (detected.Length == 0 && data.TryGetProperty("detectedLanguage", out var d) && d.ValueKind == JsonValueKind.String)
                detected = d.GetString() ?? "";
        }

        return new TranslationResult(output.ToString(), detected, Name);
    }

    /// <summary>Splits text at sentence/line boundaries into pieces that fit MyMemory's query limit.</summary>
    private static IEnumerable<string> Chunk(string text)
    {
        var current = new StringBuilder();
        foreach (string piece in Regex.Split(text, @"(?<=[.!?。！？\n])"))
        {
            foreach (string part in HardSplit(piece))
            {
                if (current.Length > 0 && Encoding.UTF8.GetByteCount(current.ToString()) + Encoding.UTF8.GetByteCount(part) > MaxChunkBytes)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                current.Append(part);
            }
        }
        if (current.Length > 0) yield return current.ToString();
    }

    private static IEnumerable<string> HardSplit(string piece)
    {
        if (Encoding.UTF8.GetByteCount(piece) <= MaxChunkBytes)
        {
            yield return piece;
            yield break;
        }

        var sb = new StringBuilder();
        foreach (var element in piece.EnumerateRunes())
        {
            if (Encoding.UTF8.GetByteCount(sb.ToString()) + element.Utf8SequenceLength > MaxChunkBytes)
            {
                yield return sb.ToString();
                sb.Clear();
            }
            sb.Append(element.ToString());
        }
        if (sb.Length > 0) yield return sb.ToString();
    }
}
