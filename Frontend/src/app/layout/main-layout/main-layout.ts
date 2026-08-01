import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { Roles } from '../../core/models/auth.models';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  managerOnly: boolean;
}

const SIDEBAR_COLLAPSED_KEY = 'smartinvest_sidebar_collapsed';
const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const ALLOWED_AVATAR_TYPES = ['image/png', 'image/jpeg', 'image/webp'];

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.css',
})
export class MainLayout {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly user = this.auth.user;
  protected readonly isManager = this.auth.isManager;
  protected readonly avatarUrl = this.auth.avatarUrl;

  protected readonly roleLabel = computed(() => {
    switch (this.auth.role()) {
      case Roles.SuperAdmin:
        return 'سوبر أدمن';
      case Roles.PlanningManager:
        return 'مدير التخطيط';
      default:
        return 'موظف تخطيط';
    }
  });

  protected readonly initial = computed(() => this.user()?.fullName?.trim()?.charAt(0) ?? '؟');

  protected readonly sidebarCollapsed = signal(localStorage.getItem(SIDEBAR_COLLAPSED_KEY) === 'true');
  protected readonly mobileNavOpen = signal(false);

  protected readonly avatarUploading = signal(false);
  protected readonly avatarError = signal<string | null>(null);

  protected toggleSidebar(): void {
    const next = !this.sidebarCollapsed();
    this.sidebarCollapsed.set(next);
    localStorage.setItem(SIDEBAR_COLLAPSED_KEY, String(next));
  }

  protected toggleMobileNav(): void {
    this.mobileNavOpen.set(!this.mobileNavOpen());
  }

  protected closeMobileNav(): void {
    this.mobileNavOpen.set(false);
  }

  protected onAvatarFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    this.avatarError.set(null);

    if (!ALLOWED_AVATAR_TYPES.includes(file.type)) {
      this.avatarError.set('صيغة الصورة غير مدعومة، برجاء اختيار PNG أو JPG أو WEBP');
      return;
    }
    if (file.size > MAX_AVATAR_BYTES) {
      this.avatarError.set('حجم الصورة يجب ألا يتجاوز 2 ميجابايت');
      return;
    }

    this.avatarUploading.set(true);
    this.auth.uploadAvatar(file).subscribe({
      next: () => this.avatarUploading.set(false),
      error: () => {
        this.avatarUploading.set(false);
        this.avatarError.set('تعذّر رفع الصورة، حاول مرة أخرى');
      },
    });
  }

  private readonly allNav: NavItem[] = [
    { label: 'لوحة التحكم', route: '/app/dashboard', icon: 'M4 13h6V4H4v9Zm10 7h6v-9h-6v9ZM4 20h6v-4H4v4ZM14 4v5h6V4h-6Z', managerOnly: true },
    { label: 'المشروعات', route: '/app/projects', icon: 'M3 7h18M3 12h18M3 17h18', managerOnly: false },
    { label: 'الإدارة المالية', route: '/app/financial', icon: 'M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6', managerOnly: false },
    { label: 'الإعدادات', route: '/app/settings', icon: 'M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6Zm7.4-3a7.4 7.4 0 0 1-.1 1.2l2.1 1.6-2 3.5-2.5-1a7.6 7.6 0 0 1-2 1.2l-.4 2.7H9.5l-.4-2.7a7.6 7.6 0 0 1-2-1.2l-2.5 1-2-3.5 2.1-1.6a7.4 7.4 0 0 1 0-2.4L2.6 8.6l2-3.5 2.5 1a7.6 7.6 0 0 1 2-1.2L9.5 2.2h5l.4 2.7a7.6 7.6 0 0 1 2 1.2l2.5-1 2 3.5-2.1 1.6c.1.4.1.8.1 1.2Z', managerOnly: false },
  ];

  protected readonly nav = computed(() =>
    this.allNav.filter((item) => !item.managerOnly || this.isManager()),
  );

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
