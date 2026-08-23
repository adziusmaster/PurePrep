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
    private readonly ISmartCreditsClient _credits;
    private readonly IBillingService _billing;

    private string _urlInput = string.Empty;
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
        OpenFocusCommand = new Command<ParsedRecipe>(recipe =>
        {
            if (recipe is not null)
                FocusRequested?.Invoke(this, recipe);
        });
    }

    public ObservableCollection<ParsedRecipe> Recipes { get; }

    public string UrlInput { get => _urlInput; set => SetField(ref _urlInput, value); }
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
        < 0 => "Smart Parser ready",
        0 => "Out of credits — add recipes manually",
        1 => "1 smart credit left",
        _ => $"{CreditBalance} smart credits left",
    };

    public ICommand ImportCommand { get; }
    public ICommand UpgradeCommand { get; }
    public ICommand TopUpCommand { get; }
    public ICommand OpenFocusCommand { get; }

    public event EventHandler<ParsedRecipe>? FocusRequested;
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Loads saved recipes and the current credit balance. Called when the page appears.</summary>
    public async Task LoadAsync()
    {
        var savedRecipes = await _repository.GetAllAsync();
        Recipes.Clear();
        foreach (var recipe in savedRecipes)
            Recipes.Add(recipe);

        await RefreshCreditsAsync();
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
            ErrorMessage = "Paste a valid recipe URL to begin.";
            return;
        }

        IsImporting = true;
        try
        {
            var recipe = await _parser.ParseAsync(source);
            await _repository.SaveAsync(recipe);
            Recipes.Insert(0, recipe);
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
            ErrorMessage = $"Could not import recipe: {ex.Message}";
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
            ErrorMessage = "Smart Credit packs are available in the Play Store build.";
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
            ErrorMessage = $"Could not complete the purchase: {ex.Message}";
        }
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
