using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Presentation;

namespace PurePrep;

public partial class RecipeDetailPage : ContentPage
{
    private ParsedRecipe _recipe;
    private readonly RecipeLibraryViewModel _library;
    private readonly RecipeDetailViewModel _viewModel;

    public RecipeDetailPage(ParsedRecipe recipe, RecipeLibraryViewModel library)
    {
        InitializeComponent();
        _recipe = recipe;
        _library = library;
        _viewModel = new RecipeDetailViewModel(recipe);
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // The recipe may have been edited on a page pushed above this one. Re-read from the
        // library (which holds the updated immutable instance) and refresh the view.
        var current = _library.Recipes.FirstOrDefault(r => r.Id == _recipe.Id);
        if (current is not null && !ReferenceEquals(current, _recipe))
        {
            _recipe = current;
            _viewModel.SetRecipe(current);
        }
    }

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnEditTapped(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new ManualAddPage(_library, _recipe));

    private async void OnCookClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new FocusPage(_recipe));

    private async void OnDeleteTapped(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlert(
            AppResources.Get("DeleteRecipeTitle"),
            AppResources.Format("DeleteRecipeBodyFormat", _recipe.Title),
            AppResources.Get("Delete"),
            AppResources.Get("Cancel"));
        if (!confirmed)
            return;

        await _library.DeleteRecipeAsync(_recipe);
        await Navigation.PopAsync();
    }
}
