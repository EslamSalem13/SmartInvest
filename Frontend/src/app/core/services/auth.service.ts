import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthResult,
  ChangePasswordRequest,
  CurrentUser,
  LoginRequest,
  ResetPasswordRequest,
  Roles,
  UpdateProfileRequest,
  UserProfile,
} from '../models/auth.models';

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
  readonly isManager = computed(
    () => this._user()?.role === Roles.PlanningManager || this.isSuperAdmin(),
  );

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

  /** المسار الافتراضي بعد تسجيل الدخول حسب الدور */
  homeRouteForRole(role: string | null): string {
    if (role === Roles.PlanningManager || role === Roles.SuperAdmin) {
      return '/app/dashboard';
    }
    return '/app/projects';
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

  getProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${environment.apiUrl}/auth/me`).pipe(
      tap((profile) => this.updateCachedUser(profile)),
    );
  }

  updateProfile(request: UpdateProfileRequest): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${environment.apiUrl}/auth/me`, request).pipe(
      tap((profile) => this.updateCachedUser(profile)),
    );
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/change-password`, request);
  }

  deleteAvatar(): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/auth/me/avatar`).pipe(
      tap(() => {
        const current = this._user();
        if (current) {
          const updated = { ...current, hasAvatar: false };
          this._user.set(updated);
          localStorage.setItem(USER_KEY, JSON.stringify(updated));
        }
        this.clearAvatarUrl();
      }),
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/forgot-password`, { email });
  }

  resetPassword(request: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/reset-password`, request);
  }

  refreshAvatar(): void {
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

  private updateCachedUser(profile: UserProfile): void {
    const current = this._user();
    if (!current) {
      return;
    }

    const updated: CurrentUser = {
      ...current,
      fullName: profile.fullName,
      email: profile.email,
      hasAvatar: profile.hasAvatar,
    };
    this._user.set(updated);
    localStorage.setItem(USER_KEY, JSON.stringify(updated));

    if (profile.hasAvatar && !this._avatarUrl()) {
      this.refreshAvatar();
    }
    if (!profile.hasAvatar) {
      this.clearAvatarUrl();
    }
  }

  private loadUser(): CurrentUser | null {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as CurrentUser;
    } catch {
      return null;
    }
  }
}
