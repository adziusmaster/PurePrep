namespace PurePrep;

using PurePrep.Domain;
using PurePrep.Presentation;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		var viewModel = new RecipeLibraryViewModel();
		viewModel.FocusRequested += OnFocusRequested;
		BindingContext = viewModel;
	}

	private async void OnFocusRequested(object? sender, ParsedRecipe recipe)
	{
		await Navigation.PushAsync(new FocusPage(recipe));
	}
}
