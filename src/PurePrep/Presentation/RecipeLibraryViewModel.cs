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
    private readonly RecipeImageAttacher _images;
    private readonly IRecipeImageStore _imageStore;

    // Full unfiltered library; Recipes is the search-filtered view bound to the UI.
    private readonly List<ParsedRecipe> _all = new();

    private const string LibrarySortKey = "library_sort";

    private string _urlInput = string.Empty;
    private string _searchText = string.Empty;
    private LibraryFilter _filter = LibraryFilter.All;
    private LibrarySort _sort = (LibrarySort)Preferences.Get(LibrarySortKey, (int)LibrarySort.RecentlyAdded);
    private bool _isImporting;
    // Set for the whole of an import (duplicate prompt included), unlike IsImporting which drives the spinner.
    private bool _importBusy;
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
        IBillingService billing,
        RecipeImageAttacher images,
        IRecipeImageStore imageStore)
    {
        _parser = parser;
        _repository = repository;
        _credits = credits;
        _billing = billing;
        _images = images;
        _imageStore = imageStore;
        CreditPacks = BuildPackOptions(billing.Packs);
        Recipes = new ObservableCollection<ParsedRecipe>();

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
        DismissClipboardSuggestionCommand = new Command(DismissClipboardSuggestion);
        SelectFilterCommand = new Command<LibraryFilter>(f => Filter = f);
        ChooseSortCommand = new Command(() => SortRequested?.Invoke(this, EventArgs.Empty));
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

    /// <summary>Which status/favourite subset the chips currently show.</summary>
    public LibraryFilter Filter
    {
        get => _filter;
        set
        {
            if (SetField(ref _filter, value))
                ApplyFilter();
        }
    }

    /// <summary>Current list ordering, persisted across launches.</summary>
    public LibrarySort Sort
    {
        get => _sort;
        set
        {
            if (SetField(ref _sort, value))
            {
                Preferences.Set(LibrarySortKey, (int)value);
                OnPropertyChanged(nameof(SortLabel));
                ApplyFilter();
            }
        }
    }

    /// <summary>The current sort's display label, shown on the sort button.</summary>
    public string SortLabel => Sort switch
    {
        LibrarySort.RecentlyCooked => AppResources.Get("SortRecentlyCooked"),
        LibrarySort.Alphabetical => AppResources.Get("SortAlphabetical"),
        _ => AppResources.Get("SortRecentlyAdded"),
    };

    /// <summary>True when the full library has any recipes (used to show the search box).</summary>
    public bool HasRecipes => _all.Count > 0;

    /// <summary>True when a search query is active.</summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

    /// <summary>True when a search is active but no recipe matched (drives the empty-state copy).</summary>
    public bool NoSearchMatches => IsSearching && Recipes.Count == 0;

    /// <summary>
    /// True when a non-"All" filter chip (with no active search) narrows a non-empty library down to zero
    /// recipes — distinct from <see cref="NoSearchMatches"/> and from the library being truly empty, each of
    /// which needs its own empty-state copy.
    /// </summary>
    public bool NoFilterMatches => HasRecipes && !IsSearching && _filter != LibraryFilter.All && Recipes.Count == 0;
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

    public ICommand UpgradeCommand { get; }
    public ICommand DismissUpgradeCommand { get; }
    public ICommand TopUpCommand { get; }
    public ICommand DismissRecipeLanguageHintCommand { get; }
    public ICommand OpenRecipeLanguageSettingsCommand { get; }
    public ICommand BuyPackCommand { get; }
    public ICommand OpenFocusCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand DismissClipboardSuggestionCommand { get; }
    public ICommand SelectFilterCommand { get; }
    public ICommand ChooseSortCommand { get; }

    public event EventHandler<ParsedRecipe>? FocusRequested;

    /// <summary>
    /// Raised on the main thread when a recipe changed underneath an open screen — its photo attached
    /// after an import, or a "Replace" re-import overwrote it — with the library's current copy, so an
    /// open detail page can refresh instead of later saving its stale copy on top.
    /// </summary>
    public event EventHandler<ParsedRecipe>? RecipeRefreshed;
    public event EventHandler<ParsedRecipe>? DetailRequested;
    public event EventHandler? SettingsRequested;

    /// <summary>Raised by <see cref="ChooseSortCommand"/>; the page answers with <see cref="Sort"/>.</summary>
    public event EventHandler? SortRequested;

    /// <summary>
    /// Asked (by the page) when an import URL matches a recipe already in the library, so the user can
    /// choose Replace / Keep both / Cancel <b>before</b> any Smart Credit is spent. When unset the
    /// import defaults to "Keep both" (never blocks, never silently overwrites).
    /// </summary>
    public Func<ParsedRecipe, Task<DuplicateImportAction>>? ResolveDuplicateImportAsync { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Loads saved recipes and the current credit balance. Called when the page appears.</summary>
    public async Task LoadAsync()
    {
        await SeedSampleIfFirstRunAsync();

        var savedRecipes = await _repository.GetAllAsync();
        _all.Clear();
        _all.AddRange(savedRecipes);
        ApplyFilter();
        OnPropertyChanged(nameof(HasRecipes));

        await RefreshCreditsAsync();
        await RefreshPricesAsync();
    }

    // Puts one recipe (a Polish pierogi classic, shown in English) into a brand-new library so the
    // first thing a new user sees is a real recipe to cook, not an empty screen. Guarded by a flag
    // rather than "is the library empty", so a user who deletes it does not get it forced back.
    private async Task SeedSampleIfFirstRunAsync()
    {
        const string seededKey = "sample_recipe_seeded";
        if (Preferences.Get(seededKey, false))
            return;

        Preferences.Set(seededKey, true);

        try
        {
            var existing = await _repository.GetAllAsync();
            if (existing.Count == 0)
            {
                var sample = SampleRecipe.Create();
                await _repository.SaveAsync(sample);
                await TrySeedSamplePhotoAsync(sample);
            }
        }
        catch
        {
            // Seeding is a nicety; never let it block the library from loading.
        }
    }

    // Attaches the bundled pan-fried pierogi photo (Resources/Raw/sample-pierogi.jpg) to the freshly
    // seeded sample, so a new user's first recipe already looks like a finished one. Best effort: any
    // failure (missing asset, IO) leaves the sample with its placeholder image, silently — this is a
    // nicety, not something worth surfacing to a user who just opened the app for the first time.
    private async Task TrySeedSamplePhotoAsync(ParsedRecipe sample)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("sample-pierogi.jpg");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);

            var path = await _imageStore.SaveBytesAsync(sample.Id, buffer.ToArray(), CancellationToken.None);
            await _repository.UpdateImagePathAsync(sample.Id, path);
        }
        catch
        {
            // Placeholder image stays; not worth failing first-run seeding over.
        }
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

    /// <summary>Rebuilds the bound <see cref="Recipes"/> collection from the full list + search/filter/sort.</summary>
    private void ApplyFilter()
    {
        // Granular sync, not Clear()+Add: a Reset rebuilds the CollectionView header, which holds the
        // search Entry, and Android then hands focus to the header's first Entry (the URL box).
        Recipes.SyncTo(LibraryQuery.Apply(_all, _searchText, _filter, _sort));
        OnPropertyChanged(nameof(NoSearchMatches));
        OnPropertyChanged(nameof(NoFilterMatches));
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

    /// <summary>
    /// Imports one link. Shared by the URL box, the share sheet, the clipboard chip and the in-app
    /// browser so credit checks, duplicate handling and error mapping stay identical for all of them.
    /// Out of credits (known zero balance, or the backend's 402) comes back as
    /// <see cref="ImportOutcome.OutOfCredits"/> — no request is made for a known zero — and the caller
    /// routes it to Buy Smart Credits; other failures also set <see cref="ErrorMessage"/>.
    /// </summary>
    public async Task<ImportResult> ImportUrlAsync(string url, bool allowDuplicate)
    {
        // A second tap while the first import (including its duplicate prompt) is still running must not
        // send — and pay for — it twice.
        if (_importBusy)
            return ImportResult.Cancelled;

        _importBusy = true;
        try
        {
            return await ImportUrlCoreAsync(url, allowDuplicate);
        }
        finally
        {
            _importBusy = false;
        }
    }

    private async Task<ImportResult> ImportUrlCoreAsync(string url, bool allowDuplicate)
    {
        UrlInput = url;
        ErrorMessage = null;

        if (ImportResult.IsOutOfCredits(CreditBalance))
            return ImportResult.OutOfCredits;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var source) ||
            (source.Scheme != Uri.UriSchemeHttp && source.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = AppResources.Get("ErrPasteValidUrl");
            return ImportResult.Failed(ImportErrorCode.InvalidUrl);
        }

        // Duplicate guard: if the same page was already imported, ask before spending a Smart Credit.
        // This runs BEFORE ParseAsync (which is what the backend charges for), so "Cancel" is free.
        // The Import sheet already told the user it's a duplicate and asks again there, so it passes
        // allowDuplicate: true to skip this second prompt and keep both.
        ParsedRecipe? replaceTarget = null;
        var existing = FindBySourceUrl(source);
        if (existing is not null && !allowDuplicate)
        {
            var action = ResolveDuplicateImportAsync is null
                ? DuplicateImportAction.KeepBoth
                : await ResolveDuplicateImportAsync(existing);

            if (action == DuplicateImportAction.Cancel)
                return ImportResult.Cancelled;

            if (action == DuplicateImportAction.Replace)
                replaceTarget = existing;
        }

        IsImporting = true;
        try
        {
            var import = await _parser.ParseAsync(source);
            var recipe = import.Recipe;
            if (replaceTarget is not null)
            {
                // Re-read the library copy: the cook may have changed notes or servings while the
                // duplicate prompt and the parse were running.
                recipe = RecipeMerge.Reimport(recipe, FindById(replaceTarget.Id) ?? replaceTarget);
                await _repository.UpdateAsync(recipe);
                ReplaceRecipe(replaceTarget, recipe);
                RecipeRefreshed?.Invoke(this, recipe);
            }
            else
            {
                await _repository.SaveAsync(recipe);
                AddNewRecipe(recipe);
            }
            UrlInput = string.Empty;
            _ = AttachImageAsync(import, recipe);
            await RefreshCreditsAsync();
            return ImportResult.Imported(recipe);
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
        finally
        {
            IsImporting = false;
        }
    }

    // Maps a failed import. Out of credits pins the balance at 0 (so every sheet now offers Buy);
    // anything else gets a localized message — the backend's code, or a neutral one for unclassified
    // transport errors — never a raw exception string.
    private ImportResult Fail(Exception error)
    {
        var result = ImportResult.FromException(error);
        if (result.Outcome == ImportOutcome.OutOfCredits)
            CreditBalance = 0;
        else
            ErrorMessage = ImportErrorText.ForCode(result.ErrorCode);
        return result;
    }

    /// <summary>
    /// Checks what was typed or pasted into the Add sheet's link box before it is handed to the Import
    /// sheet: returns the http(s) link found in it (text around a shared link is tolerated), or null
    /// after setting <see cref="ErrorMessage"/>. Nothing is imported or charged here.
    /// </summary>
    public string? PrepareLinkImport(string? input)
    {
        var url = Domain.SharedText.ExtractUrl(input);
        if (url is null)
        {
            ErrorMessage = AppResources.Get("ErrPasteValidUrl");
            return null;
        }

        ErrorMessage = null;
        return url;
    }

    private Task TopUpAsync()
    {
        // Backward-compatible entry point (single button): buy the smallest pack.
        var first = CreditPacks.Count > 0 ? CreditPacks[0] : null;
        return PurchaseAsync(first);
    }

    /// <summary>
    /// Imports a recipe from a photo/screenshot. Vision input is pricier, so the backend charges more
    /// Smart Credits — the credit guard and refund-on-failure handling live server-side.
    /// </summary>
    public Task ImportFromImageAsync(byte[] image, string mimeType) =>
        ImportNewAsync(() => _parser.ParseImageAsync(image, mimeType));

    /// <summary>Imports a recipe from pasted free text (a note, a message, a PDF's contents).</summary>
    public Task ImportFromTextAsync(string text) =>
        ImportNewAsync(() => _parser.ParseTextAsync(text));

    // Shared core for the "no URL" import sources (image, text): there's nothing to match against, so
    // these always save as a new recipe. Credit guard + error mapping mirror ImportAsync exactly.
    private async Task ImportNewAsync(Func<Task<ParsedImport>> parse)
    {
        // Ignore a repeat tap while an import is in flight: no second request, no second charge.
        if (_importBusy)
            return;

        ErrorMessage = null;

        if (ImportResult.IsOutOfCredits(CreditBalance))
        {
            IsUpgradePromptVisible = true;
            return;
        }

        _importBusy = true;
        IsImporting = true;
        try
        {
            var import = await parse();
            var recipe = import.Recipe;
            await _repository.SaveAsync(recipe);
            AddNewRecipe(recipe);
            IsUpgradePromptVisible = false;
            _ = AttachImageAsync(import, recipe);
            await RefreshCreditsAsync();
        }
        catch (Exception ex)
        {
            if (Fail(ex).Outcome == ImportOutcome.OutOfCredits)
                IsUpgradePromptVisible = true;
        }
        finally
        {
            IsImporting = false;
            _importBusy = false;
        }
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
    public async Task SaveNewAsync(ParsedRecipe recipe)
    {
        await _repository.SaveAsync(recipe);
        AddNewRecipe(recipe);
    }

    /// <summary>
    /// Persists a recipe whose translation state (cached translations / displayed language) changed.
    /// This keeps the whole recipe intact — no field rebuild — so the original text and translation
    /// cache are preserved. Switching or caching a translation is free at this layer; the Smart Credit is charged by the backend translate call, not here.
    /// </summary>
    public async Task<ParsedRecipe> UpdateRecipeAsync(ParsedRecipe original, ParsedRecipe updated)
    {
        var merged = RecipeMerge.KeepStoredPhoto(updated, FindById(updated.Id));
        await _repository.UpdateAsync(merged);
        ReplaceRecipe(original, merged);
        return merged;
    }

    /// <summary>
    /// Persists a changed recipe (notes, favourite, status, servings, editor changes) and refreshes the list.
    /// The photo is never changed here: the library's stored photo wins over the caller's copy, which may
    /// predate a photo attached after import (see <see cref="RecipeMerge.KeepStoredPhoto"/>). Returns the
    /// recipe as saved. Editing is free — no Smart Credits are used.
    /// </summary>
    public async Task<ParsedRecipe> UpdateRecipeAsync(ParsedRecipe recipe)
    {
        var merged = RecipeMerge.KeepStoredPhoto(recipe, FindById(recipe.Id));
        await _repository.UpdateAsync(merged);
        ReplaceInLibrary(merged);
        return merged;
    }

    /// <summary>
    /// Saves a recipe from the editor (new or edited) together with its photo change, so the stored recipe
    /// and the image file agree. A new photo is written first and the recipe persisted once, pointing at it;
    /// if that persist fails the file is rolled back. A removed photo is persisted first and its file deleted
    /// afterwards. Editing is free — no Smart Credits are used.
    /// </summary>
    /// <param name="newPhoto">JPEG bytes of a newly chosen photo, or null for no new photo.</param>
    /// <param name="removePhoto">True when the photo was removed in the editor (ignored with a new photo).</param>
    public async Task SaveFromEditorAsync(ParsedRecipe recipe, bool isNew, byte[]? newPhoto, bool removePhoto,
        CancellationToken cancellationToken)
    {
        // The editor may have been opened on a copy from before the import's photo attached; start from
        // the stored photo so "no change" keeps it and a new photo's rollback restores the right file.
        if (!isNew)
            recipe = RecipeMerge.KeepStoredPhoto(recipe, FindById(recipe.Id));

        if (newPhoto is not null)
        {
            // images/{id}.jpg may already hold this recipe's current photo; keep its bytes to put back.
            var previous = recipe.ImagePath is { } oldPath ? await _imageStore.ReadAsync(oldPath, cancellationToken) : null;
            var path = await _imageStore.SaveBytesAsync(recipe.Id, newPhoto, cancellationToken);
            try
            {
                await PersistAsync(recipe.WithImage(path), isNew, photoChanged: true);
            }
            catch
            {
                await RollBackPhotoAsync(recipe, path, previous);
                throw;
            }
            return;
        }

        if (!removePhoto)
        {
            await PersistAsync(recipe, isNew);
            return;
        }

        await PersistAsync(recipe.WithImage(null), isNew, photoChanged: true);
        try
        {
            await _imageStore.DeleteAsync(recipe.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            // The recipe no longer points at the file; a leftover file is harmless and the save succeeded.
            System.Diagnostics.Debug.WriteLine($"Recipe photo delete failed after save: {ex}");
        }
    }

    // Only the editor's explicit add/remove photo changes an existing recipe's photo; every other update
    // keeps the stored one.
    private async Task PersistAsync(ParsedRecipe recipe, bool isNew, bool photoChanged = false)
    {
        if (isNew)
        {
            await SaveNewAsync(recipe);
            return;
        }

        if (!photoChanged)
        {
            await UpdateRecipeAsync(recipe);
            return;
        }

        await _repository.UpdateAsync(recipe);
        await _repository.UpdateImagePathAsync(recipe.Id, recipe.ImagePath);
        ReplaceInLibrary(recipe);
    }

    // Puts the image file back the way the still-stored recipe expects it: the previous bytes when the new
    // photo overwrote them, otherwise no file at all (no orphan). Best effort — the save error is what matters.
    private async Task RollBackPhotoAsync(ParsedRecipe recipe, string? writtenPath, byte[]? previous)
    {
        try
        {
            if (previous is not null && recipe.ImagePath == writtenPath)
                await _imageStore.SaveBytesAsync(recipe.Id, previous, CancellationToken.None);
            else
                await _imageStore.DeleteAsync(recipe.Id, CancellationToken.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

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

    /// <summary>Replaces a recipe already in the full library, matched by its own <see cref="ParsedRecipe.Id"/>.</summary>
    private void ReplaceInLibrary(ParsedRecipe updated)
    {
        var index = _all.FindIndex(r => r.Id == updated.Id);
        if (index >= 0)
            _all[index] = updated;
        ApplyFilter();
    }

    /// <summary>
    /// Downloads-or-generates the just-imported recipe's photo after it has already been saved. Runs
    /// fire-and-forget from the import flow: the import itself must not wait on (or fail because of) a
    /// slow/broken image fetch, and this must never throw into the UI.
    /// </summary>
    private async Task AttachImageAsync(ParsedImport import, ParsedRecipe saved)
    {
        try
        {
            var updated = await _images.AttachAsync(import, saved, CancellationToken.None);
            if (ReferenceEquals(updated, saved) || updated.ImagePath is not { } path)
                return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    ApplyAttachedPhoto(saved.Id, path);
                }
                catch
                {
                    // Best-effort UI refresh; the recipe's image path is already persisted.
                }
            });
        }
        catch
        {
            // Best-effort: the recipe already saved successfully without a photo; never surface this.
        }
    }

    // Sets the photo on the library's CURRENT copy — not the import-time copy the attacher was given,
    // which would roll back anything the cook changed while the photo downloaded — and tells open screens.
    private void ApplyAttachedPhoto(Guid id, string path)
    {
        var index = _all.FindIndex(r => r.Id == id);
        if (index < 0)
            return;
        _all[index] = _all[index].WithImage(path);
        ApplyFilter();
        RecipeRefreshed?.Invoke(this, _all[index]);
    }

    /// <summary>
    /// Reports a share that carried no usable link, so the user is not left wondering. (Shares that
    /// do carry a link go straight to the Import sheet — see <c>ImportCoordinator</c> — rather than
    /// through the view model.)
    /// </summary>
    public void ReportSharedUrlMissing() => ErrorMessage = AppResources.Get("ErrSharedNoLink");

    private const string LastSuggestedKey = "clipboard_last_suggested";
    private string? _clipboardSuggestionUrl;

    /// <summary>The link currently offered by the clipboard chip, or null when there's nothing to offer.</summary>
    public string? ClipboardSuggestionUrl
    {
        get => _clipboardSuggestionUrl;
        private set
        {
            if (SetField(ref _clipboardSuggestionUrl, value))
            {
                OnPropertyChanged(nameof(HasClipboardSuggestion));
                OnPropertyChanged(nameof(ClipboardSuggestionHost));
                OnPropertyChanged(nameof(ClipboardSuggestionText));
            }
        }
    }

    public bool HasClipboardSuggestion => ClipboardSuggestionUrl is not null;

    public string ClipboardSuggestionHost =>
        Uri.TryCreate(ClipboardSuggestionUrl, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") + u.AbsolutePath : string.Empty;

    /// <summary>
    /// "{host} · Import?" pre-formatted for the chip. XAML's nested <c>StringFormat={loc:Translate ...}</c>
    /// is untested on device for this build, so the page binds this instead of composing it in markup.
    /// </summary>
    public string ClipboardSuggestionText =>
        HasClipboardSuggestion ? AppResources.Format("ClipboardSuggestFormat", ClipboardSuggestionHost) : string.Empty;

    /// <summary>
    /// Called when Home appears and whenever the app window regains focus. Reads the clipboard only
    /// when it changed since the last check (reading it shows Android's "pasted" toast) and offers a
    /// new recipe link.
    /// </summary>
    public async Task CheckClipboardAsync()
    {
        try
        {
            if (!Clipboard.Default.HasText || !Services.ClipboardChange.ChangedSinceLastCheck())
                return;
            var text = await Clipboard.Default.GetTextAsync();
            if (text is null)
                return;
            Services.ClipboardChange.MarkChecked();
            var url = Domain.ClipboardSuggestion.Evaluate(text, Preferences.Default.Get<string?>(LastSuggestedKey, null), _all.Select(r => r.SourceUrl));
            if (url is null)
                return;
            Preferences.Default.Set(LastSuggestedKey, url);
            ClipboardSuggestionUrl = url;
        }
        catch
        {
            // Clipboard access can be denied or throw on some OEM builds; the chip is a convenience.
        }
    }

    /// <summary>Dismisses the clipboard chip without importing (tapping ✕, or after an import starts).</summary>
    public void DismissClipboardSuggestion() => ClipboardSuggestionUrl = null;

    /// <summary>Pastes a link from the clipboard into the import box.</summary>
    public async Task PasteFromClipboardAsync()
    {
        var text = await Clipboard.Default.GetTextAsync();
        var url = Domain.SharedText.ExtractUrl(text);
        if (url is null)
        {
            ErrorMessage = AppResources.Get("ErrClipboardNoLink");
            return;
        }

        ErrorMessage = null;
        UrlInput = url;
    }

    /// <summary>
    /// Finds a recipe in the <b>full</b> library by id. Callers must not search <see cref="Recipes"/>
    /// for this: that collection is the search-filtered view, so a recipe the current query excludes
    /// would appear to have vanished.
    /// </summary>
    public ParsedRecipe? FindById(Guid id) => _all.FirstOrDefault(r => r.Id == id);

    /// <summary>
    /// Finds an already-imported recipe by source link. Used by the Import sheet to decide between
    /// "Uses 1 Smart Credit" and "Already saved" before any credit is spent. Returns null for a URL
    /// that doesn't parse rather than throwing — the sheet just shows the "new link" copy in that case.
    /// </summary>
    public ParsedRecipe? FindBySourceUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var source) ? FindBySourceUrl(source) : null;

    /// <summary>
    /// Finds an already-imported recipe whose source page matches <paramref name="source"/>, ignoring
    /// trivial differences (scheme, case, trailing slash, fragment, common tracking query params) so a
    /// re-paste of the same link is recognised as a duplicate rather than silently re-charged.
    /// </summary>
    private ParsedRecipe? FindBySourceUrl(Uri source)
    {
        var key = RecipeUrl.Normalize(source);
        return _all.FirstOrDefault(r =>
            !string.IsNullOrWhiteSpace(r.SourceUrl) &&
            Uri.TryCreate(r.SourceUrl, UriKind.Absolute, out var existing) &&
            string.Equals(RecipeUrl.Normalize(existing), key, StringComparison.Ordinal));
    }

    /// <summary>Removes a saved recipe from storage and the library list.</summary>
    public async Task DeleteRecipeAsync(ParsedRecipe recipe)
    {
        await _repository.DeleteAsync(recipe.Id);
        await _imageStore.DeleteAsync(recipe.Id, CancellationToken.None);
        _all.RemoveAll(r => r.Id == recipe.Id);
        Recipes.Remove(recipe);
        OnPropertyChanged(nameof(HasRecipes));
        OnPropertyChanged(nameof(NoSearchMatches));
        OnPropertyChanged(nameof(NoFilterMatches));
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
/// <summary>What to do when an imported URL already exists in the library.</summary>
public enum DuplicateImportAction
{
    /// <summary>Overwrite the existing recipe in place (spends one Smart Credit for the re-import).</summary>
    Replace,

    /// <summary>Import as a separate second copy (spends one Smart Credit).</summary>
    KeepBoth,

    /// <summary>Do nothing — no credit is spent.</summary>
    Cancel,
}
