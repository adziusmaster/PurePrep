using Microsoft.Extensions.DependencyInjection;
using PurePrep.Application;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Presentation;
using PurePrep.Services;

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

    private async void OnUnitsHintTapped(object? sender, EventArgs e)
    {
        // Same wiring as the home screen's settings button, so the units hint lands users directly
        // on the screen where metric/US can be toggled.
        var services = this.Handler?.MauiContext?.Services;
        var theme = services?.GetService(typeof(ThemeService)) as ThemeService;
        var credits = services?.GetService(typeof(ISmartCreditsClient)) as ISmartCreditsClient;
        var billing = services?.GetService(typeof(IBillingService)) as IBillingService;
        if (theme is not null)
            await Navigation.PushAsync(new SettingsPage(theme, credits, billing));
    }

    private async void OnEditTapped(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new ManualAddPage(_library, _recipe));

    private async void OnTranslateTapped(object? sender, EventArgs e)
    {
        var svc = IPlatformApplication.Current?.Services.GetService<IRecipeTranslator>();
        if (svc is null)
        {
            await DisplayAlert(AppResources.Get("Translate"), AppResources.Get("TranslateUnsupported"),
                AppResources.Get("Ok"));
            return;
        }

        // The original language: prefer what we already recorded, else detect it offline once so we can
        // skip charging when the user picks the language the recipe is already written in.
        var original = _library.FindById(_recipe.Id) ?? _recipe;
        var sourceLanguage = original.OriginalLanguage ?? DetectOriginalLanguage(original);

        // Offer every supported language except the one the recipe is already in. Cached languages are
        // ticked so the user can tell which switches are free.
        var languages = LocalizationService.Supported
            .Where(l => l.Code.Length == 2 && !string.Equals(l.Code, sourceLanguage, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var labelToCode = new Dictionary<string, string>();
        var names = new List<string>();
        foreach (var l in languages)
        {
            var label = original.HasTranslation(l.Code) ? $"{l.NativeName}  \u2713" : l.NativeName;
            labelToCode[label] = l.Code;
            names.Add(label);
        }

        var choice = await DisplayActionSheet(AppResources.Get("TranslateTo"),
            AppResources.Get("Cancel"), null, names.ToArray());
        if (string.IsNullOrEmpty(choice) || !labelToCode.TryGetValue(choice, out var targetCode))
            return;

        // Already cached — switching is free and instant.
        if (original.HasTranslation(targetCode))
        {
            await ApplyRecipeAsync(original, original.WithDisplayLanguage(targetCode));
            return;
        }

        var targetName = languages.First(l => l.Code == targetCode).NativeName;
        var proceed = await DisplayAlert(
            AppResources.Get("Translate"),
            AppResources.Format("TranslateCostFormat", targetName),
            AppResources.Get("TranslateConfirm"),
            AppResources.Get("Cancel"));
        if (!proceed)
            return;

        try
        {
            BusyOverlay.IsVisible = true;
            var translation = await svc.TranslateAsync(original, targetCode);
            var updated = original.WithTranslation(targetCode, translation, sourceLanguage);
            BusyOverlay.IsVisible = false;
            await ApplyRecipeAsync(original, updated);
        }
        catch (InsufficientCreditsException)
        {
            BusyOverlay.IsVisible = false;
            await ShowInsufficientCreditsAsync();
        }
        catch (RecipeImportException ex)
        {
            BusyOverlay.IsVisible = false;
            await DisplayAlert(AppResources.Get("Translate"), ImportErrorText.ForCode(ex.Code), AppResources.Get("Ok"));
        }
        catch (Exception)
        {
            BusyOverlay.IsVisible = false;
            await DisplayAlert(AppResources.Get("Translate"), ImportErrorText.ForCode(ImportErrorCode.Unknown),
                AppResources.Get("Ok"));
        }
        finally
        {
            BusyOverlay.IsVisible = false;
        }
    }

    private async void OnShowOriginalTapped(object? sender, EventArgs e)
    {
        var original = _library.FindById(_recipe.Id) ?? _recipe;
        await ApplyRecipeAsync(original, original.WithDisplayLanguage(null));
    }

    // Persists the new translation state and refreshes the view. Switching languages is free at this
    // layer; the credit (if any) was already charged by the backend translate call.
    private async Task ApplyRecipeAsync(ParsedRecipe current, ParsedRecipe updated)
    {
        await _library.UpdateRecipeAsync(current, updated);
        _recipe = updated;
        _viewModel.SetRecipe(updated);
    }

    private static string? DetectOriginalLanguage(ParsedRecipe recipe)
    {
        var combined = string.Join("\n", new[] { recipe.Title }
            .Concat(recipe.Ingredients)
            .Concat(recipe.Steps.Select(s => s.Instruction)));
        return LanguageHeuristics.Detect(combined);
    }

    private async Task ShowInsufficientCreditsAsync()
    {
        var goToSettings = await DisplayAlert(
            AppResources.Get("Translate"),
            AppResources.Get("TranslateInsufficientCredits"),
            AppResources.Get("BuyCredits"),
            AppResources.Get("Cancel"));
        if (!goToSettings)
            return;

        var services = this.Handler?.MauiContext?.Services;
        var theme = services?.GetService(typeof(ThemeService)) as ThemeService;
        var credits = services?.GetService(typeof(ISmartCreditsClient)) as ISmartCreditsClient;
        var billing = services?.GetService(typeof(IBillingService)) as IBillingService;
        if (theme is not null)
            await Navigation.PushAsync(new SettingsPage(theme, credits, billing));
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
