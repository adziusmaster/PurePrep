using PurePrep.Presentation;

namespace PurePrep;

public partial class ManualAddPage : ContentPage
{
    private readonly RecipeLibraryViewModel _viewModel;
    private static readonly string[] LineSeparators = { "\r\n", "\r", "\n" };

    public ManualAddPage(RecipeLibraryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
    }

    private async void OnCancelTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim() ?? string.Empty;
        var steps = SplitLines(StepsEditor.Text);

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError("Give your recipe a title.");
            return;
        }

        if (steps.Length == 0)
        {
            ShowError("Add at least one step in the method.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            await _viewModel.SaveManualAsync(title, SplitLines(IngredientsEditor.Text), steps);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError($"Could not save: {ex.Message}");
            SaveButton.IsEnabled = true;
        }
    }

    private static string[] SplitLines(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? Array.Empty<string>()
            : text.Split(LineSeparators, StringSplitOptions.RemoveEmptyEntries)
                  .Select(l => l.Trim())
                  .Where(l => l.Length > 0)
                  .ToArray();

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
