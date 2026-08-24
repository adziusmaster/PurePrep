using PurePrep.Domain;
using PurePrep.Presentation;

namespace PurePrep;

public partial class RecipeDetailPage : ContentPage
{
    private readonly ParsedRecipe _recipe;
    private readonly RecipeLibraryViewModel _library;

    public RecipeDetailPage(ParsedRecipe recipe, RecipeLibraryViewModel library)
    {
        InitializeComponent();
        _recipe = recipe;
        _library = library;
        BindingContext = recipe;
    }

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnCookClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new FocusPage(_recipe));

    private async void OnDeleteTapped(object? sender, EventArgs e)
    {
        var confirmed = await DisplayAlert("Delete recipe",
            $"Remove \u201c{_recipe.Title}\u201d from your library?", "Delete", "Cancel");
        if (!confirmed)
            return;

        await _library.DeleteRecipeAsync(_recipe);
        await Navigation.PopAsync();
    }
}
