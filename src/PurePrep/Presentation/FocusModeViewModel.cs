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
    private bool _isSpeaking;
    private CancellationTokenSource? _speechCts;

    private IReadOnlyList<RecipeTimer> _currentStepTimers = Array.Empty<RecipeTimer>();
    private readonly CookTimerService? _timers;
    private readonly IVoiceCommandListener? _voice;
    private readonly ReadAloudService? _readAloud;
    private readonly string? _spokenLanguage;
    private bool _isListening;
    private bool _canReadAloud;
    private readonly bool _isFirstCook;

    public FocusModeViewModel(ParsedRecipe recipe, IDispatcher? dispatcher = null, CookTimerService? timers = null, IVoiceCommandListener? voice = null, ReadAloudService? readAloud = null, string? spokenLanguage = null, int? startIndex = null)
    {
        Recipe = recipe;
        _dispatcher = dispatcher;
        _timers = timers;
        _voice = voice;
        _readAloud = readAloud;
        _spokenLanguage = string.IsNullOrWhiteSpace(spokenLanguage) ? null : spokenLanguage;
        ActiveTimers = new ObservableCollection<ActiveTimerItem>();
        StepTimerRows = new ObservableCollection<StepTimerRow>();

        _keepScreenAwake = CookingSettings.KeepScreenAwake;
        _readStepsAloud = CookingSettings.ReadStepsAloud;
        // The "Tap Next…" hint is for the very first cook only; it stays up on step 1 of that session.
        _isFirstCook = !CookingSettings.FocusHintSeen;
        if (_isFirstCook)
            CookingSettings.FocusHintSeen = true;
        PreviousCommand = new Command(() => CurrentStepIndex--, () => !IsFirstStep);
        AdvanceCommand = new Command(() =>
        {
            Haptic();
            if (IsLastStep)
                Finished?.Invoke(this, EventArgs.Empty);
            else
                CurrentStepIndex++;
        });
        ToggleIngredientsCommand = new Command(() => ShowIngredients = !ShowIngredients);
        // Chips start instantly, with no naming prompt — the detected/authored label is used as-is.
        // Renaming (or adjusting a range's minutes) is now the small pencil target's job instead.
        StartTimerCommand = new Command<RecipeTimer>(timer => _ = StartTimerAsync(timer));
        EditTimerCommand = new Command<RecipeTimer>(timer =>
        {
            if (timer is not null)
                EditTimerRequested?.Invoke(this, timer);
        });
        ToggleReadCommand = new Command(() =>
        {
            if (IsSpeaking)
                StopSpeaking();
            else
                SpeakCurrentStep(force: true);
        });
        ToggleVoiceCommand = new Command(() => _ = ToggleVoiceAsync());
        UpdateCurrentStepTimers();

        // Opening Focus Mode from the app-wide timers bar lands directly on the step the tapped
        // timer belongs to, rather than always starting from step one.
        if (startIndex is int index && index > 0 && index < Steps.Count)
            CurrentStepIndex = index;
    }

    /// <summary>Raised when the user finishes the final step.</summary>
    public event EventHandler? Finished;

    public ParsedRecipe Recipe { get; }
    public IReadOnlyList<RecipeStep> Steps => Recipe.Steps;
    /// <summary>The recipe's ingredients, plain and read-only — no tick-off state any more.</summary>
    public IReadOnlyList<string> AllIngredients => Recipe.Ingredients;
    public bool HasIngredients => Recipe.Ingredients.Count > 0;
    /// <summary>The cook's own notes, shown on the first step only (see <see cref="ShowNotes"/>).</summary>
    public string? Notes => Recipe.Notes;
    /// <summary>True on the first step only, and only when there are notes to show.</summary>
    public bool ShowNotes => CurrentStepIndex == 0 && !string.IsNullOrWhiteSpace(Notes);
    /// <summary>The "Tap Next when this step is done…" hint: step 1 of the first-ever cook only.</summary>
    public bool ShowFocusHint => _isFirstCook && CurrentStepIndex == 0;
    public int CurrentStepIndex
    {
        get => _currentStepIndex;
        set
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
            OnPropertyChanged(nameof(ShowNotes));
            OnPropertyChanged(nameof(ShowFocusHint));
            ((Command)PreviousCommand).ChangeCanExecute();
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

    /// <summary>
    /// True once we've confirmed an installed voice can speak this recipe's language. The read-aloud
    /// controls stay hidden until then (and forever, if the language has no voice), so the cook is
    /// never offered a feature that would only produce a comically-mispronounced reading.
    /// </summary>
    public bool CanReadAloud
    {
        get => _canReadAloud;
        private set
        {
            if (value == _canReadAloud) return;
            _canReadAloud = value;
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
    public ICommand AdvanceCommand { get; }
    public ICommand ToggleIngredientsCommand { get; }
    public ICommand StartTimerCommand { get; }
    public ICommand EditTimerCommand { get; }
    public ICommand ToggleReadCommand { get; }
    public ICommand ToggleVoiceCommand { get; }

    /// <summary>
    /// The ingredients this step's <see cref="RecipeStep.IngredientRefs"/> point at, resolved to
    /// text. Out-of-range indexes (a corrupt or hand-edited recipe) are skipped rather than crashing
    /// the binding — a missing chip is a far smaller problem than Focus Mode refusing to open.
    /// </summary>
    public IReadOnlyList<string> CurrentStepIngredients =>
        CurrentStep?.IngredientRefs.Where(i => i >= 0 && i < Recipe.Ingredients.Count).Select(i => Recipe.Ingredients[i]).ToArray()
        ?? Array.Empty<string>();
    public bool HasStepIngredients => CurrentStepIngredients.Count > 0;

    // ===== Voice step navigation =====

    /// <summary>
    /// True where hands-free voice commands can be offered: the platform can recognise speech AND we
    /// understand the command words in this recipe's language. Offering it for an unsupported
    /// language would just leave the mic listening for words it can never match.
    /// </summary>
    public bool IsVoiceSupported =>
        (_voice?.IsSupported ?? false) && VoiceCommandVocabulary.IsLanguageSupported(_spokenLanguage ?? DeviceLanguage);

    /// <summary>
    /// A short "say next / back / repeat" hint, with the example words in the recipe's language, so
    /// it is always clear what the cook can actually say.
    /// </summary>
    public string VoiceCommandsHint
    {
        get
        {
            var phrases = VoiceCommandVocabulary.ExamplesFor(_spokenLanguage ?? DeviceLanguage);
            return AppResources.Format("VoiceCommandsHintFormat", phrases.Next, phrases.Previous, phrases.Repeat, phrases.Stop);
        }
    }

    // The app UI language, used as a best-effort fallback when a recipe has no recorded language.
    private static string DeviceLanguage => LocalizationService.EffectiveTwoLetterCode;

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
        if (_voice is null || !IsVoiceSupported)
            return;

        if (_isListening)
        {
            await _voice.StopAsync();
            IsListening = false;
            return;
        }

        // Bias recognition towards the recipe's language, and only flip on if listening actually
        // started — a refused mic permission leaves it off.
        IsListening = await _voice.StartAsync(_spokenLanguage ?? DeviceLanguage);
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
                case VoiceCommand.Stop:
                    StopSpeaking();
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

    /// <summary>
    /// True while a step is actively being spoken. Drives the Read/Stop toggle on the button: tapping
    /// it reads the step when false, or hushes mid-sentence when true.
    /// </summary>
    public bool IsSpeaking
    {
        get => _isSpeaking;
        private set
        {
            if (_isSpeaking == value) return;
            _isSpeaking = value;
            OnPropertyChanged();
        }
    }

    // Speaks the current step. When force is false it only speaks if the read-aloud toggle is on,
    // so it can be called blindly on every step change. Any in-flight speech is cancelled first so
    // stepping quickly through a recipe never leaves a backlog of overlapping instructions.
    private void SpeakCurrentStep(bool force)
    {
        if (!force && !_readStepsAloud)
            return;

        // Never read aloud in a language we have no voice for — that mismatched reading (e.g. a
        // Polish recipe spoken by an English voice) is exactly the problem the gating exists to avoid.
        if (!_canReadAloud)
            return;

        var text = CurrentStep?.Instruction;
        if (string.IsNullOrWhiteSpace(text))
            return;

        StopSpeaking();
        var cts = new CancellationTokenSource();
        _speechCts = cts;
        var language = _spokenLanguage;
        IsSpeaking = true;

        _ = Task.Run(async () =>
        {
            try
            {
                if (_readAloud is not null)
                    await _readAloud.SpeakAsync(text, language, null, cts.Token);
                else
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
            finally
            {
                // Only clear the flag for the session that's still current — StopSpeaking() (or a
                // newer SpeakCurrentStep call) may already have replaced _speechCts, and that newer
                // session's own true/false transitions must win.
                if (ReferenceEquals(_speechCts, cts))
                    SetIsSpeaking(false);
            }
        });
    }

    // Applies IsSpeaking on the UI thread; the finally above runs on the background Task.Run thread.
    private void SetIsSpeaking(bool value)
    {
        void Apply() => IsSpeaking = value;

        if (_dispatcher is null || !_dispatcher.IsDispatchRequired)
            Apply();
        else
            _dispatcher.Dispatch(Apply);
    }

    // Confirms (once) whether an installed voice can read this recipe's language, so the read-aloud
    // controls only appear when they will actually work. Runs when the page attaches.
    private async Task EvaluateReadAloudAsync()
    {
        bool available;
        if (_readAloud is null)
        {
            available = true; // Non-Android / test host: assume the default engine can cope.
        }
        else
        {
            try { available = await _readAloud.IsLanguageAvailableAsync(_spokenLanguage); }
            catch { available = false; }
        }

        // The TTS locale query can resume off the UI thread; apply the result (which raises a
        // binding notification) back on it. We deliberately do NOT begin reading here: opening a
        // recipe should never start talking on its own. Reading only starts when the cook explicitly
        // turns the read-aloud switch on (or taps "Read step"), which is far less startling.
        void Apply()
        {
            CanReadAloud = available;
        }

        if (_dispatcher is null || !_dispatcher.IsDispatchRequired)
            Apply();
        else
            _dispatcher.Dispatch(Apply);
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
            IsSpeaking = false;
        }
    }

    // ===== Cook timers =====

    /// <summary>
    /// The timers to offer for the current step: named ones from the parser when present, otherwise
    /// durations detected in the instruction text (e.g. "simmer 20 minutes"). A range like
    /// "10–12 min" counts down to the minimum before offering "+2 min" up to the maximum.
    /// </summary>
    public IReadOnlyList<RecipeTimer> CurrentStepTimers => _currentStepTimers;
    public bool HasStepTimers => _currentStepTimers.Count > 0;

    /// <summary>
    /// Every timer currently counting down — several can run at once. The overview strip binds to
    /// this, so a roast, a sauce and a rest all stay in view with their own name and countdown.
    /// </summary>
    public ObservableCollection<ActiveTimerItem> ActiveTimers { get; }
    public bool HasActiveTimers => ActiveTimers.Count > 0;

    /// <summary>
    /// Every timer of the current step for the "+N more" sheet, each marked running (with its live
    /// countdown) while a timer started from that slot (<see cref="StepTimerSlot"/>) counts down.
    /// </summary>
    public ObservableCollection<StepTimerRow> StepTimerRows { get; }

    /// <summary>
    /// Raised (for the page) when the small pencil target on a chip is tapped, so it can open the
    /// rename/adjust sheet pre-filled with this timer's label and minutes.
    /// </summary>
    public event EventHandler<RecipeTimer>? EditTimerRequested;

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
            // A range timer sitting at its minimum invites a check rather than showing "00:00": the
            // big countdown swaps to "Check now" and a "+2 min" button appears alongside Stop.
            var showExtend = _timers?.IsAtCheckpoint(timer) ?? false;
            var display = showExtend ? AppResources.Get("CheckNow") : (_timers?.Display(timer) ?? string.Empty);

            if (existing is null)
                ActiveTimers.Add(new ActiveTimerItem(timer.Id, timer.Label, display, timer.CanExtend, showExtend, StopTimer, ExtendTimer));
            else
            {
                existing.Display = display;
                existing.ShowExtend = showExtend;
            }
        }

        OnPropertyChanged(nameof(HasActiveTimers));
        RefreshStepTimerRows();
    }

    // Updates each sheet row's running state in place, so an open sheet ticks without rebuilding.
    private void RefreshStepTimerRows()
    {
        var live = _timers?.Timers ?? Array.Empty<Domain.CookTimerState>();
        for (var i = 0; i < StepTimerRows.Count; i++)
        {
            var row = StepTimerRows[i];
            var running = StepTimerSlot.FindRunning(live, Recipe.Id, CurrentStepIndex, i, row.Timer.Label);
            row.IsRunning = running is not null;
            row.Display = running is null ? string.Empty
                : _timers!.IsAtCheckpoint(running) ? AppResources.Get("CheckNow") : _timers.Display(running);
        }
    }

    private void UpdateCurrentStepTimers()
    {
        _currentStepTimers = CurrentStep is null ? Array.Empty<RecipeTimer>() : StepTimerResolver.Resolve(CurrentStep);
        OnPropertyChanged(nameof(CurrentStepTimers));
        OnPropertyChanged(nameof(HasStepTimers));
        StepTimerRows.Clear();
        foreach (var timer in _currentStepTimers)
            StepTimerRows.Add(new StepTimerRow(timer));
        RefreshStepTimerRows();
        OnPropertyChanged(nameof(CurrentStepIngredients));
        OnPropertyChanged(nameof(HasStepIngredients));
    }

    /// <summary>
    /// Starts <paramref name="timer"/> immediately — no naming prompt. Used both by the chip's tap
    /// (the detected/authored label as-is) and by the edit sheet's Start button (a renamed/adjusted
    /// copy of <paramref name="slot"/>, the step timer it was opened from). A slot that is already
    /// running is not started twice; its countdown is already on screen.
    /// </summary>
    public async Task StartTimerAsync(RecipeTimer? timer, RecipeTimer? slot = null)
    {
        if (timer is null || _timers is null)
            return;

        int? timerIndex = IndexOfStepTimer(slot ?? timer) is var index and >= 0 ? index : null;
        if (timerIndex is int i && StepTimerSlot.FindRunning(_timers.Timers, Recipe.Id, CurrentStepIndex, i, _currentStepTimers[i].Label) is not null)
            return;

        await _timers.StartAsync(timer.Label, timer.MinSeconds, timer.MaxSeconds, Recipe.Id, CurrentStepIndex, timerIndex);
        SyncActiveTimers();
    }

    // By reference first, so two identical timers in one step ("Boil · 5 min" twice) stay separate
    // slots; value equality only as a fallback.
    private int IndexOfStepTimer(RecipeTimer timer)
    {
        for (var i = 0; i < _currentStepTimers.Count; i++)
        {
            if (ReferenceEquals(_currentStepTimers[i], timer))
                return i;
        }
        for (var i = 0; i < _currentStepTimers.Count; i++)
        {
            if (_currentStepTimers[i] == timer)
                return i;
        }
        return -1;
    }

    private void StopTimer(int id)
    {
        if (_timers is not null)
            _ = _timers.StopAsync(id);
    }

    private void ExtendTimer(int id)
    {
        if (_timers is not null)
            _ = _timers.ExtendAsync(id, 120);
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
        _ = EvaluateReadAloudAsync();

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
/// One row in the active-timers overview strip: a name, a live countdown, a stop button and — for a
/// range timer sitting at its minimum — a "+2 min" extend button. <see cref="Display"/> and
/// <see cref="ShowExtend"/> are updated in place every second so the strip does not flicker.
/// </summary>
public sealed class ActiveTimerItem : INotifyPropertyChanged
{
    private string _display;
    private bool _showExtend;

    public ActiveTimerItem(int id, string label, string display, bool canExtend, bool showExtend, Action<int> stop, Action<int> extend)
    {
        Id = id;
        Label = label;
        _display = display;
        CanExtend = canExtend;
        _showExtend = showExtend;
        StopCommand = new Command(() => stop(id));
        ExtendCommand = new Command(() => extend(id));
    }

    public int Id { get; }
    public string Label { get; }

    /// <summary>True for a range timer that has not yet reached its maximum.</summary>
    public bool CanExtend { get; }

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

    /// <summary>True once this range timer has counted down to its minimum and can still be extended
    /// — shows the "+2 min" button alongside Stop.</summary>
    public bool ShowExtend
    {
        get => _showExtend;
        set
        {
            if (value == _showExtend)
                return;
            _showExtend = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowExtend)));
        }
    }

    public ICommand StopCommand { get; }
    public ICommand ExtendCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// One row of the step's timers sheet: the timer itself (label · duration, Start and edit) or, while
/// it runs, its live countdown in place of Start. Updated in place every second.
/// </summary>
public sealed class StepTimerRow(RecipeTimer timer) : INotifyPropertyChanged
{
    private bool _isRunning;
    private string _display = string.Empty;

    public RecipeTimer Timer { get; } = timer;

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (value == _isRunning)
                return;
            _isRunning = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning)));
        }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;
}
