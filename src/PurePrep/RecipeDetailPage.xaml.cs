using Microsoft.Extensions.DependencyInjection;
using PurePrep.Application;
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

        // The recipe may have been edited on a page pushed above this one. Re-read from the full
        // library rather than the bound Recipes collection: that one is search-filtered, so an edit
        // made while a search was active could leave this page showing a stale copy.
        var current = _library.FindById(_recipe.Id);
        if (current is not null && !ReferenceEquals(current, _recipe))
        {
            _recipe = current;
            _viewModel.SetRecipe(current);
        }
    }

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnEditTapped(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new ManualAddPage(_library, _recipe));

    private async void OnTranslateTapped(object? sender, EventArgs e)
    {
        var svc = IPlatformApplication.Current?.Services.GetService<ITranslationService>();
        if (svc is null || !svc.IsSupported)
        {
            await DisplayAlert(AppResources.Get("Translate"), AppResources.Get("TranslateUnsupported"),
                AppResources.Get("Ok"));
            return;
        }

        var languages = LocalizationService.Supported
            .Where(l => l.Code.Length == 2 && svc.SupportedLanguageCodes.Contains(l.Code))
            .ToList();
        var names = languages.Select(l => l.NativeName).ToArray();

        var choice = await DisplayActionSheet(AppResources.Get("TranslateTo"),
            AppResources.Get("Cancel"), null, names);
        if (string.IsNullOrEmpty(choice))
            return;

        var target = languages.FirstOrDefault(l => l.NativeName == choice);
        if (target is null)
            return;

        try
        {
            if (!await svc.IsModelDownloadedAsync(target.Code))
            {
                var ok = await DisplayAlert(AppResources.Get("Translate"),
                    AppResources.Format("TranslateDownloadPromptFormat", target.NativeName),
                    AppResources.Get("Download"), AppResources.Get("Cancel"));
                if (!ok)
                    return;
            }

            BusyOverlay.IsVisible = true;
            var translated = await svc.TranslateAsync(_recipe, target.Code);

            if (ReferenceEquals(translated, _recipe))
            {
                BusyOverlay.IsVisible = false;
                await DisplayAlert(AppResources.Get("Translate"),
                    AppResources.Format("TranslateAlreadyFormat", target.NativeName), AppResources.Get("Ok"));
                return;
            }

            var saved = await _library.UpdateManualAsync(_recipe, translated.Title,
                translated.Ingredients, translated.Steps.Select(s => s.Instruction));
            _recipe = saved;
            _viewModel.SetRecipe(saved);
        }
        catch (Exception ex)
        {
            await DisplayAlert(AppResources.Get("Translate"),
                AppResources.Format("TranslateFailedFormat", ex.Message), AppResources.Get("Ok"));
        }
        finally
        {
            BusyOverlay.IsVisible = false;
        }
    }

    private async void OnShareTapped(object? sender, EventArgs e)
    {
        // Shares the display copy, so the recipient gets the units and scaling on screen rather
        // than whatever the source site happened to use.
        var text = RecipeBackup.ToPlainText(_viewModel.CookRecipe);
        await Share.Default.RequestAsync(new ShareTextRequest(text, _recipe.Title));
    }

    private async void OnAddToShoppingListTapped(object? sender, EventArgs e)
    {
        var store = IPlatformApplication.Current?.Services.GetService<Services.ShoppingListStore>();
        if (store is null)
            return;

        // Adds the scaled, unit-converted ingredients: doubling a recipe should double the shopping.
        var added = await store.AddAsync(_viewModel.CookRecipe.Ingredients, _recipe.Title);

        await DisplayAlert(
            AppResources.Get("ShoppingList"),
            AppResources.Format("AddedToListFormat", added),
            AppResources.Get("Ok"));
    }

    private async void OnCookClicked(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new FocusPage(_viewModel.CookRecipe));

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
