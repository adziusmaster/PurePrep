using System.Windows.Input;

namespace PurePrep.Controls;

/// <summary>
/// People-based servings control shown on the detail page for a recipe with a known yield:
/// "Serves [−] 4 people [+]" plus a ↺ reset once the count changes. The number itself only raises
/// <see cref="NumberTapped"/> — the host page owns the numeric prompt (it needs a <c>ContentPage</c>
/// for <c>AppDialog</c>) and calls back into its view model's <c>SetServings</c>.
/// </summary>
public partial class ServingsStepper : ContentView
{
    public static readonly BindableProperty CurrentServingsProperty = BindableProperty.Create(
        nameof(CurrentServings), typeof(int), typeof(ServingsStepper), 1,
        propertyChanged: (bindable, _, _) => ((ServingsStepper)bindable).OnPropertyChanged(nameof(NumberText)));

    public static readonly BindableProperty NounProperty = BindableProperty.Create(
        nameof(Noun), typeof(string), typeof(ServingsStepper), string.Empty);

    public static readonly BindableProperty IsEstimatedProperty = BindableProperty.Create(
        nameof(IsEstimated), typeof(bool), typeof(ServingsStepper), false,
        propertyChanged: (bindable, _, _) => ((ServingsStepper)bindable).OnPropertyChanged(nameof(NumberText)));

    public static readonly BindableProperty IsChangedProperty = BindableProperty.Create(
        nameof(IsChanged), typeof(bool), typeof(ServingsStepper), false);

    public static readonly BindableProperty IncreaseCommandProperty = BindableProperty.Create(
        nameof(IncreaseCommand), typeof(ICommand), typeof(ServingsStepper));

    public static readonly BindableProperty DecreaseCommandProperty = BindableProperty.Create(
        nameof(DecreaseCommand), typeof(ICommand), typeof(ServingsStepper));

    public static readonly BindableProperty ResetCommandProperty = BindableProperty.Create(
        nameof(ResetCommand), typeof(ICommand), typeof(ServingsStepper));

    public ServingsStepper() => InitializeComponent();

    /// <summary>The servings currently shown.</summary>
    public int CurrentServings
    {
        get => (int)GetValue(CurrentServingsProperty);
        set => SetValue(CurrentServingsProperty, value);
    }

    /// <summary>Noun shown after the number ("people", or the recipe's own e.g. "pancakes").</summary>
    public string Noun
    {
        get => (string)GetValue(NounProperty);
        set => SetValue(NounProperty, value);
    }

    /// <summary>True when the count is model-estimated rather than stated — prefixes the number with "~".</summary>
    public bool IsEstimated
    {
        get => (bool)GetValue(IsEstimatedProperty);
        set => SetValue(IsEstimatedProperty, value);
    }

    /// <summary>True once the count has moved away from the recipe's own servings — shows the ↺ reset.</summary>
    public bool IsChanged
    {
        get => (bool)GetValue(IsChangedProperty);
        set => SetValue(IsChangedProperty, value);
    }

    public ICommand? IncreaseCommand
    {
        get => (ICommand?)GetValue(IncreaseCommandProperty);
        set => SetValue(IncreaseCommandProperty, value);
    }

    public ICommand? DecreaseCommand
    {
        get => (ICommand?)GetValue(DecreaseCommandProperty);
        set => SetValue(DecreaseCommandProperty, value);
    }

    public ICommand? ResetCommand
    {
        get => (ICommand?)GetValue(ResetCommandProperty);
        set => SetValue(ResetCommandProperty, value);
    }

    /// <summary>"~4" when estimated, otherwise "4".</summary>
    public string NumberText => IsEstimated ? $"~{CurrentServings}" : CurrentServings.ToString();

    /// <summary>Raised when the number is tapped; the host page prompts for a new value and applies it.</summary>
    public event EventHandler? NumberTapped;

    private void OnNumberTapped(object? sender, EventArgs e) => NumberTapped?.Invoke(this, EventArgs.Empty);
}
