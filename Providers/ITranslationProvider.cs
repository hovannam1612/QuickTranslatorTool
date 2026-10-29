namespace QuickTranslator.Providers;

internal sealed record TranslationResult(string Text, string DetectedLanguage, string Provider);

internal interface ITranslationProvider
{
    string Name { get; }

    /// <param name="source">Language code or "auto".</param>
    Task<TranslationResult> TranslateAsync(string text, string source, string target, CancellationToken ct);
}
