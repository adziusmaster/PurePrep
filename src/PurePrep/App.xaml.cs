using Microsoft.Extensions.DependencyInjection;
using PurePrep.Presentation;
using PurePrep.Services;

namespace PurePrep;

public partial class App : Microsoft.Maui.Controls.Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;

		// Apply the persisted appearance before the first window/pages are built so tokens resolve.
		_services.GetRequiredService<ThemeService>().Apply();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var viewModel = _services.GetRequiredService<RecipeLibraryViewModel>();
		return new Window(new NavigationPage(new MainPage(viewModel)));
	}
}