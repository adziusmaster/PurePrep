using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Dispatching;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Services;

namespace PurePrep.Presentation;

public sealed class FocusModeViewModel : INotifyPropertyChanged
{
    private readonly IDispatcher? _dispatcher;
    private int _currentStepIndex;
    private bool _showIngredients;
    private bool _keepScreenAwake;

    private IReadOnlyList<StepTimer> _currentStepTimers = Array.Empty<StepTimer>();
    private readonly CookTimerService? _timers;

    public FocusModeViewModel(ParsedRecipe recipe, IDispatcher? dispatcher = null, CookTimerService? timers = null)
    {
        Recipe = recipe;
        _dispatcher = dispatcher;
        _timers = timers;

        Ingredients = recipe.Ingredients
            .Select(text => new CheckableIngredient(text))
            .ToArray();
        _keepScreenAwake = CookingSettings.KeepScreenAwake;
        PreviousCommand = new Command(() => CurrentStepIndex--, () => !IsFirstStep);
        NextCommand = new Command(() => CurrentStepIndex++, () => !IsLastStep);
        AdvanceCommand = new Command(() =>
        {
            Haptic();
            if (IsLastStep)
                Completed?.Invoke(this, EventArgs.Empty);
            else
                CurrentStepIndex++;
        });
        ToggleIngredientsCommand = new Command(() => ShowIngredients = !ShowIngredients);
        StartTimerCommand = new Command<StepTimer>(timer => _ = StartTimerAsync(timer));
        CancelTimerCommand = new Command(() => _ = CancelTimerAsync());
        UpdateCurrentStepTimers();
    }

    /// <summary>Raised when the user finishes the final step.</summary>
    public event EventHandler? Completed;

    public ParsedRecipe Recipe { get; }
    public IReadOnlyList<RecipeStep> Steps => Recipe.Steps;
    /// <summary>
    /// Ingredients with a tick-off state. Losing your place in a list while your hands are busy is
    /// the single most common way cooking from a screen goes wrong.
    /// </summary>
    public IReadOnlyList<CheckableIngredient> Ingredients { get; }
    public bool HasIngredients => Ingredients.Count > 0;
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        private set
        {
            if (value < 0 || value >= Steps.Count || value == _currentStepIndex)
                return;
            _currentStepIndex = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentStep));
            OnPropertyChanged(nameof(StepLabel));
            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(IsFirstStep));
            OnPropertyChanged(nameof(IsLastStep));
            OnPropertyChanged(nameof(PrimaryActionLabel));
            ((Command)PreviousCommand).ChangeCanExecute();
            ((Command)NextCommand).ChangeCanExecute();
            UpdateCurrentStepTimers();
        }
    }

    public bool ShowIngredients
    {
        get => _showIngredients;
        private set
        {
            if (value == _showIngredients) return;
            _showIngredients = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IngredientsToggleLabel));
        }
    }

    /// <summary>Label for the ingredients toggle, flipping to "Step" while the panel is open.</summary>
    public string IngredientsToggleLabel => _showIngredients ? AppResources.Get("Step") : AppResources.Get("Ingredients");

    /// <summary>Toggles (and persists) whether the display is kept on while cooking.</summary>
    public bool KeepScreenAwake
    {
        get => _keepScreenAwake;
        set
        {
            if (value == _keepScreenAwake) return;
            _keepScreenAwake = value;
            CookingSettings.KeepScreenAwake = value;
            DeviceDisplay.Current.KeepScreenOn = value;
            OnPropertyChanged();
        }
    }

    public RecipeStep? CurrentStep => Steps.Count == 0 ? null : Steps[CurrentStepIndex];
    public string StepLabel => Steps.Count == 0
        ? AppResources.Get("NoSteps")
        : AppResources.Format("StepOfFormat", CurrentStepIndex + 1, Steps.Count);
    /// <summary>Completion fraction (0–1) for the progress bar.</summary>
    public double Progress => Steps.Count == 0 ? 0 : (double)(CurrentStepIndex + 1) / Steps.Count;
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => Steps.Count == 0 || CurrentStepIndex == Steps.Count - 1;
    /// <summary>Label for the primary button: advances, or finishes on the last step.</summary>
    public string PrimaryActionLabel => IsLastStep
        ? AppResources.Get("Finish") + "  \u2713"
        : AppResources.Get("Next") + "  \u203A";
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand AdvanceCommand { get; }
    public ICommand ToggleIngredientsCommand { get; }
    public ICommand StartTimerCommand { get; }
    public ICommand CancelTimerCommand { get; }

    // ===== Cook timers =====

    /// <summary>Timers detected in the current step's instruction (e.g. "simmer 20 minutes").</summary>
    public IReadOnlyList<StepTimer> CurrentStepTimers => _currentStepTimers;
    public bool HasStepTimers => _currentStepTimers.Count > 0;

    /// <summary>True while a countdown is active.</summary>
    public bool IsTimerRunning => _timers?.IsRunning ?? false;
    public string ActiveTimerLabel => _timers?.Label ?? string.Empty;

    /// <summary>Remaining time as mm:ss (or h:mm:ss for long timers).</summary>
    public string ActiveTimerDisplay => _timers?.Display ?? string.Empty;

    private void OnTimerTick(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsTimerRunning));
        OnPropertyChanged(nameof(ActiveTimerLabel));
        OnPropertyChanged(nameof(ActiveTimerDisplay));
    }

    private void UpdateCurrentStepTimers()
    {
        _currentStepTimers = StepTimers.Detect(CurrentStep?.Instruction);
        OnPropertyChanged(nameof(CurrentStepTimers));
        OnPropertyChanged(nameof(HasStepTimers));
    }

    private async Task StartTimerAsync(StepTimer? timer)
    {
        if (timer is null || _timers is null)
            return;

        await _timers.StartAsync(timer.Label, timer.TotalSeconds);
    }

    private async Task CancelTimerAsync()
    {
        if (_timers is not null)
            await _timers.StopAsync();
    }

    private static void Haptic()
    {
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch
        {
            // Haptics are best-effort; ignore unsupported devices.
        }
    }

    /// <summary>
    /// Subscribes to the shared timer and re-reads its deadline. Paired with <see cref="Detach"/>
    /// on every appear/disappear cycle — subscribing only once in the constructor left the display
    /// frozen if this page was ever shown a second time.
    /// </summary>
    public void Attach()
    {
        if (_timers is null)
            return;

        _timers.Tick -= OnTimerTick;
        _timers.Tick += OnTimerTick;
        _timers.Resume();
        OnTimerTick(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stops listening to the shared timer. Note it does NOT stop the countdown: a timer
    /// deliberately keeps running when you leave Focus Mode, which is the whole point of moving it
    /// out of this page.
    /// </summary>
    public void Detach()
    {
        if (_timers is not null)
            _timers.Tick -= OnTimerTick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
