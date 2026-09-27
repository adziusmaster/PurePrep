namespace PurePrep.Controls;

/// <summary>The one top bar: back on the left, title, actions on the right. Same on every page.</summary>
public partial class TopBar : ContentView
{
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(nameof(Title), typeof(string), typeof(TopBar));
    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(TopBar));
    public static readonly BindableProperty ShowBackProperty = BindableProperty.Create(nameof(ShowBack), typeof(bool), typeof(TopBar), true);
    public static readonly BindableProperty ActionsProperty = BindableProperty.Create(nameof(Actions), typeof(View), typeof(TopBar));

    public TopBar() => InitializeComponent();

    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Subtitle { get => (string?)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public bool ShowBack { get => (bool)GetValue(ShowBackProperty); set => SetValue(ShowBackProperty, value); }
    public View? Actions { get => (View?)GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    public event EventHandler? BackTapped;

    private async void OnBackTapped(object? sender, EventArgs e)
    {
        if (BackTapped is not null)
            BackTapped(this, EventArgs.Empty);
        else if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();
    }
}
