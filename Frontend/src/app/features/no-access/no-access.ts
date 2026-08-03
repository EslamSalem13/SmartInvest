import { Component } from '@angular/core';

@Component({
  selector: 'app-no-access',
  template: `
    <div class="page">
      <div class="si-card box">
        <div class="ic">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6">
            <rect x="4" y="10" width="16" height="10" rx="2" /><path d="M8 10V7a4 4 0 0 1 8 0v3" />
          </svg>
        </div>
        <h2>لا توجد صلاحيات مُتاحة</h2>
        <p>حسابك لا يملك صلاحية الوصول لأي صفحة حتى الآن. برجاء التواصل مع مدير النظام لإسناد دور مناسب.</p>
      </div>
    </div>
  `,
  styles: [
    `
    .page { padding: 28px 32px; display: grid; place-items: center; min-height: 70dvh; }
    .box { max-width: 480px; padding: 44px 32px; text-align: center; }
    .ic {
      width: 62px; height: 62px; margin: 0 auto 18px; border-radius: 18px;
      display: grid; place-items: center;
      background: var(--warn-bg); color: var(--warn);
    }
    .ic svg { width: 28px; height: 28px; }
    h2 { color: var(--green-900); font-size: var(--text-xl); margin: 0 0 8px; }
    p { color: var(--muted); font-size: var(--text-sm); margin: 0; line-height: 1.8; }
    `,
  ],
})
export class NoAccess {}
