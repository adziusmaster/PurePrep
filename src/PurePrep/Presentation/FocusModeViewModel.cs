using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Dispatching;
using PurePrep.Domain;
using PurePrep.Services;

namespace PurePrep.Presentation;

public sealed class FocusModeViewModel : INotifyPropertyChanged
{
    private readonly IDispatcher? _dispatcher;
    private int _currentStepIndex;
    private bool _showIngredients;
    private bool _keepScreenAwake;

    private IReadOnlyList<StepTimer> _currentStepTimers = Array.Empty<StepTimer>();
    private IDispatcherTimer? _countdown;
    private StepTimer? _activeTimer;
    private int _remainingSeconds;

    public FocusModeViewModel(ParsedRecipe recipe, IDispatcher? dispatcher = null)
    {
        Recipe = recipe;
        _dispatcher = dispatcher;
        _keepScreenAwake = CookingSettings.KeepScreenAwake;
        PreviousCommand = new Command(() => CurrentStepIndex--, () => !IsFirstStep);
        NextCommand = new Command(() => CurrentStepIndex++, () => !IsLastStep);
        AdvanceCommand = new Command(() =>
        {
            if (IsLastStep)
                Completed?.Invoke(this, EventArgs.Empty);
            else
                CurrentStepIndex++;
        });
        ToggleIngredientsCommand = new Command(() => ShowIngredients = !ShowIngredients);
        StartTimerCommand = new Command<StepTimer>(StartTimer);
        CancelTimerCommand = new Command(CancelTimer);
        UpdateCurrentStepTimers();
    }

    /// <summary>Raised when the user finishes the final step.</summary>
    public event EventHandler? Completed;

    public ParsedRecipe Recipe { get; }
    public IReadOnlyList<RecipeStep> Steps => Recipe.Steps;
    public IReadOnlyList<string> Ingredients => Recipe.Ingredients;
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
        }
    }

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
    public string StepLabel => Steps.Count == 0 ? "No steps" : $"STEP {CurrentStepIndex + 1} OF {Steps.Count}";
    /// <summary>Completion fraction (0–1) for the progress bar.</summary>
    public double Progress => Steps.Count == 0 ? 0 : (double)(CurrentStepIndex + 1) / Steps.Count;
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => Steps.Count == 0 || CurrentStepIndex == Steps.Count - 1;
    /// <summary>Label for the primary button: advances, or finishes on the last step.</summary>
    public string PrimaryActionLabel => IsLastStep ? "Finish  \u2713" : "Next  \u203A";
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
    public bool IsTimerRunning => _countdown is not null;
    public string ActiveTimerLabel => _activeTimer?.Label ?? string.Empty;
    /// <summary>Remaining time as mm:ss (or h:mm:ss for long timers).</summary>
    public string ActiveTimerDisplay
    {
        get
        {
            var span = TimeSpan.FromSeconds(_remainingSeconds);
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes:00}:{span.Seconds:00}";
        }
    }

    private void UpdateCurrentStepTimers()
    {
        _currentStepTimers = StepTimers.Detect(CurrentStep?.Instruction);
        OnPropertyChanged(nameof(CurrentStepTimers));
        OnPropertyChanged(nameof(HasStepTimers));
    }

    private void StartTimer(StepTimer? timer)
    {
        if (timer is null || timer.TotalSeconds <= 0)
            return;

        CancelTimer();
        _activeTimer = timer;
        _remainingSeconds = timer.TotalSeconds;

        var dispatcher = _dispatcher ?? Microsoft.Maui.Controls.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        _countdown = dispatcher.CreateTimer();
        _countdown.Interval = TimeSpan.FromSeconds(1);
        _countdown.Tick += OnTick;
        _countdown.Start();

        OnPropertyChanged(nameof(IsTimerRunning));
        OnPropertyChanged(nameof(ActiveTimerLabel));
        OnPropertyChanged(nameof(ActiveTimerDisplay));
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        if (_remainingSeconds <= 0)
        {
            _remainingSeconds = 0;
            OnPropertyChanged(nameof(ActiveTimerDisplay));
            NotifyTimerFinished();
            CancelTimer();
            return;
        }

        OnPropertyChanged(nameof(ActiveTimerDisplay));
    }

    private static void NotifyTimerFinished()
    {
        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(800));
        }
        catch
        {
            // Vibration is best-effort; ignore unsupported devices.
        }
    }

    /// <summary>Stops any running countdown (call when leaving Focus mode).</summary>
    public void StopTimers() => CancelTimer();

    private void CancelTimer()
    {
        if (_countdown is not null)
        {
            _countdown.Stop();
            _countdown.Tick -= OnTick;
            _countdown = null;
        }

        _activeTimer = null;
        OnPropertyChanged(nameof(IsTimerRunning));
        OnPropertyChanged(nameof(ActiveTimerLabel));
        OnPropertyChanged(nameof(ActiveTimerDisplay));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
