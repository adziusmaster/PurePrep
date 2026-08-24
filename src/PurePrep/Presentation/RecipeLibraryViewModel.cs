using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using PurePrep.Application;
using PurePrep.Domain;
using PurePrep.Localization;

namespace PurePrep.Presentation;

public sealed class RecipeLibraryViewModel : INotifyPropertyChanged
{
    private readonly IRecipeParser _parser;
    private readonly IRecipeRepository _repository;
    private readonly ISmartCreditsClient _credits;
    private readonly IBillingService _billing;

    // Full unfiltered library; Recipes is the search-filtered view bound to the UI.
    private readonly List<ParsedRecipe> _all = new();

    private string _urlInput = string.Empty;
    private string _searchText = string.Empty;
    private bool _isImporting;
    private bool _isUpgradePromptVisible;
    private string? _errorMessage;
    // -1 = balance not yet loaded from the backend.
    private int _creditBalance = -1;

    public RecipeLibraryViewModel(
        IRecipeParser parser,
        IRecipeRepository repository,
        ISmartCreditsClient credits,
        IBillingService billing)
    {
        _parser = parser;
        _repository = repository;
        _credits = credits;
        _billing = billing;
        Recipes = new ObservableCollection<ParsedRecipe>();

        ImportCommand = new Command(async () => await ImportAsync());
        UpgradeCommand = new Command(() => IsUpgradePromptVisible = true);
        TopUpCommand = new Command(async () => await TopUpAsync());
        AddManuallyCommand = new Command(() => AddManuallyRequested?.Invoke(this, EventArgs.Empty));
        OpenFocusCommand = new Command<ParsedRecipe>(recipe =>
        {
            if (recipe is not null)
                FocusRequested?.Invoke(this, recipe);
        });
        OpenDetailCommand = new Command<ParsedRecipe>(recipe =>
        {
            if (recipe is not null)
                DetailRequested?.Invoke(this, recipe);
        });
    }

    public ObservableCollection<ParsedRecipe> Recipes { get; }

    public string UrlInput { get => _urlInput; set => SetField(ref _urlInput, value); }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                ApplyFilter();
                OnPropertyChanged(nameof(HasRecipes));
                OnPropertyChanged(nameof(IsSearching));
                OnPropertyChanged(nameof(NoSearchMatches));
            }
        }
    }

    /// <summary>True when the full library has any recipes (used to show the search box).</summary>
    public bool HasRecipes => _all.Count > 0;

    /// <summary>True when a search query is active.</summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

    /// <summary>True when a search is active but no recipe matched (drives the empty-state copy).</summary>
    public bool NoSearchMatches => IsSearching && Recipes.Count == 0;
    public bool IsImporting { get => _isImporting; private set => SetField(ref _isImporting, value); }
    public bool IsUpgradePromptVisible { get => _isUpgradePromptVisible; private set => SetField(ref _isUpgradePromptVisible, value); }
    public string? ErrorMessage { get => _errorMessage; private set => SetField(ref _errorMessage, value); }

    public int CreditBalance
    {
        get => _creditBalance;
        private set
        {
            if (SetField(ref _creditBalance, value))
                OnPropertyChanged(nameof(QuotaSummary));
        }
    }

    /// <summary>Link import (AI Smart Parser) is available while credits remain or the balance is unknown.</summary>
    public bool CanImportByLink => CreditBalance != 0;

    public string QuotaSummary => CreditBalance switch
    {
        < 0 => AppResources.Get("SmartParserReady"),
        0 => AppResources.Get("OutOfCreditsManual"),
        1 => AppResources.Get("CreditsLeftOne"),
        _ => AppResources.Format("CreditsLeftFormat", CreditBalance),
    };

    public ICommand ImportCommand { get; }
    public ICommand UpgradeCommand { get; }
    public ICommand TopUpCommand { get; }
    public ICommand AddManuallyCommand { get; }
    public ICommand OpenFocusCommand { get; }
    public ICommand OpenDetailCommand { get; }

    public event EventHandler<ParsedRecipe>? FocusRequested;
    public event EventHandler<ParsedRecipe>? DetailRequested;
    public event EventHandler? AddManuallyRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Loads saved recipes and the current credit balance. Called when the page appears.</summary>
    public async Task LoadAsync()
    {
        var savedRecipes = await _repository.GetAllAsync();
        _all.Clear();
        _all.AddRange(savedRecipes);
        ApplyFilter();
        OnPropertyChanged(nameof(HasRecipes));

        await RefreshCreditsAsync();
    }

    /// <summary>Rebuilds the bound <see cref="Recipes"/> collection from the full list + search text.</summary>
    private void ApplyFilter()
    {
        var query = _searchText?.Trim();
        IEnumerable<ParsedRecipe> view = _all;
        if (!string.IsNullOrEmpty(query))
            view = _all.Where(r => r.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

        Recipes.Clear();
        foreach (var recipe in view)
            Recipes.Add(recipe);

        OnPropertyChanged(nameof(NoSearchMatches));
    }

    private async Task RefreshCreditsAsync()
    {
        try
        {
            CreditBalance = await _credits.GetBalanceAsync();
        }
        catch
        {
            // Offline or backend unreachable: leave the balance unknown rather than blocking the UI.
        }
    }

    private async Task ImportAsync()
    {
        ErrorMessage = null;

        if (CreditBalance == 0)
        {
            IsUpgradePromptVisible = true;
            return;
        }

        if (!Uri.TryCreate(UrlInput, UriKind.Absolute, out var source) ||
            (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = AppResources.Get("ErrPasteValidUrl");
            return;
        }

        IsImporting = true;
        try
        {
            var recipe = await _parser.ParseAsync(source);
            await _repository.SaveAsync(recipe);
            AddNewRecipe(recipe);
            UrlInput = string.Empty;
            IsUpgradePromptVisible = false;
            await RefreshCreditsAsync();
        }
        catch (InsufficientCreditsException)
        {
            CreditBalance = 0;
            IsUpgradePromptVisible = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = AppResources.Format("ErrCouldNotImportFormat", ex.Message);
        }
        finally
        {
            IsImporting = false;
        }
    }

    private async Task TopUpAsync()
    {
        ErrorMessage = null;

        if (!_billing.IsSupported || _billing.Packs.Count == 0)
        {
            ErrorMessage = AppResources.Get("ErrPacksPlayStore");
            return;
        }

        try
        {
            // Minimal flow: purchase the smallest pack. A full pack-picker sheet can replace this later.
            var pack = _billing.Packs[0];
            var purchase = await _billing.BuyAsync(pack.ProductId);
            if (purchase is null)
                return; // user cancelled

            CreditBalance = await _credits.RedeemAsync(purchase.ProductId, purchase.PurchaseToken);
            IsUpgradePromptVisible = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = AppResources.Format("ErrCouldNotPurchaseFormat", ex.Message);
        }
    }

    /// <summary>Saves a hand-entered recipe. Manual add is always free — no Smart Credits are used.</summary>
    public async Task SaveManualAsync(string title, IEnumerable<string> ingredients, IEnumerable<string> steps)
    {
        var recipe = BuildRecipe(Guid.NewGuid(), title, ingredients, steps, DateTimeOffset.UtcNow, null);
        await _repository.SaveAsync(recipe);
        AddNewRecipe(recipe);
    }

    /// <summary>Updates an existing recipe in place (editing is free — no Smart Credits are used).</summary>
    public async Task<ParsedRecipe> UpdateManualAsync(ParsedRecipe original, string title,
        IEnumerable<string> ingredients, IEnumerable<string> steps)
    {
        var updated = BuildRecipe(original.Id, title, ingredients, steps, original.SavedAt, original.SourceUrl,
            original.SourceSystem);
        await _repository.UpdateAsync(updated);
        ReplaceRecipe(original, updated);
        return updated;
    }

    private static ParsedRecipe BuildRecipe(Guid id, string title, IEnumerable<string> ingredients,
        IEnumerable<string> steps, DateTimeOffset savedAt, string? sourceUrl,
        MeasurementSystem sourceSystem = MeasurementSystem.Metric) => new()
    {
        Id = id,
        Title = title.Trim(),
        SourceUrl = sourceUrl,
        SourceSystem = sourceSystem,
        SavedAt = savedAt,
        Ingredients = ingredients
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToArray(),
        Steps = steps
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Select((instruction, index) => new RecipeStep { Order = index + 1, Instruction = instruction })
            .ToArray(),
    };

    private void AddNewRecipe(ParsedRecipe recipe)
    {
        _all.Insert(0, recipe);
        ApplyFilter();
        OnPropertyChanged(nameof(HasRecipes));
    }

    private void ReplaceRecipe(ParsedRecipe original, ParsedRecipe updated)
    {
        var index = _all.FindIndex(r => r.Id == original.Id);
        if (index >= 0)
            _all[index] = updated;
        ApplyFilter();
    }

    /// <summary>Removes a saved recipe from storage and the library list.</summary>
    public async Task DeleteRecipeAsync(ParsedRecipe recipe)
    {
        await _repository.DeleteAsync(recipe.Id);
        _all.RemoveAll(r => r.Id == recipe.Id);
        Recipes.Remove(recipe);
        OnPropertyChanged(nameof(HasRecipes));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
