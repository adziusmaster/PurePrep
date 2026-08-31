# PurePrep — Product Overview

> **Status:** Closed testing (Google Play). Targeting public production release in **~2 weeks**
> (early-to-mid September 2026).
> **Current build:** 1.2.6 (version code 15) · Android · `com.adziusmaster.pureprep`

---

## What PurePrep is

PurePrep is an **anti-bloatware culinary app**. You paste a recipe URL and PurePrep strips away
the life stories, ads, pop-ups and comment sections, keeping only what you actually cook from:

- **Title**
- **Ingredients**
- **Steps**

Recipes are saved **locally on the device** and can be cooked in a distraction-free **Focus Mode**.
It's designed Android-first, with a clean, fast, single-purpose experience.

## Who it's for

Home cooks who are tired of scrolling past 2,000 words of blog narrative to reach the ingredient
list, and who want their saved recipes to work reliably, offline, and in their own language.

## Key features

| Feature | Description |
|---|---|
| **URL import** | Paste any recipe link; the app extracts a clean Title / Ingredients / Steps. |
| **On-device library** | Recipes are stored locally in SQLite — yours, offline, no account required. |
| **Focus Mode** | Full-screen, step-by-step cooking view that keeps the screen awake and the clutter away. |
| **Cook timers** | Durations inside steps (e.g. "simmer for 20 min", "leave 30 mins to cool") become tappable timers. |
| **Translation** | Imported recipes are translated into your language on-device (Google ML Kit), no per-use cost. |
| **Unit switching** | Toggle ingredients between metric and US/imperial. |
| **Manual add & edit** | Add or correct recipes by hand. |
| **Backup & import** | Export/import your recipe library as a file. |
| **Share-to-import** | Share a URL from the browser straight into PurePrep. |
| **Freemium** | A free tier with a recipe quota; Premium unlocks the full experience (Google Play Billing). |

## Supported languages

UI and recipe translation: **English, German, French, Spanish, Italian, Polish, Dutch.**
Recipe **source-language detection** additionally recognises languages such as **Romanian**, so
imported foreign recipes are translated rather than left untranslated.

---

## Architecture at a glance

PurePrep is a .NET 10, Clean-Architecture solution shared across a mobile app, a backend service,
and a browser preview.

| Project | Target | Purpose |
|---|---|---|
| `PurePrep.Core` | `net10.0` | Domain, Application interfaces, and Infrastructure (HtmlAgilityPack parser, EF Core SQLite repository). Shared by all apps. |
| `PurePrep` | `net10.0-android` (+ iOS/MacCatalyst/Windows) | The .NET MAUI app (MVVM, Focus Mode, on-device ML Kit translation, freemium UI). |
| `PurePrep.Server` | `net10.0` | Production ASP.NET Core backend deployed to Hetzner: AI recipe-parse proxy, smart-credits/billing, promo codes, and the launch **waitlist** (with GDPR consent). |
| `PurePrep.Web` | `net10.0` | Local ASP.NET Core browser preview that runs the real parser + repository behind a small JSON API, for trying behaviour without a phone. |

### Backend (PurePrep.Server)

Deployed at `https://api.pureprep.lechdigital.nl` (Docker Compose on Hetzner). Notable endpoints:

- `POST /api/ai/parse` — server-side AI recipe extraction proxy.
- `POST /api/waitlist` — launch waitlist signup (requires GDPR email consent).
- `GET  /api/admin/waitlist` — admin-only list of signups (`X-Admin-Secret`).
- `POST /api/promo/redeem`, `POST /api/billing/redeem`, `POST /api/credits/ensure` — entitlements.
- `GET  /health` — health check used by the deploy script.

The public marketing / waitlist landing page is served from the server's `wwwroot/index.html`.

---

## Release status & roadmap

- **Now — Closed testing.** Distributed to a small group of testers via Google Play closed
  testing. Actively collecting and shipping feedback fixes (navigation, translation accuracy,
  timer detection, unit discoverability).
- **~2 weeks out — Production.** Targeting an open/production Google Play release in
  **early-to-mid September 2026**, pending closed-testing sign-off and Play review.
- **At open testing / production launch**, waitlist registrants who opted in will receive a
  one-off informational email (no marketing) letting them know the app is available.

### Known items before production

- **DNS:** add an A record for the marketing host `pureprep.lechdigital.nl` → `167.233.145.128`
  (currently only `api.pureprep.lechdigital.nl` resolves).
- Continue on-device verification of MAUI/Android UI changes that can't be validated on CI.

---

## Privacy & data

- Recipes live **on the device**; no account is required to use the app.
- The waitlist stores only an email address and an explicit **GDPR consent** timestamp, used
  solely to send a single launch-notification email.
- See the in-app / hosted privacy notice (`/privacy`) for details.
