import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Roles, UserProfile } from '../../core/models/auth.models';
import { AuthService } from '../../core/services/auth.service';
import { ToastService } from '../../core/services/toast.service';

const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const ALLOWED_AVATAR_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

@Component({
  selector: 'app-profile',
  imports: [FormsModule, DatePipe],
  templateUrl: './profile.html',
  styleUrl: './profile.css',
})
export class Profile {
  private readonly auth = inject(AuthService);
  private readonly toast = inject(ToastService);

  protected readonly profile = signal<UserProfile | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly avatarBusy = signal(false);
  protected readonly passwordBusy = signal(false);
  protected readonly resetBusy = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly avatarUrl = this.auth.avatarUrl;

  protected readonly fullName = signal('');
  protected readonly email = signal('');
  protected readonly phoneNumber = signal('');
  protected readonly currentPassword = signal('');
  protected readonly newPassword = signal('');
  protected readonly confirmPassword = signal('');

  protected readonly initial = computed(
    () =>
      this.profile()?.fullName.trim().charAt(0) ||
      this.auth.user()?.fullName.trim().charAt(0) ||
      '؟',
  );

  protected readonly roleLabel = computed(() => {
    switch (this.profile()?.role) {
      case Roles.SuperAdmin:
        return 'سوبر أدمن';
      case Roles.PlanningManager:
        return 'مدير التخطيط';
      default:
        return 'موظف تخطيط';
    }
  });

  constructor() {
    this.loadProfile();
  }

  protected saveProfile(): void {
    if (this.saving()) {
      return;
    }

    const fullName = this.fullName().trim();
    const email = this.email().trim();
    if (fullName.length < 3) {
      this.toast.error('الاسم يجب أن يكون 3 أحرف على الأقل');
      return;
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.toast.error('برجاء إدخال بريد إلكتروني صحيح');
      return;
    }

    this.saving.set(true);
    this.auth
      .updateProfile({
        fullName,
        email,
        phoneNumber: this.phoneNumber().trim() || null,
      })
      .subscribe({
        next: (profile) => {
          this.saving.set(false);
          this.profile.set(profile);
          this.setForm(profile);
          this.toast.success('تم تحديث بيانات الحساب');
        },
        error: (error) => {
          this.saving.set(false);
          this.toast.error(error?.error?.message ?? 'تعذّر تحديث بيانات الحساب');
        },
      });
  }

  protected onAvatarSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.avatarBusy()) {
      return;
    }
    if (!ALLOWED_AVATAR_TYPES.includes(file.type)) {
      this.toast.error('صيغة الصورة غير مدعومة. استخدم PNG أو JPG أو WEBP');
      return;
    }
    if (file.size > MAX_AVATAR_BYTES) {
      this.toast.error('حجم الصورة يجب ألا يتجاوز 2 ميجابايت');
      return;
    }

    this.avatarBusy.set(true);
    this.auth.uploadAvatar(file).subscribe({
      next: () => {
        this.avatarBusy.set(false);
        this.profile.update((profile) => (profile ? { ...profile, hasAvatar: true } : profile));
        this.toast.success('تم تحديث الصورة الشخصية');
      },
      error: (error) => {
        this.avatarBusy.set(false);
        this.toast.error(error?.error?.message ?? 'تعذّر رفع الصورة');
      },
    });
  }

  protected deleteAvatar(): void {
    if (this.avatarBusy() || !this.profile()?.hasAvatar) {
      return;
    }
    if (!window.confirm('هل تريد حذف الصورة الشخصية؟')) {
      return;
    }

    this.avatarBusy.set(true);
    this.auth.deleteAvatar().subscribe({
      next: () => {
        this.avatarBusy.set(false);
        this.profile.update((profile) => (profile ? { ...profile, hasAvatar: false } : profile));
        this.toast.success('تم حذف الصورة الشخصية');
      },
      error: (error) => {
        this.avatarBusy.set(false);
        this.toast.error(error?.error?.message ?? 'تعذّر حذف الصورة');
      },
    });
  }

  protected changePassword(): void {
    if (this.passwordBusy()) {
      return;
    }
    if (!this.currentPassword() || this.newPassword().length < 6) {
      this.toast.error('أدخل كلمة المرور الحالية وكلمة جديدة من 6 أحرف على الأقل');
      return;
    }
    if (this.newPassword() !== this.confirmPassword()) {
      this.toast.error('تأكيد كلمة المرور الجديدة غير مطابق');
      return;
    }
    if (this.currentPassword() === this.newPassword()) {
      this.toast.error('كلمة المرور الجديدة يجب أن تختلف عن الحالية');
      return;
    }

    this.passwordBusy.set(true);
    this.auth
      .changePassword({
        currentPassword: this.currentPassword(),
        newPassword: this.newPassword(),
      })
      .subscribe({
        next: () => {
          this.passwordBusy.set(false);
          this.currentPassword.set('');
          this.newPassword.set('');
          this.confirmPassword.set('');
          this.toast.success('تم تغيير كلمة المرور بنجاح');
        },
        error: (error) => {
          this.passwordBusy.set(false);
          this.toast.error(error?.error?.message ?? 'تعذّر تغيير كلمة المرور');
        },
      });
  }

  protected sendPasswordReset(): void {
    const email = this.profile()?.email;
    if (!email || this.resetBusy()) {
      return;
    }
    this.resetBusy.set(true);
    this.auth.forgotPassword(email).subscribe({
      next: () => {
        this.resetBusy.set(false);
        this.toast.success('تم إرسال رابط إعادة تعيين كلمة المرور إلى بريدك');
      },
      error: (error) => {
        this.resetBusy.set(false);
        this.toast.error(error?.error?.message ?? 'تعذّر إرسال رابط إعادة التعيين');
      },
    });
  }

  private loadProfile(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.auth.getProfile().subscribe({
      next: (profile) => {
        this.loading.set(false);
        this.profile.set(profile);
        this.setForm(profile);
      },
      error: (error) => {
        this.loading.set(false);
        this.loadError.set(error?.error?.message ?? 'تعذّر تحميل بيانات الحساب');
      },
    });
  }

  private setForm(profile: UserProfile): void {
    this.fullName.set(profile.fullName);
    this.email.set(profile.email);
    this.phoneNumber.set(profile.phoneNumber ?? '');
  }
}
