import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { ThemeToggle } from '../../shared/theme-toggle';

@Component({
  selector: 'app-account-recovery',
  imports: [FormsModule, RouterLink, ThemeToggle],
  templateUrl: './account-recovery.html',
  styleUrl: './account-recovery.css',
})
export class AccountRecovery {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);

  protected readonly token = signal(this.route.snapshot.queryParamMap.get('token') ?? '');
  protected readonly email = signal(this.route.snapshot.queryParamMap.get('email') ?? '');
  protected readonly newPassword = signal('');
  protected readonly confirmPassword = signal('');
  protected readonly loading = signal(false);
  protected readonly success = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly resetMode = this.route.snapshot.routeConfig?.path === 'reset-password';

  protected submit(): void {
    if (this.loading()) {
      return;
    }

    this.error.set(null);
    const email = this.email().trim();
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.error.set('برجاء إدخال بريد إلكتروني صحيح');
      return;
    }

    if (!this.resetMode) {
      this.loading.set(true);
      this.auth.forgotPassword(email).subscribe({
        next: () => {
          this.loading.set(false);
          this.success.set(true);
        },
        error: (error) => {
          this.loading.set(false);
          this.error.set(error?.error?.message ?? 'تعذّر إرسال رابط إعادة التعيين');
        },
      });
      return;
    }

    if (!this.token()) {
      this.error.set('رابط إعادة تعيين كلمة المرور غير مكتمل');
      return;
    }
    if (this.newPassword().length < 6) {
      this.error.set('كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل');
      return;
    }
    if (this.newPassword() !== this.confirmPassword()) {
      this.error.set('تأكيد كلمة المرور غير مطابق');
      return;
    }

    this.loading.set(true);
    this.auth
      .resetPassword({ email, token: this.token(), newPassword: this.newPassword() })
      .subscribe({
        next: () => {
          this.loading.set(false);
          this.success.set(true);
        },
        error: (error) => {
          this.loading.set(false);
          this.error.set(error?.error?.message ?? 'تعذّر إعادة تعيين كلمة المرور');
        },
      });
  }
}
