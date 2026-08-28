using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using Microsoft.Extensions.DependencyInjection;

namespace PurePrep;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Lets PurePrep appear in the Android share sheet. Sharing a recipe link from a browser, a chat
// app or a social app is how people actually encounter recipes; without this the only way in is to
// copy the link, leave the app you were in, open PurePrep and paste.
[IntentFilter(new[] { Android.Content.Intent.ActionSend },
    Categories = new[] { Android.Content.Intent.CategoryDefault },
    DataMimeType = "text/plain")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // base.OnCreate builds the MAUI app, so services are available from here on.
        HandleShareIntent(Intent);

        if (Window is null)
            return;

        // Android 15+ (SDK 35) enforces edge-to-edge: draw behind the now-transparent
        // system bars using the modern WindowCompat API (not the deprecated
        // Window.SetStatusBarColor / SetNavigationBarColor).
        WindowCompat.SetDecorFitsSystemWindows(Window, false);

        // The window/decor background and bar-icon colours are theme-dependent; let the
        // shared ThemeService paint them so they match whichever appearance is active
        // (this is what removes the white status/navigation bands in light OS mode).
        IPlatformApplication.Current?.Services.GetService<PurePrep.Services.ThemeService>()?.ApplyNativeBars();

        // Handle insets ourselves: pad the content view by the system-bar + cutout
        // insets so no page content sits under the status or navigation bars, then
        // consume them so MAUI's own views don't apply the padding a second time.
        var content = Window.DecorView.FindViewById(Android.Resource.Id.Content);
        if (content is not null)
            ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsInsetsListener());
    }

    // LaunchMode is SingleTop, so a share arriving while PurePrep is already open is delivered
    // here rather than creating a second activity.
    protected override void OnNewIntent(Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleShareIntent(intent);
    }

    private static void HandleShareIntent(Android.Content.Intent? intent)
    {
        if (intent?.Action != Android.Content.Intent.ActionSend)
            return;

        var relay = IPlatformApplication.Current?.Services.GetService<PurePrep.Services.SharedUrlRelay>();
        if (relay is null)
            return;

        var shared = intent.GetStringExtra(Android.Content.Intent.ExtraText);
        var url = PurePrep.Domain.SharedText.ExtractUrl(shared);

        if (url is null)
            relay.PublishEmpty();
        else
            relay.Publish(url);
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
