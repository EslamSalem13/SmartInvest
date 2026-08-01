import { Component, inject } from '@angular/core';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-dashboard',
  template: `
    <div class="page">
      <header class="si-page-head">
        <div>
          <h1>لوحة التحكم</h1>
          <p class="sub">مرحبًا، {{ auth.user()?.fullName }} 👋</p>
        </div>
      </header>

      <div class="cards">
        <div class="kpi">
          <div class="kpi-top">
            <span class="lab">إجمالي المشروعات</span>
            <span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><rect x="3" y="3" width="7" height="9" rx="1.5"/><rect x="14" y="3" width="7" height="5" rx="1.5"/><rect x="14" y="12" width="7" height="9" rx="1.5"/><rect x="3" y="16" width="7" height="5" rx="1.5"/></svg></span>
          </div>
          <b class="val">—</b>
        </div>
        <div class="kpi ok">
          <div class="kpi-top">
            <span class="lab">المعتمدة</span>
            <span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M20 6 9 17l-5-5"/></svg></span>
          </div>
          <b class="val">—</b>
        </div>
        <div class="kpi warn">
          <div class="kpi-top">
            <span class="lab">قيد المراجعة</span>
            <span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 3"/></svg></span>
          </div>
          <b class="val">—</b>
        </div>
        <div class="kpi gold">
          <div class="kpi-top">
            <span class="lab">إجمالي التمويل</span>
            <span class="ic"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8"><path d="M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg></span>
          </div>
          <b class="val">—</b>
        </div>
      </div>

      <div class="placeholder si-card">
        <div class="ph-icon">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M3 3v18h18M7 15v3M12 10v8M17 6v12"/></svg>
        </div>
        <h3>لوحة تحكم مدير التخطيط</h3>
        <p>ملخص المؤشرات والإحصائيات — قيد الربط بالـ API.</p>
      </div>
    </div>
  `,
  styles: [
    `
    .page { padding: 28px 32px; }
    .cards { display: grid; grid-template-columns: repeat(4, 1fr); gap: 16px; margin-bottom: 24px; }
    @media (max-width: 720px) { .cards { grid-template-columns: repeat(2, 1fr); } }
    .kpi {
      background: linear-gradient(175deg, var(--surface) 0%, var(--surface-2) 145%);
      border: 1px solid var(--line); border-radius: var(--radius-lg);
      padding: 18px 20px; box-shadow: var(--shadow-sm); position: relative; overflow: hidden;
      transition: box-shadow .25s var(--ease), transform .25s var(--ease), border-color .25s var(--ease);
    }
    .kpi:hover { box-shadow: var(--shadow-md); transform: translateY(-3px); border-color: var(--green-500); }
    .kpi::before {
      content: ""; position: absolute; top: -45%; inset-inline-end: -18%;
      width: 140px; height: 140px; border-radius: 50%;
      background: radial-gradient(circle, var(--green-100), transparent 72%);
      opacity: .9; pointer-events: none;
    }
    .kpi.ok::before { background: radial-gradient(circle, var(--ok-bg), transparent 72%); }
    .kpi.warn::before { background: radial-gradient(circle, var(--warn-bg), transparent 72%); }
    .kpi.gold::before { background: radial-gradient(circle, var(--gold-100), transparent 72%); }
    .kpi-top { position: relative; z-index: 1; display: flex; align-items: flex-start; justify-content: space-between; }
    .lab { color: var(--muted); font-size: var(--text-sm); font-weight: 700; }
    .ic { width: 32px; height: 32px; border-radius: 9px; display: grid; place-items: center; background: var(--green-100); color: var(--green-700); flex-shrink: 0; }
    .ic svg { width: 16px; height: 16px; }
    .kpi.ok .ic { background: var(--ok-bg); color: var(--ok); }
    .kpi.warn .ic { background: var(--warn-bg); color: var(--warn); }
    .kpi.gold .ic { background: var(--gold-100); color: var(--gold-700); }
    .val { position: relative; z-index: 1; display: block; font-size: var(--text-3xl); font-weight: 800; margin-top: 10px; font-family: var(--font-heading); color: var(--green-900); }
    .placeholder { padding: 48px; text-align: center; }
    .ph-icon { width: 56px; height: 56px; margin: 0 auto; border-radius: 16px; background: var(--surface-2); color: var(--muted-2); display: grid; place-items: center; }
    .ph-icon svg { width: 26px; height: 26px; }
    .placeholder h3 { margin: 16px 0 6px; color: var(--green-900); }
    .placeholder p { color: var(--muted); margin: 0; font-size: var(--text-sm); }
    `,
  ],
})
export class Dashboard {
  protected readonly auth = inject(AuthService);
}
