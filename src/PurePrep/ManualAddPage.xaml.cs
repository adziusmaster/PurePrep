using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Presentation;

namespace PurePrep;

public partial class ManualAddPage : ContentPage
{
    private readonly RecipeLibraryViewModel _viewModel;
    private readonly ParsedRecipe? _editing;
    private static readonly string[] LineSeparators = { "\r\n", "\r", "\n" };

    public ManualAddPage(RecipeLibraryViewModel viewModel) : this(viewModel, null)
    {
    }

    public ManualAddPage(RecipeLibraryViewModel viewModel, ParsedRecipe? editing)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _editing = editing;

        if (editing is not null)
        {
            HeaderTitle.Text = AppResources.Get("EditRecipeTitle");
            HeaderSubtitle.Text = AppResources.Get("EditRecipeSubtitle");
            SaveButton.Text = AppResources.Get("SaveChanges");
            TitleEntry.Text = editing.Title;
            IngredientsEditor.Text = string.Join(Environment.NewLine, editing.Ingredients);
            StepsEditor.Text = string.Join(Environment.NewLine, editing.Steps.Select(s => s.Instruction));
        }
    }

    private async void OnCancelTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var title = TitleEntry.Text?.Trim() ?? string.Empty;
        var steps = SplitLines(StepsEditor.Text);

        if (string.IsNullOrWhiteSpace(title))
        {
            ShowError(AppResources.Get("ErrTitleRequired"));
            return;
        }

        if (steps.Length == 0)
        {
            ShowError(AppResources.Get("ErrStepsRequired"));
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var ingredients = SplitLines(IngredientsEditor.Text);
            if (_editing is not null)
                await _viewModel.UpdateManualAsync(_editing, title, ingredients, steps);
            else
                await _viewModel.SaveManualAsync(title, ingredients, steps);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ShowError(AppResources.Format("ErrCouldNotSaveFormat", ex.Message));
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
