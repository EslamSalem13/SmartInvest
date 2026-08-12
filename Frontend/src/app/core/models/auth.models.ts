export const Roles = {
  SuperAdmin: 'SuperAdmin',
  PlanningManager: 'PlanningManager',
  PlanningEmployee: 'PlanningEmployee',
} as const;

export type AppRole = (typeof Roles)[keyof typeof Roles];

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface AuthResult {
  token: string;
  expiresAt: string;
  userId: string;
  fullName: string;
  email: string;
  role: string;
  hasAvatar: boolean;
}

export interface CurrentUser {
  userId: string;
  fullName: string;
  email: string;
  role: string;
  hasAvatar: boolean;
}

export interface UserProfile {
  id: string;
  fullName: string;
  userName: string;
  email: string;
  phoneNumber: string | null;
  role: string;
  isActive: boolean;
  hasAvatar: boolean;
  createdAt: string;
}

export interface UpdateProfileRequest {
  fullName: string;
  email: string;
  phoneNumber: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}
