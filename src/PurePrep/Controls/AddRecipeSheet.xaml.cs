using PurePrep.Localization;

namespace PurePrep.Controls;

/// <summary>
/// Home's "Add a recipe" sheet: paste a link, search the web, import from a photo or text, or type
/// a recipe in. Binds to the host page's BindingContext (RecipeLibraryViewModel) for the link box and
/// clipboard suggestion; every choice is raised as an event so Home keeps owning navigation and the
/// import flows (the link still goes through the Import sheet before any credit is spent).
/// </summary>
public partial class AddRecipeSheet : ContentView
{
    // Grab handle + the sheet's own padding, plus a gap so the sheet never reaches the status bar.
    private const double SheetChromeHeight = 56 + 48;

    public static readonly BindableProperty BalanceProperty = BindableProperty.Create(
        nameof(Balance), typeof(int), typeof(AddRecipeSheet), -1,
        propertyChanged: (bindable, _, _) => ((AddRecipeSheet)bindable).OnPropertyChanged(nameof(CostText)));

    public AddRecipeSheet()
    {
        InitializeComponent();
        // Follow the inner sheet's reset-to-closed when the page leaves the screen (see BottomSheet).
        Sheet.Unloaded += (_, _) => IsVisible = false;
        // Cap the body at the space the host gives us (which shrinks while the keyboard is up, see
        // SheetKeyboard) so the rows scroll instead of pushing the sheet off the top of the screen.
        SizeChanged += (_, _) =>
        {
            if (Height > SheetChromeHeight)
                Scroller.MaximumHeightRequest = Height - SheetChromeHeight;
        };
    }

    /// <summary>Smart Credit balance for the footer ("… · you have N"); -1 while unknown hides the count.</summary>
    public int Balance
    {
        get => (int)GetValue(BalanceProperty);
        set => SetValue(BalanceProperty, value);
    }

    public string CostText => Balance < 0
        ? AppResources.Get("AddSheetCost")
        : AppResources.Format("AddSheetCostFormat", Balance);

    /// <summary>True while the sheet is shown (or animating in).</summary>
    public bool IsOpen => Sheet.IsOpen;

    /// <summary>Import tapped (or Go on the keyboard) with the text in the link box.</summary>
    public event EventHandler<string>? ImportLinkRequested;

    /// <summary>The paste icon next to the link box was tapped.</summary>
    public event EventHandler? PasteRequested;

    /// <summary>The clipboard suggestion row was tapped.</summary>
    public event EventHandler? ClipboardSuggestionRequested;

    /// <summary>A web search was submitted; the argument is the trimmed query.</summary>
    public event EventHandler<string>? WebSearchRequested;

    public event EventHandler? PhotoRequested;

    public event EventHandler? TextRequested;

    public event EventHandler? ManualRequested;

    public void Show()
    {
        IsVisible = true;
        Sheet.Show();
    }

    public async Task HideAsync()
    {
        if (UrlEntry.IsFocused)
            UrlEntry.Unfocus();
        if (WebSearchEntry.IsFocused)
            WebSearchEntry.Unfocus();
        await Sheet.HideAsync();
        if (!Sheet.IsOpen)
            IsVisible = false;
    }

    public void Hide() => _ = HideAsync();

    private void OnSheetCancelled(object? sender, EventArgs e) => Hide();

    private void OnCloseTapped(object? sender, TappedEventArgs e) => Hide();

    private async void OnImportLinkTapped(object? sender, EventArgs e)
    {
        await UrlEntry.HideSoftInputAsync(CancellationToken.None);
        ImportLinkRequested?.Invoke(this, UrlEntry.Text ?? string.Empty);
    }

    private void OnPasteTapped(object? sender, TappedEventArgs e) => PasteRequested?.Invoke(this, EventArgs.Empty);

    private void OnClipboardSuggestionTapped(object? sender, TappedEventArgs e) =>
        ClipboardSuggestionRequested?.Invoke(this, EventArgs.Empty);

    private async void OnWebSearchCompleted(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(WebSearchEntry.Text))
            return;

        var query = WebSearchEntry.Text.Trim();
        WebSearchEntry.Text = string.Empty;
        // Unfocus alone leaves the keyboard up over the browser page.
        await WebSearchEntry.HideSoftInputAsync(CancellationToken.None);
        WebSearchEntry.Unfocus();
        WebSearchRequested?.Invoke(this, query);
    }

    private void OnPhotoTapped(object? sender, TappedEventArgs e) => PhotoRequested?.Invoke(this, EventArgs.Empty);

    private void OnTextTapped(object? sender, TappedEventArgs e) => TextRequested?.Invoke(this, EventArgs.Empty);

    private void OnManualTapped(object? sender, TappedEventArgs e) => ManualRequested?.Invoke(this, EventArgs.Empty);
}
