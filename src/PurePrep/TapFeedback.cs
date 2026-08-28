namespace PurePrep;

/// <summary>
/// Gives a tappable element a visible press response.
///
/// Most of PurePrep's controls are a <see cref="Border"/> with a <see cref="TapGestureRecognizer"/>
/// rather than a <see cref="Button"/>, which looks tappable but has no pressed state — taps felt
/// dead. Attaching this animates a brief dip on tap, so every control confirms it was hit.
///
/// Usage: <c>local:TapFeedback.IsEnabled="True"</c> on the element carrying the recognizer.
/// </summary>
public static class TapFeedback
{
    public static readonly BindableProperty IsEnabledProperty = BindableProperty.CreateAttached(
        "IsEnabled", typeof(bool), typeof(TapFeedback), false, propertyChanged: OnIsEnabledChanged);

    public static bool GetIsEnabled(BindableObject view) => (bool)view.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(BindableObject view, bool value) => view.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not View view)
            return;

        foreach (var recognizer in view.GestureRecognizers.OfType<TapGestureRecognizer>())
        {
            recognizer.Tapped -= OnTapped;
            if (newValue is true)
                recognizer.Tapped += OnTapped;
        }
    }

    private static async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not TapGestureRecognizer { Parent: View view })
            return;

        try
        {
            // Short and shallow on purpose: enough to register as a response, not enough to feel
            // like an animation the user has to wait for.
            await view.ScaleTo(0.94, 60, Easing.CubicOut);
            await view.ScaleTo(1.0, 90, Easing.CubicOut);
        }
        catch
        {
            // A view torn down mid-animation (navigation on tap) is not an error worth surfacing.
            view.Scale = 1.0;
        }
    }
}
