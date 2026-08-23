import { writeFileSync, mkdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { doc, T } from './theme.js';
import { iconHtml } from './icon.js';
import { unitToggle, creditPill, importBar, card, caption } from './parts.js';

const __dir = dirname(fileURLToPath(import.meta.url));
const ROOT = join(__dir, '..');            // store-assets/
const HTML = join(__dir, 'html');
mkdirSync(HTML, { recursive: true });
const CHROME = '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome';

// ---------- Feature graphic 1024x500 ----------
const feature = doc(1024, 500, `
<div style="position:absolute;inset:0;background:radial-gradient(90% 140% at 78% 20%, #16210f 0%, ${T.bg} 62%);"></div>
<div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:space-between;padding:0 70px;">
  <div style="max-width:560px;">
    <div class="eyebrow">PurePrep / kitchen utility</div>
    <div style="font-size:74px;font-weight:800;line-height:.98;letter-spacing:-.03em;margin:16px 0 18px;">Your kitchen,<br>focused.</div>
    <div style="color:${T.muted};font-size:21px;line-height:1.5;max-width:440px;">Paste a recipe link. Keep the ingredients and steps. Lose the ads, pop-ups and life stories.</div>
  </div>
  <div style="display:flex;flex-direction:column;gap:16px;transform:rotate(-4deg);">
    ${card('Mushroom Risotto','14 steps · 18 ingredients')}
    ${card('Air-fryer Arancini','4 steps · 10 ingredients')}
  </div>
</div>`, `.grid-bg:before{opacity:.14;}`);

// ---------- Phone screens 1080x1920 ----------
const PW = 1080, PH = 1920;
const phonePad = `padding:96px 72px;height:${PH}px;display:flex;flex-direction:column;justify-content:center;`;

const p1 = doc(PW, PH, `
<div style="${phonePad}">
  <div style="display:flex;justify-content:space-between;align-items:flex-start;gap:20px;margin-bottom:40px;">
    <div class="eyebrow" style="font-size:16px;">PurePrep / kitchen utility</div>
    ${creditPill(18)}
  </div>
  <div style="font-size:78px;font-weight:800;line-height:1.0;letter-spacing:-.03em;">Your kitchen,<br>focused.</div>
  <div style="margin:26px 0 40px;">${unitToggle('metric')}</div>
  <div style="color:${T.muted};font-size:23px;line-height:1.6;max-width:760px;margin-bottom:44px;">Recipe pages are noisy. PurePrep keeps the useful part — ingredients, steps, and nothing that gets in the way.</div>
  ${importBar()}
  <div style="display:grid;grid-template-columns:1fr 1fr;gap:18px;margin-top:44px;">
    ${card('Mushroom Risotto','14 steps · 18 ingredients')}
    ${card('Air-fryer Arancini','4 steps · 10 ingredients')}
  </div>
</div>`);

const p2 = doc(PW, PH, `
<div style="${phonePad}">
  ${caption('Your recipes','Saved. Sorted.<br>Ready to cook.')}
  <div style="display:grid;grid-template-columns:1fr;gap:20px;">
    ${card('Mushroom Risotto','14 steps · 18 ingredients')}
    ${card('Air-fryer Arancini','4 steps · 10 ingredients')}
    ${card('Basic Risotto','9 steps · 8 ingredients')}
    ${card('Truffle Tagliatelle','7 steps · 11 ingredients')}
  </div>
</div>`);

const ingRow = (t) => `<li style="padding:0 0 16px 0;">${t}</li>`;
const stepRow = (n, t) => `<li style="position:relative;padding:0 0 22px 66px;">
  <span style="position:absolute;left:0;top:0;width:42px;height:42px;display:flex;align-items:center;justify-content:center;background:#26331f;color:${T.lime};border:1px solid #3a4d31;font:600 18px 'DM Mono',monospace;">${n}</span>${t}</li>`;

const p3 = doc(PW, PH, `
<div style="${phonePad}">
  ${caption('Only the good part','Ingredients<br>and steps. Full stop.')}
  <div style="font-size:40px;font-weight:700;margin-bottom:6px;">Mushroom Risotto</div>
  <div class="mono eyebrow" style="margin:30px 0 12px;">Ingredients</div>
  <ul style="list-style:none;margin:0;padding:0;color:#d7e2cf;font-size:23px;line-height:1.5;">
    ${ingRow('500 g mushrooms, sliced 3 mm thick')}${ingRow('1¼ cups arborio rice')}${ingRow('5 cups warm chicken stock')}${ingRow('½ cup parmesan, finely grated')}
  </ul>
  <div class="mono eyebrow" style="margin:28px 0 14px;">Method</div>
  <ol style="list-style:none;margin:0;padding:0;color:#d7e2cf;font-size:23px;line-height:1.45;">
    ${stepRow(1,'Sauté mushrooms in butter until golden, then set aside.')}${stepRow(2,'Soften shallots and garlic; add rice and toast one minute.')}${stepRow(3,'Add stock a ladle at a time until creamy and just cooked.')}
  </ol>
</div>`);

const p4 = doc(PW, PH, `
<div style="${phonePad}">
  ${caption('Metric ⇄ Imperial','Convert units<br>in one tap.')}
  <div style="display:flex;gap:16px;margin-bottom:44px;">${unitToggle('metric')}${unitToggle('imperial')}</div>
  <div style="display:grid;grid-template-columns:1fr 1fr;gap:18px;">
    <div style="background:${T.panel};border:1px solid #263426;padding:26px;">
      <div class="mono eyebrow" style="margin-bottom:18px;">Metric</div>
      <ul style="list-style:none;margin:0;padding:0;color:#d7e2cf;font-size:22px;line-height:2.1;">
        <li>500 g mushrooms</li><li>250 g mushrooms</li><li>44 ml olive oil</li><li>1 tsp salt</li></ul>
    </div>
    <div style="background:${T.panel};border:1px solid #263426;padding:26px;">
      <div class="mono eyebrow" style="margin-bottom:18px;">Imperial</div>
      <ul style="list-style:none;margin:0;padding:0;color:#d7e2cf;font-size:22px;line-height:2.1;">
        <li>1.1 lb mushrooms</li><li>8¾ oz mushrooms</li><li>3 tbsp olive oil</li><li>1 tsp salt</li></ul>
    </div>
  </div>
  <div style="color:${T.muted};font-size:21px;line-height:1.6;margin-top:40px;">PurePrep detects the recipe's units on import and converts everything back and forth — amounts stay accurate.</div>
</div>`);

const p5 = doc(PW, PH, `
<div style="${phonePad}">
  ${caption('Smart Import','AI untangles<br>messy recipes.')}
  <div style="display:flex;justify-content:flex-end;margin-bottom:26px;">${creditPill(18)}</div>
  <div style="background:#141b13;border:1px solid #33422d;padding:24px;margin-bottom:22px;">
    <div class="mono" style="color:${T.orange};font-size:15px;letter-spacing:.12em;margin-bottom:14px;">MESSY PAGE</div>
    <div style="color:#7f8b77;font-size:20px;line-height:1.5;">▢500g (1 lb) mushrooms , sliced 3 mm / 1/8" thick (Note 1)▢250g (1/2 lb) mushrooms , quartered (Note 1)▢3 tbsp butter▢2 tbsp olive oil…</div>
  </div>
  <div style="display:flex;justify-content:center;color:${T.lime};font-size:34px;margin:6px 0 22px;">↓</div>
  <div style="background:${T.panel};border:1px solid #3a4d31;padding:26px;">
    <div class="mono eyebrow" style="margin-bottom:16px;">Clean recipe</div>
    <ul style="list-style:none;margin:0;padding:0;color:#d7e2cf;font-size:23px;line-height:1.9;">
      <li>500 g mushrooms, sliced 3 mm thick</li><li>250 g mushrooms, quartered</li><li>3 tbsp butter</li><li>2 tbsp olive oil</li></ul>
  </div>
</div>`);

// ---------- Tablet body (scales via % + clamp), rendered at two sizes ----------
const tabletHome = (w, h) => doc(w, h, `
<div style="height:${h}px;padding:6% 6%;display:flex;flex-direction:column;justify-content:center;gap:5%;">
  <div style="display:flex;justify-content:space-between;align-items:flex-start;">
    <div class="eyebrow" style="font-size:clamp(14px,1.4vw,20px);">PurePrep / kitchen utility</div>
    <div style="display:flex;gap:1.2vw;align-items:center;">${unitToggle('metric')}${creditPill(18)}</div>
  </div>
  <div style="display:grid;grid-template-columns:1.05fr .95fr;gap:5%;align-items:center;">
    <div>
      <div style="font-size:clamp(52px,7vw,96px);font-weight:800;line-height:.98;letter-spacing:-.03em;">Your kitchen,<br>focused.</div>
      <div style="color:${T.muted};font-size:clamp(18px,2vw,28px);line-height:1.6;margin:4% 0 6%;max-width:640px;">Paste a recipe link. Keep the ingredients and steps. Lose the ads, pop-ups and life stories.</div>
      ${importBar()}
    </div>
    <div style="display:grid;grid-template-columns:1fr 1fr;gap:2.2%;">
      ${card('Mushroom Risotto','14 steps · 18 ingredients')}
      ${card('Air-fryer Arancini','4 steps · 10 ingredients')}
      ${card('Basic Risotto','9 steps · 8 ingredients')}
      ${card('Truffle Tagliatelle','7 steps · 11 ingredients')}
    </div>
  </div>
</div>`);

const tabletDetail = (w, h) => doc(w, h, `
<div style="height:${h}px;padding:6% 6%;display:flex;flex-direction:column;justify-content:center;">
  <div class="eyebrow" style="font-size:clamp(14px,1.4vw,20px);margin-bottom:2%;">Only the good part</div>
  <div style="font-size:clamp(46px,6vw,86px);font-weight:800;letter-spacing:-.03em;margin-bottom:5%;">Mushroom Risotto</div>
  <div style="display:grid;grid-template-columns:1fr 1.15fr;gap:6%;">
    <div>
      <div class="mono eyebrow" style="margin-bottom:3%;">Ingredients</div>
      <ul style="list-style:none;margin:0;padding:0;color:#d7e2cf;font-size:clamp(18px,1.9vw,26px);line-height:2.0;">
        <li>500 g mushrooms, sliced 3 mm thick</li><li>250 g mushrooms, quartered</li><li>1¼ cups arborio rice</li><li>5 cups warm chicken stock</li><li>½ cup dry white wine</li><li>½ cup parmesan, finely grated</li><li>3 tbsp butter · 2 tbsp olive oil</li></ul>
    </div>
    <div>
      <div class="mono eyebrow" style="margin-bottom:3%;">Method</div>
      <ol style="list-style:none;margin:0;padding:0;color:#d7e2cf;font-size:clamp(18px,1.9vw,26px);line-height:1.5;">
        ${stepRow(1,'Sauté mushrooms in butter until golden; set aside.')}${stepRow(2,'Soften shallots and garlic, then toast the rice.')}${stepRow(3,'Deglaze with wine and let it evaporate.')}${stepRow(4,'Add stock a ladle at a time until creamy and just cooked.')}${stepRow(5,'Stir through parmesan and the mushrooms; serve.')}</ol>
    </div>
  </div>
</div>`);

// ---------- Render everything ----------
const jobs = [
  ['icon.html', iconHtml, 512, 512, join(ROOT, 'icon-512.png')],
  ['feature.html', feature, 1024, 500, join(ROOT, 'feature-1024x500.png')],
  ['p1.html', p1, PW, PH, join(ROOT, 'phone', 'phone-1-home.png')],
  ['p2.html', p2, PW, PH, join(ROOT, 'phone', 'phone-2-library.png')],
  ['p3.html', p3, PW, PH, join(ROOT, 'phone', 'phone-3-recipe.png')],
  ['p4.html', p4, PW, PH, join(ROOT, 'phone', 'phone-4-units.png')],
  ['p5.html', p5, PW, PH, join(ROOT, 'phone', 'phone-5-ai.png')],
  ['t7a.html', tabletHome(1200, 1920), 1200, 1920, join(ROOT, 'tablet7', 'tablet7-1-home.png')],
  ['t7b.html', tabletDetail(1200, 1920), 1200, 1920, join(ROOT, 'tablet7', 'tablet7-2-recipe.png')],
  ['t10a.html', tabletHome(1600, 2560), 1600, 2560, join(ROOT, 'tablet10', 'tablet10-1-home.png')],
  ['t10b.html', tabletDetail(1600, 2560), 1600, 2560, join(ROOT, 'tablet10', 'tablet10-2-recipe.png')],
];

for (const [name, html, w, h, out] of jobs) {
  const f = join(HTML, name);
  writeFileSync(f, html);
  const r = spawnSync(CHROME, [
    '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-sandbox',
    '--force-device-scale-factor=1', `--window-size=${w},${h}`,
    '--virtual-time-budget=6000', `--screenshot=${out}`, `file://${f}`,
  ], { encoding: 'utf8' });
  console.log(`${r.status === 0 ? 'OK ' : 'ERR'} ${out} (${w}x${h})`);
  if (r.status !== 0) console.error(r.stderr?.slice(-400));
}
console.log('done');
