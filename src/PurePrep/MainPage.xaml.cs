namespace PurePrep;

using PurePrep.Domain;
using PurePrep.Presentation;

public partial class MainPage : ContentPage
{
	private bool _hasLoaded;

	public MainPage(RecipeLibraryViewModel viewModel)
	{
		InitializeComponent();
		viewModel.FocusRequested += OnFocusRequested;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_hasLoaded) return;
		_hasLoaded = true;
		await ((RecipeLibraryViewModel)BindingContext).LoadAsync();
	}

	private async void OnFocusRequested(object? sender, ParsedRecipe recipe)
	{
		await Navigation.PushAsync(new FocusPage(recipe));
	}
}
