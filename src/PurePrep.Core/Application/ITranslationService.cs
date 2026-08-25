using PurePrep.Domain;

namespace PurePrep.Application;

/// <summary>
/// On-device, offline recipe translation backed by downloadable language packs
/// (Google ML Kit on Android). Translation is free — no credits are consumed.
/// The Web/preview build gets an <see cref="UnsupportedTranslationService"/> whose
/// <see cref="IsSupported"/> is <c>false</c>.
/// </summary>
public interface ITranslationService
{
    /// <summary>True when the running platform can translate on-device.</summary>
    bool IsSupported { get; }

    /// <summary>Language codes offered for offline packs (ISO 639-1), e.g. en, de, fr, es, it, pl, nl.</summary>
    IReadOnlyList<string> SupportedLanguageCodes { get; }

    /// <summary>Whether the offline model for <paramref name="code"/> is already on the device.</summary>
    Task<bool> IsModelDownloadedAsync(string code, CancellationToken ct = default);

    /// <summary>Language codes whose offline models are currently downloaded.</summary>
    Task<IReadOnlyList<string>> GetDownloadedModelsAsync(CancellationToken ct = default);

    /// <summary>Downloads the offline model for <paramref name="code"/> (optionally Wi-Fi only).</summary>
    Task DownloadModelAsync(string code, bool requireWifi = true, CancellationToken ct = default);

    /// <summary>Deletes the offline model for <paramref name="code"/> to reclaim storage.</summary>
    Task DeleteModelAsync(string code, CancellationToken ct = default);

    /// <summary>Best-effort source-language detection; returns an ISO code or <c>null</c> if undetermined.</summary>
    Task<string?> DetectLanguageAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Translates the recipe (title, ingredients, steps) into <paramref name="targetCode"/> and
    /// returns a new <see cref="ParsedRecipe"/> preserving Id/SourceUrl/SourceSystem/SavedAt so it
    /// can replace the original in place. Downloads the required model(s) on demand.
    /// </summary>
    Task<ParsedRecipe> TranslateAsync(ParsedRecipe recipe, string targetCode, bool requireWifi = true, CancellationToken ct = default);
}

/// <summary>Fallback used on platforms without on-device translation (Web/preview, desktop).</summary>
public sealed class UnsupportedTranslationService : ITranslationService
{
    public bool IsSupported => false;

    public IReadOnlyList<string> SupportedLanguageCodes { get; } =
        new[] { "en", "de", "fr", "es", "it", "pl", "nl" };

    public Task<bool> IsModelDownloadedAsync(string code, CancellationToken ct = default) => Task.FromResult(false);

    public Task<IReadOnlyList<string>> GetDownloadedModelsAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<string>)Array.Empty<string>());

    public Task DownloadModelAsync(string code, bool requireWifi = true, CancellationToken ct = default) =>
        throw new NotSupportedException("On-device translation is not available on this platform.");

    public Task DeleteModelAsync(string code, CancellationToken ct = default) =>
        throw new NotSupportedException("On-device translation is not available on this platform.");

    public Task<string?> DetectLanguageAsync(string text, CancellationToken ct = default) => Task.FromResult<string?>(null);

    public Task<ParsedRecipe> TranslateAsync(ParsedRecipe recipe, string targetCode, bool requireWifi = true, CancellationToken ct = default) =>
        throw new NotSupportedException("On-device translation is not available on this platform.");
}
