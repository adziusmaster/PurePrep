namespace PurePrep.Controls;

/// <summary>
/// A step instruction that fits its space: steps the font down from <see cref="MaxFontSize"/> to
/// <see cref="MinFontSize"/> until the text fits the available height, and falls back to scrolling
/// with a visible overflow cue when even the minimum size is too big.
/// </summary>
public partial class StepInstructionView : ContentView
{
    private const double MaxFontSize = 34;
    private const double MinFontSize = 22;
    private const double FontStep = 2;

    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(StepInstructionView),
        propertyChanged: (b, _, _) => ((StepInstructionView)b).Refit());

    public StepInstructionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string? Text { get => (string?)GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.RequestedThemeChanged += OnThemeChanged;
        UpdateFadeBrush();
        Refit();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (Microsoft.Maui.Controls.Application.Current is { } app)
            app.RequestedThemeChanged -= OnThemeChanged;
    }

    // The theme dictionaries swap on the same event, so read the new Surface colour afterwards.
    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e) => Dispatcher.Dispatch(UpdateFadeBrush);

    private void OnViewportSizeChanged(object? sender, EventArgs e) => Refit();

    // Rotation, the keyboard, a new step in a recycled carousel card, or a translated/scaled text all
    // land here. Font scale (accessibility) is honoured because the platform does the measuring.
    private void Refit()
    {
        TextLabel.Text = Text;
        var width = Scroller.Width;
        var height = Scroller.Height;
        if (width <= 0 || height <= 0 || string.IsNullOrEmpty(Text))
        {
            TextLabel.FontSize = MaxFontSize;
            OverflowCue.IsVisible = false;
            return;
        }

        var size = MaxFontSize;
        while (true)
        {
            TextLabel.FontSize = size;
            var needed = TextLabel.Measure(width, double.PositiveInfinity).Height;
            if (needed <= height)
            {
                OverflowCue.IsVisible = false;
                break;
            }
            if (size - FontStep < MinFontSize)
            {
                OverflowCue.IsVisible = true;
                break;
            }
            size -= FontStep;
        }
        _ = Scroller.ScrollToAsync(0, 0, false);
    }

    // The cue hides once the last line is in view.
    private void OnScrolled(object? sender, ScrolledEventArgs e)
    {
        var remaining = Scroller.ContentSize.Height - Scroller.Height - e.ScrollY;
        OverflowCue.IsVisible = remaining > 4;
    }

    private async void OnCueTapped(object? sender, TappedEventArgs e) =>
        await Scroller.ScrollToAsync(0, Scroller.ScrollY + Scroller.Height * 0.8, true);

    // Card background → transparent, built from the live Surface token so it matches either theme.
    private void UpdateFadeBrush()
    {
        var surface = Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue("Surface", out var value) == true && value is Color c
            ? c : Colors.White;
        Fade.Background = new LinearGradientBrush(
            [new GradientStop(surface.WithAlpha(0), 0f), new GradientStop(surface.WithAlpha(0.92f), 0.55f), new GradientStop(surface, 1f)],
            new Point(0, 0), new Point(0, 1));
    }
}
