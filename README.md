# PurePrep

Anti-bloatware culinary app. Paste a recipe URL → PurePrep extracts only the **Title,
Ingredients, and Steps**, saves them locally, and gives you a distraction-free **Focus Mode**
for cooking.

See [`INIT.md`](./INIT.md) for the domain model, parsing design, and ViewModel state spec.

## Solution layout

| Project | Target | Purpose |
|---|---|---|
| `src/PurePrep.Core` | `net10.0` | Clean-Architecture core: Domain, Application (interfaces), Infrastructure (HtmlAgilityPack parser + EF Core SQLite repository). Shared by both apps. |
| `src/PurePrep` | `net10.0-android` (+ iOS/MacCatalyst/Windows) | .NET MAUI app (MVVM, Focus Mode, freemium UI). |
| `src/PurePrep.Web` | `net10.0` | ASP.NET Core browser preview that hosts the **real** parser + repository behind a small JSON API, so you can try behaviour before installing on a phone. |

## Run the browser preview

```bash
cd src/PurePrep.Web
dotnet run
```

Then open the printed URL (e.g. http://localhost:5284). The page paste-imports a URL, parses it
server-side with the real `RecipeParser`, saves to a local SQLite DB (`pureprep.web.db`, seeded
with a few samples on first run), enforces the 10-recipe free quota, and unlocks Focus Mode when
you toggle **Premium**.

> Offline restore note: if a private NuGet feed is unreachable, restore from nuget.org with
> `dotnet restore --source https://api.nuget.org/v3/index.json`.

### Web API

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/state` | Saved recipes + current quota |
| `POST` | `/api/parse` | `{ "url": "..." }` → parse + save (quota-gated) |
| `POST` | `/api/premium` | `{ "isPremium": true }` → toggle Premium entitlement |

## Build the MAUI app

Requires the MAUI workload (`dotnet workload install maui`):

```bash
dotnet build src/PurePrep/PurePrep.csproj -f net10.0-android
```
