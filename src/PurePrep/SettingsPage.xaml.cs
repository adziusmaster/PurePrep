using Microsoft.Extensions.DependencyInjection;
using PurePrep.Application;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Services;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace PurePrep;

public partial class SettingsPage : ContentPage
{
    private const string PrivacyUrl = "https://pureprep.lechdigital.nl/privacy";
    private readonly ThemeService _theme;
    private readonly ISmartCreditsClient? _credits;
    private readonly IBillingService? _billing;
    private bool _suppressLanguageEvent;
    private bool _suppressRecipeLanguageEvent;

    // Recipe-language options: index 0 = follow the app language (""), then each concrete language.
    private readonly List<string> _recipeLanguageCodes = new();

    public SettingsPage(ThemeService theme, ISmartCreditsClient? credits = null, IBillingService? billing = null)
    {
        InitializeComponent();
        _theme = theme;
        _credits = credits;
        _billing = billing;

        KeepAwakeSwitch.IsToggled = CookingSettings.KeepScreenAwake;
        VersionLabel.Text = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        // The buy-credits row only works where in-app billing is available (real Android build).
        BuyCreditsCard.IsVisible = _billing?.IsSupported == true;
        RefreshAppearancePills();
        RefreshUnitPills();
        BuildLanguagePicker();
        BuildRecipeLanguagePicker();
    }

    private async void OnBuyCreditsTapped(object? sender, EventArgs e)
    {
        if (_billing is null || _credits is null || !_billing.IsSupported || BuySheet.IsVisible)
            return;

        // Resolve live, tax-inclusive Play prices (falls back to placeholder labels if unavailable),
        // so the displayed price matches what the user is charged at checkout.
        var packs = await _billing.GetPacksAsync();
        if (packs.Count == 0)
            return;

        BuildPackButtons(packs);
        ShowBuySheet(true);
    }

    private void BuildPackButtons(IReadOnlyList<CreditPack> packs)
    {
        BuyPackContainer.Children.Clear();
        foreach (var pack in packs)
        {
            var button = new Button
            {
                Text = AppResources.Format("PackOptionFormat", pack.Credits, pack.DisplayPrice),
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                HeightRequest = 50,
                CornerRadius = 15,
                BackgroundColor = Token("Lime"),
                TextColor = Token("LimeInk"),
            };
            var captured = pack;
            button.Clicked += (_, _) => _ = PurchasePackAsync(captured);
            BuyPackContainer.Children.Add(button);
        }
    }

    private void ShowBuySheet(bool show)
    {
        BuySheetScrim.IsVisible = show;
        BuySheet.IsVisible = show;
        BackgroundBlur.Apply(ContentRoot, show);
    }

    private void OnDismissBuySheet(object? sender, EventArgs e) => ShowBuySheet(false);

    private async Task PurchasePackAsync(CreditPack pack)
    {
        if (_billing is null || _credits is null)
            return;

        SetBuyBusy(true);
        try
        {
            var newBalance = await CreditPurchaseFlow.PurchaseAsync(_billing, _credits, pack.ProductId);
            if (newBalance is null)
                return; // user cancelled the Google purchase sheet

            ShowBuySheet(false);
            await DisplayAlert(
                AppResources.Get("BuyCredits"),
                AppResources.Format("RedeemSuccessFormat", pack.Credits),
                AppResources.Get("Ok"));
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                AppResources.Get("BuyCredits"),
                AppResources.Format("ErrCouldNotPurchaseFormat", ex.Message),
                AppResources.Get("Ok"));
        }
        finally
        {
            SetBuyBusy(false);
        }
    }

    private void SetBuyBusy(bool busy)
    {
        BuyBusyIndicator.IsRunning = busy;
        BuyBusyIndicator.IsVisible = busy;
        BuyPackContainer.IsEnabled = !busy;
    }

    // The buy sheet is an in-page overlay, so the hardware back button should close it rather than
    // pop the page.
    protected override bool OnBackButtonPressed()
    {
        if (BuySheet.IsVisible)
        {
            ShowBuySheet(false);
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void BuildLanguagePicker()
    {
        _suppressLanguageEvent = true;
        LanguagePicker.Items.Clear();

        foreach (var lang in LocalizationService.Supported)
        {
            var label = lang.Code.Length == 0 ? AppResources.Get("LanguageSystem") : lang.NativeName;
            LanguagePicker.Items.Add(label);
        }

        var currentIndex = 0;
        for (var i = 0; i < LocalizationService.Supported.Count; i++)
        {
            if (LocalizationService.Supported[i].Code == LocalizationService.CurrentCode)
            {
                currentIndex = i;
                break;
            }
        }

        LanguagePicker.SelectedIndex = currentIndex;
        _suppressLanguageEvent = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_suppressLanguageEvent)
            return;

        var index = LanguagePicker.SelectedIndex;
        if (index < 0 || index >= LocalizationService.Supported.Count)
            return;

        var code = LocalizationService.Supported[index].Code;
        if (code == LocalizationService.CurrentCode)
            return;

        // Rebuild the whole UI in the new language (localized XAML reads culture at load time).
        // Dispatch so the Picker's change event finishes before its page is torn down.
        Dispatcher.Dispatch(() => (MauiApp.Current as App)?.ApplyLanguageAndReload(code));
    }

    private void BuildRecipeLanguagePicker()
    {
        _suppressRecipeLanguageEvent = true;
        RecipeLanguagePicker.Items.Clear();
        _recipeLanguageCodes.Clear();

        // Index 0: follow the app language.
        RecipeLanguagePicker.Items.Add(AppResources.Get("RecipeLangSameAsApp"));
        _recipeLanguageCodes.Add(string.Empty);

        // Then each concrete language (skip the "System" entry, which has an empty code).
        foreach (var lang in LocalizationService.Supported.Where(l => l.Code.Length > 0))
        {
            RecipeLanguagePicker.Items.Add(lang.NativeName);
            _recipeLanguageCodes.Add(lang.Code);
        }

        var current = RecipeLanguageSettings.CurrentCode;
        var index = _recipeLanguageCodes.IndexOf(current);
        RecipeLanguagePicker.SelectedIndex = index >= 0 ? index : 0;
        _suppressRecipeLanguageEvent = false;
    }

    private void OnRecipeLanguageChanged(object? sender, EventArgs e)
    {
        if (_suppressRecipeLanguageEvent)
            return;

        var index = RecipeLanguagePicker.SelectedIndex;
        if (index < 0 || index >= _recipeLanguageCodes.Count)
            return;

        RecipeLanguageSettings.CurrentCode = _recipeLanguageCodes[index];
    }

    private async void OnRedeemCodeTapped(object? sender, EventArgs e)
    {
        if (_credits is null)
            return;

        var input = await DisplayPromptAsync(
            AppResources.Get("RedeemCode"),
            AppResources.Get("RedeemCodePrompt"),
            accept: AppResources.Get("Redeem"),
            cancel: AppResources.Get("Cancel"),
            placeholder: AppResources.Get("RedeemCodePlaceholder"),
            maxLength: 5,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(input))
            return;

        var result = await _credits.RedeemCodeAsync(input.Trim());
        var message = result.Outcome switch
        {
            PromoRedeemOutcome.Success => AppResources.Format("RedeemSuccessFormat", result.CreditsGranted),
            PromoRedeemOutcome.Revoked => AppResources.Get("RedeemRevoked"),
            PromoRedeemOutcome.Expired => AppResources.Get("RedeemExpired"),
            PromoRedeemOutcome.AlreadyRedeemed => AppResources.Get("RedeemAlready"),
            PromoRedeemOutcome.NetworkError => AppResources.Get("RedeemNetworkError"),
            _ => AppResources.Get("RedeemInvalid"),
        };

        await DisplayAlert(AppResources.Get("RedeemResultTitle"), message, AppResources.Get("Ok"));
    }

    private void OnBackTapped(object? sender, EventArgs e) => _ = Navigation.PopAsync();

    private async void OnExportTapped(object? sender, EventArgs e)
    {
        var repository = IPlatformApplication.Current?.Services.GetService<IRecipeRepository>();
        if (repository is null)
            return;

        var recipes = await repository.GetAllAsync();
        if (recipes.Count == 0)
        {
            await DisplayAlert(AppResources.Get("ExportRecipes"),
                AppResources.Get("NoRecipesToExport"), AppResources.Get("Ok"));
            return;
        }

        // Written to the cache directory and handed straight to the share sheet: the user chooses
        // where it lands (Drive, email, Files), so the app needs no storage permission.
        var path = Path.Combine(FileSystem.CacheDirectory,
            $"pureprep-recipes-{DateTime.Now:yyyy-MM-dd}.json");
        await File.WriteAllTextAsync(path, RecipeBackup.Export(recipes));

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = AppResources.Get("ExportRecipes"),
            File = new ShareFile(path),
        });
    }

    private async void OnImportTapped(object? sender, EventArgs e)
    {
        var repository = IPlatformApplication.Current?.Services.GetService<IRecipeRepository>();
        if (repository is null)
            return;

        FileResult? file;
        try
        {
            file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = AppResources.Get("ImportRecipes"),
            });
        }
        catch (Exception)
        {
            // No picker available on this device/emulator.
            return;
        }

        if (file is null)
            return;

        try
        {
            using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var restored = RecipeBackup.Import(await reader.ReadToEndAsync());

            // Existing recipes keep their place: a restore adds what is missing rather than
            // replacing a library the user may have added to since the backup was taken.
            var existing = (await repository.GetAllAsync()).Select(r => r.Id).ToHashSet();
            var added = 0;
            foreach (var recipe in restored.Where(r => !existing.Contains(r.Id)))
            {
                await repository.SaveAsync(recipe);
                added++;
            }

            await DisplayAlert(AppResources.Get("ImportRecipes"),
                AppResources.Format("ImportedFormat", added), AppResources.Get("Ok"));
        }
        catch (InvalidBackupException)
        {
            await DisplayAlert(AppResources.Get("ImportRecipes"),
                AppResources.Get("ImportFailed"), AppResources.Get("Ok"));
        }
    }

    private async void OnLanguagePacksTapped(object? sender, EventArgs e) =>
        await Navigation.PushAsync(new LanguagePacksPage());

    private void OnSystemTapped(object? sender, EventArgs e) => SelectTheme(AppThemeChoice.System);
    private void OnLightTapped(object? sender, EventArgs e) => SelectTheme(AppThemeChoice.Light);
    private void OnDarkTapped(object? sender, EventArgs e) => SelectTheme(AppThemeChoice.Dark);

    private void SelectTheme(AppThemeChoice choice)
    {
        _theme.SetTheme(choice);
        RefreshAppearancePills();
    }

    private void OnKeepAwakeToggled(object? sender, ToggledEventArgs e) =>
        CookingSettings.KeepScreenAwake = e.Value;

    private void OnUnitsSourceTapped(object? sender, EventArgs e) => SelectUnits(UnitDisplay.Source);
    private void OnUnitsMetricTapped(object? sender, EventArgs e) => SelectUnits(UnitDisplay.Metric);
    private void OnUnitsImperialTapped(object? sender, EventArgs e) => SelectUnits(UnitDisplay.Imperial);

    private void SelectUnits(UnitDisplay display)
    {
        UnitSettings.Display = display;
        RefreshUnitPills();
    }

    private void RefreshUnitPills()
    {
        var current = UnitSettings.Display;
        Apply(UnitsSourcePill, UnitsSourceLabel, current == UnitDisplay.Source);
        Apply(UnitsMetricPill, UnitsMetricLabel, current == UnitDisplay.Metric);
        Apply(UnitsImperialPill, UnitsImperialLabel, current == UnitDisplay.Imperial);

        static void Apply(Border pill, Label label, bool selected)
        {
            pill.BackgroundColor = selected ? Token("Lime") : Colors.Transparent;
            label.TextColor = selected ? Token("LimeInk") : Token("Muted");
        }
    }

    private async void OnPrivacyTapped(object? sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync(PrivacyUrl);
        }
        catch
        {
            // Opening the browser is best-effort; ignore if no handler is available.
        }
    }

    private void RefreshAppearancePills()
    {
        Apply(SystemPill, SystemLabel, _theme.Current == AppThemeChoice.System);
        Apply(LightPill, LightLabel, _theme.Current == AppThemeChoice.Light);
        Apply(DarkPill, DarkLabel, _theme.Current == AppThemeChoice.Dark);

        static void Apply(Border pill, Label label, bool selected)
        {
            pill.BackgroundColor = selected ? Token("Lime") : Colors.Transparent;
            label.TextColor = selected ? Token("LimeInk") : Token("Muted");
        }
    }

    private static Color Token(string key) =>
        MauiApp.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;
}
