// PurePrep 1.4 store-presence proposals: the new feature graphic and the icon options.
// Rendered by build.mjs (default target). Nothing here fakes app UI — the only imagery is the
// app's own bundled sample photo (Unsplash licence) and the app's own icon mark.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { doc, T } from './theme.js';

const __dir = dirname(fileURLToPath(import.meta.url));
const REPO = join(__dir, '..', '..');

const dataUri = (file, mime) => `data:${mime};base64,${readFileSync(file).toString('base64')}`;
const PHOTO = () => dataUri(join(REPO, 'src/PurePrep/Resources/Raw/sample-pierogi.jpg'), 'image/jpeg');
const CUTOUT = () => dataUri(join(__dir, 'assets/pierogi-cutout.png'), 'image/png');

// The shipped mark (src/PurePrep/Resources/AppIcon/appiconfg.svg), drawn on a 300×300 box.
const bowl = (fill, shade = T.appInk, shadeOpacity = 0.18) => `
  <g stroke="${fill}" stroke-width="15" stroke-linecap="round" fill="none">
    <path d="M112 96 C 96 78, 128 66, 112 46"/>
    <path d="M150 100 C 134 78, 166 64, 150 40"/>
    <path d="M188 96 C 172 78, 204 66, 188 46"/>
  </g>
  <rect x="70" y="150" width="160" height="26" rx="13" fill="${fill}"/>
  <path d="M84 178 h132 a10 10 0 0 1 10 12 a76 76 0 0 1 -152 0 a10 10 0 0 1 10 -12 z" fill="${fill}"/>
  <path d="M150 250 a76 76 0 0 0 66 -60 h-132 a76 76 0 0 0 66 60 z" fill="${shade}" opacity="${shadeOpacity}"/>`;

/** The app icon as it looks on a launcher: dark tile, lime mark, rounded like Play's mask. */
const appIconTile = (size) => `
<div style="width:${size}px;height:${size}px;border-radius:${size * 0.22}px;background:${T.appBg};
            box-shadow:0 0 0 1px rgba(167,212,111,.28);display:flex;align-items:center;justify-content:center;">
  <svg width="${size * 0.86}" height="${size * 0.86}" viewBox="40 30 220 240">${bowl(T.primary)}</svg>
</div>`;

// ---------- Feature graphic 1024×500 ----------
//
// Inspiration style: one bold two-line headline with a single accent word, a dark field with one
// accent colour, and food breaking out of its frame. The photo sits in a tilted card; the cutout
// (cutout.py) is laid over it on the same pixel grid, so the pierogi spill past the card's edges.
// Play overlays nothing on the feature graphic today, but it can be cropped at the edges on some
// surfaces, so the headline stays well inside a 64 px safe margin.

const PHOTO_W = 1280, PHOTO_H = 960;   // the sample photo's own size
const FOOD_SCALE = 0.34;               // photo px → feature px

export const featureNew = () => {
  const fw = PHOTO_W * FOOD_SCALE, fh = PHOTO_H * FOOD_SCALE;       // 435 × 326
  // The card is a window onto the middle of the plate; the food is wider than it on purpose.
  const card = { w: 220, h: 266, dy: 32 };
  const photoLayer = (src, extra = '') =>
    `<img src="${src}" style="position:absolute;left:50%;top:50%;width:${fw}px;height:${fh}px;
       transform:translate(-50%,-50%);${extra}">`;
  return doc(1024, 500, `
<div style="position:absolute;inset:0;background:
  radial-gradient(46% 70% at 79% 52%, rgba(167,212,111,.20) 0%, rgba(167,212,111,.06) 45%, transparent 70%),
  ${T.appBg};"></div>

<!-- food + frame -->
<div style="position:absolute;left:800px;top:238px;width:0;height:0;transform:rotate(-7deg);">
  <!-- the card -->
  <div style="position:absolute;left:${-card.w / 2}px;top:${-card.h / 2 + card.dy}px;width:${card.w}px;height:${card.h}px;
              border-radius:28px;overflow:hidden;background:#f3f1ec;
              box-shadow:0 30px 60px rgba(0,0,0,.55), 0 0 0 6px ${T.primary};">
    <div style="position:absolute;left:50%;top:calc(50% - ${card.dy}px);width:0;height:0;">${photoLayer(PHOTO())}</div>
  </div>
  <!-- the same food, unclipped: it reads as breaking out of the card -->
  <div style="position:absolute;left:0;top:0;width:0;height:0;
              filter:drop-shadow(0 18px 22px rgba(0,0,0,.55));">${photoLayer(CUTOUT())}</div>
</div>

<!-- copy -->
<div style="position:absolute;left:64px;top:0;bottom:0;width:600px;display:flex;flex-direction:column;justify-content:center;">
  <div style="display:flex;align-items:center;gap:14px;">
    ${appIconTile(46)}
    <div style="font-weight:800;font-size:25px;letter-spacing:-.01em;color:${T.ink};">PurePrep</div>
  </div>
  <div style="font-weight:800;font-size:60px;line-height:1.02;letter-spacing:-.035em;margin:26px 0 18px;color:${T.ink};">
    The recipe.<br>Without the <span style="color:${T.primary};">noise.</span>
  </div>
  <div style="color:${T.inkSoft};font-size:21px;line-height:1.45;max-width:430px;">
    Save recipes from any website.<br>Cook them step by step.
  </div>
  <div style="display:flex;gap:10px;margin-top:26px;">
    ${['No ads', 'No account', 'No tracking'].map((t) => `
      <div style="border:1.5px solid ${T.primary};color:${T.primary};border-radius:999px;padding:8px 16px;
                  font-weight:700;font-size:16px;letter-spacing:.01em;white-space:nowrap;">${t}</div>`).join('')}
  </div>
</div>`, 'body:before{display:none;}');
};

// ---------- Icon options 512×512 ----------
//
// Play masks the 512 icon itself (rounded square), so each option fills the full square and
// draws no rounded corners, border or drop shadow of its own. The mark is kept within the
// central ~70% so no mask shape clips it.

const iconDoc = (inner, bg) => doc(512, 512, `
<div style="position:absolute;inset:0;${bg}"></div>
<div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;">${inner}</div>`,
  'body:before{display:none;}');

export const iconOptions = () => [
  {
    key: 'a-bowl-refined',
    title: 'A · Bowl, refined',
    why: 'Keeps the shipped steaming-bowl mark and colours, drops the tiny "PUREPREP" wordmark and scales the mark up, so it stays legible at launcher size — lowest-risk continuity.',
    html: iconDoc(
      `<svg width="360" height="360" viewBox="40 30 220 240">${bowl(T.primary)}</svg>`,
      `background:radial-gradient(90% 90% at 50% 38%, #18231a 0%, ${T.appBg} 70%);`),
  },
  {
    key: 'b-lime-tile',
    title: 'B · Lime tile',
    why: 'Same bowl, inverted onto the app\'s lime: a bright, warm tile that stands out in the Play grid and the launcher, where most recipe apps are red, orange or white.',
    html: iconDoc(
      `<svg width="360" height="360" viewBox="40 30 220 240">${bowl(T.appBg, T.primary, 0)}</svg>`,
      `background:linear-gradient(160deg, #b9e07f 0%, ${T.primary} 55%, #93c25a 100%);`),
  },
  {
    key: 'c-pan-p',
    title: 'C · Pan "P"',
    why: 'A new monogram: a frying pan seen from above whose handle is the stem of a "P" — the brand initial and cooking in one shape, readable even at 48 px.',
    html: iconDoc(`
<svg width="400" height="400" viewBox="28 0 400 400">
  <!-- steam rising off the pan -->
  <g stroke="${T.primary}" stroke-width="15" stroke-linecap="round" fill="none">
    <path d="M196 76 c -11 -13, 11 -22, 0 -38"/>
    <path d="M230 70 c -11 -14, 11 -24, 0 -42"/>
    <path d="M264 76 c -11 -13, 11 -22, 0 -38"/>
  </g>
  <!-- handle = the P's stem, with a hanging hole -->
  <rect x="130" y="196" width="50" height="180" rx="25" fill="${T.primary}"/>
  <circle cx="155" cy="350" r="9" fill="${T.appBg}"/>
  <!-- pan = the P's bowl -->
  <circle cx="230" cy="196" r="100" fill="${T.primary}"/>
  <circle cx="230" cy="196" r="68" fill="#1b2616"/>
</svg>`, `background:radial-gradient(90% 90% at 50% 38%, #18231a 0%, ${T.appBg} 70%);`),
  },
];

/** All options side by side: Play's rounded-square mask, a round launcher mask, small sizes, on dark and light. */
export const iconComparison = (options, files) => doc(1680, 680, `
<div style="position:absolute;inset:0;background:#1c221d;"></div>
<div style="position:absolute;left:60px;top:44px;font-weight:800;font-size:34px;letter-spacing:-.02em;color:${T.ink};">
  PurePrep icon options <span style="color:${T.muted};font-weight:600;font-size:20px;letter-spacing:0;margin-left:12px;">proposals only — the shipped icon is unchanged</span>
</div>
<div style="position:absolute;left:60px;right:60px;top:120px;display:grid;grid-template-columns:repeat(${options.length},1fr);gap:40px;">
  ${options.map((o, i) => `
  <div>
    <div style="display:flex;align-items:flex-end;gap:22px;">
      <img src="${files[i]}" style="width:300px;height:300px;border-radius:66px;display:block;">
      <div style="display:flex;flex-direction:column;gap:12px;">
        <div style="display:flex;gap:12px;align-items:center;">
          <img src="${files[i]}" style="width:84px;height:84px;border-radius:50%;display:block;">
          <img src="${files[i]}" style="width:48px;height:48px;border-radius:11px;display:block;">
        </div>
        <div style="display:flex;gap:12px;align-items:center;background:#f4f4f1;border-radius:14px;padding:10px;">
          <img src="${files[i]}" style="width:64px;height:64px;border-radius:50%;display:block;">
          <img src="${files[i]}" style="width:40px;height:40px;border-radius:9px;display:block;">
        </div>
      </div>
    </div>
    <div style="font-weight:800;font-size:24px;margin:28px 0 10px;color:${T.primary};">${o.title}</div>
    <div style="color:${T.inkSoft};font-size:17px;line-height:1.5;max-width:420px;">${o.why}</div>
  </div>`).join('')}
</div>`, 'body:before{display:none;}');

export { dataUri };
