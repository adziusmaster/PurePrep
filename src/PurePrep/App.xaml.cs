using Microsoft.Extensions.DependencyInjection;
using PurePrep.Localization;
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

		// Apply the persisted language + appearance before the first window/pages are built,
		// so localized XAML strings and theme tokens resolve correctly on first render.
		LocalizationService.Apply();
		_services.GetRequiredService<ThemeService>().Apply();

		// Resolved eagerly (rather than lazily on first use) so its SharedUrlRelay subscriptions
		// exist before a share intent can arrive.
		_services.GetRequiredService<ImportCoordinator>();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// The clipboard re-check on return from another app is driven by MainActivity.OnWindowFocusChanged,
		// not Window.Resumed: Android 10+ denies clipboard reads until the window has focus, which it
		// does not yet have when Resumed fires.
		return new Window(BuildRootPage());
	}

	private Page BuildRootPage()
	{
		var viewModel = _services.GetRequiredService<RecipeLibraryViewModel>();
		return new NavigationPage(new MainPage(viewModel));
	}

	/// <summary>Persists the language and rebuilds the root page so all XAML reloads in the new culture.</summary>
	public void ApplyLanguageAndReload(string code)
	{
		LocalizationService.Set(code);
		if (Windows.Count > 0)
			Windows[0].Page = BuildRootPage();
	}
}