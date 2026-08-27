using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
    private bool _isPurchasing;
    private bool _isRecipeLanguageHintVisible;
    private string? _errorMessage;
    // -1 = balance not yet loaded from the backend.
    private int _creditBalance = -1;
    private IReadOnlyList<CreditPackOption> _creditPacks = [];

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
        CreditPacks = BuildPackOptions(billing.Packs);
        Recipes = new ObservableCollection<ParsedRecipe>();

        ImportCommand = new Command(async () => await ImportAsync());
        UpgradeCommand = new Command(() => IsUpgradePromptVisible = true);
        DismissUpgradeCommand = new Command(() => IsUpgradePromptVisible = false);
        TopUpCommand = new Command(async () => await TopUpAsync());
        DismissRecipeLanguageHintCommand = new Command(DismissRecipeLanguageHint);
        OpenRecipeLanguageSettingsCommand = new Command(() =>
        {
            DismissRecipeLanguageHint();
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        });
        // One-time hint nudging users to pick the recipe import language (auto-translation).
        _isRecipeLanguageHintVisible = !Preferences.Get(RecipeLanguageHintDismissedKey, false);
        BuyPackCommand = new Command<CreditPackOption>(async option => await PurchaseAsync(option));
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

    /// <summary>Selectable Smart Credit packs shown in the paywall picker (empty where billing is unavailable).</summary>
    public IReadOnlyList<CreditPackOption> CreditPacks { get => _creditPacks; private set => SetField(ref _creditPacks, value); }

    private static IReadOnlyList<CreditPackOption> BuildPackOptions(IReadOnlyList<CreditPack> packs) =>
        packs
            .Select(p => new CreditPackOption(
                p.ProductId,
                p.Credits,
                p.DisplayPrice,
                AppResources.Format("PackOptionFormat", p.Credits, p.DisplayPrice)))
            .ToList();

    /// <summary>True when in-app billing works on this build, so the pack picker can be shown.</summary>
    public bool IsBillingSupported => _billing.IsSupported;

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
    public bool IsPurchasing { get => _isPurchasing; private set => SetField(ref _isPurchasing, value); }
    public bool IsUpgradePromptVisible { get => _isUpgradePromptVisible; private set => SetField(ref _isUpgradePromptVisible, value); }

    /// <summary>Closes the paywall sheet (called by the hardware back button and the scrim/close tap).</summary>
    public void CloseUpgradePrompt() => IsUpgradePromptVisible = false;

    /// <summary>One-time tip telling users imported recipes are auto-translated into their recipe language.</summary>
    public bool IsRecipeLanguageHintVisible { get => _isRecipeLanguageHintVisible; private set => SetField(ref _isRecipeLanguageHintVisible, value); }

    private const string RecipeLanguageHintDismissedKey = "hint_recipe_language_dismissed";

    private void DismissRecipeLanguageHint()
    {
        if (!IsRecipeLanguageHintVisible)
            return;
        IsRecipeLanguageHintVisible = false;
        Preferences.Set(RecipeLanguageHintDismissedKey, true);
    }
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
    public ICommand DismissUpgradeCommand { get; }
    public ICommand TopUpCommand { get; }
    public ICommand DismissRecipeLanguageHintCommand { get; }
    public ICommand OpenRecipeLanguageSettingsCommand { get; }
    public ICommand BuyPackCommand { get; }
    public ICommand AddManuallyCommand { get; }
    public ICommand OpenFocusCommand { get; }
    public ICommand OpenDetailCommand { get; }

    public event EventHandler<ParsedRecipe>? FocusRequested;
    public event EventHandler<ParsedRecipe>? DetailRequested;
    public event EventHandler? AddManuallyRequested;
    public event EventHandler? SettingsRequested;
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
        await RefreshPricesAsync();
    }

    /// <summary>
    /// Replaces the placeholder pack prices with Google Play's real localized, tax-inclusive prices so
    /// the paywall matches what the user is charged at checkout. No-op / best-effort where unsupported.
    /// </summary>
    public async Task RefreshPricesAsync()
    {
        if (!_billing.IsSupported)
            return;

        try
        {
            var packs = await _billing.GetPacksAsync();
            CreditPacks = BuildPackOptions(packs);
        }
        catch
        {
            // Keep the placeholder labels if Play can't be queried.
        }
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

    /// <summary>Re-reads the credit balance from the backend (e.g. after redeeming a code).</summary>
    public async Task RefreshCreditsAsync()
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

    private Task TopUpAsync()
    {
        // Backward-compatible entry point (single button): buy the smallest pack.
        var first = CreditPacks.Count > 0 ? CreditPacks[0] : null;
        return PurchaseAsync(first);
    }

    /// <summary>Buys the given Smart Credit pack, grants the credits server-side, then consumes the purchase.</summary>
    private async Task PurchaseAsync(CreditPackOption? option)
    {
        ErrorMessage = null;

        if (option is null || !_billing.IsSupported || CreditPacks.Count == 0)
        {
            ErrorMessage = AppResources.Get("ErrPacksPlayStore");
            return;
        }

        if (IsPurchasing)
            return;

        IsPurchasing = true;
        try
        {
            var newBalance = await CreditPurchaseFlow.PurchaseAsync(_billing, _credits, option.ProductId);
            if (newBalance is null)
                return; // user cancelled

            CreditBalance = newBalance.Value;
            IsUpgradePromptVisible = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = AppResources.Format("ErrCouldNotPurchaseFormat", ex.Message);
        }
        finally
        {
            IsPurchasing = false;
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

/// <summary>A Smart Credit pack shown in the paywall picker, with a display label ("10 credits · €0.99").</summary>
public sealed record CreditPackOption(string ProductId, int Credits, string DisplayPrice, string Label);
