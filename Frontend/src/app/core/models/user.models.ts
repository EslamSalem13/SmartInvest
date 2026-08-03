export interface AppUser {
  id: string;
  fullName: string;
  userName: string;
  email: string;
  phoneNumber: string | null;
  role: string;
  roleDisplayName: string;
  isActive: boolean;
  hasAvatar: boolean;
  createdAt: string;
}

export interface CreateEmployee {
  fullName: string;
  userName: string;
  email: string;
  phoneNumber?: string | null;
  password: string;
  role: string;
}
