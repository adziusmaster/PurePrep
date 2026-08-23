// Shared theme + HTML frame for PurePrep Play Store assets.
export const T = {
  bg: '#0c100d', ink: '#f4f1e8', muted: '#8e9a8b', line: '#30402d',
  panel: '#182018', panel2: '#202b1e', lime: '#b7df78', limeDark: '#172313', orange: '#ee9b5a',
};

export const BASE_CSS = `
@import url('https://fonts.googleapis.com/css2?family=DM+Mono:wght@400;500&family=Manrope:wght@400;600;700;800&display=swap');
:root{--ink:${T.ink};--muted:${T.muted};--line:${T.line};--panel:${T.panel};--panel2:${T.panel2};--lime:${T.lime};--lime-dark:${T.limeDark};--orange:${T.orange};}
*{box-sizing:border-box;-webkit-font-smoothing:antialiased;}
html,body{margin:0;padding:0;}
body{background:${T.bg};color:var(--ink);font-family:Manrope,sans-serif;overflow:hidden;}
.grid-bg:before{content:"";position:fixed;inset:0;pointer-events:none;opacity:.16;
  background-image:linear-gradient(rgba(183,223,120,.06) 1px,transparent 1px),linear-gradient(90deg,rgba(183,223,120,.06) 1px,transparent 1px);
  background-size:56px 56px;mask-image:linear-gradient(to bottom,black,transparent 82%);}
.eyebrow{font:500 clamp(11px,1.5vw,15px) 'DM Mono',monospace;letter-spacing:.18em;text-transform:uppercase;color:var(--lime);}
.mono{font-family:'DM Mono',monospace;}
`;

export const doc = (w, h, body, css = '') => `<!doctype html><html lang="en"><head><meta charset="utf-8">
<style>${BASE_CSS}
html,body{width:${w}px;height:${h}px;}
${css}
</style></head><body class="grid-bg">${body}</body></html>`;
