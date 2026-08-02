import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';

@Component({
  selector: 'app-settings',
  imports: [RouterLink, RouterOutlet],
  templateUrl: './settings.html',
  styleUrl: './settings.css',
})
export class Settings {
  private readonly router = inject(Router);

  /** true عند صفحة قائمة الإعدادات الرئيسية (البطاقات)، false عند الدخول داخل إعداد معيّن */
  protected readonly isIndex = toSignal(
    this.router.events.pipe(
      filter((e): e is NavigationEnd => e instanceof NavigationEnd),
      map((e) => this.checkIsIndex(e.urlAfterRedirects)),
    ),
    { initialValue: this.checkIsIndex(this.router.url) },
  );

  private checkIsIndex(url: string): boolean {
    const path = url.split('?')[0].split('#')[0];
    return path === '/app/settings' || path === '/app/settings/';
  }
}
