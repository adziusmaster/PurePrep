using Microsoft.Extensions.DependencyInjection;
using PurePrep.Presentation;

namespace PurePrep;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var viewModel = _services.GetRequiredService<RecipeLibraryViewModel>();
		return new Window(new NavigationPage(new MainPage(viewModel)));
	}
}