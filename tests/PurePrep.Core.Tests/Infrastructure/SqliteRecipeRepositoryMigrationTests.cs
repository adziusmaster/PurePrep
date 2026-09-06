using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PurePrep.Domain;
using PurePrep.Infrastructure;

namespace PurePrep.Core.Tests.Infrastructure;

/// <summary>
/// Guards the additive on-startup schema migration in <see cref="SqliteRecipeRepository"/>. A legacy
/// database (created before the translation columns existed) must upgrade in place without throwing —
/// this is the exact path that crashed on real upgraded installs when the DEFAULT '{}' literal was
/// fed through ExecuteSqlRaw's string.Format and raised a FormatException.
/// </summary>
public sealed class SqliteRecipeRepositoryMigrationTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"pureprep-mig-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_dbPath}";

    [Fact]
    public async Task GetAllAsync_UpgradesLegacyDatabaseWithoutTranslationColumns()
    {
        var recipeId = Guid.NewGuid();
        await CreateLegacyDatabaseAsync(recipeId);

        var repository = new SqliteRecipeRepository(new FileContextFactory(ConnectionString));

        // Would throw FormatException on the ALTER TABLE ... DEFAULT before the fix.
        var recipes = await repository.GetAllAsync();

        var recipe = Assert.Single(recipes);
        Assert.Equal(recipeId, recipe.Id);
        Assert.Equal("Legacy Loaf", recipe.Title);
        Assert.Empty(recipe.Translations);
        Assert.Null(recipe.DisplayLanguage);

        // The upgraded schema now carries the three translation columns.
        var columns = await GetColumnsAsync();
        Assert.Contains("OriginalLanguage", columns);
        Assert.Contains("DisplayLanguage", columns);
        Assert.Contains("TranslationsJson", columns);
    }

    private async Task CreateLegacyDatabaseAsync(Guid recipeId)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();

        await using (var create = connection.CreateCommand())
        {
            // Schema as it existed BEFORE the translation columns were added.
            create.CommandText =
                """
                CREATE TABLE "Recipes" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Recipes" PRIMARY KEY,
                    "Title" TEXT NOT NULL,
                    "SourceUrl" TEXT NULL,
                    "IngredientsJson" TEXT NOT NULL,
                    "StepsJson" TEXT NOT NULL,
                    "SourceSystem" TEXT NOT NULL DEFAULT 'Metric',
                    "SavedAt" TEXT NOT NULL
                );
                """;
            await create.ExecuteNonQueryAsync();
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                INSERT INTO "Recipes" ("Id", "Title", "SourceUrl", "IngredientsJson", "StepsJson", "SourceSystem", "SavedAt")
                VALUES ($id, 'Legacy Loaf', NULL, '["Flour"]', '[]', 'Metric', $savedAt);
                """;
            insert.Parameters.AddWithValue("$id", recipeId.ToString());
            insert.Parameters.AddWithValue("$savedAt", DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<string>> GetColumnsAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('Recipes')";
        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    private sealed class FileContextFactory(string connectionString) : IDbContextFactory<PurePrepDbContext>
    {
        public PurePrepDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<PurePrepDbContext>()
                .UseSqlite(connectionString)
                .Options;
            return new PurePrepDbContext(options);
        }
    }
}
