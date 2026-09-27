using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Services;

namespace PurePrep.Presentation;

/// <summary>
/// Wraps a saved recipe for the detail screen. Applies the user's unit preference to a display
/// copy of the recipe, then live serving-size scaling to both the ingredient list and the amounts
/// embedded in the method steps via <see cref="RecipeScaling"/>.
/// </summary>
public sealed class RecipeDetailViewModel : INotifyPropertyChanged
{
    private ParsedRecipe _recipe;
    private ParsedRecipe _active;
    private ParsedRecipe _display;
    private double _factor = 1.0;
    private string? _notes;

    public RecipeDetailViewModel(ParsedRecipe recipe)
    {
        _recipe = recipe;
        _active = recipe.Displayed();
        _display = RecipeUnits.ForDisplay(_active);
        _notes = recipe.Notes;

        ScaleOptions = new ObservableCollection<ScaleOption>
        {
            new(this, "\u00BD\u00D7", 0.5),
            new(this, "1\u00D7", 1.0),
            new(this, "2\u00D7", 2.0),
            new(this, "3\u00D7", 3.0),
        };
        SelectScaleCommand = new Command<ScaleOption>(opt => { if (opt is not null) Factor = opt.Factor; });
        IncreaseServingsCommand = new Command(() => SetServings(CurrentServings + 1));
        DecreaseServingsCommand = new Command(() => SetServings(CurrentServings - 1));
        ResetServingsCommand = new Command(() => SetServings(OriginalServings ?? 1));
        ToggleFavouriteCommand = new Command(() => IsFavourite = !IsFavourite);
        ToggleStatusCommand = new Command(() =>
        {
            _recipe = _recipe.Status == RecipeStatus.Cooked
                ? _recipe.WithStatus(RecipeStatus.WantToCook)
                : _recipe.MarkCooked(DateTimeOffset.UtcNow);
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(IsCooked));
            Persist();
        });

        // Applies servings remembered from a previous visit so the ingredient list and method steps
        // come up already scaled, instead of only scaling once the stepper is touched again.
        _factor = ServingsScale.Factor(OriginalServings, CurrentServings);
        RebuildDisplay();
    }

    public ParsedRecipe Recipe => _recipe;

    /// <summary>The recipe with units already converted to the user's preference.</summary>
    public ParsedRecipe DisplayRecipe => _display;

    /// <summary>
    /// The recipe as it should be cooked: displayed translation, units converted <b>and</b> the chosen
    /// servings applied — built by <see cref="CookingCopy.For"/>, the same helper the library card and the
    /// timers bar use, so Focus Mode shows the same quantities whichever way it was opened. With an
    /// unknown yield the ½×/2× <see cref="Factor"/> applies instead.
    /// </summary>
    public ParsedRecipe CookRecipe => CookingCopy.For(_recipe, UnitSettings.Target, _factor);

    /// <summary>
    /// The language the recipe's text is currently in, for reading aloud and voice commands: the
    /// displayed translation when one is shown, otherwise the recipe's original language, falling
    /// back to the app UI language when neither is known (legacy recipes).
    /// </summary>
    public string SpokenLanguageCode =>
        CookingCopy.SpokenLanguage(_recipe) ?? LocalizationService.EffectiveTwoLetterCode;

    public string Title => _active.Title;
    public int StepCount => _active.StepCount;
    public int IngredientCount => _active.IngredientCount;
    public bool HasIngredients => _active.HasIngredients;

    /// <summary>True when a cached translation (not the original) is currently displayed.</summary>
    public bool IsTranslated => !string.IsNullOrEmpty(_recipe.DisplayLanguage)
        && _recipe.HasTranslation(_recipe.DisplayLanguage);

    /// <summary>Native name of the language on screen, e.g. "Deutsch" — for the "translated" badge.</summary>
    public string TranslatedLanguageName => IsTranslated
        ? LocalizationService.Supported.FirstOrDefault(l => l.Code == _recipe.DisplayLanguage)?.NativeName
            ?? _recipe.DisplayLanguage!
        : string.Empty;

    /// <summary>Badge caption shown when a translation is displayed ("Translated to Deutsch").</summary>
    public string TranslatedBadge => IsTranslated
        ? AppResources.Format("TranslatedBadgeFormat", TranslatedLanguageName)
        : string.Empty;

    /// <summary>Unit-converted method steps, with amounts scaled to the chosen serving multiplier.</summary>
    public ObservableCollection<RecipeStep> DisplaySteps { get; } = new();

    /// <summary>Origin domain (e.g. "jamieoliver.com") when the recipe was imported from a link.</summary>
    public string SourceHost => TryGetHost(_recipe.SourceUrl);
    public bool HasSource => !string.IsNullOrEmpty(SourceHost);

    /// <summary>Full origin link, used by the detail page to open or copy the source.</summary>
    public string? SourceUrl => _recipe.SourceUrl;

    private static string TryGetHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host
            : string.Empty;
    }

    public ObservableCollection<string> ScaledIngredients { get; } = new();
    public ObservableCollection<ScaleOption> ScaleOptions { get; }
    public ICommand SelectScaleCommand { get; }

    /// <summary>The recipe's own servings count, detected from its text or set explicitly. Null when unknown.</summary>
    public int? OriginalServings => ServingsScale.OriginalServings(_recipe);

    /// <summary>True when a people-based yield is known, so the <c>ServingsStepper</c> can be shown.</summary>
    public bool HasKnownServings => OriginalServings is not null;

    /// <summary>The servings currently shown: the remembered choice, or the recipe's own yield, or 1.</summary>
    public int CurrentServings => _recipe.ChosenServings ?? OriginalServings ?? 1;

    /// <summary>True once the cook has scaled away from the recipe's own servings (shows the \u21BA reset).</summary>
    public bool IsServingsChanged => HasKnownServings && CurrentServings != OriginalServings;

    /// <summary>True when the servings were model-estimated rather than stated \u2014 shown as "~4".</summary>
    public bool ServingsEstimated => _recipe.ServingsEstimated && !IsServingsChanged;

    /// <summary>The noun shown after the number ("people" by default, or the recipe's own e.g. "pancakes").</summary>
    public string ServingsNounText =>
        string.IsNullOrWhiteSpace(_recipe.ServingsNoun) ? AppResources.Get("People") : _recipe.ServingsNoun!;

    public ICommand IncreaseServingsCommand { get; }
    public ICommand DecreaseServingsCommand { get; }
    public ICommand ResetServingsCommand { get; }

    /// <summary>Changes the servings the recipe is scaled to (clamped 1..99) and persists the choice.</summary>
    public void SetServings(int value)
    {
        var clamped = Math.Clamp(value, 1, 99);
        _recipe = _recipe.WithChosenServings(clamped == OriginalServings ? null : clamped);
        Factor = ServingsScale.Factor(OriginalServings, CurrentServings);
        RaiseServingsChanged();
        Persist();
    }

    /// <summary>
    /// Sets the recipe's own servings count for the first time, via the "Set servings" link shown for
    /// recipes whose yield couldn't be detected. Clears <see cref="ServingsEstimated"/> \u2014 a number the
    /// cook typed in themselves is no longer an estimate.
    /// </summary>
    public void SetInitialServings(int value)
    {
        _recipe = _recipe.WithServings(value);
        Factor = ServingsScale.Factor(OriginalServings, CurrentServings);
        RaiseServingsChanged();
        Persist();
    }

    private void RaiseServingsChanged()
    {
        OnPropertyChanged(nameof(OriginalServings));
        OnPropertyChanged(nameof(HasKnownServings));
        OnPropertyChanged(nameof(CurrentServings));
        OnPropertyChanged(nameof(IsServingsChanged));
        OnPropertyChanged(nameof(ServingsEstimated));
        OnPropertyChanged(nameof(ServingsNounText));
    }

    /// <summary>Whether the recipe is starred. Persists immediately when changed.</summary>
    public bool IsFavourite
    {
        get => _recipe.IsFavourite;
        set
        {
            if (_recipe.IsFavourite == value)
                return;
            _recipe = _recipe.WithFavourite(value);
            OnPropertyChanged(nameof(IsFavourite));
            Persist();
        }
    }

    public ICommand ToggleFavouriteCommand { get; }

    public bool IsCooked => _recipe.Status == RecipeStatus.Cooked;

    /// <summary>"Want to cook" or "Cooked 24 Sep" for the status chip.</summary>
    public string StatusText => IsCooked
        ? AppResources.Format("StatusCookedFormat", _recipe.CookedAt?.LocalDateTime.ToString("d MMM") ?? string.Empty)
        : AppResources.Get("StatusWantToCook");

    /// <summary>Flips Want to cook \u21C4 Cooked (marking the cook count/date when moving to Cooked).</summary>
    public ICommand ToggleStatusCommand { get; }

    /// <summary>
    /// The cook's own notes. Two-way bound to the notes editor; edits are kept locally as the cook
    /// types and only persisted by <see cref="CommitNotes"/>, called on the editor losing focus.
    /// </summary>
    public string? Notes
    {
        get => _notes;
        set
        {
            if (_notes == value)
                return;
            _notes = value;
            OnPropertyChanged(nameof(Notes));
        }
    }

    /// <summary>Persists the notes editor's current text. No-op when nothing changed since the last save.</summary>
    public void CommitNotes()
    {
        if (string.Equals(_notes, _recipe.Notes, StringComparison.Ordinal))
            return;
        _recipe = _recipe.WithNotes(_notes);
        _notes = _recipe.Notes;
        OnPropertyChanged(nameof(Notes));
        Persist();
    }

    /// <summary>"Prep 15 min \u00B7 Cook 30 min \u00B7 jamieoliver.com", omitting parts the recipe doesn't have.</summary>
    public string MetaLine
    {
        get
        {
            var parts = new List<string>();
            if (_recipe.PrepMinutes is > 0)
                parts.Add(AppResources.Format("PrepFormat", _recipe.PrepMinutes));
            if (_recipe.CookMinutes is > 0)
                parts.Add(AppResources.Format("CookFormat", _recipe.CookMinutes));
            if (HasSource)
                parts.Add(SourceHost);
            return string.Join(" \u00B7 ", parts);
        }
    }

    /// <summary>
    /// True when the recipe's own units differ from the user's chosen display system, so the
    /// "Ingredients not in your units?" discoverability row is worth showing. "As written" (no
    /// preference set) never shows it \u2014 the cook explicitly asked not to convert.
    /// </summary>
    public bool ShowUnitHint
    {
        get
        {
            var target = UnitSettings.Target;
            return target is not null && target != _recipe.SourceSystem;
        }
    }

    /// <summary>Raised after every persisted change (servings, favourite, status, notes) with the new recipe.</summary>
    public event EventHandler<ParsedRecipe>? RecipeChanged;

    private void Persist() => RecipeChanged?.Invoke(this, _recipe);

    /// <summary>The active serving multiplier. 1.0 shows the original quantities.</summary>
    public double Factor
    {
        get => _factor;
        set
        {
            if (Math.Abs(_factor - value) < 0.0001)
                return;
            _factor = value;
            RebuildIngredients();
            RebuildSteps();
            foreach (var option in ScaleOptions)
                option.RaiseSelectedChanged();
        }
    }

    /// <summary>
    /// Updates only the photo (attached after import) without touching anything else on screen — a
    /// full <see cref="SetRecipe"/> would also reset notes the cook may be typing.
    /// </summary>
    public void SetImage(string? imagePath)
    {
        if (_recipe.ImagePath == imagePath)
            return;
        _recipe = _recipe.WithImage(imagePath);
        OnPropertyChanged(nameof(Recipe));
    }

    /// <summary>Replaces the wrapped recipe (e.g. after an edit or a units change) and refreshes bindings.</summary>
    public void SetRecipe(ParsedRecipe recipe)
    {
        _recipe = recipe;
        _active = recipe.Displayed();
        _display = RecipeUnits.ForDisplay(_active);
        _notes = recipe.Notes;
        _factor = ServingsScale.Factor(OriginalServings, CurrentServings);
        RebuildDisplay();
        // The photo header binds to Recipe itself (an edit can add, change or remove the photo).
        OnPropertyChanged(nameof(Recipe));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(IngredientCount));
        OnPropertyChanged(nameof(HasIngredients));
        OnPropertyChanged(nameof(DisplayRecipe));
        OnPropertyChanged(nameof(SourceHost));
        OnPropertyChanged(nameof(HasSource));
        OnPropertyChanged(nameof(IsTranslated));
        OnPropertyChanged(nameof(TranslatedLanguageName));
        OnPropertyChanged(nameof(TranslatedBadge));
        OnPropertyChanged(nameof(MetaLine));
        OnPropertyChanged(nameof(ShowUnitHint));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(IsFavourite));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsCooked));
        RaiseServingsChanged();
    }

    private void RebuildDisplay()
    {
        RebuildSteps();
        RebuildIngredients();
    }

    private void RebuildSteps()
    {
        DisplaySteps.Clear();
        var atOriginal = Math.Abs(_factor - 1.0) < 0.0001;
        foreach (var step in _display.Steps)
            DisplaySteps.Add(atOriginal
                ? step
                : step with { Instruction = RecipeScaling.ScaleText(step.Instruction, _factor) });
    }

    private void RebuildIngredients()
    {
        ScaledIngredients.Clear();
        foreach (var line in _display.Ingredients)
            ScaledIngredients.Add(RecipeScaling.Scale(line, _factor));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public sealed class ScaleOption : INotifyPropertyChanged
    {
        private readonly RecipeDetailViewModel _owner;

        public ScaleOption(RecipeDetailViewModel owner, string label, double factor)
        {
            _owner = owner;
            Label = label;
            Factor = factor;
        }

        public string Label { get; }
        public double Factor { get; }
        public bool IsSelected => Math.Abs(_owner.Factor - Factor) < 0.0001;

        public void RaiseSelectedChanged() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
