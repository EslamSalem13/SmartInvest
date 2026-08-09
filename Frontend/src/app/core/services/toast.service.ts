import { Injectable, signal } from '@angular/core';

export interface Toast {
  id: number;
  text: string;
  kind: 'success' | 'error';
}

/**
 * رسائل تأكيد/خطأ عابرة على مستوى التطبيق. سبب وجودها: كل عمليات الحفظ الناجحة كانت
 * صامتة تمامًا، فبدت للمستخدم وكأن الصفحة "تُحدَّث فقط" دون أن يحدث شيء.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;

  readonly toasts = signal<Toast[]>([]);

  success(text: string): void {
    this.push(text, 'success');
  }

  error(text: string): void {
    this.push(text, 'error');
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private push(text: string, kind: Toast['kind']): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list, { id, text, kind }]);
    // الأخطاء تبقى أطول — المستخدم يحتاج وقتًا لقراءتها
    setTimeout(() => this.dismiss(id), kind === 'error' ? 6000 : 3500);
  }
}
