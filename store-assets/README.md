# PurePrep — Play Store listing assets

Every image is generated from HTML in `_src/` with headless Chrome, so any of them can be rebuilt
from source. The listing text is in [`PLAY-LISTING.md`](PLAY-LISTING.md): English and Polish, with
verified character counts, the claims check and the screenshot slot plan.

## Rebuilding

```bash
node store-assets/_src/build.mjs            # 1.4 set: new feature graphic + icon options (default)
node store-assets/_src/build.mjs icon       # live icon-512.png (1.4: option B, "Lime tile")
node store-assets/_src/build.mjs legacy     # pre-1.4 set: feature, mock screens
node store-assets/_src/build.mjs all        # all of the above
node store-assets/_src/check-listing.mjs    # verify every character count in PLAY-LISTING.md
```

Requirements: Google Chrome in `/Applications`, Node 18+, and `python3` with **Pillow** and
**numpy**. Fonts (Manrope, DM Mono) load from Google Fonts at render time, so the build needs a
network connection.

The default target never overwrites the live `feature-1024x500.png`, `icon-512.png` or `phone/`.
It writes only:

| Output | What |
|--------|------|
| `_src/assets/pierogi-cutout.png` | Food cut out of `src/PurePrep/Resources/Raw/sample-pierogi.jpg` by `_src/cutout.py`, regenerated on every build |
| `feature-1024x500.new.png` | New feature graphic (proposal) |
| `icon-options/icon-option-*.png` | Icon proposals, 512×512, 32-bit RGBA |
| `icon-options/icon-options-compare.png` | The options side by side: Play mask, round mask, small sizes, dark and light |

Chrome writes 24-bit RGB, and Play wants the icon as 32-bit RGBA. The build re-flattens every icon
itself, including `icon-512.png` in the legacy target, so there is no manual step after it.

### Source layout

| File | Role |
|------|------|
| `_src/build.mjs` | Renders every target |
| `_src/v14.js` | 1.4 feature graphic, icon options, comparison sheet |
| `_src/cutout.py` | Saturation-mask cutout of the sample photo, so the food can break out of its frame |
| `_src/theme.js` | Tokens. The 1.4 assets use the app's own colours (`appBg #0C100D`, `primary #A7D46F`, from `Colors.xaml` / `appicon.svg`) |
| `_src/parts.js`, `_src/icon.js` | Pre-1.4 mock parts and live icon (legacy target) |
| `_src/check-listing.mjs` | Character-count check for `PLAY-LISTING.md` |
| `_src/html/` | Generated HTML, kept so any asset can be opened in a browser |

## Graphic assets (Play Console → Grow → Store presence → Main store listing)

| Asset | File | Size | Play requirement |
|-------|------|------|------------------|
| App icon | `icon-512.png` (live, unchanged) | 512×512, 32-bit PNG | Required |
| Feature graphic | `feature-1024x500.new.png` → rename over `feature-1024x500.png` once approved | 1024×500 | Required |
| Phone screenshots | `phone/phone-1..8-*.png`, **to be captured from the final 1.4 build** | 1080×1920 (9:16) | 2–8 required |
| 7-inch / 10-inch tablet | `tablet7/`, `tablet10/` are the pre-1.4 mocks | 1200×1920 / 1600×2560 | Only needed to be listed as tablet-optimised |

Screenshot rules: min side ≥ 320 px, max side ≤ 3840 px, max ≤ 2× min. The feature graphic is
exactly 1024×500. That is its own fixed spec, and the 2× rule does not apply to it.

The current `phone/` and tablet images are **typographic mock-ups of the pre-1.4 app** and show
none of the 1.4 features. They should all be replaced by real captures.

### Feature graphic (1.4)

Style: dark field in the app's `#0C100D`, a single lime accent (`#A7D46F`), and a bold two-line
headline with one accent word: "The recipe. / Without the **noise.**". The pills say "No ads · No
account · No tracking". The pierogi from the app's bundled sample recipe break out of a tilted photo
card. The cutout sits on the photo's exact pixel grid, so only the parts outside the card read as
popping out. There is no fake UI. The only imagery is the sample photo (Unsplash licence, ships in
the app) and the app's own bowl mark.

### Icon options (proposals only — nothing in `src/` was touched)

| Option | File | Rationale |
|--------|------|-----------|
| A · Bowl, refined | `icon-options/icon-option-a-bowl-refined.png` | Keeps the shipped mark and colours but drops the tiny "PUREPREP" wordmark, which is illegible at launcher size, and scales the mark up. The lowest-risk continuity option. |
| B · Lime tile | `icon-options/icon-option-b-lime-tile.png` | The same bowl, inverted onto the app's lime. It stands out in the Play grid, where most recipe icons are red, orange or white. |
| C · Pan "P" | `icon-options/icon-option-c-pan-p.png` | A new monogram: a frying pan seen from above, with the handle as the stem of a "P" and steam rising off it. It is the brand initial and a cooking object in one shape. |

Play masks the 512 icon itself, so each option fills the whole square. None draws its own rounded
corners, border or shadow, and each mark stays inside the central ~70% safe zone. If one is chosen,
the adaptive launcher icon (`src/PurePrep/Resources/AppIcon/appicon*.svg`) should change with it,
so the store icon and the installed icon match.

## Phone screenshot slot plan (8)

Each slot is a two-line headline with one accent word, plus a subline, over a **real capture** from
the final 1.4 build. Record in the dark theme on the Pixel, then crop off Android's status and
navigation bars. Use the sample Pierogi plus a few recipes added by hand with photos, so no credits
are spent. Full detail (sublines, optional swaps) is in `PLAY-LISTING.md` §4.

| # | Headline (*accent*) | Screen |
|---|---|---|
| 1 | The recipe. / Without the *noise.* | Recipe detail (Pierogi: photo, meta, Serves − 4 +, ingredients) |
| 2 | *Share* a link. / Get the recipe. | Import sheet after sharing a link from Chrome |
| 3 | *Search* the web. / Add in one tap. | In-app web search with the "Add to PurePrep" button |
| 4 | Cook *step* by step. | Focus Mode step with "You'll need" chips and a timer chip |
| 5 | Timers that *know* / the recipe. | Focus Mode with the running-timers bar showing two named timers |
| 6 | *Scale* it for / any table. | Recipe detail after changing servings (scaled fractions, reset icon) |
| 7 | Your recipe box, / *organized.* | Library with photos, filter chips, a favourite |
| 8 | No ads. No account. / No *tracking.* | Add sheet: link · photo · text · type it in yourself |

The order puts the result first (a clean recipe is the strongest thing PurePrep offers a stranger),
then how a recipe gets in, then how you cook from it, then why you can trust it.

**Capturing:** `adb exec-out screencap -p > shot.png`, then crop off the status bar and the
gesture/nav bar, and put the result in `_src/shots/`. Before using a capture, check it for Android
system chrome such as notification icons or usage pills. **Never debug-install over the real app on
the maintainer's phone.** That wipes their recipes. Check the installed signature and installer
first, and ask.

## Release notes

`store/release-notes/<version>.txt`, one file per release. Each locale is wrapped in its own
`<xx-YY>` tag, so the whole file can be pasted into Play's release-notes field at once. Play allows
500 characters per locale.
