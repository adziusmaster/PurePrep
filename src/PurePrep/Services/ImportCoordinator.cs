using PurePrep.Presentation;

namespace PurePrep.Services;

/// <summary>
/// Routes every "here's a link" moment (share from another app, clipboard chip) to one flow:
/// return to Home, show the Import sheet, import on confirm, then open the new recipe. Lives at app
/// level so a share landing while Settings or a recipe is open still does something visible.
/// </summary>
public sealed class ImportCoordinator
{
    private readonly SharedUrlRelay _relay;
    private MainPage? _home;

    public ImportCoordinator(SharedUrlRelay relay)
    {
        _relay = relay;
        _relay.Received += (_, url) => MainThread.BeginInvokeOnMainThread(() => SafeOfferAsync(url));
        _relay.ReceivedWithoutUrl += (_, _) => MainThread.BeginInvokeOnMainThread(() => _home?.ViewModel.ReportSharedUrlMissing());
    }

    /// <summary>Called once the Home page exists. Also collects a share that arrived before it did (cold start).</summary>
    public void Attach(MainPage home)
    {
        _home = home;
        if (_relay.TakePending() is { } pending)
            MainThread.BeginInvokeOnMainThread(() => SafeOfferAsync(pending));
    }

    // Fire-and-forget entry point for the event handlers above: MainThread.BeginInvokeOnMainThread
    // takes an Action, so OfferAsync's Task (and any exception it throws, e.g. from navigation)
    // would otherwise go unobserved on the UI thread. Best-effort: the link is simply not offered.
    private async void SafeOfferAsync(string url)
    {
        try
        {
            await OfferAsync(url);
        }
        catch
        {
        }
    }

    /// <summary>Brings Home to the front (popping any pushed page) and opens the Import sheet for the link.</summary>
    public async Task OfferAsync(string url)
    {
        if (_home is null)
            return;
        _relay.Clear();
        var navigation = _home.Navigation;
        if (navigation.NavigationStack.Count > 1)
            await navigation.PopToRootAsync(animated: false);
        _home.ShowImportSheet(url);
    }

    /// <summary>
    /// Called whenever the app window gains focus (back from another app, or a system overlay closing).
    /// Checks the clipboard while on Home. Focus, not resume, because Android 10+ refuses clipboard
    /// reads from an app whose window is not focused yet; the clip's timestamp keeps it to one read per clip.
    /// </summary>
    public async Task OnWindowFocusedAsync()
    {
        if (_home is not null && _home.Navigation.NavigationStack.Count == 1)
            await _home.ViewModel.CheckClipboardAsync();
    }
}
