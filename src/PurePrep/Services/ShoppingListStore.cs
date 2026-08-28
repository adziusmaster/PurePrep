using System.Text.Json;
using PurePrep.Domain;

namespace PurePrep.Services;

/// <summary>
/// Persists the shopping list to a small JSON file in app data.
///
/// A file rather than a database table: the list is a handful of lines, it is rewritten wholesale
/// on every change, and keeping it out of the recipe schema avoids a migration on a database that
/// already holds users' libraries.
/// </summary>
public sealed class ShoppingListStore
{
    private readonly string _path = Path.Combine(FileSystem.AppDataDirectory, "shopping-list.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<IReadOnlyList<ShoppingListItem>> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_path))
                return [];

            var json = await File.ReadAllTextAsync(_path, ct);
            return JsonSerializer.Deserialize<ShoppingListItem[]>(json) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable list must not stop the app opening; an empty list is
            // recoverable, a crash on launch is not.
            return [];
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(items), ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: losing a shopping list is not worth an unhandled exception.
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Adds a recipe's ingredients, merging quantities into any matching lines.</summary>
    public async Task<int> AddAsync(IEnumerable<string> ingredients, string? source, CancellationToken ct = default)
    {
        var lines = ingredients.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var current = await LoadAsync(ct);
        await SaveAsync(ShoppingList.Add(current, lines, source), ct);
        return lines.Length;
    }
}
