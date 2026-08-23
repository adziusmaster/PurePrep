# PurePrep — Play Store listing assets

All images are generated from HTML in `_src/` via headless Chrome. To regenerate:

```bash
node store-assets/_src/build.mjs
# then re-flatten the icon to 32-bit RGBA:
python3 -c "from PIL import Image; Image.open('store-assets/icon-512.png').convert('RGBA').save('store-assets/icon-512.png')"
```

## Graphic assets (upload in Play Console → Store listing / Main store listing)

| Asset | File | Size | Play requirement |
|-------|------|------|------------------|
| App icon | `icon-512.png` | 512×512, 32-bit PNG | Required |
| Feature graphic | `feature-1024x500.png` | 1024×500 | Required |
| Phone screenshots | `phone/phone-1..5-*.png` | 1080×1920 (9:16) | 2–8 required |
| 7-inch tablet | `tablet7/tablet7-1..2-*.png` | 1200×1920 | Optional (needed to list as tablet-optimised) |
| 10-inch tablet | `tablet10/tablet10-1..2-*.png` | 1600×2560 | Optional (needed to list as tablet-optimised) |

Screenshot dimension rules met: min side ≥ 320 px, max side ≤ 3840 px, max ≤ 2× min.

## Suggested listing text

**App name (30 chars max):**
PurePrep — Recipe Keeper

**Short description (80 chars max):**
Paste a recipe link. Keep the ingredients and steps. Lose the ads and clutter.

**Full description:**
PurePrep is a calm, no-nonsense recipe app. Paste a recipe URL and PurePrep keeps
only what matters — the title, ingredients and steps — and drops the ads, pop-ups,
autoplay videos and endless life stories.

• Smart Import — our AI reads even the messiest recipe pages and gives you a clean,
  readable recipe every time.
• Metric ⇄ Imperial — switch units in one tap. PurePrep detects the recipe's units
  on import and converts amounts back and forth accurately.
• Your library, offline — saved recipes live on your device and load instantly.
• Add and edit by hand — no link needed. Type in your own recipes and tweak anything.
• Quiet by design — dark, high-contrast, distraction-free. No accounts, no tracking,
  no ads.

Free to use for up to 10 saved recipes. Import more by link with Smart Credits.

Privacy policy: https://lechdigital.nl/PurePrep/
