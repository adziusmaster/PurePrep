using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PurePrep.Application;
using PurePrep.Infrastructure;
using PurePrep.Presentation;
using PurePrep.Services;

namespace PurePrep;

public static class MauiProgram
{
	// Backend base URL for the AI Smart Parser + credit endpoints.
	// - Release builds target the deployed backend over HTTPS.
	// - Debug builds target 10.0.2.2, the Android emulator's alias for the host
	//   machine running the local server (dotnet run on PurePrep.Server).
#if DEBUG
	private const string BackendBaseUrl = "http://10.0.2.2:5299/";
#else
	private const string BackendBaseUrl = "https://api.pureprep.lechdigital.nl/";
#endif

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		var databasePath = Path.Combine(FileSystem.AppDataDirectory, "pureprep.db");
		builder.Services.AddDbContextFactory<PurePrepDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
		builder.Services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();

		// Anonymous device identity + in-app billing (Google Play). Billing is stubbed until the
		// Play-signed build; the paywall degrades gracefully when it is unsupported.
		builder.Services.AddSingleton<IDeviceIdentity, SecureStorageDeviceIdentity>();
#if ANDROID
		builder.Services.AddSingleton<IBillingService, PurePrep.Platforms.Android.PlayBillingService>();
#else
		builder.Services.AddSingleton<IBillingService, UnsupportedBillingService>();
#endif
		builder.Services.AddSingleton<ThemeService>();

		// Carries links shared into the app from the Android share sheet across to the library page.
		builder.Services.AddSingleton<SharedUrlRelay>();

		// Cook timers outlive the Focus Mode page, so the countdown survives navigating away.
#if ANDROID
		builder.Services.AddSingleton<PurePrep.Application.ICookTimerNotifier, PurePrep.Platforms.Android.CookTimerNotifier>();
#else
		builder.Services.AddSingleton<PurePrep.Application.ICookTimerNotifier, PurePrep.Application.UnsupportedCookTimerNotifier>();
#endif
		builder.Services.AddSingleton<CookTimerService>();

		// Shopping list, persisted as a small JSON file alongside the recipe database.
		builder.Services.AddSingleton<ShoppingListStore>();

		// Language newly imported recipes are produced in (defaults to the app UI language).
		builder.Services.AddSingleton<IRecipeLanguageProvider, RecipeLanguageSettings>();

		// On-device offline translation (free). Real ML Kit impl on Android; no-op elsewhere.
#if ANDROID
		builder.Services.AddSingleton<ITranslationService, PurePrep.Platforms.Android.MlKitTranslationService>();
#else
		builder.Services.AddSingleton<ITranslationService, UnsupportedTranslationService>();
#endif

		// Link import is powered by the backend AI Smart Parser and is gated by server-side credits.
		builder.Services.AddHttpClient<IRecipeParser, AiProxyRecipeParser>(client =>
		{
			client.BaseAddress = new Uri(BackendBaseUrl);
			client.Timeout = TimeSpan.FromSeconds(30);
		});
		builder.Services.AddHttpClient<ISmartCreditsClient, HttpSmartCreditsClient>(client =>
		{
			client.BaseAddress = new Uri(BackendBaseUrl);
			client.Timeout = TimeSpan.FromSeconds(15);
		});

		builder.Services.AddTransient<RecipeLibraryViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
