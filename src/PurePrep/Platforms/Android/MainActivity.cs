using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace PurePrep;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        if (Window is null)
            return;

        // Android 15+ (SDK 35) enforces edge-to-edge: draw behind the now-transparent
        // system bars using the modern WindowCompat API (not the deprecated
        // Window.SetStatusBarColor / SetNavigationBarColor), and keep bar icons light
        // so they read on our dark background.
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        var controller = WindowCompat.GetInsetsController(Window, Window.DecorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = false;
            controller.AppearanceLightNavigationBars = false;
        }

        // Handle insets ourselves: pad the content view by the system-bar + cutout
        // insets so no page content sits under the status or navigation bars, then
        // consume them so MAUI's own views don't apply the padding a second time.
        var content = Window.DecorView.FindViewById(Android.Resource.Id.Content);
        if (content is not null)
            ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsInsetsListener());
    }

    private sealed class SystemBarsInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat OnApplyWindowInsets(Android.Views.View? v, WindowInsetsCompat? insets)
        {
            if (v is null || insets is null)
                return insets ?? WindowInsetsCompat.Consumed;

            var bars = insets.GetInsets(WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout());
            v.SetPadding(bars.Left, bars.Top, bars.Right, bars.Bottom);
            return WindowInsetsCompat.Consumed;
        }
    }
}
