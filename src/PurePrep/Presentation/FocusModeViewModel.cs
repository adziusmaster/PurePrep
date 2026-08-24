using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Domain;
using PurePrep.Services;

namespace PurePrep.Presentation;

public sealed class FocusModeViewModel : INotifyPropertyChanged
{
    private int _currentStepIndex;
    private bool _showIngredients;
    private bool _keepScreenAwake;

    public FocusModeViewModel(ParsedRecipe recipe)
    {
        Recipe = recipe;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
