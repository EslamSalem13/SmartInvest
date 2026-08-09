import { Component, inject } from '@angular/core';
import { ToastService } from '../core/services/toast.service';

@Component({
  selector: 'app-toast-host',
  template: `
    <div class="toast-stack">
      @for (t of toast.toasts(); track t.id) {
        <div class="toast" [class.err]="t.kind === 'error'" (click)="toast.dismiss(t.id)">
          <span class="ico">{{ t.kind === 'error' ? '⚠' : '✓' }}</span>
          <span class="txt">{{ t.text }}</span>
        </div>
      }
    </div>
  `,
  styles: [`
    .toast-stack {
      position: fixed;
      bottom: 24px;
      inset-inline-start: 24px;
      z-index: 9999;
      display: flex;
      flex-direction: column;
      gap: 10px;
      pointer-events: none;
    }
    .toast {
      pointer-events: auto;
      display: flex;
      align-items: center;
      gap: 10px;
      min-width: 260px;
      max-width: 420px;
      padding: 13px 16px;
      border-radius: 10px;
      font-size: 13.5px;
      font-weight: 700;
      color: #fff;
      background: linear-gradient(155deg, #2f7d4f, #1f5c39);
      box-shadow: 0 8px 24px rgba(0, 0, 0, .22);
      cursor: pointer;
      animation: toast-in .22s ease-out;
    }
    .toast.err { background: linear-gradient(155deg, #c0392b, #96271c); }
    .toast .ico { font-size: 15px; flex-shrink: 0; }
    .toast .txt { line-height: 1.5; }
    @keyframes toast-in {
      from { opacity: 0; transform: translateY(10px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `],
})
export class ToastHost {
  protected readonly toast = inject(ToastService);
}
