using System.Linq;
using PurePrep.Localization;
using PurePrep.Presentation;
using PurePrep.Services;

namespace PurePrep.Controls;

/// <summary>
/// App-wide running-timers bar, hosted at the bottom of Home, the recipe detail page and Settings.
/// Shows up to two running timers as "Label mm:ss", plus "+N" for any more, and jumps back into Focus
/// Mode — at the step the tapped timer was started from — when tapped.
/// </summary>
public partial class TimersBar : ContentView
{
    public static readonly BindableProperty SummaryProperty = BindableProperty.Create(
        nameof(Summary), typeof(string), typeof(TimersBar), string.Empty);

    private readonly CookTimerService? _timers;

    public TimersBar()
    {
        InitializeComponent();
        _timers = IPlatformApplication.Current?.Services.GetService<CookTimerService>();
        if (_timers is not null)
            _timers.Tick += OnTick;
        Refresh();
    }

    /// <summary>
    /// The library to resolve a tapped timer's recipe from. Set by the host page (Home,
    /// RecipeDetailPage, Settings) right after construction — a fresh, unloaded VM resolved from DI
    /// here would have no recipes to find.
    /// </summary>
    public RecipeLibraryViewModel? Library { get; set; }

    public string Summary
    {
        get => (string)GetValue(SummaryProperty);
        private set => SetValue(SummaryProperty, value);
    }

    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);

        // The page/control tree is being torn down (navigated away from, or replaced) — stop
        // listening to the shared singleton service so this instance can be collected.
        if (args.NewHandler is null && _timers is not null)
            _timers.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        var timers = _timers?.Timers ?? Array.Empty<Domain.CookTimerState>();
        IsVisible = timers.Count > 0;
        if (timers.Count == 0)
            return;

        // A timer sitting at a range's checkpoint reads "00:00" forever until the cook acts on it in
        // Focus Mode — show the same localized "Check now" text the active-timer row uses instead.
        var shown = timers.Take(2).Select(t =>
        {
            var display = _timers!.IsAtCheckpoint(t) ? AppResources.Get("CheckNow") : _timers.Display(t);
            return $"{t.Label} {display}";
        });
        var summary = string.Join(" · ", shown);
        if (timers.Count > 2)
            summary += $" +{timers.Count - 2}";

        Summary = summary;
    }

    private async void OnTapped(object? sender, EventArgs e)
    {
        // Only among the (up to two) timers actually named in the summary text — a third or later
        // timer with a RecipeId shouldn't silently hijack the tap for a bar that doesn't mention it.
        var timer = (_timers?.Timers ?? Array.Empty<Domain.CookTimerState>())
            .Take(2)
            .FirstOrDefault(t => t.RecipeId is not null);
        if (timer is null || Library is null)
            return;

        var recipe = Library.FindById(timer.RecipeId!.Value);
        if (recipe is null)
            return;

        await Navigation.PushAsync(FocusPage.ForRecipe(recipe, Library, startIndex: timer.StepIndex));
    }
}
