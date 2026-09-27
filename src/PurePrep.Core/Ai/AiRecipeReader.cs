using System.Text.Json;

namespace PurePrep.Ai;

/// <summary>
/// Reads the model's JSON defensively: anything malformed or out of range is dropped on its own, so
/// a bad timer or reference never costs the user their import. Value-shape problems (a string where a
/// number is expected, an object where a string is expected, ...) never throw here — every access is
/// guarded by a <see cref="JsonValueKind"/> check first. Invalid top-level JSON still throws
/// <see cref="JsonException"/> from <see cref="JsonDocument.Parse(string, JsonDocumentOptions)"/>;
/// callers already map that to a service error.
/// </summary>
public static class AiRecipeReader
{
    private const int MaxTimerSeconds = 24 * 3600;

    public static AiRecipe Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var ingredients = StringArray(root, "ingredients");
        var details = new List<AiStep>();
        if (root.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in steps.EnumerateArray())
            {
                if (ReadStep(step, ingredients.Length) is { } parsed)
                    details.Add(parsed);
            }
        }

        var meta = new AiRecipeMeta(
            Int(root, "servings", 1, 99),
            Str(root, "servingsNoun"),
            root.TryGetProperty("servingsEstimated", out var est) && est.ValueKind == JsonValueKind.True,
            Int(root, "prepMinutes", 0, 24 * 60),
            Int(root, "cookMinutes", 0, 7 * 24 * 60));

        return new AiRecipe(Str(root, "title") ?? string.Empty, ingredients, details.Select(d => d.Text).ToArray())
        {
            Meta = meta,
            StepDetails = details,
            TimerLabels = StringArray(root, "timerLabels"),
        };
    }

    private static AiStep? ReadStep(JsonElement step, int ingredientCount)
    {
        if (step.ValueKind == JsonValueKind.String)
        {
            var legacy = step.GetString()?.Trim();
            return string.IsNullOrEmpty(legacy) ? null : new AiStep(legacy, [], []);
        }
        if (step.ValueKind != JsonValueKind.Object)
            return null;

        var text = Str(step, "text");
        if (text is null)
            return null;

        var refs = step.TryGetProperty("ingredientRefs", out var r) && r.ValueKind == JsonValueKind.Array
            ? r.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out _))
                .Select(x => x.GetInt32())
                .Where(i => i >= 0 && i < ingredientCount)
                .Distinct()
                .ToArray()
            : [];

        var timers = new List<AiTimer>();
        if (step.TryGetProperty("timers", out var t) && t.ValueKind == JsonValueKind.Array)
        {
            foreach (var timer in t.EnumerateArray())
            {
                if (ReadTimer(timer) is { } parsed)
                    timers.Add(parsed);
            }
        }
        return new AiStep(text, refs, timers.ToArray());
    }

    // A timer with a PRESENT but invalid/out-of-range maxSeconds is dropped entirely — the model gave
    // us a number it meant, and it was garbage, so the whole timer is untrustworthy. Only an ABSENT
    // maxSeconds falls back to minSeconds (a single-duration timer, not a range).
    private static AiTimer? ReadTimer(JsonElement timer)
    {
        if (timer.ValueKind != JsonValueKind.Object)
            return null;

        var label = Str(timer, "label");
        var min = Int(timer, "minSeconds", 1, MaxTimerSeconds);
        if (label is null || min is null)
            return null;

        int max;
        if (timer.TryGetProperty("maxSeconds", out _))
        {
            var maxValue = Int(timer, "maxSeconds", 1, MaxTimerSeconds);
            if (maxValue is null)
                return null;
            max = maxValue.Value;
        }
        else
        {
            max = min.Value;
        }

        var (lo, hi) = min.Value <= max ? (min.Value, max) : (max, min.Value);
        return new AiTimer(label, lo, hi);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;

    private static int? Int(JsonElement e, string name, int min, int max) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= min && n <= max
            ? n
            : null;

    private static string[] StringArray(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString()!.Trim()).Where(x => x.Length > 0).ToArray()
            : [];
}
