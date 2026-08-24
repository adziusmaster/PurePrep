using PurePrep.Services;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace PurePrep;

public partial class SettingsPage : ContentPage
{
    private const string PrivacyUrl = "https://lechdigital.nl/PurePrep/";
    private readonly ThemeService _theme;

    public SettingsPage(ThemeService theme)
    {
        InitializeComponent();
        _theme = theme;

        KeepAwakeSwitch.IsToggled = CookingSettings.KeepScreenAwake;
        VersionLabel.Text = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
        RefreshAppearancePills();
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
