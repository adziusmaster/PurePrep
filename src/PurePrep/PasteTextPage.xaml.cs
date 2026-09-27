using PurePrep.Localization;
using PurePrep.Presentation;

namespace PurePrep;

public partial class PasteTextPage : ContentPage
{
    private readonly RecipeLibraryViewModel _viewModel;

    public PasteTextPage(RecipeLibraryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
    }

    // Resize (not pan) for the keyboard while this page is visible, so the sticky Import button sits
    // just above the keyboard and the form scrolls in the space left (see SheetKeyboard).
    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        SheetKeyboard.RequestPageResize(this);
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        SheetKeyboard.ReleasePageResize(this);
#endif
    }

    private async void OnCancelTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        var text = TextEditor.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            ErrorLabel.Text = AppResources.Get("ErrPasteText");
            ErrorLabel.IsVisible = true;
            return;
        }

        // Pop first, then kick off the import so the busy indicator + any error surface on Home
        // exactly like a URL import does — the parse itself is owned by the view model.
        await Navigation.PopAsync();
        await _viewModel.ImportFromTextAsync(text);
    }
}
