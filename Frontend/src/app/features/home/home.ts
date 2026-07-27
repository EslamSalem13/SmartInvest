import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-home',
  imports: [FormsModule],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly particles = Array.from({ length: 18 }, (_, i) => i);
  protected readonly gearTeeth = Array.from({ length: 8 }, (_, i) => i * 45);

  protected readonly usernameOrEmail = signal('');
  protected readonly password = signal('');
  protected readonly showPassword = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly takingFlight = signal(false);

  private readonly reducedMotion =
    typeof window !== 'undefined' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  protected submit(): void {
    if (this.loading()) {
      return;
    }
    this.error.set(null);

    if (!this.usernameOrEmail().trim() || !this.password()) {
      this.error.set('برجاء إدخال البريد/اسم المستخدم وكلمة المرور');
      return;
    }

    this.loading.set(true);
    this.auth
      .login({ usernameOrEmail: this.usernameOrEmail().trim(), password: this.password() })
      .subscribe({
        next: (result) => {
          this.loading.set(false);
          const target = this.auth.homeRouteForRole(result.role);

          if (this.reducedMotion) {
            this.router.navigateByUrl(target);
            return;
          }

          this.takingFlight.set(true);
          setTimeout(() => this.router.navigateByUrl(target), 950);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(err?.error?.message ?? 'تعذّر تسجيل الدخول، تأكد من البيانات');
        },
      });
  }
}
