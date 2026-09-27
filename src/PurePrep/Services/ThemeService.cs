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
    private double _barDim;

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
    /// How far an open bottom sheet shows in the bar bands: 0 = none, 1 = fully (status band dimmed
    /// by the scrim, navigation band in the sheet's surface colour). Driven by <c>BottomSheet</c> in
    /// step with its backdrop fade so there is no undimmed seam at the bars.
    /// </summary>
    public void SetBarDim(double amount)
    {
        _barDim = Math.Clamp(amount, 0, 1);
        ApplyNativeBars();
    }

    /// <summary>
    /// Paints the decor background that shows through the transparent edge-to-edge bars so the
    /// status/navigation bar regions match the app background (killing the white bands seen in
    /// light OS mode) and picks readable bar-icon colours. Called on theme change and on resume;
    /// never touches the deprecated Window.SetStatusBarColor / SetNavigationBarColor.
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

        // An open sheet's backdrop only covers the content view (which is inset from the bars):
        // dim the status band with the same scrim, and continue the docked sheet's surface through
        // the navigation band, both blended in by the sheet's fade.
        var top = bg;
        var bottom = bg;
        if (_barDim > 0 && MauiApp.Current?.Resources is { } resources)
        {
            if (resources.TryGetValue("Scrim", out var s) && s is Color scrim)
                top = Blend(bg, scrim, scrim.Alpha * _barDim);
            if (resources.TryGetValue("Surface", out var f) && f is Color surface)
                bottom = Blend(bg, surface, _barDim);
        }

        if (window.DecorView.Background is not BarBandsDrawable bands)
        {
            bands = new BarBandsDrawable();
            window.DecorView.Background = bands;
        }
        bands.SetColors(top, bottom);

        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is not null)
        {
            // Light bar-icons (white glyphs) on dark chrome; dark glyphs on light chrome.
            controller.AppearanceLightStatusBars = !isDark;
            controller.AppearanceLightNavigationBars = !isDark;
        }
#endif
    }

#if ANDROID
    private static Android.Graphics.Color Blend(Android.Graphics.Color under, Color over, double amount) => new(
        (byte)Math.Round(under.R * (1 - amount) + over.Red * 255 * amount),
        (byte)Math.Round(under.G * (1 - amount) + over.Green * 255 * amount),
        (byte)Math.Round(under.B * (1 - amount) + over.Blue * 255 * amount));
#endif
}
