# PurePrep — Session Handoff / Where We Are

Last updated: end of session on version **1.2.3 (versionCode 12)**.
Working tree is clean; everything below is committed. Nothing new is pending commit.

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

- **Real Google Play Billing** (`Xamarin.Android.Google.BillingClient` 8.3.0.2,
  namespace `Android.BillingClient.Api`). This cleared the Play "AIDL / update to
  6.0.1" error — the AAB now bundles `base/root/billing.properties` version 8.3.0.
  Real impl: `src/PurePrep/Platforms/Android/PlayBillingService.cs`; non-Android
  fallback: `src/PurePrep/Services/UnsupportedBillingService.cs`.
- **Offline translation / language packs** via `Xamarin.Google.MLKit.Translate`
  117.0.3.8 (Android-only). Free, on-device. English is the bundled pivot.
  Impl: `Platforms/Android/MlKitTranslationService.cs`; Core interface
  `Application/ITranslationService.cs`; source detection is offline
  (`Domain/LanguageHeuristics.cs`). minSdk raised to **23** for ML Kit.
- **Better parse-time translation for NEW imports**: `GeminiClient.ExtractAsync`
  takes a target language and asks the LLM to output in that language (higher
  quality than an ML Kit round-trip). Recipe language is a global setting
  (`recipe_language` pref; "" = follow app UI language).
- **7-language UI** (en/de/fr/es/it/pl/nl) via resx + `TranslateExtension`;
  runtime language switch in Settings.
- **Redeem codes**: server `PromoCode`/`PromoRedemption` + `/api/promo/redeem`
  (public) and `/api/admin/promo*` (admin, `X-Admin-Secret`). `deploy/promo.sh`
  is the admin helper (needs `ADMIN_SECRET`). **The maintainer must set
  `ADMIN_SECRET` in prod `.env` and redeploy** for admin endpoints to work.
- Server still uses **DevPlayValidator** (accepts any non-empty token). Real
  purchases grant credits (fine for testing) but Google-side receipt validation
  (androidpublisher API + service account) is **not yet wired** — harden before
  public launch.
- Recurring web-preview trap: a stale dotnet process can hold port 5284 and
  serve old code. `lsof -ti tcp:5284` then kill the PID before relaunch.

---

## 5. Recent version history

- 1.2.1 / vc10 — real Google Play Billing (fixed AIDL error), pack picker,
  "Why Smart Credits?" explainer, prices halved.
- 1.2.2 / vc11 — recipe-language hint on start/import so users know they can
  auto-translate imports to their chosen language (the language must be selected
  to translate on import).
- **1.2.3 / vc12 (current)** — fixed the in-app purchase failure
  *"could not complete the purchase: Product 'credits_10' is not…"*. Root fix in
  `PlayBillingService.cs`: use the **one-time-purchase offer token** when
  launching the billing flow (required by the new one-time-product model), read
  both `ProductDetailsList` and the synthesized `ProductDetails` list, and
  surface actionable diagnostics (billing response code + per-product
  `UnfetchedProduct` status) instead of a generic "not available". Also clarified
  purchase alert titles.

---

## 6. Open issues / things to verify next session

1. **In-app purchase on a real device** — the 1.2.3 offer-token fix is untestable
   on this machine. Verify on-device (Play license-tester account, products
   `credits_10/20/50/150` created & **active**) that:
   - the pack picker buys successfully (no "product is not available"),
   - credits are granted server-side and the balance chip refreshes,
   - `ITEM_ALREADY_OWNED` reconciles (consume path).
   Note the user reported products weren't immediately available and a "redeem
   code" prompt appeared during buy — confirm this is resolved now that the buy
   flow launches correctly and products are active in Console.
2. **Purchase entry point in Settings** — buying Smart Credits should be
   available in Settings (a popup above "Redeem a code"), in addition to the
   paywall. Confirm this is wired and opens correctly.
3. **Paywall/popup UX** — the purchase sheet should close on tap-outside and via
   a close button; the system **Back** button should go back one screen, not
   close the app. Verify these behave correctly.
4. **Recipe language default** — ensure the import language is pre-selected to
   the app language by default so imports auto-translate without the user having
   to manually pick it first.
5. **Native debug symbols warning** (Play Console) — benign/non-blocking; .NET
   doesn't auto-emit the Play-format symbol zip. Managed C# crashes already have
   full stack traces (`AndroidManagedSymbols=true`). Safe to ignore.
6. **Server hardening** — wire real Google receipt validation before public
   launch; set prod `ADMIN_SECRET`.
7. **Deferred**: recipe-language pre-selection edge cases, on-device ML Kit
   translate quality/model-download/delete testing.

---

## 7. Play Console release notes (last release)

Release name pattern: `1.2.3 (12) — <summary>`. Notes have been drafted in all 7
languages for prior releases covering: AI smart import with credits, pack picker
+ "why pay" copy, real Play Billing, redeem codes, recipe-language selection,
offline language packs, search/edit/manual-add, serving scaler, cook timers,
Light/Dark/System themes, 7-language UI. Reuse/trim these per release.
