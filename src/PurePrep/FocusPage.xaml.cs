using Microsoft.Extensions.DependencyInjection;
using PurePrep.Domain;
using PurePrep.Presentation;

namespace PurePrep;

public partial class FocusPage : ContentPage
{
    private readonly FocusModeViewModel _viewModel;

    public FocusPage(ParsedRecipe recipe, string? spokenLanguage = null)
    {
        InitializeComponent();
        var timers = IPlatformApplication.Current?.Services.GetService<PurePrep.Services.CookTimerService>();
        var voice = IPlatformApplication.Current?.Services.GetService<PurePrep.Application.IVoiceCommandListener>();
        var readAloud = IPlatformApplication.Current?.Services.GetService<PurePrep.Services.ReadAloudService>();
        _viewModel = new FocusModeViewModel(recipe, Dispatcher, timers, voice, readAloud, spokenLanguage);
        _viewModel.Completed += OnCompleted;
        _viewModel.RequestTimerNameAsync = PromptTimerNameAsync;
        BindingContext = _viewModel;
    }

    // Lets the cook name a timer as it starts (pre-filled with the detected duration), so several
    // concurrent countdowns stay tellable apart. Returns null when the prompt is dismissed.
    private async Task<string?> PromptTimerNameAsync(Domain.StepTimer timer) =>
        await DisplayPromptAsync(
            Localization.AppResources.Get("TimerNameTitle"),
            Localization.AppResources.Get("TimerNameMessage"),
            accept: Localization.AppResources.Get("Start"),
            cancel: Localization.AppResources.Get("Cancel"),
            initialValue: timer.Label,
            maxLength: 40);

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = _viewModel.KeepScreenAwake;
        SetStatusBarHidden(true);

        // Subscribes and re-reads the deadline: the ticker does not run while backgrounded, so the
        // timer may well have finished since this page was last on screen.
        _viewModel.Attach();
    }

    protected override void OnDisappearing()
    {
        // Deliberately does NOT stop the countdown. A running timer used to die the moment this
        // page went away, which lost a bake if you stepped out to check something. The timer now
        // lives in a shared service; this only detaches this page's listener from it.
        _viewModel.Detach();
        DeviceDisplay.Current.KeepScreenOn = false;
        SetStatusBarHidden(false);
        base.OnDisappearing();
    }

    private async void OnCompleted(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    // Horizontal drag over the step card = step navigation. Tracking the finger (rather than the
    // built-in SwipeGestureRecognizer, which felt laggy and dropped quick flicks) makes it responsive:
    // the card follows the drag, then snaps back and advances once the drag passes a small threshold.
    private double _panTotalX;

    private void OnStepPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        const double commitThreshold = 55;   // px of travel needed to change step
        const double maxDrag = 44;            // clamp the visual follow so the card never flies off

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panTotalX = 0;
                break;

            case GestureStatus.Running:
                // TotalX is cumulative on Android; keep the last value for the commit decision.
                _panTotalX = e.TotalX;
                StepCard.TranslationX = Math.Clamp(e.TotalX, -maxDrag, maxDrag);
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var committed = _panTotalX;
                _panTotalX = 0;
                _ = StepCard.TranslateTo(0, 0, 130, Easing.CubicOut);

                if (committed <= -commitThreshold && _viewModel.NextCommand.CanExecute(null))
                    _viewModel.NextCommand.Execute(null);
                else if (committed >= commitThreshold && _viewModel.PreviousCommand.CanExecute(null))
                    _viewModel.PreviousCommand.Execute(null);
                break;
        }
    }

    // Tapping anywhere on the row toggles it, not just the checkbox — hands are busy while cooking.
    private void OnIngredientTapped(object? sender, EventArgs e)
    {
        if (sender is Element { BindingContext: Presentation.CheckableIngredient ingredient })
            ingredient.IsChecked = !ingredient.IsChecked;
    }

    // Distraction-free cooking: hide the status bar while Focus Mode is on screen.
    private static void SetStatusBarHidden(bool hidden)
    {
#if ANDROID
        var window = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window;
        if (window is null)
            return;

        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is null)
            return;

        var statusBars = AndroidX.Core.View.WindowInsetsCompat.Type.StatusBars();
        if (hidden)
            controller.Hide(statusBars);
        else
            controller.Show(statusBars);
#endif
    }
}
