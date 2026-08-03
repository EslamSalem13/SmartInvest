import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { Perm } from '../../core/models/permission.models';

interface NavItem {
  label: string;
  route: string;
  icon: string;
  /** الصلاحية المطلوبة لإظهار هذا العنصر في القائمة. */
  permission: string;
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
  protected readonly avatarUrl = this.auth.avatarUrl;

  protected readonly roleLabel = computed(() => this.auth.user()?.roleDisplayName ?? '');

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
    { label: 'لوحة التحكم', route: '/app/dashboard', icon: 'M4 13h6V4H4v9Zm10 7h6v-9h-6v9ZM4 20h6v-4H4v4ZM14 4v5h6V4h-6Z', permission: Perm.DashboardView },
    { label: 'المشروعات', route: '/app/projects', icon: 'M3 7h18M3 12h18M3 17h18', permission: Perm.ProjectsView },
    { label: 'الإدارة المالية', route: '/app/financial', icon: 'M12 1v22M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6', permission: Perm.FinancialView },
    { label: 'إدارة المستخدمين', route: '/app/users', icon: 'M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z', permission: Perm.UsersView },
    { label: 'الأدوار والصلاحيات', route: '/app/roles', icon: 'M12 2 4 6v6c0 5 3.4 8.6 8 10 4.6-1.4 8-5 8-10V6l-8-4Zm0 7v4m0 3h.01', permission: Perm.RolesManage },
    { label: 'المقاولون', route: '/app/contractors', icon: 'M3 21h18M5 21V7l7-4 7 4v14M9 21v-6h6v6', permission: Perm.ContractorsView },
    { label: 'الجهات التنفيذية', route: '/app/agencies', icon: 'M3 21h18M6 21V10l6-4 6 4v11M10 21v-5h4v5', permission: Perm.AgenciesView },
  ];

  protected readonly nav = computed(() =>
    this.allNav.filter((item) => this.auth.has(item.permission)),
  );

  protected logout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
