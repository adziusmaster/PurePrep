# PurePrep — Session Handoff / Where We Are

Last updated: end of session on version **1.4.0 (versionCode 25)** — release prep
(Task 20 of the 1.4 SDD cycle). Core + Server tests and the Web/MAUI builds are
green; no device pass yet (a separate final device pass follows) and nothing has
been deployed for 1.4 — see §8 below for what shipped in 1.4 and what's still
outstanding.

> House rule: **never commit or push automatically** — the maintainer does that.

---

## 1. What PurePrep is

Anti-bloatware Android recipe app (.NET 9/10 MAUI, Android-only, C#, MVVM, Clean
Architecture). The user pastes/shares a recipe URL, an AI backend extracts only
Title / Ingredients / Steps, and it is saved locally (EF Core + SQLite). A
distraction-free **Focus Mode** (large text, keep-screen-on, step progression)
is used while cooking. Focus Mode is **free** (an earlier premium idea was
scrapped).

### Monetization — final model
- **Smart Credits**, not a subscription. 1 credit ≈ 1 AI recipe import.
- New devices get **10 free credits** (seeded server-side on first contact).
- Saving, editing, scaling, cooking, translating are all **free**. Only the
  AI import/parse consumes a credit (because the LLM call costs real money).
- Credit packs (Google Play in-app products, consumable one-time products):
  - `credits_10` — €0.99
  - `credits_20` — €1.79
  - `credits_50` — €3.49
  - `credits_150` — €7.49
  - (Prices were halved to favour cheap, high-volume purchases. The `DisplayPrice`
    strings in code are placeholders/labels only — Google shows the **real**
    Play price at checkout. Keep Play Console prices in sync with these labels.)
- Testers can also get credits via **redeem codes** (5-char, one redeem per
  device, admin-created).

---

## 2. Repository layout

- `src/PurePrep/` — the MAUI Android app (Domain, Application, Infrastructure,
  Presentation VMs, XAML pages, Platforms/Android).
- `src/PurePrep.Core/` — shared Clean-Architecture library (net10.0) referenced
  by both MAUI and Web. Namespaces stay `PurePrep.*`.
- `src/PurePrep.Web/` — browser preview app. Real API (parse/list/quota/credits)
  backed by the real parser + SQLite; `wwwroot/index.html` calls it via fetch.
- `src/PurePrep.Server/` — backend: AI parse proxy (Gemini), credit store,
  promo/redeem codes, admin endpoints. Deployed to Hetzner via docker-compose.
- `store/`, `product-icons/` — Play Store assets and per-pack product icons.
- `INIT.md` — original spec-driven deliverables (domain model, parsing design,
  VM state).
- `deploy/` — `promo.sh` admin helper, prod compose files.

No `.sln` file. Building the MAUI app needs the maui-android workload from the
maintainer's user-local SDK at `~/dotnet-maui` (the global dotnet SDK lacks it).
See **[BUILD.md](BUILD.md)** for the full verified local build + signed-AAB recipe.
The Web and Server projects build/run with a plain `dotnet build`.

---

## 3. How to build the release AAB (important gotchas)

The maintainer's user-local SDK is at `~/dotnet-maui`. Build command:

```
export DOTNET_ROOT=$HOME/dotnet-maui PATH=$HOME/dotnet-maui:$PATH JAVA_HOME=<jdk17>
export PUREPREP_KEYSTORE_PASS=<pass from ~/keystores/pureprep-upload.pass.txt>
dotnet build src/PurePrep/PurePrep.csproj -c Release -f net10.0-android --no-restore \
  -p:UseDefaultPublishRuntimeIdentifier=false \
  -p:AndroidPackageFormat=aab \
  -p:AndroidSdkDirectory=$HOME/android-sdk -p:JavaSdkDirectory=$JAVA_HOME
```

Gotchas learned the hard way:
- **XA0035 `osx-arm64`**: Release sets `PublishTrimmed=true` → SDK appends the
  host RID. Fix with `-p:UseDefaultPublishRuntimeIdentifier=false`.
- **NuGet restore** must use `-s https://api.nuget.org/v3/index.json` (the
  codeartifact-proxy feed is unreachable and aborts restore).
- The csproj `TargetFrameworks` is multi-target; some builds temporarily
  single-target it, then **revert** so the committed csproj diff stays minimal.
- Signing keystore lives **outside the repo**: `~/keystores/pureprep-upload.jks`,
  alias `pureprep`, cert CN=adziusmaster. No secrets are committed. csproj signs
  only when `PUREPREP_KEYSTORE_PASS` is set.
- ApplicationId: `com.adziusmaster.pureprep`.
- Output AAB: `src/PurePrep/bin/Release/net10.0-android/com.adziusmaster.pureprep-Signed.aab`.

---

## 4. Key technical facts

- **Translation is AI-based (ML Kit + on-device language packs were REMOVED).**
  Recipes are translated by the server via **`POST /api/ai/translate`** (Gemini),
  which returns Title + Ingredients + Steps in the target language. Each language
  is cached on the recipe (`ParsedRecipe.Translations`, keyed by language code) so
  a language is only paid for once; the pristine original is always kept and
  switching back is free. `ParsedRecipe.Displayed()` swaps in the active-language
  translation. Client rework lives in the recipe VMs; Core interface is the
  translate call in `Ai/GeminiClient.cs` (`TranslateAsync`, `TranslateSystemPrompt`).
- **Import fetch — honest bot with transparent-browser fallback.**
  `src/PurePrep.Core/Ai/GuardedPageFetcher.cs` is registered as a **typed
  HttpClient** (`AddHttpClient<IPageFetcher, GuardedPageFetcher>` in
  `Program.cs`). It first fetches as an honest bot; if a host bot-walls it, it
  retries mimicking a browser but sends transparent `From` / purpose headers
  explaining the fetch is user-initiated (not scraping) with contact info.
  `IFetchHostMemory` remembers which hosts need the browser path.
  **CRITICAL:** the DI constructor carries `[ActivatorUtilitiesConstructor]` —
  the typed-client factory needs exactly one applicable constructor, and adding
  the browser-mimic ctor previously caused a prod-wide 500 outage on every
  import. There is a regression test in `GuardedPageFetcherTests.cs`.
- **Recipe scaling** (`src/PurePrep.Core/Domain/RecipeScaling.cs`): `Scale` scales
  a **leading** quantity if present (preserving counts like "3 eggs" and "3 × 400 g"
  multipliers), otherwise `ScaleText` scales every quantity attached to a curated
  multilingual cooking-unit vocabulary anywhere in the line (fixes trailing
  amounts like "marchewki 500 g"). `ScaleRecipe` now also scales amounts in the
  **preparation steps** (mass/volume/spoons), leaving times, temps, tin sizes
  (cm) and bare counts untouched. `RecipeDetailViewModel` re-scales steps live
  when the serving factor changes.
- **Import errors never surface as raw 500s.** `ParseEndpoint`/`TranslateEndpoint`
  map every failure to a stable `ImportErrorCode`; the client shows friendly,
  localized messages.
- **Real Google Play Billing** (`Xamarin.Android.Google.BillingClient` 8.3.0.2,
  namespace `Android.BillingClient.Api`). Real impl:
  `src/PurePrep/Platforms/Android/PlayBillingService.cs`; non-Android fallback:
  `src/PurePrep/Services/UnsupportedBillingService.cs`.
- **7-language UI** (en/de/fr/es/it/pl/nl) via resx + `TranslateExtension`;
  runtime language switch in Settings. Recipe language is a global setting
  (`recipe_language` pref; "" = follow app UI language) via
  `Services/RecipeLanguageSettings.cs`, which always resolves a non-null target
  so imports always request a translation.
- **Redeem codes**: server `PromoCode`/`PromoRedemption` + `/api/promo/redeem`
  (public) and `/api/admin/promo*` (admin, `X-Admin-Secret`). `deploy/promo.sh`
  is the admin helper (needs `ADMIN_SECRET`).
- Server still uses **DevPlayValidator** (accepts any non-empty token); Google-side
  receipt validation (androidpublisher API + service account) is **not yet wired**
  — harden before public launch.

### Prod / deploy quick reference
- SSH: `ssh coldstart-prod`; app dir `/opt/pureprep`.
- Logs: `cd /opt/pureprep && docker compose --env-file .env -f deploy/docker-compose.prod.yml logs --tail 80 pureprep`.
- Deploy: `./deploy/deploy-prod.sh --confirm` (rsyncs `PurePrep.Core` + `PurePrep.Server`,
  runs a `dotnet test` interlock, docker rebuild, health-checks
  `https://api.pureprep.lechdigital.nl/health`).
- API routes are prefixed `/api/ai/parse` and `/api/ai/translate`.

---

## 5. Recent version history

- 1.2.1–1.2.3 — real Google Play Billing (fixed AIDL error), pack picker,
  "Why Smart Credits?" explainer, prices halved, one-time-purchase offer-token fix.
- 1.2.8 — **AI translation replaces ML Kit/language packs** (7 languages, original
  always kept, per-language cache = pay once); more reliable imports; friendly
  structured import errors (`ImportErrorCode`).
- 1.2.9–1.2.10 — startup-crash fix, translation DB migration fixes, local backup
  + duplicate-import handling.
- **1.2.11 / vc20 (current)** — three fixes, all shipped this session:
  1. **Import outage fix.** `GuardedPageFetcher` had two public constructors, which
     broke the typed-HttpClient factory and returned 500 on **every** import.
     Fixed with `[ActivatorUtilitiesConstructor]` + regression test. Deployed and
     verified live (Polish + Dutch recipes import again).
  2. **Accurate scaling.** Trailing ingredient amounts ("marchewki 500 g") and
     amounts inside preparation steps now scale with the serving factor; times,
     temps, tin sizes and bare counts are left alone.
  3. **Titles translated.** Both the extract and translate Gemini prompts now
     force the recipe **title** into the target language (it previously often
     stayed in the source language). Server deployed.
  Signed AAB built for 1.2.11; release notes drafted in all 7 languages
  (`store/release-notes/1.2.11.txt`). Note: the title fix only affects **new**
  imports/translations — existing saved recipes keep their old title unless
  re-imported.

---

## 6. Open issues / things to verify next session

1. **In-app purchase on a real device** — verify on-device (Play license-tester,
   products `credits_10/20/50/150` active) that the pack picker buys, credits are
   granted server-side, the balance chip refreshes, and `ITEM_ALREADY_OWNED`
   reconciles (consume path).
2. **Server hardening** — wire real Google receipt validation before public
   launch; set prod `ADMIN_SECRET`.
3. **Remaining pending todos** (see session DB): `backup-ux` (export/restore UX),
   `copy-link` (tap-to-copy source link), `import-dedupe` (non-deterministic
   imports / duplicates), `landscape` (list issues on rotate).
4. **Origin credit cap by IP** — repeated prod `/api/ai/parse` calls from one
   machine eventually return `insufficient_credits`; use a real grant / device to
   keep testing.
5. **Native debug symbols warning** (Play Console) — benign/non-blocking.

---

## 7. Play Console release notes

Localized notes live in `store/release-notes/<version>.txt`, one `<xx-YY>` block
per locale (en-GB, en-US, de-DE, es-ES, fr-FR, it-IT, nl-NL, pl-PL). **Google Play
caps each language at 500 characters** — keep every block under it (fr/nl tend to
run longest; trim them). Reuse the previous version's file as the template. There
is no separate per-language notes file convention (e.g. `-pl.txt`) in this repo —
every version's notes live in the one bundled file.

---

## 8. PurePrep 1.4 — what shipped, key contracts, device-testing lessons

1.4 is a large release: share/clipboard import, AI parser v2 (servings, times,
named/range timers, photos), scale-by-people, Focus Mode rework, a new editor,
notes/favourites/status/sorting, in-app recipe search, unified bottom-sheet
dialogs, and a bundled sample recipe. Server must be deployed **before** the app
reaches users (older servers just omit the v2 fields; the contract is additive).

- **Parser v2 contract** — `src/PurePrep.Server/Endpoints/Contracts.cs`:
  `RecipeResponse` keeps its 1.3.x fields (`Steps` as `string[]`, etc.) and adds
  1.4 fields as `init`-only additions: `Servings`, `ServingsNoun`,
  `ServingsEstimated`, `PrepMinutes`, `CookMinutes`, `ImageUrl`, `ImageTicket`,
  `StepDetails` (`StepDto(Text, IngredientRefs, Timers)`), `TimerLabels`. The
  Gemini-side shape lives in `src/PurePrep.Core/Ai/GeminiOptions.cs`
  (`AiRecipe`/`AiRecipeMeta`/`AiStep`/`AiTimer`).
- **AI image endpoint + ticket** — `POST /api/ai/image` → `ImageEndpoint.Generate`
  (`Program.cs`). `src/PurePrep.Server/Services/ImageTicketStore.cs`
  (`internal sealed class ImageTicketStore(TimeProvider clock) : IImageTicketStore`)
  issues a single-use, device-bound, 10-minute ticket (`Lifetime =
  TimeSpan.FromMinutes(10)`; `TryRedeem` checks device + expiry, then
  `TryRemove`s it). Image generation is covered by the same import credit — no
  extra spend. The model is configurable: `Gemini__ImageModel` env var →
  `GeminiOptions.ImageModel`, default `"gemini-2.5-flash-image"`, read in
  `GeminiClient.cs` (`v1beta/models/{ImageModel}:generateContent`). Only set it
  in prod `.env` to override the default.
- **Translation retry (server-side)** — `TranslateEndpoint.Translate` retries
  the Gemini call once, in-process, if `translated.Steps.Length` doesn't match
  the source step count (so a flaky model response costs the device at most the
  one credit already charged); a still-mismatched retry throws.
- **Bottom sheets** — `src/PurePrep/Controls/BottomSheet.xaml(.cs)` is the one
  shared sheet control; `OptionsSheet` and `ImportSheet` build on it. Every
  dialog and custom sheet (Import, cooking options, timer edit, credits) now
  renders through it (spec §16.1).
- **SheetKeyboard** — `src/PurePrep/Platforms/Android/SheetKeyboard.cs`
  (`internal static class SheetKeyboard`). A bottom sheet is docked to the
  bottom, so the app's default `AdjustPan` keyboard mode (used elsewhere so a
  focused Home search box doesn't force a full relayout) would hide sheet
  buttons behind the keyboard. `SetResize(bool)` toggles
  `Window.SetSoftInputMode` between `AdjustResize`/`AdjustPan`; `MainActivity`'s
  insets listener then pads content by keyboard height while resize is active.
- **Edge-to-edge + insets (one place)** — `MainActivity.OnCreate` calls AndroidX
  `EdgeToEdge.Enable(this)` before `base.OnCreate` (transparent bars on every API
  level; Android 15+ enforces it at targetSdk 35). The only inset handling is
  `MainActivity`'s `SystemBarsInsetsListener` on the content view (system bars +
  cutout, plus IME while `SheetKeyboard.ResizeActive`), so pages, sheets, TimersBar,
  the Focus Back/Next bar and the editor Save button all sit above the nav bar.
  Bar-icon colour comes from the app theme via `ThemeService.ApplyNativeBars()`
  (`WindowInsetsControllerCompat.AppearanceLight*` + decor background), re-applied
  on theme change and in `MainActivity.OnResume`. Our code never calls
  `Window.SetStatusBarColor`/`SetNavigationBarColor`/`SetDecorFitsSystemWindows`;
  Play's remaining "deprecated edge-to-edge APIs" note comes from library bytecode
  (Material `EdgeToEdgeUtils`/`BottomSheetDialog`, still present in 1.14.0.6).
- **Focus Mode auto-fit + "You'll need"** — the "always fits the step" behaviour
  is `src/PurePrep/Controls/StepInstructionView.xaml.cs`, used by `FocusPage.xaml`
  as `<controls:StepInstructionView Text="{Binding Instruction}" />`. `Refit()`
  steps the label's `FontSize` down from `MaxFontSize` (34) to `MinFontSize` (22)
  in `FontStep` (2) increments, calling `TextLabel.Measure(width, ...)` after each
  step until the measured height fits the available space; if even the minimum
  size doesn't fit, it falls back to a scrollable view with a visible "More"
  overflow cue that hides once the last line scrolls into view. "You'll need" is
  a literal section (`FocusPage.xaml`, resx key `YoullNeed`) built from each
  step's `IngredientRefs` resolved against the recipe's ingredient list.
- **Timer slot keys** — `src/PurePrep/Services/CookTimerService.cs`
  (`sealed class CookTimerService : IDisposable`) persists running timers under
  Preferences keys `cook_timers_v2` (state) and `cook_timers_next_id`; each
  running timer gets a monotonically increasing int id (the brief's "slot" is
  not a literal key in code, just this id).
- **Editor photo + resizer** — `RecipeEditorViewModel.SetPhoto(byte[] jpeg)` /
  `RemovePhoto()` (exposes `Photo`, `HasPhoto`, `PendingPhoto`, `PhotoRemoved`).
  `ManualAddPage.xaml.cs` calls `RecipePhotoResizer.ToJpegAsync(stream, ct)`
  (`src/PurePrep/Services/RecipePhotoResizer.cs`) before handing bytes to
  `SetPhoto`.
- **In-app search + WebRecipeDetector** — `SearchBrowserPage.xaml(.cs)` hosts
  the in-app browser (spec §16.2). `src/PurePrep.Core/Ai/WebRecipeDetector.cs`
  (`public static class WebRecipeDetector`) is the pure, unit-tested Core
  parser: `Detect(jsonLdBlocks, pageUrl)` and an overload that also takes
  microdata name/image and falls back to the JSON-LD path via
  `JsonLdRecipeWalker.FindRecipe`. No server call, no credit, for browsing.
- **Sample recipe + bundled photo credit** — `src/PurePrep.Core/Domain/SampleRecipe.cs`
  (`SampleRecipe.Create()`) is the full 1.4 Pierogi sample (spec §16.3); its XML
  doc comment credits the source ("based on Ania Gotuje's recipe — no egg in the
  dough"). `RecipeLibraryViewModel.SeedSampleIfFirstRunAsync()` (pref
  `sample_recipe_seeded`) seeds it once; `TrySeedSamplePhotoAsync` loads the
  bundled `Resources/Raw/sample-pierogi.jpg` via
  `FileSystem.OpenAppPackageFileAsync` and saves it through the image store,
  best-effort (falls back to the placeholder silently on failure).
- **Backup with photos** — `RecipeBackup.Export(recipes, images)` /
  `ImportWithImages(json)` (Task 7) now round-trip each recipe's photo as
  base64; Settings' export reads each recipe's image via
  `IRecipeImageStore.ReadAsync` and restore writes it back via
  `SaveBytesAsync` + `UpdateAsync(recipe.WithImage(path))` (Task 20).

### Device-testing lessons (not previously written down anywhere in this repo)

- **Never debug-install over a Play-distributed build.** Before any `-t:Install`,
  run `adb shell dumpsys package com.adziusmaster.pureprep | grep
  installerPackageName` — proceed only if it's null (debug build already
  present); if it shows `com.android.vending`, stop and do not uninstall.
- **One device agent at a time.** The Pixel is a single shared piece of
  hardware — don't run concurrent adb sessions/installs against it.
- If a debug install misbehaves (stale state, permission prompts stuck), prefer
  `adb shell pm clear com.adziusmaster.pureprep` then re-run the
  `-t:Install` build over guessing at in-app fixes.

### Known gaps going into the final device pass

- This task did **not** touch the device (per its brief) — the full click-through
  (Home, share, import sheet, recipe detail, Focus Mode, editor, Settings
  export/restore round-trip, dark theme) from the 1.4 spec's testing section
  still needs to happen before a release build.
- The server has 1.4 endpoints (`/api/ai/image`, v2 parse/translate fields) but
  has **not** been deployed; deploy before the 1.4 app reaches real users
  (`./deploy/deploy-prod.sh --confirm`, maintainer's call).
- No signed AAB has been built for 1.4.0 yet.
