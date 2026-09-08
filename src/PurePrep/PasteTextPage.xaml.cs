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
