# PurePrep — Product Overview

> **Status:** Closed testing (Google Play), release-prep for open/production.
> **Current build:** 1.4.0 (version code 25) · Android · `com.adziusmaster.pureprep`

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
| **URL import** | Paste, share, or copy a recipe link; PurePrep extracts a clean Title / Ingredients / Steps, servings, prep/cook times, named timers and a photo (AI Smart Parser v2). |
| **In-app recipe search** | Search the web for recipes without leaving the app; recognised recipe pages get a floating "Add to PurePrep" button. |
| **On-device library** | Recipes are stored locally in SQLite — yours, offline, no account required. |
| **Focus Mode** | Full-screen, step-by-step cooking view that always fits the step to the screen, shows "You'll need" per step, keeps the screen awake, and offers Read-aloud/Stop with running timers. |
| **Cook timers** | Durations inside steps become named, tappable timers (including ranges, e.g. "15–20 min"), with a running-timers bar visible across the app. |
| **Scale by people** | Adjust servings with a stepper; ingredient amounts (and step amounts) scale live. |
| **Recipe editor** | One row per ingredient and step, drag to reorder, attach/replace a photo. |
| **Notes, favourites & status** | Add notes, mark favourites, track Want to cook / Cooked, and sort the library. |
| **Translation** | Imported recipes are translated into your language via the server (Google Gemini); each language is cached so it's paid for once. |
| **Unit switching** | Toggle ingredients between metric and US/imperial. |
| **Manual add & edit** | Add or correct recipes by hand. |
| **Backup & restore** | Export/import your recipe library as a file, including each recipe's saved photo. |
| **Share-to-import** | Share a URL from another app, or just copy a link — PurePrep offers to import it. |
| **Smart Credits** | A free-credit starter balance plus paid top-ups (Google Play Billing) cover AI recipe/photo imports; saving, editing, scaling and cooking are always free. |

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

- Recipes, and any **photo** attached to them, live **on the device**; no account is required to use the app.
- In-app recipe search loads **Google and third-party recipe sites inside the app**; those pages carry their own cookies/consent, same as in a regular browser.
- The waitlist stores only an email address and an explicit **GDPR consent** timestamp, used
  solely to send a single launch-notification email.
- See the in-app / hosted privacy notice (`/privacy`) for details.
