import { Component, inject } from '@angular/core';
import { ThemeService } from '../core/services/theme.service';

@Component({
  selector: 'app-theme-toggle',
  template: `
    <button
      type="button"
      class="theme-toggle"
      (click)="theme.toggle()"
      [attr.aria-pressed]="theme.isDark()"
      [attr.aria-label]="theme.isDark() ? 'تفعيل الوضع الفاتح' : 'تفعيل الوضع الداكن'"
      [attr.title]="theme.isDark() ? 'الوضع الفاتح' : 'الوضع الداكن'"
    >
      @if (theme.isDark()) {
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2v2M12 20v2M4.93 4.93l1.42 1.42M17.65 17.65l1.42 1.42M2 12h2M20 12h2M4.93 19.07l1.42-1.42M17.65 6.35l1.42-1.42" />
        </svg>
        <span>الوضع الفاتح</span>
      } @else {
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
          <path d="M20.5 15.25A8.5 8.5 0 0 1 8.75 3.5 8.5 8.5 0 1 0 20.5 15.25Z" />
        </svg>
        <span>الوضع الداكن</span>
      }
    </button>
  `,
  styles: [`
    :host { display: block; min-width: 0; }
    .theme-toggle {
      width: 100%; min-height: 42px; padding: 9px 12px;
      display: flex; align-items: center; justify-content: center; gap: 9px;
      border: 1px solid color-mix(in srgb, var(--gold) 36%, var(--line));
      border-radius: 11px;
      background: color-mix(in srgb, var(--surface-2) 88%, transparent);
      color: var(--ink); font: 700 13px var(--font); white-space: nowrap;
      box-shadow: var(--shadow-xs);
      transition: transform .15s var(--ease), background .15s var(--ease), border-color .15s var(--ease), color .15s var(--ease);
    }
    .theme-toggle:hover { transform: translateY(-1px); border-color: var(--gold); background: var(--surface-3); }
    .theme-toggle svg { width: 18px; height: 18px; flex: 0 0 auto; color: var(--gold); }
    @media (max-width: 900px) { .theme-toggle { min-height: 46px; } }
  `],
})
export class ThemeToggle {
  protected readonly theme = inject(ThemeService);
}
