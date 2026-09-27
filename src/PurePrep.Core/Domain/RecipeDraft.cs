namespace PurePrep.Domain;

public sealed class DraftIngredient
{
    public Guid Key { get; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
}

public sealed class DraftStep
{
    public Guid Key { get; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public List<Guid> IngredientKeys { get; } = new();
    public IReadOnlyList<RecipeTimer> Timers { get; set; } = Array.Empty<RecipeTimer>();
}

/// <summary>
/// Editable, list-shaped copy of a recipe. Steps reference ingredients by stable key while editing,
/// so reordering or deleting ingredients never points a step at the wrong one; keys become indexes
/// again only when the draft is applied.
/// </summary>
public sealed class RecipeDraft
{
    public string Title { get; set; } = string.Empty;
    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }
    public List<DraftIngredient> Ingredients { get; } = new();
    public List<DraftStep> Steps { get; } = new();

    public static RecipeDraft Empty() => new();

    public static RecipeDraft FromRecipe(ParsedRecipe recipe)
    {
        var draft = new RecipeDraft
        {
            Title = recipe.Title,
            Servings = recipe.Servings,
            PrepMinutes = recipe.PrepMinutes,
            CookMinutes = recipe.CookMinutes,
        };
        foreach (var line in recipe.Ingredients)
            draft.Ingredients.Add(new DraftIngredient { Text = line });
        foreach (var step in recipe.Steps.OrderBy(s => s.Order))
        {
            var draftStep = new DraftStep { Text = step.Instruction, Timers = step.Timers };
            foreach (var index in step.IngredientRefs.Where(i => i >= 0 && i < draft.Ingredients.Count))
                draftStep.IngredientKeys.Add(draft.Ingredients[index].Key);
            draft.Steps.Add(draftStep);
        }
        return draft;
    }

    public DraftIngredient InsertIngredient(int index, string text = "")
    {
        var row = new DraftIngredient { Text = text };
        Ingredients.Insert(Math.Clamp(index, 0, Ingredients.Count), row);
        return row;
    }

    public void RemoveIngredient(Guid key)
    {
        Ingredients.RemoveAll(i => i.Key == key);
        foreach (var step in Steps)
            step.IngredientKeys.Remove(key);
    }

    public void ReorderIngredients(IReadOnlyList<Guid> keysInOrder) => Reorder(Ingredients, keysInOrder, i => i.Key);

    public IReadOnlyList<DraftIngredient> PasteIngredients(int index, string text)
    {
        var rows = SplitLines(text).Select(line => new DraftIngredient { Text = line }).ToList();
        Ingredients.InsertRange(Math.Clamp(index, 0, Ingredients.Count), rows);
        return rows;
    }

    public DraftStep InsertStep(int index, string text = "")
    {
        var step = new DraftStep { Text = text, Timers = StepTimerResolver.Detect(text) };
        Steps.Insert(Math.Clamp(index, 0, Steps.Count), step);
        return step;
    }

    public void RemoveStep(Guid key) => Steps.RemoveAll(s => s.Key == key);

    public void ReorderSteps(IReadOnlyList<Guid> keysInOrder) => Reorder(Steps, keysInOrder, s => s.Key);

    /// <summary>
    /// Updates a step's text and re-detects its timers, keeping the parser's label for any timer whose
    /// duration still appears so an edit doesn't throw away "Fry onion" in favour of "Gently fry the".
    /// </summary>
    public void UpdateStepText(Guid key, string text)
    {
        var step = Steps.FirstOrDefault(s => s.Key == key);
        if (step is null)
            return;
        step.Text = text;
        var previous = step.Timers;
        step.Timers = StepTimerResolver.Detect(text)
            .Select(t => previous.FirstOrDefault(p => p.MinSeconds == t.MinSeconds && p.MaxSeconds == t.MaxSeconds) ?? t)
            .ToArray();
    }

    public ParsedRecipe ApplyTo(ParsedRecipe original)
    {
        var (ingredients, steps) = Build();
        return original with
        {
            Title = Title.Trim(),
            Servings = Servings,
            // A yield the cook typed in themselves is no longer a model estimate.
            ServingsEstimated = original.ServingsEstimated && Servings == original.Servings,
            PrepMinutes = PrepMinutes,
            CookMinutes = CookMinutes,
            Ingredients = ingredients,
            Steps = steps,
            // The original-language text changed; cached translations no longer match it.
            Translations = new Dictionary<string, RecipeTranslation>(StringComparer.OrdinalIgnoreCase),
            DisplayLanguage = null,
        };
    }

    public ParsedRecipe ToNewRecipe()
    {
        var (ingredients, steps) = Build();
        return new ParsedRecipe
        {
            Title = Title.Trim(),
            Servings = Servings,
            PrepMinutes = PrepMinutes,
            CookMinutes = CookMinutes,
            Ingredients = ingredients,
            Steps = steps,
        };
    }

    private (string[] Ingredients, RecipeStep[] Steps) Build()
    {
        var kept = Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.Text)).ToList();
        var indexByKey = kept.Select((i, index) => (i.Key, index)).ToDictionary(x => x.Key, x => x.index);
        var steps = Steps
            .Where(s => !string.IsNullOrWhiteSpace(s.Text))
            .Select((s, index) => new RecipeStep
            {
                Order = index + 1,
                Instruction = s.Text.Trim(),
                Timers = s.Timers,
                IngredientRefs = s.IngredientKeys.Where(indexByKey.ContainsKey).Select(k => indexByKey[k]).ToArray(),
            })
            .ToArray();
        return (kept.Select(i => i.Text.Trim()).ToArray(), steps);
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Split('\n')
            .Select(line => line.Trim().TrimStart('-', '•', '*', '·').Trim())
            .Where(line => line.Length > 0);

    private static void Reorder<T>(List<T> list, IReadOnlyList<Guid> keysInOrder, Func<T, Guid> key)
    {
        var byKey = list.ToDictionary(key);
        var ordered = keysInOrder.Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
        ordered.AddRange(list.Where(item => !keysInOrder.Contains(key(item))));
        list.Clear();
        list.AddRange(ordered);
    }
}
