using Microsoft.Extensions.DependencyInjection;
using PurePrep.Application;

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
		var parser = _services.GetRequiredService<IRecipeParser>();
		var repository = _services.GetRequiredService<IRecipeRepository>();
		return new Window(new NavigationPage(new MainPage(parser, repository)));
	}
}