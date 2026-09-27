# PurePrep 1.5 — Log meals to Google Health (Health Connect) — Design (draft)

- **Date:** 2026-09-25
- **Status:** Draft for 1.5 — idea approved by the maintainer; to be refined (brainstorm → approve → plan) when 1.5 starts
- **Target:** Android

## 1. Intent

After cooking, one tap logs the dish just eaten to the user's diet in **Google Health** (the app formerly called Fitbit, package `com.fitbit.FitbitMobile`). PurePrep therefore needs per-serving nutrition (calories and macros) for each recipe.

**Success looks like:** finish cooking in Focus Mode → "Log 1 serving" → the meal counts towards "Calorie intake" in Google Health → Food and drink, with macros, in the right meal slot.

## 2. Findings (maintainer's Pixel 10, 2026-09-25)

- Google Health is installed and has Health Connect permissions **READ_NUTRITION** and **WRITE_NUTRITION** granted (also READ_HYDRATION, READ_TOTAL_CALORIES_BURNED).
- Google Health → Health → **Food and drink** shows daily "Calorie intake" against a personalised target (2,400–2,500 cal), plus "Active energy burned" and a "+" for manual logging.
- Health Connect is available system-wide (`com.google.android.healthconnect.controller`, Android 14+ built-in).
- **Unverified:** whether Google Health shows a Health Connect `NutritionRecord` from another app as a named food entry with macros, or only counts its calories in the total. Verify with a throwaway record before committing to UI copy such as "shows up in Google Health".

## 3. Architecture (proposed)

**Integration: Health Connect, on device.** Health Connect is the replacement for the deprecated Google Fit APIs. There is no server and no Google sign-in. PurePrep requests only `android.permission.health.WRITE_NUTRITION` (plus READ_NUTRITION only if we later show "logged today").

- Record type: `NutritionRecord` with `name` (recipe title), `mealType` (breakfast/lunch/dinner/snack), start/end time, `energy`, `protein`, `totalCarbohydrate`, `sugar`, `dietaryFiber`, `totalFat`, `saturatedFat`, `sodium`. Everything is scaled by the servings eaten.
- Access: a Core port `IHealthLog` (Application layer) with an Android implementation using the Health Connect client (`androidx.health.connect:connect-client` via a .NET Android binding — **spike needed:** is a maintained binding available for .NET 10 MAUI, or do we write a thin Java/Kotlin AAR binding?). Unsupported platforms or a missing Health Connect → the feature is hidden.

**Nutrition source: LLM estimate per serving at import (recommended).**
- The Gemini extraction schema gains `nutritionPerServing { kcal, proteinG, carbsG, sugarG, fiberG, fatG, saturatedFatG, sodiumMg, estimated: true }`, produced in the same parse call, so it is included in the import credit.
- Stored on `ParsedRecipe.Nutrition` (a new value object; SQLite column + backup v3). It scales with `ChosenServings` / servings eaten via a pure Core function.
- Recipes saved before 1.5, and manual recipes: an "Estimate nutrition" action (server endpoint, **1 credit**, same spend/refund pattern as translate).
- Alternative, deferred: ingredient matching against USDA FoodData Central / Open Food Facts. More accurate, much more work; revisit if users need precision.
- Values are always labelled **estimates** and are editable by the user in the recipe editor.

## 4. UX (proposed)

- **Recipe page:** a compact "Nutrition per serving (estimate)" card: kcal big, then P / C / F with sugar and fibre small. Tap → full list and edit.
- **End of Focus Mode:** the "Done! Mark as cooked?" bottom sheet gains **"Log to Google Health"** with a servings picker (½ / 1 / 1½ / 2 / custom) and a meal type pre-selected from the time of day. It is one sheet and one tap for the common case.
- **⋯ menu on the recipe:** "Log a meal" (e.g. leftovers the next day), same sheet.
- **First use:** explain what is written, then run the Health Connect permission request (WRITE_NUTRITION only). Denied → offer again later from Settings; never nag.
- **Settings → Health:** connection status, "Open Health Connect", a default servings value.

## 5. Store & privacy

- Google Play requires the **Health Connect declaration** (Play Console → App content → Health apps) listing each data type and its use, and a privacy policy that covers health data. That takes a few days of review: plan it before the release date.
- The privacy notice (`/privacy`) is updated: nutrition estimates are computed from the recipe; meal logs are written only to Health Connect on the device; PurePrep's server never receives health data.

### 5.1 Launch & visibility
- Research Google's current partner / showcase options for Health Connect and Google Health (e.g. listings of "apps that work with Health Connect", Google Health's compatible-apps list). The former "Works with Google Fit" badge programme ended with Google Fit, so the current process is unknown — verify before launch and apply once the integration is live and the Play declaration is approved.
- Mention "Works with Google Health (Health Connect)" in the Play listing and on lechdigital.nl regardless of any programme; check Google's brand guidelines for how the name may be used.

## 6. Open questions (resolve when 1.5 starts)

1. Is a .NET Health Connect binding usable, or do we need our own AAR binding? (Spike.)
2. Does Google Health display third-party `NutritionRecord`s as named entries with macros? (Verify on the maintainer's phone.)
3. Which nutrients to show by default (kcal + P/C/F, or also sugar, fibre, salt)?
4. Is the "Estimate nutrition" price for existing recipes 1 credit, or free for the first N?
5. Should hydration (e.g. soups) or allergens be in scope? (Probably not; YAGNI.)

## 7. Testing (outline)

- Core: nutrition scaling by servings eaten; `AiRecipeReader` v3 tolerant parsing (missing/negative/implausible values → dropped); meal type from time of day; backup v3 round-trip.
- Server: schema/prompt contract; the estimate endpoint's credit spend/refund.
- Device: permission flow, write a record, confirm it in Google Health → Food and drink, deny path.
