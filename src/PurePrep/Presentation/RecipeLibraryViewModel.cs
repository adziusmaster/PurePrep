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
        Recipes = new ObservableCollection<ParsedRecipe>
        {
            new()
            {
                Title = "Miso butter mushrooms",
                SourceUrl = "https://pureprep.local/recipes/miso-mushrooms",
                Ingredients = new[] { "450 g mushrooms", "2 tbsp butter", "1 tbsp white miso", "1 tsp sesame oil" },
                Steps = new[]
                {
                    new RecipeStep { Order = 1, Instruction = "Wipe the mushrooms clean and tear any large ones in half." },
                    new RecipeStep { Order = 2, Instruction = "Sear in a hot pan until deeply golden, 6 to 8 minutes." },
                    new RecipeStep { Order = 3, Instruction = "Lower the heat. Add butter, miso, and sesame oil, then toss." }
                }
            },
            new()
            {
                Title = "Weeknight tomato orzo",
                SourceUrl = "https://pureprep.local/recipes/tomato-orzo",
                Ingredients = new[] { "250 g orzo", "400 g chopped tomatoes", "700 ml vegetable stock", "1 lemon" },
                Steps = new[]
                {
                    new RecipeStep { Order = 1, Instruction = "Toast the orzo in olive oil for 2 minutes." },
                    new RecipeStep { Order = 2, Instruction = "Stir in tomatoes and stock. Simmer until tender." },
                    new RecipeStep { Order = 3, Instruction = "Finish with lemon zest, juice, and black pepper." }
                }
            },
            new()
            {
                Title = "Crisp-edged potato frittata",
                SourceUrl = "https://pureprep.local/recipes/potato-frittata",
                Ingredients = new[] { "500 g potatoes", "6 eggs", "1 small onion", "80 g cheddar" },
                Steps = new[]
                {
                    new RecipeStep { Order = 1, Instruction = "Boil sliced potatoes until just tender, then drain." },
                    new RecipeStep { Order = 2, Instruction = "Soften the onion in an oven-safe skillet." },
                    new RecipeStep { Order = 3, Instruction = "Add potatoes and beaten eggs. Cook until set around the edge." }
                }
            }
        };

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
    public string QuotaSummary => Quota.IsPremium ? "Premium access" : $"{Quota.RemainingFreeRecipes} free saves remaining";
    public ICommand ImportCommand { get; }
    public ICommand UpgradeCommand { get; }
    public ICommand OpenFocusCommand { get; }

    public event EventHandler<ParsedRecipe>? FocusRequested;

    public event PropertyChangedEventHandler? PropertyChanged;

    private async Task ImportAsync()
    {
        ErrorMessage = null;
        if (!Quota.CanSaveRecipe)
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
            Quota.RecordRecipeSaved();
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