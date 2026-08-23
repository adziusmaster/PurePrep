using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Application;
using PurePrep.Domain;

namespace PurePrep.Presentation;

public sealed class RecipeLibraryViewModel : INotifyPropertyChanged
{
    private readonly IRecipeParser _parser;
    private readonly IRecipeRepository _repository;
    private string _urlInput = string.Empty;
    private bool _isImporting;
    private bool _isUpgradePromptVisible;
    private string? _errorMessage;

    public RecipeLibraryViewModel(IRecipeParser parser, IRecipeRepository repository)
    {
        _parser = parser;
        _repository = repository;
        Recipes = new ObservableCollection<ParsedRecipe>();

        ImportCommand = new Command(async () => await ImportAsync());
        UpgradeCommand = new Command(() => IsUpgradePromptVisible = true);
        OpenFocusCommand = new Command<ParsedRecipe>(recipe =>
        {
            if (recipe is not null)
                FocusRequested?.Invoke(this, recipe);
        });
    }

    public ObservableCollection<ParsedRecipe> Recipes { get; }
    public UserQuota Quota { get; } = new();

    public string UrlInput { get => _urlInput; set => SetField(ref _urlInput, value); }
    public bool IsImporting { get => _isImporting; private set => SetField(ref _isImporting, value); }
    public bool IsUpgradePromptVisible { get => _isUpgradePromptVisible; private set => SetField(ref _isUpgradePromptVisible, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }
    public string QuotaSummary => Quota.CanImportByLink
        ? $"{Quota.Credits} smart credits left"
        : "Out of credits — add recipes manually";
    public ICommand ImportCommand { get; }
    public ICommand UpgradeCommand { get; }
    public ICommand OpenFocusCommand { get; }

    public event EventHandler<ParsedRecipe>? FocusRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task ImportAsync()
    {
        ErrorMessage = null;
        if (!Quota.CanImportByLink)
        {
            IsUpgradePromptVisible = true;
            return;
        }

        if (!Uri.TryCreate(UrlInput, UriKind.Absolute, out var source) ||
            (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = "Paste a valid recipe URL to begin.";
            return;
        }

        IsImporting = true;
        try
        {
            var recipe = await _parser.ParseAsync(source);
            await _repository.SaveAsync(recipe);
            Recipes.Insert(0, recipe);
            Quota.TrySpendCredit();
            UrlInput = string.Empty;
            OnPropertyChanged(nameof(QuotaSummary));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not parse recipe: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}