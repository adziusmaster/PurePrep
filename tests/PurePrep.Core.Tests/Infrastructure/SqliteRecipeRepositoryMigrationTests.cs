using FluentAssertions;
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

    [Fact]
    public async Task GetAllAsync_WhenDatabaseIsFrom133_ShouldLoadWithDefaultsForNewFields()
    {
        // Arrange — a 1.3.3-shaped table: translation columns present, 1.4 columns absent, old StepsJson.
        await Create133DatabaseAsync();
        var repository = new SqliteRecipeRepository(new FileContextFactory(ConnectionString));

        // Act
        var recipes = await repository.GetAllAsync();

        // Assert
        var old = recipes.Should().ContainSingle().Subject;
        old.Status.Should().Be(RecipeStatus.WantToCook);
        old.Servings.Should().BeNull();
        old.Steps[0].Timers.Should().BeEmpty();
        old.Steps[0].IngredientRefs.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WhenRecipeHasAllNewFields_ShouldRoundTripThem()
    {
        // Arrange
        var repository = new SqliteRecipeRepository(new FileContextFactory(ConnectionString));
        var recipe = new ParsedRecipe
        {
            Title = "Pasta",
            Ingredients = ["200 g pasta"],
            Steps = [new RecipeStep { Order = 1, Instruction = "Boil.", Timers = [new RecipeTimer("Boil", 600, 720)], IngredientRefs = [0] }],
        };
        await repository.SaveAsync(recipe);
        var updated = recipe.WithNotes("Less salt").WithFavourite(true).WithServings(4).WithChosenServings(2)
            .WithImage("images/p.jpg").MarkCooked(DateTimeOffset.UnixEpoch) with
            { ServingsNoun = "people", ServingsEstimated = true, PrepMinutes = 5, CookMinutes = 12 };

        // Act
        await repository.UpdateAsync(updated);
        await repository.UpdateImagePathAsync(updated.Id, updated.ImagePath);
        var loaded = (await repository.GetAllAsync()).Single();

        // Assert
        loaded.Should().BeEquivalentTo(updated, o => o
            .Excluding(r => r.Translations)
            .Excluding(r => r.SavedAt));
    }

    [Fact]
    public async Task UpdateAsync_WhenCopyPredatesAttachedPhoto_ShouldKeepStoredImagePath()
    {
        // Arrange — the import returned the recipe, then the photo attached in the background.
        var repository = new SqliteRecipeRepository(new FileContextFactory(ConnectionString));
        var imported = new ParsedRecipe { Title = "Pasta" };
        await repository.SaveAsync(imported);
        await repository.UpdateImagePathAsync(imported.Id, "images/p.jpg");

        // Act — the detail page persists a favourite from its image-less copy.
        await repository.UpdateAsync(imported.WithFavourite(true));
        var loaded = (await repository.GetAllAsync()).Single();

        // Assert
        loaded.ImagePath.Should().Be("images/p.jpg");
        loaded.IsFavourite.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateImagePathAsync_WhenNull_ShouldClearThePhoto()
    {
        // Arrange
        var repository = new SqliteRecipeRepository(new FileContextFactory(ConnectionString));
        var recipe = new ParsedRecipe { Title = "Pasta" }.WithImage("images/p.jpg");
        await repository.SaveAsync(recipe);

        // Act
        await repository.UpdateImagePathAsync(recipe.Id, null);
        var loaded = (await repository.GetAllAsync()).Single();

        // Assert
        loaded.ImagePath.Should().BeNull();
    }

    [Fact]
    public async Task UpdateImagePathAsync_WhenRecipeMissing_ShouldDoNothing()
    {
        // Arrange
        var repository = new SqliteRecipeRepository(new FileContextFactory(ConnectionString));

        // Act
        Func<Task> act = async () => await repository.UpdateImagePathAsync(Guid.NewGuid(), "images/x.jpg");

        // Assert
        await act.Should().NotThrowAsync();
        (await repository.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenCalledOnFreshDatabase_ShouldNotThrowOnRepeatedMigration()
    {
        // Arrange
        var repository = new SqliteRecipeRepository(new FileContextFactory(ConnectionString));

        // Act
        Func<Task> act = async () =>
        {
            await repository.SaveAsync(new ParsedRecipe { Title = "A" });
            await repository.SaveAsync(new ParsedRecipe { Title = "B" });
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    private async Task Create133DatabaseAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Recipes (Id TEXT NOT NULL PRIMARY KEY, Title TEXT NOT NULL, SourceUrl TEXT NULL,
              IngredientsJson TEXT NOT NULL, StepsJson TEXT NOT NULL, SourceSystem TEXT NOT NULL DEFAULT 'Metric',
              SavedAt TEXT NOT NULL, OriginalLanguage TEXT NULL, DisplayLanguage TEXT NULL,
              TranslationsJson TEXT NOT NULL DEFAULT '');
            INSERT INTO Recipes VALUES ('7d1f2a3b-0000-0000-0000-000000000001','Old','https://a.b/c',
              '["1 egg"]','[{"Order":1,"Instruction":"Boil 5 min."}]','Metric','2026-01-01 00:00:00+00:00',NULL,NULL,'');
            """;
        await cmd.ExecuteNonQueryAsync();
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
