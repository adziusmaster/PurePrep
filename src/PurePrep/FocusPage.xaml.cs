using PurePrep.Domain;
using PurePrep.Presentation;

namespace PurePrep;

public partial class FocusPage : ContentPage
{
    private readonly FocusModeViewModel _viewModel;

    public FocusPage(ParsedRecipe recipe)
    {
        InitializeComponent();
        _viewModel = new FocusModeViewModel(recipe, Dispatcher);
        _viewModel.Completed += OnCompleted;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = _viewModel.KeepScreenAwake;
        SetStatusBarHidden(true);
    }

    protected override void OnDisappearing()
    {
        _viewModel.StopTimers();
        DeviceDisplay.Current.KeepScreenOn = false;
        SetStatusBarHidden(false);
        base.OnDisappearing();
    }

    private async void OnCompleted(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

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
