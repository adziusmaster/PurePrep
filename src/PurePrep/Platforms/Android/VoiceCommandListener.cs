using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Speech;
using PurePrep.Application;

namespace PurePrep.Platforms.Android;

/// <summary>
/// Hands-free step navigation via Android's on-device <see cref="SpeechRecognizer"/>.
///
/// The recogniser is one-shot, so this keeps a fresh listening session armed: whenever a result or a
/// (recoverable) error comes back, it starts another while listening is still on. Everything touching
/// the recogniser runs on the main thread, which the platform requires. Command matching is keyword
/// based and intentionally forgiving, so near-misses like "go back" or "again please" still land.
/// </summary>
public sealed class VoiceCommandListener : Java.Lang.Object, IVoiceCommandListener, IRecognitionListener
{
    private SpeechRecognizer? _recognizer;
    private bool _listening;
    private string? _languageTag;

    private static Context Context => global::Android.App.Application.Context;

    public bool IsSupported => SpeechRecognizer.IsRecognitionAvailable(Context);

    public event EventHandler<VoiceCommand>? CommandRecognized;

    public async Task<bool> StartAsync(string? languageTag = null, CancellationToken cancellationToken = default)
    {
        if (!IsSupported)
            return false;

        var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Microphone>();
        if (status != PermissionStatus.Granted)
            return false;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _languageTag = languageTag;
            _listening = true;
            EnsureRecognizer();
            StartListening();
        });

        return true;
    }

    public Task StopAsync() =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
            _listening = false;
            try
            {
                _recognizer?.StopListening();
                _recognizer?.Cancel();
                _recognizer?.Destroy();
            }
            catch
            {
                // The recogniser may already be torn down; releasing the mic is best-effort.
            }
            finally
            {
                _recognizer = null;
            }
        });

    private void EnsureRecognizer()
    {
        if (_recognizer is not null)
            return;

        _recognizer = SpeechRecognizer.CreateSpeechRecognizer(Context);
        _recognizer?.SetRecognitionListener(this);
    }

    private void StartListening()
    {
        if (!_listening || _recognizer is null)
            return;

        var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
        intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
        intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
        // Bias the recogniser towards the recipe's language so commands are heard in the language the
        // steps are actually read in; fall back to the device language when it is unknown.
        var locale = string.IsNullOrWhiteSpace(_languageTag)
            ? Java.Util.Locale.Default
            : Java.Util.Locale.ForLanguageTag(_languageTag);
        intent.PutExtra(RecognizerIntent.ExtraLanguage, locale);

        try
        {
            _recognizer.StartListening(intent);
        }
        catch
        {
            // If arming fails we simply stop; touch/swipe navigation is unaffected.
            _listening = false;
        }
    }

    // Re-arms the next listening session on the UI thread, unless the user has switched the mic off.
    private void Rearm()
    {
        if (!_listening)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_listening || _recognizer is null)
                return;
            _recognizer.Cancel();
            StartListening();
        });
    }

    private void Handle(IList<string>? phrases)
    {
        if (phrases is null)
            return;

        foreach (var phrase in phrases)
        {
            if (VoiceCommandVocabulary.TryMatch(phrase, out var command))
            {
                CommandRecognized?.Invoke(this, command);
                return;
            }
        }
    }

    // ===== IRecognitionListener =====

    public void OnResults(Bundle? results)
    {
        Handle(results?.GetStringArrayList(SpeechRecognizer.ResultsRecognition));
        Rearm();
    }

    public void OnPartialResults(Bundle? partialResults)
    {
        // Acted on only on final results to avoid double-firing a command mid-utterance.
    }

    public void OnError([GeneratedEnum] SpeechRecognizerError error)
    {
        // "No match" and timeouts are normal in a quiet kitchen; just listen again. A busy-recogniser
        // error is transient too. Anything else, we still try to re-arm — the worst case is that
        // listening quietly stops, which is a safe degradation.
        Rearm();
    }

    public void OnReadyForSpeech(Bundle? @params) { }
    public void OnBeginningOfSpeech() { }
    public void OnRmsChanged(float rmsdB) { }
    public void OnBufferReceived(byte[]? buffer) { }
    public void OnEndOfSpeech() { }
    public void OnEvent(int eventType, Bundle? @params) { }
}
