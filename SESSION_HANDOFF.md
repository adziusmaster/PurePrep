# PurePrep — Session Handoff / Where We Are

Last updated: end of session on version **1.2.11 (versionCode 20)**.
Code changes are committed by the maintainer; the signed AAB for 1.2.11 has been
built and the server is deployed. `store/release-notes/1.2.11.txt` is a new
(untracked) file pending commit.

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
run longest; trim them). Reuse the previous version's file as the template.
