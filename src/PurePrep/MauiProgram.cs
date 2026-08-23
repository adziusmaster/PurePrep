using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using PurePrep.Application;
using PurePrep.Infrastructure;
using PurePrep.Presentation;

namespace PurePrep;

public static class MauiProgram
{
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
		builder.Services.AddSingleton(_ =>
		{
			var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
			// Many recipe sites reject requests without a browser-like User-Agent (HTTP 403).
			client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Mobile Safari/537.36");
			client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
			client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
			return client;
		});
		builder.Services.AddSingleton<IRecipeParser, RecipeParser>();
		builder.Services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();
		builder.Services.AddTransient<RecipeLibraryViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
