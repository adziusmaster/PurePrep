namespace PurePrep;

using PurePrep.Domain;
using PurePrep.Presentation;
using PurePrep.Services;

public partial class MainPage : ContentPage
{
	private bool _hasLoaded;

	public MainPage(RecipeLibraryViewModel viewModel)
	{
		InitializeComponent();
		viewModel.FocusRequested += OnFocusRequested;
		viewModel.DetailRequested += OnDetailRequested;
		viewModel.AddManuallyRequested += OnAddManuallyRequested;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var vm = (RecipeLibraryViewModel)BindingContext;
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

	private async void OnFocusRequested(object? sender, ParsedRecipe recipe)
	{
		await Navigation.PushAsync(new FocusPage(recipe));
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
		if (theme is not null)
			await Navigation.PushAsync(new SettingsPage(theme, credits));
	}
}

