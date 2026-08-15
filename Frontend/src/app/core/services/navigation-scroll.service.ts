import { DestroyRef, Injectable, inject } from '@angular/core';
import { NavigationEnd, NavigationStart, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

/** يحفظ موضع القوائم خلال جلسة التطبيق ويستعيدها بعد الرجوع من صفحات التفاصيل. */
@Injectable({ providedIn: 'root' })
export class NavigationScrollService {
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly positions = new Map<string, number>();
  private currentUrl = this.routeKey(this.router.url);
  private restoreVersion = 0;

  constructor() {
    if (typeof window === 'undefined') return;

    this.router.events
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((event) => {
        if (event instanceof NavigationStart) {
          this.positions.set(this.currentUrl, window.scrollY);
          this.restoreVersion++;
          return;
        }

        if (event instanceof NavigationEnd) {
          this.currentUrl = this.routeKey(event.urlAfterRedirects);
          this.restore(this.positions.get(this.currentUrl) ?? 0);
        }
      });
  }

  private restore(target: number): void {
    const version = ++this.restoreVersion;
    let attempts = 0;

    const tryRestore = () => {
      if (version !== this.restoreVersion) return;

      window.scrollTo({ top: target, behavior: 'auto' });
      const reachedTarget = Math.abs(window.scrollY - target) <= 2;

      // القوائم تُحمّل بياناتها بعد NavigationEnd؛ ننتظر حتى يصبح طول الصفحة كافيًا.
      if (!reachedTarget && attempts < 30) {
        attempts++;
        window.setTimeout(tryRestore, 60);
      }
    };

    window.requestAnimationFrame(tryRestore);
  }

  private routeKey(url: string): string {
    return url.split('#')[0] || '/';
  }
}
