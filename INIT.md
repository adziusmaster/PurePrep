# PurePrep — Project INIT / Design Acknowledgment

> Anti-bloatware culinary app. Paste a URL → extract only Title, Ingredients, Steps →
> save locally → cook in a distraction-free **Focus Mode**.
> Stack: .NET 9/10 MAUI (Android-first), C# 12+, MVVM, Clean Architecture,
> HtmlAgilityPack, EF Core (SQLite), Microsoft DI, Google Play Billing (freemium).

This document acknowledges the spec-driven prompt and captures the agreed design before
further UI/parsing work. It reflects what already exists in the repository and the direction
for the browser-preview wiring.

---

## 1. Domain Model

### 1.1 Parsed Recipe
The recipe aggregate is intentionally minimal — only the essentials survive the parse.

```csharp
public sealed class ParsedRecipe
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Title { get; init; }
    public string? SourceUrl { get; init; }
    public IReadOnlyList<string> Ingredients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<RecipeStep> Steps { get; init; } = Array.Empty<RecipeStep>();
    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RecipeStep
{
    public int Order { get; init; }
    public required string Instruction { get; init; }
}
```

- Immutable (`init`-only) — a parsed recipe is a value snapshot, never mutated after import.
- `RecipeStep.Order` drives Focus Mode progression.

### 1.2 User Quota (Freemium State)
Encapsulates the freemium rules so no UI layer can bypass them.

```csharp
public sealed class UserQuota
{
    public const int FreeRecipeLimit = 10;
    public int SavedRecipeCount { get; private set; }
    public bool IsPremium { get; private set; }
    public int RemainingFreeRecipes => IsPremium ? int.MaxValue : Math.Max(0, FreeRecipeLimit - SavedRecipeCount);
    public bool CanSaveRecipe   => IsPremium || SavedRecipeCount < FreeRecipeLimit;
    public bool CanUseFocusMode => true;         // Focus Mode is free for everyone
    public void RecordRecipeSaved();             // throws if quota exceeded
    public void SetPremiumStatus(bool isPremium);
}
```

- **Free tier:** up to `10` saved recipes.
- **Premium tier:** unlimited recipe storage (entitlement from Google Play Billing).
- **Focus Mode is free on both tiers** — the only paid benefit is unlimited storage.
- The quota is the single source of truth; ViewModels only *read* derived flags.

---

## 2. Technical Design — HTML Parsing (`HtmlAgilityPack`)

Parsing runs fully **off the main thread** (`async` fetch + parse) so the UI never blocks.
Contract: `IRecipeParser.ParseAsync(Uri, CancellationToken) → ParsedRecipe`.

### Strategy: layered fallback (graceful degradation)
1. **Primary — JSON-LD (`application/ld+json`).**
   Iterate every `<script type="application/ld+json">`, `JsonDocument.Parse`, and
   recursively locate a node whose `@type` is (or contains) `"Recipe"`, including inside
   `@graph` arrays. Extract `name`, `recipeIngredient`, `recipeInstructions`.
   `recipeInstructions` is normalized across its many shapes: plain strings,
   `HowToStep` objects (`text`), and `HowToSection` (`itemListElement`).
   This is the fast path and covers the majority of major recipe sites.

2. **Fallback — semantic HTML / microdata.**
   If JSON-LD is missing or malformed, fall back to:
   - Title: `[itemprop=name]` → `<h1>` → `og:title` meta.
   - Ingredients: `[itemprop=recipeIngredient]` → class-name heuristic (`*ingredient*`,
     case-insensitive).
   - Steps: `[itemprop=recipeInstructions]` (and nested `[itemprop=text]`), de-duplicated
     and ordered.

3. **Resilience rules.**
   - Malformed JSON in one block never aborts the parse — the loop continues and the
     semantic-HTML fallback still runs.
   - All text is cleaned via `HtmlEntity.DeEntitize` + `WebUtility.HtmlDecode`, with
     newlines collapsed and trimmed.
   - If *neither* strategy yields ingredients or steps, a clear
     `InvalidOperationException("No recipe data was found on this page.")` is thrown for
     the UI to surface gracefully (no crash, no silent empty recipe).

This design isolates network + parsing in Infrastructure; Domain stays framework-free and
the same parser is reused by the MAUI app **and** the web preview (see §4).

---

## 3. ViewModel State Definitions (MVVM)

### 3.1 `RecipeLibraryViewModel`
| Member | Type | Purpose |
|---|---|---|
| `Recipes` | `ObservableCollection<ParsedRecipe>` | Instantly-loading local library |
| `Quota` | `UserQuota` | Freemium state |
| `UrlInput` | `string` | Bound to the paste-URL entry |
| `IsImporting` | `bool` | Spinner / disable state during async parse |
| `IsUpgradePromptVisible` | `bool` | Elegant, non-aggressive upgrade banner |
| `ErrorMessage` | `string?` | Friendly parse/validation errors |
| `QuotaSummary` | `string` (derived) | e.g. "7 free saves remaining" / "Premium access" |
| `ImportCommand` | `ICommand` | Validate URL → parse → save → record quota |
| `UpgradeCommand` | `ICommand` | Reveal upgrade prompt / start billing |
| `OpenFocusCommand` | `ICommand<ParsedRecipe>` | Enter Focus Mode (gated by `CanUseFocusMode`) |
| `FocusRequested` | `event` | Navigation signal to the Focus page |

Import flow: quota check → URL validation → `IsImporting=true` → `ParseAsync` → persist →
insert at top → `RecordRecipeSaved()` → refresh `QuotaSummary`; errors captured into
`ErrorMessage`; `finally` clears `IsImporting`.

### 3.2 `FocusModeViewModel`
| Member | Type | Purpose |
|---|---|---|
| `Recipe` | `ParsedRecipe` | Recipe being cooked |
| `Steps` | `IReadOnlyList<RecipeStep>` | Ordered steps |
| `CurrentStepIndex` | `int` | Highlighted step |
| `CurrentStep` | `RecipeStep?` (derived) | Large-typography instruction |
| `StepLabel` | `string` (derived) | "STEP 2 OF 5" |
| `IsFirstStep` / `IsLastStep` | `bool` (derived) | Button enable state |
| `PreviousCommand` / `NextCommand` | `ICommand` | Step-by-step progression |

The Focus **page** enforces `DeviceDisplay.Current.KeepScreenOn = true` on appear and
resets it on disappear, so the screen never sleeps while cooking.

### 3.3 Paywall / Billing integration
Surfaced through `RecipeLibraryViewModel` (`Quota`, `IsUpgradePromptVisible`,
`UpgradeCommand`) rather than a separate blocking page:
- Quota badge always visible (remaining free saves).
- On the 11th save attempt (free limit reached) → reveal the inline upgrade banner
  (no pop-ups). `UpgradeCommand` will invoke Google Play Billing; a successful purchase
  calls `Quota.SetPremiumStatus(true)` to unlock unlimited storage. Focus Mode is never gated.

---

## 4. Browser Preview Wiring (added this session)

To validate real behaviour before installing on a phone, the Clean-Architecture layers
(Domain / Application / Infrastructure) are extracted into a shared **`PurePrep.Core`**
library referenced by both the MAUI app and **`PurePrep.Web`**. The web project hosts the
*real* `RecipeParser` + EF Core SQLite repository behind a small JSON API, and the existing
minimalist front-end calls it via `fetch`. See `src/PurePrep.Web` and the README.

---

## Sign-off
Design is implemented as above. Awaiting approval before extending parsing heuristics and
building out the full XAML/billing UI.
