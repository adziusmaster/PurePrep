namespace PurePrep.Controls;

/// <summary>
/// The shared bottom-sheet chrome behind every modal in the app (AppDialog's alert/confirm/prompt/
/// choose, the Import sheet, Focus Mode's options and timer-edit sheets): a translucent,
/// theme-tinted backdrop that keeps the page visible, and a full-width sheet docked to the bottom
/// with 28px top corners and a grab handle, sliding up on show and down on hide.
/// Place it as the last child of a page's root Grid (spanning every row/column) and put the sheet's
/// body in <see cref="SheetContent"/>. Open sheets form one stack with AppDialog so the hardware back
/// button cancels the top-most one (<see cref="TryCancelTop"/>).
/// </summary>
[ContentProperty(nameof(SheetContent))]
public partial class BottomSheet : ContentView
{
    private const uint FadeMs = 150;
    private const uint SlideMs = 200;

    // Every sheet currently on screen, newest last — the hardware back button cancels the last one.
    private static readonly List<BottomSheet> OpenSheets = new();

    public static readonly BindableProperty SheetContentProperty = BindableProperty.Create(
        nameof(SheetContent), typeof(View), typeof(BottomSheet),
        propertyChanged: (bindable, _, value) => ((BottomSheet)bindable).Presenter.Content = value as View);

    public static readonly BindableProperty DismissOnBackdropTapProperty = BindableProperty.Create(
        nameof(DismissOnBackdropTap), typeof(bool), typeof(BottomSheet), true);

    public BottomSheet()
    {
        InitializeComponent();
        // A sheet removed from the tree while open (page torn down) must not linger in the back stack,
        // and must come back closed: a page returned to later would otherwise think the sheet is still
        // up (IsOpen) while nothing is on screen, so the next Show() would be a no-op.
        Unloaded += (_, _) =>
        {
            var wasOpen = IsOpen;
            IsOpen = false;
            Backdrop.CancelAnimations();
            Sheet.CancelAnimations();
            Backdrop.Opacity = 0;
            Sheet.TranslationY = Sheet.Height > 0 ? Sheet.Height + 40 : 900;
            IsVisible = false;
            OpenSheets.Remove(this);
            UpdateKeyboardMode();
            if (OpenSheets.Count == 0)
            {
                _barDimAnimator?.AbortAnimation(BarDimAnimation);
                SetBarDim(0);
            }
            // Let the owner settle as if cancelled, so an awaited dialog completes instead of hanging.
            if (wasOpen)
                Cancel();
        };
    }

    /// <summary>The sheet's body, shown under the grab handle.</summary>
    public View? SheetContent
    {
        get => (View?)GetValue(SheetContentProperty);
        set => SetValue(SheetContentProperty, value);
    }

    /// <summary>When true (the default) a tap on the backdrop cancels the sheet.</summary>
    public bool DismissOnBackdropTap
    {
        get => (bool)GetValue(DismissOnBackdropTapProperty);
        set => SetValue(DismissOnBackdropTapProperty, value);
    }

    /// <summary>True while the sheet is shown (or animating in).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// Raised when the user dismisses the sheet without choosing anything — backdrop tap or the
    /// hardware back button. The owner decides what cancel means and normally calls <see cref="HideAsync"/>.
    /// </summary>
    public event EventHandler? Cancelled;

    /// <summary>True while at least one bottom sheet is on screen.</summary>
    public static bool AnyOpen => OpenSheets.Count > 0;

    /// <summary>Cancels the top-most open sheet; returns whether there was one to cancel.</summary>
    public static bool TryCancelTop()
    {
        if (OpenSheets.Count == 0)
            return false;
        OpenSheets[^1].Cancel();
        return true;
    }

    /// <summary>Raises <see cref="Cancelled"/>, exactly as a backdrop tap or back press would.</summary>
    public void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    /// <summary>Fire-and-forget <see cref="ShowAsync"/> for XAML-hosted sheets.</summary>
    public void Show() => _ = ShowAsync();

    /// <summary>Fire-and-forget <see cref="HideAsync"/> for XAML-hosted sheets.</summary>
    public void Hide() => _ = HideAsync();

    /// <summary>Fades the backdrop in and slides the sheet up.</summary>
    public async Task ShowAsync()
    {
        if (IsOpen)
            return;
        IsOpen = true;
        OpenSheets.Remove(this);
        OpenSheets.Add(this);
        UpdateKeyboardMode();
        AnimateBarDim(1, Easing.CubicOut);

        Backdrop.CancelAnimations();
        Sheet.CancelAnimations();
        Backdrop.Opacity = 0;
        // Before the first layout Height is unknown; start well below any sheet's height instead.
        Sheet.TranslationY = Sheet.Height > 0 ? Sheet.Height + 40 : 900;
        IsVisible = true;

        await Task.WhenAll(
            Backdrop.FadeToAsync(1, FadeMs, Easing.CubicOut),
            Sheet.TranslateToAsync(0, 0, SlideMs, Easing.CubicOut));
    }

    /// <summary>Slides the sheet down and fades the backdrop out, then hides the control.</summary>
    public async Task HideAsync()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        OpenSheets.Remove(this);
        UpdateKeyboardMode();
        if (OpenSheets.Count == 0)
            AnimateBarDim(0, Easing.CubicIn);

        Backdrop.CancelAnimations();
        Sheet.CancelAnimations();
        await Task.WhenAll(
            Backdrop.FadeToAsync(0, FadeMs, Easing.CubicIn),
            Sheet.TranslateToAsync(0, Sheet.Height > 0 ? Sheet.Height + 40 : 900, SlideMs, Easing.CubicIn));

        // A ShowAsync may have started while we were animating out; only hide if still closed.
        if (!IsOpen)
            IsVisible = false;
    }

    // While any sheet is open the window resizes for the keyboard so the docked sheet stays above
    // it; pages otherwise keep the default pan behaviour (see SheetKeyboard).
    private static void UpdateKeyboardMode()
    {
#if ANDROID
        SheetKeyboard.SetSheetOpen(OpenSheets.Count > 0);
#endif
    }

    // The app is edge-to-edge but its content is inset from the status/navigation bars, so the
    // backdrop can't reach behind them; ThemeService tints the bands instead (status band dimmed,
    // navigation band continuing the sheet), faded in step with the backdrop. One level for all sheets.
    private const string BarDimAnimation = "BarDim";
    private static double _barDim;
    private static BottomSheet? _barDimAnimator;

    private void AnimateBarDim(double to, Easing easing)
    {
        _barDimAnimator?.AbortAnimation(BarDimAnimation);
        _barDimAnimator = this;
        new Animation(SetBarDim, _barDim, to).Commit(this, BarDimAnimation, length: FadeMs, easing: easing);
    }

    private static void SetBarDim(double value)
    {
        _barDim = value;
#if ANDROID
        IPlatformApplication.Current?.Services.GetService<Services.ThemeService>()?.SetBarDim(value);
#endif
    }

    private void OnBackdropTapped(object? sender, TappedEventArgs e)
    {
        if (DismissOnBackdropTap)
            Cancel();
    }
}
