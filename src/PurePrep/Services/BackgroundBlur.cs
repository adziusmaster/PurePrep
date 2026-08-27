namespace PurePrep.Services;

/// <summary>
/// Applies (or clears) a native background blur to a page's content view so modal sheets read as
/// "frosted" rather than darkened. Android 12+ (API 31) uses a RenderEffect; on older devices and
/// other platforms this is a no-op and the light scrim alone provides separation.
/// </summary>
public static class BackgroundBlur
{
    public static void Apply(VisualElement? content, bool enabled)
    {
#if ANDROID
        if (content?.Handler?.PlatformView is not Android.Views.View view)
            return;

        if (enabled && Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.S)
        {
            var radius = 18f * (view.Context?.Resources?.DisplayMetrics?.Density ?? 1f);
            view.SetRenderEffect(
                Android.Graphics.RenderEffect.CreateBlurEffect(radius, radius, Android.Graphics.Shader.TileMode.Clamp!));
        }
        else
        {
            view.SetRenderEffect(null);
        }
#endif
    }
}
