using PurePrep.Localization;
using PurePrep.Services;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace PurePrep;

public partial class SettingsPage : ContentPage
{
    private const string PrivacyUrl = "https://lechdigital.nl/PurePrep/";
    private readonly ThemeService _theme;
    private bool _suppressLanguageEvent;

    public SettingsPage(ThemeService theme)
    {
        InitializeComponent();
        _theme = theme;

        KeepAwakeSwitch.IsToggled = CookingSettings.KeepScreenAwake;
        VersionLabel.Text = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        RefreshAppearancePills();
        BuildLanguagePicker();
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

    private void OnBackTapped(object? sender, EventArgs e) => _ = Navigation.PopAsync();

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
