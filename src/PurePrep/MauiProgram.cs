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
	// - Release builds, and Debug builds by default, target the deployed production backend over
	//   HTTPS. A local backend is opt-in: build with -p:PurePrepLocalApi=true (defines LOCAL_API,
	//   Debug configuration only — see PurePrep.csproj) to point Debug builds at
	//   http://localhost:5299/ instead, then forward the port:
	//     adb reverse tcp:5299 tcp:5299
	//   That works for both a physical device and the emulator. (The emulator also has its own
	//   host alias, 10.0.2.2, if you'd rather skip adb reverse there — swap it in locally, don't
	//   commit it back.) LOCAL_API also unlocks cleartext-to-localhost only in the Android manifest
	//   (see PurePrep.csproj's AndroidManifestOverlay); without it, plain HTTP is blocked.
	// - The `DEBUG &&` half of the check below is belt-and-braces: PurePrep.csproj already scopes
	//   LOCAL_API to Configuration=='Debug', but this line must never resolve to the localhost URL
	//   in a Release build even if that guard were ever loosened by mistake.
#if DEBUG && LOCAL_API
	private const string BackendBaseUrl = "http://localhost:5299/";
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
				fonts.AddFont("MaterialSymbolsRounded.ttf", "MaterialSymbols");
			});

		// Switch colours: MAUI paints OnColor/ThumbColor as colour filters and leaves the off state
		// to the native (night-mode) defaults, which vanish on the light theme's white cards. After
		// every colour/state mapping, drop the filters and set explicit on/off tint lists instead.
		// OnColor/ThumbColor are DynamicResource-bound, so a theme swap re-runs this with new colours.
#if ANDROID
		foreach (var key in new[] { nameof(ISwitch.TrackColor), nameof(ISwitch.ThumbColor), nameof(ISwitch.IsOn) })
			Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping(key, (handler, view) => ApplySwitchColors(handler.PlatformView, view));
#endif

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

		// Direct "save to device" backup (public Downloads) alongside the share sheet.
#if ANDROID
		builder.Services.AddSingleton<ILocalBackupSaver, PurePrep.Platforms.Android.MediaStoreBackupSaver>();
#else
		builder.Services.AddSingleton<ILocalBackupSaver, UnsupportedLocalBackupSaver>();
#endif

		// Carries links shared into the app from the Android share sheet across to the library page.
		builder.Services.AddSingleton<SharedUrlRelay>();

		// Routes every "here's a link" moment (share, clipboard chip) to the Import sheet on Home.
		builder.Services.AddSingleton<ImportCoordinator>();

		// Cook timers outlive the Focus Mode page, so the countdown survives navigating away.
#if ANDROID
		builder.Services.AddSingleton<PurePrep.Application.ICookTimerNotifier, PurePrep.Platforms.Android.CookTimerNotifier>();
#else
		builder.Services.AddSingleton<PurePrep.Application.ICookTimerNotifier, PurePrep.Application.UnsupportedCookTimerNotifier>();
#endif
		builder.Services.AddSingleton<CookTimerService>();

		// Reads steps aloud in the recipe's own language (resolves an installed TTS voice for it).
		builder.Services.AddSingleton<PurePrep.Services.ReadAloudService>();

		// Hands-free voice step navigation in Focus Mode (Android on-device speech recognition).
#if ANDROID
		builder.Services.AddSingleton<PurePrep.Application.IVoiceCommandListener, PurePrep.Platforms.Android.VoiceCommandListener>();
#else
		builder.Services.AddSingleton<PurePrep.Application.IVoiceCommandListener, PurePrep.Application.UnsupportedVoiceCommandListener>();
#endif

		// Language newly imported recipes are produced in (defaults to the app UI language).
		builder.Services.AddSingleton<IRecipeLanguageProvider, RecipeLanguageSettings>();

		// AI recipe translation (backend Gemini). Replaces the retired on-device ML Kit packs: results
		// are cached per language on the device, so each language is paid for once and then free/offline.
		builder.Services.AddHttpClient<IRecipeTranslator, AiProxyRecipeTranslator>(client =>
		{
			client.BaseAddress = new Uri(BackendBaseUrl);
			client.Timeout = TimeSpan.FromSeconds(30);
		});

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

		// Recipe photo: the page's own image is downloaded, shrunk to editor size and stored; when that
		// fails, an AI-generated one is fetched instead (already paid for by the import's Smart Credit).
		builder.Services.AddSingleton<IRecipePhotoShrinker, RecipePhotoShrinker>();
		builder.Services.AddHttpClient<IRecipeImageStore, FileRecipeImageStore>((http, services) =>
			new FileRecipeImageStore(
				http,
				FileSystem.AppDataDirectory,
				services.GetRequiredService<IRecipePhotoShrinker>(),
				services.GetService<ILogger<FileRecipeImageStore>>()));
		// Image generation takes longer than a text call, and 30 s can cut it off while the server is
		// still waiting on the model (the single-use ticket is then spent for nothing).
		// It runs in the background after the recipe is already open, so a generous limit costs nothing.
		builder.Services.AddHttpClient<IRecipeImageGenerator, AiProxyRecipeImageGenerator>(client =>
		{
			client.BaseAddress = new Uri(BackendBaseUrl);
			client.Timeout = TimeSpan.FromSeconds(90);
		});
		builder.Services.AddSingleton<RecipeImageAttacher>();

		builder.Services.AddTransient<RecipeLibraryViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
		// Debug-level lines from the app's own code (e.g. why an imported recipe got no photo).
		builder.Logging.AddFilter("PurePrep", LogLevel.Debug);
#endif

		return builder.Build();
	}

#if ANDROID
	private static void ApplySwitchColors(AndroidX.AppCompat.Widget.SwitchCompat platformView, ISwitch view)
	{
		static Android.Graphics.Color Native(Color color) => new(
			(byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255), (byte)(color.Alpha * 255));

		static Color Token(string key, Color fallback) =>
			Microsoft.Maui.Controls.Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
				? color
				: fallback;

		var trackOn = view.TrackColor ?? Token("SwitchTrackOn", Colors.YellowGreen);
		var thumbOn = view.ThumbColor ?? Token("SwitchThumbOn", Colors.White);
		var trackOff = Token("SwitchTrackOff", Colors.LightGray);
		var thumbOff = Token("SwitchThumbOff", Colors.Gray);

		int[][] states = [[Android.Resource.Attribute.StateChecked], []];
		platformView.TrackDrawable?.ClearColorFilter();
		platformView.ThumbDrawable?.ClearColorFilter();
		platformView.TrackTintList = new Android.Content.Res.ColorStateList(states, [Native(trackOn), Native(trackOff)]);
		platformView.ThumbTintList = new Android.Content.Res.ColorStateList(states, [Native(thumbOn), Native(thumbOff)]);
	}
#endif
}
