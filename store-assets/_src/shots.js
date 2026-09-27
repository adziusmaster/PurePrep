// PurePrep 1.4 phone screenshots: a two-line headline (one lime accent word) and a subline over a
// real capture from the app, framed as a phone. Rendered by build.mjs (`shots` target).
//
// Copy comes straight from PLAY-LISTING.md §9 (English table + "Localized headlines" tables), so the
// listing document stays the single source of truth. Captures come from _src/shots/<lang>/, named
// `<slot>-<anything>.png`, as full-screen Pixel captures (1080×2424). Android's status and
// navigation bars are cropped here, by the constants below, so the raw captures never need editing.
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { doc, T } from './theme.js';

const __dir = dirname(fileURLToPath(import.meta.url));
export const SHOTS_DIR = join(__dir, 'shots');
const LISTING = join(__dir, '..', 'PLAY-LISTING.md');

export const W = 1080, H = 1920;           // Play phone screenshot, 9:16

// ---------- capture crop ----------
// Heights of Android's system bars in a 1080-wide Pixel capture, measured on the real English
// captures (see README). The status bar is cut off the top and the navigation bar off the
// bottom; anything else in the capture is the app's own UI and is kept.
export const STATUS_BAR = 140;
export const NAV_BAR = 126;

export const LANGS = { en: 'en-US', pl: 'pl-PL', de: 'de-DE', fr: 'fr-FR', es: 'es-ES', it: 'it-IT', nl: 'nl-NL' };

// Output file slug per slot, in PLAY-LISTING.md §9 order.
export const SLUGS = ['recipe', 'share', 'search', 'focus', 'timers', 'scale', 'library', 'private'];

// ---------- slot 7 switch ----------
// Slot 7 is provisional: the Library capture, or the listing's optional swap "Settings → language
// list" (PLAY-LISTING.md §9, "Optional swaps"). Pick with `node build.mjs shots en --slot7=languages`,
// or change the default here. A retake of the library screen just replaces _src/shots/<lang>/7-*.png.
export const SLOT7_DEFAULT = 'library';
export const SLOT7 = {
  library: null,                                     // the §9 table row + 7-*.png
  languages: {
    capture: 'swap7-settings-language.png',
    slug: 'languages',
    layout: { tilt: 2, dx: -10, gap: 100, fit: true },
    // Headline from §9's optional swap. The subline is not in PLAY-LISTING.md yet; English only.
    copy: { en: { lines: ['Cooks in *seven*', 'languages.'], sub: 'Switch the whole app in Settings.' } },
  },
};

// ---------- copy ----------

/** Rows of the first table after `heading` in PLAY-LISTING.md: [{ n, headline, sub }]. */
const tableAfter = (md, heading) => {
  const at = md.indexOf(heading);
  if (at < 0) return null;
  const rows = [];
  for (const line of md.slice(at).split('\n').slice(1)) {
    if (/^#{2,4} /.test(line) && rows.length) break;
    const m = line.match(/^\|\s*(\d)\s*\|\s*(.+?)\s*\|\s*(.+?)\s*\|/);
    if (m) rows.push({ n: Number(m[1]), headline: m[2], sub: m[3] });
    else if (rows.length && !line.startsWith('|')) break;
  }
  return rows.length ? rows : null;
};

/** The eight slots' copy for a language, or null if PLAY-LISTING.md has no table for it. */
export const copyFor = (lang) => {
  const md = readFileSync(LISTING, 'utf8');
  const rows = lang === 'en'
    ? tableAfter(md, '## 9. Phone screenshot slot plan')
    : tableAfter(md, `#### ${LANGS[lang]}`);
  if (!rows || rows.length !== 8) return null;
  return rows.map((r) => {
    const [l1, l2 = ''] = r.headline.split(' / ');
    return { n: r.n, lines: [l1, l2], sub: r.sub };
  });
};

const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
/** Markdown `*word*` → the lime accent. Everything else is plain text. */
const accent = (s) => esc(s)
  .replace(/"([^"]+)"/g, '\u201C$1\u201D')           // straight quotes → typographic (en copy uses "…")
  .replace(/\*(.+?)\*/g, `<em>$1</em>`);

// ---------- captures ----------

const pngSize = (file) => {
  const b = readFileSync(file);
  return { w: b.readUInt32BE(16), h: b.readUInt32BE(20) };
};

/** The capture for a slot, or a labelled placeholder (same size, fake bars) if none exists yet. */
export const captureFor = (lang, n, CAPTURE = {}) => {
  const dir = join(SHOTS_DIR, lang);
  const pick = CAPTURE[n] && existsSync(join(dir, CAPTURE[n])) ? CAPTURE[n] : null;
  const name = pick ?? (existsSync(dir) && readdirSync(dir).filter((f) => f.startsWith(`${n}-`) && f.endsWith('.png')).sort()[0]);
  if (name) {
    const file = join(dir, name);
    // Referenced by URL relative to _src/html/, so the generated HTML stays small.
    return { src: `../shots/${lang}/${encodeURIComponent(name)}`, ...pngSize(file), file, real: true };
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="1080" height="2424">
    <rect width="1080" height="2424" fill="#3a3f3b"/>
    <rect width="1080" height="${STATUS_BAR}" fill="#b04a4a"/>
    <rect y="${2424 - NAV_BAR}" width="1080" height="${NAV_BAR}" fill="#b04a4a"/>
    <rect x="60" y="${STATUS_BAR + 60}" width="960" height="560" rx="28" fill="#4c524d"/>
    ${[0, 1, 2, 3, 4, 5, 6].map((i) => `<rect x="60" y="${STATUS_BAR + 700 + i * 120}" width="${900 - i * 60}" height="44" rx="10" fill="#4c524d"/>`).join('')}
    <text x="540" y="1900" fill="#9aa39b" font-family="sans-serif" font-size="64" text-anchor="middle">placeholder · slot ${n}</text>
  </svg>`;
  return { src: `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`, w: 1080, h: 2424, real: false };
};

// ---------- layout ----------

const PAD = 76;                    // side margin for the copy
const HEAD_MAX = 100;              // headline size before auto-shrink, px
const HEAD_MIN = 54;
const SUB_SIZE = 36;

// Per-slot composition. `tilt` in degrees, `dx` shifts the phone horizontally, `gap` is the space
// between the copy's last line and the frame (the page places the frame after the fonts load). The variety is deliberate but small: the set should read as one family.
const LAYOUT = {
  // photo: the recipe photo's rect in the slot-1 capture (x, y, w, h in capture px), measured from
  // _src/shots/en/1-*.png. Re-measure if that capture is retaken; the build checks nothing here.
  1: { tilt: -4, dx: 30, gap: 160, photo: { x: 57, y: 372, w: 967, h: 517 } },
  // fit: the whole screen is shown (scaled to the space under the copy) instead of bleeding off the
  // bottom, for captures whose point is at the bottom: the import sheet, the floating button, the add
  // sheet. pop: a rect of the capture (px) lifted out of the screen at `scale`, over its own spot.
  2: { tilt: 3, dx: -10, gap: 100, fit: true },
  3: { tilt: -3, dx: 10, gap: 100, fit: true, pop: { x: 296, y: 2099, w: 489, h: 135, scale: 1.3, radius: 68 } },
  4: { tilt: 0, dx: 0, gap: 140 },
  5: { tilt: 3, dx: -20, gap: 160 },
  6: { tilt: -3, dx: 20, gap: 160 },
  7: { tilt: 2, dx: -10, gap: 150 },
  8: { tilt: -2, dx: 10, gap: 100, fit: true },
};

const SCREEN_W = 776;              // visible screen width in the frame
const BEZEL = 14;
const FIT_BOTTOM = 70;             // a fit-mode phone's gap to the bottom edge

/**
 * Slots 1–3 share one 3240-wide backdrop, so seen side by side in the Play carousel the glow and the
 * lime arc run on from one screenshot into the next. Each slot shows its own 1080-wide window of it.
 */
const panorama = (n) => `
<svg width="${W * 3}" height="${H}" viewBox="0 0 ${W * 3} ${H}" style="position:absolute;left:${-(n - 1) * W}px;top:0;">
  <defs>
    <radialGradient id="g" cx="50%" cy="50%" r="50%">
      <stop offset="0" stop-color="${T.primary}" stop-opacity=".22"/>
      <stop offset=".5" stop-color="${T.primary}" stop-opacity=".07"/>
      <stop offset="1" stop-color="${T.primary}" stop-opacity="0"/>
    </radialGradient>
  </defs>
  <ellipse cx="620" cy="1250" rx="900" ry="820" fill="url(#g)"/>
  <ellipse cx="1700" cy="1150" rx="820" ry="760" fill="url(#g)"/>
  <ellipse cx="2700" cy="1280" rx="880" ry="800" fill="url(#g)"/>
  <path d="M -80 1500 C 700 520, 1500 1780, 2200 900 S 3100 700, 3340 1060"
        fill="none" stroke="${T.primary}" stroke-opacity=".32" stroke-width="3"/>
  <path d="M -80 1560 C 700 580, 1500 1840, 2200 960 S 3100 760, 3340 1120"
        fill="none" stroke="${T.primary}" stroke-opacity=".12" stroke-width="2"/>
</svg>`;

/** Slots 4–8: one glow behind the phone, placed to balance its tilt. */
const glow = (n) => {
  const { tilt } = LAYOUT[n];
  const x = 50 - tilt * 4;
  return `<div style="position:absolute;inset:0;background:
    radial-gradient(70% 48% at ${x}% 66%, rgba(167,212,111,.20) 0%, rgba(167,212,111,.06) 48%, transparent 72%);"></div>`;
};

const CUTOUT = () => '../assets/pierogi-cutout.png';   // relative to _src/html/
const PHOTO_W = 1280, PHOTO_H = 960;   // sample-pierogi.jpg, the grid pierogi-cutout.png is on

/**
 * The food breaking out of the frame: the cutout laid over the capture's recipe photo on the exact
 * same pixel grid. The app shows the photo aspect-fill (centred), which crops the plate top and
 * bottom; the cutout is not clipped, so only the food the app cropped away spills past the photo
 * and the phone's screen. `photo` is the photo's rect in capture pixels.
 */
const breakout = ({ x, y, w, h }, scale) => {
  const s = Math.max(w / PHOTO_W, h / PHOTO_H);
  const fw = PHOTO_W * s, fh = PHOTO_H * s;
  return `<img src="${CUTOUT()}" style="position:absolute;
    left:${BEZEL + (x - (fw - w) / 2) * scale}px;top:${BEZEL + (y - STATUS_BAR - (fh - h) / 2) * scale}px;
    width:${fw * scale}px;height:${fh * scale}px;filter:drop-shadow(0 16px 18px rgba(0,0,0,.55));">`;
};

/** A piece of the capture lifted out of the screen, enlarged over its own position. */
const popOut = (cap, { x, y, w, h, scale: k, radius = 24 }, scale) => `
<div style="position:absolute;left:${BEZEL + x * scale}px;top:${BEZEL + (y - STATUS_BAR) * scale}px;
            width:${w * scale}px;height:${h * scale}px;transform:scale(${k});border-radius:${radius * scale}px;overflow:hidden;
            box-shadow:0 24px 50px rgba(0,0,0,.6), 0 0 0 1px rgba(255,255,255,.06);">
  <img src="${cap.src}" style="position:absolute;left:${-x * scale}px;top:${-y * scale}px;width:${cap.w * scale}px;height:${cap.h * scale}px;">
</div>`;

/** The capture, cropped to the app's own UI, in a rounded phone frame with a subtle bezel. */
const phone = (cap, { tilt, dx, gap, photo, fit, pop }) => {
  const scale = SCREEN_W / cap.w;
  const screenH = Math.round((cap.h - STATUS_BAR - NAV_BAR) * scale);
  const fw = SCREEN_W + BEZEL * 2;
  return `
<div class="phone" data-gap="${gap}" data-fit="${fit ? 1 : ''}" data-tilt="${tilt}"
     style="position:absolute;left:${(W - fw) / 2 + dx}px;top:${400 + gap}px;width:${fw}px;height:${screenH + BEZEL * 2}px;
            transform:rotate(${tilt}deg);transform-origin:${fit ? '50% 100%' : '50% 30%'};border-radius:78px;padding:${BEZEL}px;
            background:linear-gradient(155deg,#2a312b 0%,#151a16 45%,#1d231e 100%);
            box-shadow:0 0 0 2px rgba(255,255,255,.07) inset, 0 0 0 1.5px #050705,
                       0 60px 110px rgba(0,0,0,.62), 0 18px 40px rgba(0,0,0,.45);">
  <div style="position:relative;width:${SCREEN_W}px;height:${screenH}px;border-radius:64px;overflow:hidden;background:#000;">
    <img src="${cap.src}" style="position:absolute;left:0;top:${-Math.round(STATUS_BAR * scale)}px;width:${SCREEN_W}px;height:${Math.round(cap.h * scale)}px;display:block;">
  </div>
  ${photo && cap.real ? breakout(photo, scale) : ''}
  ${pop && cap.real ? popOut(cap, pop, scale) : ''}
</div>`;
};

const COPY_CSS = `
body:before{display:none;}
.copy{position:absolute;left:${PAD}px;right:${PAD}px;top:112px;}
.head{font-weight:800;line-height:1.04;letter-spacing:-.035em;color:${T.ink};}
.head div{white-space:nowrap;}
.head em{font-style:normal;color:${T.primary};}
.sub{margin-top:30px;font-weight:600;font-size:${SUB_SIZE}px;line-height:1.36;color:${T.inkSoft};letter-spacing:-.005em;}
`;

// Shrinks the headline until both lines fit the copy width, and the subline until it fits two
// lines. Runs after the web fonts load, so it measures the real glyphs.
const FIT_JS = (max) => `
<script>
document.fonts.ready.then(() => {
  const box = document.querySelector('.copy'), head = document.querySelector('.head'), sub = document.querySelector('.sub');
  let s = ${max};
  head.style.fontSize = s + 'px';
  const wide = () => [...head.children].some((l) => l.scrollWidth > box.clientWidth);
  while (wide() && s > ${HEAD_MIN}) { s -= 1; head.style.fontSize = s + 'px'; }
  // Last resort for a line that is too long even at the minimum size: let it wrap rather than clip.
  if (wide()) for (const l of head.children) l.style.whiteSpace = 'normal';
  let t = ${SUB_SIZE};
  const lh = () => parseFloat(getComputedStyle(sub).lineHeight);
  while (sub.scrollHeight > lh() * 2 + 2 && t > 26) { t -= 1; sub.style.fontSize = t + 'px'; }
  const phone = document.querySelector('.phone');
  const top = box.getBoundingClientRect().bottom + Number(phone.dataset.gap);
  if (phone.dataset.fit) {
    // Bottom-anchored ${FIT_BOTTOM}px above the canvas edge, scaled down to the room under the copy.
    const h = phone.offsetHeight, k = Math.min(1, (${H - FIT_BOTTOM} - top) / h);
    phone.style.top = (${H - FIT_BOTTOM} - h) + 'px';
    phone.style.transform = 'rotate(' + phone.dataset.tilt + 'deg) scale(' + k.toFixed(4) + ')';
  } else phone.style.top = Math.round(top) + 'px';
  document.body.dataset.fit = s;
});
</script>`;

const copyBlock = (c) => `
<div class="copy">
  <div class="head">${c.lines.filter(Boolean).map((l) => `<div>${accent(l)}</div>`).join('')}</div>
  <div class="sub">${accent(c.sub)}</div>
</div>`;

/** The page that measures how large every headline of a language can be (read back via --dump-dom). */
export const measurePage = (copy) => doc(W, H, copy.map((c) => `
<div class="copy" style="position:relative;left:auto;right:auto;top:auto;width:${W - PAD * 2}px;margin-left:${PAD}px;">
  <div class="head" data-n="${c.n}">${c.lines.filter(Boolean).map((l) => `<div>${accent(l)}</div>`).join('')}</div>
</div>`).join('') + `
<script>
document.fonts.ready.then(() => {
  const out = {};
  for (const head of document.querySelectorAll('.head')) {
    const w = head.parentElement.clientWidth;
    let s = ${HEAD_MAX};
    head.style.fontSize = s + 'px';
    while ([...head.children].some((l) => l.scrollWidth > w) && s > ${HEAD_MIN}) { s -= 1; head.style.fontSize = s + 'px'; }
    out[head.dataset.n] = s;
  }
  document.body.setAttribute('data-fits', JSON.stringify(out));
});
</script>`, COPY_CSS);

/**
 * One size for the whole language, so the set reads as a family: the smallest slot's fit, but never
 * below 90 % of the median fit. A slot that needs less than that keeps its own, smaller fit (the page
 * script shrinks it further if it must), so nothing ever overflows.
 */
export const languageSize = (fits) => {
  const v = Object.values(fits).sort((a, b) => a - b);
  const median = v[Math.floor(v.length / 2)];
  return Math.max(v[0], Math.floor(median * 0.9));
};

export const shotPage = (c, cap, headSize, layout = LAYOUT[c.n]) => doc(W, H, `
<div style="position:absolute;inset:0;background:${T.appBg};"></div>
${c.n <= 3 ? panorama(c.n) : glow(c.n)}
${phone(cap, layout)}
${copyBlock(c)}
${FIT_JS(headSize)}`, COPY_CSS);

/** All eight renders in one row, in carousel order, for review. */
export const contactPage = (lang, files, notes) => {
  const tw = 300, th = Math.round(tw * H / W), gap = 18, m = 48;
  const width = m * 2 + tw * files.length + gap * (files.length - 1);
  return {
    width, height: th + 190,
    html: doc(width, th + 190, `
<div style="position:absolute;inset:0;background:#1c221d;"></div>
<div style="position:absolute;left:${m}px;top:34px;font-weight:800;font-size:30px;color:${T.ink};letter-spacing:-.02em;">
  PurePrep phone screenshots · ${LANGS[lang]}
  <span style="color:${T.muted};font-weight:600;font-size:19px;margin-left:12px;">${esc(notes)}</span>
</div>
<div style="position:absolute;left:${m}px;top:100px;display:flex;gap:${gap}px;">
  ${files.map((f, i) => `<div>
    <img src="${f.rel}" style="width:${tw}px;height:${th}px;display:block;border-radius:6px;">
    <div class="mono" style="margin-top:10px;font-size:14px;color:${f.real ? T.muted : T.orange};">${i + 1} · ${f.slug}${f.real ? '' : ' · PLACEHOLDER'}</div>
  </div>`).join('')}
</div>`, 'body:before{display:none;}'),
  };
};
