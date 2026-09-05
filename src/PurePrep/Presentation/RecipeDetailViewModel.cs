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
/// copy of the recipe, then live serving-size scaling to the ingredient list via
/// <see cref="RecipeScaling"/>. Steps are unit-converted but never scaled.
/// </summary>
public sealed class RecipeDetailViewModel : INotifyPropertyChanged
{
    private ParsedRecipe _recipe;
    private ParsedRecipe _active;
    private ParsedRecipe _display;
    private int? _baseServings;
    private double _factor = 1.0;

    public RecipeDetailViewModel(ParsedRecipe recipe)
    {
        _recipe = recipe;
        _active = recipe.Displayed();
        _display = RecipeUnits.ForDisplay(_active);
        _baseServings = ServingsDetector.Detect(_active.Title, _active.Ingredients, _active.Steps.Select(s => s.Instruction));

        ScaleOptions = new ObservableCollection<ScaleOption>
        {
            new(this, "\u00BD\u00D7", 0.5),
            new(this, "1\u00D7", 1.0),
            new(this, "2\u00D7", 2.0),
            new(this, "3\u00D7", 3.0),
        };
        SelectScaleCommand = new Command<ScaleOption>(opt => { if (opt is not null) Factor = opt.Factor; });
        RebuildDisplay();
    }

    public ParsedRecipe Recipe => _recipe;

    /// <summary>The recipe with units already converted to the user's preference.</summary>
    public ParsedRecipe DisplayRecipe => _display;

    /// <summary>
    /// The recipe as it should be cooked: units converted <b>and</b> the chosen serving multiplier
    /// applied. Focus Mode previously received <see cref="DisplayRecipe"/>, which is only
    /// unit-converted — so scaling to 2x and tapping Cook still showed the original quantities,
    /// at exactly the moment the scaled ones are needed.
    /// </summary>
    public ParsedRecipe CookRecipe => RecipeScaling.ScaleRecipe(_display, _factor);

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

    /// <summary>Unit-converted method steps (not scaled).</summary>
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

    /// <summary>
    /// Caption that explains the scaler: shows the resulting servings when the recipe's yield
    /// could be detected ("Serves 8"), otherwise the plain multiplier ("Scale 2x").
    /// </summary>
    public string ServingsCaption
    {
        get
        {
            if (_baseServings is int baseServings)
            {
                var scaled = Math.Max(1, (int)Math.Round(baseServings * _factor, MidpointRounding.AwayFromZero));
                return AppResources.Format("ServesFormat", scaled);
            }

            var label = ScaleOptions.FirstOrDefault(o => o.IsSelected)?.Label ?? "1\u00D7";
            return AppResources.Format("ScaleCaptionFormat", label);
        }
    }

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
            OnPropertyChanged(nameof(ServingsCaption));
            foreach (var option in ScaleOptions)
                option.RaiseSelectedChanged();
        }
    }

    /// <summary>Replaces the wrapped recipe (e.g. after an edit or a units change) and refreshes bindings.</summary>
    public void SetRecipe(ParsedRecipe recipe)
    {
        _recipe = recipe;
        _active = recipe.Displayed();
        _display = RecipeUnits.ForDisplay(_active);
        _baseServings = ServingsDetector.Detect(_active.Title, _active.Ingredients, _active.Steps.Select(s => s.Instruction));
        RebuildDisplay();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(IngredientCount));
        OnPropertyChanged(nameof(HasIngredients));
        OnPropertyChanged(nameof(DisplayRecipe));
        OnPropertyChanged(nameof(SourceHost));
        OnPropertyChanged(nameof(HasSource));
        OnPropertyChanged(nameof(ServingsCaption));
        OnPropertyChanged(nameof(IsTranslated));
        OnPropertyChanged(nameof(TranslatedLanguageName));
        OnPropertyChanged(nameof(TranslatedBadge));
    }

    private void RebuildDisplay()
    {
        DisplaySteps.Clear();
        foreach (var step in _display.Steps)
            DisplaySteps.Add(step);
        RebuildIngredients();
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
