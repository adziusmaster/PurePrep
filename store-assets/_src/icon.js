import { doc, T } from './theme.js';

const mark = `
<svg width="300" height="300" viewBox="0 0 300 300" fill="none" xmlns="http://www.w3.org/2000/svg">
  <g stroke="${T.lime}" stroke-width="15" stroke-linecap="round" fill="none" opacity="0.95">
    <path d="M112 96 C 96 78, 128 66, 112 46"/>
    <path d="M150 100 C 134 78, 166 64, 150 40"/>
    <path d="M188 96 C 172 78, 204 66, 188 46"/>
  </g>
  <rect x="70" y="150" width="160" height="26" rx="13" fill="${T.lime}"/>
  <path d="M84 178 h132 a10 10 0 0 1 10 12 a76 76 0 0 1 -152 0 a10 10 0 0 1 10 -12 z" fill="${T.lime}"/>
  <path d="M150 250 a76 76 0 0 0 66 -60 h-132 a76 76 0 0 0 66 60 z" fill="${T.limeDark}" opacity="0.16"/>
</svg>`;

export const iconHtml = doc(512, 512, `
<div style="position:absolute;inset:0;background:radial-gradient(120% 120% at 30% 20%, #16210f 0%, ${T.bg} 60%);"></div>
<div style="position:absolute;inset:26px;border:2px solid rgba(183,223,120,.22);border-radius:96px;"></div>
<div style="position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:14px;">
  ${mark}
  <div class="mono" style="font-weight:500;font-size:34px;letter-spacing:.14em;color:${T.ink};">PUREPREP</div>
</div>`, `.grid-bg:before{opacity:.10;background-size:40px 40px;}`);
