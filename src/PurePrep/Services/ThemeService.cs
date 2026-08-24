using Microsoft.Maui.Controls;
using PurePrep.Resources.Styles;
using MauiApp = Microsoft.Maui.Controls.Application;

namespace PurePrep.Services;

/// <summary>
/// Owns the app-wide appearance. Swaps the token <see cref="ResourceDictionary"/> that all
/// screens reference through <c>DynamicResource</c>, keeps <see cref="Application.UserAppTheme"/>
/// in sync, and updates the native Android status/navigation bars so nothing renders white.
/// </summary>
public sealed class ThemeService
{
    private const string PreferenceKey = "app_theme_choice";

    private readonly DarkTheme _dark = new();
    private readonly LightTheme _light = new();
    private ResourceDictionary? _active;

    public AppThemeChoice Current { get; private set; }

    public ThemeService()
    {
        Current = (AppThemeChoice)Preferences.Default.Get(PreferenceKey, (int)AppThemeChoice.Dark);

        if (MauiApp.Current is { } app)
            app.RequestedThemeChanged += (_, _) => { if (Current == AppThemeChoice.System) Apply(); };
    }

    public bool IsDarkEffective => ResolveEffective() == AppTheme.Dark;

    /// <summary>Persists and applies a new appearance choice.</summary>
    public void SetTheme(AppThemeChoice choice)
    {
        Current = choice;
        Preferences.Default.Set(PreferenceKey, (int)choice);
        Apply();
    }

    /// <summary>Applies the current choice to app resources, MAUI theme, and native bars.</summary>
    public void Apply()
    {
        if (MauiApp.Current is not { } app)
            return;

        app.UserAppTheme = Current switch
        {
            AppThemeChoice.Light => AppTheme.Light,
            AppThemeChoice.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified
        };

        var effective = ResolveEffective();
        var wanted = effective == AppTheme.Dark ? (ResourceDictionary)_dark : _light;
        if (!ReferenceEquals(_active, wanted))
        {
            if (_active is not null)
                app.Resources.MergedDictionaries.Remove(_active);
            app.Resources.MergedDictionaries.Add(wanted);
            _active = wanted;
        }

        ApplyNativeBars();
    }

    private AppTheme ResolveEffective() => Current switch
    {
        AppThemeChoice.Light => AppTheme.Light,
        AppThemeChoice.Dark => AppTheme.Dark,
        _ => MauiApp.Current?.PlatformAppTheme ?? AppTheme.Dark
    };

    /// <summary>
    /// Paints the window chrome so the status/navigation bar regions match the app background
    /// (killing the white bands seen in light OS mode) and picks readable bar-icon colours.
    /// </summary>
    public void ApplyNativeBars()
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var window = activity?.Window;
        if (window is null)
            return;

        var isDark = IsDarkEffective;
        var bg = isDark
            ? new Android.Graphics.Color(0x0B, 0x0F, 0x0C)
            : new Android.Graphics.Color(0xF4, 0xF5, 0xEF);

        window.DecorView.SetBackgroundColor(bg);

        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is not null)
        {
            // Light bar-icons (white glyphs) on dark chrome; dark glyphs on light chrome.
            controller.AppearanceLightStatusBars = !isDark;
            controller.AppearanceLightNavigationBars = !isDark;
        }
#endif
    }
}
