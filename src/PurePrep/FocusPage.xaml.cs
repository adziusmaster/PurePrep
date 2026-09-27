using Microsoft.Extensions.DependencyInjection;
using PurePrep.Domain;
using PurePrep.Presentation;

namespace PurePrep;

public partial class FocusPage : ContentPage
{
    private readonly FocusModeViewModel _viewModel;
    private readonly ParsedRecipe _originalRecipe;
    private readonly RecipeLibraryViewModel _library;
    private Domain.RecipeTimer? _editingTimer;

    /// <param name="recipe">The cook copy shown on screen — already unit-converted/scaled/translated.</param>
    /// <param name="originalRecipe">
    /// The pristine, persisted recipe. Marking the recipe cooked applies to THIS one, never the scaled
    /// or translated display copy above — cooking a translated or 2×-scaled version must not silently
    /// overwrite the saved original with a copy in the wrong language or servings.
    /// </param>
    /// <param name="library">Used to persist the "mark as cooked" update once cooking finishes.</param>
    /// <param name="startIndex">
    /// When set, opens directly on this step instead of the first — used by the app-wide timers bar
    /// to jump straight back to the step a running timer belongs to.
    /// </param>
    public FocusPage(ParsedRecipe recipe, ParsedRecipe originalRecipe, RecipeLibraryViewModel library, string? spokenLanguage = null, int? startIndex = null)
    {
        InitializeComponent();
        _originalRecipe = originalRecipe;
        _library = library;
        var timers = IPlatformApplication.Current?.Services.GetService<PurePrep.Services.CookTimerService>();
        var voice = IPlatformApplication.Current?.Services.GetService<PurePrep.Application.IVoiceCommandListener>();
        var readAloud = IPlatformApplication.Current?.Services.GetService<PurePrep.Services.ReadAloudService>();
        _viewModel = new FocusModeViewModel(recipe, Dispatcher, timers, voice, readAloud, spokenLanguage, startIndex);
        _viewModel.Finished += OnFinished;
        _viewModel.EditTimerRequested += OnEditTimerRequested;
        BindingContext = _viewModel;
    }

    /// <summary>
    /// Opens Focus Mode on a saved recipe the same way from every entry point (detail screen, library
    /// card, running-timers bar): displayed translation, the cook's units and remembered servings
    /// (<see cref="CookingCopy.For"/>), read aloud in the language that text is in.
    /// </summary>
    /// <param name="fallbackFactor">The detail screen's ½×/2× choice, used only when the yield is unknown.</param>
    public static FocusPage ForRecipe(ParsedRecipe saved, RecipeLibraryViewModel library, double fallbackFactor = 1d, int? startIndex = null) =>
        new(CookingCopy.For(saved, Services.UnitSettings.Target, fallbackFactor), saved, library,
            CookingCopy.SpokenLanguage(saved) ?? Localization.LocalizationService.EffectiveTwoLetterCode, startIndex);

    // Chips start instantly with no naming prompt; this small pencil target is the only way to
    // rename a timer or adjust a range's minutes before it starts. Pre-fills the sheet and shows it.
    private void OnEditTimerRequested(object? sender, Domain.RecipeTimer timer)
    {
        // From the step's timers sheet: swap it for the edit sheet rather than stacking the two.
        StepTimersSheet.Hide();
        _editingTimer = timer;
        TimerLabelEntry.Text = timer.Label;
        TimerMinutesEntry.Text = Math.Max(1, timer.MinSeconds / 60).ToString();
        TimerEditSheet.Show();
    }

    // "+N more": every timer of the step, with Start (or its countdown) and the pencil per row.
    private void OnMoreTimersTapped(object? sender, EventArgs e) => StepTimersSheet.Show();

    private void OnStepTimersCancelled(object? sender, EventArgs e) => StepTimersSheet.Hide();

    private void OnTimerEditCancelled(object? sender, EventArgs e) => CloseTimerEdit();

    // Unfocus alone leaves the Android keyboard up; hide it explicitly along with the sheet.
    private void CloseTimerEdit()
    {
        foreach (var entry in new[] { TimerLabelEntry, TimerMinutesEntry })
        {
            if (entry.IsSoftInputShowing())
                _ = entry.HideSoftInputAsync(CancellationToken.None);
            entry.Unfocus();
        }
        TimerEditSheet.Hide();
    }

    private async void OnTimerEditStartClicked(object? sender, EventArgs e)
    {
        CloseTimerEdit();

        var label = TimerLabelEntry.Text?.Trim();
        if (string.IsNullOrEmpty(label))
            label = _editingTimer?.Label ?? string.Empty;

        if (!int.TryParse(TimerMinutesEntry.Text, out var minutes) || minutes <= 0)
            minutes = Math.Max(1, (_editingTimer?.MinSeconds ?? 60) / 60);

        // The edit sheet only offers a single minutes value, so an edited range collapses to one
        // duration — adjusting a range further can still be done again from the same pencil target.
        await _viewModel.StartTimerAsync(new Domain.RecipeTimer(label, minutes * 60, minutes * 60), slot: _editingTimer);
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

    // Asks whether to mark the recipe cooked once the last step is finished. Focus Mode closes either
    // way — declining just skips persisting; only the ORIGINAL recipe (never the scaled/translated
    // cook copy) is ever marked cooked.
    private async void OnFinished(object? sender, EventArgs e)
    {
        var markCooked = await Services.AppDialog.ConfirmAsync(this,
            Localization.AppResources.Get("DoneTitle"),
            Localization.AppResources.Get("MarkAsCooked"),
            accept: Localization.AppResources.Get("Ok"),
            cancel: Localization.AppResources.Get("Cancel"));

        // Mark the library's current copy, not the one captured when Focus opened: its photo may have
        // attached, or its notes changed, since then.
        if (markCooked)
            await _library.UpdateRecipeAsync((_library.FindById(_originalRecipe.Id) ?? _originalRecipe).MarkCooked(DateTimeOffset.UtcNow));

        await Navigation.PopAsync();
    }

    private async void OnBackTapped(object? sender, EventArgs e) => await Navigation.PopAsync();

    private void OnOptionsTapped(object? sender, EventArgs e) => OptionsSheet.Show();

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
