using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Domain;

namespace PurePrep.Presentation;

/// <summary>
/// Wraps a saved recipe for the detail screen and applies live serving-size scaling
/// to the ingredient list via <see cref="RecipeScaling"/>. Steps are never scaled.
/// </summary>
public sealed class RecipeDetailViewModel : INotifyPropertyChanged
{
    private ParsedRecipe _recipe;
    private double _factor = 1.0;

    public RecipeDetailViewModel(ParsedRecipe recipe)
    {
        _recipe = recipe;
        ScaleOptions = new ObservableCollection<ScaleOption>
        {
            new(this, "½×", 0.5),
            new(this, "1×", 1.0),
            new(this, "2×", 2.0),
            new(this, "3×", 3.0),
        };
        SelectScaleCommand = new Command<ScaleOption>(opt => { if (opt is not null) Factor = opt.Factor; });
        RebuildIngredients();
    }

    public ParsedRecipe Recipe => _recipe;

    public string Title => _recipe.Title;
    public int StepCount => _recipe.StepCount;
    public int IngredientCount => _recipe.IngredientCount;
    public bool HasIngredients => _recipe.HasIngredients;
    public IReadOnlyList<RecipeStep> Steps => _recipe.Steps;

    /// <summary>Origin domain (e.g. "jamieoliver.com") when the recipe was imported from a link.</summary>
    public string SourceHost => TryGetHost(_recipe.SourceUrl);
    public bool HasSource => !string.IsNullOrEmpty(SourceHost);

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
            foreach (var option in ScaleOptions)
                option.RaiseSelectedChanged();
        }
    }

    /// <summary>Replaces the wrapped recipe (e.g. after an edit) and refreshes all bindings.</summary>
    public void SetRecipe(ParsedRecipe recipe)
    {
        _recipe = recipe;
        RebuildIngredients();
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(IngredientCount));
        OnPropertyChanged(nameof(HasIngredients));
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(SourceHost));
        OnPropertyChanged(nameof(HasSource));
    }

    private void RebuildIngredients()
    {
        ScaledIngredients.Clear();
        foreach (var line in _recipe.Ingredients)
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
