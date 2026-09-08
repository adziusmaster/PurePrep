using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Dispatching;
using PurePrep.Application;
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
    private bool _readStepsAloud;
    private CancellationTokenSource? _speechCts;

    private IReadOnlyList<StepTimer> _currentStepTimers = Array.Empty<StepTimer>();
    private readonly CookTimerService? _timers;
    private readonly IVoiceCommandListener? _voice;
    private bool _isListening;

    public FocusModeViewModel(ParsedRecipe recipe, IDispatcher? dispatcher = null, CookTimerService? timers = null, IVoiceCommandListener? voice = null)
    {
        Recipe = recipe;
        _dispatcher = dispatcher;
        _timers = timers;
        _voice = voice;
        ActiveTimers = new ObservableCollection<ActiveTimerItem>();

        Ingredients = recipe.Ingredients
            .Select(text => new CheckableIngredient(text))
            .ToArray();
        _keepScreenAwake = CookingSettings.KeepScreenAwake;
        _readStepsAloud = CookingSettings.ReadStepsAloud;
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
        ReadStepCommand = new Command(() => SpeakCurrentStep(force: true));
        ToggleVoiceCommand = new Command(() => _ = ToggleVoiceAsync());
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
            SpeakCurrentStep(force: false);
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

    /// <summary>
    /// Toggles (and persists) reading each step aloud. Turning it on reads the current step at once;
    /// turning it off stops any speech mid-sentence, so it doubles as a hush button.
    /// </summary>
    public bool ReadStepsAloud
    {
        get => _readStepsAloud;
        set
        {
            if (value == _readStepsAloud) return;
            _readStepsAloud = value;
            CookingSettings.ReadStepsAloud = value;
            OnPropertyChanged();

            if (value)
                SpeakCurrentStep(force: true);
            else
                StopSpeaking();
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
    public ICommand ReadStepCommand { get; }
    public ICommand ToggleVoiceCommand { get; }

    // ===== Voice step navigation =====

    /// <summary>True where hands-free voice commands can be offered (mic + recogniser present).</summary>
    public bool IsVoiceSupported => _voice?.IsSupported ?? false;

    /// <summary>True while the microphone is actively listening for "next" / "back" / "repeat".</summary>
    public bool IsListening
    {
        get => _isListening;
        private set
        {
            if (value == _isListening) return;
            _isListening = value;
            OnPropertyChanged();
        }
    }

    private async Task ToggleVoiceAsync()
    {
        if (_voice is null || !_voice.IsSupported)
            return;

        if (_isListening)
        {
            await _voice.StopAsync();
            IsListening = false;
            return;
        }

        // Only flips on if listening actually started — a refused mic permission leaves it off.
        IsListening = await _voice.StartAsync();
    }

    private void OnVoiceCommand(object? sender, VoiceCommand command)
    {
        // Recogniser callbacks may not arrive on the UI thread on every device; marshal to be safe.
        void Apply()
        {
            switch (command)
            {
                case VoiceCommand.Next:
                    if (!IsLastStep) CurrentStepIndex++;
                    break;
                case VoiceCommand.Previous:
                    if (!IsFirstStep) CurrentStepIndex--;
                    break;
                case VoiceCommand.Repeat:
                    SpeakCurrentStep(force: true);
                    break;
            }
        }

        if (_dispatcher is not null && !_dispatcher.IsDispatchRequired)
            Apply();
        else if (_dispatcher is not null)
            _dispatcher.Dispatch(Apply);
        else
            Apply();
    }

    // ===== Read aloud (text-to-speech) =====

    // Speaks the current step. When force is false it only speaks if the read-aloud toggle is on,
    // so it can be called blindly on every step change. Any in-flight speech is cancelled first so
    // stepping quickly through a recipe never leaves a backlog of overlapping instructions.
    private void SpeakCurrentStep(bool force)
    {
        if (!force && !_readStepsAloud)
            return;

        var text = CurrentStep?.Instruction;
        if (string.IsNullOrWhiteSpace(text))
            return;

        StopSpeaking();
        var cts = new CancellationTokenSource();
        _speechCts = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await TextToSpeech.Default.SpeakAsync(text, null, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer step or hushed by the user — expected, nothing to do.
            }
            catch
            {
                // TTS is unavailable or no engine is installed; reading aloud is a bonus, not a
                // requirement, so fail silently rather than disrupt cooking.
            }
        });
    }

    private void StopSpeaking()
    {
        if (_speechCts is null)
            return;

        try
        {
            _speechCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            _speechCts.Dispose();
            _speechCts = null;
        }
    }

    // ===== Cook timers =====

    /// <summary>Timers detected in the current step's instruction (e.g. "simmer 20 minutes").</summary>
    public IReadOnlyList<StepTimer> CurrentStepTimers => _currentStepTimers;
    public bool HasStepTimers => _currentStepTimers.Count > 0;

    /// <summary>
    /// Every timer currently counting down — several can run at once. The overview strip binds to
    /// this, so a roast, a sauce and a rest all stay in view with their own name and countdown.
    /// </summary>
    public ObservableCollection<ActiveTimerItem> ActiveTimers { get; }
    public bool HasActiveTimers => ActiveTimers.Count > 0;

    /// <summary>
    /// Asked (by the page) for a name when a timer is started, pre-filled with the detected label,
    /// so concurrent timers are told apart at a glance. A null result cancels the start; when unset
    /// the detected label is used as-is.
    /// </summary>
    public Func<StepTimer, Task<string?>>? RequestTimerNameAsync { get; set; }

    private void OnTimerTick(object? sender, EventArgs e) => SyncActiveTimers();

    // Reconciles the bound collection with the service's live timer set: refreshes each countdown,
    // drops timers that have stopped/finished, and adds any newly started ones. Editing in place
    // (rather than clearing) keeps the strip from flickering every second.
    private void SyncActiveTimers()
    {
        var live = _timers?.Timers ?? Array.Empty<Domain.CookTimerState>();

        for (var i = ActiveTimers.Count - 1; i >= 0; i--)
        {
            if (live.All(t => t.Id != ActiveTimers[i].Id))
                ActiveTimers.RemoveAt(i);
        }

        foreach (var timer in live)
        {
            var existing = ActiveTimers.FirstOrDefault(t => t.Id == timer.Id);
            var display = _timers?.Display(timer) ?? string.Empty;
            if (existing is null)
                ActiveTimers.Add(new ActiveTimerItem(timer.Id, timer.Label, display, StopTimer));
            else
                existing.Display = display;
        }

        OnPropertyChanged(nameof(HasActiveTimers));
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

        var label = timer.Label;
        if (RequestTimerNameAsync is not null)
        {
            var chosen = await RequestTimerNameAsync(timer);
            if (chosen is null)
                return; // The user cancelled the name prompt.

            chosen = chosen.Trim();
            if (chosen.Length > 0)
                label = chosen;
        }

        await _timers.StartAsync(label, timer.TotalSeconds);
        SyncActiveTimers();
    }

    private void StopTimer(int id)
    {
        if (_timers is not null)
            _ = _timers.StopAsync(id);
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
        if (_voice is not null)
        {
            _voice.CommandRecognized -= OnVoiceCommand;
            _voice.CommandRecognized += OnVoiceCommand;
        }

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
        StopSpeaking();

        if (_voice is not null)
        {
            _voice.CommandRecognized -= OnVoiceCommand;
            if (_isListening)
            {
                _ = _voice.StopAsync();
                IsListening = false;
            }
        }

        if (_timers is not null)
            _timers.Tick -= OnTimerTick;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// One row in the active-timers overview strip: a name, a live countdown, and a stop button. Its
/// <see cref="Display"/> is updated in place every second so the strip does not flicker.
/// </summary>
public sealed class ActiveTimerItem : INotifyPropertyChanged
{
    private string _display;

    public ActiveTimerItem(int id, string label, string display, Action<int> stop)
    {
        Id = id;
        Label = label;
        _display = display;
        StopCommand = new Command(() => stop(id));
    }

    public int Id { get; }
    public string Label { get; }

    public string Display
    {
        get => _display;
        set
        {
            if (value == _display)
                return;
            _display = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Display)));
        }
    }

    public ICommand StopCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
}
