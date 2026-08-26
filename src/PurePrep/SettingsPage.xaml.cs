using PurePrep.Application;
using PurePrep.Localization;
using PurePrep.Services;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace PurePrep;

public partial class SettingsPage : ContentPage
{
    private const string PrivacyUrl = "https://lechdigital.nl/PurePrep/";
    private readonly ThemeService _theme;
    private readonly ISmartCreditsClient? _credits;
    private bool _suppressLanguageEvent;
    private bool _suppressRecipeLanguageEvent;

    // Recipe-language options: index 0 = follow the app language (""), then each concrete language.
    private readonly List<string> _recipeLanguageCodes = new();

    public SettingsPage(ThemeService theme, ISmartCreditsClient? credits = null)
    {
        InitializeComponent();
        _theme = theme;
        _credits = credits;

        KeepAwakeSwitch.IsToggled = CookingSettings.KeepScreenAwake;
        VersionLabel.Text = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        RefreshAppearancePills();
        RefreshUnitPills();
        BuildLanguagePicker();
        BuildRecipeLanguagePicker();
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
