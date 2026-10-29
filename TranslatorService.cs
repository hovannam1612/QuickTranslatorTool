using QuickTranslator.Providers;

namespace QuickTranslator;

/// <summary>Translates text with the engine chosen in the settings, falling back to the others if it fails.</summary>
internal sealed class TranslatorService
{
    public const int MaxLength = 5000;

    private readonly Settings _settings;
    private readonly GoogleFreeProvider _googleFree;
    private readonly GoogleCloudProvider _googleCloud;
    private readonly MyMemoryProvider _myMemory;

    public TranslatorService(Settings settings)
    {
        _settings = settings;
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) QuickTranslator/1.0");
        _googleFree = new GoogleFreeProvider(http);
        _googleCloud = new GoogleCloudProvider(http, () => _settings.GoogleApiKey);
        _myMemory = new MyMemoryProvider(http);
    }

    public async Task<TranslationResult> TranslateAsync(string text, string source, string target, CancellationToken ct)
    {
        if (text.Length > MaxLength) text = text[..MaxLength];

        Exception? firstError = null;
        foreach (var provider in Chain())
        {
            try
            {
                var result = await provider.TranslateAsync(text, source, target, ct);
                if (result.Text.Length > 0 || text.Trim().Length == 0) return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                firstError ??= ex;
            }
        }
        throw firstError ?? new InvalidOperationException("No translation engine returned a result.");
    }

    /// <summary>The preferred engine first, then the remaining usable ones as fallback.</summary>
    private IEnumerable<ITranslationProvider> Chain()
    {
        ITranslationProvider preferred = _settings.Engine switch
        {
            Engine.GoogleCloud => _googleCloud,
            Engine.MyMemory => _myMemory,
            _ => _googleFree,
        };

        yield return preferred;
        if (preferred != _googleFree) yield return _googleFree;
        if (preferred != _googleCloud && _googleCloud.IsConfigured) yield return _googleCloud;
        if (preferred != _myMemory) yield return _myMemory;
    }
}
