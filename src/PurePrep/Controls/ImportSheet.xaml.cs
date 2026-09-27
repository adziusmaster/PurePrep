using PurePrep.Application;
using PurePrep.Localization;

namespace PurePrep.Controls;

/// <summary>
/// The "import from anywhere" sheet: shown over Home whenever a link arrives from the Android share
/// sheet (warm or cold start) or the clipboard chip. One control covers both the "new link" (spends a
/// Smart Credit) and "already saved" (open or re-import) cases so all three arrival paths present the
/// same confirmation before anything is charged. The in-app browser hosts it too, adding a title and
/// photo preview of the recipe page.
/// </summary>
public partial class ImportSheet : ContentView
{
    public static readonly BindableProperty UrlProperty = BindableProperty.Create(
        nameof(Url), typeof(string), typeof(ImportSheet), string.Empty);

    public static readonly BindableProperty HostProperty = BindableProperty.Create(
        nameof(Host), typeof(string), typeof(ImportSheet), string.Empty);

    public static readonly BindableProperty BalanceProperty = BindableProperty.Create(
        nameof(Balance), typeof(int), typeof(ImportSheet), -1,
        propertyChanged: (bindable, _, _) => ((ImportSheet)bindable).OnActionStateChanged());

    public static readonly BindableProperty IsDuplicateProperty = BindableProperty.Create(
        nameof(IsDuplicate), typeof(bool), typeof(ImportSheet), false,
        propertyChanged: (bindable, _, _) => ((ImportSheet)bindable).OnActionStateChanged());

    public static readonly BindableProperty PreviewTitleProperty = BindableProperty.Create(
        nameof(PreviewTitle), typeof(string), typeof(ImportSheet));

    public static readonly BindableProperty PreviewImageProperty = BindableProperty.Create(
        nameof(PreviewImage), typeof(ImageSource), typeof(ImportSheet),
        propertyChanged: (bindable, _, _) => ((ImportSheet)bindable).OnPropertyChanged(nameof(HasPreviewImage)));

    public ImportSheet()
    {
        InitializeComponent();
        // The inner sheet resets itself to closed when its page leaves the screen; follow it so a page
        // returned to later doesn't keep an invisible "open" overlay (and a back press it swallows).
        Sheet.Unloaded += (_, _) => IsVisible = false;
    }

    /// <summary>The link being offered for import.</summary>
    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    /// <summary>The link's host, shown bold next to the link icon.</summary>
    public string Host
    {
        get => (string)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    /// <summary>Smart Credit balance shown in the cost line ("Uses 1 Smart Credit · you have N").</summary>
    public int Balance
    {
        get => (int)GetValue(BalanceProperty);
        set => SetValue(BalanceProperty, value);
    }

    /// <summary>True when <see cref="Url"/> matches a recipe already saved in the library.</summary>
    public bool IsDuplicate
    {
        get => (bool)GetValue(IsDuplicateProperty);
        set => SetValue(IsDuplicateProperty, value);
    }

    /// <summary>Recipe title shown above the link (in-app search only; null hides it).</summary>
    public string? PreviewTitle
    {
        get => (string?)GetValue(PreviewTitleProperty);
        set => SetValue(PreviewTitleProperty, value);
    }

    /// <summary>Recipe photo shown at the top of the sheet (in-app search only; null hides it).</summary>
    public ImageSource? PreviewImage
    {
        get => (ImageSource?)GetValue(PreviewImageProperty);
        set => SetValue(PreviewImageProperty, value);
    }

    public bool HasPreviewImage => PreviewImage is not null;

    /// <summary>
    /// True for a known zero balance: the sheet then offers Buy Smart Credits instead of Import, so no
    /// entry point ever attempts an import it can't pay for. Unknown (-1) still offers Import.
    /// </summary>
    public bool IsOutOfCredits => ImportResult.IsOutOfCredits(Balance);

    /// <summary>New link with credits (or an unknown balance): Cancel / Import.</summary>
    public bool ShowsImport => !IsDuplicate && !IsOutOfCredits;

    /// <summary>New link with no credits left: Cancel / Buy Smart Credits.</summary>
    public bool ShowsBuyCredits => !IsDuplicate && IsOutOfCredits;

    /// <summary>
    /// The cost line text, kept in sync with <see cref="Balance"/>. Until a real balance is known
    /// (it starts at -1) only the cost is shown, never "you have -1"; at zero it explains why the
    /// primary action is Buy Smart Credits.
    /// </summary>
    public string CostText => Balance switch
    {
        < 0 => AppResources.Get("ImportSheetCost"),
        0 => AppResources.Get("ImportSheetNoCredits"),
        _ => AppResources.Format("ImportSheetCostFormat", Balance),
    };

    /// <summary>Raised when Import / Import again is tapped.</summary>
    public event EventHandler? ImportTapped;

    /// <summary>Raised when Buy Smart Credits is tapped (new link, zero balance); the host opens BuyCreditsPage.</summary>
    public event EventHandler? BuyCreditsTapped;

    /// <summary>Raised when Open it is tapped (duplicate link only).</summary>
    public event EventHandler? OpenExistingTapped;

    /// <summary>Raised when Cancel is tapped, or the sheet is dismissed (backdrop tap / back button).</summary>
    public event EventHandler? CancelTapped;

    public void Show()
    {
        IsVisible = true;
        Sheet.Show();
    }

    public async void Hide()
    {
        await Sheet.HideAsync();
        if (!Sheet.IsOpen)
            IsVisible = false;
    }

    private void OnSheetCancelled(object? sender, EventArgs e) => CancelTapped?.Invoke(this, EventArgs.Empty);

    // The host hides the sheet on the first tap; a second tap landing during the slide-out animation
    // must not start (and pay for) a second import.
    private void OnImportClicked(object? sender, EventArgs e)
    {
        if (!Sheet.IsOpen)
            return;
        ImportTapped?.Invoke(this, EventArgs.Empty);
    }

    private void OnBuyCreditsClicked(object? sender, EventArgs e) => BuyCreditsTapped?.Invoke(this, EventArgs.Empty);

    private void OnActionStateChanged()
    {
        OnPropertyChanged(nameof(CostText));
        OnPropertyChanged(nameof(IsOutOfCredits));
        OnPropertyChanged(nameof(ShowsImport));
        OnPropertyChanged(nameof(ShowsBuyCredits));
    }

    private void OnOpenExistingClicked(object? sender, EventArgs e) => OpenExistingTapped?.Invoke(this, EventArgs.Empty);

    private void OnCancelClicked(object? sender, EventArgs e) => CancelTapped?.Invoke(this, EventArgs.Empty);
}
