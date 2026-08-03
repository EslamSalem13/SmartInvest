import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResult, CurrentUser, LoginRequest, Roles } from '../models/auth.models';
import { Perm } from '../models/permission.models';

const TOKEN_KEY = 'smartinvest_token';
const USER_KEY = 'smartinvest_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly _user = signal<CurrentUser | null>(this.loadUser());
  private readonly _avatarUrl = signal<string | null>(null);

  readonly user = this._user.asReadonly();
  readonly avatarUrl = this._avatarUrl.asReadonly();
  readonly isAuthenticated = computed(() => this._user() !== null);
  readonly role = computed(() => this._user()?.role ?? null);
  readonly isSuperAdmin = computed(() => this._user()?.role === Roles.SuperAdmin);
  readonly permissions = computed(() => this._user()?.permissions ?? []);

  /** هل يملك المستخدم هذه الصلاحية؟ السوبر أدمن يملك كل شيء. */
  has(permission: string): boolean {
    return this.isSuperAdmin() || this.permissions().includes(permission);
  }

  hasAny(...permissions: string[]): boolean {
    return this.isSuperAdmin() || permissions.some((p) => this.permissions().includes(p));
  }

  constructor() {
    if (this._user()?.hasAvatar) {
      this.refreshAvatar();
    }
  }

  login(request: LoginRequest): Observable<AuthResult> {
    return this.http.post<AuthResult>(`${environment.apiUrl}/auth/login`, request).pipe(
      tap((result) => this.setSession(result)),
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this._user.set(null);
    this.clearAvatarUrl();
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  /** أول صفحة يستطيع المستخدم فتحها فعلًا حسب صلاحياته. */
  homeRoute(): string {
    const landing: [string, string][] = [
      [Perm.DashboardView, '/app/dashboard'],
      [Perm.ProjectsView, '/app/projects'],
      [Perm.FinancialView, '/app/financial'],
      [Perm.MemosView, '/app/financial/memos'],
      [Perm.PlansView, '/app/plans'],
      [Perm.ContractorsView, '/app/contractors'],
      [Perm.AgenciesView, '/app/agencies'],
      [Perm.UsersView, '/app/users'],
      [Perm.RolesManage, '/app/roles'],
    ];

    return landing.find(([permission]) => this.has(permission))?.[1] ?? '/app/no-access';
  }

  uploadAvatar(file: File): Observable<void> {
    const form = new FormData();
    form.append('file', file);
    return this.http.put<void>(`${environment.apiUrl}/auth/me/avatar`, form).pipe(
      tap(() => {
        const current = this._user();
        if (current) {
          const updated = { ...current, hasAvatar: true };
          this._user.set(updated);
          localStorage.setItem(USER_KEY, JSON.stringify(updated));
        }
        this.refreshAvatar();
      }),
    );
  }

  private refreshAvatar(): void {
    const userId = this._user()?.userId;
    if (!userId) {
      return;
    }
    this.http
      .get(`${environment.apiUrl}/auth/users/${userId}/avatar`, { responseType: 'blob' })
      .subscribe({
        next: (blob) => {
          this.clearAvatarUrl();
          this._avatarUrl.set(URL.createObjectURL(blob));
        },
        error: () => this.clearAvatarUrl(),
      });
  }

  private clearAvatarUrl(): void {
    const existing = this._avatarUrl();
    if (existing) {
      URL.revokeObjectURL(existing);
    }
    this._avatarUrl.set(null);
  }

  private setSession(result: AuthResult): void {
    const user: CurrentUser = {
      userId: result.userId,
      fullName: result.fullName,
      email: result.email,
      role: result.role,
      roleDisplayName: result.roleDisplayName ?? '',
      permissions: result.permissions ?? [],
      hasAvatar: result.hasAvatar,
    };
    localStorage.setItem(TOKEN_KEY, result.token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    this._user.set(user);
    if (user.hasAvatar) {
      this.refreshAvatar();
    } else {
      this.clearAvatarUrl();
    }
  }

  private loadUser(): CurrentUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }
    try {
      const user = JSON.parse(raw) as CurrentUser;
      // جلسات قديمة محفوظة قبل نظام الصلاحيات
      user.permissions ??= [];
      return user;
    } catch {
      return null;
    }
  }
}
