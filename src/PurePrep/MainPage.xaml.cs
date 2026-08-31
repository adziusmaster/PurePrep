namespace PurePrep;

using System.ComponentModel;
using PurePrep.Domain;
using PurePrep.Presentation;
using PurePrep.Services;

public partial class MainPage : ContentPage, IHardwareBackHandler
{
	private bool _hasLoaded;

	private readonly SharedUrlRelay? _sharedUrls;

	public MainPage(RecipeLibraryViewModel viewModel)
	{
		InitializeComponent();
		_sharedUrls = IPlatformApplication.Current?.Services.GetService<SharedUrlRelay>();
		if (_sharedUrls is not null)
		{
			_sharedUrls.Received += OnSharedUrlReceived;
			_sharedUrls.ReceivedWithoutUrl += OnSharedUrlMissing;
		}
		viewModel.FocusRequested += OnFocusRequested;
		viewModel.DetailRequested += OnDetailRequested;
		viewModel.AddManuallyRequested += OnAddManuallyRequested;
		viewModel.SettingsRequested += OnSettingsTapped;
		viewModel.PropertyChanged += OnViewModelPropertyChanged;
		BindingContext = viewModel;
	}

	private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(RecipeLibraryViewModel.IsUpgradePromptVisible))
			BackgroundBlur.Apply(ContentRoot, ((RecipeLibraryViewModel)BindingContext).IsUpgradePromptVisible);
	}

	private void OnSharedUrlReceived(object? sender, string url) =>
		Dispatcher.Dispatch(() =>
		{
			((RecipeLibraryViewModel)BindingContext).ApplySharedUrl(url);
			_sharedUrls?.Clear();
		});

	private void OnSharedUrlMissing(object? sender, EventArgs e) =>
		Dispatcher.Dispatch(() => ((RecipeLibraryViewModel)BindingContext).ReportSharedUrlMissing());

	private async void OnPasteTapped(object? sender, EventArgs e) =>
		await ((RecipeLibraryViewModel)BindingContext).PasteFromClipboardAsync();

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var vm = (RecipeLibraryViewModel)BindingContext;

		// A share that arrived before this page existed (cold start) is waiting to be collected.
		if (_sharedUrls?.TakePending() is { } sharedUrl)
			vm.ApplySharedUrl(sharedUrl);
		if (!_hasLoaded)
		{
			_hasLoaded = true;
			await vm.LoadAsync();
		}
		else
		{
			// Returning from another page (e.g. after redeeming a code): refresh the credit chip.
			await vm.RefreshCreditsAsync();
		}
	}

	// The paywall sheet is an in-page overlay, so the hardware back button should close it rather
	// than pop/exit. MainActivity drives the back policy and calls this first.
	public bool OnHardwareBack()
	{
		var vm = (RecipeLibraryViewModel)BindingContext;
		if (vm.IsUpgradePromptVisible)
		{
			vm.CloseUpgradePrompt();
			return true;
		}

		return false;
	}

	private async void OnFocusRequested(object? sender, ParsedRecipe recipe)
	{
		// Convert to the user's chosen units first. Cooking straight from a library card used to
		// hand Focus Mode the raw recipe, so the same button behaved differently here and on the
		// detail screen — metric quantities for someone who had selected imperial.
		await Navigation.PushAsync(new FocusPage(RecipeUnits.ForDisplay(recipe)));
	}

	private async void OnDetailRequested(object? sender, ParsedRecipe recipe)
	{
		await Navigation.PushAsync(new RecipeDetailPage(recipe, (RecipeLibraryViewModel)BindingContext));
	}

	private async void OnAddManuallyRequested(object? sender, EventArgs e)
	{
		await Navigation.PushAsync(new ManualAddPage((RecipeLibraryViewModel)BindingContext));
	}

	private async void OnSettingsTapped(object? sender, EventArgs e)
	{
		var services = this.Handler?.MauiContext?.Services;
		var theme = services?.GetService(typeof(ThemeService)) as ThemeService;
		var credits = services?.GetService(typeof(PurePrep.Application.ISmartCreditsClient)) as PurePrep.Application.ISmartCreditsClient;
		var billing = services?.GetService(typeof(PurePrep.Application.IBillingService)) as PurePrep.Application.IBillingService;
		if (theme is not null)
			await Navigation.PushAsync(new SettingsPage(theme, credits, billing));
	}
}

