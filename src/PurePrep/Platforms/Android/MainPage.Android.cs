namespace PurePrep;

using Android.Graphics;
using Android.OS;
using AView = Android.Views.View;

public partial class MainPage
{
	// Applies (or clears) a native Gaussian blur to the page content behind the upgrade sheet using
	// RenderEffect, so the modal reads as "frosted" rather than darkened. RenderEffect requires
	// Android 12 (API 31); on older devices the light scrim alone is used.
	partial void SetBackgroundBlur(bool enabled)
	{
		if (ContentRoot?.Handler?.PlatformView is not AView view)
			return;

		if (enabled && Build.VERSION.SdkInt >= BuildVersionCodes.S)
		{
			var radius = 18f * (view.Context?.Resources?.DisplayMetrics?.Density ?? 1f);
			view.SetRenderEffect(RenderEffect.CreateBlurEffect(radius, radius, Shader.TileMode.Clamp!));
		}
		else
		{
			view.SetRenderEffect(null);
		}
	}
}
