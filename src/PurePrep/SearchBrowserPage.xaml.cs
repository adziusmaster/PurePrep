namespace PurePrep;

using System.Globalization;
using PurePrep.Ai;
using PurePrep.Application;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Presentation;
using PurePrep.Resources.Styles;
using PurePrep.Services;

/// <summary>
/// In-app recipe search: a WebView that starts on a Google search for "&lt;query&gt; recipe". After each
/// page load it scans the page (JSON-LD, Recipe microdata, title) and, when <see cref="WebRecipeDetector"/>
/// finds a Recipe, offers "+ Add to PurePrep", which opens the shared Import sheet with a title/photo
/// preview; other non-Google pages get a quieter "Try adding this page". Nothing is imported (or
/// charged) until the user confirms in that sheet.
/// </summary>
public partial class SearchBrowserPage : ContentPage, IHardwareBackHandler
{
    // Payload is capped (at most 10 blocks of 200,000 chars each; Core also rejects oversized results).
    // Collects what Core needs to decide "is this a recipe": every ld+json block's raw text, the first
    // schema.org/Recipe microdata item's own name + image (props of nested items such as the author
    // are skipped), and the document title. Detection itself happens in Core (WebRecipeDetector).
    private const string ScanScript =
        "(function(){" +
        "var ld=Array.from(document.querySelectorAll('script[type=\"application/ld+json\"]')).slice(0,10).map(function(s){return (s.textContent||'').slice(0,200000);});" +
        "var md=Array.from(document.querySelectorAll('[itemscope][itemtype]')).find(function(e){return e.getAttribute('itemtype').toLowerCase().indexOf('schema.org/recipe')>=0;});" +
        "var own=function(p){return md?Array.from(md.querySelectorAll('[itemprop~=\"'+p+'\"]')).find(function(e){return e.parentElement.closest('[itemscope]')===md;}):null;};" +
        "var n=own('name'),i=own('image');" +
        "return JSON.stringify({ld:ld," +
        "mdName:n?(n.getAttribute('content')||n.textContent):null," +
        "mdImage:i?(i.getAttribute('src')||i.getAttribute('content')||i.getAttribute('href')):null," +
        "title:document.title});" +
        "})()";

    // Recipe sites that render their JSON-LD client-side may not have it yet when Navigated fires,
    // so a miss is retried once after a short delay.
    private static readonly TimeSpan DetectRetryDelay = TimeSpan.FromMilliseconds(1500);

    private const long MaxPreviewImageBytes = 8 * 1024 * 1024;

    private static readonly HttpClient PreviewHttp = new() { Timeout = TimeSpan.FromSeconds(10) };

    private readonly RecipeLibraryViewModel _library;
    private string? _currentUrl;
    // What the scan found, tied to the URL it was made for: a result only counts while that URL is
    // still the current page, so a late scan can never put the previous page's recipe on screen.
    private PageDetection? _detection;
    private int _navigation;
    private bool _importing;

    public SearchBrowserPage(string query, RecipeLibraryViewModel library)
    {
        InitializeComponent();
        _library = library;

        var terms = $"{query} {AppResources.Get("RecipeSearchWord")}";
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        Browser.Source = new UrlWebViewSource
        {
            Url = $"https://www.google.com/search?q={Uri.EscapeDataString(terms)}&hl={language}",
        };
        Bar.Title = "google.com";
    }

    // Re-read the balance whenever the browser shows (first open, back from Buy Smart Credits) so the
    // Import sheet offers Import or Buy based on the real balance, not a stale or unknown one.
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _library.RefreshCreditsAsync();
        if (ImportSheet.IsVisible)
            ImportSheet.Balance = _library.CreditBalance;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url == "about:blank")
            return;

        // Fail closed — http(s) only: intent://, market://, tel:, unparseable URLs and friends are never followed.
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var target) ||
            (target.Scheme != Uri.UriSchemeHttp && target.Scheme != Uri.UriSchemeHttps))
        {
            e.Cancel = true;
            return;
        }

        // A new page is on its way: the old page's recipe no longer applies.
        ResetDetection();
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var page) ||
            (page.Scheme != Uri.UriSchemeHttp && page.Scheme != Uri.UriSchemeHttps))
            return;

        // Some navigations (history, redirects) arrive without a Navigating first.
        if (e.Url != _currentUrl)
            ResetDetection();
        _currentUrl = e.Url;
        Bar.Title = HostOf(page);
        if (e.Result != WebNavigationResult.Success)
            return;

        var navigation = _navigation;
        var scan = await ScanAsync();
        var recipe = Detect(scan, page);
        if (recipe is null && navigation == _navigation)
        {
            await Task.Delay(DetectRetryDelay);
            if (navigation == _navigation)
            {
                scan = await ScanAsync() ?? scan;
                recipe = Detect(scan, page);
            }
        }

        // Ignore results for a page the user has already left.
        if (navigation != _navigation)
            return;
        var title = string.IsNullOrWhiteSpace(scan?.Title) ? null : scan.Title.Trim();
        _detection = new PageDetection(e.Url, recipe, title);
        UpdatePill();
    }

    private void ResetDetection()
    {
        _navigation++;
        _detection = null;
        UpdatePill();
    }

    /// <summary>The scan result for the page on screen, or null (not scanned yet / stale).</summary>
    private PageDetection? CurrentDetection =>
        _detection is { } detection && detection.Url == _currentUrl ? detection : null;

    private async Task<WebPageScan?> ScanAsync()
    {
        try
        {
            return WebPageScan.Decode(await Browser.EvaluateJavaScriptAsync(ScanScript));
        }
        catch
        {
            // Script failure / page torn down mid-evaluation: treat as "not a recipe".
            return null;
        }
    }

    private static WebRecipeInfo? Detect(WebPageScan? scan, Uri page)
    {
        if (scan is null)
            return null;
        try
        {
            return WebRecipeDetector.Detect(scan.JsonLdBlocks, scan.MicrodataName, scan.MicrodataImage, page);
        }
        catch
        {
            // Defence in depth: Core never throws on page content, but this runs in an async void
            // handler where an escaping exception would crash the app.
            return null;
        }
    }

    // Green "+ Add to PurePrep" for a detected recipe; otherwise, once the page has been scanned, the
    // quieter outlined "Try adding this page" — except on Google's own pages (the search results).
    private void UpdatePill()
    {
        var detection = CurrentDetection;
        var isRecipe = detection?.Recipe is not null;
        var offerFallback = detection is not null && !isRecipe && !IsGooglePage(detection.Url);

        AddPill.IsVisible = _importing || isRecipe;
        AddPillContent.IsVisible = !_importing;
        AddPillBusy.IsVisible = _importing;
        AddPillBusy.IsRunning = _importing;
        TryPill.IsVisible = !_importing && offerFallback;
    }

    private static bool IsGooglePage(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && GoogleHost.IsGoogle(uri);

    private void OnAddTapped(object? sender, EventArgs e)
    {
        if (_importing || CurrentDetection is not { Recipe: { } recipe } detection)
            return;
        ShowImportSheet(detection.Url, recipe);
    }

    private void OnTryAddTapped(object? sender, EventArgs e)
    {
        if (_importing || _currentUrl is null)
            return;
        ShowImportSheet(_currentUrl, null);
    }

    private async void OnMoreTapped(object? sender, EventArgs e)
    {
        if (_currentUrl is null)
            return;

        var openInBrowser = AppResources.Get("OpenInBrowser");
        var tryImport = AppResources.Get("TryImportAnyway");
        var choice = await AppDialog.ChooseAsync(this, AppResources.Get("More"), AppResources.Get("Cancel"),
            new DialogChoice(openInBrowser, Icon: Icons.OpenInNew),
            new DialogChoice(tryImport, Icon: Icons.Add));

        if (choice == openInBrowser)
        {
            try
            {
                await Launcher.OpenAsync(_currentUrl);
            }
            catch
            {
                // No browser installed: fall back to copying so the link is never lost.
                await Clipboard.SetTextAsync(_currentUrl);
            }
        }
        else if (choice == tryImport && !_importing)
        {
            // Not detected as a recipe (or detected, but the user went via the menu): same sheet,
            // with the preview only when there is one.
            ShowImportSheet(_currentUrl, CurrentDetection?.Recipe);
        }
    }

    private void ShowImportSheet(string url, WebRecipeInfo? recipe)
    {
        ImportSheet.Url = url;
        ImportSheet.Host = Uri.TryCreate(url, UriKind.Absolute, out var u) ? HostOf(u) : url;
        ImportSheet.Balance = _library.CreditBalance;
        ImportSheet.IsDuplicate = _library.FindBySourceUrl(url) is not null;
        // No recipe detected: the page's own title (if any) stands in; the host is shown below anyway.
        ImportSheet.PreviewTitle = recipe?.Title ?? CurrentDetection?.Title;
        ImportSheet.PreviewImage = null;
        ImportSheet.Show();

        if (recipe?.ImageUrl is { } image)
            _ = LoadPreviewImageAsync(url, image);
    }

    // Downloads the recipe photo for the sheet. Any failure (timeout, too big, not an image) just
    // leaves the sheet without a photo.
    private async Task LoadPreviewImageAsync(string pageUrl, Uri imageUrl)
    {
        try
        {
            using var response = await PreviewHttp.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxPreviewImageBytes)
                return;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                if (buffer.Length + read > MaxPreviewImageBytes)
                    return;
                buffer.Write(chunk, 0, read);
            }

            var bytes = buffer.ToArray();
            // Only if the sheet is still showing this page.
            if (ImportSheet.IsVisible && ImportSheet.Url == pageUrl)
                ImportSheet.PreviewImage = ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch
        {
            // No photo.
        }
    }

    private async void OnImportSheetImport(object? sender, EventArgs e)
    {
        // A repeat tap while an import is already running must not charge twice.
        if (_importing)
            return;

        var url = ImportSheet.Url;
        var allowDuplicate = ImportSheet.IsDuplicate;
        ImportSheet.Hide();

        _importing = true;
        UpdatePill();
        ImportResult result;
        try
        {
            result = await _library.ImportUrlAsync(url, allowDuplicate);
        }
        finally
        {
            _importing = false;
            UpdatePill();
        }

        // The user may have left (top-bar back) while the import ran: the recipe is in the library
        // either way, so only navigate / report when this page is still the one on screen.
        if (Navigation.NavigationStack.LastOrDefault() != this)
            return;

        if (result is { Outcome: ImportOutcome.Imported, Recipe: { } saved })
        {
            // Replace the browser with the new recipe so back returns Home.
            await Navigation.PushAsync(new RecipeDetailPage(saved, _library));
            Navigation.RemovePage(this);
        }
        else if (result.Outcome == ImportOutcome.OutOfCredits)
        {
            // The server said 402 (the balance was unknown or stale): Buy credits, never an error.
            await BuyCreditsPage.OpenAsync(Navigation, 0);
        }
        else if (result.Outcome == ImportOutcome.Failed && !string.IsNullOrEmpty(_library.ErrorMessage))
        {
            await AppDialog.AlertAsync(this, AppResources.Get("Import"), _library.ErrorMessage, AppResources.Get("Ok"));
        }
    }

    // Zero balance: the sheet offered Buy Smart Credits instead of Import, so nothing was attempted.
    private async void OnImportSheetBuyCredits(object? sender, EventArgs e)
    {
        ImportSheet.Hide();
        await BuyCreditsPage.OpenAsync(Navigation, _library.CreditBalance);
    }

    private async void OnImportSheetOpenExisting(object? sender, EventArgs e)
    {
        ImportSheet.Hide();
        if (_library.FindBySourceUrl(ImportSheet.Url) is { } existing)
            await Navigation.PushAsync(new RecipeDetailPage(existing, _library));
    }

    private void OnImportSheetCancel(object? sender, EventArgs e) => ImportSheet.Hide();

    // Hardware back: close the sheet, else step back through the web history, else leave the page
    // (MainActivity pops when this returns false). The top bar's back arrow always leaves.
    public bool OnHardwareBack()
    {
        if (ImportSheet.IsVisible)
        {
            ImportSheet.Hide();
            return true;
        }

        if (Browser.CanGoBack)
        {
            ResetDetection();
            Browser.GoBack();
            return true;
        }

        return false;
    }

    private static string HostOf(Uri uri) =>
        uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;

    private sealed record PageDetection(string Url, WebRecipeInfo? Recipe, string? Title);
}
