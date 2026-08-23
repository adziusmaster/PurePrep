using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Domain;

namespace PurePrep.Presentation;

public sealed class FocusModeViewModel : INotifyPropertyChanged
{
    private int _currentStepIndex;

    public FocusModeViewModel(ParsedRecipe recipe)
    {
        Recipe = recipe;
        PreviousCommand = new Command(() => CurrentStepIndex--, () => !IsFirstStep);
        NextCommand = new Command(() => CurrentStepIndex++, () => !IsLastStep);
    }

    public ParsedRecipe Recipe { get; }
    public IReadOnlyList<RecipeStep> Steps => Recipe.Steps;
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
            OnPropertyChanged(nameof(IsFirstStep));
            OnPropertyChanged(nameof(IsLastStep));
            ((Command)PreviousCommand).ChangeCanExecute();
            ((Command)NextCommand).ChangeCanExecute();
        }
    }

    public RecipeStep? CurrentStep => Steps.Count == 0 ? null : Steps[CurrentStepIndex];
    public string StepLabel => Steps.Count == 0 ? "No steps" : $"STEP {CurrentStepIndex + 1} OF {Steps.Count}";
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => Steps.Count == 0 || CurrentStepIndex == Steps.Count - 1;
    public ICommand PreviousCommand { get; }
    public ICommand NextCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
