using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity;
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
        // Edge-to-edge on every API level (Android 15+ enforces it at targetSdk 35 anyway): the
        // system bars become transparent and the window draws behind them. This is AndroidX's
        // supported path — no deprecated Window.SetStatusBarColor / SetNavigationBarColor /
        // SetDecorFitsSystemWindows calls of our own. Content insets are applied below.
        EdgeToEdge.Enable(this);

        base.OnCreate(savedInstanceState);

        // base.OnCreate builds the MAUI app, so services are available from here on.
        HandleShareIntent(Intent);

        // Own the hardware/gesture back button ourselves. At targetSdk 35+ Android routes back
        // through the predictive-back OnBackInvokedCallback, which bypasses MAUI's per-page
        // OnBackButtonPressed — that is why back was closing the app from every screen. This
        // dispatcher callback works with both classic and predictive back.
        OnBackPressedDispatcher.AddCallback(this, new BackPolicyCallback(this));

        if (Window is null)
            return;

        // EdgeToEdge picks bar-icon colours from the OS dark mode, but PurePrep has its own
        // appearance setting; the shared ThemeService re-applies them from the app theme and
        // paints the decor background that shows through the transparent bars.
        ApplyNativeBars();

        // The one place insets are handled: pad the content view by the system-bar + cutout
        // insets so no page content sits under the status or navigation bars, then
        // consume them so MAUI's own views don't apply the padding a second time.
        var content = Window.DecorView.FindViewById(Android.Resource.Id.Content);
        if (content is not null)
            ViewCompat.SetOnApplyWindowInsetsListener(content, new SystemBarsInsetsListener());
    }

    // Something else (the photo picker, the camera, the share sheet) may have repainted the bar
    // icons while PurePrep was in the background.
    protected override void OnResume()
    {
        base.OnResume();
        ApplyNativeBars();
    }

    // Copying a link in another app and switching back should offer it on Home. This runs on window
    // focus rather than on resume: Android 10+ denies clipboard reads ("application is not in focus")
    // until the window is focused, which happens after OnResume. Focus also returns when a system
    // overlay closes; ClipboardChange's per-clip timestamp keeps that to one read per clip.
    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus)
            _ = CheckClipboardOnFocusAsync();
    }

    private static async Task CheckClipboardOnFocusAsync()
    {
        try
        {
            if (IPlatformApplication.Current?.Services.GetService<PurePrep.Services.ImportCoordinator>() is { } coordinator)
                await coordinator.OnWindowFocusedAsync();
        }
        catch
        {
            // Best-effort convenience: the chip simply isn't offered.
        }
    }

    private static void ApplyNativeBars() =>
        IPlatformApplication.Current?.Services.GetService<PurePrep.Services.ThemeService>()?.ApplyNativeBars();

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
            // While the window resizes for the keyboard (SheetKeyboard: a bottom sheet is open or a form
            // page asked for it), the keyboard's inset (which already includes the navigation bar) wins
            // so the sheet or the page's sticky button sits above it.
            var bottom = bars.Bottom;
            if (SheetKeyboard.ResizeActive)
                bottom = System.Math.Max(bottom, insets.GetInsets(WindowInsetsCompat.Type.Ime()).Bottom);
            v.SetPadding(bars.Left, bars.Top, bars.Right, bottom);
            return WindowInsetsCompat.Consumed;
        }
    }

    // Central hardware-back policy. Mirrors what users expect on Android: dismiss an open overlay,
    // otherwise navigate back through the MAUI stack, and only on the home screen require a second
    // press (within the window) to actually leave the app.
    private sealed class BackPolicyCallback : OnBackPressedCallback
    {
        private static readonly TimeSpan ExitWindow = TimeSpan.FromSeconds(2);
        private readonly MainActivity _activity;
        private DateTime _lastBackPress = DateTime.MinValue;

        public BackPolicyCallback(MainActivity activity) : base(true) => _activity = activity;

        public override void HandleOnBackPressed()
        {
            var page = Microsoft.Maui.Controls.Application.Current?.Windows is { Count: > 0 } windows
                ? windows[0].Page
                : null;

            if (page is null)
            {
                _activity.Finish();
                return;
            }

            var nav = page.Navigation;

            // 1. A modal page is on top — close it.
            if (nav.ModalStack.Count > 0)
            {
                _ = nav.PopModalAsync();
                return;
            }

            // 2. A styled in-app dialog (alert/prompt/choose) is on top — cancel it.
            if (PurePrep.Services.AppDialog.TryHandleBack())
                return;

            // 3. The visible page has an in-page overlay (buy sheet / upgrade prompt) — let it close.
            var current = (page as Microsoft.Maui.Controls.NavigationPage)?.CurrentPage ?? page;
            if (current is IHardwareBackHandler handler && handler.OnHardwareBack())
                return;

            // 3. A page is pushed on the stack — go back to the previous one.
            if (nav.NavigationStack.Count > 1)
            {
                _ = nav.PopAsync();
                return;
            }

            // 4. We're at the home screen — press back again within the window to exit.
            var now = DateTime.UtcNow;
            if (now - _lastBackPress <= ExitWindow)
            {
                _activity.Finish();
                return;
            }

            _lastBackPress = now;
            Android.Widget.Toast.MakeText(
                _activity,
                PurePrep.Localization.AppResources.Get("PressBackAgainToExit"),
                Android.Widget.ToastLength.Short)?.Show();
        }
    }
}
