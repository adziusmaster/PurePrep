using Microsoft.Maui.Media;

namespace PurePrep.Services;

/// <summary>
/// Wraps text-to-speech so steps are read in the recipe's own language instead of always the device
/// default (which made a Polish recipe read by an English voice sound comical). It resolves an
/// installed <see cref="Locale"/> matching the recipe language — honouring the cook's preferred
/// voice when one is chosen in Settings — and reports whether a language can be spoken at all, so the
/// read-aloud controls can be hidden when it cannot.
/// </summary>
public sealed class ReadAloudService
{
    private readonly ITextToSpeech _tts;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<Locale>? _locales;

    public ReadAloudService(ITextToSpeech? tts = null) => _tts = tts ?? TextToSpeech.Default;

    /// <summary>All installed voices, grouped nowhere — just the raw platform list, cached once.</summary>
    public async Task<IReadOnlyList<Locale>> GetVoicesAsync()
    {
        if (_locales is not null)
            return _locales;

        await _gate.WaitAsync();
        try
        {
            _locales ??= (await _tts.GetLocalesAsync()).OrderBy(l => l.Language).ThenBy(l => l.Name).ToList();
        }
        catch
        {
            // No engine, or the query failed: treat as "nothing installed" rather than throwing into
            // the cooking flow. Read-aloud simply won't be offered.
            _locales = Array.Empty<Locale>();
        }
        finally
        {
            _gate.Release();
        }

        return _locales;
    }

    /// <summary>True when at least one installed voice can speak the given language.</summary>
    public async Task<bool> IsLanguageAvailableAsync(string? languageCode)
    {
        var voices = await GetVoicesAsync();
        if (voices.Count == 0)
            return false;
        if (string.IsNullOrWhiteSpace(languageCode))
            return true; // Unknown language: let the device default engine handle it.

        var baseCode = BaseCode(languageCode);
        return voices.Any(v => BaseCode(v.Language) == baseCode);
    }

    /// <summary>Installed voices that can speak the given language (empty when none).</summary>
    public async Task<IReadOnlyList<Locale>> GetVoicesForAsync(string? languageCode)
    {
        var voices = await GetVoicesAsync();
        if (string.IsNullOrWhiteSpace(languageCode))
            return voices;
        var baseCode = BaseCode(languageCode);
        return voices.Where(v => BaseCode(v.Language) == baseCode).ToList();
    }

    /// <summary>
    /// Reads <paramref name="text"/> aloud in <paramref name="languageCode"/>, using the preferred
    /// voice when it belongs to that language, otherwise the first matching installed voice.
    /// </summary>
    public async Task SpeakAsync(string text, string? languageCode, string? preferredVoiceId, CancellationToken cancellationToken)
    {
        var locale = await ResolveAsync(languageCode, preferredVoiceId);
        var options = locale is null ? null : new SpeechOptions { Locale = locale };
        await _tts.SpeakAsync(text, options, cancellationToken);
    }

    private async Task<Locale?> ResolveAsync(string? languageCode, string? preferredVoiceId)
    {
        var voices = await GetVoicesAsync();
        if (voices.Count == 0)
            return null;

        var matches = string.IsNullOrWhiteSpace(languageCode)
            ? voices
            : voices.Where(v => BaseCode(v.Language) == BaseCode(languageCode)).ToList();
        if (matches.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(preferredVoiceId))
        {
            var preferred = matches.FirstOrDefault(v => VoiceId(v) == preferredVoiceId);
            if (preferred is not null)
                return preferred;
        }

        return matches[0];
    }

    /// <summary>Stable identity for a voice, since <see cref="Locale.Id"/> is empty on some engines.</summary>
    public static string VoiceId(Locale locale) => $"{locale.Language}|{locale.Country}|{locale.Name}";

    private static string BaseCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;
        var span = code.AsSpan().Trim();
        var cut = span.IndexOfAny('-', '_');
        if (cut > 0)
            span = span[..cut];
        return span.ToString().ToLowerInvariant();
    }
}
