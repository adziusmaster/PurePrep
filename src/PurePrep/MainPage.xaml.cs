namespace PurePrep;

using System.ComponentModel;
using PurePrep.Domain;
using PurePrep.Localization;
using PurePrep.Presentation;
using PurePrep.Services;

public partial class MainPage : ContentPage, IHardwareBackHandler
{
	private bool _hasLoaded;

	private readonly ImportCoordinator? _coordinator;

	public MainPage(RecipeLibraryViewModel viewModel)
	{
		InitializeComponent();
		_coordinator = IPlatformApplication.Current?.Services.GetService<ImportCoordinator>();
		TimersBarControl.Library = viewModel;
		viewModel.FocusRequested += OnFocusRequested;
		viewModel.DetailRequested += OnDetailRequested;
		viewModel.SettingsRequested += OnSettingsTapped;
		viewModel.SortRequested += OnSortRequested;
		viewModel.ResolveDuplicateImportAsync = OnResolveDuplicateImportAsync;
		viewModel.PropertyChanged += OnViewModelPropertyChanged;
		BindingContext = viewModel;
		SizeChanged += OnPageSizeChanged;
		AddSheet.PropertyChanged += OnSheetVisibilityChanged;
		ImportSheet.PropertyChanged += OnSheetVisibilityChanged;
		BottomDock.SizeChanged += (_, _) =>
		{
			if (BottomDock.Height > 0)
				BottomDockSpacer.HeightRequest = BottomDock.Height + 8;
		};
	}

	public RecipeLibraryViewModel ViewModel => (RecipeLibraryViewModel)BindingContext;

	// MAUI-Android keeps a stale measured width on a centred, max-width container after an
	// orientation change, so rotating to landscape and back could leave the list mis-sized (and
	// sometimes unscrollable). Re-stamping an explicit width on every size change forces a clean
	// re-measure: full width when narrower than the cap, the centred cap when wider.
	private void OnPageSizeChanged(object? sender, EventArgs e)
	{
		const double maxWidth = 760;
		if (Width <= 0)
			return;
		ContentRoot.WidthRequest = Math.Min(Width, maxWidth);
		BottomDock.WidthRequest = Math.Min(Width, maxWidth);
	}

	// The floating Add button steps aside while the Add or Import sheet is up. It fades rather than
	// collapses so the bottom dock (and the list's footer spacer sized from it) keeps its height.
	private void OnSheetVisibilityChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(IsVisible))
			return;
		var sheetUp = AddSheet.IsVisible || ImportSheet.IsVisible;
		AddButton.Opacity = sheetUp ? 0 : 1;
		AddButton.InputTransparent = sheetUp;
	}

	private void OnAddRecipeTapped(object? sender, EventArgs e)
	{
		AddSheet.Balance = ViewModel.CreditBalance;
		AddSheet.Show();
	}

	// Link box → the same Import sheet a share or the clipboard banner opens, so the cost, duplicate
	// and out-of-credits handling are identical. An unusable link keeps the Add sheet open with the error.
	private async void OnAddSheetImportLink(object? sender, string input)
	{
		if (ViewModel.PrepareLinkImport(input) is not { } url)
			return;
		await AddSheet.HideAsync();
		ShowImportSheet(url);
	}

	private async void OnAddSheetClipboardSuggestion(object? sender, EventArgs e)
	{
		if (ViewModel.ClipboardSuggestionUrl is not { } url)
			return;
		await AddSheet.HideAsync();
		ShowImportSheet(url);
	}

	// "Search the web": opens the in-app browser on a recipe search.
	private async void OnAddSheetWebSearch(object? sender, string query)
	{
		await AddSheet.HideAsync();
		await Navigation.PushAsync(new SearchBrowserPage(query, ViewModel));
	}

	private async void OnAddSheetPhoto(object? sender, EventArgs e)
	{
		await AddSheet.HideAsync();
		await ImportPhotoAsync();
	}

	private async void OnAddSheetText(object? sender, EventArgs e)
	{
		await AddSheet.HideAsync();
		await Navigation.PushAsync(new PasteTextPage(ViewModel));
	}

	private async void OnAddSheetManual(object? sender, EventArgs e)
	{
		await AddSheet.HideAsync();
		await Navigation.PushAsync(new ManualAddPage(ViewModel));
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(RecipeLibraryViewModel.IsUpgradePromptVisible))
			return;

		var vm = (RecipeLibraryViewModel)BindingContext;
		if (!vm.IsUpgradePromptVisible)
			return;

		// The paywall is now a real page, not an in-page overlay: open it and immediately clear the
		// flag so it acts as a one-shot trigger (and hardware-back pops the page as usual).
		vm.CloseUpgradePrompt();
		Dispatcher.Dispatch(async () => await OpenBuyCreditsAsync());
	}

	private bool _buyCreditsOpen;

	private async Task OpenBuyCreditsAsync()
	{
		if (_buyCreditsOpen)
			return;
		_buyCreditsOpen = true;
		try
		{
			await BuyCreditsPage.OpenAsync(Navigation, ViewModel.CreditBalance);
		}
		finally
		{
			_buyCreditsOpen = false;
		}
	}

	/// <summary>Configures and opens the Import sheet for a link that arrived from a share or the clipboard chip.</summary>
	public void ShowImportSheet(string url)
	{
		var existing = ViewModel.FindBySourceUrl(url);
		ImportSheet.Url = url;
		ImportSheet.Host = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : url;
		ImportSheet.Balance = ViewModel.CreditBalance;
		ImportSheet.IsDuplicate = existing is not null;
		ImportSheet.PreviewTitle = null;
		ImportSheet.PreviewImage = null;
		ImportSheet.Show();
	}

	private async void OnImportSheetImport(object? sender, EventArgs e)
	{
		ImportSheet.Hide();
		// Only clear the chip when it's the link actually being imported — this sheet can just as
		// easily be showing a share that arrived on top of an untouched clipboard suggestion.
		if (RecipeUrl.SameRecipe(ImportSheet.Url, ViewModel.ClipboardSuggestionUrl))
			ViewModel.DismissClipboardSuggestion();
		var result = await ViewModel.ImportUrlAsync(ImportSheet.Url, allowDuplicate: ImportSheet.IsDuplicate);
		if (result is { Outcome: PurePrep.Application.ImportOutcome.Imported, Recipe: { } saved })
			await Navigation.PushAsync(new RecipeDetailPage(saved, ViewModel));
		else if (result.Outcome == PurePrep.Application.ImportOutcome.OutOfCredits)
			await OpenBuyCreditsAsync();
	}

	// The sheet offered Buy Smart Credits instead of Import (zero balance): nothing is attempted.
	private async void OnImportSheetBuyCredits(object? sender, EventArgs e)
	{
		ImportSheet.Hide();
		await OpenBuyCreditsAsync();
	}

	private async void OnImportSheetOpenExisting(object? sender, EventArgs e)
	{
		ImportSheet.Hide();
		if (ViewModel.FindBySourceUrl(ImportSheet.Url) is { } existing)
			await Navigation.PushAsync(new RecipeDetailPage(existing, ViewModel));
	}

	private void OnImportSheetCancel(object? sender, EventArgs e) => ImportSheet.Hide();

	private void OnClipboardChipTapped(object? sender, EventArgs e)
	{
		if (ViewModel.ClipboardSuggestionUrl is { } url)
			ShowImportSheet(url);
	}

	private async void OnPasteTapped(object? sender, EventArgs e) =>
		await ((RecipeLibraryViewModel)BindingContext).PasteFromClipboardAsync();

	private async Task ImportPhotoAsync()
	{
		var vm = (RecipeLibraryViewModel)BindingContext;

		var take = AppResources.Get("ImportPhotoTake");
		var choose = AppResources.Get("ImportPhotoChoose");
		var cancel = AppResources.Get("Cancel");

		// Offer camera only where it's actually supported, otherwise fall straight to the gallery.
		string? action;
		if (MediaPicker.Default.IsCaptureSupported)
		{
			action = await Services.AppDialog.ChooseAsync(this, AppResources.Get("ImportPhotoTitle"), cancel, take, choose);
		}
		else
		{
			action = choose;
		}

		if (action == cancel || string.IsNullOrEmpty(action))
			return;

		try
		{
			var photo = action == take
				? await MediaPicker.Default.CapturePhotoAsync()
				: await MediaPicker.Default.PickPhotoAsync();
			if (photo is null)
				return;

			using var stream = await photo.OpenReadAsync();
			using var memory = new MemoryStream();
			await stream.CopyToAsync(memory);

			var mimeType = string.IsNullOrWhiteSpace(photo.ContentType) ? "image/jpeg" : photo.ContentType;
			await vm.ImportFromImageAsync(memory.ToArray(), mimeType);
		}
		catch (FeatureNotSupportedException)
		{
			await Services.AppDialog.AlertAsync(this, AppResources.Get("ImportPhotoTitle"), AppResources.Get("ImportPhotoUnavailable"), AppResources.Get("Cancel"));
		}
		catch (PermissionException)
		{
			await Services.AppDialog.AlertAsync(this, AppResources.Get("ImportPhotoTitle"), AppResources.Get("ImportPhotoUnavailable"), AppResources.Get("Cancel"));
		}
	}

	private async void OnSortRequested(object? sender, EventArgs e)
	{
		var vm = (RecipeLibraryViewModel)BindingContext;

		var recentlyAdded = AppResources.Get("SortRecentlyAdded");
		var recentlyCooked = AppResources.Get("SortRecentlyCooked");
		var alphabetical = AppResources.Get("SortAlphabetical");
		var cancel = AppResources.Get("Cancel");

		var choice = await Services.AppDialog.ChooseAsync(this, AppResources.Get("SortBy"), cancel, recentlyAdded, recentlyCooked, alphabetical);

		if (choice == recentlyAdded)
			vm.Sort = Domain.LibrarySort.RecentlyAdded;
		else if (choice == recentlyCooked)
			vm.Sort = Domain.LibrarySort.RecentlyCooked;
		else if (choice == alphabetical)
			vm.Sort = Domain.LibrarySort.Alphabetical;
	}

	private async Task<DuplicateImportAction> OnResolveDuplicateImportAsync(Domain.ParsedRecipe existing)
	{
		var replace = AppResources.Get("DuplicateReplace");
		var keepBoth = AppResources.Get("DuplicateKeepBoth");
		var cancel = AppResources.Get("Cancel");

		var choice = await Services.AppDialog.ChooseAsync(this, string.Format(AppResources.Get("DuplicateTitleFormat"), existing.Title), cancel, replace, keepBoth);

		if (choice == replace)
			return DuplicateImportAction.Replace;
		if (choice == keepBoth)
			return DuplicateImportAction.KeepBoth;
		return DuplicateImportAction.Cancel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var vm = ViewModel;

		if (!_hasLoaded)
		{
			_hasLoaded = true;
			await vm.LoadAsync();
		}
		else
		{
			// Returning from another page: reload the library so changes made elsewhere are reflected
			// on Home — most importantly a backup restore in Settings, which repopulates SQLite behind
			// our back. LoadAsync also refreshes the credit chip, so it covers the redeem-code case too.
			await vm.LoadAsync();
		}

		// Attach only once the library + balance are loaded: a cold-start pending share is collected
		// here and opens the Import sheet, which needs FindBySourceUrl and CreditBalance to reflect
		// real data, not the empty/unknown state from before LoadAsync ran.
		_coordinator?.Attach(this);

		await vm.CheckClipboardAsync();
	}

	// The paywall/import sheets are in-page overlays, so the hardware back button should close
	// whichever is open rather than pop/exit. MainActivity drives the back policy and calls this first.
	public bool OnHardwareBack()
	{
		if (AddSheet.IsOpen)
		{
			AddSheet.Hide();
			return true;
		}

		if (ImportSheet.IsVisible)
		{
			ImportSheet.Hide();
			return true;
		}

		var vm = ViewModel;
		if (vm.IsUpgradePromptVisible)
		{
			vm.CloseUpgradePrompt();
			return true;
		}

		return false;
	}

	private async void OnFocusRequested(object? sender, ParsedRecipe recipe)
	{
		// Same cook copy as the detail screen's Cook button: displayed translation, the cook's units and
		// remembered servings, read aloud in that text's language. The saved `recipe` is passed through
		// separately as the original, so "mark as cooked" persists on the saved recipe, not this copy.
		await Navigation.PushAsync(FocusPage.ForRecipe(recipe, (RecipeLibraryViewModel)BindingContext));
	}

	private async void OnDetailRequested(object? sender, ParsedRecipe recipe)
	{
		await Navigation.PushAsync(new RecipeDetailPage(recipe, (RecipeLibraryViewModel)BindingContext));
	}

	private async void OnSettingsTapped(object? sender, EventArgs e)
	{
		// The Add sheet's translate tip links here; close the sheet so it isn't still up on return.
		if (AddSheet.IsOpen)
			await AddSheet.HideAsync();
		var services = this.Handler?.MauiContext?.Services;
		var theme = services?.GetService(typeof(ThemeService)) as ThemeService;
		var credits = services?.GetService(typeof(PurePrep.Application.ISmartCreditsClient)) as PurePrep.Application.ISmartCreditsClient;
		var billing = services?.GetService(typeof(PurePrep.Application.IBillingService)) as PurePrep.Application.IBillingService;
		if (theme is not null)
			await Navigation.PushAsync(new SettingsPage(theme, credits, billing, (RecipeLibraryViewModel)BindingContext));
	}
}

