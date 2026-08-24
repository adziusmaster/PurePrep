using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Domain;

namespace PurePrep.Presentation;

public sealed class FocusModeViewModel : INotifyPropertyChanged
{
    private int _currentStepIndex;
    private bool _showIngredients;

    public FocusModeViewModel(ParsedRecipe recipe)
    {
        Recipe = recipe;
        PreviousCommand = new Command(() => CurrentStepIndex--, () => !IsFirstStep);
        NextCommand = new Command(() => CurrentStepIndex++, () => !IsLastStep);
        ToggleIngredientsCommand = new Command(() => ShowIngredients = !ShowIngredients);
    }

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
            ((Command)PreviousCommand).ChangeCanExecute();
            ((Command)NextCommand).ChangeCanExecute();
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

    public RecipeStep? CurrentStep => Steps.Count == 0 ? null : Steps[CurrentStepIndex];
    public string StepLabel => Steps.Count == 0 ? "No steps" : $"STEP {CurrentStepIndex + 1} OF {Steps.Count}";
    /// <summary>Completion fraction (0–1) for the progress bar.</summary>
    public double Progress => Steps.Count == 0 ? 0 : (double)(CurrentStepIndex + 1) / Steps.Count;
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => Steps.Count == 0 || CurrentStepIndex == Steps.Count - 1;
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand ToggleIngredientsCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
