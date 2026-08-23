namespace PurePrep;

using PurePrep.Domain;
using PurePrep.Application;
using PurePrep.Presentation;

public partial class MainPage : ContentPage
{
	private readonly IRecipeRepository _repository;
	private bool _hasLoaded;

	public MainPage(IRecipeParser parser, IRecipeRepository repository)
	{
		InitializeComponent();
		_repository = repository;
		var viewModel = new RecipeLibraryViewModel(parser, repository);
		viewModel.FocusRequested += OnFocusRequested;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_hasLoaded) return;
		_hasLoaded = true;
		var savedRecipes = await _repository.GetAllAsync();
		if (savedRecipes.Count == 0) return;
		var viewModel = (RecipeLibraryViewModel)BindingContext;
		viewModel.Recipes.Clear();
		foreach (var recipe in savedRecipes) viewModel.Recipes.Add(recipe);
	}

	private async void OnFocusRequested(object? sender, ParsedRecipe recipe)
	{
		await Navigation.PushAsync(new FocusPage(recipe));
	}
}
