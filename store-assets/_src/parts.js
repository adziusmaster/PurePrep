import { doc, T } from './theme.js';

// Reusable UI fragments styled like the real app.
export const unitToggle = (active = 'metric') => `
<div style="display:inline-flex;border:1px solid ${T.line};overflow:hidden;border-radius:2px;">
  <div class="mono" style="padding:12px 20px;font-weight:600;font-size:15px;letter-spacing:.08em;background:${active==='metric'?T.lime:'transparent'};color:${active==='metric'?T.limeDark:T.muted};">Metric</div>
  <div class="mono" style="padding:12px 20px;font-weight:600;font-size:15px;letter-spacing:.08em;background:${active==='imperial'?T.lime:'transparent'};color:${active==='imperial'?T.limeDark:T.muted};">Imperial</div>
</div>`;

export const creditPill = (n = 18) => `
<div class="mono" style="border:1px solid ${T.line};padding:13px 17px;color:${T.lime};background:rgba(32,43,30,.75);font-size:15px;white-space:nowrap;">${n} smart credits left</div>`;

export const importBar = () => `
<div style="display:flex;align-items:center;border-bottom:1px solid #779b50;padding-bottom:16px;gap:16px;">
  <div style="flex:1;color:#8fa07f;font-size:22px;">Paste a recipe URL</div>
  <div style="background:${T.lime};color:${T.limeDark};font-weight:800;padding:15px 26px;font-size:19px;border-radius:2px;">Import</div>
</div>`;

export const card = (title, meta) => `
<div style="background:${T.panel};border:1px solid #263426;padding:24px;display:flex;flex-direction:column;justify-content:space-between;min-height:200px;border-radius:2px;">
  <div class="mono" style="color:${T.lime};font-size:12px;letter-spacing:.14em;">READY TO COOK</div>
  <div>
    <div style="font-size:24px;font-weight:700;line-height:1.15;margin-bottom:10px;">${title}</div>
    <div style="color:${T.muted};font-size:15px;">${meta}</div>
  </div>
</div>`;

export const caption = (eyebrow, head) => `
<div style="margin-bottom:44px;">
  <div class="eyebrow">${eyebrow}</div>
  <div style="font-size:56px;font-weight:800;line-height:1.03;letter-spacing:-.03em;margin-top:14px;max-width:900px;">${head}</div>
</div>`;
