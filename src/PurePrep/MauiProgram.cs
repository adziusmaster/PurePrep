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
		builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
		builder.Services.AddSingleton<IRecipeParser, RecipeParser>();
		builder.Services.AddSingleton<IRecipeRepository, SqliteRecipeRepository>();
		builder.Services.AddTransient<RecipeLibraryViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
