using Microsoft.Extensions.DependencyInjection;
using PurePrep.Domain;
using PurePrep.Presentation;

namespace PurePrep;

public partial class FocusPage : ContentPage
{
    private readonly FocusModeViewModel _viewModel;

    public FocusPage(ParsedRecipe recipe)
    {
        InitializeComponent();
        var timers = IPlatformApplication.Current?.Services.GetService<PurePrep.Services.CookTimerService>();
        _viewModel = new FocusModeViewModel(recipe, Dispatcher, timers);
        _viewModel.Completed += OnCompleted;
        BindingContext = _viewModel;
    }

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
